using System.Windows.Input;
using Snapkit.App.Editor;

namespace Snapkit.App.Tests;

public class EditorKeysTests
{
    private static readonly Dictionary<ToolGroup, EditorTool> Last = new()
    {
        [ToolGroup.Shape] = EditorTool.Ellipse,
        [ToolGroup.Arrow] = EditorTool.CurvedArrow,
        [ToolGroup.Pencil] = EditorTool.Marker,
    };

    [Fact]
    public void ToolFor_R_ReturnsLastShapeVariant() => Assert.Equal(EditorTool.Ellipse, EditorKeys.ToolFor(Key.R, Last));

    [Fact]
    public void ToolFor_A_ReturnsLastArrowVariant() => Assert.Equal(EditorTool.CurvedArrow, EditorKeys.ToolFor(Key.A, Last));

    [Fact]
    public void ToolFor_R_WithoutHistory_IsRectangle() => Assert.Equal(EditorTool.Rectangle, EditorKeys.ToolFor(Key.R, new Dictionary<ToolGroup, EditorTool>()));

    [Theory]
    [InlineData(Key.V, EditorTool.Select)]
    [InlineData(Key.L, EditorTool.Line)]
    [InlineData(Key.P, EditorTool.Pencil)]
    [InlineData(Key.M, EditorTool.Marker)]
    [InlineData(Key.T, EditorTool.Text)]
    [InlineData(Key.E, EditorTool.Eraser)]
    [InlineData(Key.B, EditorTool.Blur)]
    [InlineData(Key.X, EditorTool.Crop)]
    [InlineData(Key.C, EditorTool.Pin)]
    public void ToolFor_LetterKeys(Key key, EditorTool tool) => Assert.Equal(tool, EditorKeys.ToolFor(key, Last));

    [Fact]
    public void ToolFor_UnknownKey_IsNull() => Assert.Null(EditorKeys.ToolFor(Key.Q, Last));

    [Fact]
    public void GroupOf_Ellipse_IsShape() => Assert.Equal(ToolGroup.Shape, EditorKeys.GroupOf(EditorTool.Ellipse));

    [Fact]
    public void GroupOf_Text_IsNone() => Assert.Equal(ToolGroup.None, EditorKeys.GroupOf(EditorTool.Text));

    [Fact]
    public void Variants_Pencil_ArePencilAndMarker()
        => Assert.Equal([EditorTool.Pencil, EditorTool.Marker], EditorKeys.Variants(ToolGroup.Pencil));

    [Fact]
    public void Variants_AllBelongToTheirGroup()
    {
        foreach (var group in new[] { ToolGroup.Shape, ToolGroup.Arrow, ToolGroup.Pencil })
            Assert.All(EditorKeys.Variants(group), tool => Assert.Equal(group, EditorKeys.GroupOf(tool)));
    }

    [Fact]
    public void Shortcuts_CoverEveryToolKey()
    {
        foreach (var letter in new[] { "V", "R", "A", "L", "P", "M", "T", "E", "B", "X", "C" })
            Assert.Contains(EditorKeys.Shortcuts, s => s.Keys == letter);
    }
}
