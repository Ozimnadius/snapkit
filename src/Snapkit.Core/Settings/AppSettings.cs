namespace Snapkit.Core.Settings;

/// <summary>Настройки программы (этап 1 — без окна настроек, только файл).</summary>
public sealed record AppSettings(
    bool LaunchAtStartup = true,
    bool ShowNotifications = true,
    bool ClearStripAfterPaste = false,
    bool StripTopmost = true,
    double? StripLeft = null,
    double? StripTop = null,
    string AccentColor = "#2F7BF5",
    bool SnippingHintShown = false);
