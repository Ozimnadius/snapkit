using System.Text.Json;

namespace Snapkit.Core.Settings;

/// <summary>Файл настроек settings.json. Повреждённый файл откладывается в settings.bad.json, берутся настройки по умолчанию.</summary>
public sealed class SettingsStore(string filePath)
{
    /// <summary>WasReset — файл был, но не читался; пользователю стоит об этом сказать.</summary>
    public (AppSettings Settings, bool WasReset) Load()
    {
        if (!File.Exists(filePath)) return (new AppSettings(), false);
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllBytes(filePath), SnapkitJson.Options);
            if (settings is not null) return (settings, false);
        }
        catch (JsonException)
        {
        }
        // Повреждённый файл откладываем в сторону: при следующем запуске будут обычные настройки без сообщения
        File.Move(filePath, Path.Combine(Path.GetDirectoryName(filePath)!, "settings.bad.json"), overwrite: true);
        return (new AppSettings(), true);
    }

    public void Save(AppSettings settings)
        => AtomicFile.Write(filePath, JsonSerializer.SerializeToUtf8Bytes(settings, SnapkitJson.Options));
}
