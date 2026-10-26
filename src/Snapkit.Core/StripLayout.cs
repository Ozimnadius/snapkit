namespace Snapkit.Core;

/// <summary>Геометрия ленты для перетаскивания карточек.</summary>
public static class StripLayout
{
    /// <summary>
    /// Перед какой карточкой бросили (0…count): y — от верха первой карточки; карточка i начинается
    /// на i × stripeHeight, последняя видна целиком (cardHeight). Граница — середина видимой части карточки.
    /// </summary>
    public static int DropSlot(double y, int count, double cardHeight, double stripeHeight)
    {
        for (var i = 0; i < count; i++)
        {
            var visible = i == count - 1 ? cardHeight : stripeHeight;
            if (y < i * stripeHeight + visible / 2) return i;
        }
        return count;
    }

    /// <summary>Мышь сдвинулась достаточно, чтобы это было перетаскивание, а не щелчок.</summary>
    public static bool IsDragStarted(double dx, double dy, double minX, double minY)
        => Math.Abs(dx) >= minX || Math.Abs(dy) >= minY;
}
