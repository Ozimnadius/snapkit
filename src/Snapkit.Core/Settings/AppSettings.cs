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
    bool SnippingHintShown = false,
    // Клавиши снимка через запятую: «PrintScreen, Ctrl+F12». Окно настроек клавиш — в этапе 4
    string CaptureHotkeys = "PrintScreen",
    // Свои цвета разметки через запятую, новые первыми, не больше 8
    string CustomColors = "");
