using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>1.9 T1 / T2: <see cref="SkUiTileLayout"/> and <see cref="SkUiCoreTileLayout"/>, one engine.</summary>
public class TileLayoutTests
{
    private const double Tolerance = 0.001;

    private static SkUiTileLayout Tiles(int count, double tileSize = 100, double spacing = 0)
    {
        var layout = new SkUiTileLayout { TileSize = tileSize, ColumnSpacing = spacing, RowSpacing = spacing };
        for (var index = 0; index < count; index++)
            layout.Children.Add(new SkUiBox { Color = Colors.Red });
        return layout;
    }

    private static SkUiCoreTileLayout CoreTiles(int count, double tileSize = 100, double spacing = 0)
    {
        var layout = new SkUiCoreTileLayout().SetTileSize(tileSize).SetColumnSpacing(spacing).SetRowSpacing(spacing);
        for (var index = 0; index < count; index++)
            layout.Add(new SkUiCoreBox().SetColor(Colors.Red));
        return layout;
    }

    private static void Tap(SkUiView root, Point position)
    {
        root.Touch(new(1, SkUiTouchAction.Pressed, position, TimeSpan.Zero));
        root.Touch(new(1, SkUiTouchAction.Released, position, TimeSpan.FromMilliseconds(50)));
    }

    private static List<ISkUiRenderable> RenderChildren(ISkUiRenderable node)
    {
        var children = new List<ISkUiRenderable>();
        node.GetRenderChildren(children);
        return children;
    }

    [Theory]
    [InlineData(300, 0, 0, 3, 100)] // three tiles of exactly the tile size
    [InlineData(301, 0, 0, 4, 75.25)] // one DIP more: a fourth column, tiles stretched to fill
    [InlineData(250, 0, 0, 3, 250 / 3d)]
    [InlineData(99, 0, 0, 1, 99)] // narrower than one tile: one smaller tile
    [InlineData(301, 0, 90, 3, 100)] // 75.25 is under the minimum: a column dropped, tiles capped at the tile size
    [InlineData(320, 10, 0, 3, 100)] // spacing between columns: 3 × 100 + 2 × 10
    [InlineData(321, 10, 0, 4, 72.75)]
    public void ColumnsAndTileSizesFollowTheWidth(double width, double spacing, double minimum, int columns, double tile)
    {
        var layout = Tiles(8, spacing: spacing);
        layout.MinimumTileSize = minimum;
        SkUiTestHelpers.Arrange(layout, width, 1000);

        Assert.Equal(columns, layout.Columns);
        var first = layout.Children[0].Frame;
        Assert.Equal(tile, first.Width, Tolerance);
        Assert.Equal(tile, first.Height, Tolerance);
        Assert.Equal(columns < 8 ? tile + spacing : 0, layout.Children[columns].Frame.Y, Tolerance); // the first tile of row two
        Assert.Equal(0, layout.Children[columns].Frame.X, Tolerance);
    }

    [Fact]
    public void PaddingSpacingAndAspectRatioPlaceTheTiles()
    {
        var layout = Tiles(5, spacing: 10);
        layout.Padding = new Thickness(10, 20);
        layout.RowSpacing = 6;
        layout.TileAspectRatio = 2;
        var size = ((IView)layout).Measure(340, double.PositiveInfinity);
        SkUiTestHelpers.Arrange(layout, 340, 1000);

        Assert.Equal(3, layout.Columns);
        Assert.Equal(new Rect(10, 20, 100, 50), layout.Children[0].Frame);
        Assert.Equal(new Rect(230, 20, 100, 50), layout.Children[2].Frame);
        Assert.Equal(new Rect(120, 76, 100, 50), layout.Children[4].Frame);
        Assert.Equal(new Size(340, 20 + 50 + 6 + 50 + 20), size);
    }

    [Fact]
    public void TilesCappedAtTheTileSizeLeaveTheRestAtTheRowsEnd()
    {
        var layout = Tiles(2);
        layout.MinimumTileSize = 100;
        var size = ((IView)layout).Measure(390, double.PositiveInfinity);
        Assert.Equal(new Size(200, 100), size); // three columns of 100, two children: 200 used; 190 left over
        SkUiTestHelpers.Arrange(layout, 390, 200);
        Assert.Equal(3, layout.Columns);
        Assert.Equal(new Rect(100, 0, 100, 100), layout.Children[1].Frame);
    }

    [Fact]
    public void AnUnconstrainedWidthPutsEveryTileInOneRow()
    {
        var layout = Tiles(4, spacing: 5);
        var size = ((IView)layout).Measure(double.PositiveInfinity, double.PositiveInfinity);
        Assert.Equal(new Size(4 * 100 + 3 * 5, 100), size);
    }

    [Fact]
    public void HiddenChildrenTakeNoTile()
    {
        var layout = Tiles(4);
        ((SkUiView)layout.Children[1]).IsVisible = false;
        SkUiTestHelpers.Arrange(layout, 300, 300);
        Assert.Equal(new Rect(100, 0, 100, 100), layout.Children[2].Frame);
        Assert.Equal(new Rect(200, 0, 100, 100), layout.Children[3].Frame);
    }

    [Fact]
    public void RightToLeftMirrorsTheColumns()
    {
        var layout = Tiles(4);
        layout.MinimumTileSize = 100; // three columns of 100, 50 left over at the row's end (the left in RTL)
        layout.FlowDirection = FlowDirection.RightToLeft;
        SkUiTestHelpers.Arrange(layout, 350, 300);
        Assert.Equal(new Rect(250, 0, 100, 100), layout.Children[0].Frame);
        Assert.Equal(new Rect(50, 0, 100, 100), layout.Children[2].Frame);
    }

    [Theory]
    [InlineData(300, false)]
    [InlineData(301, false)]
    [InlineData(301, true)]
    public void CoreAndSkUiTilesHaveTheSameFrames(double width, bool rtl)
    {
        var drawn = Tiles(7, spacing: 4);
        drawn.Padding = new Thickness(3, 5);
        drawn.TileAspectRatio = 1.5;
        var core = CoreTiles(7, spacing: 4).SetPadding(new Thickness(3, 5));
        core.SetTileAspectRatio(1.5);
        var host = new SkUiCoreHost().SetContent(core);
        if (rtl)
        {
            drawn.FlowDirection = FlowDirection.RightToLeft;
            host.FlowDirection = FlowDirection.RightToLeft;
        }
        SkUiTestHelpers.Arrange(drawn, width, 1000);
        SkUiTestHelpers.Arrange(host, width, 1000);
        for (var index = 0; index < 7; index++)
            Assert.Equal(drawn.Children[index].Frame, ((SkUiCoreNode)core.Children[index]).Frame);
    }

    [Fact]
    public void ChildrenPastTheRowLimitAreNeitherMeasuredNorDrawn()
    {
        var layout = Tiles(6);
        var probe = new MeasureProbe();
        layout.Children.Add(probe);
        layout.MaximumRows = 2;
        using var surface = new SkUiTestSurface(layout, 300, 300);
        surface.Frame();

        Assert.Equal(0, probe.Measures);
        Assert.Equal(1, layout.Overflow.HiddenCount);
        Assert.DoesNotContain(probe, RenderChildren(layout));
        Assert.Equal(new Size(300, 200), layout.DesiredSize);
    }

    [Fact]
    public void TheOverflowTileShowsTheHiddenCountOverTheLastTileAndTakesTheTap()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher(); // bindings
        var layout = Tiles(9);
        SkUiLabel? label = null;
        var commanded = new List<object?>();
        var tapped = 0;
        layout.MaximumRows = 2;
        layout.OverflowTemplate = new DataTemplate(() =>
        {
            label = new SkUiLabel();
            label.SetBinding(SkUiLabel.TextProperty, new Binding(nameof(SkUiTileOverflow.HiddenCount), stringFormat: "+{0}"));
            return label;
        });
        layout.OverflowCommand = new Command<object?>(commanded.Add);
        layout.OverflowTapped += (_, _) => tapped++;
        using var surface = new SkUiTestSurface(layout, 300, 300);
        surface.Frame();

        Assert.Equal("+3", label!.Text);
        var overflow = RenderChildren(layout)[^1];
        Assert.Equal(new Rect(200, 100, 100, 100), ((ISkUiView)overflow).Frame); // the sixth tile
        Assert.Contains((ISkUiRenderable)layout.Children[5], RenderChildren(layout)); // the last shown child stays under it

        Tap(layout, new Point(250, 150));
        Assert.Equal(1, tapped);
        Assert.Same(layout.Overflow, Assert.Single(commanded));
        Tap(layout, new Point(50, 50)); // a child's tile: not the overflow
        Assert.Equal(1, tapped);

        layout.Children.RemoveAt(8);
        layout.Children.RemoveAt(7);
        SkUiTestHelpers.Arrange(layout, 300, 300);
        surface.Frame();
        Assert.Equal("+1", label.Text);
        layout.Children.RemoveAt(6);
        SkUiTestHelpers.Arrange(layout, 300, 300);
        surface.Frame();
        Assert.Equal(0, layout.Overflow.HiddenCount);
        Assert.Equal(6, RenderChildren(layout).Count); // everything fits: no overflow tile
    }

    [Fact]
    public void ChildrenLeftOutAndShownAgainAreDrawnAgain()
    {
        var layout = Tiles(6);
        ((SkUiBox)layout.Children[5]).Color = Colors.Blue;
        using var surface = new SkUiTestSurface(layout, 300, 300);
        surface.Frame();
        Assert.Equal(SKColors.Blue, surface.Bitmap.GetPixel(250, 150));

        layout.MaximumRows = 1;
        SkUiTestHelpers.Arrange(layout, 300, 300);
        surface.Frame();
        Assert.Equal(new SKColor(0, 0, 0, 0), surface.Bitmap.GetPixel(250, 150));

        layout.MaximumRows = -1;
        SkUiTestHelpers.Arrange(layout, 300, 300);
        surface.Frame();
        Assert.Equal(SKColors.Blue, surface.Bitmap.GetPixel(250, 150));
    }

    [Fact]
    public void CoreOverflowTileComesFromItsFactory()
    {
        var core = CoreTiles(5).SetMaximumRows(1);
        var counts = new List<int>();
        var tapped = 0;
        core.SetOverflowTemplate(overflow =>
        {
            overflow.PropertyChanged += (_, _) => counts.Add(overflow.HiddenCount);
            return new SkUiCoreBox().SetColor(Colors.Blue);
        });
        core.OverflowTapped += (_, _) => tapped++;
        var host = new SkUiCoreHost().SetContent(core);
        using var surface = new SkUiTestSurface(host, 300, 300);
        surface.Frame();

        Assert.Equal([2], counts);
        Assert.Equal(SKColors.Blue, surface.Bitmap.GetPixel(250, 50)); // the third tile, under the overflow tile
        Assert.Equal(4, RenderChildren(core).Count); // three tiles and the overflow tile
        Tap(host, new Point(250, 50));
        Assert.Equal(1, tapped);

        core.SetMaximumRows(-1);
        SkUiTestHelpers.Arrange(host, 300, 300);
        surface.Frame();
        Assert.Equal([2, 0], counts);
        Assert.Equal(5, RenderChildren(core).Count);
    }

    [Fact]
    public void InvalidOptionsAreRejected()
    {
        var layout = new SkUiTileLayout();
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.SetMaximumRows(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.SetTileSize(0));
        layout.MaximumRows = 0; // bindable: ignored, as MAUI ignores invalid values
        Assert.Equal(-1, layout.MaximumRows);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiCoreTileLayout().SetTileAspectRatio(double.NaN));
    }

    private sealed class MeasureProbe : SkUiBox
    {
        public int Measures { get; private set; }

        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            Measures++;
            return base.MeasureContent(widthConstraint, heightConstraint);
        }
    }
}
