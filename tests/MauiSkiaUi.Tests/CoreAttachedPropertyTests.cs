using System.ComponentModel;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary><see cref="SkUiCoreAttachedProperty{T}"/>: per-node values that Core layouts read from their children.</summary>
public class CoreAttachedPropertyTests
{
    private static readonly SkUiCoreAttachedProperty<int> Weight = new("Weight", typeof(CoreAttachedPropertyTests), 1, validate: value => value > 0);
    private static readonly SkUiCoreAttachedProperty<string?> Note = new("Note", typeof(CoreAttachedPropertyTests), affectsParentMeasure: false);

    private static void Layout(SkUiCoreNode node, double width = 200, double height = 200)
    {
        node.Measure(width, height);
        node.Arrange(new Rect(0, 0, width, height));
    }

    [Fact]
    public void ValuesDefaultSetClearAndValidate()
    {
        var node = new SkUiCoreBox();
        Assert.Equal(1, node.GetValue(Weight));
        Assert.False(node.IsSet(Weight));
        Assert.Null(node.GetValue(Note));

        node.SetValue(Weight, 3).SetValue(Note, "x");
        Assert.Equal(3, node.GetValue(Weight));
        Assert.Equal("x", node.GetValue(Note));
        Assert.True(node.IsSet(Weight));

        node.ClearValue(Weight);
        Assert.Equal(1, node.GetValue(Weight));
        Assert.False(node.IsSet(Weight));
        Assert.Equal("x", node.GetValue(Note));

        // Writing the default is the same as clearing (MAUI would still report it as set).
        node.SetValue(Weight, 4);
        Assert.True(node.IsSet(Weight));
        node.SetValue(Weight, 1);
        Assert.False(node.IsSet(Weight));
        Assert.Equal(1, node.GetValue(Weight));

        Assert.Throws<ArgumentOutOfRangeException>(() => node.SetValue(Weight, 0));
        Assert.Throws<ArgumentException>(() => new SkUiCoreAttachedProperty<int>("Bad", typeof(CoreAttachedPropertyTests), 0, validate: value => value > 0));
        Assert.Equal("CoreAttachedPropertyTests.Weight", Weight.ToString());
    }

    [Fact]
    public void ChangesRaisePropertyChangedOnceWithThePropertyName()
    {
        var node = new SkUiCoreBox();
        var names = new List<string?>();
        ((INotifyPropertyChanged)node).PropertyChanged += (_, args) => names.Add(args.PropertyName);
        node.SetValue(Weight, 1); // the default: no change
        node.SetValue(Weight, 2);
        node.SetValue(Weight, 2);
        node.ClearValue(Weight);
        node.ClearValue(Weight);
        Assert.Equal(["Weight", "Weight"], names);
    }

    [Fact]
    public void RepeatedWritesDoNotAllocate()
    {
        var node = new SkUiCoreBox();
        node.SetValue(Weight, 2);
        node.SetValue(SkUiCoreGrid.RowProperty, 1);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var round = 0; round < 100; round++)
        {
            node.SetValue(Weight, 2 + round % 2);
            node.SetValue(SkUiCoreGrid.RowProperty, round % 3);
            _ = node.GetValue(Weight) + node.GetValue(SkUiCoreGrid.RowProperty);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - allocated);
    }

    [Fact]
    public void GridPlacementIsCarriedByTheChild()
    {
        var grid = new SkUiCoreGrid().SetColumnDefinitions([new(new SkUiCoreGridLength(50)), new(new SkUiCoreGridLength(50))]);
        var child = new FixedCoreNode(new Size(10, 10));
        child.SetValue(SkUiCoreGrid.ColumnProperty, 1); // before adding
        grid.Add(child);
        Layout(grid);
        Assert.Equal(50, child.Frame.X);
        Assert.Equal(new SkUiCoreGridPlacement(0, 1), grid.GetPlacement(child));

        child.SetValue(SkUiCoreGrid.ColumnProperty, 0); // re-measures the parent
        Layout(grid);
        Assert.Equal(0, child.Frame.X);

        grid.SetColumn(child, 1);
        Assert.Equal(1, child.GetValue(SkUiCoreGrid.ColumnProperty));
        Assert.Throws<ArgumentOutOfRangeException>(() => child.SetValue(SkUiCoreGrid.RowSpanProperty, 0));

        // Moving to another grid keeps the placement.
        grid.Remove(child);
        var other = new SkUiCoreGrid().SetColumnDefinitions([new(new SkUiCoreGridLength(30)), new(new SkUiCoreGridLength(30))]);
        other.Add(child);
        Layout(other);
        Assert.Equal(30, child.Frame.X);
        Assert.Throws<ArgumentException>(() => grid.SetPlacement(child, 0, 0));
    }

    [Fact]
    public void GridAddValidatesBeforeWritingThePlacement()
    {
        var first = new SkUiCoreGrid();
        var child = new FixedCoreNode(new Size(10, 10));
        first.Add(child, 0, 0);
        Assert.Throws<InvalidOperationException>(() => new SkUiCoreGrid().Add(child, 2, 3));
        Assert.Equal(new SkUiCoreGridPlacement(0, 0), first.GetPlacement(child));
    }

    [Fact]
    public void AbsoluteChildrenCanBeAddedWithTheirOwnBounds()
    {
        var layout = new SkUiCoreAbsoluteLayout();
        var placed = new FixedCoreNode(new Size(10, 10));
        placed.SetValue(SkUiCoreAbsoluteLayout.LayoutBoundsProperty, new Rect(20, 30, 40, 50));
        var auto = new FixedCoreNode(new Size(12, 14));
        layout.Add(placed).Add(auto);
        Layout(layout);
        Assert.Equal(new Rect(20, 30, 40, 50), placed.Frame);
        Assert.Equal(new Rect(0, 0, 12, 14), auto.Frame); // default: measured size at (0,0)

        layout.SetLayoutBounds(auto, new Rect(0.5, 0, 20, 20), SkUiCoreAbsoluteLayoutFlags.X);
        Layout(layout);
        Assert.Equal(new Rect(90, 0, 20, 20), auto.Frame);
        Assert.Equal(SkUiCoreAbsoluteLayoutFlags.X, auto.GetValue(SkUiCoreAbsoluteLayout.LayoutFlagsProperty));
    }
}
