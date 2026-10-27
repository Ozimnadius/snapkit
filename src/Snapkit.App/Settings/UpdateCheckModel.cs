using Snapkit.App.Shell;
using Snapkit.Core.Updates;

namespace Snapkit.App.Settings;

/// <summary>
/// «Проверить обновления» в окне настроек: ответ пишется прямо в окно, даже при выключенных уведомлениях.
/// По кнопке предлагается и версия, отложенная после уведомления.
/// </summary>
/// <param name="current">Версия этой копии.</param>
/// <param name="canInstall">Копия из релиза умеет ставить обновление сама; сборке build.ps1 — только ссылка на релиз.</param>
/// <param name="fetchLatest">Последний релиз с GitHub или null — нет сети, GitHub недоступен, в релизе нет exe с хешем.</param>
/// <param name="install">Скачать, сверить хеш и запустить новую версию; false — не вышло (подробности в журнале).</param>
/// <param name="openReleasePage">Открыть страницу последнего релиза в браузере.</param>
public sealed class UpdateCheckModel(
    Version current,
    bool canInstall,
    Func<Task<ReleaseInfo?>> fetchLatest,
    Func<ReleaseInfo, Task<bool>> install,
    Action openReleasePage) : ObservableObject
{
    private string _status = "";
    private string _actionText = "";
    private bool _isIdle = true;
    private ReleaseInfo? _available;

    /// <summary>Что сейчас сказать: «Проверяю…», «У вас последняя версия», «Вышла версия 0.5.0»…</summary>
    public string Status { get => _status; private set => Set(ref _status, value); }

    /// <summary>Подпись кнопки рядом с ответом: «Обновить», «Скачать с GitHub»; пусто — кнопки нет.</summary>
    public string ActionText { get => _actionText; private set => Set(ref _actionText, value); }

    /// <summary>Ничего не ждём — кнопки доступны.</summary>
    public bool IsIdle { get => _isIdle; private set => Set(ref _isIdle, value); }

    public async Task CheckAsync()
    {
        if (!IsIdle) return;
        IsIdle = false;
        _available = null;
        ActionText = "";
        Status = "Проверяю…";
        try
        {
            var latest = await fetchLatest();
            if (latest is null)
            {
                Status = "Не удалось проверить обновления — попробуйте позже";
                return;
            }
            if (latest.Version <= current)
            {
                Status = "У вас последняя версия";
                return;
            }
            _available = latest;
            Status = $"Вышла версия {latest.Version}";
            ActionText = canInstall ? "Обновить" : "Скачать с GitHub";
        }
        finally
        {
            IsIdle = true;
        }
    }

    /// <summary>Кнопка рядом с ответом: поставить новую версию или открыть страницу релиза.</summary>
    public async Task RunActionAsync()
    {
        if (!IsIdle || _available is not { } latest) return;
        if (!canInstall)
        {
            openReleasePage();
            return;
        }
        IsIdle = false;
        ActionText = "";
        Status = $"Скачиваю версию {latest.Version}…";
        try
        {
            if (await install(latest))
            {
                // Новая версия встаёт на место этой копии и закрывает её сама
                Status = $"Ставлю версию {latest.Version} — Snapkit перезапустится";
                return;
            }
            Status = "Не удалось обновить. Подробности — в журнале";
            ActionText = "Обновить";
        }
        finally
        {
            IsIdle = true;
        }
    }
}
