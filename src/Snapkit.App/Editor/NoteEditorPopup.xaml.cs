using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Snapkit.App.Editor;

/// <summary>
/// Поле комментария, которое всплывает под фигурой. Enter — сохранить,
/// Shift+Enter — новая строка, Esc или крестик — закрыть без сохранения.
/// </summary>
public partial class NoteEditorPopup : UserControl
{
    public NoteEditorPopup(Guid annotationId, string text)
    {
        InitializeComponent();
        AnnotationId = annotationId;
        NoteText.Text = text;
        NoteText.CaretIndex = text.Length;
        Loaded += (_, _) => NoteText.Focus();
        // Клики по полю не должны доходить до холста и рамки снимка
        MouseLeftButtonDown += (_, e) => e.Handled = true;
    }

    public Guid AnnotationId { get; }
    public string Text => NoteText.Text;

    public event EventHandler? Committed;
    public event EventHandler? Dismissed;

    private void NoteText_TextChanged(object sender, TextChangedEventArgs e)
        => Placeholder.Visibility = NoteText.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void NoteText_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            Committed?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Dismissed?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Dismissed?.Invoke(this, EventArgs.Empty);
}
