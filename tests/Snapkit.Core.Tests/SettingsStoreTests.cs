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
    public void SaveThenLoad_RoundTrips()
    {
        var saved = new AppSettings(LaunchAtStartup: false, ClearStripAfterPaste: true, StripLeft: 100.5, StripTop: 42,
            AccentColor: "#8E24AA", SnippingHintShown: true);
        new SettingsStore(FilePath).Save(saved);
        var (loaded, wasReset) = new SettingsStore(FilePath).Load();
        Assert.Equal(saved, loaded);
        Assert.False(wasReset);
    }
}
