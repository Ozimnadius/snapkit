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
    // Запасной путь: GitHub API отвечает 60 раз в час на адрес, а за VPN этот лимит часто выбирают чужие запросы
    private const string ApiLatestUrl = "https://api.github.com/repos/Ozimnadius/snapkit/releases/latest";

    /// <summary>
    /// Страница последнего релиза: переадресует на …/releases/tag/vX.Y.Z (так узнаём версию без API)
    /// и открывается в браузере у копий, которые сами не обновляются.
    /// </summary>
    public const string ReleasePageUrl = "https://github.com/Ozimnadius/snapkit/releases/latest";

    // Статические поля задаются сверху вниз: версия нужна HTTP-клиентам (заголовок User-Agent), поэтому она выше
    /// <summary>Версия этой копии: «0.4.0».</summary>
    public static Version CurrentVersion { get; } =
        typeof(Updater).Assembly.GetName().Version is { } v ? new Version(v.Major, v.Minor, v.Build) : new Version(0, 0, 0);

    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);
    private static readonly HttpClient Http = CreateClient(followRedirects: true);
    private static readonly HttpClient NoRedirectHttp = CreateClient(followRedirects: false);
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

    /// <summary>
    /// Последний релиз с GitHub или null (нет сети, GitHub недоступен). Версия — со страницы релизов, хеш — из
    /// Snapkit.exe.sha256 рядом с exe; у релизов без этого файла (0.4.0) — из GitHub API. Не новее этой копии —
    /// хеш не нужен; новее, а хеша не достать — Sha256 = null: показать можно, поставить нельзя.
    /// </summary>
    public static async Task<ReleaseInfo?> FetchLatestAsync()
    {
        var version = await LatestVersionAsync();
        if (version is not null)
        {
            if (version <= CurrentVersion) return ReleaseInfo.FromPage(version, null);
            if (await HashFileAsync(version) is { } hash) return ReleaseInfo.FromPage(version, hash);
        }
        if (await FetchFromApiAsync() is { } fromApi) return fromApi;
        return version is null ? null : ReleaseInfo.FromPage(version, null);
    }

    // github.com/…/releases/latest переадресует на …/releases/tag/vX.Y.Z — лимит API на неё не действует
    private static async Task<Version?> LatestVersionAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(CheckTimeout);
            using var response = await NoRedirectHttp.GetAsync(ReleasePageUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var location = response.Headers.Location;
            if (location is { IsAbsoluteUri: false }) location = new Uri(new Uri(ReleasePageUrl), location);
            var version = ReleaseInfo.VersionFromTagUrl(location?.AbsoluteUri);
            if (version is null) Log.Info($"Проверка обновления: страница релизов ответила {(int)response.StatusCode} без версии");
            return version;
        }
        catch (Exception ex)
        {
            // Нет сети, GitHub недоступен, ответ оборвался — работаем дальше как есть
            Log.Info($"Проверка обновления не удалась: {ex.GetType().Name}");
            return null;
        }
    }

    // Snapkit.exe.sha256 кладёт в релиз его сборка; у релизов до 0.4.1 этого файла нет
    private static async Task<string?> HashFileAsync(Version version)
    {
        try
        {
            using var timeout = new CancellationTokenSource(CheckTimeout);
            using var response = await Http.GetAsync(ReleaseInfo.HashFileUrl(version), timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                Log.Info($"Проверка обновления: у версии {version} нет файла с хешем ({(int)response.StatusCode})");
                return null;
            }
            var hash = ReleaseInfo.ParseHashFile(await response.Content.ReadAsStringAsync(timeout.Token));
            if (hash is null) Log.Info($"Проверка обновления: файл с хешем версии {version} не разобран");
            return hash;
        }
        catch (Exception ex)
        {
            Log.Info($"Проверка обновления: файл с хешем не получен: {ex.GetType().Name}");
            return null;
        }
    }

    private static async Task<ReleaseInfo?> FetchFromApiAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(CheckTimeout);
            using var response = await Http.GetAsync(ApiLatestUrl, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                // 403 — исчерпан лимит запросов к API с этого адреса
                Log.Info($"Проверка обновления через GitHub API: ответ {(int)response.StatusCode}");
                return null;
            }
            var latest = ReleaseInfo.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            if (latest is null) Log.Info("Проверка обновления: в последнем релизе нет Snapkit.exe с хешем");
            return latest;
        }
        catch (Exception ex)
        {
            Log.Info($"Проверка обновления через GitHub API не удалась: {ex.GetType().Name}");
            return null;
        }
    }

    /// <summary>
    /// Скачать, сверить хеш и запустить новую версию. false — не вышло (причина в журнале).
    /// notify: false — без уведомлений, когда ответ показывает окно настроек.
    /// </summary>
    public async Task<bool> InstallAsync(ReleaseInfo latest, bool notify = true)
    {
        // Без хеша скачанный файл нечем проверить — не ставим
        if (latest.Sha256 is not { } sha256)
        {
            Log.Info($"Обновление {latest.Version}: нет хеша — не ставлю");
            return false;
        }
        if (_installing) return true;
        _installing = true;
        try
        {
            if (notify) Toasts.Show("Скачиваю обновление…");
            byte[] file;
            using (var timeout = new CancellationTokenSource(DownloadTimeout))
                file = await Http.GetByteArrayAsync(latest.ExeUrl, timeout.Token);
            // Файл, хеш которого не совпал, на диск не попадает и не запускается
            if (!UpdatePolicy.HashMatches(file, sha256))
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

    private static HttpClient CreateClient(bool followRedirects)
    {
        // Сроки задаются на каждый запрос: проверка короткая, скачивание exe — долгое
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = followRedirects }) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Snapkit/{CurrentVersion}");
        return client;
    }
}
