using System.Runtime.CompilerServices;
using Snapkit.App.Shell;

namespace Snapkit.App.Tests;

internal static class TestSetup
{
    /// <summary>Тесты не пишут в настоящий журнал программы пользователя.</summary>
    [ModuleInitializer]
    internal static void DisableAppLog() => Log.Enabled = false;
}
