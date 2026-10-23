using System.IO;
using System.Text.RegularExpressions;

namespace Snapkit.App.Tests;

/// <summary>
/// Пользователь не читает по-английски, а сообщения исключений .NET — английские.
/// Их можно писать в журнал, но не показывать в уведомлениях и окнах.
/// </summary>
public class UserFacingTextTests
{
    [Fact]
    public void ExceptionMessages_AreNeverShownToUser()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Snapkit.slnx"))) root = root.Parent;
        Assert.NotNull(root);

        var shown = new Regex(@"(Toasts\.(Show|Error)|MessageBox\.Show)\([^;]*\.Message\b");
        var offenders = Directory.GetFiles(Path.Combine(root!.FullName, "src", "Snapkit.App"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: Path.GetFileName(f), Line: i + 1, Text: line)))
            .Where(x => shown.IsMatch(x.Text))
            .Select(x => $"{x.File}:{x.Line}")
            .ToList();

        Assert.True(offenders.Count == 0, "Сообщение исключения показывается пользователю: " + string.Join(", ", offenders));
    }
}
