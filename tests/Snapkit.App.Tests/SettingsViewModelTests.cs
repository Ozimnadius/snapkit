using Snapkit.App.Settings;
using Snapkit.Core;
using Snapkit.Core.Input;
using Snapkit.Core.Settings;

namespace Snapkit.App.Tests;

public class SettingsViewModelTests
{
    private static readonly Hotkey PrintScreen = new(HotkeyMods.None, Vk.Snapshot);
    private static readonly Hotkey CtrlF12 = new(HotkeyMods.Ctrl, 0x7B);
    private static readonly AppSettings Stage3 = new AppSettings() with { CaptureHotkeys = "PrintScreen, Ctrl+F12" };

    [Fact]
    public void Build_KeepsUntouchedFields()
    {
        // Окно не касается места ленты, своих цветов и прежней области — они остаются как были
        var current = Stage3 with { StripLeft = 100, CustomColors = "#112233", LastRegion = new RectI(1, 2, 3, 4) };
        var built = new SettingsViewModel(current, "0.3.1").Build(current);
        Assert.Equal((100d, "#112233", new RectI(1, 2, 3, 4)), (built.StripLeft!.Value, built.CustomColors, built.LastRegion!.Value));
    }

    [Fact]
    public void Stage3Keys_ShownInTwoFields()
    {
        var model = new SettingsViewModel(Stage3, "0.3.1");
        Assert.Equal((PrintScreen, CtrlF12), (model.Region1, model.Region2));
        Assert.Null(model.Full1);
    }

    [Fact]
    public void Duplicate_BlocksSave()
    {
        var model = new SettingsViewModel(Stage3, "0.3.1") { FullScreenEnabled = true, Full1 = CtrlF12 };
        Assert.Equal("Это сочетание уже назначено", model.HotkeyErrors[2]);
        Assert.False(model.CanSave);
    }

    [Fact]
    public void SuggestFullScreen_SetsCtrlPrintScreen()
    {
        var model = new SettingsViewModel(Stage3, "0.3.1");
        model.SuggestFullScreen();
        Assert.Equal(new Hotkey(HotkeyMods.Ctrl, Vk.Snapshot), model.Full1);
        Assert.True(model.FullScreenEnabled);
        Assert.True(model.CanSave);
    }

    [Fact]
    public void Build_WritesHotkeysAsText()
    {
        var model = new SettingsViewModel(Stage3, "0.3.1");
        model.SuggestFullScreen();
        var built = model.Build(Stage3);
        Assert.Equal(("Print Screen, Ctrl+F12", "Ctrl+Print Screen", true), (built.CaptureHotkeys, built.FullScreenHotkeys, built.FullScreenHotkeysEnabled));
    }

    [Fact]
    public void RegionEnabledWithoutKeys_BlocksSave()
    {
        var model = new SettingsViewModel(Stage3, "0.3.1") { Region1 = null, Region2 = null };
        Assert.Equal("Назначьте сочетание или снимите галочку", model.HotkeyErrors[0]);
        Assert.False(model.CanSave);
        model.CaptureEnabled = false;
        Assert.True(model.CanSave);
    }

    [Fact]
    public void Build_DefaultFolderStaysEmpty()
    {
        // Папка по умолчанию не записывается в файл — пусть так и остаётся «Изображения\Snapkit»
        var model = new SettingsViewModel(Stage3, "0.3.1");
        Assert.Equal("", model.Build(Stage3).LastSaveFolder);
        model.SaveFolder = @"D:\Снимки";
        Assert.Equal(@"D:\Снимки", model.Build(Stage3).LastSaveFolder);
    }

    [Fact]
    public void Build_TakesGeneralSaveAndLook()
    {
        var model = new SettingsViewModel(Stage3, "0.3.1")
        {
            LaunchAtStartup = false, ShowNotifications = false, ClearStripAfterPaste = true, ProposeLastRegion = true,
            ShowCursor = true, AutoUpdate = false, AutoSave = true, SaveFormat = SaveFormat.Jpeg, JpegQuality = 70,
            Theme = AppTheme.Light, Accent = "#16A34A",
        };
        var built = model.Build(Stage3);
        Assert.Equal((false, false, true, true, true, false), (built.LaunchAtStartup, built.ShowNotifications, built.ClearStripAfterPaste,
            built.ProposeLastRegion, built.ShowCursor, built.AutoUpdate));
        Assert.Equal((true, SaveFormat.Jpeg, 70, AppTheme.Light, "#16A34A"), (built.AutoSave, built.SaveFormat, built.JpegQuality, built.Theme, built.AccentColor));
    }

    [Fact]
    public void Build_KeepsChangesMadeWhileOpen()
    {
        // Пока окно открыто, ленту передвинули, сделали снимок, отложили обновление, выбрали папку в «Сохранить снимок…»
        var model = new SettingsViewModel(Stage3, "0.3.1");
        var latest = Stage3 with
        {
            StripLeft = 500, LastRegion = new RectI(5, 6, 70, 80), SnoozedUpdateVersion = "0.4.0", LastSaveFolder = @"D:\Другое",
        };
        var built = model.Build(latest);
        Assert.Equal((500d, new RectI(5, 6, 70, 80), "0.4.0", @"D:\Другое"),
            (built.StripLeft!.Value, built.LastRegion!.Value, built.SnoozedUpdateVersion, built.LastSaveFolder));
    }
}
