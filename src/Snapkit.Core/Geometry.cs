namespace Snapkit.Core;

/// <summary>Точка в пикселях снимка.</summary>
public readonly record struct PointD(double X, double Y);

/// <summary>Прямоугольник в целых пикселях (экрана или снимка).</summary>
public readonly record struct RectI(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}
