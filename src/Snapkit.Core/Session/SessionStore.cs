using System.Text.Json;
using Snapkit.Core.Model;

namespace Snapkit.Core.Session;

/// <summary>
/// Сессия на диске: session.json со списком снимков и разметкой плюс исходники {Id}.png.
/// Нужна, чтобы лента пережила сбой программы или перезагрузку компьютера.
/// </summary>
public sealed class SessionStore(string directory)
{
    private string JsonPath => Path.Combine(directory, "session.json");
    private string BadJsonPath => Path.Combine(directory, "session.bad.json");

    /// <summary>
    /// Нет файла — пустая лента. Повреждённый файл откладывается в session.bad.json.
    /// Снимки, у которых нет исходной картинки, выбрасываются.
    /// </summary>
    public IReadOnlyList<Capture> Load()
    {
        if (!File.Exists(JsonPath)) return [];
        List<Capture>? captures;
        try
        {
            captures = JsonSerializer.Deserialize<List<Capture>>(File.ReadAllBytes(JsonPath), SnapkitJson.Options);
        }
        catch (JsonException)
        {
            File.Move(JsonPath, BadJsonPath, overwrite: true);
            return [];
        }
        return (captures ?? []).Where(c => File.Exists(SourceImagePath(c.Id))).ToList();
    }

    public void Save(IReadOnlyList<Capture> captures)
        => AtomicFile.Write(JsonPath, JsonSerializer.SerializeToUtf8Bytes(captures, SnapkitJson.Options));

    public string SourceImagePath(Guid id) => Path.Combine(directory, id + ".png");

    public void WriteSourceImage(Guid id, byte[] png) => AtomicFile.Write(SourceImagePath(id), png);

    /// <summary>Удаляет исходники снимков, которых больше нет в ленте.</summary>
    public void RemoveOrphanImages(IReadOnlyList<Capture> captures)
    {
        if (!Directory.Exists(directory)) return;
        var keep = captures.Select(c => SourceImagePath(c.Id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var png in Directory.GetFiles(directory, "*.png"))
        {
            if (!keep.Contains(png)) File.Delete(png);
        }
    }

    public void Clear()
    {
        if (!Directory.Exists(directory)) return;
        File.Delete(JsonPath);
        foreach (var png in Directory.GetFiles(directory, "*.png")) File.Delete(png);
    }
}
