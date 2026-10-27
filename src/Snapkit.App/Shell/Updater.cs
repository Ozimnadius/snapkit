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
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);
    private static readonly HttpClient Http = CreateClient();
    private bool _installing;

    /// <summary>Версия этой копии: «0.3.1».</summary>
    public static Version CurrentVersion { get; } =
        typeof(Updater).Assembly.GetName().Version is { } v ? new Version(v.Major, v.Minor, v.Build) : new Version(0, 0, 0);

    /// <summary>Проверить GitHub и, если есть что, предложить обновление. Ошибки сети — только в журнал.</summary>
    public async Task CheckAsync()
    {
        string json;
        try
        {
            using var timeout = new CancellationTokenSource(CheckTimeout);
            json = await Http.GetStringAsync(LatestUrl, timeout.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Log.Info($"Проверка обновления не удалась: {ex.GetType().Name}");
            return;
        }
        if (ReleaseInfo.Parse(json) is not { } latest)
        {
            Log.Info("Проверка обновления: в последнем релизе нет Snapkit.exe с хешем");
            return;
        }
        var current = settings();
        if (!UpdatePolicy.ShouldOffer(CurrentVersion, latest, current.SnoozedUpdateVersion, current.SnoozedUpdateUntil, DateTimeOffset.Now))
            return;
        // Закрыли уведомление, не нажав «Обновить», — до следующих суток про эту версию не напоминаем
        saveSettings(current with { SnoozedUpdateVersion = latest.Version.ToString(), SnoozedUpdateUntil = DateTimeOffset.Now.AddDays(1) });
        Log.Info($"Вышла версия {latest.Version}");
        Toasts.Offer($"Вышла версия {latest.Version} — обновить?", "Обновить", () => _ = InstallAsync(latest));
    }

    private async Task InstallAsync(ReleaseInfo latest)
    {
        if (_installing) return;
        _installing = true;
        try
        {
            Toasts.Show("Скачиваю обновление…");
            byte[] file;
            using (var timeout = new CancellationTokenSource(DownloadTimeout))
                file = await Http.GetByteArrayAsync(latest.ExeUrl, timeout.Token);
            // Файл, хеш которого не совпал, на диск не попадает и не запускается
            if (!UpdatePolicy.HashMatches(file, latest.Sha256))
            {
                Log.Info($"Обновление {latest.Version}: хеш не совпал");
                Toasts.Error("Обновление не прошло проверку и не установлено");
                return;
            }
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Snapkit", "update");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"Snapkit-{latest.Version}.exe");
            await File.WriteAllBytesAsync(path, file);
            Log.Info($"Запускаю обновление {latest.Version}");
            Process.Start(new ProcessStartInfo(path) { WorkingDirectory = folder, UseShellExecute = false });
        }
        catch (Exception ex)
        {
            Log.Error("обновление", ex);
            Toasts.Error("Не удалось обновить Snapkit. Подробности — в журнале");
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
