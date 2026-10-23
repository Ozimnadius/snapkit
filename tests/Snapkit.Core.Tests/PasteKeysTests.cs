using Snapkit.Core.Input;

namespace Snapkit.Core.Tests;

public class PasteKeysTests
{
    [Fact]
    public void CtrlV_NoModifiers()
        => Assert.Equal(
            new KeyStroke[] { new(Vk.LCtrl, false), new(Vk.V, false), new(Vk.V, true), new(Vk.LCtrl, true) },
            PasteKeys.CtrlV(Mod.None));

    [Fact]
    public void CtrlV_ShiftHeld_ReleasesShiftFirst()
        => Assert.Equal(
            new KeyStroke[] { new(Vk.LShift, true), new(Vk.LCtrl, false), new(Vk.V, false), new(Vk.V, true), new(Vk.LCtrl, true) },
            PasteKeys.CtrlV(Mod.LShift));

    [Fact]
    public void CtrlV_BothShiftsAndAlt_ReleasesEachSide()
        => Assert.Equal(
            new KeyStroke[]
            {
                new(Vk.LShift, true), new(Vk.RShift, true), new(Vk.RAlt, true),
                new(Vk.LCtrl, false), new(Vk.V, false), new(Vk.V, true), new(Vk.LCtrl, true),
            },
            PasteKeys.CtrlV(Mod.LShift | Mod.RShift | Mod.RAlt));

    [Fact]
    public void CtrlV_CtrlHeld_OnlyV()
        => Assert.Equal(new KeyStroke[] { new(Vk.V, false), new(Vk.V, true) }, PasteKeys.CtrlV(Mod.RCtrl));

    [Fact]
    public void Restore_PhysicalShift_PressesItBack()
        => Assert.Equal(new KeyStroke[] { new(Vk.LShift, false) }, PasteKeys.Restore(Mod.LShift, Mod.None));

    [Fact]
    public void Restore_ShiftAlreadyLogicallyDown_DoesNothing()
        => Assert.Empty(PasteKeys.Restore(Mod.LShift, Mod.LShift));

    [Fact]
    public void Restore_StuckCtrl_ReleasesIt()
        => Assert.Equal(new KeyStroke[] { new(Vk.LCtrl, true) }, PasteKeys.Restore(Mod.None, Mod.LCtrl));

    [Fact]
    public void Restore_PhysicallyHeldCtrl_IsKept()
        => Assert.Empty(PasteKeys.Restore(Mod.RCtrl, Mod.RCtrl));
}
