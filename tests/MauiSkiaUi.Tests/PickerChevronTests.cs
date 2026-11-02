using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary><see cref="SkUiPickerBase.IsChevronVisible"/>: a hidden chevron gives its room back, on both layers and every kind of picker.</summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PickerChevronTests : IDisposable
{
    private static readonly string[] Fruits = ["Apple", "Pear", "Plum", "Fig"];
    private static int _pointer = 9500;

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly SkUiLook _previousLook = SkUiLook.Current;
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previousPresenter = SkUiPopups.Presenter;

    public PickerChevronTests()
    {
        // A look with a chevron (the iOS and Android looks have none).
        SkUiLook.Current = new DefaultSkUiLook();
        SkUiPopups.Presenter = _presenter;
    }

    public void Dispose()
    {
        SkUiPopups.Presenter = _previousPresenter;
        SkUiLook.Current = _previousLook;
        _ui.Dispose();
    }

    private static SkUiVerticalStackLayout Root(SkUiView view)
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        stack.Children.Add(view);
        SkUiTestHelpers.Arrange(stack, 360, 700);
        return stack;
    }

    private static void TapAt(SkUiView root, SkUiView picker, double x, double y)
    {
        var bounds = SkUiDiagnostics.GetRootBounds(picker)!.Value;
        var at = new Point(bounds.X + x, bounds.Y + y);
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    [Fact]
    public void TheChevronIsShownByDefault()
    {
        Assert.True(new SkUiPicker().IsChevronVisible);
        Assert.True(new SkUiCorePicker().IsChevronVisible);
    }

    [Fact]
    public void AHiddenChevronGivesItsRoomBack()
    {
        var picker = new SkUiPicker { ItemsSource = Fruits, SelectedIndex = 0, HorizontalOptions = LayoutOptions.Start };
        var root = Root(picker);
        var with = picker.Width;

        picker.SetIsChevronVisible(false);
        SkUiTestHelpers.Arrange(root, 360, 700);

        Assert.Equal(with - SkUiLook.Current.PickerChevronWidth, picker.Width, 0.5);
    }

    [Fact]
    public void TheClearButtonMovesToTheEndWithoutAChevron()
    {
        var picker = new SkUiPicker { ItemsSource = Fruits, SelectedIndex = 2, IsClearButtonVisible = true, IsChevronVisible = false };
        var root = Root(picker);

        TapAt(root, picker, picker.Width - SkUiLook.Current.PickerClearButtonWidth / 2, picker.Height / 2);
        _ui.RunPending();

        Assert.Equal(-1, picker.SelectedIndex);
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void ASelectedItemTemplateGetsTheChevronsRoom()
    {
        var picker = new SkUiPicker
        {
            ItemsSource = Fruits, SelectedIndex = 1, HorizontalOptions = LayoutOptions.Start,
            SelectedItemTemplate = new DataTemplate(() => new SkUiBox { WidthRequest = 60, HeightRequest = 18 }),
        };
        var root = Root(picker);
        var with = picker.Width;

        picker.IsChevronVisible = false;
        SkUiTestHelpers.Arrange(root, 360, 700);

        Assert.Equal(with - SkUiLook.Current.PickerChevronWidth, picker.Width, 0.5);
    }

    [Fact]
    public void ADatePickerHidesItsChevron()
    {
        var picker = new SkUiDatePicker { Date = new DateTime(2026, 11, 2), HorizontalOptions = LayoutOptions.Start };
        var root = Root(picker);
        var with = picker.Width;

        picker.IsChevronVisible = false;
        SkUiTestHelpers.Arrange(root, 360, 700);

        Assert.Equal(with - SkUiLook.Current.PickerChevronWidth, picker.Width, 0.5);
    }

    [Fact]
    public void ACorePickerHidesItsChevron()
    {
        var core = new SkUiCorePicker().SetItemsSource(Fruits).SetSelectedIndex(0);
        var with = core.Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
        var changed = new List<string?>();
        core.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        core.SetIsChevronVisible(false);

        Assert.False(core.IsChevronVisible);
        Assert.Contains(nameof(SkUiCorePicker.IsChevronVisible), changed);
        Assert.Equal(with - SkUiLook.Current.PickerChevronWidth, core.Measure(double.PositiveInfinity, double.PositiveInfinity).Width, 0.5);
    }
}
