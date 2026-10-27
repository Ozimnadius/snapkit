using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Snapkit.App.Rendering;
using Snapkit.Core.Input;
using Snapkit.Core.Settings;
using Snapkit.Core.Theming;

namespace Snapkit.App.Settings;

/// <summary>
/// Окно «Настройки». Правит черновик (SettingsViewModel); по «Сохранить» отдаёт новые настройки
/// событием Saved — применяет их App. «Отмена» и ✕ ничего не меняют.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _model;
    private HotkeyBox? _recording;

    public SettingsWindow(SettingsViewModel model)
    {
        InitializeComponent();
        _model = model;
        DataContext = model;
        VersionText.Text = $"Версия {model.Version}";
        foreach (var box in new[] { Region1Box, Region2Box, Full1Box, Full2Box }) box.RecordRequested += (_, _) => StartRecording(box);

        (model.SaveFormat == SaveFormat.Jpeg ? JpegButton : PngButton).IsChecked = true;
        (model.Theme switch { AppTheme.Light => LightTheme, AppTheme.System => SystemTheme, _ => DarkTheme }).IsChecked = true;
        BuildAccentSwatches();
        UpdateSample();
        GeneralTab.IsChecked = true;

        // Ушли из окна или закрыли его — запись сочетания прекращается
        Deactivated += (_, _) => StopRecording();
        Closed += (_, _) => StopRecording();
    }

    /// <summary>Нажали «Сохранить»: App собирает новые настройки через Build поверх нынешних.</summary>
    public event EventHandler<SettingsViewModel>? Saved;

    /// <summary>Ссылка «Удалить Snapkit с компьютера…».</summary>
    public event EventHandler? UninstallRequested;

    /// <summary>Поле ждёт сочетание (true) или запись закончилась (false) — App включает и выключает запись в перехвате.</summary>
    public event EventHandler<bool>? RecordingChanged;

    /// <summary>Сочетание из перехвата клавиатуры (вызывать на потоке окна).</summary>
    public void Record(Hotkey hotkey)
    {
        if (_recording is not { } box) return;
        StopRecording();
        if (hotkey.Modifiers != HotkeyMods.None)
        {
            box.Hotkey = hotkey;
            return;
        }
        switch (hotkey.Vk)
        {
            case Vk.Escape:
                return; // ввод отменён, поле как было
            case 0x08 or 0x2E: // Backspace, Delete — очистить
                box.Hotkey = null;
                return;
            default:
                box.Hotkey = hotkey;
                return;
        }
    }

    private void StartRecording(HotkeyBox box)
    {
        if (_recording is not null) _recording.IsRecording = false;
        _recording = box;
        box.IsRecording = true;
        RecordingChanged?.Invoke(this, true);
    }

    private void StopRecording()
    {
        if (_recording is null) return;
        _recording.IsRecording = false;
        _recording = null;
        RecordingChanged?.Invoke(this, false);
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        StopRecording();
        GeneralPanel.Visibility = Visible(GeneralTab);
        KeysPanel.Visibility = Visible(KeysTab);
        SavePanel.Visibility = Visible(SaveTab);
        LookPanel.Visibility = Visible(LookTab);
    }

    private static Visibility Visible(RadioButton tab) => tab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

    private void Format_Checked(object sender, RoutedEventArgs e)
    {
        _model.SaveFormat = JpegButton.IsChecked == true ? SaveFormat.Jpeg : SaveFormat.Png;
        QualityPanel.IsEnabled = _model.SaveFormat == SaveFormat.Jpeg;
        QualityPanel.Opacity = QualityPanel.IsEnabled ? 1 : 0.45;
    }

    private void Theme_Checked(object sender, RoutedEventArgs e)
        => _model.Theme = LightTheme.IsChecked == true ? AppTheme.Light : SystemTheme.IsChecked == true ? AppTheme.System : AppTheme.Dark;

    private void BuildAccentSwatches()
    {
        foreach (var option in AccentOptions.All)
        {
            var swatch = new RadioButton
            {
                Style = (Style)FindResource("AccentSwatch"),
                Background = Palette.CreateAccentFill(option),
                GroupName = "accent",
                IsChecked = option.Value == _model.Accent,
            };
            swatch.Checked += (_, _) =>
            {
                _model.Accent = option.Value;
                UpdateSample();
            };
            AccentPanel.Children.Add(swatch);
        }
    }

    // Образец «так будет выглядеть» — выбранным цветом, ещё до «Сохранить»
    private void UpdateSample()
    {
        var option = AccentOptions.Parse(_model.Accent);
        var fill = Palette.CreateAccentFill(option);
        SampleFrame.Stroke = Palette.Brush(option.First);
        SamplePin.Background = fill;
        SampleDone.Background = fill;
    }

    private void SuggestFullScreen_Click(object sender, RoutedEventArgs e) => _model.SuggestFullScreen();

    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Папка сохранения",
            InitialDirectory = Directory.Exists(_model.SaveFolder) ? _model.SaveFolder : "",
        };
        if (dialog.ShowDialog(this) == true) _model.SaveFolder = dialog.FolderName;
    }

    private void Uninstall_Click(object sender, RoutedEventArgs e) => UninstallRequested?.Invoke(this, EventArgs.Empty);

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!_model.CanSave) return;
        Saved?.Invoke(this, _model);
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
