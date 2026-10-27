using Snapkit.App.Files;
using Snapkit.App.Shell;
using Snapkit.Core.Input;
using Snapkit.Core.Settings;
using Snapkit.Core.Theming;

namespace Snapkit.App.Settings;

/// <summary>
/// Черновик настроек для окна «Настройки»: всё меняется здесь, в настоящие настройки попадает
/// только по «Сохранить» (Build). Поля окна, которых нет в AppSettings, не трогаются.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private Hotkey? _region1, _region2, _full1, _full2;
    private bool _captureEnabled, _fullScreenEnabled;
    private string _saveFolder;
    private bool _saveFolderChanged;
    private string _accent;
    private AppTheme _theme;
    private SaveFormat _saveFormat;
    private int _jpegQuality;

    public SettingsViewModel(AppSettings current, string version)
    {
        Version = version;
        LaunchAtStartup = current.LaunchAtStartup;
        ShowNotifications = current.ShowNotifications;
        ShowStrip = current.ShowStrip;
        ClearStripAfterPaste = current.ClearStripAfterPaste;
        ProposeLastRegion = current.ProposeLastRegion;
        ShowCursor = current.ShowCursor;
        AutoUpdate = current.AutoUpdate;
        _captureEnabled = current.CaptureHotkeysEnabled;
        _fullScreenEnabled = current.FullScreenHotkeysEnabled;
        // Без подстановки Print Screen: пустое поле в окне — это пустое поле
        var region = HotkeyParser.ParseList(current.CaptureHotkeys, orPrintScreen: false);
        var full = HotkeyParser.ParseList(current.FullScreenHotkeys, orPrintScreen: false);
        _region1 = region.Count > 0 ? region[0] : null;
        _region2 = region.Count > 1 ? region[1] : null;
        _full1 = full.Count > 0 ? full[0] : null;
        _full2 = full.Count > 1 ? full[1] : null;
        AutoSave = current.AutoSave;
        _saveFormat = current.SaveFormat;
        _jpegQuality = Math.Clamp(current.JpegQuality, 1, 100);
        _saveFolder = CaptureFiles.SaveFolder(current.LastSaveFolder);
        _theme = current.Theme;
        _accent = AccentOptions.Parse(current.AccentColor).Value;
    }

    public string Version { get; }

    /// <summary>«Проверить обновления»; null — ссылки нет.</summary>
    public UpdateCheckModel? Updates { get; init; }

    // «Общие»
    public bool LaunchAtStartup { get; set; }
    public bool ShowNotifications { get; set; }
    public bool ShowStrip { get; set; }
    public bool ClearStripAfterPaste { get; set; }
    public bool ProposeLastRegion { get; set; }
    public bool ShowCursor { get; set; }
    public bool AutoUpdate { get; set; }

    // «Клавиши»: до двух сочетаний на действие
    public bool CaptureEnabled { get => _captureEnabled; set { if (Set(ref _captureEnabled, value)) HotkeysChanged(); } }
    public bool FullScreenEnabled { get => _fullScreenEnabled; set { if (Set(ref _fullScreenEnabled, value)) HotkeysChanged(); } }
    public Hotkey? Region1 { get => _region1; set { if (Set(ref _region1, value)) HotkeysChanged(); } }
    public Hotkey? Region2 { get => _region2; set { if (Set(ref _region2, value)) HotkeysChanged(); } }
    public Hotkey? Full1 { get => _full1; set { if (Set(ref _full1, value)) HotkeysChanged(); } }
    public Hotkey? Full2 { get => _full2; set { if (Set(ref _full2, value)) HotkeysChanged(); } }

    /// <summary>Ошибки по четырём полям: снимок области (1, 2), снимок экрана (1, 2).</summary>
    public IReadOnlyList<string?> HotkeyErrors
    {
        get
        {
            var errors = HotkeyRules.CheckAll([_region1, _region2, _full1, _full2]).ToArray();
            const string empty = "Назначьте сочетание или снимите галочку";
            if (_captureEnabled && _region1 is null && _region2 is null) errors[0] = empty;
            if (_fullScreenEnabled && _full1 is null && _full2 is null) errors[2] = empty;
            return errors;
        }
    }

    public bool CanSave => HotkeyErrors.All(e => e is null);

    /// <summary>«Предложить: Ctrl+Print Screen» для снимка всего экрана.</summary>
    public void SuggestFullScreen()
    {
        Full1 = new Hotkey(HotkeyMods.Ctrl, Vk.Snapshot);
        FullScreenEnabled = true;
    }

    // «Сохранение»
    public bool AutoSave { get; set; }
    public SaveFormat SaveFormat { get => _saveFormat; set => Set(ref _saveFormat, value); }
    public int JpegQuality { get => _jpegQuality; set => Set(ref _jpegQuality, Math.Clamp(value, 1, 100)); }
    public string SaveFolder
    {
        get => _saveFolder;
        set
        {
            if (Set(ref _saveFolder, value)) _saveFolderChanged = true;
        }
    }

    // «Вид»
    public AppTheme Theme { get => _theme; set => Set(ref _theme, value); }
    /// <summary>Цвет программы: значение из AccentOptions.All.</summary>
    public string Accent { get => _accent; set => Set(ref _accent, value); }

    /// <summary>
    /// Новые настройки поверх latest — настроек на момент «Сохранить»: всё, чего окно не касается (место ленты,
    /// прежняя рамка, свои цвета, отложенное обновление), остаётся таким, каким стало, пока окно было открыто.
    /// </summary>
    public AppSettings Build(AppSettings latest) => latest with
    {
        LaunchAtStartup = LaunchAtStartup,
        ShowNotifications = ShowNotifications,
        ShowStrip = ShowStrip,
        ClearStripAfterPaste = ClearStripAfterPaste,
        ProposeLastRegion = ProposeLastRegion,
        ShowCursor = ShowCursor,
        AutoUpdate = AutoUpdate,
        CaptureHotkeysEnabled = _captureEnabled,
        FullScreenHotkeysEnabled = _fullScreenEnabled,
        CaptureHotkeys = HotkeyParser.FormatList(new[] { _region1, _region2 }.OfType<Hotkey>()),
        FullScreenHotkeys = HotkeyParser.FormatList(new[] { _full1, _full2 }.OfType<Hotkey>()),
        AutoSave = AutoSave,
        SaveFormat = _saveFormat,
        JpegQuality = _jpegQuality,
        // Папку не меняли в окне — остаётся та, что выбрана в «Сохранить снимок…» и т. п.; папка по умолчанию в файл не пишется
        LastSaveFolder = !_saveFolderChanged ? latest.LastSaveFolder
            : _saveFolder == CaptureFiles.DefaultSaveFolder ? "" : _saveFolder,
        Theme = _theme,
        AccentColor = _accent,
    };

    private void HotkeysChanged()
    {
        OnPropertyChanged(nameof(HotkeyErrors));
        OnPropertyChanged(nameof(CanSave));
    }
}
