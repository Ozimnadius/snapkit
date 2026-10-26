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
    public void SaveThenLoad_RoundTrips()
    {
        var saved = new AppSettings(LaunchAtStartup: false, ClearStripAfterPaste: true, StripLeft: 100.5, StripTop: 42,
            AccentColor: "#8E24AA", SnippingHintShown: true, CaptureHotkeys: "PrintScreen, Ctrl+F12", CustomColors: "#112233,#445566");
        new SettingsStore(FilePath).Save(saved);
        var (loaded, wasReset) = new SettingsStore(FilePath).Load();
        Assert.Equal(saved, loaded);
        Assert.False(wasReset);
    }
}
