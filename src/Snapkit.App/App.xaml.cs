using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Snapkit.App.Capturing;
using Snapkit.App.Clipboarding;
using Snapkit.App.Files;
using Snapkit.App.Imaging;
using Snapkit.App.Input;
using Snapkit.App.Interop;
using Snapkit.App.Notifications;
using Snapkit.App.Paste;
using Snapkit.App.Rendering;
using Snapkit.App.Session;
using Snapkit.App.Shell;
using Snapkit.App.Strip;
using Snapkit.Core;
using Snapkit.Core.Input;
using Snapkit.Core.Session;
using Snapkit.Core.Settings;
using Forms = System.Windows.Forms;

namespace Snapkit.App;

/// <summary>Точка сборки программы: настройки, сессия, лента, трей и их связи.</summary>
public partial class App : Application
{
    private const double StripWidth = 210;
    private const double StripEdgeMargin = 16;
    // Ширина карточки: окно 210 − рамка и поля ленты 22 − рамка и поля подложки стопки 12
    private const double ThumbnailWidth = 176;

    private static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Snapkit");
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Snapkit", "settings.json");

    private SettingsStore _settingsStore = null!;
    private AppSettings _settings = new();
    private SessionService _session = null!;
    private StripViewModel _stripModel = null!;
    private StripWindow _strip = null!;
    private CapsuleWindow _capsule = null!;
    // Что сейчас на экране: развёрнутая лента, капсула или ничего
    private StripView _view = StripView.Hidden;
    private TrayIcon? _tray;
    private CaptureController _capture = null!;
    private ClipboardService? _clipboard;
    private PackagePublisher _publisher = null!;
    private KeyRouter _router = null!;
    private PasteCoordinator? _paste;
    private KeyboardHookThread? _hook;
    // Есть что вставить по Ctrl+V; читается с потока хука клавиатуры
    private volatile bool _hasPasteContent;
    private bool _clearOnExit;
    private bool _stripWasVisible;
    private bool _capsuleWasVisible;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Error("необработанное исключение", (Exception)args.ExceptionObject);

        if (TryInstallItself()) return;

        if (!SingleInstance.TryAcquire(
                () => Dispatcher.BeginInvoke(() =>
                {
                    Log.Info("Повторный запуск Snapkit — показываю ленту");
                    ShowStrip();
                }),
                () => Dispatcher.BeginInvoke(() =>
                {
                    Log.Info("Ставится новая версия — закрываюсь");
                    Shutdown();
                })))
        {
            Log.Info("Snapkit уже запущен — попросил его показать ленту");
            Shutdown();
            return;
        }

        _settingsStore = new SettingsStore(SettingsPath);
        var settingsWereReset = false;
        try
        {
            (_settings, settingsWereReset) = _settingsStore.Load();
        }
        catch (Exception ex)
        {
            // Файл занят или недоступен — работаем с настройками по умолчанию
            Log.Error("чтение настроек", ex);
            settingsWereReset = true;
        }
        Toasts.Enabled = _settings.ShowNotifications;
        ApplyAutoStart();

        _session = new SessionService(new SessionStore(Path.Combine(DataDirectory, "session")));
        // Файлы прошлых перетаскиваний больше не нужны
        try { CaptureFiles.CleanDragRoot(DragDirectory); }
        catch (Exception ex) { Log.Error("очистка файлов перетаскивания", ex); }
        _session.SaveFailed += (_, ex) =>
        {
            Log.Error("запись ленты на диск", ex);
            Toasts.Error("Не удалось сохранить ленту на диск. Подробности — в журнале");
        };
        _clipboard = new ClipboardService();
        _publisher = new PackagePublisher(_session, _clipboard);
        _clipboard.ClipboardChanged += (_, _) => UpdatePackageDisplaced();
        _session.Changed += (_, change) =>
        {
            RefreshStrip();
            OnSessionChangedForClipboard(change);
            UpdatePasteState();
        };

        _stripModel = new StripViewModel { IsTopmost = _settings.StripTopmost };
        _strip = new StripWindow(_stripModel);
        // Окно ленты закрывают только в обход программы (Диспетчер задач, Alt+F4) — для разбора сбоев
        _strip.Closed += (_, _) => Log.Info("Окно ленты закрыто");
        _capsule = new CapsuleWindow(_stripModel);
        _capsule.NewCaptureRequested += (_, _) => StartNewCapture();
        _capsule.ExpandRequested += (_, _) => ApplyStripView(StripView.Strip);
        _capsule.Moved += (_, _) => SaveSettings(_settings with { CapsuleLeft = _capsule.Left, CapsuleTop = _capsule.Top });
        _capture = new CaptureController(_session, HideChrome, RestoreChrome)
        {
            CopyDraft = async (source, annotations, note) =>
            {
                await _publisher.CopyDraftAsync(source, annotations, note);
                UpdatePasteState();
            },
            CustomColors = () => _settings.CustomColors,
            SaveCustomColors = colors => SaveSettings(_settings with { CustomColors = colors }),
        };
        _capture.CaptureAdded += (_, id) => OnCaptureAdded(id);
        _capture.StripFull += (_, _) =>
        {
            Toasts.Show(CaptureFeedback.StripFull);
            ShowStrip();
        };
        _stripModel.ShowStripSetting = _settings.ShowStrip;
        WireStrip();
        WireTray();
        // Перехват клавиш — до восстановления ленты: что бы ни случилось с лентой, Print Screen работает
        StartKeyboardHook();

        var restored = 0;
        try { restored = _session.RestoreFromDisk(); }
        catch (Exception ex)
        {
            Log.Error("восстановление ленты", ex);
            Toasts.Error("Не удалось восстановить ленту после сбоя");
        }
        Log.Info($"Запуск Snapkit: восстановлено снимков с диска — {restored}");

        UpdatePasteState();
        RefreshStrip();
        PlaceStrip();
        ApplyStripView(StripPresence.OnStartup(_settings.ShowStrip, _settings.StripCollapsed));

        if (settingsWereReset) Toasts.Show("Файл настроек не читался, настройки созданы заново");
        // После установки новой версии лента тоже читается с диска, но это не сбой
        if (SelfInstall.IsJustInstalled(e.Args)) Toasts.Show("Snapkit установлен: ярлык в «Пуске», запуск вместе с Windows");
        else if (restored > 0) Toasts.Show("Снимки восстановлены после сбоя");
    }

    /// <summary>
    /// Exe из релиза, запущенный не из своей папки, ставит себя и запускает установленную копию.
    /// true — установка прошла, этой копии надо закрыться.
    /// </summary>
    private bool TryInstallItself()
    {
        var exe = Environment.ProcessPath;
        if (exe is null || SelfInstall.Decide(exe, AutoStart.InstallDirectory, SelfInstall.IsSingleFile) != SelfInstallAction.Install)
            return false;
        try
        {
            Log.Info($"Установка Snapkit из {exe} в {AutoStart.InstallDirectory}");
            SelfInstall.InstallAndLaunch(exe, AutoStart.InstallDirectory);
            Shutdown();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("установка", ex);
            MessageBox.Show($"Не удалось установить Snapkit в папку {AutoStart.InstallDirectory}. " +
                            "Программа запустится без установки. Подробности — в журнале.",
                "Snapkit", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        // Выключение или выход из Windows: ленту не удаляем, она восстановится при следующем запуске
        WaitForDiskWrites();
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Info(_clearOnExit ? "Выход из Snapkit с очисткой ленты" : "Выход из Snapkit");
        _hook?.Dispose();
        _tray?.Dispose();
        _clipboard?.Dispose();
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
        _strip.DeleteCaptureRequested += (_, id) =>
        {
            if (_session.Remove(id) is not { } removed) return;
            Toasts.Show("Снимок удалён", "Вернуть", () =>
            {
                if (!_session.Restore(removed)) Toasts.Show("В ленте нет места, чтобы вернуть снимок");
            });
        };
        _strip.ToggleSentRequested += (_, id) =>
        {
            if (_session.Model.Captures.FirstOrDefault(c => c.Id == id) is not { } capture) return;
            _session.SetSent(id, !capture.IsSent);
            PublishPackageIfUnsent();
        };
        _strip.ResendLastRequested += (_, _) =>
        {
            if (!_session.ResendLastPaste())
            {
                Toasts.Show("Снимков последней вставки в ленте уже нет");
                return;
            }
            PublishPackageIfUnsent();
        };
        _strip.ResendAllRequested += (_, _) =>
        {
            _session.MarkAllUnsent();
            PublishPackageIfUnsent();
        };
        _strip.OpenCaptureRequested += (_, id) => _capture.Reopen(id);
        _strip.ClearRequested += (_, _) => { if (ConfirmClear()) _session.ClearAll(); };
        _strip.CloseRequested += (_, _) =>
        {
            if (!ConfirmClear()) return;
            _session.ClearAll();
            ApplyStripView(StripView.Hidden);
            if (_settings.StripCollapsed) SaveSettings(_settings with { StripCollapsed = false });
        };
        _strip.PasteImageRequested += (_, _) => ImportImageFromClipboard();
        _strip.CopyPackageRequested += (_, _) => RunClipboard(_publisher.CopyPackageCommandAsync);
        _strip.CopyCaptureRequested += (_, id) => RunClipboard(() => _publisher.CopyCaptureAsync(id));
        _strip.ExitRequested += (_, _) => ExitApp();
        _strip.ShowStripToggled += (_, show) => SetShowStrip(show);
        _strip.MinimizeRequested += (_, _) => ApplyStripView(StripPresence.OnMinimize(_settings.ShowStrip));
        _strip.ReorderRequested += (_, drop) => _session.MoveBefore(drop.Id, drop.Slot);
        _strip.DragFileFor = DragFileFor;
        _strip.SaveCaptureRequested += (_, id) => SaveCapture(id);
        _strip.SavePackageRequested += (_, _) => SavePackage();
        _strip.FilesDropped += (_, files) => ImportImages(files);
        _strip.ImportFileRequested += (_, _) =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Импортировать картинку",
                Filter = "Изображения|*.png;*.jpg;*.jpeg",
                Multiselect = true,
            };
            if (dialog.ShowDialog(_strip) == true) ImportImages(dialog.FileNames);
        };
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
            // Щелчок у часов показывает или прячет развёрнутую ленту; настройку не меняет
            if (_strip.IsVisible) ApplyStripView(StripView.Hidden);
            else ShowStrip();
        };
        _tray.ExitRequested += (_, _) => ExitApp();
        _tray.ShowStripChecked = _settings.ShowStrip;
        _tray.ShowStripToggled += (_, show) => SetShowStrip(show);
    }

    private void StartNewCapture() => _capture.StartNewCapture();

    /// <summary>
    /// Новый снимок: лента включена — закрытая лента появляется, капсула остаётся капсулой;
    /// выключена — короткое уведомление, что снимок в пачке.
    /// </summary>
    private void OnCaptureAdded(Guid id)
    {
        ApplyStripView(StripPresence.AfterNewCapture(_view, _settings.ShowStrip));
        if (_settings.ShowStrip) return;
        if (CaptureLetters.Assign(_session.Model.Captures).TryGetValue(id, out var letter))
            Toasts.Show(CaptureFeedback.Ready(letter));
    }

    /// <summary>
    /// Единственное место, которое показывает и прячет ленту и капсулу. Свёрнута лента или нет,
    /// запоминается, только когда на экране лента или капсула: выключение ленты его не сбрасывает.
    /// </summary>
    private void ApplyStripView(StripView view)
    {
        _view = view;
        switch (view)
        {
            case StripView.Hidden:
                _strip.Hide();
                _capsule.Hide();
                return;
            case StripView.Strip:
                _capsule.Hide();
                if (!_strip.IsVisible) _strip.Show();
                break;
            case StripView.Capsule:
                _strip.Hide();
                if (!_capsule.IsVisible)
                {
                    _capsule.Show();
                    PlaceCapsule();
                }
                break;
        }
        var collapsed = view == StripView.Capsule;
        if (_settings.StripCollapsed != collapsed) SaveSettings(_settings with { StripCollapsed = collapsed });
    }

    /// <summary>Капсула — где её оставили, иначе у правого края рабочей области на высоте ленты.</summary>
    private void PlaceCapsule()
    {
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (_settings is { CapsuleLeft: { } left, CapsuleTop: { } top } && screen.Contains(new Point(left + 20, top + 20)))
        {
            _capsule.Left = left;
            _capsule.Top = top;
            return;
        }
        var area = SystemParameters.WorkArea;
        _capsule.Left = area.Right - _capsule.ActualWidth - StripEdgeMargin;
        _capsule.Top = _strip.Top;
    }

    /// <summary>Галочка «Показывать ленту» (меню значка у часов или «…» ленты): сразу показать или спрятать.</summary>
    private void SetShowStrip(bool show)
    {
        SaveSettings(_settings with { ShowStrip = show });
        _stripModel.ShowStripSetting = show;
        if (_tray is not null) _tray.ShowStripChecked = show;
        ApplyStripView(StripPresence.OnShowStripChanged(show, _settings.StripCollapsed));
    }

    private static readonly string DragDirectory = Path.Combine(DataDirectory, "drag");

    /// <summary>Папка для окон сохранения: последняя выбранная, иначе «Изображения\Snapkit».</summary>
    private string SaveFolder()
    {
        var folder = _settings.LastSaveFolder is { Length: > 0 } last && Directory.Exists(last) ? last : CaptureFiles.DefaultSaveFolder;
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>«Сохранить снимок…»: PNG с разметкой туда, куда укажут.</summary>
    private void SaveCapture(Guid id)
    {
        var captures = _session.Model.Captures;
        if (captures.FirstOrDefault(c => c.Id == id) is not { } capture) return;
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Сохранить снимок",
                Filter = "PNG|*.png",
                FileName = PackageExport.TimestampName(DateTime.Now) + ".png",
                InitialDirectory = SaveFolder(),
            };
            if (dialog.ShowDialog(_strip) != true) return;
            char? letter = CaptureLetters.Assign(captures).TryGetValue(id, out var l) ? l : null;
            CaptureFiles.SavePng(dialog.FileName, CaptureRenderer.Render(_session.GetSource(id), capture, letter ?? 'A'));
            SaveSettings(_settings with { LastSaveFolder = Path.GetDirectoryName(dialog.FileName) ?? "" });
            Toasts.Show("Снимок сохранён");
        }
        catch (Exception ex)
        {
            Log.Error("сохранение снимка", ex);
            Toasts.Error("Не удалось сохранить файл. Подробности — в журнале");
        }
    }

    /// <summary>«Сохранить пакет…»: папка с A.png, B.png… и prompt.md; отметки «отправлен» не меняются.</summary>
    private void SavePackage()
    {
        var package = PackageSelector.ForExport(_session.Model.Captures);
        if (package.Count == 0)
        {
            Toasts.Show("Сначала сделайте снимок.");
            return;
        }
        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Куда сохранить пакет", InitialDirectory = SaveFolder() };
            if (dialog.ShowDialog(_strip) != true) return;
            var items = package
                .Select(p => (p, CaptureRenderer.Render(_session.GetSource(p.Capture.Id), p.Capture, p.Letter)))
                .ToList();
            var folder = CaptureFiles.SavePackage(dialog.FolderName, DateTime.Now, items);
            SaveSettings(_settings with { LastSaveFolder = dialog.FolderName });
            Toasts.Show("Пакет сохранён", "Открыть папку", () => System.Diagnostics.Process.Start("explorer.exe", $"\"{folder}\""));
        }
        catch (Exception ex)
        {
            Log.Error("сохранение пакета", ex);
            Toasts.Error("Не удалось сохранить файл. Подробности — в журнале");
        }
    }

    /// <summary>Снимок с разметкой файлом для перетаскивания в другое окно; null — не получилось.</summary>
    private string? DragFileFor(Guid id)
    {
        try
        {
            var captures = _session.Model.Captures;
            if (captures.FirstOrDefault(c => c.Id == id) is not { } capture) return null;
            char? letter = CaptureLetters.Assign(captures).TryGetValue(id, out var l) ? l : null;
            var rendered = CaptureRenderer.Render(_session.GetSource(id), capture, letter ?? 'A');
            return CaptureFiles.WriteDragFile(DragDirectory, rendered, letter);
        }
        catch (Exception ex)
        {
            Log.Error("файл для перетаскивания", ex);
            return null;
        }
    }

    /// <summary>Глобальные клавиши: Print Screen — снимок, Ctrl+V — вставка пачки.</summary>
    private void StartKeyboardHook()
    {
        var captureKeys = HotkeyParser.ParseList(_settings.CaptureHotkeys);
        _router = new KeyRouter(() => new PasteContext(_hasPasteContent, IsPackageCurrent(), Foreground.IsOwn()), ModifierState.Read)
        {
            Hotkeys = captureKeys.ToDictionary(key => key, _ => KeyAction.CaptureRegion),
        };
        _stripModel.EmptyHint = EmptyHintFor(captureKeys);
        _paste = new PasteCoordinator(_session, _clipboard!, _router, _publisher.CreatePastePlan, () => _settings.ClearStripAfterPaste);
        _hook = new KeyboardHookThread(_router, action => Dispatcher.BeginInvoke(() => OnKeyAction(action)));
        try { _hook.Start(); }
        catch (Exception ex)
        {
            Log.Error("перехват клавиатуры", ex);
            Toasts.Error("Не удалось перехватить клавиши: Print Screen и вставка пачки работать не будут");
        }
    }

    /// <summary>«Нажми Print Screen или «Новый снимок»» — с клавишами из настроек.</summary>
    private static string EmptyHintFor(IReadOnlyList<Hotkey> keys)
    {
        var names = keys.Select(HotkeyParser.Display).ToList();
        var list = names.Count == 1 ? names[0] : string.Join(", ", names);
        return $"Нажми {list} или «Новый снимок»";
    }

    // Вызывается с потока хука клавиатуры
    private bool IsPackageCurrent() => _clipboard?.IsPackageCurrent() ?? false;

    /// <summary>
    /// После снимка или правки в буфер ложится пакет. После удаления — только если пакет
    /// и так наш, чтобы не затирать то, что пользователь скопировал сам.
    /// </summary>
    private void OnSessionChangedForClipboard(SessionChange change)
    {
        var republish = ClipboardRepublish.After(change.Kind, change.CaptureId is not null, _session.Model.UnsentCount > 0,
            stripPackageInClipboard: IsPackageCurrent() && _publisher.IsStripPackage);
        if (republish) RunClipboard(_publisher.PublishPackageAsync);
    }

    /// <summary>Пачку поменяли с ленты — пакет снова в буфер, и следующий Ctrl+V в любом окне вставит её.</summary>
    private void PublishPackageIfUnsent()
    {
        if (_session.Model.UnsentCount > 0) RunClipboard(_publisher.PublishPackageAsync);
        else UpdatePasteState();
    }

    private void UpdatePackageDisplaced()
        => _stripModel.PackageDisplaced = _session.Model.UnsentCount > 0 && !IsPackageCurrent();

    // Что можно вставить по Ctrl+V — для перехвата на потоке хука
    private void UpdatePasteState()
    {
        _hasPasteContent = _publisher.HasPasteContent;
        UpdatePackageDisplaced();
    }

    private async void RunClipboard(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (ClipboardBusyException)
        {
            Toasts.Error("Буфер обмена занят другой программой. Повторите");
        }
        catch (Exception ex)
        {
            Log.Error("буфер обмена", ex);
            Toasts.Error("Не удалось положить снимки в буфер обмена");
        }
        UpdatePasteState();
    }

    private void OnKeyAction(KeyAction action)
    {
        switch (action)
        {
            case KeyAction.CaptureRegion:
                StartNewCapture();
                break;
            case KeyAction.PasteBatch:
                PasteBatch();
                break;
        }
    }

    private async void PasteBatch()
    {
        if (_paste is null) return;
        try
        {
            var outcome = await _paste.PasteBatchAsync();
            Log.Info($"Вставка пачки: {outcome}");
            var message = PasteCoordinator.Describe(outcome);
            if (message.Length == 0) return;
            if (outcome.Status is PasteStatus.CompletedVerified or PasteStatus.CompletedUnverified) Toasts.Show(message);
            else Toasts.Error(message);
        }
        catch (Exception ex)
        {
            Log.Error("вставка пачки", ex);
            Toasts.Error("Вставка не удалась. Подробности — в журнале");
        }
        _stripModel.CanResendLast = _session.HasLastPaste;
        UpdatePasteState();
    }

    // На время снимка лента и уведомления прячутся, чтобы не попасть в кадр
    private void HideChrome()
    {
        _stripWasVisible = _strip.IsVisible;
        _capsuleWasVisible = _capsule.IsVisible;
        if (_stripWasVisible) _strip.Hide();
        if (_capsuleWasVisible) _capsule.Hide();
        Toasts.HideNow();
    }

    private void RestoreChrome()
    {
        if (_stripWasVisible) _strip.Show();
        if (_capsuleWasVisible) _capsule.Show();
    }

    private void ShowStrip()
    {
        ApplyStripView(StripView.Strip);
        _strip.Activate();
    }

    private void RefreshStrip()
    {
        _stripModel.CanResendLast = _session.HasLastPaste;
        var thumbnailPixels = (int)Math.Ceiling(ThumbnailWidth * VisualTreeHelper.GetDpi(_strip).DpiScaleX);
        _stripModel.Refresh(_session.Model.Captures, (capture, letter) =>
        {
            try
            {
                return CaptureRenderer.RenderThumbnail(_session.GetSource(capture.Id), capture, letter, thumbnailPixels);
            }
            catch (Exception ex)
            {
                // Одна испорченная миниатюра не должна ломать всю ленту
                Log.Error("миниатюра снимка", ex);
                return EmptyThumbnail;
            }
        });
    }

    private static readonly BitmapSource EmptyThumbnail = CreateEmptyThumbnail();

    private static BitmapSource CreateEmptyThumbnail()
    {
        var image = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
        image.Freeze();
        return image;
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
        if (!_session.Model.CanMakeRoom)
        {
            Toasts.Show(CaptureFeedback.StripFull);
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

        AddImportedImage(image);
        Toasts.Show("Изображение добавлено.");
    }

    /// <summary>Картинки из файлов (меню или перетаскивание из Проводника) — новыми снимками по порядку.</summary>
    private void ImportImages(IReadOnlyList<string> paths)
    {
        var files = paths.Where(p => Path.GetExtension(p).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg").ToList();
        if (files.Count == 0)
        {
            Toasts.Show("Можно добавить только картинки PNG и JPEG.");
            return;
        }
        var added = 0;
        foreach (var file in files)
        {
            // Места нет — оставшиеся файлы пропускаются
            if (!_session.Model.CanMakeRoom) break;
            try
            {
                AddImportedImage(ImageCodecs.DecodeImage(File.ReadAllBytes(file)));
                added++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                Log.Error($"импорт файла {Path.GetFileName(file)}", ex);
            }
        }
        var skipped = files.Count - added;
        Toasts.Show(skipped == 0
            ? $"Добавлено снимков: {added}"
            : $"Добавлено снимков: {added}, не поместилось или не читается: {skipped}");
    }

    /// <summary>У картинки извне нет места на экране — считаем, что она по центру основного монитора.</summary>
    private void AddImportedImage(BitmapSource image)
    {
        var monitor = Forms.Screen.PrimaryScreen!.Bounds;
        var bounds = new RectI(monitor.X + (monitor.Width - image.PixelWidth) / 2, monitor.Y + (monitor.Height - image.PixelHeight) / 2,
            image.PixelWidth, image.PixelHeight);
        _session.AddCapture(image, bounds, [], "");
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
        Toasts.Error("Что-то пошло не так. Подробности — в журнале");
        e.Handled = true;
    }
}
