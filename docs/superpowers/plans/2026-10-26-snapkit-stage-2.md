# Snapkit, этап 2 — весь редактор

> **Для исполнителей-агентов:** ОБЯЗАТЕЛЬНЫЙ НАВЫК: superpowers:subagent-driven-development
> (рекомендуется) или superpowers:executing-plans — выполнять задачу за задачей. Шаги
> отмечаются флажками (`- [ ]`).

**Цель:** в редакторе есть все инструменты из раздела 6.2 описания: фигуры, стрелки, линия,
карандаш, маркер, ластик, размытие, обрезка, заливки, стили линий, свои цвета и пипетка,
изменение размера фигур, перемещение кружка, подсказка «?».

**Архитектура:** как в этапе 1. Вся геометрия (границы, попадание, маркеры размера, обрезка,
кружки) и размытие пикселей — в `Snapkit.Core`, с тестами. `Snapkit.App` только рисует
и обрабатывает мышь. Фигура остаётся объектом `Annotation`: новые виды и поля добавляются
с умолчаниями, поэтому ленты этапа 1 читаются без потерь. Размытие и пипетка берут пиксели
из одного источника — `Source` в `EditorContext` и `BlurSource` при отрисовке.

**Технологии:** C# 14 / .NET 10, WPF, xUnit — без новых пакетов.

**Описание проекта:** [docs/superpowers/specs/2026-10-23-snapkit-design.md](../specs/2026-10-23-snapkit-design.md),
разделы 6.1–6.3. План этапа 1 — [2026-10-23-snapkit-stage-1.md](2026-10-23-snapkit-stage-1.md).

## Общие ограничения

- Всё из «Общих ограничений» плана этапа 1, кроме ветки: работа идёт в ветке
  `feat/stage-2-editor`. Автор коммитов — уже настроенный в репозитории `Ozimnadius`
  со скрытой почтой; глобальную настройку git не трогать.
- Весь текст для пользователя — только на русском. Комментарии в коде — на русском.
- `dotnet` может не быть в PATH: тогда `& "$env:ProgramFiles\dotnet\dotnet.exe"`.
- Snapkit для ручных проверок запускать только через `build.ps1` (он запускает программу
  через Проводник). Из терминала Claude Desktop нельзя запускать `Snapkit.exe` напрямую:
  Windows подменит ему `AppData`.
- Фигуры хранятся в файле ленты. Новые поля — только с умолчаниями, новые значения
  перечислений — только в конец.
- Цвета и размеры этапа (решения плана):
  - маркер — ширина `толщина × 4`, непрозрачность 35 %, концы плоские;
  - полупрозрачная заливка — 30 % цвета заливки;
  - скругление — `min(12 + толщина, min(ширина, высота) / 2)`;
  - пунктир — штрих `3 × толщина`, промежуток `2 × толщина`; точки — штрих 0 с круглым
    концом, промежуток `2 × толщина`;
  - размытие — усреднение блоками 12 px снимка и сглаживание; область без контура;
  - маркеры размера — квадраты 9 DIP, белые с обводкой цвета акцента; попадание — 8 DIP;
  - карандаш и маркер прореживаются: точка ближе 2 px к предыдущей не сохраняется;
  - своих цветов — не больше 8, новые первыми.

## На что смотреть при проверке

1. **Старая лента после обновления.** Снимки, нарисованные в этапе 1, открываются,
   правятся и вставляются как раньше. Тест в задаче 1 (`Stage1Json_LoadsWithDefaults`).
2. **Размытие действительно прячет.** В итоговой картинке, которая уходит в чат, текст
   под размытием не читается — не только в редакторе. Тесты в задачах 5 и 6
   (`Obscure_Stripes_LoseContrast`, `Blur_InFinalImage_HidesStripes`).
3. **Обрезка снимка из ленты.** Картинка, разметка, кружки и место на экране согласованы;
   после повторного открытия снимок стоит там, где был обрезан. Тесты в задачах 4 и 11
   (`Crop_ShiftsShapes`, `UpdateCaptureImage_ReplacesSourceOnDiskAndInMemory`).
4. **Кружок не теряется за краем.** После обрезки или уменьшения области кружок
   с заметкой целиком внутри снимка. Тест в задаче 4 (`Crop_PinOutside_MovesInside`).
5. **Редактор не тормозит.** Перетаскивание большой области размытия и длинный штрих
   карандаша не замедляют отрисовку. Размытая картинка считается один раз на область
   (кэш, задача 6), штрих прореживается (задача 5, `Simplify_DropsTooClosePoints`).

---

## Подготовка

- [ ] **Создать ветку:** `git switch -c feat/stage-2-editor` от актуальной `main`.
- [ ] **Проверить, что всё зелёное:** `dotnet test Snapkit.slnx --filter "Category!=Clipboard&Category!=Desktop"`.

---

### Задача 1: Модель фигур этапа 2 и совместимость со старыми лентами

**Файлы:**
- Изменить: `src/Snapkit.Core/Model/Annotation.cs`, `src/Snapkit.Core/Editing/AnnotationGeometry.cs`,
  `src/Snapkit.App/Session/SessionService.cs` (`IsUsable`)
- Тесты: `tests/Snapkit.Core.Tests/SessionStoreTests.cs`, `tests/Snapkit.Core.Tests/AnnotationGeometryTests.cs`

**Интерфейсы:**
- Создаёт:
```csharp
public enum AnnotationKind { Rectangle, Arrow, Text, Pin, RoundedRectangle, Ellipse, CurvedArrow, WideArrow, Line, Pencil, Marker, Blur }
public enum FillMode { None, Solid, SemiTransparent, Blur }
public enum LineStyle { Solid, Dashed, Dotted }

// Points: Rectangle, RoundedRectangle, Ellipse, Blur — [угол, противоположный угол];
// Arrow, WideArrow, Line — [начало, конец]; CurvedArrow — [начало, точка изгиба на кривой, конец];
// Pencil, Marker — ломаная; Text, Pin — [точка привязки].
public sealed record Annotation(Guid Id, AnnotationKind Kind, ImmutableArray<PointD> Points, string Color,
    double Thickness, string? Text, string? Note,
    FillMode Fill = FillMode.None,
    string? FillColor = null,          // null — цвет обводки
    LineStyle LineStyle = LineStyle.Solid,
    PointD? PinOffset = null);         // сдвиг кружка от его места по умолчанию

public static int AnnotationGeometry.MinPoints(AnnotationKind kind);   // CurvedArrow 3; Text, Pin 1; остальные 2
public static bool AnnotationGeometry.IsWellFormed(Annotation a);      // точек не меньше MinPoints, все координаты и толщина конечны, толщина > 0
```
- `SessionService.IsUsable` проверяет фигуры через `IsWellFormed` вместо своего правила.

- [ ] **Шаг 1: Тесты.**
```csharp
[Fact] void Stage1Json_LoadsWithDefaults()
// session.json в виде этапа 1 (Rectangle без Fill, FillColor, LineStyle, PinOffset) и PNG рядом →
// Load: Fill == None, FillColor == null, LineStyle == Solid, PinOffset == null, остальное как в файле
[Fact] void Stage2Fields_RoundTrip()
// Ellipse с Fill = Blur, FillColor = "#43A047", LineStyle = Dashed, PinOffset = (5, -3)
// и CurvedArrow из трёх точек → Save, Load → поля и точки (.ToArray()) совпадают
[Theory] void MinPoints_PerKind(AnnotationKind kind, int expected)
// CurvedArrow → 3; Text, Pin → 1; Rectangle, Ellipse, Line, Pencil, Marker, Blur, WideArrow → 2
[Fact] void IsWellFormed_CurvedArrowWithTwoPoints_IsFalse()
[Fact] void IsWellFormed_NaNPoint_IsFalse()
```
- [ ] **Шаг 2: Запуск** — `dotnet test tests/Snapkit.Core.Tests --filter "FullyQualifiedName~SessionStoreTests|FullyQualifiedName~AnnotationGeometryTests"`.
  Ожидается: не компилируется (нет новых типов).
- [ ] **Шаг 3: Реализация.** Новые параметры записи — в конце, с умолчаниями. `IsUsable`
  в `SessionService` вызывает `AnnotationGeometry.IsWellFormed` для каждой фигуры, проверка
  цвета через `Palette.Color` остаётся и для `FillColor`, если он задан.
- [ ] **Шаг 4: Запуск** тех же тестов — зелёные; весь набор Core и App зелёный.
- [ ] **Шаг 5: Коммит** — `feat(core): добавить фигуры, заливку, стиль линии и сдвиг кружка в модель`.

---

### Задача 2: Геометрия новых фигур

**Файлы:**
- Изменить: `src/Snapkit.Core/Editing/AnnotationGeometry.cs`
- Тесты: `tests/Snapkit.Core.Tests/AnnotationGeometryTests.cs`

**Интерфейсы:**
- Использует: модель из задачи 1.
- Создаёт и меняет:
```csharp
public static double AnnotationGeometry.MarkerWidth(double thickness);          // thickness * 4
public static double AnnotationGeometry.WideArrowHeadHalfWidth(double thickness); // thickness * 1.5 + 6
public static IReadOnlyList<PointD> AnnotationGeometry.CurvePoints(Annotation curvedArrow, int segments);
// квадратичная кривая Безье через Points[0], Points[1] (точка изгиба лежит на кривой при t = 0.5), Points[2];
// контрольная точка = 2 * изгиб − (начало + конец) / 2; возвращает segments + 1 точек
public static PointD AnnotationGeometry.DefaultBend(PointD start, PointD end);
// середина отрезка, сдвинутая влево от направления на 20 % длины — стрелка сразу видна изогнутой
public static PointD AnnotationGeometry.PinCenter(Annotation a);   // + PinOffset; место по умолчанию:
// Rectangle, RoundedRectangle, Ellipse, Blur — левый верхний угол границ; остальные — Points[0]
public static (double X, double Y, double W, double H) AnnotationGeometry.Bounds(Annotation a);   // + новые виды
public static bool AnnotationGeometry.HitTest(Annotation a, PointD p, double tolerance);          // + новые виды
public static bool AnnotationGeometry.IsBigEnough(Annotation a, double minSize);
// рамки, овал, размытие — обе стороны ≥ minSize; линии и стрелки — длина ≥ minSize;
// карандаш и маркер — не меньше 2 точек и длина ломаной ≥ minSize
```
Правила попадания:
- рамка, скруглённая рамка, овал: только контур, если `Fill == None`; с заливкой — и внутри;
- размытие — внутри области;
- линия, стрелка — расстояние до отрезка ≤ допуск + толщина / 2; широкая стрелка —
  ≤ допуск + `WideArrowHeadHalfWidth`; изогнутая — до ломаной `CurvePoints(a, 24)`;
- карандаш — до ломаной ≤ допуск + толщина / 2; маркер — ≤ допуск + `MarkerWidth` / 2.

- [ ] **Шаг 1: Тесты** (значения — в пикселях снимка, допуск 4):
```csharp
[Fact] void Ellipse_HitOnOutline_NotInside_WhenNotFilled()   // овал (0,0)-(100,60): (50,0) — да, (50,30) — нет
[Fact] void Ellipse_HitInside_WhenFilled()                   // тот же с Fill = Solid: (50,30) — да
[Fact] void Rectangle_SemiTransparentFill_HitInside()
[Fact] void Blur_HitInside()
[Fact] void CurvedArrow_PassesThroughBendPoint()             // CurvePoints(a, 24)[12] ≈ Points[1] (±0.01)
[Fact] void CurvedArrow_HitNearCurve_NotOnChord()            // начало (0,0), изгиб (50,40), конец (100,0): (50,40) — да, (50,0) — нет
[Fact] void Bounds_CurvedArrow_ContainsBendPoint()
[Fact] void Pencil_HitNearPolyline_NotFarAway()
[Fact] void Marker_HitWithinMarkerWidth()                    // толщина 4: точка в 7 px от линии — да, в 15 — нет
[Fact] void PinCenter_Ellipse_IsTopLeftOfBounds()
[Fact] void PinCenter_AppliesPinOffset()
[Fact] void DefaultBend_IsOffMidpointBy20PercentOfLength()
[Fact] void IsBigEnough_PencilWithOnePoint_IsFalse()
```
- [ ] **Шаг 2: Запуск** — `--filter FullyQualifiedName~AnnotationGeometryTests` → новые тесты красные.
- [ ] **Шаг 3: Реализация.** Старые тесты геометрии не меняются и остаются зелёными.
- [ ] **Шаг 4: Запуск** — зелёные; весь набор Core зелёный.
- [ ] **Шаг 5: Коммит** — `feat(core): описать геометрию овала, линий, изогнутых стрелок, карандаша и размытия`.

---

### Задача 3: Маркеры изменения размера и перемещение кружка

**Файлы:**
- Создать: `src/Snapkit.Core/Editing/AnnotationHandles.cs`
- Тесты: `tests/Snapkit.Core.Tests/AnnotationHandlesTests.cs`

**Интерфейсы:**
```csharp
public static class AnnotationHandles
{
    // Rectangle, RoundedRectangle, Ellipse, Blur — 8 точек по часовой с левого верхнего угла:
    // СЗ, С, СВ, В, ЮВ, Ю, ЮЗ, З. Arrow, WideArrow, Line — [начало, конец].
    // CurvedArrow — [начало, изгиб, конец]. Text, Pin, Pencil, Marker — нет маркеров (только перемещение).
    public static IReadOnlyList<PointD> Of(Annotation a);
    public static int? Hit(Annotation a, PointD p, double radius);
    public static Annotation Move(Annotation a, int handle, PointD to);   // рамки не выворачиваются: углы нормализуются
    public static bool HitPin(Annotation a, PointD p, double tolerance);  // только у фигур с заметкой
    public static Annotation WithPinAt(Annotation a, PointD center);      // PinOffset так, чтобы PinCenter == center
}
```
`AnnotationGeometry.Translate` сдвигает точки и не трогает `PinOffset` — кружок едет вместе с фигурой.

- [ ] **Шаг 1: Тесты:**
```csharp
[Fact] void Of_Rectangle_IsCornersAndMidpointsClockwise()
// (10,20)-(110,70) → (10,20) (60,20) (110,20) (110,45) (110,70) (60,70) (10,70) (10,45)
[Fact] void Move_SouthEast_ResizesRectangle()              // маркер 4 → (150,100): границы (10,20,140,80)
[Fact] void Move_North_ChangesOnlyTop()
[Fact] void Move_NorthWestPastOpposite_KeepsPositiveSize()
[Fact] void Move_ArrowEnd_MovesOnlyEnd()
[Fact] void Move_CurvedArrowBend_MovesOnlyBend()
[Fact] void Of_PencilTextPin_AreEmpty()
[Fact] void WithPinAt_PinCenterEqualsTarget()
[Fact] void WithPinAt_DoesNotMoveShape()
[Fact] void Translate_KeepsPinOffset()
[Fact] void HitPin_WithoutNote_IsFalse()
```
- [ ] **Шаг 2: Запуск** — красные (нет класса).
- [ ] **Шаг 3: Реализация.**
- [ ] **Шаг 4: Запуск** — зелёные; весь набор Core зелёный.
- [ ] **Шаг 5: Коммит** — `feat(core): добавить маркеры размера фигур и перемещение кружка`.

---

### Задача 4: Обрезка и кружки внутри снимка

**Файлы:**
- Изменить: `src/Snapkit.Core/Editing/AnnotationGeometry.cs`
- Тесты: `tests/Snapkit.Core.Tests/AnnotationCropTests.cs`

**Интерфейсы:**
```csharp
// Кружки с заметкой, центр которых ближе PinDiameter / 2 к краю или за краем снимка width × height,
// получают PinOffset: кружок целиком внутри. Остальные фигуры не меняются.
public static ImmutableList<Annotation> AnnotationGeometry.KeepPinsInside(ImmutableList<Annotation> items, int width, int height);
// Сдвиг на (−crop.X, −crop.Y), затем KeepPinsInside(crop.Width, crop.Height). Фигуры не удаляются:
// то, что за краем, обрежет сама картинка.
public static ImmutableList<Annotation> AnnotationGeometry.Crop(ImmutableList<Annotation> items, RectI crop);
```
- [ ] **Шаг 1: Тесты:**
```csharp
[Fact] void Crop_ShiftsShapes()                     // рамка (50,60)-(90,100), обрезка (20,30,200,200) → (30,30)-(70,70)
[Fact] void Crop_PinOutside_MovesInside()           // рамка (5,5)-(60,60) с заметкой, обрезка (20,20,100,100) → центр кружка в [13; 87] по обеим осям
[Fact] void Crop_PinInside_IsUntouched()            // PinOffset остаётся null
[Fact] void Crop_KeepsShapesPartlyOutside()         // число фигур не меняется
[Fact] void KeepPinsInside_ShapeWithoutNote_IsUntouched()
[Fact] void KeepPinsInside_StandalonePin_MovesInside()
```
- [ ] **Шаг 2–4:** красные → реализация → зелёные, весь Core зелёный.
- [ ] **Шаг 5: Коммит** — `feat(core): обрезать разметку и держать кружки внутри снимка`.

---

### Задача 5: Размытие пикселей и прореживание штрихов

**Файлы:**
- Создать: `src/Snapkit.Core/Imaging/ImageFilters.cs`, `src/Snapkit.Core/Editing/StrokeSimplifier.cs`
- Тесты: `tests/Snapkit.Core.Tests/ImageFiltersTests.cs`, `tests/Snapkit.Core.Tests/StrokeSimplifierTests.cs`

**Интерфейсы:**
```csharp
public static class ImageFilters
{
    public const int ObscureBlock = 12;
    // pixels — BGRA32 всего изображения, stride = width * 4. Возвращает пиксели области area,
    // пересечённой с изображением: среднее по блокам ObscureBlock, затем сглаживание
    // по соседним блокам, альфа 255. Пустое пересечение — Width = Height = 0.
    public static (byte[] Pixels, RectI Area) ObscureArea(byte[] pixels, int width, int height, RectI area);
}
public static class StrokeSimplifier
{
    // Первая и последняя точки всегда остаются; точка ближе minStep к последней сохранённой — пропускается.
    public static ImmutableArray<PointD> Simplify(IReadOnlyList<PointD> points, double minStep);
}
```
- [ ] **Шаг 1: Тесты:**
```csharp
[Fact] void Obscure_UniformArea_KeepsColor()
[Fact] void Obscure_Checkerboard_BecomesNearlyFlat()      // шахматка 1 px 48×48: разброс каждого канала < 40
[Fact] void Obscure_Stripes_LoseContrast()                 // вертикальные полосы 2 px чёрная/белая: разброс < 60
[Fact] void Obscure_AreaPartlyOutside_IsClamped()          // area (-10,-10,30,30) на 20×20 → Area == (0,0,20,20)
[Fact] void Obscure_NoIntersection_ReturnsEmpty()
[Fact] void Obscure_DoesNotChangeInput()
[Fact] void Simplify_DropsTooClosePoints()
[Fact] void Simplify_KeepsFirstAndLast()
[Fact] void Simplify_SinglePoint_StaysSingle()
```
- [ ] **Шаг 2–4:** красные → реализация → зелёные.
- [ ] **Шаг 5: Коммит** — `feat(core): размывать область снимка и прореживать штрихи`.

---

### Задача 6: Отрисовка новых фигур, заливок, стилей линий и размытия

**Файлы:**
- Изменить: `src/Snapkit.App/Rendering/AnnotationPainter.cs`, `src/Snapkit.App/Rendering/CaptureRenderer.cs`
- Создать: `src/Snapkit.App/Rendering/ObscuredImages.cs`
- Тесты: `tests/Snapkit.App.Tests/AnnotationPainterTests.cs`, `tests/Snapkit.App.Tests/CaptureRendererTests.cs`

**Интерфейсы:**
- Использует: `AnnotationGeometry`, `ImageFilters.ObscureArea`.
- Создаёт:
```csharp
// Откуда брать пиксели для размытия: Origin — где пиксель (0,0) картинки в координатах фигур.
public sealed record BlurSource(BitmapSource Image, PointD Origin);

public static void AnnotationPainter.DrawAll(DrawingContext dc, IEnumerable<Annotation> annotations, char? letter, BlurSource? source = null);
public static void AnnotationPainter.Draw(DrawingContext dc, Annotation a, BlurSource? source = null);

// Размытая копия области: считается один раз на (картинка, область), кэш слабо связан с картинкой.
internal static class ObscuredImages { public static BitmapSource? Get(BlurSource source, RectI areaInImage); }
```
- `CaptureRenderer` передаёт `new BlurSource(source, new PointD(0, 0))`.
- Отрисовка (значения — из «Общих ограничений»):
  - контуры рамок, овала, линии, стрелок и карандаша — со стилем линии;
  - заливка `Solid` — цвет заливки; `SemiTransparent` — 30 %; `Blur` — размытая картинка,
    обрезанная по форме фигуры (`PushClip`), затем контур;
  - `Blur` — размытая картинка области, без контура; без `source` — серый прямоугольник 60 %;
  - широкая стрелка — залитый клин от тонкого начала к наконечнику;
  - изогнутая стрелка — кривая `CurvePoints` и наконечник по направлению последнего отрезка;
  - маркер — ломаная 35 % цвета шириной `MarkerWidth`, концы плоские;
  - кружок — в `PinCenter` (с учётом `PinOffset`).

- [ ] **Шаг 1: Тесты** (как `EditorCanvasTests`: `StaRunner`, `RenderTargetBitmap`, проверка пикселей):
```csharp
[Fact] void Blur_InFinalImage_HidesStripes()      // CaptureRenderer.Render: полосы под размытием — разброс < 60, вне его — без изменений
[Fact] void Ellipse_SolidFill_FillsCenter()
[Fact] void SemiTransparentFill_MixesWithBackground()   // центр между фоном и цветом заливки
[Fact] void Marker_IsTranslucent()
[Fact] void DashedRectangle_HasGapsOnEdge()
[Fact] void CurvedArrow_DrawsThroughBend()
[Fact] void PinOffset_MovesPinCircle()                  // цвет акцента в сдвинутом центре, фон — в месте по умолчанию
[Fact] void ObscuredImages_SameArea_ReturnsCachedInstance()
```
- [ ] **Шаг 2: Запуск** — `dotnet test tests/Snapkit.App.Tests --filter "FullyQualifiedName~AnnotationPainterTests|FullyQualifiedName~CaptureRendererTests"` → красные.
- [ ] **Шаг 3: Реализация.**
- [ ] **Шаг 4: Запуск** — зелёные; весь набор (без буфера и окон) зелёный.
- [ ] **Шаг 5: Коммит** — `feat(render): рисовать новые фигуры, заливки, пунктир и размытие`.

---

### Задача 7: Источник пикселей в редакторе — фон, размытие, пипетка

**Файлы:**
- Изменить: `src/Snapkit.App/Editor/EditorTool.cs` (`EditorContext`), `src/Snapkit.App/Editor/EditorCanvas.cs`,
  `src/Snapkit.App/Editor/EditorSurface.cs`, `src/Snapkit.App/Capturing/CaptureController.cs`
- Создать: `src/Snapkit.App/Imaging/PixelSampler.cs`
- Тесты: `tests/Snapkit.App.Tests/PixelSamplerTests.cs`

**Интерфейсы:**
```csharp
// Вместо BitmapSource? Background:
public sealed record EditorContext(Size MonitorSize, double Scale, RectI Region, RectI WorkArea,
    BitmapSource Source,      // новый снимок — кадр монитора; повторное открытие — исходник снимка
    PointD SourceOrigin,      // где пиксель (0,0) Source в координатах монитора: (0,0) или место снимка
    bool ShowSource,          // рисовать Source на холсте (повторное открытие); у нового снимка его показывает окно
    ImmutableList<Annotation> Annotations, string SnapshotNote, char Letter, bool RegionEditable);

public static class PixelSampler { public static string? HexAt(BitmapSource image, PointD origin, PointD point); } // "#RRGGBB" или null за краем
```
- `EditorCanvas` получает `Source`, `SourceOrigin`, `ShowSource`; фон рисуется в `SourceOrigin − Offset`,
  а в `DrawAll` уходит `new BlurSource(Source, SourceOrigin)`.
- `CaptureController.StartEditor` — `Source = window.Frame`, `SourceOrigin = (0,0)`, `ShowSource = false`;
  `Reopen` — `Source = source`, `SourceOrigin = (local.X, local.Y)`, `ShowSource = true`.

- [ ] **Шаг 1: Тесты:** `HexAt_ReturnsPixelColor`, `HexAt_RespectsOrigin`, `HexAt_OutsideImage_IsNull`.
- [ ] **Шаг 2–4:** красные → реализация → зелёные, весь набор зелёный.
- [ ] **Шаг 5: Ручная проверка** (`build.ps1`): новый снимок и повторное открытие выглядят как
  раньше; рамка рисуется на месте.
- [ ] **Шаг 6: Коммит** — `refactor(editor): передавать редактору исходник снимка для размытия и пипетки`.

---

### Задача 8: Панель инструментов этапа 2, клавиши и подсказка «?»

**Файлы:**
- Изменить: `src/Snapkit.App/Editor/EditorTool.cs`, `src/Snapkit.App/Editor/EditorToolbar.xaml(.cs)`,
  `src/Snapkit.App/Editor/EditorSurface.cs` (`HandleKey`, `SetTool`)
- Создать: `src/Snapkit.App/Editor/EditorKeys.cs`, `src/Snapkit.App/Editor/ShortcutsPopup.xaml(.cs)`
- Тесты: `tests/Snapkit.App.Tests/EditorKeysTests.cs`

**Интерфейсы:**
```csharp
public enum EditorTool { Select, Rectangle, RoundedRectangle, Ellipse, Arrow, CurvedArrow, WideArrow, Line,
                         Pencil, Marker, Text, Eraser, Blur, Crop, Pin }
public enum ToolGroup { None, Shape, Arrow, Pencil }
public static class EditorKeys
{
    public static ToolGroup GroupOf(EditorTool tool);
    public static IReadOnlyList<EditorTool> Variants(ToolGroup group);
    // V Выбор, R фигура (последний вариант группы), A стрелка (последний вариант), L линия, P карандаш,
    // M маркер, T текст, E ластик, B размыть, X обрезать, C комментарий; иначе null
    public static EditorTool? ToolFor(Key key, IReadOnlyDictionary<ToolGroup, EditorTool> lastVariant);
    public static IReadOnlyList<(string Keys, string Action)> Shortcuts { get; }   // для подсказки «?»
}
```
- Панель слева направо (раздел 6.2): Выбор · **Фигура ▾** (Прямоугольник, Скруглённый
  прямоугольник, Овал) · **Стрелка ▾** (Стрелка, Изогнутая стрелка, Широкая стрелка) · Линия ·
  **Карандаш ▾** (Карандаш, Маркер) · Текст · Ластик · Размыть · Обрезать · Комментарий · «?» ·
  разделитель · Цвет обводки · Заливка · Толщина ▾ · разделитель · Отменить · Повторить ·
  Копировать · «Готово».
- Кнопка группы: основная часть выбирает последний вариант группы и показывает его значок,
  узкая часть «▾» открывает меню вариантов с названиями. Последние варианты запоминаются
  между снимками (как инструмент).
- «?» и F1 открывают `ShortcutsPopup` — список `EditorKeys.Shortcuts`; Esc или щелчок вне — закрыть.
- Подсказки кнопок — с клавишей: «Ластик (E)».
- Пока инструменты задач 9–11 не подключены, кнопки уже есть и переключают инструмент.

- [ ] **Шаг 1: Тесты:** `ToolFor_R_ReturnsLastShapeVariant`, `ToolFor_A_ReturnsLastArrowVariant`,
  `ToolFor_UnknownKey_IsNull`, `GroupOf_Ellipse_IsShape`, `Variants_Pencil_ArePencilAndMarker`,
  `Shortcuts_CoverEveryToolKey`.
- [ ] **Шаг 2–4:** красные → реализация → зелёные.
- [ ] **Шаг 5: Ручная проверка:** панель целиком на экране у снимка во весь экран
  (`EditorLayoutTests` уже держат её вне панели комментариев); меню групп открываются;
  клавиши переключают инструменты; «?» показывает подсказку.
- [ ] **Шаг 6: Коммит** — `feat(editor): добавить панель инструментов этапа 2, клавиши и подсказку`.

---

### Задача 9: Рисование новых фигур, ластик, маркеры размера и перемещение кружка

**Файлы:**
- Изменить: `src/Snapkit.App/Editor/EditorSurface.cs`, `src/Snapkit.App/Editor/EditorCanvas.cs`

**Интерфейсы:**
- Использует: `AnnotationGeometry` (`IsBigEnough`, `DefaultBend`), `AnnotationHandles`,
  `StrokeSimplifier`, `AnnotationPainter`.
- Поведение:
  - Рамки, овал, линия, стрелки, размытие — перетаскиванием. Изогнутая стрелка создаётся
    с изгибом `DefaultBend`. Карандаш и маркер собирают точки при движении; при отпускании —
    `Simplify(точки, 2)`.
  - Малые фигуры не создаются (`IsBigEnough(фигура, 4)`). После каждой новой фигуры, кроме
    текста, открывается поле заметки, как в этапе 1.
  - Новая фигура получает текущие цвет, толщину, заливку и стиль линии.
  - **Ластик:** щелчок по фигуре удаляет её вместе с заметкой (`TopmostHit`); курсор — `Cursors.No`
    над пустым местом, `Cursors.Hand` над фигурой.
  - **Выбор:** у выделенной фигуры видны маркеры `AnnotationHandles.Of`. Тянуть маркер —
    `Move`, тянуть кружок заметки — `WithPinAt`, тянуть саму фигуру — сдвиг, как в этапе 1.
    Каждое законченное перетаскивание — один шаг истории.
  - `EditorCanvas` рисует маркеры выделенной фигуры (размеры — из «Общих ограничений»).

- [ ] **Шаг 1: Реализация.**
- [ ] **Шаг 2: Собрать** `build.ps1` → тесты зелёные, программа запущена.
- [ ] **Шаг 3: Ручная проверка:** каждый инструмент рисует; Ctrl+Z / Ctrl+Y отменяют и возвращают
  каждое действие; ластик удаляет; рамка и овал меняют размер за маркеры; изгиб стрелки тянется;
  кружок A1 сдвигается и остаётся сдвинутым после «Готово» и повторного открытия.
- [ ] **Шаг 4: Коммит** — `feat(editor): рисовать новые фигуры, стирать, менять размер и двигать кружок`.

---

### Задача 10: Цвета, заливка и стиль линии

**Файлы:**
- Изменить: `src/Snapkit.Core/Settings/AppSettings.cs`, `src/Snapkit.App/Editor/EditorToolbar.xaml(.cs)`,
  `src/Snapkit.App/Editor/EditorSurface.cs`, `src/Snapkit.App/Capturing/CaptureController.cs`, `src/Snapkit.App/App.xaml.cs`
- Создать: `src/Snapkit.Core/Editing/HexColor.cs`, `src/Snapkit.App/Editor/ColorPickerPopup.xaml(.cs)`
- Тесты: `tests/Snapkit.Core.Tests/HexColorTests.cs`, `tests/Snapkit.Core.Tests/SettingsStoreTests.cs`

**Интерфейсы:**
```csharp
// AppSettings: новый параметр в конце
string CustomColors = ""            // «#112233,#445566» — свои цвета, новые первыми, не больше 8

public static class HexColor
{
    public static bool TryNormalize(string input, out string hex);    // "e53935", "#E53935", "#abc" → "#E53935" / "#AABBCC"
    public static string FromHsv(double hue, double saturation, double value);   // hue 0–360, s и v 0–1
    public static (double Hue, double Saturation, double Value) ToHsv(string hex);
    public static string AddRecent(string list, string hex, int max = 8);        // в начало, без повторов, не длиннее max
    public static IReadOnlyList<string> Parse(string list);
}
// CaptureController: свои цвета из настроек и их сохранение
public Func<string>? CustomColors { get; set; }
public Action<string>? SaveCustomColors { get; set; }
```
- **Цвет обводки** — всплывающее окно `ColorPickerPopup`: 6 цветов палитры, свои цвета,
  поле HEX, ползунки «Оттенок» и «Яркость», кнопка «Пипетка». Пипетка: курсор-крест,
  щелчок по снимку берёт `PixelSampler.HexAt`, Esc отменяет. Новый цвет из HEX, ползунков
  или пипетки попадает в свои цвета (`AddRecent`) и сохраняется в настройки.
- **Заливка** — меню «Нет / Сплошная / Полупрозрачная / Размытием» и «Цвет заливки…»
  (тот же выбор цвета; по умолчанию — цвет обводки). Кнопка показана, только когда
  инструмент её поддерживает: рамки и овал (и выделенная такая фигура).
- **Толщина ▾** — 2, 4, 6, 8 и стиль: «Сплошная / Пунктир / Точки».
- Новые цвет, заливка, толщина и стиль применяются и к выделенной фигуре (одним шагом
  истории) и запоминаются между снимками.

- [ ] **Шаг 1: Тесты:**
```csharp
[Theory] void TryNormalize_AcceptsCommonForms(string input, string expected)   // "e53935", "#E53935", "#abc", " #43a047 "
[Theory] void TryNormalize_RejectsGarbage(string input)                         // "", "#12", "zzzzzz", "#1234567"
[Fact] void Hsv_RoundTrip_KeepsColor()                                           // для 6 цветов палитры, отклонение ≤ 1 на канал
[Fact] void AddRecent_PutsFirst_NoDuplicates_LimitsToMax()
[Fact] void Load_OldSettingsWithoutCustomColors_IsEmpty()                        // в SettingsStoreTests
```
- [ ] **Шаг 2–4:** красные → реализация → зелёные, весь набор зелёный.
- [ ] **Шаг 5: Ручная проверка:** свой цвет через HEX сохраняется после перезапуска Snapkit;
  пипетка берёт точный цвет пикселя; заливки и стили видны в редакторе и во вставке.
- [ ] **Шаг 6: Коммит** — `feat(editor): добавить выбор цвета, свои цвета, пипетку, заливку и стиль линии`.

---

### Задача 11: Обрезка снимка

**Файлы:**
- Изменить: `src/Snapkit.App/Editor/EditorSurface.cs`, `src/Snapkit.App/Capturing/CaptureController.cs`,
  `src/Snapkit.App/Session/SessionService.cs`
- Тесты: `tests/Snapkit.App.Tests/SessionServiceTests.cs`

**Интерфейсы:**
```csharp
public event EventHandler<RectI>? EditorSurface.CropRequested;   // новая область в координатах монитора
public void SessionService.UpdateCaptureImage(Capture updated, BitmapSource source);
// новый исходник в памяти и на диске ({Id}.png), модель обновлена, событие Updated
```
- **Инструмент «Обрезать»:** перетаскивание внутри снимка выделяет новую область (пунктирная
  рамка, вне её — затемнение). Отпустили — `CropRequested(область ∩ текущая область)`.
  Область меньше 4×4 не принимается.
- **`CaptureController`:** на `CropRequested` — `window.PresetRegion(r)` и
  `editor.SetRegion(window.Region!.Value)`. Работает и у нового снимка, и у открытого из ленты.
- **`EditorSurface.CurrentAnnotations`** применяет `KeepPinsInside` к размеру текущей области —
  кружки остаются внутри и после обрезки, и после уменьшения рамки маркерами.
- **Снимок из ленты, «Готово»:** если `editor.Region != local` — `crop` = область относительно
  `local`; новый исходник — вырезка `crop` из старого; `PixelWidth`, `PixelHeight` — размер
  `crop`; `ScreenBounds` сдвигается на `crop.X`, `crop.Y`; разметка — `result.Annotations`
  (уже в координатах новой области); `session.UpdateCaptureImage(...)`.
- **Решение плана:** обрезка не входит в историю правок — Ctrl+Z её не отменяет. Отменить
  можно, закрыв редактор по Esc без сохранения.

- [ ] **Шаг 1: Тесты:**
```csharp
[Fact] async Task UpdateCaptureImage_ReplacesSourceOnDiskAndInMemory()
// AddCapture 100×80 → UpdateCaptureImage(capture с 40×30, исходник 40×30) → GetSource 40×30;
// после FlushAsync новый SessionService(тот же store).RestoreFromDisk() → 1 снимок 40×30 и исходник 40×30
[Fact] void UpdateCaptureImage_RaisesUpdated()
```
- [ ] **Шаг 2–4:** красные → реализация → зелёные, весь набор зелёный.
- [ ] **Шаг 5: Ручная проверка:** обрезать новый снимок и снимок из ленты; кружок с края
  уезжает внутрь; после «Готово» карточка показывает обрезанный снимок; повторное открытие
  ставит его на место обрезанной области; вставка в чат — обрезанная картинка.
- [ ] **Шаг 6: Коммит** — `feat(editor): обрезать снимок, в том числе открытый из ленты`.

---

### Задача 12: README и приёмка этапа 2

**Файлы:**
- Изменить: `README.md` (раздел «Как пользоваться» — инструменты и клавиши)
- Создать: `docs/superpowers/plans/2026-10-26-snapkit-stage-2-acceptance.md`

- [ ] **Шаг 1:** README — короткий список инструментов с клавишами (из `EditorKeys.Shortcuts`).
- [ ] **Шаг 2:** список приёмки — проверки:
  1. Каждый инструмент рисует, у каждой фигуры появляется поле заметки, номера A1, A2… верные.
  2. Размытие: телефон или пароль под размытием не читается во вставке в чат — у нового снимка
     и у открытого из ленты.
  3. Обрезка нового снимка и снимка из ленты, кружки внутри, место при повторном открытии.
  4. Маркеры размера, изгиб стрелки, сдвиг кружка; Ctrl+Z / Ctrl+Y на каждом действии.
  5. Свой цвет через HEX сохраняется после перезапуска; пипетка; заливки; пунктир и точки.
  6. Снимок, сделанный до обновления (этап 1), открывается и правится.
  7. «?» показывает все клавиши.
  8. Большая область размытия и длинный штрих карандаша не тормозят.
- [ ] **Шаг 3:** полный прогон `dotnet test Snapkit.slnx` (с буфером и окнами) — зелёный.
- [ ] **Шаг 4: Коммит** — `docs: добавить инструменты редактора в README и список приёмки этапа 2`.
- [ ] **Шаг 5:** приёмка с пользователем; слияние в `main` и отправка на GitHub — только с его согласия.
