# Snapkit, этап 4 — окно настроек

> **Для исполнителей-агентов:** ОБЯЗАТЕЛЬНЫЙ НАВЫК: superpowers:subagent-driven-development
> (рекомендуется) или superpowers:executing-plans — выполнять задачу за задачей. Шаги
> отмечаются флажками (`- [ ]`).

**Цель:** окно «Настройки» с вкладками «Общие / Клавиши / Сохранение / Вид» и то, что
за ним стоит: снимок всего экрана, та же область, курсор на снимке, автосохранение и JPEG,
светлая тема и «Как в Windows», цвет программы, автообновление и удаление программы.

**Архитектура:** как в этапах 1–3. Правила — что считать верным сочетанием, какие клавиши
к какому действию, имена файлов автосохранения, цвета тем и их контраст, разбор релиза
GitHub, когда предлагать обновление, — в `Snapkit.Core` с тестами. `App` собирает: окно
настроек правит черновик `AppSettings`, по «Сохранить» `App.ApplySettings` раздаёт новые
значения перехвату клавиш, теме, снимку, сохранению и обновлению. Цвета темы
переключаются заменой кистей в ресурсах приложения (`DynamicResource`).

**Технологии:** C# 14 / .NET 10, WPF, WinForms (значок у часов), xUnit, `HttpClient`
и `Microsoft.Win32.Registry` из .NET — без новых пакетов.

**Описание:** [docs/superpowers/specs/2026-10-27-snapkit-stage-4-design.md](../specs/2026-10-27-snapkit-stage-4-design.md)
(главное), наброски в [2026-10-27-snapkit-stage-4-mockups](../specs/2026-10-27-snapkit-stage-4-mockups/),
основное [2026-10-23-snapkit-design.md](../specs/2026-10-23-snapkit-design.md) (раздел 10).
Где они расходятся, верно описание этапа 4.

## Общие ограничения

- Всё из «Общих ограничений» планов этапов 1–3, кроме ветки: работа идёт в ветке
  `feat/stage-4-settings`. Автор коммитов — уже настроенный в репозитории `Ozimnadius`;
  глобальную настройку git не трогать. Коммиты — Conventional Commits, описание на русском.
- Весь текст для пользователя и комментарии в коде — только на русском.
- `dotnet` может не быть в PATH: `"C:\Program Files\dotnet\dotnet.exe"`.
- Snapkit для ручных проверок запускать только через `build.ps1`.
- Тесты интерфейса, которым нужна тема, — через `UiApp.Run`, с картинками без темы —
  через `StaRunner.Run`. Тесты, меняющие общее состояние (`Palette`, ресурсы приложения),
  — в отдельной коллекции xUnit без параллельного запуска.
- Старый `settings.json` (этапы 1–3) должен читаться: новые поля `AppSettings` — только
  в конец записи и только с умолчаниями, новые значения перечислений — только в конец.
- Решения плана (значений нет в описании):
  - сочетания в файле — как сейчас, текстом через запятую: `CaptureHotkeys`
    («PrintScreen, Ctrl+F12»), новое `FullScreenHotkeys` (пусто — не назначено);
  - цвет программы в файле — `"#RRGGBB"` или градиент `"#RRGGBB|#RRGGBB"`; рамка выделения,
    маркеры и обводки берут первый цвет, заливки (кнопки, кружки заметок) — градиент
    под 135°; цвет при наведении — первый цвет, смешанный с белым на 15 %;
  - 12 цветов программы — в задаче 6 (подобраны так, что белый текст на них читается);
  - светлая тема: панель `#FFFFFF`, приподнятая панель `#F3F5F8`, рамка `#D5DAE1`,
    наведение `#E8EBF0`, текст `#1F2328`, приглушённый текст `#6B7280`;
  - «Как в Windows» — значение `AppsUseLightTheme` в
    `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize` (1 — светлая, нет
    значения — тёмная); смена ловится через `SystemEvents.UserPreferenceChanged`;
  - обновление: `https://api.github.com/repos/Ozimnadius/snapkit/releases/latest`, файл
    релиза `Snapkit.exe`, хеш — поле `digest` («sha256:…»); без хеша обновление
    не предлагается; скачивание — в `%LOCALAPPDATA%\Snapkit\update\`; проверяет только
    установленная однофайловая копия (`SelfInstall.IsSingleFile` и
    `AutoStart.IsInstalledCopy`), сборки `build.ps1` — нет;
  - строка в «Приложениях» — ключ `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\Snapkit`,
    издатель `Ozimnadius`, удаление — `"…\Snapkit.exe" --uninstall`;
  - файл автосохранения — `yyyy-MM-dd_HH-mm-ss` по `CreatedAt` снимка (местное время),
    `.png` или `.jpg`; два снимка в одну секунду — второй `… (2)`, третий `… (3)` по
    `(CreatedAt, Id)`;
  - `LastSaveFolder` и есть «Папка сохранения» (пусто — `Изображения\Snapkit`).

## На что смотреть при проверке

1. **После «Сохранить» работают новые клавиши, а старые — нет;** выключенное действие
   не срабатывает. Тест в задаче 1 (`HotkeyMap_BuildsFromSettings`).
2. **Пока поле ждёт сочетание, Print Screen не запускает снимок и не открывает «Ножницы»
   Windows.** Тест в задаче 7 (`Recording_SwallowsPrintScreenWithoutCapture`).
3. **В светлой теме всё читается,** белый текст на любом цвете программы — тоже. Тесты
   в задачах 5 и 6 (`Palettes_TextIsReadable`, `AllAccents_WhiteTextIsReadable`).
4. **Автосохранение не плодит копии** при правке и не затирает соседний снимок той же
   секунды. Тесты в задаче 4 (`AutoSaveName_*`).
5. **Клавиши пользователя переживают переход на этап 4.** Тест в задаче 1
   (`Load_Stage3Settings_KeepsHotkeysAndDefaults`).

---

## Подготовка

- [ ] **Ветка** `feat/stage-4-settings` уже есть (в ней описание этапа). Работать в ней.
- [ ] **Проверить, что всё зелёное:** `dotnet test Snapkit.slnx --filter "Category!=Clipboard&Category!=Desktop"`.

---

### Задача 1: Настройки и правила клавиш в ядре

**Файлы:**
- Изменить: `src/Snapkit.Core/Settings/AppSettings.cs`, `src/Snapkit.Core/Input/HotkeyParser.cs`
- Создать: `src/Snapkit.Core/Settings/SettingsEnums.cs`, `src/Snapkit.Core/Input/HotkeyRules.cs`
- Тесты: `tests/Snapkit.Core.Tests/SettingsStoreTests.cs`, `tests/Snapkit.Core.Tests/HotkeyParserTests.cs`,
  создать `tests/Snapkit.Core.Tests/HotkeyRulesTests.cs`

**Интерфейсы:**
- Создаёт в конце `AppSettings` (умолчания — как здесь):
```csharp
string FullScreenHotkeys = "",
bool CaptureHotkeysEnabled = true,
bool FullScreenHotkeysEnabled = false,
bool ProposeLastRegion = false,
RectI? LastRegion = null,
bool ShowCursor = false,
bool AutoUpdate = true,
string SnoozedUpdateVersion = "",
DateTimeOffset? SnoozedUpdateUntil = null,
bool AutoSave = false,
SaveFormat SaveFormat = SaveFormat.Png,
int JpegQuality = 90,
AppTheme Theme = AppTheme.Dark
```
- Создаёт: `public enum SaveFormat { Png, Jpeg }`, `public enum AppTheme { Dark, Light, System }`
  (пространство `Snapkit.Core.Settings`).
- Меняет и создаёт в `HotkeyParser`:
```csharp
/// <summary>Список через запятую; непонятные пропускаются. Пустой — Print Screen, если orPrintScreen, иначе пустой.</summary>
public static IReadOnlyList<Hotkey> ParseList(string text, bool orPrintScreen = true);
/// <summary>Обратно в текст для файла: «PrintScreen, Ctrl+F12».</summary>
public static string FormatList(IEnumerable<Hotkey> hotkeys);
```
- Создаёт `HotkeyRules`:
```csharp
/// <summary>null — сочетание годится; иначе текст для пользователя.</summary>
public static string? Check(Hotkey hotkey);
/// <summary>Ошибки по полям (до двух на действие); повтор в любом другом поле — «Это сочетание уже назначено».</summary>
public static IReadOnlyList<string?> CheckAll(IReadOnlyList<Hotkey?> fields);
/// <summary>Какие клавиши к какому действию: выключенное действие и пустые поля не попадают.</summary>
public static IReadOnlyDictionary<Hotkey, KeyAction> BuildMap(AppSettings settings);
```

- [ ] **Шаг 1: Тесты.**
  - `Load_Stage3Settings_KeepsHotkeysAndDefaults`: `settings.json` этапа 3 с
    `"captureHotkeys": "PrintScreen, Ctrl+F12"` и без новых полей → `CaptureHotkeys` прежний,
    `FullScreenHotkeys == ""`, `AutoUpdate`, `CaptureHotkeysEnabled` — `true`, `Theme == Dark`,
    `JpegQuality == 90`, `SaveFormat == Png`;
  - `SaveThenLoad_NewFields_RoundTrip`: все новые поля не по умолчанию (в том числе
    `LastRegion = new RectI(10, 20, 300, 200)`, `Theme = System`) — читаются те же;
  - `ParseList_EmptyWithoutDefault_IsEmpty`, `FormatList_RoundTrips`
    (`FormatList(ParseList("PrintScreen, Ctrl+F12"))` → `"Print Screen, Ctrl+F12"`, и он
    снова разбирается в те же два сочетания);
  - `Check_*`: `Ctrl+S` — `null`; `S` без модификаторов — «Добавьте Ctrl, Alt или Shift»;
    `Print Screen` и `F5` без модификаторов — `null`; `F13` без модификаторов — «Добавьте
    Ctrl, Alt или Shift»; `Win+S` — «Добавьте Ctrl, Alt или Shift»; клавиша не из
    `HotkeyParser` (например `Vk 0x25`, стрелка) — «Эту клавишу назначить нельзя»;
  - `CheckAll_Duplicate_MarksSecond`: `[Ctrl+F12, null, Ctrl+F12, null]` → ошибка только
    у третьего поля: «Это сочетание уже назначено»;
  - `HotkeyMap_BuildsFromSettings`: «PrintScreen, Ctrl+F12» и «Ctrl+PrintScreen» при обоих
    включённых → три клавиши, последняя — `CaptureFullScreen`; `FullScreenHotkeysEnabled = false`
    → только две; `CaptureHotkeysEnabled = false` и пусто у экрана → пустой словарь.
- [ ] **Шаг 2: Запустить — падают:** `dotnet test tests/Snapkit.Core.Tests --filter "FullyQualifiedName~SettingsStoreTests|FullyQualifiedName~HotkeyParserTests|FullyQualifiedName~HotkeyRulesTests"`.
- [ ] **Шаг 3: Реализовать.** `BuildMap` разбирает до двух сочетаний на действие
  (`ParseList(..., orPrintScreen: true)` для области, `false` для экрана); при повторе
  сочетания берётся первое действие.
- [ ] **Шаг 4: Запустить — зелёные,** затем весь `Snapkit.Core.Tests`.
- [ ] **Шаг 5: Коммит** `feat(core): настройки этапа 4 и правила сочетаний клавиш`.

---

### Задача 2: Снимок всего экрана и та же область

**Файлы:**
- Создать: `src/Snapkit.Core/Session/RegionMemory.cs`
- Изменить: `src/Snapkit.App/Capturing/CaptureController.cs`, `src/Snapkit.App/App.xaml.cs`
- Тесты: создать `tests/Snapkit.Core.Tests/RegionMemoryTests.cs`

**Интерфейсы:**
- Потребляет: `AppSettings.ProposeLastRegion`, `LastRegion` (задача 1), `HotkeyRules.BuildMap`.
- Создаёт:
```csharp
/// <summary>Прежняя область, если она целиком на одном из нынешних мониторов; иначе null.</summary>
public static RectI? RegionMemory.FitOnScreen(RectI region, IReadOnlyList<RectI> monitors);
```
- Меняет в `CaptureController`:
```csharp
/// <summary>preset — рамка в физических пикселях экрана: окно её монитора сразу ставит её и открывает редактор.</summary>
public void StartNewCapture(RectI? preset = null);
/// <summary>Весь монитор под мышью — сразу в редакторе.</summary>
public void StartFullScreenCapture();
```

- [ ] **Шаг 1: Тесты** `RegionMemoryTests`: два монитора `(0,0,1920,1080)` и `(1920,0,2560,1440)`;
  область на втором — та же; на стыке мониторов — `null`; за пределами всех — `null`;
  пустой список мониторов — `null`.
- [ ] **Шаг 2: Запустить — падают.**
- [ ] **Шаг 3: Реализовать** `FitOnScreen`; в `CaptureController` после показа окон найти
  окно монитора, где лежит `preset`, и вызвать `PresetRegion` с рамкой в координатах
  монитора (как в `Reopen`). `StartFullScreenCapture()` = `StartNewCapture(MonitorUnderCursor().Bounds)`.
  В `App`: `OnKeyAction` — `CaptureFullScreen` → `StartFullScreenCapture()`; все «Новый снимок»
  (клавиша, значок у часов, лента, капсула) передают `FitOnScreen(LastRegion, мониторы)`,
  когда `ProposeLastRegion`; при `CaptureAdded` запомнить `LastRegion = ScreenBounds` снимка
  (`SaveSettings`, только если изменилась). Перехват клавиш строится из `HotkeyRules.BuildMap(_settings)`.
- [ ] **Шаг 4: Запустить — зелёные.** Весь набор без `Clipboard` и `Desktop` — зелёный.
- [ ] **Шаг 5: Коммит** `feat(capture): снимок всего экрана и та же область`.

---

### Задача 3: Курсор на снимке

**Файлы:**
- Создать: `src/Snapkit.Core/Session/CursorPlacement.cs`
- Изменить: `src/Snapkit.App/Capturing/ScreenCapturer.cs`, `src/Snapkit.App/Capturing/CaptureController.cs`, `src/Snapkit.App/App.xaml.cs`
- Тесты: создать `tests/Snapkit.Core.Tests/CursorPlacementTests.cs`

**Интерфейсы:**
- Создаёт:
```csharp
/// <summary>Где рисовать значок курсора в кадре bounds: позиция курсора минус «горячая точка» минус угол кадра.</summary>
public static (int X, int Y) CursorPlacement.TopLeft((int X, int Y) cursor, (int X, int Y) hotspot, RectI bounds);
```
- Меняет: `ScreenCapturer.Capture(RectI physicalBounds, bool includeCursor = false)`;
  `CaptureController.ShowCursor` — `Func<bool>?`, задаёт `App` из `_settings.ShowCursor`.

- [ ] **Шаг 1: Тесты:** курсор `(500, 300)`, горячая точка `(4, 2)`, кадр `(400, 200, 800, 600)`
  → `(96, 98)`; кадр второго монитора с отрицательным `X` — верный сдвиг.
- [ ] **Шаг 2: Запустить — падают.**
- [ ] **Шаг 3: Реализовать.** После `BitBlt`: `GetCursorInfo` (только `CURSOR_SHOWING`),
  `GetIconInfo` — горячая точка (освободить `hbmMask`/`hbmColor`), `DrawIconEx(..., DI_NORMAL)`
  в контекст кадра в точке из `CursorPlacement`. Кадры всех мониторов снимаются с курсором,
  если `ShowCursor()`; `Reopen` — без курсора.
- [ ] **Шаг 4: Запустить — зелёные.**
- [ ] **Шаг 5: Коммит** `feat(capture): курсор мыши на снимке`.

---

### Задача 4: Сохранение — JPEG, папка, автосохранение

**Файлы:**
- Создать: `src/Snapkit.Core/AutoSaveNames.cs`
- Изменить: `src/Snapkit.Core/PackageExport.cs`, `src/Snapkit.App/Imaging/ImageCodecs.cs`,
  `src/Snapkit.App/Files/CaptureFiles.cs`, `src/Snapkit.App/App.xaml.cs`
- Тесты: `tests/Snapkit.Core.Tests/PackageExportTests.cs`, создать `tests/Snapkit.Core.Tests/AutoSaveNamesTests.cs`,
  `tests/Snapkit.App.Tests/CaptureFilesTests.cs`

**Интерфейсы:**
- Потребляет: `SaveFormat`, `AppSettings.AutoSave`, `SaveFormat`, `JpegQuality`, `LastSaveFolder`.
- Создаёт и меняет:
```csharp
public static string PackageExport.Extension(SaveFormat format);                 // ".png" / ".jpg"
public static string PackageExport.ImageFileName(char letter, SaveFormat format = SaveFormat.Png);
public static string PackageExport.BuildPromptMarkdown(IReadOnlyList<LetteredCapture> package, SaveFormat format = SaveFormat.Png);
/// <summary>Имя файла автосохранения снимка: время снимка, «(2)»… для соседей той же секунды, расширение формата.</summary>
public static string AutoSaveNames.For(Capture capture, IReadOnlyList<Capture> strip, SaveFormat format);
public static byte[] ImageCodecs.EncodeJpeg(BitmapSource image, int quality);    // quality обрезается до 1…100
public static string CaptureFiles.SaveFolder(string configured);                 // пусто — DefaultSaveFolder
public static void CaptureFiles.Save(string path, BitmapSource rendered, SaveFormat format, int jpegQuality);
public static string CaptureFiles.SavePackage(string parent, DateTime now,
    IReadOnlyList<(LetteredCapture Item, BitmapSource Rendered)> package, SaveFormat format, int jpegQuality);
```

- [ ] **Шаг 1: Тесты.**
  - `AutoSaveName_UsesCaptureTime`: снимок с `CreatedAt` → `TimestampName(CreatedAt.LocalDateTime) + ".png"`;
  - `AutoSaveName_SameSecond_GetsNumber`: два снимка в одну секунду (второй на 300 мс позже) —
    у второго `… (2).png`, у первого без номера;
  - `AutoSaveName_StableAfterReorder`: тот же результат, если поменять снимки местами на ленте;
  - `AutoSaveName_Jpeg_UsesJpg`;
  - `BuildPromptMarkdown_Jpeg_LinksJpg`: `![Снимок A](A.jpg)`;
  - `Save_Jpeg_WritesJpeg` (App): файл начинается с `FF D8`, качество 10 даёт файл меньше,
    чем качество 95, на одной картинке;
  - `SavePackage_Jpeg_WritesJpgAndLinks`: в папке `A.jpg`, в `prompt.md` — ссылка на `A.jpg`.
- [ ] **Шаг 2: Запустить — падают.**
- [ ] **Шаг 3: Реализовать.** В `App`: окна «Сохранить снимок…»/«Сохранить всё…» начинают
  с `SaveFolder(LastSaveFolder)`, фильтр и расширение — по формату; запись — через `Save`
  и новый `SavePackage`. Автосохранение — в обработчике `CaptureDone`, если `AutoSave`:
  `CaptureRenderer.Render` с буквой места на ленте → `Save` в
  `SaveFolder(LastSaveFolder)\AutoSaveNames.For(...)` (папку создать, файл перезаписать);
  ошибка — `Toasts.Error("Не удалось сохранить снимок в папку. Подробности — в журнале")`.
  Перетаскивание и буфер остаются в PNG.
- [ ] **Шаг 4: Запустить — зелёные.**
- [ ] **Шаг 5: Коммит** `feat(files): JPEG, папка сохранения и автосохранение`.

---

### Задача 5: Темы — тёмная, светлая, «Как в Windows»

**Файлы:**
- Создать: `src/Snapkit.Core/Theming/ThemePalette.cs`, `src/Snapkit.Core/Theming/ColorContrast.cs`,
  `src/Snapkit.App/Shell/ThemeManager.cs`
- Изменить: `src/Snapkit.App/Theme.xaml` и все окна и всплывающие панели с цветами темы
  (`Strip/*.xaml`, `Editor/*.xaml`, `Notifications/ToastWindow.xaml`), код, где цвет
  панели или текста задан в C# (`EditorToolbar.xaml.cs` — `Brushes.White` у значков
  и образцов толщины), `src/Snapkit.App/App.xaml.cs`
- Тесты: создать `tests/Snapkit.Core.Tests/ThemePaletteTests.cs`, `tests/Snapkit.App.Tests/ThemeTests.cs`

**Интерфейсы:**
- Создаёт:
```csharp
public sealed record ThemePalette(string Panel, string PanelRaised, string PanelBorder, string Hover,
    string Text, string MutedText, string Chip, string WarningText)
{
    public static ThemePalette Dark { get; }   // нынешние цвета; Chip "#33FFFFFF", WarningText "#FBBF5A"
    public static ThemePalette Light { get; }  // см. «Общие ограничения»; Chip "#E8EBF0", WarningText "#B45309"
    public static ThemePalette Resolve(AppTheme choice, bool windowsAppsLight);
}
/// <summary>Контраст по WCAG для цветов «#RRGGBB» (альфа не учитывается).</summary>
public static double ColorContrast.Ratio(string foreground, string background);
// App
public static class ThemeManager
{
    public const string PanelKey = "PanelBrush", PanelRaisedKey = "PanelRaisedBrush", PanelBorderKey = "PanelBorderBrush",
        HoverKey = "HoverBrush", TextKey = "TextBrush", MutedTextKey = "MutedTextBrush", ChipKey = "ChipBrush",
        WarningTextKey = "WarningTextBrush";
    /// <summary>Заменить кисти темы в ресурсах приложения — окна с DynamicResource перекрашиваются сразу.</summary>
    public static void Apply(ThemePalette palette);
    /// <summary>Тема приложений Windows светлая (AppsUseLightTheme == 1).</summary>
    public static bool WindowsAppsLight();
}
```

- [ ] **Шаг 1: Тесты.**
  - `Palettes_TextIsReadable`: для `Dark` и `Light` контраст `Text`/`Panel` и `Text`/`PanelRaised`
    не меньше 4.5, `MutedText`/`Panel` и `WarningText`/`Panel` — не меньше 3;
  - `Resolve_*`: `Dark`/`Light` — сами; `System` — по `windowsAppsLight`;
  - `ThemeKeys_AreDynamicInXaml` (App): ни в одном `*.xaml` из `src/Snapkit.App` нет
    `StaticResource` для восьми ключей темы и `AccentBrush`/`AccentHoverBrush`
    (поиск исходников — как в `UserFacingTextTests`);
  - `Apply_Light_ChangesResources` (`UiApp`): после `Apply(ThemePalette.Light)` у ресурса
    `PanelBrush` цвет `#FFFFFF`; после `Apply(ThemePalette.Dark)` — `#1E2128`.
- [ ] **Шаг 2: Запустить — падают.**
- [ ] **Шаг 3: Реализовать.** В `Theme.xaml` остаются тёмные значения по умолчанию и новые
  ключи `ChipBrush`, `WarningTextBrush`; все ссылки на ключи темы во всех XAML — `DynamicResource`;
  счётчики (`#33FFFFFF`) — `ChipBrush`, подсказка ленты (`#FBBF5A`) — `WarningTextBrush`;
  полупрозрачные подложки поверх снимка (`#B31E2128` под буквой на карточке и т. п.)
  и затемнение экрана остаются как есть. В C# — `SetResourceReference` вместо `Brushes.White`
  для текста на панелях. `App` применяет тему при запуске (`ThemePalette.Resolve(_settings.Theme,
  ThemeManager.WindowsAppsLight())`) и, пока выбрано `System`, — по `SystemEvents.UserPreferenceChanged`
  (отписаться при выходе).
- [ ] **Шаг 4: Запустить — зелёные.** Ручная проверка через `build.ps1` с `"theme": "Light"`
  в `settings.json`: лента, капсула, уведомление, редактор, меню «…» — читаются.
- [ ] **Шаг 5: Коммит** `feat(theme): светлая тема и «Как в Windows»`.

---

### Задача 6: Цвет программы

**Файлы:**
- Создать: `src/Snapkit.Core/Theming/AccentOptions.cs`
- Изменить: `src/Snapkit.App/Rendering/Palette.cs`, `src/Snapkit.App/Shell/ThemeManager.cs`,
  места с `Palette.Accent` (`AnnotationPainter`, `EditorCanvas`, `EditorSurface`, `CaptureOverlayWindow`),
  `src/Snapkit.App/Strip/StripViewModel.cs`, `src/Snapkit.App/App.xaml.cs`
- Тесты: создать `tests/Snapkit.Core.Tests/AccentOptionsTests.cs`, `tests/Snapkit.App.Tests/PaletteTests.cs`,
  `tests/Snapkit.App.Tests/StripViewModelTests.cs`

**Интерфейсы:**
- Создаёт:
```csharp
public sealed record AccentOption(string Value, string First, string? Second);
public static class AccentOptions
{
    /// <summary>12 цветов: 6 однотонных, затем 6 градиентов — в этом порядке.</summary>
    public static IReadOnlyList<AccentOption> All { get; }
    public const string Default = "#2F7BF5";
    /// <summary>«#RRGGBB» или «#RRGGBB|#RRGGBB»; непонятное — Default.</summary>
    public static AccentOption Parse(string value);
}
// App
public static class Palette
{
    public static string Accent { get; }            // первый цвет — рамка, маркеры, обводки
    public static Brush AccentFill { get; }         // заливка: однотонная или градиент, заморожена
    public static Brush CreateAccentFill(AccentOption option);  // без общего состояния — для тестов
    public static void SetAccent(string value);
}
public static void ThemeManager.ApplyAccent(AccentOption option);  // AccentBrush = заливка, AccentHoverBrush = первый цвет + 15 % белого
public void StripViewModel.ForgetThumbnails();
```
- Значения `All`: однотонные `#2F7BF5`, `#16A34A`, `#A855F7`, `#EA580C`, `#EC4899`, `#0D9488`;
  градиенты `#6366F1|#3B82F6`, `#EA580C|#DC2626`, `#16A34A|#0D9488`, `#D97706|#DB2777`,
  `#EC4899|#8B5CF6`, `#0891B2|#2563EB`.

- [ ] **Шаг 1: Тесты.**
  - `AllAccents_WhiteTextIsReadable`: для каждого цвета каждого варианта
    `ColorContrast.Ratio("#FFFFFF", цвет) >= 3`;
  - `All_HasTwelveWithDefaultFirst`; `Parse_Gradient`, `Parse_Invalid_IsDefault` (`"зелёный"`, `""`, `"#12"`);
  - `CreateAccentFill_Gradient_IsFrozenTwoStops` (App), `CreateAccentFill_Solid_IsSolid`;
  - `ForgetThumbnails_RedrawsOnNextRefresh` (`StripViewModelTests`): после `ForgetThumbnails`
    следующий `Refresh` того же списка снова вызывает отрисовку.
- [ ] **Шаг 2: Запустить — падают.**
- [ ] **Шаг 3: Реализовать.** `Palette.Accent` из константы становится свойством; кружки
  заметок в `AnnotationPainter` заливаются `AccentFill`, маркеры и рамки — `Accent`.
  `App` при запуске и при смене цвета: `Palette.SetAccent`, `ThemeManager.ApplyAccent`,
  `_stripModel.ForgetThumbnails()`, `RefreshStrip()`.
- [ ] **Шаг 4: Запустить — зелёные.**
- [ ] **Шаг 5: Коммит** `feat(theme): цвет программы — 12 вариантов`.

---

### Задача 7: Окно «Настройки»

**Файлы:**
- Создать: `src/Snapkit.App/Settings/SettingsViewModel.cs`, `src/Snapkit.App/Settings/SettingsWindow.xaml(.cs)`,
  `src/Snapkit.App/Settings/HotkeyBox.xaml(.cs)`
- Изменить: `src/Snapkit.Core/Input/KeyRouter.cs`, `src/Snapkit.App/Shell/TrayIcon.cs`,
  `src/Snapkit.App/Strip/StripWindow.xaml.cs`, `src/Snapkit.App/App.xaml.cs`
- Тесты: `tests/Snapkit.Core.Tests/KeyRouterTests.cs`, создать `tests/Snapkit.App.Tests/SettingsViewModelTests.cs`

**Интерфейсы:**
- Потребляет: всё из задач 1–6 (`HotkeyRules`, `AccentOptions`, `ThemePalette`, `CaptureFiles.SaveFolder`).
- Создаёт в `KeyRouter`:
```csharp
/// <summary>
/// Запись сочетания в поле настроек: пока задано, любая не-модификаторная клавиша (вместе с зажатыми
/// модификаторами) отдаётся сюда и глотается; снимки и перехват Ctrl+V не срабатывают.
/// </summary>
public Action<Hotkey>? RecordHotkey { get; set; }
```
- Создаёт:
```csharp
public sealed class SettingsViewModel : ObservableObject
{
    public SettingsViewModel(AppSettings current, string version);
    // галочки «Общих», поля «Сохранения» и «Вида» — по одному свойству на поле AppSettings
    public Hotkey? Region1 { get; set; } public Hotkey? Region2 { get; set; }
    public Hotkey? Full1 { get; set; } public Hotkey? Full2 { get; set; }
    public IReadOnlyList<string?> HotkeyErrors { get; }   // по четырём полям, из HotkeyRules.CheckAll
    public bool CanSave { get; }
    public void SuggestFullScreen();                       // Full1 = Ctrl+Print Screen, действие включается
    /// <summary>Новые настройки: всё, чего окно не касается (место ленты, свои цвета…), — из current.</summary>
    public AppSettings Build();
}
public sealed partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel model);
    public event EventHandler<AppSettings>? Saved;
    public event EventHandler? UninstallRequested;          // ссылка «Удалить Snapkit с компьютера…» (задача 9)
    public event EventHandler<bool>? RecordingChanged;      // поле ждёт сочетание
}
```
- `TrayIcon.SettingsRequested`, `StripWindow.SettingsRequested` — пункт «Настройки…»
  (в меню значка — перед «Выйти», в «…» — перед «Выйти»).

- [ ] **Шаг 1: Тесты.**
  - `Recording_SwallowsPrintScreenWithoutCapture` (`KeyRouterTests`): при заданном
    `RecordHotkey` нажатие Print Screen → решение `Suppress = true`, `Action = None`,
    в запись пришло `Print Screen`; Ctrl+F12 → пришло `Ctrl+F12`; голый Ctrl — ничего не пришло;
    после `RecordHotkey = null` Print Screen снова даёт `CaptureRegion`;
  - `SettingsViewModelTests`: `Build_KeepsUntouchedFields` (место ленты, свои цвета,
    `LastRegion` — как были); `Stage3Keys_ShownInTwoFields` («PrintScreen, Ctrl+F12» →
    `Region1`, `Region2`); `Duplicate_BlocksSave` (у `Full1` — «Это сочетание уже назначено»,
    `CanSave == false`); `SuggestFullScreen_SetsCtrlPrintScreen`; `Build_WritesHotkeysAsText`
    (`CaptureHotkeys == "Print Screen, Ctrl+F12"`).
- [ ] **Шаг 2: Запустить — падают.**
- [ ] **Шаг 3: Реализовать.** Окно — как `settings-tabs.html`: свой заголовок с ✕, вкладки
  кнопками, «Отмена» и «Сохранить» (недоступна, пока есть ошибки), цвета — `DynamicResource`.
  `HotkeyBox`: щелчок — «Нажмите сочетание…», `RecordingChanged(true)`; пришло сочетание —
  Esc отменяет ввод, Backspace/Delete очищают поле, остальное записывается; ошибка — красным
  текстом под полем. Вкладка «Вид» — карточки тем с превью, 12 кружков `AccentOptions.All`,
  образец «так будет выглядеть» (рамка, кружок «A1», кнопка «Готово» цветом программы).
  Внизу «Общих» — «Версия X.Y.Z · Удалить Snapkit с компьютера…».
  `App.OpenSettings()`: одно окно (повторный вызов — `Activate`), `RecordingChanged` ставит
  и снимает `_router.RecordHotkey` (с `Dispatcher.BeginInvoke` в окно). `App.ApplySettings(AppSettings next)`:
  `SaveSettings`, `Toasts.Enabled`, `ApplyAutoStart`, `SetShowStrip` (если изменилось),
  `_router.Hotkeys = HotkeyRules.BuildMap(next)`, подсказка пустой ленты, тема и цвет
  (задачи 5–6), обновление (задача 8).
- [ ] **Шаг 4: Запустить — зелёные.** Ручная проверка: каждое поле окна меняет поведение
  после «Сохранить», «Отмена» ничего не меняет.
- [ ] **Шаг 5: Коммит** `feat(settings): окно настроек`.

---

### Задача 8: Автообновление

**Файлы:**
- Создать: `src/Snapkit.Core/Updates/ReleaseInfo.cs`, `src/Snapkit.Core/Updates/UpdatePolicy.cs`,
  `src/Snapkit.App/Shell/Updater.cs`
- Изменить: `src/Snapkit.App/Shell/SelfInstall.cs`, `src/Snapkit.App/Notifications/Toasts.cs`, `src/Snapkit.App/App.xaml.cs`
- Тесты: создать `tests/Snapkit.Core.Tests/ReleaseInfoTests.cs`, `tests/Snapkit.Core.Tests/UpdatePolicyTests.cs`

**Интерфейсы:**
- Создаёт:
```csharp
public sealed record ReleaseInfo(Version Version, string ExeUrl, string Sha256)
{
    /// <summary>Ответ GitHub «latest release»: tag_name «v0.3.2», файл Snapkit.exe и его digest «sha256:…»; иначе null.</summary>
    public static ReleaseInfo? Parse(string json);
}
public static class UpdatePolicy
{
    /// <summary>Версия новее и не отложена: snoozedVersion совпадает и now раньше snoozedUntil — не предлагать.</summary>
    public static bool ShouldOffer(Version current, ReleaseInfo latest, string snoozedVersion, DateTimeOffset? snoozedUntil, DateTimeOffset now);
    public static bool HashMatches(ReadOnlySpan<byte> file, string sha256Hex);
}
public static void Toasts.Offer(string message, string actionText, Action onAction);  // и при выключенных уведомлениях, 15 с
public const string SelfInstall.UpdatedArgument = "--updated";
```
- `App`: проверка через 10 с после запуска и каждые 24 ч (`DispatcherTimer`), только если
  `AutoUpdate`, `SelfInstall.IsSingleFile` и `AutoStart.IsInstalledCopy`.

- [ ] **Шаг 1: Тесты.**
  - `Parse_LatestRelease`: JSON из `gh release view v0.3.1 --json` по образцу GitHub API
    (`tag_name`, `assets[].name`, `browser_download_url`, `digest`) → `0.3.1`, ссылка,
    хеш без «sha256:»; без `Snapkit.exe` или без `digest` — `null`; мусор — `null`;
  - `ShouldOffer_*`: новее — да; та же или старше — нет; отложена эта версия до завтра — нет;
    отложена старая версия, вышла новее — да; отложено до вчера — да;
  - `HashMatches_*`: верный хеш (регистр не важен) — да, чужой — нет.
- [ ] **Шаг 2: Запустить — падают.**
- [ ] **Шаг 3: Реализовать.** `Updater.CheckAsync`: `HttpClient` с `User-Agent: Snapkit/<версия>`,
  тайм-аут 30 с; ошибки сети — только в журнал. Есть что предложить — сразу отложить эту
  версию на сутки (`SnoozedUpdateVersion`, `SnoozedUpdateUntil = now + 24 ч`) и
  `Toasts.Offer($"Вышла версия {v} — обновить?", "Обновить", ...)`. «Обновить»: «Скачиваю
  обновление…», файл в `%LOCALAPPDATA%\Snapkit\update\Snapkit-{v}.exe`, `HashMatches`;
  не совпал — удалить файл, `Toasts.Error("Обновление не прошло проверку и не установлено")`;
  совпал — запустить его (самоустановка закроет эту копию). `SelfInstall.InstallAndLaunch`
  передаёт `--updated`, если в папке программы уже был `Snapkit.exe`; с ним при запуске —
  «Snapkit обновлён до версии X.Y.Z» вместо сообщения об установке.
- [ ] **Шаг 4: Запустить — зелёные.**
- [ ] **Шаг 5: Коммит** `feat(update): автообновление с GitHub с проверкой хеша`.

---

### Задача 9: Удаление программы

**Файлы:**
- Создать: `src/Snapkit.App/Shell/Uninstaller.cs`
- Изменить: `src/Snapkit.App/Shell/SelfInstall.cs` (`StopOtherCopies` — `internal`), `src/Snapkit.App/App.xaml.cs`
- Тесты: создать `tests/Snapkit.App.Tests/UninstallerTests.cs`

**Интерфейсы:**
```csharp
public static class Uninstaller
{
    public const string Argument = "--uninstall";
    /// <summary>Строка в «Параметры → Приложения»: имя, версия, значок, издатель, команда удаления.</summary>
    public static void Register(string exePath, string version);
    /// <summary>Удалить содержимое папок данных, кроме keep и всего, что его содержит или лежит в нём.</summary>
    public static void DeleteData(IEnumerable<string> directories, string keep);
    /// <summary>Всё по разделу 2.10 описания; папку программы удаляет отложенная команда после выхода.</summary>
    public static void Run(string saveFolder);
}
```

- [ ] **Шаг 1: Тесты** (временные папки): `DeleteData_RemovesEverything` — файлы и подпапки
  исчезают, сами папки тоже; `DeleteData_KeepsSaveFolder` — папка сохранения внутри
  `%LOCALAPPDATA%\Snapkit`-подобной папки и её файлы остаются, остальное удалено;
  `DeleteData_MissingDirectory_IsIgnored`.
- [ ] **Шаг 2: Запустить — падают.**
- [ ] **Шаг 3: Реализовать.** `Register` — значения `DisplayName = "Snapkit"`, `DisplayVersion`,
  `Publisher = "Ozimnadius"`, `DisplayIcon = "\"exe\",0"`, `UninstallString = "\"exe\" --uninstall"`,
  `InstallLocation`, `NoModify = 1`, `NoRepair = 1`, `EstimatedSize` (КБ); вызывать при запуске
  рядом с `ApplyAutoStart`, только из установленной однофайловой копии. `Run`: запись
  автозапуска, ключ `Uninstall\Snapkit`, ярлык `Snapkit.lnk` в «Пуске», `DeleteData` для
  `%LOCALAPPDATA%\Snapkit` и `%APPDATA%\Snapkit` с `keep = saveFolder`, затем скрытый
  `cmd /c ping 127.0.0.1 -n 3 > nul & rmdir /s /q "<папка программы>"`.
  В `App.OnStartup` **первым делом**, до самоустановки и проверки второй копии: если есть
  `--uninstall` — вопрос «Удалить Snapkit? Снимки ленты и настройки тоже удалятся.
  Сохранённые файлы в папке сохранения останутся» (Да/Нет); «Да» — `StopOtherCopies`,
  `Run`, «Snapkit удалён», выход. Ссылка в настройках запускает `Snapkit.exe --uninstall`
  (вопрос задаёт он).
- [ ] **Шаг 4: Запустить — зелёные.**
- [ ] **Шаг 5: Коммит** `feat(shell): удаление программы и строка в «Приложениях»`.

---

### Задача 10: README, CHANGELOG, список приёмки, полный прогон

**Файлы:**
- Изменить: `README.md`, `CHANGELOG.md`
- Создать: `docs/superpowers/plans/2026-10-27-snapkit-stage-4-acceptance.md`

- [ ] **Шаг 1: README** — раздел «Настройки» (что на каждой вкладке), снимок всего экрана,
  та же область, автосохранение, светлая тема, обновление и удаление; убрать «клавиши
  только в файле».
- [ ] **Шаг 2: CHANGELOG** — раздел `## [Не выпущено]`: «Добавлено» (окно настроек и всё
  из него), «Совместимость» (настройки 0.3.x читаются, клавиши переносятся). Номер версии —
  при выпуске, с согласия пользователя.
- [ ] **Шаг 3: Список приёмки** по образцу этапа 3: по проверке на каждый раздел 2.x описания
  (окно и «Отмена», каждая галочка, обе клавиши и запись Print Screen, весь экран на втором
  мониторе, та же область после перезапуска, курсор, автосохранение и правка, JPEG,
  папка, три темы и смена темы Windows, 12 цветов и кружки на снимке, обновление — на
  поддельном релизе или следующем настоящем, удаление из настроек и из «Приложений»,
  сохранённые файлы целы).
- [ ] **Шаг 4: Полный прогон** `dotnet test Snapkit.slnx` (с буфером обмена и окнами) — зелёный;
  `build.ps1` — сборка ставится и запускается.
- [ ] **Шаг 5: Коммит** `docs: настройки этапа 4 в README, CHANGELOG и список приёмки`.
