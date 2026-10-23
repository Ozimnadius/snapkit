using Snapkit.Core;

namespace Snapkit.App.Capturing;

/// <summary>
/// Монитор в физических пикселях виртуального экрана.
/// DpiScale — масштаб Windows для этого монитора (1.0 = 100 %, 1.5 = 150 %).
/// </summary>
public sealed record MonitorInfo(nint Handle, RectI Bounds, RectI WorkArea, double DpiScale, bool IsPrimary)
{
    public bool Contains(int x, int y) => x >= Bounds.X && x < Bounds.Right && y >= Bounds.Y && y < Bounds.Bottom;
}
