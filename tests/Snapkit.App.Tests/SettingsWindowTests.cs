using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Snapkit.App.Settings;
using Snapkit.Core.Input;
using Snapkit.Core.Settings;

namespace Snapkit.App.Tests;

public class SettingsWindowTests
{
    // Привязки WPF применяет из очереди потока окна — даём ей пройти, как в показанном окне
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);

    [Fact]
    public void RecordAndSave_GivesNewHotkeys() => UiApp.Run(() =>
    {
        // Окно строится из разметки (ошибки XAML видны только здесь), пишет сочетание и отдаёт его по «Сохранить»
        var window = new SettingsWindow(new SettingsViewModel(new AppSettings() with { CaptureHotkeys = "PrintScreen" }, "0.3.1"));
        var states = new List<bool>();
        window.RecordingChanged += (_, recording) => states.Add(recording);
        AppSettings? saved = null;
        window.Saved += (_, model) => saved = model.Build(new AppSettings() with { CaptureHotkeys = "PrintScreen" });
        Pump();

        window.Region2Box.Frame.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonUpEvent,
        });
        Assert.True(window.Region2Box.IsRecording);
        window.Record(new Hotkey(HotkeyMods.Ctrl, 'S'));
        Assert.False(window.Region2Box.IsRecording);
        window.SaveButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        Assert.Equal(new[] { true, false }, states);
        Assert.Equal("Print Screen, Ctrl+S", saved?.CaptureHotkeys);
    });

    [Fact]
    public void CheckUpdates_ShowsAnswerAndUpdateButton() => UiApp.Run(() =>
    {
        var latest = new Snapkit.Core.Updates.ReleaseInfo(new Version(0, 5, 0), "https://example/Snapkit.exe", "00");
        var updates = new UpdateCheckModel(new Version(0, 4, 0), true, () => Task.FromResult<Snapkit.Core.Updates.ReleaseInfo?>(latest),
            _ => Task.FromResult(true), () => { });
        var window = new SettingsWindow(new SettingsViewModel(new AppSettings(), "0.4.0") { Updates = updates });
        Pump();
        Assert.Equal(Visibility.Collapsed, window.UpdatePanel.Visibility);

        window.CheckUpdatesButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump();

        Assert.Equal(Visibility.Visible, window.UpdatePanel.Visibility);
        Assert.Equal("Вышла версия 0.5.0", window.UpdateStatusText.Text);
        Assert.Equal("Обновить", window.UpdateActionButton.Content);
        Assert.Equal(Visibility.Visible, window.UpdateActionButton.Visibility);
        window.Close();
    });

    [Fact]
    public void CheckUpdates_UpToDate_NoActionButton() => UiApp.Run(() =>
    {
        var latest = new Snapkit.Core.Updates.ReleaseInfo(new Version(0, 4, 0), "https://example/Snapkit.exe", "00");
        var updates = new UpdateCheckModel(new Version(0, 4, 0), true, () => Task.FromResult<Snapkit.Core.Updates.ReleaseInfo?>(latest),
            _ => Task.FromResult(true), () => { });
        var window = new SettingsWindow(new SettingsViewModel(new AppSettings(), "0.4.0") { Updates = updates });
        Pump();

        window.CheckUpdatesButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump();

        Assert.Equal("У вас последняя версия", window.UpdateStatusText.Text);
        Assert.Equal(Visibility.Collapsed, window.UpdateActionButton.Visibility);
        window.Close();
    });

    [Fact]
    public void NoUpdates_NoCheckLink() => UiApp.Run(() =>
    {
        var window = new SettingsWindow(new SettingsViewModel(new AppSettings(), "0.4.0"));
        Pump();
        Assert.Equal(Visibility.Collapsed, window.CheckUpdatesButton.Visibility);
        window.Close();
    });

    [Fact]
    public void Record_Escape_KeepsField() => UiApp.Run(() =>
    {
        var window = new SettingsWindow(new SettingsViewModel(new AppSettings(), "0.3.1"));
        Pump();
        window.Region1Box.Frame.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonUpEvent,
        });
        window.Record(new Hotkey(HotkeyMods.None, Vk.Escape));
        Assert.Equal(new Hotkey(HotkeyMods.None, Vk.Snapshot), window.Region1Box.Hotkey);
        window.Close();
    });
}
