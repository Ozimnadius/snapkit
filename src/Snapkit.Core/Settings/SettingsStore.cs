using System.Text.Json;

namespace Snapkit.Core.Settings;

/// <summary>Файл настроек settings.json. Повреждённый файл заменяется настройками по умолчанию.</summary>
public sealed class SettingsStore(string filePath)
{
    /// <summary>WasReset — файл был, но не читался; пользователю стоит об этом сказать.</summary>
    public (AppSettings Settings, bool WasReset) Load()
    {
        if (!File.Exists(filePath)) return (new AppSettings(), false);
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllBytes(filePath), SnapkitJson.Options);
            return settings is null ? (new AppSettings(), true) : (settings, false);
        }
        catch (JsonException)
        {
            return (new AppSettings(), true);
        }
    }

    public void Save(AppSettings settings)
        => AtomicFile.Write(filePath, JsonSerializer.SerializeToUtf8Bytes(settings, SnapkitJson.Options));
}
