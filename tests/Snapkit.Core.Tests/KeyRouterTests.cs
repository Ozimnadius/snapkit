using Snapkit.Core.Input;

namespace Snapkit.Core.Tests;

public class KeyRouterTests
{
    private static readonly PasteContext Ready = new(HasPasteContent: true, PackageIsCurrent: true, ForegroundIsOwn: false);
    private static readonly KeyDecision Pass = new(false, KeyAction.None);
    private static readonly KeyDecision Swallow = new(true, KeyAction.None);

    private static KeyInput Down(int vk, bool inj = false) => new(vk, IsDown: true, IsInjected: inj, IsOwn: false);
    private static KeyInput Up(int vk, bool inj = false) => new(vk, IsDown: false, IsInjected: inj, IsOwn: false);

    [Fact]
    public void PhysicalCtrlV_IsIntercepted()
    {
        var r = new KeyRouter(() => Ready);
        r.Process(Down(Vk.LCtrl));
        Assert.Equal(new KeyDecision(true, KeyAction.PasteBatch), r.Process(Down(Vk.V)));
        Assert.Equal(Swallow, r.Process(Up(Vk.V)));
        Assert.Equal(Pass, r.Process(Up(Vk.LCtrl)));
    }

    [Fact]
    public void Ahk_ShiftWheelUp_IsIntercepted()
    {   // AutoHotkey: Shift зажат рукой, Send ^v сам отпускает и возвращает Shift
        var r = new KeyRouter(() => Ready);
        r.Process(Down(Vk.LShift));
        r.Process(Up(Vk.LShift, inj: true));
        r.Process(Down(Vk.LCtrl, inj: true));
        Assert.Equal(KeyAction.PasteBatch, r.Process(Down(Vk.V, inj: true)).Action);
        Assert.True(r.Process(Up(Vk.V, inj: true)).Suppress);
        r.Process(Up(Vk.LCtrl, inj: true));
        r.Process(Down(Vk.LShift, inj: true));
        Assert.Equal(Mod.LShift, r.Physical);
        Assert.Equal(Mod.LShift, r.Logical);
    }

    [Fact]
    public void CtrlShiftV_IsIntercepted()
    {
        var r = new KeyRouter(() => Ready);
        r.Process(Down(Vk.LShift));
        r.Process(Down(Vk.LCtrl));
        Assert.Equal(new KeyDecision(true, KeyAction.PasteBatch), r.Process(Down(Vk.V)));
    }

    [Fact]
    public void RightCtrlV_IsIntercepted()
    {
        var r = new KeyRouter(() => Ready);
        r.Process(Down(Vk.RCtrl));
        Assert.Equal(KeyAction.PasteBatch, r.Process(Down(Vk.V)).Action);
    }

    [Fact]
    public void CtrlAltV_IsNotIntercepted()
    {
        var r = new KeyRouter(() => Ready);
        r.Process(Down(Vk.LCtrl));
        r.Process(Down(Vk.LAlt));
        Assert.Equal(Pass, r.Process(Down(Vk.V)));
    }

    [Fact]
    public void CtrlWinV_IsNotIntercepted()
    {
        var r = new KeyRouter(() => Ready);
        r.Process(Down(Vk.LCtrl));
        r.Process(Down(Vk.LWin));
        Assert.Equal(Pass, r.Process(Down(Vk.V)));
    }

    [Fact]
    public void PlainV_IsNotIntercepted()
    {
        var r = new KeyRouter(() => Ready);
        Assert.Equal(Pass, r.Process(Down(Vk.V)));
        Assert.Equal(Pass, r.Process(Up(Vk.V)));
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void CtrlV_PassesThrough_WhenContextNotReady(bool unsent, bool current, bool own)
    {
        var r = new KeyRouter(() => new PasteContext(unsent, current, own));
        r.Process(Down(Vk.LCtrl));
        Assert.Equal(Pass, r.Process(Down(Vk.V)));
        Assert.Equal(Pass, r.Process(Up(Vk.V)));
    }

    [Fact]
    public void PasteContext_IsAskedOnlyForCtrlV()
    {
        var asked = 0;
        var r = new KeyRouter(() => { asked++; return Ready; });
        r.Process(Down(Vk.V));
        r.Process(Down(0x41)); // A
        r.Process(Down(Vk.LCtrl));
        Assert.Equal(0, asked);
        r.Process(Down(Vk.V));
        Assert.Equal(1, asked);
    }

    [Fact]
    public void OwnInput_IsNeverIntercepted_ButUpdatesLogical()
    {
        var r = new KeyRouter(() => Ready);
        Assert.Equal(Pass, r.Process(new KeyInput(Vk.LCtrl, IsDown: true, IsInjected: true, IsOwn: true)));
        Assert.Equal(Pass, r.Process(new KeyInput(Vk.V, IsDown: true, IsInjected: true, IsOwn: true)));
        Assert.Equal(Mod.LCtrl, r.Logical);
        Assert.Equal(Mod.None, r.Physical);
    }

    [Fact]
    public void WhilePasting_CtrlVRepeatsAreSwallowed()
    {
        var r = new KeyRouter(() => Ready) { PasteInProgress = true };
        r.Process(Down(Vk.LCtrl));
        Assert.Equal(Swallow, r.Process(Down(Vk.V)));
        Assert.Equal(Swallow, r.Process(Up(Vk.V)));
    }

    [Fact]
    public void WhilePasting_EscapeIsRecorded()
    {
        var r = new KeyRouter(() => Ready) { PasteInProgress = true };
        Assert.Equal(Pass, r.Process(Down(Vk.Escape)));
        Assert.True(r.EscapePressedDuringPaste);
    }

    [Fact]
    public void NotPasting_EscapeIsNotRecorded()
    {
        var r = new KeyRouter(() => Ready);
        r.Process(Down(Vk.Escape));
        Assert.False(r.EscapePressedDuringPaste);
    }

    [Fact]
    public void PrintScreen_DownTriggers_UpSuppressed()
    {
        var r = new KeyRouter(() => Ready);
        Assert.Equal(new KeyDecision(true, KeyAction.CaptureRegion), r.Process(Down(Vk.Snapshot)));
        Assert.Equal(Swallow, r.Process(Up(Vk.Snapshot)));
    }

    [Fact]
    public void PrintScreen_UpWithoutDown_Triggers()
    {
        var r = new KeyRouter(() => Ready);
        Assert.Equal(new KeyDecision(true, KeyAction.CaptureRegion), r.Process(Up(Vk.Snapshot)));
    }

    [Fact]
    public void ShiftPrintScreen_DoesNotMatchPlainHotkey()
    {
        var r = new KeyRouter(() => Ready);
        r.Process(Down(Vk.LShift));
        Assert.Equal(Pass, r.Process(Down(Vk.Snapshot)));
        Assert.Equal(Pass, r.Process(Up(Vk.Snapshot)));
    }

    [Fact]
    public void CustomHotkey_CtrlPrintScreen()
    {
        var r = new KeyRouter(() => Ready)
        {
            Hotkeys = new Dictionary<Hotkey, KeyAction>
            {
                [new Hotkey(HotkeyMods.None, Vk.Snapshot)] = KeyAction.CaptureRegion,
                [new Hotkey(HotkeyMods.Ctrl, Vk.Snapshot)] = KeyAction.CaptureFullScreen,
            },
        };
        r.Process(Down(Vk.RCtrl));
        Assert.Equal(new KeyDecision(true, KeyAction.CaptureFullScreen), r.Process(Down(Vk.Snapshot)));
        Assert.Equal(Swallow, r.Process(Up(Vk.Snapshot)));
    }

    // --- Сверка с Windows: на экране блокировки хук не видит отпускание клавиш ---

    [Fact]
    public void Resync_DropsModifierWhoseReleaseWasMissed()
    {
        var actual = Mod.None;
        var r = new KeyRouter(() => Ready, () => actual);
        r.Process(Down(Vk.LWin)); // Win+L: отпускание Win ушло на экран блокировки
        Assert.Equal(new KeyDecision(true, KeyAction.CaptureRegion), r.Process(Down(Vk.Snapshot)));
        Assert.Equal(Mod.None, r.Logical);
    }

    [Fact]
    public void Resync_AfterCtrlAltDel_PlainVIsNotIntercepted()
    {
        var actual = Mod.None;
        var r = new KeyRouter(() => Ready, () => actual);
        r.Process(Down(Vk.LCtrl));
        r.Process(Down(Vk.LAlt)); // Ctrl+Alt+Del: отпускания не видны
        Assert.Equal(Pass, r.Process(Down(Vk.V)));
    }

    [Fact]
    public void Resync_DropsStalePhysicalModifier()
    {
        var actual = Mod.None;
        var r = new KeyRouter(() => Ready, () => actual);
        r.Process(Down(Vk.LShift)); // отпускание потерялось
        r.Process(Down(0x41));      // любая обычная клавиша — сверка
        Assert.Equal(Mod.None, r.Physical);
    }

    [Fact]
    public void Resync_KeepsPhysicalShiftReleasedByAutoHotkey()
    {
        var actual = Mod.LShift;
        var r = new KeyRouter(() => Ready, () => actual);
        r.Process(Down(Vk.LShift));
        actual = Mod.None;
        r.Process(Up(Vk.LShift, inj: true));
        actual = Mod.LCtrl;
        r.Process(Down(Vk.LCtrl, inj: true));
        Assert.Equal(KeyAction.PasteBatch, r.Process(Down(Vk.V, inj: true)).Action);
        Assert.Equal(Mod.LShift, r.Physical); // Shift всё ещё в руке — его надо будет вернуть
    }

    [Fact]
    public void Resync_Method_TakesActualState()
    {
        var actual = Mod.LCtrl;
        var r = new KeyRouter(() => Ready, () => actual);
        r.Process(Down(Vk.LCtrl));
        actual = Mod.None;
        r.Resync();
        Assert.Equal(Mod.None, r.Logical);
    }

    // --- Пауза после вставки: лишние щелчки колеса с AutoHotkey ---

    [Fact]
    public void AfterPaste_ExtraCtrlVIsBrieflySwallowed()
    {
        var now = DateTime.UnixEpoch;
        var nothingToPaste = new PasteContext(HasPasteContent: false, PackageIsCurrent: false, ForegroundIsOwn: false);
        var r = new KeyRouter(() => nothingToPaste, clock: () => now) { PasteInProgress = true };
        r.PasteInProgress = false; // пачка вставлена
        r.Process(Down(Vk.LCtrl));
        Assert.Equal(Swallow, r.Process(Down(Vk.V)));
        Assert.Equal(Swallow, r.Process(Up(Vk.V)));
        now += KeyRouter.AfterPasteGrace + TimeSpan.FromMilliseconds(1);
        Assert.Equal(Pass, r.Process(Down(Vk.V)));
    }

    [Fact]
    public void CtrlF12_TriggersCapture_WhenConfigured()
    {
        var actual = Mod.None;
        var r = new KeyRouter(() => Ready, () => actual)
        {
            Hotkeys = new Dictionary<Hotkey, KeyAction> { [new Hotkey(HotkeyMods.Ctrl, 0x7B)] = KeyAction.CaptureRegion },
        };
        actual = Mod.LCtrl;
        r.Process(Down(Vk.LCtrl));
        Assert.Equal(new KeyDecision(true, KeyAction.CaptureRegion), r.Process(Down(0x7B)));
        Assert.Equal(Swallow, r.Process(Up(0x7B)));
        actual = Mod.None;
        Assert.Equal(Pass, r.Process(Down(0x7B))); // F12 без Ctrl — обычная клавиша
    }

    [Fact]
    public void Modifiers_AreNeverSuppressed()
    {
        var r = new KeyRouter(() => Ready) { PasteInProgress = true };
        foreach (var vk in new[] { Vk.LShift, Vk.RShift, Vk.LCtrl, Vk.RCtrl, Vk.LAlt, Vk.RAlt, Vk.LWin, Vk.RWin })
        {
            Assert.Equal(Pass, r.Process(Down(vk)));
            Assert.Equal(Pass, r.Process(Up(vk)));
        }
        Assert.Equal(Mod.None, r.Logical);
    }

    [Fact]
    public void Recording_SwallowsPrintScreenWithoutCapture()
    {
        // Окно настроек ждёт сочетание: Print Screen не запускает снимок и не уходит в Windows («Ножницы»)
        var r = new KeyRouter(() => Ready);
        var recorded = new List<Hotkey>();
        r.RecordHotkey = recorded.Add;

        Assert.Equal(Swallow, r.Process(Down(Vk.Snapshot)));
        Assert.Equal(Swallow, r.Process(Up(Vk.Snapshot)));
        r.Process(Down(Vk.LCtrl));
        Assert.Equal(Swallow, r.Process(Down(0x7B)));
        r.Process(Up(0x7B));
        r.Process(Up(Vk.LCtrl));
        // Голый Ctrl — ещё не сочетание
        r.Process(Down(Vk.LCtrl));
        r.Process(Up(Vk.LCtrl));

        Assert.Equal(new[] { new Hotkey(HotkeyMods.None, Vk.Snapshot), new Hotkey(HotkeyMods.Ctrl, 0x7B) }, recorded);

        r.RecordHotkey = null;
        Assert.Equal(new KeyDecision(true, KeyAction.CaptureRegion), r.Process(Down(Vk.Snapshot)));
    }

    [Fact]
    public void Recording_PrintScreenOnlyReleased_IsRecorded()
    {
        // Print Screen иногда приходит только отпусканием
        var r = new KeyRouter(() => Ready);
        var recorded = new List<Hotkey>();
        r.RecordHotkey = recorded.Add;
        Assert.Equal(Swallow, r.Process(Up(Vk.Snapshot)));
        Assert.Equal(new[] { new Hotkey(HotkeyMods.None, Vk.Snapshot) }, recorded);
    }
}
