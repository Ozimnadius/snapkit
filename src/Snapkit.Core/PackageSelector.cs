using Snapkit.Core.Model;

namespace Snapkit.Core;

/// <summary>Снимок пакета вместе с его буквой.</summary>
public readonly record struct LetteredCapture(char Letter, Capture Capture);

/// <summary>Пакет — вся лента в её порядке, с буквами мест: «Копировать всё», «Сохранить всё…».</summary>
public static class PackageSelector
{
    public static IReadOnlyList<LetteredCapture> All(IReadOnlyList<Capture> strip)
    {
        var letters = CaptureLetters.Assign(strip);
        return strip.Select(c => new LetteredCapture(letters[c.Id], c)).ToList();
    }
}
