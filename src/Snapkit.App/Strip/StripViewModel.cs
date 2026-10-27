using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;
using Snapkit.App.Shell;
using Snapkit.Core;
using Snapkit.Core.Model;
using Snapkit.Core.Session;

namespace Snapkit.App.Strip;

/// <summary>Состояние ленты: карточки, счётчик снимков, подсказки.</summary>
public sealed class StripViewModel : ObservableObject
{
    /// <summary>Полная высота карточки и видимая полоса у карточек под стопкой (компактно, как у Snapik).</summary>
    public const double CardHeight = 96;
    public const double StripeHeight = 26;

    // Миниатюра перерисовывается, только если изменился снимок или его буква.
    private Dictionary<Guid, (Capture Capture, char Letter, BitmapSource Thumb)> _thumbs = [];

    private int _captureCount;
    private bool _isEmpty = true;
    private bool _showSoftWarning;
    private bool _isTopmost = true;
    private bool _showStripSetting = true;
    private string _emptyHint = "Нажми Print Screen или «Новый снимок»";

    public ObservableCollection<CaptureCardViewModel> Cards { get; } = [];

    /// <summary>Сколько снимков на ленте — число рядом с «Snapkit» и в капсуле.</summary>
    public int CaptureCount { get => _captureCount; private set => Set(ref _captureCount, value); }
    public bool IsEmpty { get => _isEmpty; private set => Set(ref _isEmpty, value); }
    public bool ShowSoftWarning { get => _showSoftWarning; private set => Set(ref _showSoftWarning, value); }
    public string EmptyHint { get => _emptyHint; set => Set(ref _emptyHint, value); }

    public bool IsTopmost { get => _isTopmost; set => Set(ref _isTopmost, value); }

    /// <summary>Настройка «Показывать ленту» — для галочки в меню «…».</summary>
    public bool ShowStripSetting { get => _showStripSetting; set => Set(ref _showStripSetting, value); }

    public string SoftWarningText { get; } = $"Чаты обычно принимают до {SessionModel.SoftWarningCount} картинок за раз";

    /// <summary>Забыть миниатюры: следующий Refresh перерисует все (сменился цвет программы).</summary>
    public void ForgetThumbnails() => _thumbs = [];

    public void Refresh(IReadOnlyList<Capture> captures, Func<Capture, char, BitmapSource> renderThumbnail)
    {
        var letters = CaptureLetters.Assign(captures);
        var thumbs = new Dictionary<Guid, (Capture, char, BitmapSource)>();
        Cards.Clear();
        for (var i = 0; i < captures.Count; i++)
        {
            var capture = captures[i];
            var letter = letters[capture.Id];
            var thumb = _thumbs.TryGetValue(capture.Id, out var cached)
                        && ReferenceEquals(cached.Capture, capture) && cached.Letter == letter
                ? cached.Thumb
                : renderThumbnail(capture, letter);
            thumbs[capture.Id] = (capture, letter, thumb);

            var margin = new Thickness(0, i == 0 ? 0 : -(CardHeight - StripeHeight), 0, 0);
            Cards.Add(new CaptureCardViewModel(capture.Id, thumb, letter.ToString(), capture.NoteCount, margin));
        }
        _thumbs = thumbs;

        CaptureCount = captures.Count;
        IsEmpty = captures.Count == 0;
        ShowSoftWarning = captures.Count >= SessionModel.SoftWarningCount;
    }
}
