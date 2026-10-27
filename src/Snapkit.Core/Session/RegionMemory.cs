namespace Snapkit.Core.Session;

/// <summary>«Предлагать ту же область»: прежняя рамка годится, только если целиком на одном из мониторов.</summary>
public static class RegionMemory
{
    /// <summary>Прежняя область, если она целиком на одном из нынешних мониторов; иначе null.</summary>
    public static RectI? FitOnScreen(RectI region, IReadOnlyList<RectI> monitors)
        => monitors.Any(m => Contains(m, region)) ? region : null;

    private static bool Contains(RectI outer, RectI inner)
        => inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;
}
