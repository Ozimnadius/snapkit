using Snapkit.Core.Settings;

namespace Snapkit.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "snapkit-tests-" + Guid.NewGuid());
    private string FilePath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Load_Missing_ReturnsDefaults()
    {
        var (settings, wasReset) = new SettingsStore(FilePath).Load();
        Assert.Equal(new AppSettings(), settings);
        Assert.False(wasReset);
    }

    [Fact]
    public void Load_Corrupt_ReturnsDefaultsAndWasReset()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{сломано");
        var (settings, wasReset) = new SettingsStore(FilePath).Load();
        Assert.Equal(new AppSettings(), settings);
        Assert.True(wasReset);
    }

    [Fact]
    public void Load_Corrupt_MovesFileAside_SoNextStartIsQuiet()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{сломано");
        var store = new SettingsStore(FilePath);
        Assert.True(store.Load().WasReset);
        Assert.False(store.Load().WasReset); // при следующем запуске сообщения уже нет
        Assert.True(File.Exists(Path.Combine(_dir, "settings.bad.json")));
    }

    [Fact]
    public void Load_OldFileWithoutHotkeys_UsesPrintScreen()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "launchAtStartup": true, "stripTopmost": false }""");
        var (settings, wasReset) = new SettingsStore(FilePath).Load();
        Assert.False(wasReset);
        Assert.False(settings.StripTopmost);
        Assert.Equal("PrintScreen", settings.CaptureHotkeys);
    }

    [Fact]
    public void Load_OldSettingsWithoutCustomColors_IsEmpty()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "launchAtStartup": true, "captureHotkeys": "PrintScreen, Ctrl+F12" }""");
        Assert.Equal("", new SettingsStore(FilePath).Load().Settings.CustomColors);
    }

    [Fact]
    public void Load_OldSettingsWithoutLastSaveFolder_IsEmpty()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "launchAtStartup": true, "customColors": "#00E505" }""");
        Assert.Equal("", new SettingsStore(FilePath).Load().Settings.LastSaveFolder);
    }

    [Fact]
    public void Load_OldSettingsWithoutShowStrip_ShowsStrip()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "launchAtStartup": true, "customColors": "#00E505" }""");
        Assert.True(new SettingsStore(FilePath).Load().Settings.ShowStrip);
    }

    [Fact]
    public void ShowStrip_RoundTrip()
    {
        new SettingsStore(FilePath).Save(new AppSettings(ShowStrip: false, LastSaveFolder: @"D:\Снимки"));
        var loaded = new SettingsStore(FilePath).Load().Settings;
        Assert.Equal((false, @"D:\Снимки"), (loaded.ShowStrip, loaded.LastSaveFolder));
    }

    [Fact]
    public void Load_OldSettingsWithoutCollapsed_IsExpanded()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "showStrip": true, "lastSaveFolder": "" }""");
        var settings = new SettingsStore(FilePath).Load().Settings;
        Assert.Equal((false, (double?)null, (double?)null), (settings.StripCollapsed, settings.CapsuleLeft, settings.CapsuleTop));
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var saved = new AppSettings(LaunchAtStartup: false, ClearStripAfterPaste: true, StripLeft: 100.5, StripTop: 42,
            AccentColor: "#8E24AA", SnippingHintShown: true, CaptureHotkeys: "PrintScreen, Ctrl+F12", CustomColors: "#112233,#445566");
        new SettingsStore(FilePath).Save(saved);
        var (loaded, wasReset) = new SettingsStore(FilePath).Load();
        Assert.Equal(saved, loaded);
        Assert.False(wasReset);
    }

    [Fact]
    public void Load_Stage3Settings_KeepsHotkeysAndDefaults()
    {
        // Файл этапа 3: клавиши пользователя переносятся, новые поля — по умолчанию
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "captureHotkeys": "PrintScreen, Ctrl+F12", "showStrip": true }""");
        var (settings, wasReset) = new SettingsStore(FilePath).Load();
        Assert.False(wasReset);
        Assert.Equal("PrintScreen, Ctrl+F12", settings.CaptureHotkeys);
        Assert.Equal("", settings.FullScreenHotkeys);
        Assert.True(settings.CaptureHotkeysEnabled);
        Assert.True(settings.AutoUpdate);
        Assert.Equal(AppTheme.Dark, settings.Theme);
        Assert.Equal(90, settings.JpegQuality);
        Assert.Equal(SaveFormat.Png, settings.SaveFormat);
    }

    [Fact]
    public void SaveThenLoad_NewFields_RoundTrip()
    {
        var original = new AppSettings() with
        {
            FullScreenHotkeys = "Ctrl+PrintScreen",
            CaptureHotkeysEnabled = false,
            FullScreenHotkeysEnabled = true,
            ProposeLastRegion = true,
            LastRegion = new RectI(10, 20, 300, 200),
            ShowCursor = true,
            AutoUpdate = false,
            SnoozedUpdateVersion = "0.3.2",
            SnoozedUpdateUntil = new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.FromHours(3)),
            AutoSave = true,
            SaveFormat = SaveFormat.Jpeg,
            JpegQuality = 75,
            Theme = AppTheme.System,
        };
        var store = new SettingsStore(FilePath);
        Directory.CreateDirectory(_dir);
        store.Save(original);
        Assert.Equal(original, store.Load().Settings);
    }
}
