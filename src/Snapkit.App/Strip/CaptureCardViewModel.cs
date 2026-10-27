using System.Windows;
using System.Windows.Media.Imaging;

namespace Snapkit.App.Strip;

/// <summary>Карточка снимка в ленте.</summary>
public sealed class CaptureCardViewModel(
    Guid id, BitmapSource thumbnail, string letter, int noteCount, Thickness topMargin)
{
    public Guid Id { get; } = id;
    public BitmapSource Thumbnail { get; } = thumbnail;

    /// <summary>Буква — место снимка на ленте (A, B…).</summary>
    public string Letter { get; } = letter;
    public int NoteCount { get; } = noteCount;
    public bool HasNotes => NoteCount > 0;

    /// <summary>Отрицательный отступ сверху: карточка наезжает на предыдущую — получается стопка.</summary>
    public Thickness TopMargin { get; } = topMargin;
}
