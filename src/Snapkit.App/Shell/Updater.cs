using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Snapkit.App.Notifications;
using Snapkit.Core.Settings;
using Snapkit.Core.Updates;

namespace Snapkit.App.Shell;

/// <summary>
/// Автообновление с GitHub: найти релиз новее, предложить, скачать, сверить хеш и запустить новую версию —
/// дальше её самоустановка закроет эту копию и встанет на место.
/// </summary>
/// <param name="settings">Нынешние настройки.</param>
/// <param name="saveSettings">Сохранить настройки (отложить предложенную версию).</param>
public sealed class Updater(Func<AppSettings> settings, Action<AppSettings> saveSettings)
{
    private const string LatestUrl = "https://api.github.com/repos/Ozimnadius/snapkit/releases/latest";

    /// <summary>Страница последнего релиза — для копий, которые сами не обновляются.</summary>
    public const string ReleasePageUrl = "https://github.com/Ozimnadius/snapkit/releases/latest";

    // Статические поля задаются сверху вниз: версия нужна HTTP-клиенту (заголовок User-Agent), поэтому она выше
    /// <summary>Версия этой копии: «0.4.0».</summary>
    public static Version CurrentVersion { get; } =
        typeof(Updater).Assembly.GetName().Version is { } v ? new Version(v.Major, v.Minor, v.Build) : new Version(0, 0, 0);

    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);
    private static readonly HttpClient Http = CreateClient();
    private bool _installing;

    /// <summary>Проверить GitHub и, если есть что, предложить обновление уведомлением. Ошибки сети — только в журнал.</summary>
    public async Task CheckAsync()
    {
        if (await FetchLatestAsync() is not { } latest) return;
        var current = settings();
        if (!UpdatePolicy.ShouldOffer(CurrentVersion, latest, current.SnoozedUpdateVersion, current.SnoozedUpdateUntil, DateTimeOffset.Now))
            return;
        // Закрыли уведомление, не нажав «Обновить», — до следующих суток про эту версию не напоминаем
        saveSettings(current with { SnoozedUpdateVersion = latest.Version.ToString(), SnoozedUpdateUntil = DateTimeOffset.Now.AddDays(1) });
        Log.Info($"Вышла версия {latest.Version}");
        Toasts.Offer($"Вышла версия {latest.Version} — обновить?", "Обновить", () => _ = InstallAsync(latest));
    }

    /// <summary>Последний релиз с GitHub или null: нет сети, GitHub недоступен, в релизе нет Snapkit.exe с хешем.</summary>
    public static async Task<ReleaseInfo?> FetchLatestAsync()
    {
        string json;
        try
        {
            using var timeout = new CancellationTokenSource(CheckTimeout);
            json = await Http.GetStringAsync(LatestUrl, timeout.Token);
        }
        catch (Exception ex)
        {
            // Нет сети, GitHub недоступен, ответ оборвался — работаем дальше как есть
            Log.Info($"Проверка обновления не удалась: {ex.GetType().Name}");
            return null;
        }
        var latest = ReleaseInfo.Parse(json);
        if (latest is null) Log.Info("Проверка обновления: в последнем релизе нет Snapkit.exe с хешем");
        return latest;
    }

    /// <summary>
    /// Скачать, сверить хеш и запустить новую версию. false — не вышло (причина в журнале).
    /// notify: false — без уведомлений, когда ответ показывает окно настроек.
    /// </summary>
    public async Task<bool> InstallAsync(ReleaseInfo latest, bool notify = true)
    {
        if (_installing) return true;
        _installing = true;
        try
        {
            if (notify) Toasts.Show("Скачиваю обновление…");
            byte[] file;
            using (var timeout = new CancellationTokenSource(DownloadTimeout))
                file = await Http.GetByteArrayAsync(latest.ExeUrl, timeout.Token);
            // Файл, хеш которого не совпал, на диск не попадает и не запускается
            if (!UpdatePolicy.HashMatches(file, latest.Sha256))
            {
                Log.Info($"Обновление {latest.Version}: хеш не совпал");
                if (notify) Toasts.Error("Обновление не прошло проверку и не установлено");
                return false;
            }
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Snapkit", "update");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"Snapkit-{latest.Version}.exe");
            await File.WriteAllBytesAsync(path, file);
            Log.Info($"Запускаю обновление {latest.Version}");
            Process.Start(new ProcessStartInfo(path) { WorkingDirectory = folder, UseShellExecute = false });
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("обновление", ex);
            if (notify) Toasts.Error("Не удалось обновить Snapkit. Подробности — в журнале");
            return false;
        }
        finally
        {
            _installing = false;
        }
    }

    private static HttpClient CreateClient()
    {
        // Сроки задаются на каждый запрос: проверка короткая, скачивание exe — долгое
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Snapkit/{CurrentVersion}");
        return client;
    }
}
