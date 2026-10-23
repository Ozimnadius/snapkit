using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Snapkit.App.Editor;

/// <summary>Строка панели комментариев: «A1  У Макса значок VK в админке».</summary>
public sealed record CommentItem(Guid Id, string Label, string Text);

/// <summary>Панель «Комментарии» у края экрана: все заметки снимка и общий комментарий к нему.</summary>
public partial class CommentsPanel : UserControl
{
    private bool _settingNote;

    public CommentsPanel()
    {
        InitializeComponent();
        // Клики по панели не должны доходить до холста и рамки снимка
        MouseLeftButtonDown += (_, e) => e.Handled = true;
    }

    public event EventHandler<Guid>? ItemClicked;
    public event EventHandler<Guid>? ItemRemoved;
    public event EventHandler<string>? SnapshotNoteChanged;

    public void SetItems(IReadOnlyList<CommentItem> items)
    {
        Items.ItemsSource = items;
        EmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetSnapshotNote(string note)
    {
        _settingNote = true;
        SnapshotNoteText.Text = note;
        _settingNote = false;
    }

    private void Row_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid id }) ItemClicked?.Invoke(this, id);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid id }) ItemRemoved?.Invoke(this, id);
    }

    private void SnapshotNoteText_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_settingNote) SnapshotNoteChanged?.Invoke(this, SnapshotNoteText.Text);
    }
}
