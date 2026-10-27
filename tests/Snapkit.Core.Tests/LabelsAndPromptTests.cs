using Snapkit.Core;
using Snapkit.Core.Model;

namespace Snapkit.Core.Tests;

public class LabelsAndPromptTests
{
    private static Annotation Rect(string? note) => new(
        Guid.NewGuid(), AnnotationKind.Rectangle, [new PointD(0, 0), new PointD(10, 10)],
        "#E53935", 4, null, note);

    private static Capture Cap(params Annotation[] annotations) => new(
        Guid.NewGuid(), 100, 50, new RectI(0, 0, 100, 50), [.. annotations], "",
        DateTimeOffset.UnixEpoch);

    [Fact]
    public void Letters_ArePositionsInStrip()
    {
        // Буква — место на ленте сверху вниз
        var first = Cap(); var second = Cap(); var third = Cap();
        var letters = CaptureLetters.Assign([first, second, third]);
        Assert.Equal(('A', 'B', 'C'), (letters[first.Id], letters[second.Id], letters[third.Id]));
    }

    [Fact]
    public void NoteNumbers_CountOnlyAnnotationsWithNotes()
    {
        var a = Rect("x"); var b = Rect(null); var c = Rect("y");
        var numbered = NoteNumbers.Number(Cap(a, b, c));
        Assert.Equal(new[] { (a, 1), (c, 2) }, numbered);
    }

    [Fact]
    public void NoteLabel_CombinesLetterAndNumber()
        => Assert.Equal("A1", NoteNumbers.Label('A', 1));

    [Fact]
    public void Prompt_FormatsCapturesAndNotes()
    {
        var capA = Cap(Rect("У Макса значок VK в админке"), Rect("Поле Telegram шире остальных"))
            with { SnapshotNote = "страница настроек соцсетей" };
        var capB = Cap(Rect("Кнопка «Сохранить» не активна"));
        var text = PromptBuilder.Build([new('A', capA), new('B', capB)]);
        Assert.Equal(
            "Снимок A\nA1 — У Макса значок VK в админке\nA2 — Поле Telegram шире остальных\n" +
            "Комментарий к снимку: страница настроек соцсетей\n\n" +
            "Снимок B\nB1 — Кнопка «Сохранить» не активна", text);
    }

    [Fact]
    public void Prompt_SkipsCapturesWithoutNotes_KeepsLetters()
    {
        var text = PromptBuilder.Build([new('A', Cap(Rect(null))), new('B', Cap(Rect("есть")))]);
        Assert.Equal("Снимок B\nB1 — есть", text);
    }

    [Fact]
    public void Prompt_EmptyWhenNoNotes()
        => Assert.Equal("", PromptBuilder.Build([new('A', Cap(Rect(null)))]));

    [Fact]
    public void Prompt_IndentsMultilineNote()
    {
        var text = PromptBuilder.Build([new('A', Cap(Rect("строка 1\nстрока 2")))]);
        Assert.Equal("Снимок A\nA1 — строка 1\n     строка 2", text); // отступ = длина "A1 — "
    }

    [Fact]
    public void Prompt_IndentsMultilineSnapshotNoteByTwoSpaces()
    {
        var cap = Cap() with { SnapshotNote = "первая\nвторая" };
        var text = PromptBuilder.Build([new('A', cap)]);
        Assert.Equal("Снимок A\nКомментарий к снимку: первая\n  вторая", text);
    }

    [Fact]
    public void NoteCount_IncludesSnapshotNote()
        => Assert.Equal(2, (Cap(Rect("x"), Rect(null)) with { SnapshotNote = "общая" }).NoteCount);

    [Fact]
    public void NoteCount_IgnoresBlankSnapshotNote()
        => Assert.Equal(1, (Cap(Rect("x")) with { SnapshotNote = "   " }).NoteCount);
}
