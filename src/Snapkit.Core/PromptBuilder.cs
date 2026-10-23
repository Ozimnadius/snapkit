using System.Text;

namespace Snapkit.Core;

/// <summary>
/// Текст, который уходит в чат вместе с картинками:
/// <code>
/// Снимок A
/// A1 — комментарий
/// Комментарий к снимку: общая заметка
/// </code>
/// Снимки без комментариев пропускаются, но буквы остальных не меняются.
/// Строки разделяются "\n"; на "\r\n" их заменяет буфер обмена.
/// </summary>
public static class PromptBuilder
{
    private const string SnapshotNotePrefix = "Комментарий к снимку: ";
    private const string SnapshotNoteIndent = "  ";

    public static string Build(IReadOnlyList<LetteredCapture> package)
    {
        var blocks = new List<string>();
        foreach (var (letter, capture) in package)
        {
            var notes = NoteNumbers.Number(capture);
            if (notes.Count == 0 && !capture.HasSnapshotNote) continue;

            var block = new StringBuilder($"Снимок {letter}");
            foreach (var (annotation, number) in notes)
            {
                var label = NoteNumbers.Label(letter, number);
                block.Append('\n').Append(label).Append(" — ")
                     .Append(Indent(annotation.Note!.Trim(), new string(' ', label.Length + 3)));
            }
            if (capture.HasSnapshotNote)
            {
                block.Append('\n').Append(SnapshotNotePrefix)
                     .Append(Indent(capture.SnapshotNote.Trim(), SnapshotNoteIndent));
            }
            blocks.Add(block.ToString());
        }
        return string.Join("\n\n", blocks);
    }

    // Продолжения многострочного текста сдвигаются, чтобы стоять под началом первой строки.
    private static string Indent(string text, string indent)
        => text.ReplaceLineEndings("\n").Replace("\n", "\n" + indent);
}
