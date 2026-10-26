using System.IO;
using System.Windows.Media.Imaging;
using Snapkit.App.Imaging;
using Snapkit.Core;

namespace Snapkit.App.Files;

/// <summary>Снимки файлами: для перетаскивания в другие окна и для сохранения на диск.</summary>
public static class CaptureFiles
{
    /// <summary>Имя файла перетаскиваемого снимка: «Снимок A.png» или «Снимок.png» у отправленного.</summary>
    public static string DragFileName(char? letter) => letter is { } l ? $"Снимок {l}.png" : "Снимок.png";

    /// <summary>PNG для перетаскивания в своей подпапке dragRoot\&lt;guid&gt;\ — имена не пересекаются; возвращает путь.</summary>
    public static string WriteDragFile(string dragRoot, BitmapSource rendered, char? letter)
    {
        var folder = Path.Combine(dragRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, DragFileName(letter));
        File.WriteAllBytes(path, ImageCodecs.EncodePng(rendered));
        return path;
    }

    /// <summary>Папка сохранения по умолчанию — «Изображения\Snapkit».</summary>
    public static string DefaultSaveFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Snapkit");

    public static void SavePng(string path, BitmapSource rendered) => File.WriteAllBytes(path, ImageCodecs.EncodePng(rendered));

    /// <summary>
    /// Создать parent\&lt;время&gt; (или «… (2)», если занято) и положить A.png, B.png… и prompt.md;
    /// вернуть путь папки.
    /// </summary>
    public static string SavePackage(string parent, DateTime now, IReadOnlyList<(LetteredCapture Item, BitmapSource Rendered)> package)
    {
        var name = PackageExport.UniqueName(PackageExport.TimestampName(now), n => Path.Exists(Path.Combine(parent, n)));
        var folder = Path.Combine(parent, name);
        Directory.CreateDirectory(folder);
        foreach (var (item, rendered) in package) SavePng(Path.Combine(folder, PackageExport.ImageFileName(item.Letter)), rendered);
        File.WriteAllText(Path.Combine(folder, "prompt.md"), PackageExport.BuildPromptMarkdown(package.Select(p => p.Item).ToList()));
        return folder;
    }

    /// <summary>Удалить папку перетаскивания целиком; занятые файлы пропускаются до следующего раза.</summary>
    public static void CleanDragRoot(string dragRoot)
    {
        if (!Directory.Exists(dragRoot)) return;
        foreach (var folder in Directory.EnumerateDirectories(dragRoot))
        {
            try { Directory.Delete(folder, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
