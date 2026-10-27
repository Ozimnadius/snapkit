using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using Snapkit.App.Shell;
using Snapkit.Core.Theming;

namespace Snapkit.App.Tests;

[CollectionDefinition("Theme", DisableParallelization = true)]
public sealed class ThemeCollection;

/// <summary>Смена темы на лету: кисти в ресурсах приложения и DynamicResource во всех окнах.</summary>
[Collection("Theme")]
public class ThemeTests
{
    [Fact]
    public void ThemeKeys_AreDynamicInXaml()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Snapkit.slnx"))) root = root.Parent;
        Assert.NotNull(root);

        // Окна с StaticResource на цвета темы не перекрашиваются при смене темы
        var keys = "PanelBrush|PanelRaisedBrush|PanelBorderBrush|HoverBrush|TextBrush|MutedTextBrush|ChipBrush|WarningTextBrush|AccentBrush|AccentHoverBrush";
        var staticUse = new Regex($@"StaticResource\s+({keys})\b");
        var offenders = Directory.GetFiles(Path.Combine(root!.FullName, "src", "Snapkit.App"), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: Path.GetFileName(f), Line: i + 1, Text: line)))
            .Where(x => staticUse.IsMatch(x.Text))
            .Select(x => $"{x.File}:{x.Line}")
            .ToList();

        Assert.True(offenders.Count == 0, "Цвет темы подключён без смены на лету: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Apply_Light_ChangesResources() => UiApp.Run(() =>
    {
        try
        {
            ThemeManager.Apply(ThemePalette.Light);
            Assert.Equal(Colors.White, ((SolidColorBrush)Application.Current.Resources[ThemeManager.PanelKey]).Color);
            ThemeManager.Apply(ThemePalette.Dark);
            Assert.Equal(Color.FromRgb(0x1E, 0x21, 0x28), ((SolidColorBrush)Application.Current.Resources[ThemeManager.PanelKey]).Color);
        }
        finally
        {
            ThemeManager.Apply(ThemePalette.Dark);
        }
    });

    [Fact]
    public void TextBoxCaret_FollowsTheme()
    {
        // Белый курсор на белом поле светлой темы не виден — печатать пришлось бы вслепую
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Snapkit.slnx"))) root = root.Parent;
        var offenders = Directory.GetFiles(Path.Combine(root!.FullName, "src", "Snapkit.App"), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: Path.GetFileName(f), Line: i + 1, Text: line)))
            .Where(x => x.Text.Contains("CaretBrush=\"White\""))
            .Select(x => $"{x.File}:{x.Line}")
            .ToList();
        Assert.True(offenders.Count == 0, "Белый курсор в поле ввода: " + string.Join(", ", offenders));
    }
}
