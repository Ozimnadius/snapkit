namespace Snapkit.Core.Settings;

/// <summary>Настройки программы: файл settings.json, правятся в окне «Настройки» (этап 4).</summary>
public sealed record AppSettings(
    bool LaunchAtStartup = true,
    bool ShowNotifications = true,
    bool ClearStripAfterPaste = false,
    bool StripTopmost = true,
    double? StripLeft = null,
    double? StripTop = null,
    string AccentColor = "#2F7BF5",
    bool SnippingHintShown = false,
    // Клавиши снимка области через запятую: «PrintScreen, Ctrl+F12»; берутся первые два
    string CaptureHotkeys = "PrintScreen",
    // Свои цвета разметки через запятую, новые первыми, не больше 8
    string CustomColors = "",
    // Последняя папка «Сохранить снимок…» и «Сохранить всё…»; пусто — «Изображения\Snapkit»
    string LastSaveFolder = "",
    // Показывать ленту при запуске и после снимков; выключена — после снимка короткое уведомление
    bool ShowStrip = true,
    // Лента свёрнута в капсулу — и после перезапуска появится капсулой; где стоит капсула
    bool StripCollapsed = false,
    double? CapsuleLeft = null,
    double? CapsuleTop = null,
    // Этап 4. Клавиши снимка всего экрана (пусто — не назначено) и галочки «включено» у действий
    string FullScreenHotkeys = "",
    bool CaptureHotkeysEnabled = true,
    bool FullScreenHotkeysEnabled = false,
    // Предлагать прежнюю рамку; последняя снятая область — в физических пикселях экрана
    bool ProposeLastRegion = false,
    RectI? LastRegion = null,
    bool ShowCursor = false,
    // Автообновление; предложенную версию не показываем снова до SnoozedUpdateUntil
    bool AutoUpdate = true,
    string SnoozedUpdateVersion = "",
    DateTimeOffset? SnoozedUpdateUntil = null,
    // Сохранение: автосохранение после «Готово», формат и качество JPEG (1–100)
    bool AutoSave = false,
    SaveFormat SaveFormat = SaveFormat.Png,
    int JpegQuality = 90,
    AppTheme Theme = AppTheme.Dark);
