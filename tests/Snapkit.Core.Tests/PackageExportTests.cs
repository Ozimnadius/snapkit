using Snapkit.Core.Settings;
using Snapkit.Core.Model;
using static Snapkit.Core.Tests.TestData;

namespace Snapkit.Core.Tests;

public class PackageExportTests
{
    [Fact]
    public void TimestampName_IsSortableDateAndTime()
        => Assert.Equal("2026-10-26_15-40-12", PackageExport.TimestampName(new DateTime(2026, 10, 26, 15, 40, 12)));

    [Fact]
    public void ImageFileName_IsLetter() => Assert.Equal("B.png", PackageExport.ImageFileName('B'));

    [Fact]
    public void UniqueName_AddsNumberWhenTaken()
    {
        Assert.Equal("x", PackageExport.UniqueName("x", _ => false));
        Assert.Equal("x (3)", PackageExport.UniqueName("x", n => n is "x" or "x (2)"));
    }

    [Fact]
    public void BuildPromptMarkdown_ImagesThenChatText()
    {
        IReadOnlyList<LetteredCapture> package =
        [
            new('A', Cap(Shape(AnnotationKind.Rectangle, note: "кнопка"))),
            new('B', Cap()),
        ];
        Assert.Equal("![Снимок A](A.png)\n![Снимок B](B.png)\n\n" + PromptBuilder.Build(package),
            PackageExport.BuildPromptMarkdown(package));
    }

    [Fact]
    public void BuildPromptMarkdown_NoNotes_OnlyImages()
        => Assert.Equal("![Снимок A](A.png)", PackageExport.BuildPromptMarkdown([new('A', Cap())]));

    [Fact]
    public void ImageFileName_Jpeg_IsJpg() => Assert.Equal("B.jpg", PackageExport.ImageFileName('B', SaveFormat.Jpeg));

    [Fact]
    public void BuildPromptMarkdown_Jpeg_LinksJpg()
    {
        var item = new LetteredCapture('A', Cap(Shape(AnnotationKind.Rectangle, note: "кнопка")));
        Assert.StartsWith("![Снимок A](A.jpg)", PackageExport.BuildPromptMarkdown([item], SaveFormat.Jpeg));
    }
}
