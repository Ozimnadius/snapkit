using System.IO;
using System.Windows.Media.Imaging;
using Snapkit.App.Imaging;

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
