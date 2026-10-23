using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Snapkit.App.Capturing;
using Snapkit.App.Imaging;
using Snapkit.App.Notifications;
using Snapkit.App.Rendering;
using Snapkit.App.Session;
using Snapkit.App.Shell;
using Snapkit.App.Strip;
using Snapkit.Core;
using Snapkit.Core.Session;
using Snapkit.Core.Settings;
using Forms = System.Windows.Forms;

namespace Snapkit.App;

/// <summary>Точка сборки программы: настройки, сессия, лента, трей и их связи.</summary>
public partial class App : Application
{
    private const double StripWidth = 240;
    private const double StripEdgeMargin = 16;
    private const double ThumbnailWidth = 218;

    private static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Snapkit");
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Snapkit", "settings.json");

    private SettingsStore _settingsStore = null!;
    private AppSettings _settings = new();
    private SessionService _session = null!;
    private StripViewModel _stripModel = null!;
    private StripWindow _strip = null!;
    private TrayIcon? _tray;
    private CaptureController _capture = null!;
    private bool _clearOnExit;
    private bool _stripWasVisible;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Error("необработанное исключение", (Exception)args.ExceptionObject);

        if (!SingleInstance.TryAcquire(() => Dispatcher.BeginInvoke(ShowStrip)))
        {
            Shutdown();
            return;
        }

        _settingsStore = new SettingsStore(SettingsPath);
        var (settings, settingsWereReset) = _settingsStore.Load();
        _settings = settings;
        Toasts.Enabled = _settings.ShowNotifications;
        ApplyAutoStart();

        _session = new SessionService(new SessionStore(Path.Combine(DataDirectory, "session")));
        _session.SaveFailed += (_, ex) =>
        {
            Log.Error("запись ленты на диск", ex);
            Toasts.Error("Не удалось сохранить ленту на диск: " + ex.Message);
        };
        _session.Changed += (_, _) => RefreshStrip();

        _stripModel = new StripViewModel { IsTopmost = _settings.StripTopmost };
        _strip = new StripWindow(_stripModel);
        _capture = new CaptureController(_session, HideChrome, RestoreChrome);
        WireStrip();
        WireTray();

        var restored = 0;
        try { restored = _session.RestoreFromDisk(); }
        catch (Exception ex)
        {
            Log.Error("восстановление ленты", ex);
            Toasts.Error("Не удалось восстановить ленту после сбоя");
        }

        RefreshStrip();
        PlaceStrip();
        _strip.Show();

        if (settingsWereReset) Toasts.Show("Файл настроек не читался, настройки созданы заново");
        if (restored > 0) Toasts.Show("Снимки восстановлены после сбоя");
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        // Выключение или выход из Windows: ленту не удаляем, она восстановится при следующем запуске
        WaitForDiskWrites();
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        if (_session is not null)
        {
            if (_clearOnExit) _session.ClearAll();
            WaitForDiskWrites();
        }
        base.OnExit(e);
    }

    private void WireStrip()
    {
        _strip.NewCaptureRequested += (_, _) => StartNewCapture();
        _strip.DeleteCaptureRequested += (_, id) => _session.Remove(id);
        _strip.ClearRequested += (_, _) => { if (ConfirmClear()) _session.ClearAll(); };
        _strip.CloseRequested += (_, _) =>
        {
            if (!ConfirmClear()) return;
            _session.ClearAll();
            _strip.Hide();
        };
        _strip.PasteImageRequested += (_, _) => ImportImageFromClipboard();
        _strip.ExitRequested += (_, _) => ExitApp();
        _strip.TopmostToggled += (_, topmost) =>
        {
            _stripModel.IsTopmost = topmost;
            SaveSettings(_settings with { StripTopmost = topmost });
        };
        _strip.Moved += (_, _) => SaveSettings(_settings with { StripLeft = _strip.Left, StripTop = _strip.Top });
    }

    private void WireTray()
    {
        _tray = new TrayIcon();
        _tray.NewCaptureRequested += (_, _) => StartNewCapture();
        _tray.ShowStripRequested += (_, _) => ShowStrip();
        _tray.ToggleStripRequested += (_, _) =>
        {
            if (_strip.IsVisible) _strip.Hide();
            else ShowStrip();
        };
        _tray.ExitRequested += (_, _) => ExitApp();
    }

    private void StartNewCapture() => _capture.StartNewCapture();

    // На время снимка лента и уведомления прячутся, чтобы не попасть в кадр
    private void HideChrome()
    {
        _stripWasVisible = _strip.IsVisible;
        if (_stripWasVisible) _strip.Hide();
        Toasts.HideNow();
    }

    private void RestoreChrome()
    {
        if (_stripWasVisible) _strip.Show();
    }

    private void ShowStrip()
    {
        if (!_strip.IsVisible) _strip.Show();
        _strip.Activate();
    }

    private void RefreshStrip()
    {
        var thumbnailPixels = (int)Math.Ceiling(ThumbnailWidth * VisualTreeHelper.GetDpi(_strip).DpiScaleX);
        _stripModel.Refresh(_session.Model.Captures,
            (capture, letter) => CaptureRenderer.RenderThumbnail(_session.GetSource(capture.Id), capture, letter, thumbnailPixels));
    }

    private void PlaceStrip()
    {
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (_settings is { StripLeft: { } left, StripTop: { } top } && screen.Contains(new Point(left + StripWidth / 2, top + 10)))
        {
            _strip.Left = left;
            _strip.Top = top;
            return;
        }
        var area = SystemParameters.WorkArea;
        _strip.Left = area.Right - StripWidth - StripEdgeMargin;
        _strip.Top = area.Top + 80;
    }

    /// <summary>Подтверждение перед удалением снимков: спрашиваем, только если есть неотправленные.</summary>
    private bool ConfirmClear()
    {
        if (_session.Model.UnsentCount == 0) return true;
        return MessageBox.Show(_strip, "Удалить снимки сессии?\nНеотправленные снимки будут потеряны.", "Snapkit",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private void ExitApp()
    {
        if (_session.Model.UnsentCount > 0
            && MessageBox.Show(_strip, "Выйти из Snapkit?\nСнимки ленты будут удалены.", "Snapkit",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }
        _clearOnExit = true;
        Shutdown();
    }

    private void ImportImageFromClipboard()
    {
        if (_session.Model.IsFull)
        {
            Toasts.Show($"В ленте максимум {SessionModel.MaxCaptures} снимков. Отправьте или удалите лишние");
            return;
        }

        BitmapSource? image;
        try
        {
            image = ReadClipboardImage();
        }
        catch (ExternalException ex)
        {
            Log.Error("чтение буфера обмена", ex);
            Toasts.Error("Буфер обмена занят другой программой. Повторите");
            return;
        }

        if (image is null)
        {
            Toasts.Show("В буфере нет изображения.");
            return;
        }

        // У картинки из буфера нет места на экране — считаем, что она по центру основного монитора
        var monitor = Forms.Screen.PrimaryScreen!.Bounds;
        var bounds = new RectI(monitor.X + (monitor.Width - image.PixelWidth) / 2, monitor.Y + (monitor.Height - image.PixelHeight) / 2,
            image.PixelWidth, image.PixelHeight);
        _session.AddCapture(image, bounds, [], "");
        Toasts.Show("Изображение добавлено.");
    }

    private static BitmapSource? ReadClipboardImage()
    {
        // PNG точнее: в нём нет проблем с прозрачностью, как у CF_DIB
        if (Clipboard.ContainsData("PNG") && Clipboard.GetData("PNG") is MemoryStream png)
            return ImageCodecs.DecodePng(png.ToArray());
        if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } bitmap)
            return ImageCodecs.Opaque(bitmap);
        return null;
    }

    private void SaveSettings(AppSettings settings)
    {
        _settings = settings;
        try { _settingsStore.Save(settings); }
        catch (Exception ex)
        {
            Log.Error("сохранение настроек", ex);
            Toasts.Error("Не удалось сохранить настройки");
        }
    }

    private void ApplyAutoStart()
    {
        var exe = Environment.ProcessPath;
        // Автозапуск прописывает только установленная копия, а не сборка из папки проекта
        if (exe is null || !AutoStart.IsInstalledCopy(exe)) return;
        try { AutoStart.Apply(_settings.LaunchAtStartup, exe); }
        catch (Exception ex) { Log.Error("автозапуск", ex); }
    }

    private void WaitForDiskWrites()
    {
        try { _session.FlushAsync().Wait(TimeSpan.FromSeconds(3)); }
        catch (Exception ex) { Log.Error("ожидание записи на диск", ex); }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("в интерфейсе", e.Exception);
        Toasts.Error("Ошибка: " + e.Exception.Message);
        e.Handled = true;
    }
}
