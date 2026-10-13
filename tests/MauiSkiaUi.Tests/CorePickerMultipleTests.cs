using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>The Core twins of the multi-select picker and the multi-column picker, on the same engine as the SkUi ones.</summary>
[Collection(GlobalStateCollection.Name)]
public sealed class CorePickerMultipleTests : IDisposable
{
    private static readonly string[] Monkeys = ["Baboon", "Capuchin Monkey", "Blue Monkey", "Squirrel Monkey"];

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;

    public CorePickerMultipleTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _ui.Dispose();
    }

    private static SkUiVerticalStackLayout Host(SkUiCoreNode node)
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        stack.Children.Add(new SkUiCoreHost().SetContent(node));
        SkUiTestHelpers.Arrange(stack, 360, 700);
        return stack;
    }

    private static string? FieldText(SkUiVerticalStackLayout root, SkUiCoreNode node) =>
        SkUiSemanticsTree.Build(root).Flatten().Single(element => ReferenceEquals(element.Source, node)).Value;

    [Fact]
    public void TheCorePickerChoosesSeveralItems()
    {
        var core = new SkUiCorePicker { Title = "Monkeys", SelectionMode = SkUiSelectionMode.Multiple };
        foreach (var monkey in Monkeys)
            core.Items.Add(monkey);
        var root = Host(core);
        var changes = new List<int>();
        core.SelectionChanged += (_, args) => changes.Add(args.CurrentSelection.Count);

        core.SetSelectedItems(["Blue Monkey", "Baboon"]);
        Assert.Equal([0, 2], core.SelectedIndices);
        Assert.Equal("Baboon, Blue Monkey", FieldText(root, core));

        core.IsOpen = true;
        var request = Assert.IsType<SkUiMultiListPopupRequest>(_presenter.Current);
        Assert.Equal([0, 2], request.SelectedIndices);
        Assert.True(request.SetSelected(3, true));
        Assert.True(core.IsOpen);
        Assert.Equal([0, 2, 3], core.SelectedIndices);

        core.SetSelectedCountFormat("{0} chosen");
        Assert.Equal("3 chosen", FieldText(root, core));
        _presenter.Dismiss();
        _ui.RunPending();
        Assert.False(core.IsOpen);
        Assert.Equal([2, 3], changes);
        Assert.Equal(-1, core.SelectedIndex);

        core.Items.Remove("Baboon");
        Assert.Equal([1, 2], core.SelectedIndices);
        core.SelectionMode = SkUiSelectionMode.Single;
        Assert.Equal(1, core.SelectedIndex);
        Assert.Empty(core.SelectedItems);
    }

    [Fact]
    public void TheCoreColumnsPickerChoosesWithWheels()
    {
        var core = new SkUiCoreMultiColumnPicker { Title = "Time", Separator = ":" };
        core.AddColumn(new SkUiPickerColumn { ItemsSource = new[] { "09", "10", "11" } });
        core.AddColumn(new SkUiPickerColumn { ItemsSource = new[] { "00", "30" }, Loop = true });
        var root = Host(core);
        var changes = new List<(int, int)>();
        core.SelectionChanged += (_, args) => changes.Add((args.ColumnIndex, args.CurrentIndex));

        core.IsOpen = true;
        var request = Assert.IsType<SkUiColumnsPopupRequest>(_presenter.Current);
        var content = Assert.IsType<SkUiColumnsPopup>(request.Content);
        content.Wheel(0).Position = 2;
        Assert.True(request.SetSelectedIndex(1, 1));

        Assert.Equal([2, 1], core.SelectedIndices);
        Assert.Equal([(0, 2), (1, 1)], changes);
        Assert.Equal("11:30", FieldText(root, core));
        core.Columns.Clear();
        _ui.RunPending();
        Assert.False(core.IsOpen);
    }

    [Fact]
    public void TheCoreAndTheSkUiColumnsPickerDrawTheSameField()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var skia = new SkUiMultiColumnPicker { Title = "Time", Separator = ":", FontFamily = SkUiTestHelpers.BundledFontFamily };
        var core = new SkUiCoreMultiColumnPicker { Title = "Time", Separator = ":", FontFamily = SkUiTestHelpers.BundledFontFamily };
        foreach (var picker in new[] { (Action<SkUiPickerColumn>)(skia.Columns.Add), core.Columns.Add })
        {
            picker(new SkUiPickerColumn { ItemsSource = new[] { "09", "10" }, SelectedIndex = 1 });
            picker(new SkUiPickerColumn { ItemsSource = new[] { "00", "30" }, SelectedIndex = 1 });
        }
        SkUiTestHelpers.Arrange(skia, 200, 44);
        SkUiTestHelpers.Arrange(new SkUiCoreHost().SetContent(core), 200, 44);

        using var skiaBitmap = Render(skia.Paint);
        using var coreBitmap = Render(core.Paint);

        Assert.Equal(skiaBitmap.Bytes, coreBitmap.Bytes);
        Assert.Contains(skiaBitmap.Pixels, pixel => pixel.Alpha > 0);
    }

    private static SKBitmap Render(Action<SKCanvas> paint)
    {
        var bitmap = new SKBitmap(200, 44);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        paint(canvas);
        return bitmap;
    }
}
