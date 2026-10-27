namespace Snapkit.Core.Session;

/// <summary>Курсор на снимке: где рисовать его значок внутри кадра.</summary>
public static class CursorPlacement
{
    /// <summary>Позиция курсора минус «горячая точка» значка минус угол кадра — всё в физических пикселях.</summary>
    public static (int X, int Y) TopLeft((int X, int Y) cursor, (int X, int Y) hotspot, RectI bounds)
        => (cursor.X - hotspot.X - bounds.X, cursor.Y - hotspot.Y - bounds.Y);
}
