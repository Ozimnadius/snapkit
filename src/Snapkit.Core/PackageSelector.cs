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
}
