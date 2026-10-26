using System.Windows;
using System.Windows.Controls;

namespace Snapkit.App.Editor;

/// <summary>Подсказка «?» со всеми клавишами редактора. Esc или крестик закрывают её.</summary>
public partial class ShortcutsPopup : UserControl
{
    public ShortcutsPopup()
    {
        InitializeComponent();
        Rows.ItemsSource = EditorKeys.Shortcuts.Select(s => new { s.Keys, s.Action }).ToList();
        // Клики по подсказке не должны доходить до холста и рамки снимка
        MouseLeftButtonDown += (_, e) => e.Handled = true;
    }

    public event EventHandler? Closed;

    private void Close_Click(object sender, RoutedEventArgs e) => Closed?.Invoke(this, EventArgs.Empty);
}
