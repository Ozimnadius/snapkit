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
    string CustomColors = "",
    // Последняя папка «Сохранить снимок…» и «Сохранить пакет…»; пусто — «Изображения\Snapkit»
    string LastSaveFolder = "",
    // Показывать ленту при запуске и после снимков; выключена — после снимка короткое уведомление
    bool ShowStrip = true,
    // Лента свёрнута в капсулу — и после перезапуска появится капсулой; где стоит капсула
    bool StripCollapsed = false,
    double? CapsuleLeft = null,
    double? CapsuleTop = null,
    // Звуки затвора, ленты и вставки (громкость — в окне настроек этапа 4)
    bool PlaySounds = true);
