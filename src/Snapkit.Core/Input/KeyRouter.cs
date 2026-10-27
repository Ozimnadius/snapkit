namespace Snapkit.Core.Input;

/// <summary>
/// Решает по каждому событию клавиатуры: пропустить его, подавить и/или запустить действие.
/// Process вызывается с потока хука (под замком вызывающего) и должен работать мгновенно.
/// Logical, Physical, PasteInProgress и EscapePressedDuringPaste читаются и пишутся с других потоков.
/// </summary>
/// <param name="actualModifiers">
/// Модификаторы, нажатые с точки зрения Windows (GetAsyncKeyState). Нужны для сверки:
/// на экране блокировки (Win+L, Ctrl+Alt+Del, запрос UAC) хук не видит отпускание клавиш.
/// </param>
/// <param name="clock">Часы — для паузы после вставки; в тестах подменяются.</param>
public sealed class KeyRouter(Func<PasteContext> pasteContext, Func<Mod>? actualModifiers = null, Func<DateTime>? clock = null)
{
    /// <summary>
    /// Сколько ещё глотать Ctrl+V после конца вставки: колесо мыши с AutoHotkey шлёт
    /// несколько Ctrl+V подряд, и последние не должны вставить текст ещё раз.
    /// </summary>
    public static readonly TimeSpan AfterPasteGrace = TimeSpan.FromMilliseconds(400);

    private static readonly KeyDecision Pass = new(false, KeyAction.None);
    private static readonly KeyDecision Swallow = new(true, KeyAction.None);

    public static IReadOnlyDictionary<Hotkey, KeyAction> DefaultHotkeys { get; } = new Dictionary<Hotkey, KeyAction>
    {
        [new Hotkey(HotkeyMods.None, Vk.Snapshot)] = KeyAction.CaptureRegion,
    };

    private volatile IReadOnlyDictionary<Hotkey, KeyAction> _hotkeys = DefaultHotkeys;
    private volatile int _logical;
    private volatile int _physical;
    private volatile bool _pasteInProgress;
    private volatile bool _escapePressed;
    private long _graceUntilTicks;

    // Клавиши, чьё нажатие подавлено: их отпускание тоже нужно подавить.
    private readonly HashSet<int> _suppressedDown = [];

    public IReadOnlyDictionary<Hotkey, KeyAction> Hotkeys { get => _hotkeys; set => _hotkeys = value; }

    private volatile Action<Hotkey>? _recordHotkey;

    /// <summary>
    /// Запись сочетания в поле настроек: пока задано, любая не-модификаторная клавиша (вместе с зажатыми
    /// модификаторами) отдаётся сюда и глотается; снимки и перехват Ctrl+V не срабатывают.
    /// Вызывается с потока хука клавиатуры.
    /// </summary>
    public Action<Hotkey>? RecordHotkey { get => _recordHotkey; set => _recordHotkey = value; }
    public bool PasteInProgress
    {
        get => _pasteInProgress;
        set
        {
            if (_pasteInProgress && !value) Volatile.Write(ref _graceUntilTicks, (Now() + AfterPasteGrace).Ticks);
            _pasteInProgress = value;
        }
    }
    public bool EscapePressedDuringPaste { get => _escapePressed; set => _escapePressed = value; }

    /// <summary>Модификаторы с точки зрения Windows: все события, включая программные.</summary>
    public Mod Logical => (Mod)_logical;

    /// <summary>Модификаторы, реально зажатые на клавиатуре.</summary>
    public Mod Physical => (Mod)_physical;

    public KeyDecision Process(KeyInput input)
    {
        var mod = ModKeys.FromVk(input.Vk);
        if (mod != Mod.None)
        {
            _logical = Apply(_logical, mod, input.IsDown);
            if (!input.IsInjected) _physical = Apply(_physical, mod, input.IsDown);
            return Pass;
        }

        // Свои программные нажатия (вставка пачки) никогда не перехватываем.
        if (input.IsOwn) return Pass;

        // Перед решением по обычной клавише сверяем модификаторы с Windows
        Resync();

        return input.IsDown ? OnDown(input.Vk) : OnUp(input.Vk);
    }

    private KeyDecision OnDown(int vk)
    {
        var logical = Logical;

        if (_recordHotkey is { } record)
        {
            _suppressedDown.Add(vk);
            record(new Hotkey(ModKeys.Summary(logical), vk));
            return Swallow;
        }

        if (_pasteInProgress && vk == Vk.Escape)
        {
            _escapePressed = true;
            return Pass;
        }
        // Повторы Ctrl+V во время вставки и сразу после неё не должны вставить что-то лишнее
        if ((_pasteInProgress || InGrace()) && vk == Vk.V && ModKeys.HasCtrl(logical))
        {
            _suppressedDown.Add(vk);
            return Swallow;
        }

        if (_hotkeys.TryGetValue(new Hotkey(ModKeys.Summary(logical), vk), out var action))
        {
            _suppressedDown.Add(vk);
            return new KeyDecision(true, action);
        }

        if (vk == Vk.V && ModKeys.HasCtrl(logical) && !ModKeys.HasAlt(logical) && !ModKeys.HasWin(logical)
            && pasteContext().AllowsIntercept)
        {
            _suppressedDown.Add(vk);
            return new KeyDecision(true, KeyAction.PasteBatch);
        }

        return Pass;
    }

    private KeyDecision OnUp(int vk)
    {
        if (_suppressedDown.Remove(vk)) return Swallow;

        var logical = Logical;
        // Print Screen иногда приходит только отпусканием — при записи сочетания тоже
        if (vk == Vk.Snapshot && _recordHotkey is { } record)
        {
            record(new Hotkey(ModKeys.Summary(logical), vk));
            return Swallow;
        }
        if (_pasteInProgress && vk == Vk.V && ModKeys.HasCtrl(logical)) return Swallow;

        // Print Screen иногда приходит только отпусканием, без нажатия.
        if (vk == Vk.Snapshot && _hotkeys.TryGetValue(new Hotkey(ModKeys.Summary(logical), vk), out var action))
            return new KeyDecision(true, action);

        return Pass;
    }

    /// <summary>
    /// Привести модификаторы к состоянию Windows. Бит, который мы считали нажатым, а Windows —
    /// нет, — это пропущенное отпускание: он убирается и из Physical. Shift, отпущенный
    /// программой (AutoHotkey) при зажатой руке, мы и так считаем отпущенным, поэтому он
    /// в Physical остаётся.
    /// </summary>
    public void Resync()
    {
        if (actualModifiers is null) return;
        var actual = (int)actualModifiers();
        var missedReleases = _logical & ~actual;
        _physical &= ~missedReleases;
        _logical = actual;
    }

    private DateTime Now() => clock?.Invoke() ?? DateTime.UtcNow;

    private bool InGrace() => Now().Ticks < Volatile.Read(ref _graceUntilTicks);

    private static int Apply(int state, Mod mod, bool down) => down ? state | (int)mod : state & ~(int)mod;
}
