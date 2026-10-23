using System.Collections.Immutable;

namespace Snapkit.Core.Model;

/// <summary>
/// Снимок в ленте. Исходная картинка хранится отдельно (файл {Id}.png в папке сессии),
/// разметка — объектами в Annotations, итоговая картинка собирается по требованию.
/// ScreenBounds — где снимок был на экране, в физических пикселях виртуального экрана.
/// </summary>
public sealed record Capture(
    Guid Id,
    int PixelWidth,
    int PixelHeight,
    RectI ScreenBounds,
    ImmutableList<Annotation> Annotations,
    string SnapshotNote,
    bool IsSent,
    DateTimeOffset CreatedAt)
{
    public bool HasSnapshotNote => !string.IsNullOrWhiteSpace(SnapshotNote);

    /// <summary>Число заметок для значка в ленте: заметки с кружками плюс общий комментарий.</summary>
    public int NoteCount => Annotations.Count(a => a.HasNote) + (HasSnapshotNote ? 1 : 0);
}
