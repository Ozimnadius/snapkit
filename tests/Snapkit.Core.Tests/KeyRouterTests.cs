using Snapkit.Core.Input;

namespace Snapkit.Core.Tests;

public class KeyRouterTests
{
    private static readonly PasteContext Ready = new(HasUnsent: true, PackageIsCurrent: true, ForegroundIsOwn: false);
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
}
