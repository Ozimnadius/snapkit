using Snapkit.Core;
using Snapkit.Core.Model;

namespace Snapkit.Core.Tests;

public class LabelsAndPromptTests
{
    private static Annotation Rect(string? note) => new(
        Guid.NewGuid(), AnnotationKind.Rectangle, [new PointD(0, 0), new PointD(10, 10)],
        "#E53935", 4, null, note);

    private static Capture Cap(bool sent, params Annotation[] annotations) => new(
        Guid.NewGuid(), 100, 50, new RectI(0, 0, 100, 50), [.. annotations], "", sent,
        DateTimeOffset.UnixEpoch);

    [Fact]
    public void Letters_SkipSentCaptures()
    {
        var sent = Cap(true); var first = Cap(false); var second = Cap(false);
        var letters = CaptureLetters.Assign([sent, first, second]);
        Assert.False(letters.ContainsKey(sent.Id));
        Assert.Equal('A', letters[first.Id]);
        Assert.Equal('B', letters[second.Id]);
    }

    [Fact]
    public void NoteNumbers_CountOnlyAnnotationsWithNotes()
    {
        var a = Rect("x"); var b = Rect(null); var c = Rect("y");
        var numbered = NoteNumbers.Number(Cap(false, a, b, c));
        Assert.Equal(new[] { (a, 1), (c, 2) }, numbered);
    }

    [Fact]
    public void NoteLabel_CombinesLetterAndNumber()
        => Assert.Equal("A1", NoteNumbers.Label('A', 1));

    [Fact]
    public void Prompt_FormatsCapturesAndNotes()
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

    [Fact]
    public void Prompt_SkipsCapturesWithoutNotes_KeepsLetters()
    {
        var text = PromptBuilder.Build([new('A', Cap(false, Rect(null))), new('B', Cap(false, Rect("есть")))]);
        Assert.Equal("Снимок B\nB1 — есть", text);
    }

    [Fact]
    public void Prompt_EmptyWhenNoNotes()
        => Assert.Equal("", PromptBuilder.Build([new('A', Cap(false, Rect(null)))]));

    [Fact]
    public void Prompt_IndentsMultilineNote()
    {
        var text = PromptBuilder.Build([new('A', Cap(false, Rect("строка 1\nстрока 2")))]);
        Assert.Equal("Снимок A\nA1 — строка 1\n     строка 2", text); // отступ = длина "A1 — "
    }

    [Fact]
    public void Prompt_IndentsMultilineSnapshotNoteByTwoSpaces()
    {
        var cap = Cap(false) with { SnapshotNote = "первая\nвторая" };
        var text = PromptBuilder.Build([new('A', cap)]);
        Assert.Equal("Снимок A\nКомментарий к снимку: первая\n  вторая", text);
    }

    [Fact]
    public void Package_IsUnsentInStripOrderWithLetters()
    {
        var s = Cap(true); var u1 = Cap(false); var u2 = Cap(false);
        Assert.Equal(new LetteredCapture[] { new('A', u1), new('B', u2) }, PackageSelector.Select([u1, s, u2]));
    }

    [Fact]
    public void NoteCount_IncludesSnapshotNote()
        => Assert.Equal(2, (Cap(false, Rect("x"), Rect(null)) with { SnapshotNote = "общая" }).NoteCount);

    [Fact]
    public void NoteCount_IgnoresBlankSnapshotNote()
        => Assert.Equal(1, (Cap(false, Rect("x")) with { SnapshotNote = "   " }).NoteCount);
}
