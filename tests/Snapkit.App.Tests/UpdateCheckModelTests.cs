using Snapkit.App.Settings;
using Snapkit.Core.Updates;

namespace Snapkit.App.Tests;

public class UpdateCheckModelTests
{
    private static readonly Version Current = new(0, 4, 0);

    private static ReleaseInfo Release(string version)
        => new(Version.Parse(version), $"https://github.com/Ozimnadius/snapkit/releases/download/v{version}/Snapkit.exe", "00");

    private static UpdateCheckModel Model(ReleaseInfo? latest, bool canInstall = true,
        Func<ReleaseInfo, Task<bool>>? install = null, Action? openReleasePage = null)
        => new(Current, canInstall, () => Task.FromResult(latest), install ?? (_ => Task.FromResult(true)), openReleasePage ?? (() => { }));

    [Fact]
    public async Task Check_Newer_OffersUpdate()
    {
        var model = Model(Release("0.5.0"));
        await model.CheckAsync();
        Assert.Equal("Вышла версия 0.5.0", model.Status);
        Assert.Equal("Обновить", model.ActionText);
    }

    [Fact]
    public async Task Check_Newer_NotReleaseCopy_OffersReleasePage()
    {
        // Сборка build.ps1 сама не обновляется — только ссылка на релиз
        var model = Model(Release("0.5.0"), canInstall: false);
        await model.CheckAsync();
        Assert.Equal("Вышла версия 0.5.0", model.Status);
        Assert.Equal("Скачать с GitHub", model.ActionText);
    }

    [Theory]
    [InlineData("0.4.0")]
    [InlineData("0.3.1")]
    public async Task Check_SameOrOlder_UpToDate(string version)
    {
        var model = Model(Release(version));
        await model.CheckAsync();
        Assert.Equal("У вас последняя версия", model.Status);
        Assert.Equal("", model.ActionText);
    }

    [Fact]
    public async Task Check_NoAnswer_TryLater()
    {
        var model = Model(null);
        await model.CheckAsync();
        Assert.Equal("Не удалось проверить обновления — попробуйте позже", model.Status);
        Assert.Equal("", model.ActionText);
    }

    [Fact]
    public async Task Check_WhileWaiting_Busy()
    {
        var answer = new TaskCompletionSource<ReleaseInfo?>();
        var model = new UpdateCheckModel(Current, true, () => answer.Task, _ => Task.FromResult(true), () => { });

        var check = model.CheckAsync();
        Assert.False(model.IsIdle);
        Assert.Equal("Проверяю…", model.Status);

        answer.SetResult(Release("0.4.0"));
        await check;
        Assert.True(model.IsIdle);
    }

    [Fact]
    public async Task Action_Update_InstallsLatest()
    {
        ReleaseInfo? installed = null;
        var model = Model(Release("0.5.0"), install: latest =>
        {
            installed = latest;
            return Task.FromResult(true);
        });
        await model.CheckAsync();
        await model.RunActionAsync();
        Assert.Equal(new Version(0, 5, 0), installed?.Version);
        Assert.Equal("Ставлю версию 0.5.0 — Snapkit перезапустится", model.Status);
        Assert.Equal("", model.ActionText);
    }

    [Fact]
    public async Task Action_Update_WhileDownloading_Busy()
    {
        var done = new TaskCompletionSource<bool>();
        var model = Model(Release("0.5.0"), install: _ => done.Task);
        await model.CheckAsync();

        var action = model.RunActionAsync();
        Assert.False(model.IsIdle);
        Assert.Equal("Скачиваю версию 0.5.0…", model.Status);

        done.SetResult(true);
        await action;
    }

    [Fact]
    public async Task Action_UpdateFailed_SeeLog_CanRetry()
    {
        var model = Model(Release("0.5.0"), install: _ => Task.FromResult(false));
        await model.CheckAsync();
        await model.RunActionAsync();
        Assert.Equal("Не удалось обновить. Подробности — в журнале", model.Status);
        Assert.Equal("Обновить", model.ActionText);
        Assert.True(model.IsIdle);
    }

    [Fact]
    public async Task Action_NotReleaseCopy_OpensReleasePage()
    {
        var opened = false;
        var installCalled = false;
        var model = Model(Release("0.5.0"), canInstall: false,
            install: _ => { installCalled = true; return Task.FromResult(true); },
            openReleasePage: () => opened = true);
        await model.CheckAsync();
        await model.RunActionAsync();
        Assert.True(opened);
        Assert.False(installCalled);
    }

    [Fact]
    public async Task Action_BeforeCheck_DoesNothing()
    {
        var installCalled = false;
        var model = Model(Release("0.5.0"), install: _ => { installCalled = true; return Task.FromResult(true); });
        await model.RunActionAsync();
        Assert.False(installCalled);
    }
}
