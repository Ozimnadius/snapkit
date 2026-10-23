using Snapkit.Core.Model;

namespace Snapkit.Core;

/// <summary>
/// Буквы снимков: A, B, C… по порядку среди неотправленных снимков ленты —
/// в том порядке, в котором они уйдут в чат. У отправленных букв нет.
/// </summary>
public static class CaptureLetters
{
    public static IReadOnlyDictionary<Guid, char> Assign(IEnumerable<Capture> stripOrder)
    {
        var letters = new Dictionary<Guid, char>();
        var next = 'A';
        foreach (var capture in stripOrder)
        {
            if (capture.IsSent) continue;
            letters[capture.Id] = next++;
        }
        return letters;
    }
}

/// <summary>Номера заметок на снимке: 1, 2, 3… по порядку фигур, у которых есть комментарий.</summary>
public static class NoteNumbers
{
    public static IReadOnlyList<(Annotation Annotation, int Number)> Number(Capture capture)
    {
        var result = new List<(Annotation, int)>();
        foreach (var annotation in capture.Annotations)
        {
            if (annotation.HasNote) result.Add((annotation, result.Count + 1));
        }
        return result;
    }

    public static string Label(char letter, int number) => $"{letter}{number}";
}
