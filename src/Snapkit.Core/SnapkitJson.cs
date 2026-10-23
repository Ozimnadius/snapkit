using System.Text.Json;
using System.Text.Json.Serialization;

namespace Snapkit.Core;

/// <summary>Общие настройки JSON для файлов сессии и настроек.</summary>
public static class SnapkitJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        // Русский текст в файле остаётся читаемым, а не превращается в \uXXXX
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}

/// <summary>Надёжная запись файла: сначала во временный, затем замена одним действием.</summary>
internal static class AtomicFile
{
    public static void Write(string path, ReadOnlySpan<byte> content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(content);
            stream.Flush(flushToDisk: true);
        }
        File.Move(tmp, path, overwrite: true);
    }
}
