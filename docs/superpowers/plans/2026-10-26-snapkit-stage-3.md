# Snapkit, этап 3 — лента и файлы

> **Для исполнителей-агентов:** ОБЯЗАТЕЛЬНЫЙ НАВЫК: superpowers:subagent-driven-development
> (рекомендуется) или superpowers:executing-plans — выполнять задачу за задачей. Шаги
> отмечаются флажками (`- [ ]`).

**Цель:** пачкой следующего Ctrl+V можно управлять с ленты (переключатель на карточке,
повтор последней вставки, порядок перетаскиванием), снимки уходят файлами (перетаскивание
в другие окна, «Сохранить снимок…», «Сохранить пакет…»), удалённый снимок можно вернуть,
картинки импортируются из файлов, ленту можно не показывать, а переполненная лента сама
освобождает место от старых отправленных снимков.

**Архитектура:** как в этапах 1–2. Правила ленты — порядок, отметки «отправлен»,
вытеснение, место вставки при перетаскивании, имена файлов и `prompt.md` — в `Snapkit.Core`
с тестами. `SessionService` (App) добавляет к ним картинки в памяти, запись на диск
и события. Окно ленты и значок у часов только сообщают о действиях; что делать, решает
`App`. Картинки для файлов рисует тот же `CaptureRenderer.Render`, что и для чата.

**Технологии:** C# 14 / .NET 10, WPF, WinForms (значок у часов), xUnit — без новых пакетов.

**Описание:** [docs/superpowers/specs/2026-10-26-snapkit-stage-3-design.md](../specs/2026-10-26-snapkit-stage-3-design.md)
(главное) и основное [2026-10-23-snapkit-design.md](../specs/2026-10-23-snapkit-design.md),
разделы 6.5, 7, 8, 9. Где они расходятся, верно описание этапа 3.

## Общие ограничения

- Всё из «Общих ограничений» планов этапов 1 и 2, кроме ветки: работа идёт в ветке
  `feat/stage-3-strip`. Автор коммитов — уже настроенный в репозитории `Ozimnadius`;
  глобальную настройку git не трогать. Коммиты — Conventional Commits, описание на русском.
- Весь текст для пользователя и комментарии в коде — только на русском.
- `dotnet` может не быть в PATH: `"C:\Program Files\dotnet\dotnet.exe"`.
- Snapkit для ручных проверок запускать только через `build.ps1`.
- Тесты интерфейса, которым нужна тема, идут через `UiApp.Run` (`tests/Snapkit.App.Tests/UiApp.cs`),
  тесты с картинками без темы — через `StaRunner.Run`.
- Файлы ленты и настроек этапов 1–2 должны читаться: новые поля — только в конец записей
  и только с умолчаниями, новые значения перечислений — только в конец.
- Решения плана (значения, которых нет в описании):
  - имя файла и папки по времени — `yyyy-MM-dd_HH-mm-ss`; занятое имя — `имя (2)`, `имя (3)`…;
  - папка сохранения по умолчанию — `Изображения\Snapkit` (`Environment.SpecialFolder.MyPictures`);
  - временные файлы перетаскивания — `%LOCALAPPDATA%\Snapkit\drag\<guid>\`, вся папка `drag`
    очищается при запуске;
  - файл снимка с буквой — `Снимок A.png`, отправленного — `Снимок.png`; отправленный снимок
    рисуется с буквой `A` (как «Копировать снимок»);
  - `prompt.md` — сначала строки `![Снимок A](A.png)` по одной на снимок, пустая строка,
    затем текст для чата из `PromptBuilder.Build` (если он не пустой);
  - «самый старый отправленный» — с самым ранним `CreatedAt`, а не первый по порядку ленты;
  - выключение «Показывать ленту» сразу прячет ленту, включение — сразу показывает.

## На что смотреть при проверке

1. **Буквы на ленте совпадают с тем, что уйдёт в чат** после переключений значка,
   «Повторить последнюю вставку» и перестановки. Тесты в задаче 1
   (`MoveBefore_ChangesPackageOrder`, `SetSent_ToggleBack_ReturnsLetter`).
2. **Вытеснение не трогает неотправленные** и берёт самый старый отправленный, даже если
   его передвинули в конец ленты. Тесты в задаче 1 (`EvictOldestSent_AllUnsent_ReturnsNull`,
   `EvictOldestSent_TakesEarliestCreated`) и задаче 2 (`AddCapture_WhenFull_EvictsOldestSent`).
3. **Файлы наружу — с разметкой и размытием,** как в чате. Тест в задаче 5
   (`SavePackage_WritesRenderedImagesAndPrompt`, полосы под размытием не видны).
4. **«Вернуть» ставит снимок туда же и с той же отметкой;** если места уже нет — ничего
   не ломает. Тесты в задаче 2 (`Restore_PutsBackAtIndexWithSentFlag`, `Restore_WhenNoRoom_ReturnsFalse`).
5. **Старые настройки читаются,** лента по умолчанию показывается. Тест в задаче 7
   (`Load_OldSettingsWithoutShowStrip_ShowsStrip`).

---

## Подготовка

- [ ] **Создать ветку:** `git switch -c feat/stage-3-strip` от актуальной `main`.
- [ ] **Проверить, что всё зелёное:** `dotnet test Snapkit.slnx --filter "Category!=Clipboard&Category!=Desktop"`.

---

### Задача 1: Правила ленты в модели

**Файлы:**
- Изменить: `src/Snapkit.Core/Session/SessionModel.cs`, `src/Snapkit.Core/PackageSelector.cs`
- Тесты: `tests/Snapkit.Core.Tests/SessionModelTests.cs` (дописать или создать), `tests/Snapkit.Core.Tests/PackageSelectorTests.cs`

**Интерфейсы:**
- Создаёт в `SessionModel`:
```csharp
/// <summary>Есть место для нового снимка: лента не полна или в ней есть отправленный, которого можно вытеснить.</summary>
public bool CanMakeRoom { get; }
/// <summary>Отметка «отправлен» одного снимка (щелчок по значку карточки). Неизвестный Id — ничего.</summary>
public void SetSent(Guid id, bool isSent);
/// <summary>«Повторить последнюю вставку»: эти снимки — неотправленные, остальные — отправленные. Отсутствующие Id пропускаются.</summary>
public void ResendOnly(IReadOnlyCollection<Guid> ids);
/// <summary>Поставить снимок перед карточкой с номером slot в нынешнем порядке (slot == Count — в конец).</summary>
public void MoveBefore(Guid id, int slot);
/// <summary>Вставить снимок на место index (обрезается до 0…Count). Бросает, если лента полна.</summary>
public void Insert(int index, Capture capture);
/// <summary>Удалить отправленный снимок с самым ранним CreatedAt; null — отправленных нет.</summary>
public Capture? EvictOldestSent();
```
- Создаёт в `PackageSelector`:
```csharp
/// <summary>Снимки для «Сохранить пакет…» (правило 6.5): неотправленные, а если их нет — вся лента; буквы A, B… по порядку; отметки не меняются.</summary>
public static IReadOnlyList<LetteredCapture> ForExport(IReadOnlyList<Capture> strip);
```

- [ ] **Шаг 1: Тесты.** Лента из трёх снимков `a, b, c` (`TestData.Cap`), все неотправленные:
  - `SetSent_RemovesFromPackage`: `SetSent(b.Id, true)` → `PackageSelector.Select` даёт `[(A, a), (B, c)]`;
  - `SetSent_ToggleBack_ReturnsLetter`: затем `SetSent(b.Id, false)` → `[(A, a), (B, b), (C, c)]`;
  - `ResendOnly_MarksOthersSent`: `ResendOnly([c.Id, Guid.NewGuid()])` → неотправлен только `c`;
  - `MoveBefore_ChangesPackageOrder`: `MoveBefore(c.Id, 0)` → порядок `c, a, b`, буквы `C→A`;
    `MoveBefore(a.Id, 3)` из `a, b, c` → `b, c, a`; `MoveBefore(b.Id, 1)` и `MoveBefore(b.Id, 2)` — порядок не меняется;
  - `Insert_PutsAtIndex`: удалить `b`, `Insert(1, b)` → `a, b, c`; `Insert(99, x)` → в конец;
  - `EvictOldestSent_AllUnsent_ReturnsNull` и `CanMakeRoom` для полной ленты без отправленных — `false`;
  - `EvictOldestSent_TakesEarliestCreated`: отправлены `a` (CreatedAt +1 мин) и `c` (CreatedAt 0),
    `c` передвинут в конец → удаляется `c`;
  - `ForExport_NoUnsent_TakesWholeStripWithoutMarking`: все отправлены → три снимка с буквами
    A, B, C, `IsSent` у всех по-прежнему `true`; есть неотправленные → только они.
- [ ] **Шаг 2: Запустить — падают** (методов нет): `dotnet test tests/Snapkit.Core.Tests --filter "FullyQualifiedName~SessionModelTests|FullyQualifiedName~PackageSelectorTests"`.
- [ ] **Шаг 3: Реализовать** методы выше. Каждый изменяющий метод вызывает `RaiseChanged()`.
- [ ] **Шаг 4: Запустить — зелёные.** Затем весь `Snapkit.Core.Tests` — зелёный.
- [ ] **Шаг 5: Коммит** `feat(core): переключение, повтор, порядок и вытеснение снимков ленты`.

---

### Задача 2: Сессия — вернуть удалённый, повтор вставки, место для нового

**Файлы:**
- Изменить: `src/Snapkit.App/Session/SessionService.cs`, `src/Snapkit.App/Paste/PasteCoordinator.cs`
- Тесты: `tests/Snapkit.App.Tests/SessionServiceTests.cs`, `tests/Snapkit.App.Tests/PasteCoordinatorTests.cs`

**Интерфейсы:**
- Потребляет: методы `SessionModel` из задачи 1.
- Создаёт:
```csharp
public enum SessionChangeKind { Added, Updated, Removed, Cleared, SentMarked, Reordered }   // Reordered — в конец

/// <summary>Удалённый снимок для «Вернуть»: разметка, картинка и место в ленте.</summary>
public sealed record RemovedCapture(Capture Capture, BitmapSource Source, int Index);

public RemovedCapture? Remove(Guid id);                 // было void
public bool Restore(RemovedCapture removed);            // false — места нет; событие Added
public void SetSent(Guid id, bool isSent);              // событие SentMarked
public void RememberPaste(IReadOnlyList<Guid> ids);     // пачка последней вставки по Ctrl+V
public bool HasLastPaste { get; }
public void ResendLastPaste();                          // ResendOnly(запомненные), событие SentMarked
public void MoveBefore(Guid id, int slot);              // событие Reordered
```
- `AddCapture` перед добавлением при полной ленте вытесняет `EvictOldestSent()` (картинка
  уходит из памяти, событие `Removed` для вытесненного); если места нет — бросает
  `InvalidOperationException` (вызывающие проверяют `Model.CanMakeRoom` заранее).
- `PasteCoordinator.PasteBatchAsync`: сразу после получения непустого плана —
  `if (plan.FromStrip) session.RememberPaste(plan.CaptureIds);` (до любой остановки).

- [ ] **Шаг 1: Тесты** (`StaRunner`, как соседние):
  - `Restore_PutsBackAtIndexWithSentFlag`: три снимка, второй отмечен отправленным, удалён,
    `Restore` → снова второй, `IsSent == true`, `GetSource` даёт ту же картинку;
  - `Restore_WhenNoRoom_ReturnsFalse`: удалить один из 20 неотправленных, добавить новый, `Restore` → `false`, в ленте 20;
  - `AddCapture_WhenFull_EvictsOldestSent`: 20 снимков, отправлены два, добавить 21-й → в ленте 20,
    пропал отправленный с меньшим `CreatedAt`, событие `Removed` с его Id пришло;
  - `ResendLastPaste_SkipsDeleted`: `RememberPaste([a, b])`, удалить `b`, все отметить отправленными,
    `ResendLastPaste` → неотправлен только `a`;
  - `HasLastPaste_FalseUntilRemembered`;
  - в `PasteCoordinatorClipboardTests` (категория Clipboard): `StoppedPaste_IsRememberedAsLastPaste` —
    в сессии снимки `a, b, c`; план — картинки и Id `a, b` (`PastePlan.FromStrip = true`); `send`
    при первой картинке ставит `router.EscapePressedDuringPaste = true` → исход `StoppedByUser`,
    `session.HasLastPaste == true`; затем `ResendLastPaste` → неотправлены `a` и `b`, отправлен `c`.
- [ ] **Шаг 2: Запустить — падают:** `dotnet test tests/Snapkit.App.Tests --filter "FullyQualifiedName~SessionServiceTests"`
  и отдельно `--filter "FullyQualifiedName~StoppedPaste_IsRememberedAsLastPaste"`.
- [ ] **Шаг 3: Реализовать.** `Remove` возвращает индекс и картинку (из `_sources` или `GetSource`
  до удаления); `Restore` проверяет `Model.CanMakeRoom`, вытесняет при необходимости, затем
  `Model.Insert`, кладёт картинку в `_sources`, `Persist()`. Память о вставке — поле, не на диске.
- [ ] **Шаг 4: Запустить — зелёные.** Весь `Snapkit.App.Tests` без категорий Clipboard/Desktop — зелёный.
- [ ] **Шаг 5: Коммит** `feat(session): вернуть удалённый снимок, повтор вставки и вытеснение`.

---

### Задача 3: Карточка и меню — пачка, «Вернуть», повтор

**Файлы:**
- Изменить: `src/Snapkit.App/Strip/CaptureCardViewModel.cs`, `src/Snapkit.App/Strip/StripViewModel.cs`,
  `src/Snapkit.App/Strip/StripWindow.xaml(.cs)`, `src/Snapkit.App/App.xaml.cs`
- Тесты: `tests/Snapkit.App.Tests/StripViewModelTests.cs` (дописать или создать)

**Интерфейсы:**
- Потребляет: `SessionService.SetSent`, `ResendLastPaste`, `HasLastPaste`, `Remove`, `Restore`, `MarkAllUnsent`.
- Создаёт:
```csharp
// CaptureCardViewModel
public string BadgeToolTip { get; }   // «В пачке — щёлкните, чтобы убрать» / «Отправлен — щёлкните, чтобы вставить ещё раз»
// StripViewModel
public bool CanResendLast { get; set; }
// StripWindow
public event EventHandler<Guid>? ToggleSentRequested;
public event EventHandler? ResendLastRequested;
public event EventHandler? ResendAllRequested;
```
- Поведение:
  - значок карточки (кружок с буквой или галочка) — кнопка: щелчок поднимает `ToggleSentRequested`
    и **не** открывает редактор (событие `MouseLeftButtonUp` карточки не срабатывает: `e.Handled`);
  - меню «…»: после «Копировать пакет» — «Повторить последнюю вставку» (`IsEnabled = CanResendLast`)
    и «Вся лента в пачку»;
  - `App`: переключение → `SetSent(id, !capture.IsSent)`; повтор → `ResendLastPaste()`;
    «Вся лента в пачку» → `MarkAllUnsent()`; после каждого из трёх, если неотправленных > 0, —
    `RunClipboard(_publisher.PublishPackageAsync)` (пакет в буфер, перехват Ctrl+V включён);
    `CanResendLast` обновляется в `RefreshStrip`;
  - удаление карточки: `Remove` → `Toasts.Show("Снимок удалён", "Вернуть", () => { if (!_session.Restore(removed)) Toasts.Show("В ленте нет места, чтобы вернуть снимок"); })`.

- [ ] **Шаг 1: Тесты:** `Card_BadgeToolTip_Unsent` и `Card_BadgeToolTip_Sent` — точные строки выше;
  `Refresh_SentCardHasNoLetter` (`StaRunner`, миниатюры — картинки 1×1) — снимки `a, b, c`, `b` отправлен →
  у карточек буквы `A`, `null`, `B`, у второй `IsSent == true` и подсказка для отправленного.
- [ ] **Шаг 2: Запустить — падают.**
- [ ] **Шаг 3: Реализовать** модели, разметку значка-кнопки (стиль без рамки, курсор «рука»,
  подсказка `BadgeToolTip`), меню и связи в `App`.
- [ ] **Шаг 4: Запустить — зелёные;** весь `Snapkit.App.Tests` без Clipboard/Desktop — зелёный.
- [ ] **Шаг 5: Коммит** `feat(strip): переключатель пачки на карточке, повтор вставки и «Вернуть»`.

---

### Задача 4: Порядок перетаскиванием и перетаскивание в другие окна

**Файлы:**
- Создать: `src/Snapkit.Core/StripLayout.cs`, `src/Snapkit.App/Files/CaptureFiles.cs`
- Изменить: `src/Snapkit.App/Strip/StripWindow.xaml(.cs)`, `src/Snapkit.App/App.xaml.cs`
- Тесты: `tests/Snapkit.Core.Tests/StripLayoutTests.cs`, `tests/Snapkit.App.Tests/CaptureFilesTests.cs`

**Интерфейсы:**
- Создаёт:
```csharp
public static class StripLayout
{
    /// <summary>Перед какой карточкой бросили (0…count): y — от верха первой карточки; карточка i начинается
    /// на i × stripeHeight, последняя видна целиком (cardHeight). Граница — середина видимой части карточки.</summary>
    public static int DropSlot(double y, int count, double cardHeight, double stripeHeight);
    /// <summary>Мышь сдвинулась достаточно, чтобы это было перетаскивание, а не щелчок.</summary>
    public static bool IsDragStarted(double dx, double dy, double minX, double minY);
}
public static class CaptureFiles
{
    public static string DragFileName(char? letter);                     // «Снимок A.png» / «Снимок.png»
    /// <summary>PNG для перетаскивания в своей подпапке dragRoot\<guid>\; возвращает путь.</summary>
    public static string WriteDragFile(string dragRoot, BitmapSource rendered, char? letter);
    /// <summary>Удалить папку перетаскивания целиком; ошибки (файл занят) пропускаются.</summary>
    public static void CleanDragRoot(string dragRoot);
}
// StripWindow
public Func<Guid, string?>? DragFileFor { get; set; }        // App рисует снимок и пишет файл
public event EventHandler<(Guid Id, int Slot)>? ReorderRequested;
```
- Поведение: нажатие на карточке + сдвиг больше `SystemParameters.MinimumHorizontalDragDistance` /
  `MinimumVerticalDragDistance` (`IsDragStarted`) → `DragDrop.DoDragDrop` с `DataObject`:
  `DataFormats.FileDrop` = `[DragFileFor(id)]` и свой формат `"Snapkit.CaptureId"` = `id.ToString()`,
  эффекты `Copy | Move`. Лента — `AllowDrop`: данные со своим форматом → `ReorderRequested(id, DropSlot(...))`
  (координата — от верха списка карточек с учётом прокрутки). После перетаскивания щелчок
  по карточке не открывает редактор. `App`: `ReorderRequested` → `_session.MoveBefore`;
  `DragFileFor` → `CaptureRenderer.Render(source, capture, letter ?? 'A')` → `WriteDragFile`;
  при запуске — `CleanDragRoot(%LOCALAPPDATA%\Snapkit\drag)`.

- [ ] **Шаг 1: Тесты:**
  - `DropSlot` при `count = 3`, `cardHeight = 124`, `stripeHeight = 34`: `y = -5 → 0`, `10 → 0`, `20 → 1`,
    `60 → 2`, `118 → 2`, `138 → 3`, `500 → 3`; при `count = 0` → `0`;
  - `IsDragStarted(3, 2, 4, 4) == false`, `IsDragStarted(-4, 0, 4, 4) == true`, `IsDragStarted(0, 5, 4, 4) == true`;
  - `DragFileName('B') == "Снимок B.png"`, `DragFileName(null) == "Снимок.png"`;
  - `WriteDragFile_TwiceSameLetter_BothFilesExist` (временная папка теста; файлы — PNG указанного размера);
  - `CleanDragRoot_RemovesEverything` и не бросает на несуществующей папке.
- [ ] **Шаг 2: Запустить — падают.**
- [ ] **Шаг 3: Реализовать** `StripLayout`, `CaptureFiles` (часть этой задачи), перетаскивание в `StripWindow`, связи в `App`.
- [ ] **Шаг 4: Запустить — зелёные;** весь набор без Clipboard/Desktop — зелёный.
- [ ] **Шаг 5: Коммит** `feat(strip): порядок карточек и перетаскивание снимка в другие окна`.

---

### Задача 5: «Сохранить снимок…» и «Сохранить пакет…»

**Файлы:**
- Создать: `src/Snapkit.Core/PackageExport.cs`
- Изменить: `src/Snapkit.App/Files/CaptureFiles.cs`, `src/Snapkit.Core/Settings/AppSettings.cs`,
  `src/Snapkit.App/Strip/StripWindow.xaml(.cs)`, `src/Snapkit.App/App.xaml.cs`
- Тесты: `tests/Snapkit.Core.Tests/PackageExportTests.cs`, `tests/Snapkit.App.Tests/CaptureFilesTests.cs`

**Интерфейсы:**
- Потребляет: `PackageSelector.ForExport` (задача 1), `CaptureRenderer.Render`, `PromptBuilder.Build`.
- Создаёт:
```csharp
public static class PackageExport
{
    public static string TimestampName(DateTime time);                       // «2026-10-26_15-40-12»
    public static string ImageFileName(char letter);                         // «A.png»
    public static string BuildPromptMarkdown(IReadOnlyList<LetteredCapture> package);
    /// <summary>name, если свободно, иначе «name (2)», «name (3)»…</summary>
    public static string UniqueName(string name, Func<string, bool> isTaken);
}
// CaptureFiles
public static void SavePng(string path, BitmapSource rendered);
/// <summary>Создать parent\<время> (или «… (2)») и положить A.png, B.png… и prompt.md; вернуть путь папки.</summary>
public static string SavePackage(string parent, DateTime now, IReadOnlyList<(LetteredCapture Item, BitmapSource Rendered)> package);
public static string DefaultSaveFolder { get; }                          // MyPictures\Snapkit
// AppSettings — последним параметром:
string LastSaveFolder = ""
// StripWindow
public event EventHandler<Guid>? SaveCaptureRequested;   // кнопка на карточке, рядом с «Копировать снимок»
public event EventHandler? SavePackageRequested;         // меню «…» «Сохранить пакет…»
```
- Поведение `App`:
  - «Сохранить снимок…» — `Microsoft.Win32.SaveFileDialog`: фильтр `PNG|*.png`, имя `TimestampName(now) + ".png"`,
    папка `LastSaveFolder` или `DefaultSaveFolder` (создать); после сохранения `LastSaveFolder` = папка файла;
  - «Сохранить пакет…» — `Microsoft.Win32.OpenFolderDialog` с той же начальной папкой; снимки —
    `ForExport`, картинки — `CaptureRenderer.Render`; отметки не меняются; затем
    `Toasts.Show("Пакет сохранён", "Открыть папку", () => Process.Start("explorer.exe", $"\"{folder}\""))`;
    пустая лента — `Toasts.Show("Сначала сделайте снимок.")`;
  - ошибки записи — `Log.Error` и `Toasts.Error("Не удалось сохранить файл. Подробности — в журнале")`.

- [ ] **Шаг 1: Тесты:**
  - `TimestampName(new DateTime(2026, 10, 26, 15, 40, 12)) == "2026-10-26_15-40-12"`;
  - `UniqueName("x", n => n is "x" or "x (2)") == "x (3)"`, свободное — без номера;
  - `BuildPromptMarkdown`: два снимка, у A заметка «кнопка», у B заметок нет → строки
    `![Снимок A](A.png)`, `![Снимок B](B.png)`, пустая строка, затем ровно `PromptBuilder.Build(package)`;
    без заметок вообще — только строки картинок;
  - `SavePackage_WritesRenderedImagesAndPrompt` (`StaRunner`, временная папка): снимок с полосами
    по 2 px и фигурой `Blur` (как `Blur_InFinalImage_HidesStripes`) → в папке `A.png` и `prompt.md`;
    в `A.png` под размытием разброс яркости меньше 60; повторный вызов с тем же временем создаёт «… (2)»;
  - `Load_OldSettingsWithoutLastSaveFolder_IsEmpty` в `SettingsStoreTests`.
- [ ] **Шаг 2: Запустить — падают.**
- [ ] **Шаг 3: Реализовать** `PackageExport`, `CaptureFiles.SavePng/SavePackage`, настройку, кнопку и пункт меню, связи в `App`.
- [ ] **Шаг 4: Запустить — зелёные;** весь набор без Clipboard/Desktop — зелёный.
- [ ] **Шаг 5: Коммит** `feat(files): сохранение снимка и пакета с prompt.md`.

---

### Задача 6: Импорт картинок из файлов

**Файлы:**
- Изменить: `src/Snapkit.App/Imaging/ImageCodecs.cs`, `src/Snapkit.App/Strip/StripWindow.xaml(.cs)`, `src/Snapkit.App/App.xaml.cs`
- Тесты: `tests/Snapkit.App.Tests/ImageCodecsTests.cs`

**Интерфейсы:**
- Потребляет: `SessionModel.CanMakeRoom`, `SessionService.AddCapture` (вытесняет сам, задача 2).
- Создаёт:
```csharp
/// <summary>PNG или JPEG → Bgra32, 96 DPI, непрозрачная. Бросает NotSupportedException на другом формате.</summary>
public static BitmapSource DecodeImage(byte[] data);
// StripWindow
public event EventHandler? ImportFileRequested;            // меню «…» «Импортировать файл…»
public event EventHandler<string[]>? FilesDropped;         // PNG/JPEG из Проводника на ленту
```
- Поведение `App`: общий метод `ImportImages(IReadOnlyList<string> paths)` — берёт файлы
  `.png/.jpg/.jpeg` по порядку; перед каждым `Model.CanMakeRoom`, иначе остальные пропускаются;
  нечитаемый файл пропускается; картинка встаёт по центру основного монитора, как «Вставить
  изображение из буфера» (вынести общий `AddImportedImage(BitmapSource)`); итог —
  `Toasts.Show("Добавлено снимков: N")`, при пропусках — `"Добавлено снимков: N, не поместилось или не читается: M"`.
  «Импортировать файл…» — `Microsoft.Win32.OpenFileDialog`, `Multiselect = true`, фильтр
  `Изображения|*.png;*.jpg;*.jpeg`. Перетаскивание со своим форматом `"Snapkit.CaptureId"` импортом не считается.
- [ ] **Шаг 1: Тесты:** `DecodeImage_Jpeg_IsBgra32At96Dpi` (JPEG 30×20 через `JpegBitmapEncoder` с DPI 144 →
  `Bgra32`, 96 DPI, 30×20), `DecodeImage_Png_KeepsSize`, `DecodeImage_Garbage_Throws`.
- [ ] **Шаг 2: Запустить — падают.**
- [ ] **Шаг 3: Реализовать.**
- [ ] **Шаг 4: Запустить — зелёные;** весь набор без Clipboard/Desktop — зелёный.
- [ ] **Шаг 5: Коммит** `feat(strip): импорт PNG и JPEG из файла и перетаскиванием`.

---

### Задача 7: «Показывать ленту», отклик на новый снимок, полная лента

**Файлы:**
- Создать: `src/Snapkit.Core/Session/CaptureFeedback.cs`
- Изменить: `src/Snapkit.Core/Settings/AppSettings.cs`, `src/Snapkit.App/Shell/TrayIcon.cs`,
  `src/Snapkit.App/Strip/StripViewModel.cs`, `src/Snapkit.App/Strip/StripWindow.xaml.cs`,
  `src/Snapkit.App/Capturing/CaptureController.cs`, `src/Snapkit.App/App.xaml.cs`
- Тесты: `tests/Snapkit.Core.Tests/SettingsStoreTests.cs`, `tests/Snapkit.Core.Tests/CaptureFeedbackTests.cs`

**Интерфейсы:**
- Создаёт:
```csharp
// AppSettings — последним параметром (после LastSaveFolder):
bool ShowStrip = true
public static class CaptureFeedback
{
    public static string Ready(char letter);   // «Снимок A готов — Ctrl+V вставит»
    public const string StripFull = "В ленте 20 неотправленных снимков. Вставьте или удалите лишние";
}
// TrayIcon
public bool ShowStripChecked { get; set; }
public event EventHandler<bool>? ShowStripToggled;      // галочка «Показывать ленту» в меню
// StripViewModel
public bool ShowStripSetting { get; set; }
// StripWindow
public event EventHandler<bool>? ShowStripToggled;      // галочка «Показывать ленту» в меню «…»
// CaptureController
public event EventHandler<Guid>? CaptureAdded;          // новый снимок встал на ленту (после restoreChrome)
public event EventHandler? StripFull;                   // снимок не начат: место не освободить
```
- Поведение:
  - `CaptureController.StartNewCapture`: вместо `IsFull` — `if (!session.Model.CanMakeRoom) { StripFull?.Invoke(...); return; }`;
    `CaptureAdded` — после `AddCapture` нового снимка (не при повторном открытии);
  - `App`: при запуске лента показывается, только если `ShowStrip`; `CaptureAdded` → если `ShowStrip` —
    показать ленту без активации (`_strip.Show()`, если скрыта), иначе
    `Toasts.Show(CaptureFeedback.Ready(буква снимка))`; `StripFull` → `Toasts.Show(CaptureFeedback.StripFull)`
    и показать ленту; переключение галочки (из значка или меню «…») → `SaveSettings`, синхронизировать
    обе галочки, включили — показать ленту, выключили — спрятать; щелчок по значку у часов
    показывает и прячет ленту, как сейчас, не меняя настройку;
  - `ImportImageFromClipboard` и `ImportImages` вместо `IsFull` проверяют `CanMakeRoom` с тем же текстом `StripFull`.
- [ ] **Шаг 1: Тесты:** `Load_OldSettingsWithoutShowStrip_ShowsStrip` (JSON этапа 2 без поля → `ShowStrip == true`),
  `ShowStrip_RoundTrip`, `Ready_UsesLetter` (`Ready('B') == "Снимок B готов — Ctrl+V вставит"`).
- [ ] **Шаг 2: Запустить — падают.**
- [ ] **Шаг 3: Реализовать.**
- [ ] **Шаг 4: Запустить — зелёные;** весь набор без Clipboard/Desktop — зелёный.
- [ ] **Шаг 5: Коммит** `feat(strip): настройка «Показывать ленту» и отклик на новый снимок`.

---

### Задача 8: README, список приёмки, полный прогон

**Файлы:**
- Изменить: `README.md` (раздел «Как пользоваться»: значок-переключатель, «Повторить последнюю
  вставку», «Вся лента в пачку», перетаскивание, «Сохранить снимок…», «Сохранить пакет…»,
  импорт, «Показывать ленту», вытеснение старых отправленных)
- Создать: `docs/superpowers/plans/2026-10-26-snapkit-stage-3-acceptance.md` — по образцу этапа 2:
  «Перед началом» (полный прогон, `build.ps1`) и проверки: 1) переключатель и вставка
  выбранных; 2) повтор последней вставки, в том числе остановленной Esc; 3) порядок
  перетаскиванием и буквы во вставке; 4) перетаскивание в Telegram/Проводник/браузер —
  файл с разметкой и размытием; 5) «Сохранить снимок…» и «Сохранить пакет…» (`prompt.md`,
  «Открыть папку»); 6) «Вернуть»; 7) импорт меню и перетаскиванием; 8) «Показывать ленту»
  выкл/вкл, уведомление «Снимок A готов», лента возвращается после «Свернуть»; 9) 21-й снимок
  вытесняет старый отправленный; «После приёмки» — слияние и v0.3.0 только с согласия.

- [ ] **Шаг 1:** README и список приёмки.
- [ ] **Шаг 2:** полный прогон `dotnet test Snapkit.slnx` (с Clipboard и Desktop) — зелёный.
- [ ] **Шаг 3: Коммит** `docs(stage-3): README и список приёмки`.
