# Snapkit, этап 1 — главный путь: снимок → разметка → лента → вставка по Ctrl+V

> **Для исполнителей-агентов:** ОБЯЗАТЕЛЬНЫЙ НАВЫК: superpowers:subagent-driven-development
> (рекомендуется) или superpowers:executing-plans — выполнять задачу за задачей. Шаги
> отмечаются флажками (`- [ ]`).

**Цель:** рабочая программа, которой пользователь может заменить Snapik в основном сценарии.
Print Screen → выделить область → рамка, стрелка, текст и комментарии с кружками A1 →
лента → Ctrl+V (в том числе Shift+колесо вверх из AutoHotkey) вставляет все картинки
и текст в любое окно.

**Архитектура:** два проекта. `Snapkit.Core` — чистая логика без WPF и Win32: модель,
нумерация, текст для чата, сессия на диске, маршрутизация клавиш, геометрия; покрыт
тестами. `Snapkit.App` — WPF-приложение: захват экрана, окна, редактор, лента, хук
клавиатуры на отдельном потоке, буфер обмена с отложенной выдачей, вставка через `SendInput`.

**Технологии:** C# 14 / .NET 10, WPF, WinForms (только `NotifyIcon`), Win32 через
`LibraryImport`, xUnit (шаблон `dotnet new xunit`).

**Описание проекта:** [docs/superpowers/specs/2026-10-23-snapkit-design.md](../specs/2026-10-23-snapkit-design.md).
Образцы интерфейса — `docs/superpowers/specs/snapik-reference/`. Исполнитель читает и описание,
и план.

## Этапы проекта

| Этап | Что входит | План |
|---|---|---|
| **1 (этот)** | каркас и сборка; модель; лента (без анимаций) и сессия на диске; трей; одна копия; автозапуск; уведомления; захват и выделение области; редактор: выбор, рамка, стрелка, текст, комментарий, цвет, толщина, отменить/повторить, копировать, «Готово»; комментарии и кружки; повторное открытие; Print Screen; пакет в буфере, «Копировать пакет»; вставка по Ctrl+V | этот файл |
| 2 | весь редактор: овал, скруглённый, изогнутая и широкая стрелки, линия, карандаш, маркер, ластик, размытие, обрезка, заливки, стили линий, пипетка, свои цвета, ресайз фигур маркерами, перемещение кружка, подсказка «?» | отдельный план |
| 3 | лента и файлы: стопка с анимацией, капсула, перетаскивание, «Вернуть», «Отправить ещё раз» на карточке, импорт файла, «Сохранить на компьютер», «Сохранить пакет…», автосохранение, PNG/JPEG | отдельный план |
| 4 | настройки и вид: окно из 4 вкладок, назначение клавиш, снимок всего экрана, «та же область», курсор на снимке, темы, акценты, палитры, звуки | отдельный план |

## Общие ограничения

- Платформа: `net10.0` (Core), `net10.0-windows` (App и App.Tests), x64, Windows 10/11, DPI —
  PerMonitorV2 через `app.manifest`.
- В приложении без сторонних NuGet-пакетов. В тестах — только то, что даёт шаблон `xunit`.
- Весь текст для пользователя (окна, уведомления, подсказки) — только на русском.
  Комментарии в коде — на русском. Имена в коде — на английском.
- Программа **никогда** не нажимает Enter и не отправляет сообщение.
- Свои программные нажатия помечаются `dwExtraInfo = 0x534E4B54` (`InputSender.Marker`).
- Хук клавиатуры работает на отдельном потоке. Его обработчик не блокируется и не ждёт
  UI-поток.
- Пути: сессия — `%LOCALAPPDATA%\Snapkit\session\`, настройки — `%APPDATA%\Snapkit\settings.json`,
  программа — `%LOCALAPPDATA%\Programs\Snapkit\`.
- Лимит ленты — 20 снимков (`SessionModel.MaxCaptures`). С 10 неотправленных снимков
  в ленте видна подсказка «Чаты обычно принимают до 10 картинок за раз».
- Ресурсы Snapik (код, звуки, иконки, название) не используются.
- Без интернета и без телеметрии.
- Коммиты — Conventional Commits: тип и область на английском, описание на русском,
  повелительное наклонение. Каждое сообщение коммита заканчивается строкой
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Работа идёт в ветке
  `feat/stage-1-mvp`.
- Цвета по умолчанию: акцент `#2F7BF5` (кружки, рамка выделения, кнопка «Готово»),
  палитра разметки `#E53935` (по умолчанию), `#FB8C00`, `#FDD835`, `#43A047`, `#1E88E5`,
  `#8E24AA`. Толщины 2, 4 (по умолчанию), 6, 8 px. Фон панелей `#1E2128`, текст `#E8EAED`,
  радиус скругления 10.

## На что смотреть при проверке

Места, которые описание подразумевает, но обычные тесты задач не затрагивают. Тест для
каждого добавлен в задачу, которой принадлежит код:

1. **Shift физически зажат во время вставки** (Shift+колесо вверх в AutoHotkey). Картинки
   должны вставиться картинками, а не превратиться во «вставку только текста»
   (Ctrl+Shift+V), и ни одна клавиша не должна залипнуть. Тесты в задаче 4
   (`Ahk_ShiftWheelUp_IsIntercepted`, `CtrlV_ShiftHeld_ReleasesShiftFirst`,
   `Restore_PhysicalShift_PressesItBack`).
2. **Буфер обмена занят другой программой.** Публикация завершается ошибкой «занят»
   не дольше чем за секунду, программа не зависает, снимки не теряются. Тест в задаче 12
   (`Publish_WhileClipboardHeld_ThrowsBusy`).
3. **Журнал буфера Windows (Win+V) или менеджер буфера читает отложенные данные.** Пакет
   не считается вытесненным, а чтение чужим процессом не считается подтверждением
   вставки. Тесты в задаче 12 (`ForeignRead_KeepsPackageCurrent`,
   `ForeignRead_DoesNotConfirmRenderForOtherPid`).
4. **Мониторы с разным масштабом (100 % и 150 %).** Выделение и итоговый PNG совпадают
   с экраном пиксель в пиксель. Ручная проверка в задаче 8, шаг 6.
5. **Программу убили во время записи сессии или файл сессии повреждён.** Запуск не падает,
   повреждённый файл откладывается в сторону. Тест в задаче 3
   (`Load_CorruptJson_ReturnsEmptyAndKeepsBadCopy`).

---

## Подготовка (до задачи 1)

- [ ] **Установить .NET 10 SDK** — только после отдельного согласия пользователя:
  `winget install --id Microsoft.DotNet.SDK.10 --accept-package-agreements`. Проверка:
  `dotnet --list-sdks` показывает строку `10.0.`.
- [ ] **Snapik закрыт и убран из автозапуска** — это делает пользователь.
- [ ] **Создать ветку:** `git switch -c feat/stage-1-mvp`.

---

### Задача 1: Каркас решения, иконка, сборка одной командой

**Файлы:**
- Создать: `Snapkit.slnx` (`dotnet new sln -n Snapkit`; в .NET 10 получается `.slnx`), `.gitignore`
- Создать: `src/Snapkit.Core/Snapkit.Core.csproj` (`net10.0`, `Nullable` enable)
- Создать: `src/Snapkit.App/Snapkit.App.csproj`, `App.xaml`, `App.xaml.cs`, `app.manifest`, `Assets/snapkit.ico`
- Создать: `tests/Snapkit.Core.Tests/` (`dotnet new xunit`), `tests/Snapkit.App.Tests/` (`dotnet new xunit`, затем `net10.0-windows` + `<UseWPF>true</UseWPF>`), `tests/Snapkit.App.Tests/StaRunner.cs`
- Создать: `tools/make-icon.ps1`, `build.ps1`, `README.md`

**Интерфейсы:**
- Создаёт: `StaRunner.Run(Action body)` — выполняет `body` в новом STA-потоке и пробрасывает
  исключение. Нужен всем WPF-тестам.

- [ ] **Шаг 1: Создать решение и проекты, связать ссылки.** Core.Tests → Core; App → Core;
  App.Tests → App и Core. В `Snapkit.App.csproj`:
  `<OutputType>WinExe</OutputType>`, `<UseWPF>true</UseWPF>`, `<UseWindowsForms>true</UseWindowsForms>`,
  `<Using Remove="System.Windows.Forms" />`, `<Using Remove="System.Drawing" />`,
  `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`, `<ApplicationManifest>app.manifest</ApplicationManifest>`,
  `<ApplicationIcon>Assets\snapkit.ico</ApplicationIcon>`, `<AssemblyName>Snapkit</AssemblyName>`,
  `<RuntimeIdentifier>win-x64</RuntimeIdentifier>`. `snapkit.ico` подключить как `<Resource>`.
  В `app.manifest` — `dpiAware` `true/PM` и `dpiAwareness` `PerMonitorV2`.
- [ ] **Шаг 2: `tools/make-icon.ps1`** (Windows PowerShell 5.1, `System.Drawing`) рисует кадры
  16, 24, 32, 48 и 256 px. На каждом скруглённый квадрат `#2F7BF5` (радиус — 22 % стороны),
  четыре белых уголка видоискателя и белая точка в центре. Кадры сохраняются как PNG
  и собираются в ICO с PNG-вложениями: заголовок 6 байт, записи по 16 байт, данные PNG.
  Запуск: `powershell -File tools/make-icon.ps1` → появился `src/Snapkit.App/Assets/snapkit.ico`.
- [ ] **Шаг 3: Пустое приложение.** `App.OnStartup` показывает иконку в трее
  (`System.Windows.Forms.NotifyIcon`, подсказка «Snapkit»), правый клик — меню с пунктом
  «Выйти». `ShutdownMode = OnExplicitShutdown`.
- [ ] **Шаг 4: `build.ps1`.** Последовательно:
  `dotnet test Snapkit.slnx --filter "Category!=Clipboard"` → при ошибке остановиться;
  `dotnet publish src/Snapkit.App -c Release -r win-x64 --self-contained true -o artifacts/publish`;
  `Stop-Process -Name Snapkit -ErrorAction SilentlyContinue` и ожидание выхода процесса;
  копирование `artifacts/publish/*` в `%LOCALAPPDATA%\Programs\Snapkit\`; ярлык
  `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Snapkit.lnk` через COM `WScript.Shell`;
  `Start-Process` нового `Snapkit.exe`. Все сообщения скрипта — на русском.
- [ ] **Шаг 5: Проверка.** Запустить `powershell -File build.ps1` → тесты зелёные, у часов
  появилась иконка Snapkit, «Выйти» закрывает программу. В «Пуске» есть ярлык «Snapkit».
- [ ] **Шаг 6: README.md** — что это, как собрать (`build.ps1`), где лежат данные. На русском.
- [ ] **Шаг 7: Коммит** — `build: создать каркас решения Snapkit и скрипт сборки`.

---

### Задача 2: Модель снимка, буквы, номера заметок, текст для чата

**Файлы:**
- Создать: `src/Snapkit.Core/Geometry.cs`, `src/Snapkit.Core/Model/Annotation.cs`, `src/Snapkit.Core/Model/Capture.cs`
- Создать: `src/Snapkit.Core/Labels.cs`, `src/Snapkit.Core/PromptBuilder.cs`, `src/Snapkit.Core/PackageSelector.cs`, `src/Snapkit.Core/RussianPlural.cs`
- Тест: `tests/Snapkit.Core.Tests/LabelsAndPromptTests.cs`, `tests/Snapkit.Core.Tests/RussianPluralTests.cs`

**Интерфейсы — создаёт:**
```csharp
public readonly record struct PointD(double X, double Y);
public readonly record struct RectI(int X, int Y, int Width, int Height)
{ public int Right => X + Width; public int Bottom => Y + Height; }

public enum AnnotationKind { Rectangle, Arrow, Text, Pin }
// Points: Rectangle — [угол, противоположный угол]; Arrow — [начало, конец]; Text и Pin — [точка привязки].
// Координаты — в пикселях снимка. Note — комментарий; пусто или null — без кружка.
public sealed record Annotation(Guid Id, AnnotationKind Kind, ImmutableArray<PointD> Points,
    string Color, double Thickness, string? Text, string? Note)
{ public bool HasNote => !string.IsNullOrWhiteSpace(Note); }

// ScreenBounds — где снимок был на экране (физические пиксели виртуального экрана).
public sealed record Capture(Guid Id, int PixelWidth, int PixelHeight, RectI ScreenBounds,
    ImmutableList<Annotation> Annotations, string SnapshotNote, bool IsSent, DateTimeOffset CreatedAt)
{ public int NoteCount => …; } // заметки с кружками + 1, если SnapshotNote не пуст

public static class CaptureLetters { public static IReadOnlyDictionary<Guid, char> Assign(IEnumerable<Capture> stripOrder); }
public static class NoteNumbers
{
    public static IReadOnlyList<(Annotation Annotation, int Number)> Number(Capture capture);
    public static string Label(char letter, int number); // 'A', 1 → "A1"
}
public readonly record struct LetteredCapture(char Letter, Capture Capture);
public static class PackageSelector { public static IReadOnlyList<LetteredCapture> Select(IReadOnlyList<Capture> strip); }
public static class PromptBuilder { public static string Build(IReadOnlyList<LetteredCapture> package); }
public static class RussianPlural { public static string Format(int n, string one, string few, string many); }
```

- [ ] **Шаг 1: Написать падающие тесты** (вспомогательные `Rect(note)` и `Cap(sent, params Annotation[])`
  создают объекты с `Guid.NewGuid()`, цветом `#E53935`, толщиной 4 и датой `DateTimeOffset.UnixEpoch`):

```csharp
[Fact] public void Letters_SkipSentCaptures()
{
    var sent = Cap(true); var first = Cap(false); var second = Cap(false);
    var letters = CaptureLetters.Assign([sent, first, second]);
    Assert.False(letters.ContainsKey(sent.Id));
    Assert.Equal('A', letters[first.Id]);
    Assert.Equal('B', letters[second.Id]);
}

[Fact] public void NoteNumbers_CountOnlyAnnotationsWithNotes()
{
    var a = Rect("x"); var b = Rect(null); var c = Rect("y");
    var numbered = NoteNumbers.Number(Cap(false, a, b, c));
    Assert.Equal(new[] { (a, 1), (c, 2) }, numbered);
}

[Fact] public void Prompt_FormatsCapturesAndNotes()
{
    var capA = Cap(false, Rect("У Макса значок VK в админке"), Rect("Поле Telegram шире остальных"))
        with { SnapshotNote = "страница настроек соцсетей" };
    var capB = Cap(false, Rect("Кнопка «Сохранить» не активна"));
    var text = PromptBuilder.Build([new('A', capA), new('B', capB)]);
    Assert.Equal(
        "Снимок A\nA1 — У Макса значок VK в админке\nA2 — Поле Telegram шире остальных\n" +
        "Комментарий к снимку: страница настроек соцсетей\n\n" +
        "Снимок B\nB1 — Кнопка «Сохранить» не активна", text);
}

[Fact] public void Prompt_SkipsCapturesWithoutNotes_KeepsLetters()
{
    var text = PromptBuilder.Build([new('A', Cap(false, Rect(null))), new('B', Cap(false, Rect("есть")))]);
    Assert.Equal("Снимок B\nB1 — есть", text);
}

[Fact] public void Prompt_EmptyWhenNoNotes()
    => Assert.Equal("", PromptBuilder.Build([new('A', Cap(false, Rect(null)))]));

[Fact] public void Prompt_IndentsMultilineNote()
{
    var text = PromptBuilder.Build([new('A', Cap(false, Rect("строка 1\nстрока 2")))]);
    Assert.Equal("Снимок A\nA1 — строка 1\n     строка 2", text); // отступ = длина "A1 — "
}

[Fact] public void Package_IsUnsentInStripOrderWithLetters()
{
    var s = Cap(true); var u1 = Cap(false); var u2 = Cap(false);
    Assert.Equal(new LetteredCapture[] { new('A', u1), new('B', u2) }, PackageSelector.Select([u1, s, u2]));
}

[Fact] public void NoteCount_IncludesSnapshotNote()
    => Assert.Equal(2, (Cap(false, Rect("x"), Rect(null)) with { SnapshotNote = "общая" }).NoteCount);

// RussianPluralTests
[Theory]
[InlineData(1, "1 изображение")] [InlineData(2, "2 изображения")] [InlineData(5, "5 изображений")]
[InlineData(11, "11 изображений")] [InlineData(21, "21 изображение")] [InlineData(22, "22 изображения")]
public void Plural(int n, string expected)
    => Assert.Equal(expected, RussianPlural.Format(n, "изображение", "изображения", "изображений"));
```

- [ ] **Шаг 2: Запустить** `dotnet test tests/Snapkit.Core.Tests` → ошибка сборки: типов ещё нет.
- [ ] **Шаг 3: Реализовать типы из блока «Интерфейсы».** Продолжения многострочной заметки
  получают отступ `label.Length + 3` пробелов. Продолжения общего комментария — 2 пробела.
  Строки разделяются `\n`; на `\r\n` их заменяет буфер обмена (задача 11).
  `RussianPlural.Format` возвращает `"{n} {форма}"`.
- [ ] **Шаг 4: Запустить** `dotnet test tests/Snapkit.Core.Tests` → все тесты проходят.
- [ ] **Шаг 5: Коммит** — `feat(core): добавить модель снимка, буквы, номера заметок и текст для чата`.

---

### Задача 3: Модель сессии, хранение сессии и настроек

**Файлы:**
- Создать: `src/Snapkit.Core/Session/SessionModel.cs`, `src/Snapkit.Core/Session/SessionStore.cs`, `src/Snapkit.Core/SnapkitJson.cs`
- Создать: `src/Snapkit.Core/Settings/AppSettings.cs`, `src/Snapkit.Core/Settings/SettingsStore.cs`
- Тест: `tests/Snapkit.Core.Tests/SessionModelTests.cs`, `SessionStoreTests.cs`, `SettingsStoreTests.cs`

**Интерфейсы:**
- Использует: `Capture`, `Annotation` (задача 2).
- Создаёт:
```csharp
public sealed class SessionModel
{
    public const int MaxCaptures = 20; public const int SoftWarningUnsent = 10;
    public IReadOnlyList<Capture> Captures { get; }
    public int UnsentCount { get; }
    public bool IsFull { get; }
    public event EventHandler? Changed;
    public void Add(Capture capture);          // в конец; InvalidOperationException, если IsFull
    public void Update(Capture updated);       // по Id; правка разметки или общей заметки снимает IsSent
    public Capture? Remove(Guid id);
    public void Clear();
    public void MarkSent(IEnumerable<Guid> ids);
    public void MarkAllUnsent();
    public void Load(IEnumerable<Capture> captures); // заменить содержимое, Changed один раз
}
public sealed class SessionStore(string directory)
{
    public IReadOnlyList<Capture> Load();      // нет файла → []; снимки без PNG выбрасываются
    public void Save(IReadOnlyList<Capture> captures);
    public string SourceImagePath(Guid id);    // {directory}\{id}.png
    public void WriteSourceImage(Guid id, byte[] png);
    public void Clear();                       // удалить session.json и все *.png
}
public static class SnapkitJson { public static JsonSerializerOptions Options { get; } } // Web-умолчания + JsonStringEnumConverter + отступы

public sealed record AppSettings(bool LaunchAtStartup = true, bool ShowNotifications = true, bool ClearStripAfterPaste = false,
    bool StripTopmost = true, double? StripLeft = null, double? StripTop = null, string AccentColor = "#2F7BF5",
    bool SnippingHintShown = false);
public sealed class SettingsStore(string filePath)
{
    public (AppSettings Settings, bool WasReset) Load(); // нет файла → (по умолчанию, false); повреждён → (по умолчанию, true)
    public void Save(AppSettings settings);
}
```

- [ ] **Шаг 1: Написать падающие тесты.** `SessionModelTests`:
  - `Add_AppendsAndRaisesChanged` — после `Add` снимок последний, `Changed` вызван один раз.
  - `Add_WhenFull_Throws` — 20 раз `Add`, затем `IsFull == true`, 21-й `Add` бросает `InvalidOperationException`.
  - `Update_ChangedAnnotationsOnSentCapture_MakesItUnsent` — отправленный снимок,
    `Update(c with { Annotations = c.Annotations.Add(...) })` → `IsSent == false`.
  - `Update_SameAnnotationsAndNote_KeepsSent` — `Update(c with { })` → `IsSent == true`
    (сравнение `ReferenceEquals` для `Annotations` и `==` для `SnapshotNote`).
  - `MarkSent_And_MarkAllUnsent_UpdateUnsentCount` — 3 снимка, `MarkSent` для двух →
    `UnsentCount == 1`, `MarkAllUnsent` → `3`.
  - `Remove_ReturnsRemovedCapture`.

  `SessionStoreTests` (временная папка `Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())`):
  - `SaveThenLoad_RoundTrips` — снимок с рамкой, стрелкой, текстом и пином плюс PNG-файл →
    `Load()` возвращает тот же снимок. Поля сравниваются по одному, точки — через
    `SequenceEqual`: равенство записей с `ImmutableArray` сравнивает ссылки.
  - `Load_DropsCapturesWithoutImageFile`.
  - `Load_CorruptJson_ReturnsEmptyAndKeepsBadCopy` — в `session.json` записан текст `{не json`
    → `Load()` возвращает `[]`, рядом лежит `session.bad.json`, `session.json` удалён.
  - `Save_LeavesNoTempFile` — после `Save` в папке нет `*.tmp`.
  - `Clear_RemovesJsonAndImages`.

  `SettingsStoreTests`: `Load_Missing_ReturnsDefaults`, `Load_Corrupt_ReturnsDefaultsAndWasReset`,
  `SaveThenLoad_RoundTrips`.
- [ ] **Шаг 2: Запустить** `dotnet test tests/Snapkit.Core.Tests` → ошибки сборки.
- [ ] **Шаг 3: Реализовать.** Запись в обоих хранилищах: `{файл}.tmp` →
  `File.Move(tmp, файл, overwrite: true)`. Хранилища создают свою папку при первой записи.
- [ ] **Шаг 4: Запустить тесты** → все проходят.
- [ ] **Шаг 5: Коммит** — `feat(core): добавить модель сессии и хранение сессии и настроек на диске`.

---

### Задача 4: Маршрутизация клавиш и последовательность нажатий для вставки

**Файлы:**
- Создать: `src/Snapkit.Core/Input/Keys.cs`, `src/Snapkit.Core/Input/KeyRouter.cs`, `src/Snapkit.Core/Input/PasteKeys.cs`
- Тест: `tests/Snapkit.Core.Tests/KeyRouterTests.cs`, `PasteKeysTests.cs`

**Интерфейсы — создаёт:**
```csharp
public static class Vk { public const int LShift = 0xA0, RShift = 0xA1, LCtrl = 0xA2, RCtrl = 0xA3,
    LAlt = 0xA4, RAlt = 0xA5, LWin = 0x5B, RWin = 0x5C, V = 0x56, Snapshot = 0x2C, Escape = 0x1B; }
[Flags] public enum Mod { None = 0, LShift = 1, RShift = 2, LCtrl = 4, RCtrl = 8, LAlt = 16, RAlt = 32, LWin = 64, RWin = 128 }
[Flags] public enum HotkeyMods { None = 0, Ctrl = 1, Alt = 2, Shift = 4, Win = 8 }
public readonly record struct Hotkey(HotkeyMods Modifiers, int Vk);
public readonly record struct KeyInput(int Vk, bool IsDown, bool IsInjected, bool IsOwn);
public enum KeyAction { None, CaptureRegion, CaptureFullScreen, PasteBatch }
public readonly record struct KeyDecision(bool Suppress, KeyAction Action);
public readonly record struct PasteContext(bool HasUnsent, bool PackageIsCurrent, bool ForegroundIsOwn);

public sealed class KeyRouter(Func<PasteContext> pasteContext)
{
    public IReadOnlyDictionary<Hotkey, KeyAction> Hotkeys { get; set; }  // по умолчанию { (None, Snapshot) → CaptureRegion }
    public bool PasteInProgress { get; set; }   // ставит и снимает UI-поток
    public bool EscapePressedDuringPaste { get; set; }
    public Mod Logical { get; }    // все события, включая программные; хранится в volatile int
    public Mod Physical { get; }   // только события с клавиатуры (IsInjected == false)
    public KeyDecision Process(KeyInput input);
}
public readonly record struct KeyStroke(int Vk, bool Up);
public static class PasteKeys
{
    public static IReadOnlyList<KeyStroke> CtrlV(Mod logicalHeld);
    public static IReadOnlyList<KeyStroke> Restore(Mod physicalHeld, Mod logicalNow);
}
```
**Правила `Process`** (в этом порядке):
1. Клавиши-модификаторы обновляют `Logical` (всегда) и `Physical` (если не `IsInjected`)
   и никогда не подавляются.
2. `IsOwn` → `(false, None)`.
3. Во время `PasteInProgress`: Esc (нажатие) ставит `EscapePressedDuringPaste`, но
   не подавляется. Нажатие и отпускание V при логически нажатом Ctrl подавляются
   без действия.
4. Горячая клавиша срабатывает, когда `Vk` совпадает, а сводка `Logical` (Ctrl, Alt,
   Shift, Win) **точно** равна `Modifiers`. Нажатие подавляется и даёт действие, отпускание
   той же клавиши подавляется. Для `Vk.Snapshot`: отпускание без предшествующего
   нажатия тоже подавляется и даёт действие.
5. Ctrl+V: нажатие V, в `Logical` есть Ctrl, нет Alt и нет Win (Shift допустим). Тогда
   вызывается `pasteContext()`. Если `HasUnsent && PackageIsCurrent && !ForegroundIsOwn` →
   `(true, PasteBatch)`, а отпускание этой V подавляется.
6. Остальное → `(false, None)`.

**Правила `PasteKeys`:** `CtrlV` сначала отпускает каждый логически нажатый Shift, Alt
и Win (своей стороной). Затем, если Ctrl не нажат: LCtrl↓, V↓, V↑, LCtrl↑; если нажат —
только V↓, V↑. `Restore` нажимает Shift, который физически нажат, а логически нет,
и отпускает Ctrl, который логически нажат, а физически нет.

- [ ] **Шаг 1: Написать падающие тесты** (`Down(vk, inj=false)`, `Up(vk, inj=false)`;
  `Ready = new PasteContext(true, true, false)`):

```csharp
[Fact] public void PhysicalCtrlV_IsIntercepted()
{
    var r = new KeyRouter(() => Ready);
    r.Process(Down(Vk.LCtrl));
    Assert.Equal(new KeyDecision(true, KeyAction.PasteBatch), r.Process(Down(Vk.V)));
    Assert.Equal(new KeyDecision(true, KeyAction.None), r.Process(Up(Vk.V)));
    Assert.Equal(new KeyDecision(false, KeyAction.None), r.Process(Up(Vk.LCtrl)));
}

[Fact] public void Ahk_ShiftWheelUp_IsIntercepted()
{   // AutoHotkey: Shift зажат рукой, Send ^v сам отпускает и возвращает Shift
    var r = new KeyRouter(() => Ready);
    r.Process(Down(Vk.LShift));
    r.Process(Up(Vk.LShift, inj: true));
    r.Process(Down(Vk.LCtrl, inj: true));
    Assert.Equal(KeyAction.PasteBatch, r.Process(Down(Vk.V, inj: true)).Action);
    Assert.True(r.Process(Up(Vk.V, inj: true)).Suppress);
    r.Process(Up(Vk.LCtrl, inj: true));
    r.Process(Down(Vk.LShift, inj: true));
    Assert.Equal(Mod.LShift, r.Physical);
    Assert.Equal(Mod.LShift, r.Logical);
}

[Fact] public void CtrlShiftV_IsIntercepted() { /* LShift↓, LCtrl↓, V↓ → PasteBatch */ }
[Fact] public void CtrlAltV_IsNotIntercepted() { /* LCtrl↓, LAlt↓, V↓ → (false, None) */ }

[Theory] [InlineData(false, true, false)] [InlineData(true, false, false)] [InlineData(true, true, true)]
public void CtrlV_PassesThrough_WhenContextNotReady(bool unsent, bool current, bool own)
{ /* LCtrl↓, V↓ → (false, None) */ }

[Fact] public void OwnInput_IsNeverIntercepted_ButUpdatesLogical()
{   // new KeyInput(Vk.LCtrl, true, true, IsOwn: true), затем V с IsOwn → (false, None); Logical содержит LCtrl
}

[Fact] public void WhilePasting_CtrlVRepeatsAreSwallowed() { /* PasteInProgress = true; LCtrl↓, V↓ → (true, None) */ }
[Fact] public void WhilePasting_EscapeIsRecorded() { /* Esc↓ → (false, None), EscapePressedDuringPaste == true */ }
[Fact] public void PrintScreen_DownTriggers_UpSuppressed() { /* Snapshot↓ → (true, CaptureRegion); Snapshot↑ → (true, None) */ }
[Fact] public void PrintScreen_UpWithoutDown_Triggers() { /* Snapshot↑ → (true, CaptureRegion) */ }
[Fact] public void ShiftPrintScreen_DoesNotMatchPlainHotkey() { /* LShift↓, Snapshot↓ → (false, None) */ }
[Fact] public void CustomHotkey_CtrlPrintScreen() { /* Hotkeys += (Ctrl, Snapshot) → CaptureFullScreen */ }

// PasteKeysTests
[Fact] public void CtrlV_NoModifiers()
    => Assert.Equal(new KeyStroke[] { new(Vk.LCtrl, false), new(Vk.V, false), new(Vk.V, true), new(Vk.LCtrl, true) }, PasteKeys.CtrlV(Mod.None));
[Fact] public void CtrlV_ShiftHeld_ReleasesShiftFirst()
    => Assert.Equal(new KeyStroke[] { new(Vk.LShift, true), new(Vk.LCtrl, false), new(Vk.V, false), new(Vk.V, true), new(Vk.LCtrl, true) },
                    PasteKeys.CtrlV(Mod.LShift));
[Fact] public void CtrlV_CtrlHeld_OnlyV()
    => Assert.Equal(new KeyStroke[] { new(Vk.V, false), new(Vk.V, true) }, PasteKeys.CtrlV(Mod.RCtrl));
[Fact] public void Restore_PhysicalShift_PressesItBack()
    => Assert.Equal(new KeyStroke[] { new(Vk.LShift, false) }, PasteKeys.Restore(Mod.LShift, Mod.None));
[Fact] public void Restore_StuckCtrl_ReleasesIt()
    => Assert.Equal(new KeyStroke[] { new(Vk.LCtrl, true) }, PasteKeys.Restore(Mod.None, Mod.LCtrl));
```
  Тела, заданные комментариями, исполнитель пишет в том же стиле, что и полные тесты выше.
- [ ] **Шаг 2: Запустить тесты** → ошибки сборки.
- [ ] **Шаг 3: Реализовать по правилам выше.**
- [ ] **Шаг 4: Запустить тесты** → все проходят.
- [ ] **Шаг 5: Коммит** — `feat(core): добавить маршрутизацию клавиш и нажатия для вставки`.

---

### Задача 5: История правок и геометрия разметки

**Файлы:**
- Создать: `src/Snapkit.Core/Editing/AnnotationHistory.cs`, `src/Snapkit.Core/Editing/AnnotationGeometry.cs`
- Тест: `tests/Snapkit.Core.Tests/AnnotationHistoryTests.cs`, `AnnotationGeometryTests.cs`

**Интерфейсы — создаёт:**
```csharp
public sealed class AnnotationHistory(ImmutableList<Annotation> initial)
{
    public ImmutableList<Annotation> Current { get; }
    public bool CanUndo { get; } public bool CanRedo { get; }
    public bool IsChanged { get; }   // Current отличается (по ссылке) от initial
    public void Push(ImmutableList<Annotation> next);  // очищает redo
    public void Undo(); public void Redo();
}
public static class AnnotationGeometry
{
    public const double PinDiameter = 26;
    public static PointD PinCenter(Annotation a);   // Rectangle — левый верхний угол нормализованного прямоугольника; Arrow — начало; Text и Pin — точка привязки
    public static bool HitTest(Annotation a, PointD p, double tolerance); // Rectangle — расстояние до контура; Arrow — до отрезка; Pin — до центра (радиус + tolerance); Text — прямоугольник привязки шириной Text.Length × размер шрифта × 0,6 и высотой размер × 1,3
    public static double TextFontSize(double thickness);                  // 12 + thickness × 2
    public static Annotation Translate(Annotation a, double dx, double dy);
    public static (double X, double Y, double W, double H) Bounds(Annotation a);
    public static Guid? TopmostHit(IReadOnlyList<Annotation> items, PointD p, double tolerance); // последний в списке выигрывает; кружок заметки тоже попадание
}
```
- [ ] **Шаг 1: Написать падающие тесты:** `Push_Undo_Redo_RestoresStates`,
  `Push_AfterUndo_ClearsRedo`, `IsChanged_FalseAfterUndoToInitial`,
  `HitTest_Rectangle_OnBorderNotInside`, а также для прямоугольника (10,10)–(110,60) с допуском 4:
  - точка (10,30) — попадание;
  - точка (60,35) — промах;
  - точка (113,30) — попадание.

  Ещё тесты:
  - `HitTest_Arrow_NearSegment` — стрелка (0,0)→(100,0): точки (50,3) — попадание, (50,10) — промах.
  - `PinCenter_RectangleIsNormalizedTopLeft` — точки (110,60) и (10,10) → центр (10,10).
  - `TopmostHit_PrefersLastAnnotation`.
  - `TopmostHit_HitsPinCircle` — точка внутри кружка заметки рамки, но не на её контуре → эта рамка.
  - `Translate_MovesAllPoints`.
- [ ] **Шаг 2: Запустить** → ошибки сборки. **Шаг 3: Реализовать. Шаг 4: Запустить** → все тесты проходят.
- [ ] **Шаг 5: Коммит** — `feat(core): добавить историю правок и геометрию разметки`.

---

### Задача 6: Кодеки изображений и рендер снимка с разметкой

**Файлы:**
- Создать: `src/Snapkit.App/Imaging/ImageCodecs.cs`, `src/Snapkit.App/Rendering/AnnotationPainter.cs`, `src/Snapkit.App/Rendering/CaptureRenderer.cs`, `src/Snapkit.App/Rendering/Palette.cs`
- Тест: `tests/Snapkit.App.Tests/ImageCodecsTests.cs`, `CaptureRendererTests.cs`

**Интерфейсы:**
- Использует: `Annotation`, `Capture`, `NoteNumbers`, `AnnotationGeometry` (задачи 2 и 5).
- Создаёт:
```csharp
public static class ImageCodecs
{
    public static byte[] EncodePng(BitmapSource image);
    public static BitmapSource DecodePng(byte[] png);      // замороженный, Bgra32, DPI 96
    public static byte[] EncodeDib(BitmapSource image);    // BITMAPINFOHEADER (40 байт) + пиксели 32 bpp BI_RGB, строки снизу вверх
}
public static class Palette { public static readonly string[] Markup; public const string Accent = "#2F7BF5"; public static Brush Brush(string hex); }
public static class AnnotationPainter
{   // одна процедура рисования для редактора и итоговой картинки; координаты — пиксели снимка
    public static void Draw(DrawingContext dc, Annotation a);
    public static void DrawPin(DrawingContext dc, PointD center, string label); // круг Accent диаметром PinDiameter, белая жирная надпись 12 px, Segoe UI
}
public static class CaptureRenderer
{   // letter: буква снимка для кружков (A1, A2…)
    public static BitmapSource Render(BitmapSource source, Capture capture, char letter);
    public static BitmapSource RenderThumbnail(BitmapSource source, Capture capture, char? letter, int maxWidth);
}
```
Рисование:
- рамка — контур толщиной `Thickness`;
- стрелка — отрезок плюс залитый треугольный наконечник длиной `max(12, Thickness × 3.5)`;
- текст — `FormattedText`, Segoe UI, размер `TextFontSize(Thickness)`, тот же цвет;
- кружки рисуются после всех фигур, у каждой заметки в центре `PinCenter`.

- [ ] **Шаг 1: Написать падающие тесты** (каждый внутри `StaRunner.Run`):
  - `EncodeDib_HeaderAndBottomUpRows` — для картинки 3×2, где верхняя строка красная,
    а нижняя синяя:
    - `BitConverter.ToInt32(dib, 0) == 40`;
    - ширина 3, высота +2;
    - `ToInt16(dib, 14) == 32`;
    - длина `40 + 3 × 2 × 4`;
    - первые пиксели после заголовка синие (нижняя строка идёт первой).
  - `Png_RoundTrip_KeepsSizeAndPixels`.
  - `Render_OutputHasCapturePixelSize` — снимок 200×100 → результат 200×100 при DPI 96.
  - `Render_DrawsPinWithAccentColor` — рамка с заметкой в точках (50,50)–(150,90) →
    пиксель (50,50) результата имеет цвет `#2F7BF5`.
- [ ] **Шаг 2: Запустить** `dotnet test tests/Snapkit.App.Tests` → ошибки сборки.
  **Шаг 3: Реализовать.** Рендер — `DrawingVisual` и `RenderTargetBitmap(w, h, 96, 96, Pbgra32)`,
  затем `FormatConvertedBitmap` в Bgra32 и `Freeze()`. **Шаг 4: Тесты** → все проходят.
- [ ] **Шаг 5: Коммит** — `feat(app): добавить кодеки PNG и DIB и рендер снимка с разметкой`.

---

### Задача 7: Лента, сессия на диске, трей, одна копия, автозапуск, уведомления

**Файлы:**
- Создать: `src/Snapkit.App/Session/SessionService.cs`
- Создать: `src/Snapkit.App/Strip/StripWindow.xaml(.cs)`, `src/Snapkit.App/Strip/StripViewModel.cs`, `src/Snapkit.App/Strip/CaptureCardViewModel.cs`
- Создать: `src/Snapkit.App/Notifications/ToastWindow.xaml(.cs)`, `src/Snapkit.App/Notifications/Toasts.cs`
- Создать: `src/Snapkit.App/Shell/TrayIcon.cs`, `src/Snapkit.App/Shell/SingleInstance.cs`, `src/Snapkit.App/Shell/AutoStart.cs`, `src/Snapkit.App/Interop/NativeMethods.cs`
- Изменить: `src/Snapkit.App/App.xaml.cs`

**Интерфейсы:**
- Использует: `SessionModel`, `SessionStore`, `SettingsStore`, `CaptureLetters`, `CaptureRenderer`, `ImageCodecs`.
- Создаёт:
```csharp
public sealed class SessionService   // живёт на UI-потоке
{
    public SessionModel Model { get; }
    public BitmapSource GetSource(Guid id);
    public Guid AddCapture(BitmapSource source, RectI screenBounds, ImmutableList<Annotation> annotations, string snapshotNote);
    public void UpdateCapture(Capture updated);
    public void Remove(Guid id);
    public void ClearAll();          // модель и диск
    public int RestoreFromDisk();    // число восстановленных снимков
    public event EventHandler<SessionChange>? Changed;   // Added, Updated, Removed, Cleared, SentMarked
}
public enum SessionChangeKind { Added, Updated, Removed, Cleared, SentMarked }
public sealed record SessionChange(SessionChangeKind Kind, Guid? CaptureId);
public static class Toasts { public static void Show(string message, string? actionText = null, Action? onAction = null); }
public static class AutoStart { public static void Apply(bool enabled, string exePath); }   // HKCU\...\Run, значение «Snapkit»
public static class SingleInstance { public static bool TryAcquire(Action onSecondInstance); } // Mutex «Local\Snapkit.SingleInstance», событие «Local\Snapkit.ShowStrip»
```
Запись на диск идёт в фоне строго по очереди: одна задача за раз, состояние берётся
последнее. Ошибки записи показываются уведомлением и не роняют программу.

Лента по скриншоту `snapik-reference/strip.png`:
- **Окно:** ширина 240, `WindowStyle=None`, `AllowsTransparency=True`, фон `#1E2128`,
  скругление 10. По умолчанию у правого края рабочей области основного монитора,
  перетаскивается за заголовок, позиция сохраняется в `AppSettings`.
- **Заголовок:** значок, «Snapkit», счётчик `UnsentCount`, кнопки «Очистить» (корзина),
  «…», «Свернуть» (прячет ленту в трей), «Закрыть».
- **Карточки** — стопкой, каждая следующая перекрывает предыдущую. Новая снизу, видна
  целиком. На карточке миниатюра (`RenderThumbnail`), буква в кружке цвета акцента
  у неотправленной или галочка у отправленной, значок комментария с `NoteCount`.
  Клик открывает снимок (подключается в задаче 10). Наведение показывает кнопку «×»
  (удалить).
- **Внизу** кнопка «Новый снимок» цвета акцента (подключается в задаче 8). При пустой
  ленте подсказка «Нажми Print Screen или «Новый снимок»». При `UnsentCount >= 10` —
  строка «Чаты обычно принимают до 10 картинок за раз».
- **Меню «…»:** ✓ Поверх других окон, Вставить изображение из буфера, Копировать пакет
  (подключается в задаче 12), Очистить ленту, Выйти.
- **«Очистить» и «Закрыть»** при неотправленных снимках спрашивают «Удалить снимки
  сессии?» (Да/Нет). «Закрыть» после этого прячет ленту и очищает сессию. Программа
  продолжает работать в трее.
- **Исключение из снимков:** ленте и уведомлениям ставится
  `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE = 0x11)`, чтобы они не попадали
  на снимки.

Уведомление:
- стоит у правого нижнего угла рабочей области основного монитора, отступ 16;
- не забирает фокус: `ShowActivated=false`, `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`;
- исчезает через 2,5 с, а с кнопкой действия — через 5 с;
- новое уведомление заменяет старое.

Трей: клик показывает или прячет ленту, меню — «Новый снимок / Показать ленту / Выйти».

Запуск в `App.OnStartup`:
1. `SingleInstance`.
2. Настройки; если файл был повреждён — уведомление «Файл настроек не читался,
   настройки созданы заново».
3. `AutoStart.Apply`.
4. Если `RestoreFromDisk() > 0` — уведомление «Снимки восстановлены после сбоя».
5. Показать ленту.

Сессия очищается при выходе через «Выйти», но не при завершении сеанса Windows
(`SessionEnding`) и не при убийстве процесса.

- [ ] **Шаг 1: Реализовать** по описанию. Счётчик и буквы пересчитываются по
  `SessionService.Changed`.
- [ ] **Шаг 2: Собрать** `powershell -File build.ps1` → тесты зелёные, программа запустилась.
- [ ] **Шаг 3: Ручная проверка:**
  1. Лента у правого края, в ней подсказка.
  2. Скопировать любую картинку (Win+Shift+S) → «…» → «Вставить изображение из буфера»
     → карточка с буквой A, счётчик 1. Повторить → B, счётчик 2.
  3. Удалить A крестиком → оставшийся снимок стал A.
  4. Завершить процесс Snapkit в Диспетчере задач и запустить снова → снимки на месте,
     уведомление «Снимки восстановлены после сбоя».
  5. Запустить второй раз из «Пуска» → вторая копия не открылась, первая показала ленту.
  6. «Выйти» → при следующем запуске лента пустая.
- [ ] **Шаг 4: Коммит** — `feat(strip): добавить ленту снимков, сессию на диске, трей и уведомления`.

---

### Задача 8: Захват экрана и выделение области

**Файлы:**
- Создать: `src/Snapkit.App/Capture/ScreenCapturer.cs`, `src/Snapkit.App/Capture/MonitorInfo.cs`
- Создать: `src/Snapkit.App/Capture/CaptureOverlayWindow.xaml(.cs)`, `src/Snapkit.App/Capture/CaptureController.cs`
- Изменить: `StripWindow.xaml.cs` («Новый снимок»), `TrayIcon.cs` («Новый снимок»)

**Интерфейсы:**
- Использует: `SessionService.AddCapture`, `RectI`.
- Создаёт:
```csharp
public sealed record MonitorInfo(nint Handle, RectI Bounds, RectI WorkArea, double DpiScale, bool IsPrimary);
public static class ScreenCapturer
{
    public static IReadOnlyList<MonitorInfo> GetMonitors();          // EnumDisplayMonitors + GetMonitorInfo + GetDpiForMonitor(MDT_EFFECTIVE_DPI)
    public static BitmapSource Capture(RectI physicalBounds);        // BitBlt SRCCOPY|CAPTUREBLT из GetDC(0), Bgra32, DPI 96, Freeze
}
public sealed class CaptureController
{
    public bool IsOpen { get; }
    public void StartNewCapture();      // уже открыт → ничего; лента полна → уведомление «В ленте максимум 20 снимков. Отправьте или удалите лишние»
    public event EventHandler? Closed;
}
```
Устройство:
- **Окна.** По одному окну `CaptureOverlayWindow` на монитор: `WindowStyle=None`,
  `Topmost`, `ShowInTaskbar=false`. В `SourceInitialized` окно ставится на физические
  границы монитора через `SetWindowPos(HWND_TOPMOST, …)`.
- **Кадр.** В окне показан кадр своего монитора, растянутый на всё окно: окно имеет
  размер монитора в DIP, поэтому 1 пиксель кадра = 1 физический пиксель. Поверх кадра —
  затемнение `#80000000`.
- **Координаты.** Позиция мыши в DIP × `DpiScale` = физический пиксель внутри монитора.
  Выделение ограничено одним монитором — тем, где начато **[решение плана: так проще
  и надёжнее при разном масштабе]**.
- **Порядок старта.** Сначала запомнить окно, активное до снимка. Потом снять кадры всех
  мониторов. Только после этого показать окна. Активировать окно монитора под курсором.
- **Выделение.** Перетаскиванием. Внутри выделения нет затемнения. Рамка цвета акцента
  с 8 маркерами. У рамки подпись «Ш × В». Сверху по центру подсказка «Выделите область ·
  Esc отменяет». После отпускания мыши рамку можно двигать и тянуть за маркеры.
  Выделение меньше 4×4 px не считается.
- **Завершение в этой задаче** (до редактора): Enter или двойной клик внутри рамки
  → `AddCapture(вырезка, физические границы на виртуальном экране, [], "")` → окна
  закрываются → фокус возвращается окну, активному до снимка. Esc закрывает все окна.

- [ ] **Шаг 1: Реализовать.**
- [ ] **Шаг 2: Собрать** `build.ps1`.
- [ ] **Шаг 3: Проверка:** «Новый снимок» → экран застыл и потемнел → выделить → Enter →
  в ленте новая карточка, миниатюра совпадает с выделенным.
- [ ] **Шаг 4: Проверка Esc:** во время выделения Esc → окна закрылись, лента не изменилась.
- [ ] **Шаг 5: Проверка ленты:** лента не попадает на снимок.
- [ ] **Шаг 6: Проверка масштаба (пункт 4 из «На что смотреть»):** если подключены
  мониторы с разным масштабом — снять область на каждом, вставить картинку в Paint →
  размер в пикселях и содержимое совпадают с экраном. Если второго монитора нет, проверить
  на одном мониторе при масштабе 125 % или 150 %.
- [ ] **Шаг 7: Коммит** — `feat(capture): добавить захват экрана и выделение области`.

---

### Задача 9: Редактор — инструменты, история, «Готово» и отмена

**Файлы:**
- Создать: `src/Snapkit.App/Editor/EditorSurface.xaml(.cs)`, `src/Snapkit.App/Editor/EditorCanvas.cs`, `src/Snapkit.App/Editor/EditorToolbar.xaml(.cs)`, `src/Snapkit.App/Editor/EditorTool.cs`
- Изменить: `CaptureOverlayWindow`, `CaptureController`

**Интерфейсы:**
- Использует: `AnnotationHistory`, `AnnotationGeometry`, `AnnotationPainter`, `Palette`,
  `CaptureRenderer`, `ImageCodecs`, `SessionService`.
- Создаёт:
```csharp
public enum EditorTool { Select, Rectangle, Arrow, Text, Pin }
public sealed record EditorResult(ImmutableList<Annotation> Annotations, string SnapshotNote, bool IsChanged);
public sealed class EditorSurface : UserControl
{
    public EditorSurface(BitmapSource image, ImmutableList<Annotation> annotations, string snapshotNote, char letter);
    public event EventHandler<EditorResult>? Done;
    public event EventHandler? Cancelled;
    public event EventHandler? CopyRequested;     // Ctrl+Shift+C; обработчик подключается в задаче 12
}
```
- **`EditorCanvas`** (`FrameworkElement`) имеет размер `PixelWidth × PixelHeight` DIP
  внутри `ScaleTransform(1 / DpiScale)`, так что его координаты совпадают с пикселями снимка.
  `OnRender` рисует все аннотации через `AnnotationPainter`, затем маркеры выделенной фигуры.
- **Панель инструментов** (как на `snapik-reference/editor-reopened.png`) стоит под областью,
  а если места нет — над ней, выровнена по левому краю области:
  - инструменты: Выбор (V), Прямоугольник (R), Стрелка (A), Текст (T), Комментарий (C);
  - затем цвет — 6 цветов `Palette.Markup` во всплывающем меню;
  - толщина — 2/4/6/8;
  - Отменить, Повторить, Копировать;
  - синяя кнопка «Готово».
- **Клавиши:** Ctrl+Z; Ctrl+Y и Ctrl+Shift+Z; Delete — удалить выделенную фигуру;
  Enter вне текстового поля — «Готово»; Esc; Ctrl+Shift+C — копировать.
- **Рамка и стрелка** рисуются перетаскиванием; меньше 4 px — не создаётся. Каждое
  изменение — `history.Push`.
- **Текст:** клик ставит `TextBox` на месте. Enter завершает, Shift+Enter переносит строку,
  пустой текст не создаётся.
- **Выбор:** клик через `TopmostHit` выделяет фигуру, перетаскивание сдвигает её
  (`Translate`), Delete удаляет.
- **Маркеры рамки области** из задачи 8 работают в режиме «Выбор». Если сдвинуть левую
  или верхнюю границу, все аннотации сдвигаются на ту же величину с обратным знаком,
  чтобы остаться на месте экрана.
- **Esc у нового снимка:** если `history.IsChanged`, спросить «Отменить снимок? Разметка
  будет потеряна.» (Да/Нет, окно-владелец — оверлей). Иначе закрыть сразу.
- **«Готово»** у нового снимка → `AddCapture(вырезка, границы, Current, note)`. Буква нового
  снимка — `'A' + UnsentCount`.
- После закрытия фокус возвращается окну, активному до снимка.

- [ ] **Шаг 1: Реализовать.** В задаче 8 по Enter снимок сразу уходил в ленту — теперь
  выделение открывает редактор.
- [ ] **Шаг 2: Собрать** `build.ps1`.
- [ ] **Шаг 3: Проверка:**
  1. Нарисовать рамку, стрелку и текст. Ctrl+Z трижды → пусто, Ctrl+Y → рамка вернулась.
  2. Сдвинуть рамку в режиме «Выбор», затем удалить её через Delete.
  3. Сменить цвет и толщину → новая фигура нарисована ими.
  4. «Готово» → в ленте миниатюра с разметкой.
  5. Esc с разметкой → вопрос; «Нет» — остаёмся в редакторе.
- [ ] **Шаг 4: Коммит** — `feat(editor): добавить редактор с рамкой, стрелкой, текстом и историей правок`.

---

### Задача 10: Комментарии, кружки, панель «Комментарии», повторное открытие

**Файлы:**
- Создать: `src/Snapkit.App/Editor/NoteEditorPopup.xaml(.cs)`, `src/Snapkit.App/Editor/CommentsPanel.xaml(.cs)`
- Изменить: `EditorSurface`, `EditorCanvas`, `CaptureController`, `StripWindow` (клик по карточке)

**Интерфейсы:**
- Использует: `NoteNumbers`, `CaptureLetters`, `SessionService.UpdateCapture`, `ScreenCapturer`.
- Создаёт: `CaptureController.Reopen(Guid captureId)`.

Поведение по описанию (6.3) и образцу `editor-reopened.png`:
- **Поле заметки.** Сразу после того как нарисована рамка или стрелка, под фигурой
  всплывает поле заметки: ширина 260, тёмная плашка со скруглением, крестик справа.
  Место — левый нижний угол фигуры + 8 px; если не помещается — над фигурой.
  - Enter сохраняет `Note` через `history.Push`; Shift+Enter переносит строку; Esc или «×»
    закрывают поле.
  - Начали рисовать новую фигуру при открытом поле: непустой текст сохраняется,
    пустое поле просто закрывается.
- **Комментарий без фигуры.** Инструмент «Комментарий»: клик ставит `Pin` и открывает
  поле. Если поле закрыто пустым, пин удаляется.
- **Правка заметки.** Двойной клик в режиме «Выбор» по фигуре с заметкой или по её
  кружку открывает поле с текстом заметки.
- **Кружки** рисуются с меткой `NoteNumbers.Label(letter, n)`.
- **Панель «Комментарии»:**
  - ширина 300, у правого края рабочей области монитора снимка, фон `#1E2128`,
    заголовок «Комментарии»;
  - пункты «[A1] текст» — клик выделяет фигуру, «×» удаляет заметку (у `Pin` — вместе
    с пином);
  - пустой список — «Нет комментариев»;
  - внизу поле «Комментарий к снимку» (многострочное) — это `SnapshotNote`.
- **Повторное открытие** — `Reopen`:
  - монитор — тот, что содержит центр `ScreenBounds`; фон — свежий кадр этого монитора
    с затемнением; снимок стоит ровно на `ScreenBounds`, рядом панели;
  - если такого монитора нет — основной монитор, снимок по центру, уменьшен, если не влезает;
  - маркеры рамки области скрыты;
  - Esc закрывает без изменений;
  - «Готово» при `IsChanged` → `UpdateCapture` (модель сама снимет «отправлен»);
  - буква — `CaptureLetters.Assign` по ленте, где у этого снимка `IsSent = false`.

- [ ] **Шаг 1: Реализовать.**
- [ ] **Шаг 2: Собрать** `build.ps1`.
- [ ] **Шаг 3: Проверка** по образцу `editor-reopened.png`:
  1. Рамка → под ней поле → «У Макса значок VK» → Enter → в углу рамки кружок A1,
     в панели «A1 У Макса значок VK».
  2. Вторая рамка без текста → начать рисовать стрелку → у второй рамки кружка нет.
  3. Инструмент «Комментарий» → клик → текст → кружок A2.
  4. «Комментарий к снимку» заполнен → «Готово» → в ленте значок комментария показывает 3.
  5. Клик по карточке → снимок на прежнем месте экрана со всеми кружками. Удалить A2
     → «Готово» → значок показывает 2.
- [ ] **Шаг 4: Коммит** — `feat(editor): добавить комментарии с кружками и повторное открытие снимка`.

---

### Задача 11: Хук клавиатуры и Print Screen

**Файлы:**
- Создать: `src/Snapkit.App/Input/KeyboardHookThread.cs`, `src/Snapkit.App/Input/InputSender.cs`
- Изменить: `App.xaml.cs`, `NativeMethods.cs`

**Интерфейсы:**
- Использует: `KeyRouter`, `KeyInput`, `KeyAction`, `PasteContext`, `KeyStroke` (задача 4).
- Создаёт:
```csharp
public sealed class KeyboardHookThread : IDisposable
{   // свой поток: SetWindowsHookEx(WH_KEYBOARD_LL, …, GetModuleHandle(null), 0) + цикл GetMessage; делегат хранится в поле
    public KeyboardHookThread(KeyRouter router, Action<KeyAction> onAction);   // onAction вызывается с потока хука — внутри только Dispatcher.BeginInvoke
    public void Start();
    public void Dispose();   // PostThreadMessage(WM_QUIT), UnhookWindowsHookEx, Join
}
public static class InputSender
{
    public const nuint Marker = 0x534E4B54;
    public static void Send(IReadOnlyList<KeyStroke> strokes);   // один вызов SendInput; wScan = MapVirtualKey; KEYEVENTF_EXTENDEDKEY для RCtrl, RAlt, LWin, RWin
}
```
В обработчике хука из `KBDLLHOOKSTRUCT` собирается
`KeyInput(vkCode, down = WM_KEYDOWN или WM_SYSKEYDOWN, injected = (flags & LLKHF_INJECTED) != 0, own = dwExtraInfo == Marker)`,
затем `router.Process` под `lock`. Подавление — `return 1`, иначе `CallNextHookEx`.

Функцию `pasteContext` App собирает только из потокобезопасных значений:
- `volatile bool hasUnsent` — обновляется по `SessionService.Changed`;
- `ClipboardService.IsPackageCurrent()` (до задачи 12 — `false`);
- `GetForegroundWindow` → `GetWindowThreadProcessId` == `Environment.ProcessId`.

Действие `CaptureRegion` → `CaptureController.StartNewCapture()` на UI-потоке.

- [ ] **Шаг 1: Реализовать.**
- [ ] **Шаг 2: Собрать** `build.ps1`.
- [ ] **Шаг 3: Проверка:** Print Screen → открылся режим снимка Snapkit, «Ножницы»
  не открылись. Shift+Print Screen работает как в Windows по умолчанию. Набор текста
  в Блокноте без задержек.
- [ ] **Шаг 4: Если «Ножницы» всё же открываются:** при старте проверить
  `HKCU\Control Panel\Keyboard\PrintScreenKeyForSnippingEnabled`. Если значение 1 и
  `AppSettings.SnippingHintShown` ещё false — показать уведомление «Print Screen
  открывает «Ножницы». Отключите это в параметрах Windows» с кнопкой «Открыть параметры»
  (`Process.Start` с `ms-settings:easeofaccess-keyboard`, `UseShellExecute = true`)
  и записать `SnippingHintShown = true`. Сама программа значение не меняет.
  Если «Ножницы» не открываются — этот шаг пропустить.
- [ ] **Шаг 5: Коммит** — `feat(input): добавить хук клавиатуры и снимок по Print Screen`.

---

### Задача 12: Буфер обмена — пакет, отложенная выдача, «Копировать пакет» и «Копировать снимок»

**Файлы:**
- Создать: `src/Snapkit.App/Clipboard/ClipboardService.cs`, `src/Snapkit.App/Clipboard/PackagePublisher.cs`
- Изменить: `App.xaml.cs`, `StripWindow` (меню «Копировать пакет», кнопка «Копировать снимок» на карточке, подсказка «Пакет вытеснен»), `EditorSurface` (`CopyRequested`)
- Тест: `tests/Snapkit.App.Tests/ClipboardServiceTests.cs`, `tests/Snapkit.App.Tests/ClipboardTestHost.cs`

**Интерфейсы:**
- Использует: `ImageCodecs`, `CaptureRenderer`, `PackageSelector`, `PromptBuilder`, `SessionService`.
- Создаёт:
```csharp
public enum ClipboardKind { None, Package, SingleCapture, PasteStep }
public sealed record ClipboardPayload(byte[]? Png, byte[]? Dib, string? Text);
public sealed class ClipboardBusyException : Exception;
public sealed class ClipboardService : IDisposable   // создаётся на UI-потоке; скрытое окно HwndSource с ParentWindow = HWND_MESSAGE
{
    public Task PublishAsync(ClipboardPayload payload, ClipboardKind kind, bool excludeFromHistory);
    public bool IsPackageCurrent();                                 // можно с любого потока: _kind == Package && GetClipboardOwner() == _hwnd
    public Task<bool> ExpectRender(int targetPid, TimeSpan timeout); // вызывать ДО PublishAsync
    public event EventHandler? ClipboardChanged;                    // AddClipboardFormatListener → WM_CLIPBOARDUPDATE
}
public sealed class PackagePublisher
{
    public Task PublishPackageAsync();               // пакет = PackageSelector.Select; картинка последнего снимка пакета + PromptBuilder; kind = Package
    public Task CopyPackageCommandAsync();           // если UnsentCount == 0 → MarkAllUnsent; затем PublishPackageAsync; уведомление «Пакет скопирован: N снимков. Нажмите Ctrl+V в чате»
    public Task CopyCaptureAsync(Guid id);           // одна картинка, kind = SingleCapture; уведомление «Снимок A скопирован»
}
```
**Устройство `ClipboardService`:**
- Форматы: `PNG = RegisterClipboardFormat("PNG")`, `CF_DIB = 8`, `CF_UNICODETEXT = 13`,
  `Exclude = RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing")`.
- **`PublishAsync`:**
  1. `OpenClipboard(_hwnd)` — до 10 попыток через 30 мс, затем `ClipboardBusyException`.
  2. `EmptyClipboard`.
  3. Каждый непустой формат — `SetClipboardData(fmt, 0)`, то есть отложенная выдача.
  4. Если `excludeFromHistory` — `SetClipboardData(Exclude, GlobalAlloc(GMEM_MOVEABLE, 4))`
     с нулями.
  5. `CloseClipboard`. Запомнить `payload` и `kind`.
- **`WM_RENDERFORMAT` (0x0305):**
  1. Прочитать `pid = GetWindowThreadProcessId(GetOpenClipboardWindow())`.
  2. Если `pid` совпадает с ожидаемым — завершить `ExpectRender` значением `true`.
  3. Выдать данные: `GlobalAlloc` плюс копирование, `SetClipboardData(fmt, h)` без
     `OpenClipboard`. Текст — UTF-16 с заменой `\n` на `\r\n` и завершающим нулём.
- **`WM_RENDERALLFORMATS` (0x0306):** `OpenClipboard`; если `GetClipboardOwner() == _hwnd` —
  выдать все форматы; `CloseClipboard`.
- **`WM_DESTROYCLIPBOARD` (0x0307):** `_kind = None`.
- **Уточнение к описанию (7.2):** «ничего не копировали после снимка» проверяется
  по **владельцу буфера** (`GetClipboardOwner`), а не по `GetClipboardSequenceNumber`.
  Номер меняется даже тогда, когда журнал буфера Windows читает наши отложенные данные.
  Поведение для пользователя то же.

**Когда публикуется пакет:**
- после `Added` и `Updated` в сессии;
- по «Копировать пакет»;
- после `Removed` — только если `IsPackageCurrent()` (чтобы не трогать чужой буфер).

`StripViewModel.PackageDisplaced = UnsentCount > 0 && !IsPackageCurrent()` пересчитывается
по `ClipboardChanged`. Когда пакет вытеснен, на ленте строка «Пакет вытеснен — нажмите
«Копировать пакет», чтобы вставить снимки».

- [ ] **Шаг 1: Написать падающие тесты** с меткой `[Trait("Category", "Clipboard")]`.
  `ClipboardTestHost` запускает STA-поток с `Dispatcher.Run()`, создаёт там
  `ClipboardService` и выполняет вызовы через `Dispatcher.Invoke`. Чтение «чужим
  процессом» в тестах имитирует тестовый поток: `OpenClipboard(0)`, `GetClipboardData`.
  - `Publish_ThenForeignRead_GetsPngBytes` — прочитанные байты PNG равны опубликованным.
  - `RenderedText_UsesCrLf` — `"a\nb"` читается как `"a\r\nb"`.
  - `ForeignRead_KeepsPackageCurrent` — после чтения `IsPackageCurrent() == true`.
  - `ForeignRead_DoesNotConfirmRenderForOtherPid` — `ExpectRender(pid: 1, 300 мс)` → `false`.
  - `ForeignRead_ConfirmsRenderForOwnPid` — `ExpectRender(Environment.ProcessId, 1 с)` → `true`.
  - `OtherOwnerWrites_PackageNotCurrent` — тестовый поток открывает буфер, выполняет
    `EmptyClipboard` и кладёт текст → `false`.
  - `Publish_WhileClipboardHeld_ThrowsBusy` — тестовый поток держит `OpenClipboard(0)` →
    `PublishAsync` бросает `ClipboardBusyException` быстрее 1 с.
- [ ] **Шаг 2: Запустить** `dotnet test tests/Snapkit.App.Tests --filter "Category=Clipboard"` →
  ошибки сборки. **Шаг 3: Реализовать. Шаг 4: Запустить** → все тесты проходят.
- [ ] **Шаг 5: Подключить** `PackagePublisher` к сессии, ленте и редактору. Собрать `build.ps1`.
- [ ] **Шаг 6: Проверка:**
  1. Снимок с комментарием → обычный Ctrl+V в Paint вставляет картинку, в Блокнот —
     текст «Снимок A…».
  2. Скопировать строку в Блокноте → на ленте строка «Пакет вытеснен». «Копировать пакет»
     → строка исчезла.
  3. Открыть журнал Win+V → строка «Пакет вытеснен» не появилась.
- [ ] **Шаг 7: Коммит** — `feat(clipboard): добавить пакет в буфере с отложенной выдачей`.

---

### Задача 13: Вставка пачки по Ctrl+V

**Файлы:**
- Создать: `src/Snapkit.App/Paste/PasteCoordinator.cs`
- Изменить: `App.xaml.cs` (`KeyAction.PasteBatch` → `PasteCoordinator`)

**Интерфейсы:**
- Использует: `KeyRouter` (`PasteInProgress`, `EscapePressedDuringPaste`, `Logical`, `Physical`),
  `PasteKeys`, `InputSender`, `ClipboardService`, `CaptureRenderer`, `ImageCodecs`,
  `PackageSelector`, `PromptBuilder`, `SessionService`, `RussianPlural`.
- Создаёт:
```csharp
public enum PasteStatus { CompletedVerified, CompletedUnverified, StoppedTargetChanged, StoppedByUser, ClipboardBusy, NothingToPaste }
public sealed record PasteOutcome(PasteStatus Status, int ImagesPasted, int NotesCount);
public sealed class PasteCoordinator
{
    public bool IsRunning { get; }
    public Task<PasteOutcome> PasteBatchAsync();   // повторный вызов во время работы → NothingToPaste
}
```
**Алгоритм:**
1. `router.PasteInProgress = true`, `EscapePressedDuringPaste = false`. Запомнить
   `target = GetForegroundWindow()` и его `pid`.
2. Пакет `PackageSelector.Select`; пусто → `NothingToPaste`. Заранее подготовить
   для каждого снимка PNG и DIB через `CaptureRenderer.Render` с его буквой, а также
   текст `PromptBuilder.Build`.
3. Ждать, пока в `router.Logical` не будет V и Ctrl, но не дольше 1000 мс (опрос раз
   в 15 мс).
4. Для каждого шага (картинки по порядку, затем текст, если он не пуст):
   1. Если `GetForegroundWindow() != target` → `StoppedTargetChanged`.
   2. Если `EscapePressedDuringPaste` → `StoppedByUser`.
   3. `var ok = clipboard.ExpectRender(pid, 1500 мс)`.
   4. `await PublishAsync(шаг, PasteStep, excludeFromHistory: true)`.
   5. `InputSender.Send(PasteKeys.CtrlV(router.Logical))`.
   6. `verified &= await ok`.
   7. Пауза: 120 мс, если шаг подтверждён, иначе 400 мс **[числа можно подобрать на шаге
      проверки]**.
5. В `finally`: `InputSender.Send(PasteKeys.Restore(router.Physical, router.Logical))`,
   `PasteInProgress = false`.
6. Успех: `MarkSent(id пакета)`; если `ClearStripAfterPaste` → `ClearAll()`. При остановке
   или ошибке снимки не помечаются.

**Уведомления:**
- проверено — «Вставлено: {изображения} и {заметки}» через `RussianPlural`,
  например «Вставлено: 2 изображения и 3 заметки»;
- без подтверждения — «Вставлено: … Проверить вставку не удалось»;
- окно сменилось — «Вставка остановлена: окно сменилось»;
- Esc — «Вставка остановлена»;
- буфер занят — «Буфер обмена занят другой программой. Повторите вставку».

После вставки в буфере остаётся текст последнего шага, перехват выключен
(неотправленных нет).

- [ ] **Шаг 1: Реализовать** и подключить.
- [ ] **Шаг 2: Собрать** `build.ps1`.
- [ ] **Шаг 3: Проверка в Claude Desktop:**
  1. Два снимка с комментариями. Клик в поле ввода → Ctrl+V → две картинки с кружками
     и текст. Сообщение не отправлено.
  2. То же через **Shift+колесо вверх** (AutoHotkey). Затем в Блокноте набрать
     «абв» → буквы маленькие (Shift не залип), Ctrl не залип.
- [ ] **Шаг 4: Проверка в Chrome** на claude.ai и в ChatGPT — то же самое.
- [ ] **Шаг 5: Проверка обычной вставки:**
  1. Снимок → скопировать строку в PhpStorm → Ctrl+V вставил строку, а не снимки.
  2. Неотправленных нет → Ctrl+V в чате работает как обычно.
- [ ] **Шаг 6: Проверка остановки:** 5 снимков, во время вставки переключиться в другое
  окно → вставка остановилась, в другое окно ничего не попало, уведомление.
- [ ] **Шаг 7: Подбор пауз:** если какая-то картинка теряется — увеличить паузы
  и повторить шаги 3–4. Итоговые значения записать константами с комментарием.
- [ ] **Шаг 8: Коммит** — `feat(paste): добавить вставку пачки снимков по Ctrl+V`.

---

### Задача 14: Приёмка этапа 1 вместе с пользователем

- [ ] **Шаг 1:** Пользователь проходит пункты 1–7 и 9 раздела 14 описания проекта
  (пункт 8 — если есть второй монитор, пункт 10 относится к этапу 3). Найденные ошибки
  исправляются по одной, с коммитом `fix(<область>): …` на каждую.
- [ ] **Шаг 2:** Пользователь неделю работает со Snapkit вместо Snapik. Его замечания
  становятся входными данными для планов этапов 2–4.
- [ ] **Шаг 3:** Завершение ветки — навык superpowers:finishing-a-development-branch.
