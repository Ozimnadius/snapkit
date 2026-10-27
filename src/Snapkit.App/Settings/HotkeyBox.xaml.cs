using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Snapkit.App.Shell;
using Snapkit.Core.Input;

namespace Snapkit.App.Settings;

/// <summary>
/// Поле сочетания клавиш. Щелчок просит окно начать запись (RecordRequested); пока идёт запись,
/// нажатия ловит перехват клавиатуры Snapkit — так доходят и Print Screen, и сочетания, занятые Windows.
/// </summary>
public sealed partial class HotkeyBox : UserControl
{
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey), typeof(Hotkey?), typeof(HotkeyBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((HotkeyBox)d).Render()));

    private bool _isRecording;

    public HotkeyBox()
    {
        InitializeComponent();
        Render();
    }

    public Hotkey? Hotkey
    {
        get => (Hotkey?)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    /// <summary>Поле ждёт сочетание.</summary>
    public bool IsRecording
    {
        get => _isRecording;
        set
        {
            _isRecording = value;
            Render();
        }
    }

    /// <summary>Щёлкнули по полю — окно должно начать запись сочетания.</summary>
    public event EventHandler? RecordRequested;

    private void Frame_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => RecordRequested?.Invoke(this, EventArgs.Empty);

    private void Render()
    {
        Keys.Children.Clear();
        Frame.SetResourceReference(Border.BorderBrushProperty, _isRecording ? "AccentBrush" : ThemeManager.PanelBorderKey);
        if (_isRecording)
        {
            Keys.Children.Add(Label("Нажмите сочетание…", muted: false));
            Hint.Text = "Esc — отмена, Backspace — очистить";
            return;
        }
        Hint.Text = "Нажми, чтобы изменить";
        if (Hotkey is not { } hotkey)
        {
            Keys.Children.Add(Label("Не назначено", muted: true));
            return;
        }
        foreach (var part in HotkeyParser.Display(hotkey).Split('+'))
        {
            var chip = new Border
            {
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(0, 0, 5, 0), Child = Label(part, muted: false),
            };
            chip.SetResourceReference(Border.BorderBrushProperty, ThemeManager.MutedTextKey);
            Keys.Children.Add(chip);
        }
    }

    private static TextBlock Label(string text, bool muted)
    {
        var label = new TextBlock { Text = text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        label.SetResourceReference(TextBlock.ForegroundProperty, muted ? ThemeManager.MutedTextKey : ThemeManager.TextKey);
        return label;
    }
}
