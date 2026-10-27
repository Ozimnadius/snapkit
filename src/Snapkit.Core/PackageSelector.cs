using Snapkit.Core.Model;

namespace Snapkit.Core;

/// <summary>Снимок пакета вместе с его буквой.</summary>
public readonly record struct LetteredCapture(char Letter, Capture Capture);

/// <summary>Пакет — неотправленные снимки ленты в её порядке, с буквами их мест на ленте.</summary>
public static class PackageSelector
{
    public static IReadOnlyList<LetteredCapture> Select(IReadOnlyList<Capture> strip)
    {
        var letters = CaptureLetters.Assign(strip);
        return strip
            .Where(c => !c.IsSent)
            .Select(c => new LetteredCapture(letters[c.Id], c))
            .ToList();
    }

    /// <summary>
    /// Вся лента для «Сохранить всё…»: все снимки, и отправленные тоже, с буквами их мест;
    /// отметки не меняются.
    /// </summary>
    public static IReadOnlyList<LetteredCapture> All(IReadOnlyList<Capture> strip)
    {
        var letters = CaptureLetters.Assign(strip);
        return strip.Select(c => new LetteredCapture(letters[c.Id], c)).ToList();
    }
}
