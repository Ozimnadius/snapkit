namespace Snapkit.Core.Input;

/// <summary>
/// Решает по каждому событию клавиатуры: пропустить его, подавить и/или запустить действие.
/// Process вызывается с потока хука (под замком вызывающего) и должен работать мгновенно.
/// Logical, Physical, PasteInProgress и EscapePressedDuringPaste читаются и пишутся с других потоков.
/// </summary>
public sealed class KeyRouter(Func<PasteContext> pasteContext)
{
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

    // Клавиши, чьё нажатие подавлено: их отпускание тоже нужно подавить.
    private readonly HashSet<int> _suppressedDown = [];

    public IReadOnlyDictionary<Hotkey, KeyAction> Hotkeys { get => _hotkeys; set => _hotkeys = value; }
    public bool PasteInProgress { get => _pasteInProgress; set => _pasteInProgress = value; }
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

        return input.IsDown ? OnDown(input.Vk) : OnUp(input.Vk);
    }

    private KeyDecision OnDown(int vk)
    {
        var logical = Logical;

        if (_pasteInProgress)
        {
            if (vk == Vk.Escape) { _escapePressed = true; return Pass; }
            // Повторы Ctrl+V от удержания клавиш не должны вставить что-то лишнее.
            if (vk == Vk.V && ModKeys.HasCtrl(logical)) { _suppressedDown.Add(vk); return Swallow; }
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
        if (_pasteInProgress && vk == Vk.V && ModKeys.HasCtrl(logical)) return Swallow;

        // Print Screen иногда приходит только отпусканием, без нажатия.
        if (vk == Vk.Snapshot && _hotkeys.TryGetValue(new Hotkey(ModKeys.Summary(logical), vk), out var action))
            return new KeyDecision(true, action);

        return Pass;
    }

    private static int Apply(int state, Mod mod, bool down) => down ? state | (int)mod : state & ~(int)mod;
}
