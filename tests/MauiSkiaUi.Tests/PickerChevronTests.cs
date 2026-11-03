using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// <see cref="SkUiPickerBase.IsChevronVisible"/>: the look's chevron by default, a hidden one gives its room back, a shown one appears
/// also on looks without one; on both layers and every kind of picker.
/// </summary>
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
    public void TheLookDecidesByDefault()
    {
        Assert.Null(new SkUiPicker().IsChevronVisible);
        Assert.Null(new SkUiCorePicker().IsChevronVisible);
    }

    [Fact]
    public void TheLooksSayWhetherFieldsShowAChevron()
    {
        Assert.True(new DefaultSkUiLook().PickerShowsChevron);
        Assert.True(new WindowsSkUiLook().PickerShowsChevron);
        Assert.False(new IosSkUiLook().PickerShowsChevron);
        Assert.False(new MacSkUiLook().PickerShowsChevron);
        Assert.False(new AndroidSkUiLook().PickerShowsChevron);
        // As in 1.0, a look without one keeps no room for it.
        Assert.Equal(0, new IosSkUiLook().PickerChevronWidth);
        Assert.Equal(0, new AndroidSkUiLook().PickerChevronWidth);
    }

    [Fact]
    public void ALookThatKeepsRoomForAChevronShowsOne()
    {
        // A look derived from one without a chevron that gives it room (1.0's way) shows it, in that room.
        SkUiLook.Current = new IosSkUiLook();
        var picker = new SkUiPicker { ItemsSource = Fruits, SelectedIndex = 0, HorizontalOptions = LayoutOptions.Start };
        Root(picker);
        var without = picker.Width;

        SkUiLook.Current = new NarrowChevronLook();
        Assert.True(SkUiLook.Current.PickerShowsChevron);
        var with = new SkUiPicker { ItemsSource = Fruits, SelectedIndex = 0, HorizontalOptions = LayoutOptions.Start };
        Root(with);
        Assert.Equal(without + NarrowChevronLook.Width, with.Width, 0.5);
    }

    [Fact]
    public void APickerShowsAChevronOnALookWithout()
    {
        SkUiLook.Current = new MacSkUiLook();
        var picker = new SkUiPicker { ItemsSource = Fruits, SelectedIndex = 0, HorizontalOptions = LayoutOptions.Start };
        var root = Root(picker);
        var without = picker.Width;

        picker.IsChevronVisible = true;
        SkUiTestHelpers.Arrange(root, 360, 700);
        // The look keeps no room for one: the base look's.
        Assert.Equal(without + SkUiLook.DefaultPickerChevronWidth, picker.Width, 0.5);

        picker.IsChevronVisible = null; // the look's again: none
        SkUiTestHelpers.Arrange(root, 360, 700);
        Assert.Equal(without, picker.Width, 0.5);
    }

    [Fact]
    public void ALookWithoutAChevronKeepsNoRoom()
    {
        SkUiPicker NewPicker() => new() { ItemsSource = Fruits, SelectedIndex = 0, HorizontalOptions = LayoutOptions.Start };
        var with = NewPicker();
        Root(with);

        SkUiLook.Current = new HiddenChevronLook();
        var without = NewPicker();
        Root(without);

        Assert.Equal(with.Width - SkUiLook.Current.PickerChevronWidth, without.Width, 0.5);
    }

    [Fact]
    public void ACorePickerShowsAChevronOnALookWithout()
    {
        SkUiLook.Current = new AndroidSkUiLook();
        var core = new SkUiCorePicker().SetItemsSource(Fruits).SetSelectedIndex(0);
        var without = core.Measure(double.PositiveInfinity, double.PositiveInfinity).Width;

        core.SetIsChevronVisible(true);

        Assert.Equal(without + SkUiLook.DefaultPickerChevronWidth, core.Measure(double.PositiveInfinity, double.PositiveInfinity).Width, 0.5);
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

    /// <summary>The default look without the chevron.</summary>
    private sealed class HiddenChevronLook : DefaultSkUiLook
    {
        public override bool PickerShowsChevron => false;
    }

    /// <summary>The iOS look with a narrow chevron, through its width alone.</summary>
    private sealed class NarrowChevronLook : IosSkUiLook
    {
        public const double Width = 24;

        public override double PickerChevronWidth => Width;
    }
}

/// <summary><see cref="SkUiPickerBase.IsChevronVisible"/> from XAML (a nullable bool).</summary>
[Collection(RuntimeXamlCollection.Name)]
public sealed class PickerChevronXamlTests
{
    [Fact]
    public void XamlSetsTheChevron()
    {
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiVerticalStackLayout>
                <sk:SkUiPicker IsChevronVisible="True" />
                <sk:SkUiDatePicker IsChevronVisible="False" />
                <sk:SkUiPicker />
              </sk:SkUiVerticalStackLayout>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var pickers = ((SkUiVerticalStackLayout)root.Content).Children.Cast<SkUiPickerBase>().ToArray();
        Assert.Equal([true, false, null], pickers.Select(picker => picker.IsChevronVisible));
    }
}
