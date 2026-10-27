using Snapkit.Core.Settings;

namespace Snapkit.Core;

/// <summary>Имена и текст для «Сохранить снимок…» и «Сохранить всё…».</summary>
public static class PackageExport
{
    /// <summary>Имя файла или папки по времени: «2026-10-26_15-40-12» — сортируется по порядку.</summary>
    public static string TimestampName(DateTime time) => time.ToString("yyyy-MM-dd_HH-mm-ss");

    public static string Extension(SaveFormat format) => format == SaveFormat.Jpeg ? ".jpg" : ".png";

    public static string ImageFileName(char letter, SaveFormat format = SaveFormat.Png) => letter + Extension(format);

    /// <summary>
    /// prompt.md: ссылки на картинки по одной в строке, затем тот же текст, что уходит в чат,
    /// — ИИ в терминале прочитает картинки по ссылкам. Без заметок — только ссылки.
    /// </summary>
    public static string BuildPromptMarkdown(IReadOnlyList<LetteredCapture> package, SaveFormat format = SaveFormat.Png)
    {
        var images = string.Join("\n", package.Select(p => $"![Снимок {p.Letter}]({ImageFileName(p.Letter, format)})"));
        var prompt = PromptBuilder.Build(package);
        return prompt.Length == 0 ? images : images + "\n\n" + prompt;
    }

    /// <summary>name, если свободно, иначе «name (2)», «name (3)»…</summary>
    public static string UniqueName(string name, Func<string, bool> isTaken)
    {
        if (!isTaken(name)) return name;
        for (var number = 2; ; number++)
        {
            var candidate = $"{name} ({number})";
            if (!isTaken(candidate)) return candidate;
        }
    }
}
