using Snapkit.Core.Model;

namespace Snapkit.Core;

/// <summary>Снимок пакета вместе с его буквой.</summary>
public readonly record struct LetteredCapture(char Letter, Capture Capture);

/// <summary>Пакет — неотправленные снимки ленты в её порядке, с буквами A, B, C…</summary>
public static class PackageSelector
{
    public static IReadOnlyList<LetteredCapture> Select(IReadOnlyList<Capture> strip)
    {
        var letters = CaptureLetters.Assign(strip);
        return strip
            .Where(c => letters.ContainsKey(c.Id))
            .Select(c => new LetteredCapture(letters[c.Id], c))
            .ToList();
    }

    /// <summary>
    /// Снимки для «Сохранить пакет…» (правило 6.5): неотправленные, а если их нет — вся лента;
    /// буквы A, B… по порядку; отметки не меняются.
    /// </summary>
    public static IReadOnlyList<LetteredCapture> ForExport(IReadOnlyList<Capture> strip)
    {
        if (strip.Any(c => !c.IsSent)) return Select(strip);
        var letter = 'A';
        return strip.Select(c => new LetteredCapture(letter++, c)).ToList();
    }
}
