using System.Collections.Immutable;
using Snapkit.Core.Model;

namespace Snapkit.Core.Tests;

/// <summary>Короткие конструкторы снимков и фигур для тестов.</summary>
internal static class TestData
{
    public static Annotation Shape(AnnotationKind kind, string? note = null, string? text = null, params PointD[] points) => new(
        Guid.NewGuid(), kind,
        points.Length > 0 ? [.. points] : [new PointD(0, 0), new PointD(10, 10)],
        "#E53935", 4, text, note);

    public static Capture Cap(bool sent = false, params Annotation[] annotations) => new(
        Guid.NewGuid(), 100, 50, new RectI(10, 20, 100, 50), [.. annotations], "", sent,
        DateTimeOffset.UnixEpoch);

    /// <summary>Минимальный настоящий PNG 1×1.</summary>
    public static readonly byte[] Png1x1 = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
}
