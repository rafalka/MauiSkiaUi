using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Layouts;
using MauiSkiaUi.CommunityToolkit;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.10 L1–L3: the layout extension points made public, and the Community Toolkit's dock and uniform items layouts drawn on
/// the toolkit's own layout managers (SkiaUi.Maui.CommunityToolkit).
/// </summary>
public class ToolkitLayoutTests
{
    private const double Tolerance = 0.001;

    /// <summary>A drawn child of a given size that can change it.</summary>
    private sealed class SizedView(double width, double height) : SkUiView
    {
        private Size _natural = new(width, height);

        public void Resize(double newWidth, double newHeight)
        {
            _natural = new Size(newWidth, newHeight);
            InvalidateMeasure();
        }

        protected override Size MeasureContent(double widthConstraint, double heightConstraint) => _natural;
    }

    /// <summary>A layout of its own on the public extension points: a child's attached <c>Grid.Row</c> re-measures it.</summary>
    private sealed class CountingLayout : SkUiLayout
    {
        public int ChildrenChanges { get; private set; }
        public int Measures { get; private set; }

        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            Measures++;
            return base.MeasureContent(widthConstraint, heightConstraint);
        }

        protected override bool AffectsChildLayout(string? propertyName) => propertyName == Grid.RowProperty.PropertyName;

        protected override void OnChildrenChanged() => ChildrenChanges++;
    }

    private static void AssertFrame(Rect expected, Rect actual)
    {
        Assert.Equal(expected.X, actual.X, Tolerance);
        Assert.Equal(expected.Y, actual.Y, Tolerance);
        Assert.Equal(expected.Width, actual.Width, Tolerance);
        Assert.Equal(expected.Height, actual.Height, Tolerance);
    }

    // ---- L1 ----

    [Fact]
    public void A_layout_of_its_own_is_told_of_children_changes_and_re_measures_on_its_attached_properties()
    {
        var layout = new CountingLayout();
        var first = new SizedView(10, 10);
        var second = new SizedView(10, 10);

        layout.Children.Add(first);
        layout.Children.Add(second);
        layout.Children[1] = new SizedView(5, 5);
        layout.Children.RemoveAt(1);
        layout.Children.Clear();
        Assert.Equal(5, layout.ChildrenChanges);

        layout.Children.Add(first);
        SkUiTestHelpers.Arrange(layout, 100, 100);
        var measures = layout.Measures;
        SkUiTestHelpers.Arrange(layout, 100, 100);
        Assert.Equal(measures, layout.Measures); // cached

        Grid.SetRow(first, 1);
        SkUiTestHelpers.Arrange(layout, 100, 100);
        Assert.Equal(measures + 1, layout.Measures);

        Grid.SetColumn(first, 1); // not listed: no new measure
        SkUiTestHelpers.Arrange(layout, 100, 100);
        Assert.Equal(measures + 1, layout.Measures);
    }

    // ---- L2: dock layout ----

    private static (SkUiDockLayout Dock, SizedView Top, SizedView Bottom, SizedView Left, SizedView Right, SizedView Fill) Docked()
    {
        var dock = new SkUiDockLayout();
        var top = new SizedView(10, 50);
        var bottom = new SizedView(10, 30);
        var left = new SizedView(40, 10);
        var right = new SizedView(20, 10);
        var fill = new SizedView(10, 10);
        dock.Add(top, DockPosition.Top);
        dock.Add(bottom, DockPosition.Bottom);
        dock.Add(left, DockPosition.Left);
        dock.Add(right, DockPosition.Right);
        dock.Children.Add(fill);
        return (dock, top, bottom, left, right, fill);
    }

    [Fact]
    public void Dock_children_take_their_sides_in_order_and_the_last_fills_the_rest()
    {
        var (dock, top, bottom, left, right, fill) = Docked();
        SkUiTestHelpers.Arrange(dock, 300, 200);

        AssertFrame(new Rect(0, 0, 300, 50), top.Frame);
        AssertFrame(new Rect(0, 170, 300, 30), bottom.Frame);
        AssertFrame(new Rect(0, 50, 40, 120), left.Frame);
        AssertFrame(new Rect(280, 50, 20, 120), right.Frame);
        AssertFrame(new Rect(40, 50, 240, 120), fill.Frame);
    }

    [Fact]
    public void Dock_measures_the_docked_sides_and_the_last_child()
    {
        var (dock, _, _, _, _, _) = Docked();
        var size = ((IView)dock).Measure(300, 200);

        // Heights of the top and bottom children, widths of the left and right ones, and the undocked last child's both.
        Assert.Equal(70, size.Width, Tolerance);
        Assert.Equal(90, size.Height, Tolerance);
    }

    [Fact]
    public void Dock_spacing_follows_each_docked_child()
    {
        var (dock, top, bottom, left, right, fill) = Docked();
        dock.HorizontalSpacing = 5;
        dock.VerticalSpacing = 10;
        SkUiTestHelpers.Arrange(dock, 300, 200);

        AssertFrame(new Rect(0, 0, 300, 50), top.Frame);
        AssertFrame(new Rect(0, 170, 300, 30), bottom.Frame);
        AssertFrame(new Rect(0, 60, 40, 100), left.Frame);
        AssertFrame(new Rect(280, 60, 20, 100), right.Frame);
        AssertFrame(new Rect(45, 60, 230, 100), fill.Frame);
    }

    [Fact]
    public void Dock_padding_insets_the_children_and_the_last_child_docks_without_ShouldExpandLastChild()
    {
        var (dock, top, _, left, _, fill) = Docked();
        dock.Padding = new Thickness(10);
        dock.ShouldExpandLastChild = false;
        SkUiTestHelpers.Arrange(dock, 300, 200);

        AssertFrame(new Rect(10, 10, 280, 50), top.Frame);
        AssertFrame(new Rect(10, 60, 40, 100), left.Frame);
        AssertFrame(new Rect(50, 60, 220, 10), fill.Frame); // DockPosition.None docks as Top
    }

    [Fact]
    public void Dock_skips_hidden_children()
    {
        var (dock, top, _, left, _, _) = Docked();
        top.IsVisible = false;
        SkUiTestHelpers.Arrange(dock, 300, 200);

        AssertFrame(new Rect(0, 0, 40, 170), left.Frame);
    }

    [Fact]
    public void A_dock_position_changed_later_lays_the_dock_out_again()
    {
        var (dock, _, _, left, _, fill) = Docked();
        SkUiTestHelpers.Arrange(dock, 300, 200);

        DockLayout.SetDockPosition(left, DockPosition.Right); // the toolkit's own attached property
        SkUiTestHelpers.Arrange(dock, 300, 200);

        Assert.Equal(DockPosition.Right, SkUiDockLayout.GetDockPosition(left));
        AssertFrame(new Rect(260, 50, 40, 120), left.Frame);
        AssertFrame(new Rect(0, 50, 240, 120), fill.Frame);
    }

    [Fact]
    public void Dock_mirrors_right_to_left()
    {
        var (dock, _, _, left, right, fill) = Docked();
        var root = new SkUiContentView { FlowDirection = FlowDirection.RightToLeft, Content = dock };
        SkUiTestHelpers.Arrange(root, 300, 200);

        AssertFrame(new Rect(260, 50, 40, 120), left.Frame);
        AssertFrame(new Rect(0, 50, 20, 120), right.Frame);
        AssertFrame(new Rect(20, 50, 240, 120), fill.Frame);
    }

    [Fact]
    public void Dock_spacing_is_validated()
    {
        var dock = new SkUiDockLayout();
        Assert.Throws<ArgumentOutOfRangeException>(() => dock.SetHorizontalSpacing(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => dock.SetVerticalSpacing(double.NaN));

        dock.HorizontalSpacing = 4;
        dock.HorizontalSpacing = -1; // ignored, as MAUI ignores invalid bindable values
        Assert.Equal(4, dock.HorizontalSpacing);
        Assert.Same(DockLayout.DockPositionProperty, SkUiDockLayout.DockPositionProperty);
    }

    // ---- L3: uniform items layout ----

    private static (SkUiUniformItemsLayout Layout, List<SizedView> Items) Uniform(int count)
    {
        var layout = new SkUiUniformItemsLayout();
        var items = new List<SizedView>();
        for (var index = 0; index < count; index++)
        {
            var item = new SizedView(50, 40);
            items.Add(item);
            layout.Children.Add(item);
        }
        return (layout, items);
    }

    [Fact]
    public void Uniform_cells_take_the_first_childs_size_and_share_the_width()
    {
        var (layout, items) = Uniform(7);
        var size = ((IView)layout).Measure(230, 1000);
        Assert.Equal(200, size.Width, Tolerance); // four columns of 50
        Assert.Equal(80, size.Height, Tolerance); // two rows of 40

        ((IView)layout).Arrange(new Rect(0, 0, 230, 1000));
        for (var index = 0; index < items.Count; index++)
            AssertFrame(new Rect(index % 4 * 57.5, index / 4 * 40, 57.5, 40), items[index].Frame);
    }

    [Fact]
    public void Uniform_max_columns_and_padding()
    {
        var (layout, items) = Uniform(7);
        layout.MaxColumns = 3;
        layout.Padding = new Thickness(10);
        SkUiTestHelpers.Arrange(layout, 230, 1000);

        var cell = 210 / 3d;
        AssertFrame(new Rect(10, 10, cell, 40), items[0].Frame);
        AssertFrame(new Rect(10 + 2 * cell, 10, cell, 40), items[2].Frame);
        AssertFrame(new Rect(10, 90, cell, 40), items[6].Frame);
    }

    [Fact]
    public void Uniform_children_past_max_rows_are_arranged_empty()
    {
        var (layout, items) = Uniform(7);
        layout.MaxRows = 1;
        var size = ((IView)layout).Measure(230, 1000);
        Assert.Equal(40, size.Height, Tolerance);

        ((IView)layout).Arrange(new Rect(0, 0, 230, 1000));
        AssertFrame(new Rect(3 * 57.5, 0, 57.5, 40), items[3].Frame);
        for (var index = 4; index < items.Count; index++)
            Assert.Equal(Size.Zero, items[index].Frame.Size);

        layout.MaxRows = 2; // shown again
        SkUiTestHelpers.Arrange(layout, 230, 1000);
        AssertFrame(new Rect(0, 40, 57.5, 40), items[4].Frame);
    }

    [Fact]
    public void Uniform_cells_follow_the_first_child_when_it_changes_size()
    {
        var (layout, items) = Uniform(4);
        SkUiTestHelpers.Arrange(layout, 230, 1000);
        Assert.Equal(57.5, items[0].Frame.Width, Tolerance);

        items[0].Resize(100, 60); // the toolkit's manager alone would keep 50 × 40
        SkUiTestHelpers.Arrange(layout, 230, 1000);

        AssertFrame(new Rect(115, 0, 115, 60), items[1].Frame);
        AssertFrame(new Rect(0, 60, 115, 60), items[2].Frame);
    }

    [Fact]
    public void Uniform_skips_hidden_children_and_follows_added_ones()
    {
        var (layout, items) = Uniform(4);
        items[0].IsVisible = false;
        SkUiTestHelpers.Arrange(layout, 230, 1000);
        AssertFrame(new Rect(0, 0, 230 / 3d, 40), items[1].Frame);

        var added = new SizedView(50, 40);
        layout.Children.Add(added);
        SkUiTestHelpers.Arrange(layout, 230, 1000);
        AssertFrame(new Rect(3 * 57.5, 0, 57.5, 40), added.Frame);
    }

    [Fact]
    public void Uniform_mirrors_right_to_left()
    {
        var (layout, items) = Uniform(4);
        var root = new SkUiContentView { FlowDirection = FlowDirection.RightToLeft, Content = layout };
        SkUiTestHelpers.Arrange(root, 230, 1000);

        AssertFrame(new Rect(230 - 57.5, 0, 57.5, 40), items[0].Frame);
    }

    [Fact]
    public void Uniform_limits_are_validated()
    {
        var layout = new SkUiUniformItemsLayout();
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.SetMaxRows(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.SetMaxColumns(-1));

        layout.MaxColumns = 2;
        layout.MaxColumns = 0; // ignored
        Assert.Equal(2, layout.MaxColumns);
        Assert.Equal(int.MaxValue, layout.MaxRows);
    }
}
