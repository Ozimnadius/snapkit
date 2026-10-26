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

    /// <summary>
    /// Чья видимая часть под мышью (y — от верха первой карточки): у карточек в стопке — полоса,
    /// у последней — вся карточка; -1 — ни одной. Раскрывается именно она, а не та, что уже поднята.
    /// </summary>
    public static int HoverIndex(double y, int count, double cardHeight, double stripeHeight)
    {
        if (count == 0 || y < 0) return -1;
        var index = (int)(y / stripeHeight);
        if (index < count - 1) return index;
        return y < (count - 1) * stripeHeight + cardHeight ? count - 1 : -1;
    }

    /// <summary>Место броска, только если точка над списком карточек (width × height); иначе null.</summary>
    public static int? DropSlotInside(double x, double y, double width, double height, int count, double cardHeight, double stripeHeight)
        => x < 0 || y < 0 || x >= width || y >= height ? null : DropSlot(y, count, cardHeight, stripeHeight);

    /// <summary>Где рисовать линию «сюда встанет» для места slot (от верха первой карточки).</summary>
    public static double SlotTop(int slot, int count, double cardHeight, double stripeHeight)
        => slot < count ? slot * stripeHeight : (count - 1) * stripeHeight + cardHeight;

    /// <summary>Мышь сдвинулась достаточно, чтобы это было перетаскивание, а не щелчок.</summary>
    public static bool IsDragStarted(double dx, double dy, double minX, double minY)
        => Math.Abs(dx) >= minX || Math.Abs(dy) >= minY;
}
