using Snapkit.Core.Input;
using Snapkit.Core.Settings;

namespace Snapkit.Core.Tests;

public class HotkeyRulesTests
{
    private const string NeedModifier = "Добавьте Ctrl, Alt или Shift";

    [Fact]
    public void Check_WithCtrl_IsFine() => Assert.Null(HotkeyRules.Check(new Hotkey(HotkeyMods.Ctrl, 'S')));

    [Fact]
    public void Check_LetterAlone_NeedsModifier() => Assert.Equal(NeedModifier, HotkeyRules.Check(new Hotkey(HotkeyMods.None, 'S')));

    [Fact]
    public void Check_PrintScreenAndF5Alone_AreFine()
    {
        Assert.Null(HotkeyRules.Check(new Hotkey(HotkeyMods.None, Vk.Snapshot)));
        Assert.Null(HotkeyRules.Check(new Hotkey(HotkeyMods.None, 0x74)));
    }

    [Fact]
    public void Check_F13Alone_NeedsModifier() => Assert.Equal(NeedModifier, HotkeyRules.Check(new Hotkey(HotkeyMods.None, 0x7C)));

    [Fact]
    public void Check_WinOnly_NeedsModifier() => Assert.Equal(NeedModifier, HotkeyRules.Check(new Hotkey(HotkeyMods.Win, 'S')));

    [Fact]
    public void Check_UnsupportedKey_CannotBeAssigned()
        => Assert.Equal("Эту клавишу назначить нельзя", HotkeyRules.Check(new Hotkey(HotkeyMods.Ctrl, 0x25)));

    [Fact]
    public void CheckAll_Duplicate_MarksSecond()
    {
        var ctrlF12 = new Hotkey(HotkeyMods.Ctrl, 0x7B);
        Assert.Equal(new string?[] { null, null, "Это сочетание уже назначено", null },
            HotkeyRules.CheckAll([ctrlF12, null, ctrlF12, null]));
    }

    [Fact]
    public void HotkeyMap_BuildsFromSettings()
    {
        var settings = new AppSettings() with
        {
            CaptureHotkeys = "PrintScreen, Ctrl+F12",
            FullScreenHotkeys = "Ctrl+PrintScreen",
            FullScreenHotkeysEnabled = true,
        };
        var map = HotkeyRules.BuildMap(settings);
        Assert.Equal(3, map.Count);
        Assert.Equal(KeyAction.CaptureRegion, map[new Hotkey(HotkeyMods.None, Vk.Snapshot)]);
        Assert.Equal(KeyAction.CaptureRegion, map[new Hotkey(HotkeyMods.Ctrl, 0x7B)]);
        Assert.Equal(KeyAction.CaptureFullScreen, map[new Hotkey(HotkeyMods.Ctrl, Vk.Snapshot)]);

        // Выключенный снимок экрана не попадает
        Assert.Equal(2, HotkeyRules.BuildMap(settings with { FullScreenHotkeysEnabled = false }).Count);
        // Выключенный снимок области и пустой экран — ни одной клавиши
        Assert.Empty(HotkeyRules.BuildMap(new AppSettings() with { CaptureHotkeysEnabled = false }));
    }
}
