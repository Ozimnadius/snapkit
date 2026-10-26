using Snapkit.Core.Model;

namespace Snapkit.Core;

/// <summary>
/// Буквы снимков: A, B, C… — место снимка на ленте сверху вниз, у всех снимков, и отправленных тоже.
/// Выбор снимков (значок карточки) буквы остальных не сдвигает; они меняются только при перестановке
/// и удалении. В чат снимки уходят с теми же буквами, что на ленте.
/// </summary>
public static class CaptureLetters
{
    public static IReadOnlyDictionary<Guid, char> Assign(IEnumerable<Capture> stripOrder)
    {
        var letters = new Dictionary<Guid, char>();
        var next = 'A';
        foreach (var capture in stripOrder) letters[capture.Id] = next++;
        return letters;
    }

    /// <summary>Буква нового снимка: следующее место на ленте (полная лента вытеснит один снимок).</summary>
    public static char ForNew(int stripCount, int maxCaptures) => (char)('A' + Math.Min(stripCount, maxCaptures - 1));
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
