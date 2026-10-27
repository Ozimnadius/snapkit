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
using Snapkit.Core.Theming;
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
    private Settings.SettingsWindow? _settingsWindow;
    private Updater? _updater;
    private DispatcherTimer? _updateTimer;
    private static readonly TimeSpan FirstUpdateCheck = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(24);
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

        // Удаление из «Параметры → Приложения» или из настроек — раньше самоустановки и проверки второй копии
        if (e.Args.Contains(Uninstaller.Argument))
        {
            RunUninstall();
            return;
        }

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
        ApplyTheme();
        ApplyAccent();
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        ApplyAutoStart();
        RegisterForUninstall();

        _session = new SessionService(new SessionStore(Path.Combine(DataDirectory, "session")));
        // Файлы прошлых перетаскиваний больше не нужны
        try { CaptureFiles.CleanDragRoot(DragDirectory); }
        catch (Exception ex) { Log.Error("очистка файлов перетаскивания", ex); }
        _session.SaveFailed += (_, ex) =>
        {
            Log.Error("запись ленты на диск", ex);
            Toasts.Error("Не удалось сохранить ленту на диск. Подробности — в журнале");
        };
        _session.Evicted += (_, _) => Toasts.Show(CaptureFeedback.Evicted);
        _clipboard = new ClipboardService();
        _publisher = new PackagePublisher(_session, _clipboard);
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
            ShowCursor = () => _settings.ShowCursor,
        };
        _capture.CaptureAdded += (_, _) => ApplyStripView(StripPresence.AfterNewCapture(_view, _settings.ShowStrip));
        // Последняя снятая область — для «Предлагать ту же область»
        _capture.CaptureAdded += (_, id) =>
        {
            if (_session.Model.Captures.FirstOrDefault(c => c.Id == id) is { } capture && _settings.LastRegion != capture.ScreenBounds)
                SaveSettings(_settings with { LastRegion = capture.ScreenBounds });
        };
        // «Готово» — как «Копировать снимок»: в буфере только этот снимок, о чём и сообщаем
        _capture.CaptureDone += (_, id) =>
        {
            RunClipboard(() => _publisher.PublishCaptureAsync(id));
            Toasts.Show(CaptureFeedback.Copied);
            if (_settings.AutoSave) AutoSave(id);
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
        StartUpdateChecks();
        if (SelfInstall.IsJustUpdated(e.Args)) Toasts.Show($"Snapkit обновлён до версии {AppVersion}");
        else if (SelfInstall.IsJustInstalled(e.Args)) Toasts.Show("Snapkit установлен: ярлык в «Пуске», запуск вместе с Windows");
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
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
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
        _strip.CopyAllRequested += (_, _) => RunClipboard(_publisher.CopyAllCommandAsync);
        _strip.CopyCaptureRequested += (_, id) => RunClipboard(() => _publisher.CopyCaptureAsync(id));
        _strip.ExitRequested += (_, _) => ExitApp();
        _strip.SettingsRequested += (_, _) => OpenSettings();
        _strip.ShowStripToggled += (_, show) => SetShowStrip(show);
        _strip.MinimizeRequested += (_, _) => ApplyStripView(StripPresence.OnMinimize(_settings.ShowStrip));
        _strip.ReorderRequested += (_, drop) => _session.MoveBefore(drop.Id, drop.Slot);
        _strip.DragFileFor = DragFileFor;
        _strip.DroppedOutside += (_, id) => PasteNotesAfterDrop(id);
        _strip.SaveCaptureRequested += (_, id) => SaveCapture(id);
        _strip.SaveAllRequested += (_, _) => SaveAll();
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
        _tray.SettingsRequested += (_, _) => OpenSettings();
        _tray.ShowStripChecked = _settings.ShowStrip;
        _tray.ShowStripToggled += (_, show) => SetShowStrip(show);
    }

    private void StartNewCapture() => _capture.StartNewCapture(_settings.ProposeLastRegion ? _settings.LastRegion : null);

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
        var configured = CaptureFiles.SaveFolder(_settings.LastSaveFolder);
        // Папку из настроек удалили — окна сохранения начинают с обычной
        var folder = configured == CaptureFiles.DefaultSaveFolder || Directory.Exists(configured) ? configured : CaptureFiles.DefaultSaveFolder;
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
                Filter = _settings.SaveFormat == SaveFormat.Jpeg ? "JPEG|*.jpg" : "PNG|*.png",
                FileName = PackageExport.TimestampName(DateTime.Now) + PackageExport.Extension(_settings.SaveFormat),
                InitialDirectory = SaveFolder(),
            };
            if (dialog.ShowDialog(_strip) != true) return;
            char? letter = CaptureLetters.Assign(captures).TryGetValue(id, out var l) ? l : null;
            CaptureFiles.Save(dialog.FileName, CaptureRenderer.Render(_session.GetSource(id), capture, letter ?? 'A'),
                _settings.SaveFormat, _settings.JpegQuality);
            SaveSettings(_settings with { LastSaveFolder = Path.GetDirectoryName(dialog.FileName) ?? "" });
            Toasts.Show("Снимок сохранён");
        }
        catch (Exception ex)
        {
            Log.Error("сохранение снимка", ex);
            Toasts.Error("Не удалось сохранить файл. Подробности — в журнале");
        }
    }

    /// <summary>Автосохранение после «Готово»: файл по времени снимка в папке сохранения; правка перезаписывает его.</summary>
    private void AutoSave(Guid id)
    {
        var captures = _session.Model.Captures;
        if (captures.FirstOrDefault(c => c.Id == id) is not { } capture) return;
        try
        {
            var path = Path.Combine(SaveFolder(), AutoSaveNames.For(capture, captures, _settings.SaveFormat));
            var rendered = CaptureRenderer.Render(_session.GetSource(id), capture, CaptureLetters.Assign(captures)[id]);
            CaptureFiles.Save(path, rendered, _settings.SaveFormat, _settings.JpegQuality);
        }
        catch (Exception ex)
        {
            Log.Error("автосохранение снимка", ex);
            Toasts.Error("Не удалось сохранить снимок в папку. Подробности — в журнале");
        }
    }

    /// <summary>«Сохранить всё…»: папка со всей лентой — A.png, B.png… и prompt.md.</summary>
    private void SaveAll()
    {
        var package = PackageSelector.All(_session.Model.Captures);
        if (package.Count == 0)
        {
            Toasts.Show("Сначала сделайте снимок.");
            return;
        }
        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Куда сохранить снимки", InitialDirectory = SaveFolder() };
            if (dialog.ShowDialog(_strip) != true) return;
            var items = package
                .Select(p => (p, CaptureRenderer.Render(_session.GetSource(p.Capture.Id), p.Capture, p.Letter)))
                .ToList();
            var folder = CaptureFiles.SavePackage(dialog.FolderName, DateTime.Now, items, _settings.SaveFormat, _settings.JpegQuality);
            SaveSettings(_settings with { LastSaveFolder = dialog.FolderName });
            Toasts.Show("Снимки сохранены", "Открыть папку", () => System.Diagnostics.Process.Start("explorer.exe", $"\"{folder}\""));
        }
        catch (Exception ex)
        {
            Log.Error("сохранение снимков", ex);
            Toasts.Error("Не удалось сохранить файл. Подробности — в журнале");
        }
    }

    // Окну-получателю нужно время, чтобы открыть поле подписи к фото (Telegram)
    private static readonly TimeSpan AfterDropPause = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// Картинку получатель забрал перетаскиванием; текст заметок Snapkit вставляет туда сам —
    /// в Telegram он встаёт подписью к фото. Не дошёл — остаётся в буфере для ручного Ctrl+V.
    /// </summary>
    private async void PasteNotesAfterDrop(Guid id)
    {
        try
        {
            if (_paste is null) return;
            var captures = _session.Model.Captures;
            if (captures.FirstOrDefault(c => c.Id == id) is not { } capture) return;
            var letter = CaptureLetters.Assign(captures).TryGetValue(id, out var l) ? l : 'A';
            var text = PromptBuilder.Build([new LetteredCapture(letter, capture)]);
            if (!DropNotes.ShouldPaste(text.Length == 0 ? null : Foreground.ActivateUnderCursor(), text)) return;
            await Task.Delay(AfterDropPause);
            var outcome = await _paste.PasteTextAsync(text, capture.NoteCount);
            Log.Info($"Заметки после переноса: {outcome}");
            // Текст положен и Ctrl+V нажат, но окно его не забрало — пусть вставят вручную
            if (outcome.Status == PasteStatus.CompletedUnverified) Toasts.Show("Текст заметок в буфере — нажмите Ctrl+V");
        }
        catch (Exception ex)
        {
            Log.Error("заметки после переноса", ex);
        }
        UpdatePasteState();
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
        _router = new KeyRouter(() => new PasteContext(_hasPasteContent, IsPackageCurrent(), Foreground.IsOwn()), ModifierState.Read)
        {
            Hotkeys = HotkeyRules.BuildMap(_settings),
        };
        _stripModel.EmptyHint = EmptyHintFor(_settings);
        _paste = new PasteCoordinator(_session, _clipboard!, _router, _publisher.CreatePastePlan, () => _settings.ClearStripAfterPaste,
            restoreAfter: _publisher.RestoreAfterPasteAsync);
        _hook = new KeyboardHookThread(_router, action => Dispatcher.BeginInvoke(() => OnKeyAction(action)));
        try { _hook.Start(); }
        catch (Exception ex)
        {
            Log.Error("перехват клавиатуры", ex);
            Toasts.Error("Не удалось перехватить клавиши: Print Screen и вставка пачки работать не будут");
        }
    }

    /// <summary>«Нажми Print Screen или «Новый снимок»» — с клавишами из настроек.</summary>
    private static string EmptyHintFor(AppSettings settings)
    {
        var keys = settings.CaptureHotkeysEnabled
            ? HotkeyParser.ParseList(settings.CaptureHotkeys).Take(HotkeyRules.PerAction).ToList()
            : [];
        if (keys.Count == 0) return "Нажми «Новый снимок»";
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
        var action = ClipboardRepublish.After(change.Kind, change.CaptureId is not null, _session.Model.Captures.Count > 0,
            stripPackageInClipboard: IsPackageCurrent() && _publisher.IsStripPackage);
        if (action == ClipboardAction.Package) RunClipboard(_publisher.PublishPackageAsync);
    }

    // Что можно вставить по Ctrl+V — для перехвата на потоке хука
    private void UpdatePasteState()
    {
        _hasPasteContent = _publisher.HasPasteContent;
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
            case KeyAction.CaptureFullScreen:
                _capture.StartFullScreenCapture();
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

    /// <summary>Подтверждение перед удалением снимков: спрашиваем, если лента не пустая.</summary>
    private bool ConfirmClear()
    {
        if (_session.Model.Captures.Count == 0) return true;
        return MessageBox.Show(_strip, "Удалить все снимки ленты?", "Snapkit",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private void ExitApp()
    {
        if (_session.Model.Captures.Count > 0
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
        // Больше, чем помещается на ленту, брать незачем: первые картинки вытеснили бы следующие
        foreach (var file in files.Take(SessionModel.MaxCaptures))
        {
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
        var id = _session.AddCapture(image, bounds, [], "");
        // Картинку извне сразу можно вставить: в буфере она одна, как после «Готово»
        RunClipboard(() => _publisher.PublishCaptureAsync(id));
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

    /// <summary>Номер версии программы: «0.3.1».</summary>
    private static string AppVersion
        => typeof(App).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "?";

    /// <summary>«Настройки…»: одно окно на всю программу, повторный вызов выводит его вперёд.</summary>
    private void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }
        var window = new Settings.SettingsWindow(new Settings.SettingsViewModel(_settings, AppVersion));
        window.Saved += (_, next) => ApplySettings(next);
        // Удаление идёт отдельным запуском с --uninstall: вопрос задаёт он, эту копию он закроет сам
        window.UninstallRequested += (_, _) =>
        {
            if (Environment.ProcessPath is not { } exe) return;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, Uninstaller.Argument) { UseShellExecute = false }); }
            catch (Exception ex)
            {
                Log.Error("запуск удаления", ex);
                Toasts.Error("Не удалось запустить удаление. Подробности — в журнале");
            }
        };
        // Пока поле ждёт сочетание, перехват отдаёт нажатия окну, а не снимку
        window.RecordingChanged += (_, recording) =>
        {
            if (_router is null) return;
            _router.RecordHotkey = recording ? hotkey => Dispatcher.BeginInvoke(() => window.Record(hotkey)) : null;
        };
        window.Closed += (_, _) =>
        {
            if (_router is not null) _router.RecordHotkey = null;
            _settingsWindow = null;
        };
        _settingsWindow = window;
        window.Show();
        window.Activate();
    }

    /// <summary>
    /// Автообновление: через 10 с после запуска и раз в сутки. Только у установленной копии из релиза
    /// (один exe) — сборки build.ps1 не обновляются.
    /// </summary>
    private void StartUpdateChecks()
    {
        var exe = Environment.ProcessPath;
        if (!_settings.AutoUpdate || _updateTimer is not null || !SelfInstall.IsSingleFile || exe is null || !AutoStart.IsInstalledCopy(exe))
            return;
        _updater ??= new Updater(() => _settings, SaveSettings);
        var timer = new DispatcherTimer { Interval = FirstUpdateCheck };
        timer.Tick += async (_, _) =>
        {
            timer.Interval = UpdateCheckInterval;
            await _updater.CheckAsync();
        };
        _updateTimer = timer;
        timer.Start();
    }

    private void StopUpdateChecks()
    {
        _updateTimer?.Stop();
        _updateTimer = null;
    }

    /// <summary>«Сохранить» в окне настроек: записать файл и раздать новые значения.</summary>
    private void ApplySettings(AppSettings next)
    {
        var previous = _settings;
        SaveSettings(next);
        Toasts.Enabled = next.ShowNotifications;
        if (previous.LaunchAtStartup != next.LaunchAtStartup) ApplyAutoStart();
        if (previous.ShowStrip != next.ShowStrip) SetShowStrip(next.ShowStrip);
        if (_router is not null) _router.Hotkeys = HotkeyRules.BuildMap(next);
        _stripModel.EmptyHint = EmptyHintFor(next);
        if (previous.Theme != next.Theme) ApplyTheme();
        if (previous.AutoUpdate != next.AutoUpdate)
        {
            if (next.AutoUpdate) StartUpdateChecks();
            else StopUpdateChecks();
        }
        if (previous.AccentColor != next.AccentColor)
        {
            ApplyAccent();
            _stripModel.ForgetThumbnails();
            RefreshStrip();
        }
    }

    /// <summary>Тема из настроек; «Как в Windows» — по теме приложений Windows.</summary>
    private void ApplyTheme() => ThemeManager.Apply(ThemePalette.Resolve(_settings.Theme, ThemeManager.WindowsAppsLight()));

    /// <summary>Цвет программы из настроек: для картинок (кружки заметок) и для окон.</summary>
    private void ApplyAccent()
    {
        Palette.SetAccent(_settings.AccentColor);
        ThemeManager.ApplyAccent(AccentOptions.Parse(_settings.AccentColor));
    }

    // Пользователь сменил тему Windows — при «Как в Windows» перекрашиваемся сразу
    private void OnUserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category == Microsoft.Win32.UserPreferenceCategory.General && _settings.Theme == AppTheme.System)
            Dispatcher.BeginInvoke(ApplyTheme);
    }

    /// <summary>Строка в «Параметры → Приложения» — только у установленной копии из релиза.</summary>
    private static void RegisterForUninstall()
    {
        var exe = Environment.ProcessPath;
        if (!SelfInstall.IsSingleFile || exe is null || !AutoStart.IsInstalledCopy(exe)) return;
        try { Uninstaller.Register(exe, AppVersion); }
        catch (Exception ex) { Log.Error("строка в списке программ", ex); }
    }

    /// <summary>
    /// «Удалить Snapkit»: вопрос, закрыть работающую копию и удалить всё, кроме сохранённых снимков.
    /// После удаления выходим сразу, мимо OnExit: обычный выход пишет журнал и вернул бы папку данных.
    /// </summary>
    private void RunUninstall()
    {
        var answer = MessageBox.Show(
            "Удалить Snapkit?\nСнимки ленты и настройки тоже удалятся. Сохранённые файлы в папке сохранения останутся.",
            "Snapkit", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
        {
            Shutdown();
            return;
        }
        try
        {
            SelfInstall.StopOtherCopies();
            var saveFolder = CaptureFiles.SaveFolder(new SettingsStore(SettingsPath).Load().Settings.LastSaveFolder);
            Uninstaller.Run(saveFolder);
        }
        catch (Exception ex)
        {
            Log.Error("удаление Snapkit", ex);
            MessageBox.Show("Не удалось удалить Snapkit полностью. Подробности — в журнале.", "Snapkit",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }
        MessageBox.Show("Snapkit удалён. Сохранённые снимки остались в папке сохранения.", "Snapkit",
            MessageBoxButton.OK, MessageBoxImage.Information);
        Environment.Exit(0);
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
