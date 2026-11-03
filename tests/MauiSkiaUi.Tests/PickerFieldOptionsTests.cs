using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// A picker field's own padding (<see cref="SkUiPickerBase.TextPadding"/>), chevron (<see cref="SkUiPickerBase.ChevronColor"/>,
/// <see cref="SkUiPickerBase.ChevronSize"/>, <see cref="SkUiPickerBase.ChevronAreaWidth"/>) and leading view
/// (<see cref="SkUiPickerBase.LeadingView"/>), on both layers.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PickerFieldOptionsTests : IDisposable
{
    private static readonly string[] Fruits = ["Apple", "Pear", "Plum", "Fig"];

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly SkUiLook _previousLook = SkUiLook.Current;
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previousPresenter = SkUiPopups.Presenter;

    public PickerFieldOptionsTests()
    {
        // A look with a chevron.
        SkUiLook.Current = new DefaultSkUiLook();
        SkUiPopups.Presenter = _presenter;
    }

    public void Dispose()
    {
        SkUiPopups.Presenter = _previousPresenter;
        SkUiLook.Current = _previousLook;
        _ui.Dispose();
    }

    private static SkUiVerticalStackLayout Root(SkUiView view, bool rightToLeft = false)
    {
        var stack = new SkUiVerticalStackLayout
        {
            Padding = new Thickness(10, 20), FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight
        };
        stack.Children.Add(view);
        SkUiTestHelpers.Arrange(stack, 360, 700);
        return stack;
    }

    private static SkUiPicker NewPicker() => new() { ItemsSource = Fruits, SelectedIndex = 0, HorizontalOptions = LayoutOptions.Start, MinimumHeightRequest = 0 };

    private static Size MeasureCore(SkUiCorePicker core) => core.Measure(double.PositiveInfinity, double.PositiveInfinity);

    #region TextPadding

    [Fact]
    public void TextPaddingReplacesTheLooks()
    {
        var look = SkUiLook.Current.PickerFieldPadding;
        var picker = NewPicker();
        var root = Root(picker);
        var (width, height) = (picker.Width, picker.Height);
        Assert.Null(picker.TextPadding);

        picker.SetTextPadding(new Thickness(30, 2));
        SkUiTestHelpers.Arrange(root, 360, 700);

        Assert.Equal(width + 60 - look.HorizontalThickness, picker.Width, 0.5);
        Assert.Equal(height + 4 - look.VerticalThickness, picker.Height, 0.5);

        picker.TextPadding = null; // the look's again
        SkUiTestHelpers.Arrange(root, 360, 700);
        Assert.Equal(width, picker.Width, 0.5);
    }

    [Fact]
    public void TextPaddingPlacesTheSelectedItemTemplate()
    {
        var picker = new SkUiPicker
        {
            ItemsSource = Fruits, SelectedIndex = 1, TextPadding = new Thickness(16, 8),
            SelectedItemTemplate = new DataTemplate(() => new SkUiBox { WidthRequest = 60, HeightRequest = 18, HorizontalOptions = LayoutOptions.Start })
        };
        Root(picker);

        Assert.Equal(16, picker.FieldView!.Frame.Left, 0.5);
    }

    [Fact]
    public void ACorePickerTakesTextPadding()
    {
        var core = new SkUiCorePicker().SetItemsSource(Fruits).SetSelectedIndex(0);
        var width = MeasureCore(core).Width;
        var changed = new List<string?>();
        core.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        core.SetTextPadding(new Thickness(30, 8));

        Assert.Equal(new Thickness(30, 8), core.TextPadding);
        Assert.Contains(nameof(SkUiCorePicker.TextPadding), changed);
        Assert.Equal(width + 60 - SkUiLook.Current.PickerFieldPadding.HorizontalThickness, MeasureCore(core).Width, 0.5);
    }

    [Fact]
    public void InvalidPaddingThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiPicker().SetTextPadding(new Thickness(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiCorePicker().SetTextPadding(new Thickness(double.NaN)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiPicker().SetChevronSize(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiCorePicker().SetChevronAreaWidth(double.PositiveInfinity));
    }

    #endregion

    #region Chevron

    [Fact]
    public void ChevronAreaWidthSetsTheChevronsRoom()
    {
        var picker = NewPicker();
        var root = Root(picker);
        var width = picker.Width;

        picker.ChevronAreaWidth = 12;
        SkUiTestHelpers.Arrange(root, 360, 700);
        Assert.Equal(width - SkUiLook.Current.PickerChevronWidth + 12, picker.Width, 0.5);

        // A hidden chevron keeps no room.
        picker.IsChevronVisible = false;
        SkUiTestHelpers.Arrange(root, 360, 700);
        Assert.Equal(width - SkUiLook.Current.PickerChevronWidth, picker.Width, 0.5);
    }

    [Fact]
    public void ACorePickerTakesTheChevronsRoom()
    {
        var core = new SkUiCorePicker().SetItemsSource(Fruits).SetSelectedIndex(0);
        var width = MeasureCore(core).Width;

        core.SetChevronAreaWidth(20);

        Assert.Equal(20, core.ChevronAreaWidth);
        Assert.Equal(width - SkUiLook.Current.PickerChevronWidth + 20, MeasureCore(core).Width, 0.5);
    }

    [Fact]
    public void TheLookGetsTheChevronsColorAndSize()
    {
        var look = new DefaultSkUiLook();
        var paints = new List<SkUiPickerFieldPaint>();
        look.PickerFieldPainter = (_, paint) => paints.Add(paint);
        SkUiLook.Current = look;
        var picker = new SkUiPicker { ItemsSource = Fruits, ChevronAreaWidth = 24 };
        var root = Root(picker);
        using var surface = new SkUiTestSurface(root, 360, 200);

        surface.Frame();
        Assert.Null(paints[^1].ChevronColor);
        Assert.Null(paints[^1].ChevronSize);
        Assert.Equal(24, paints[^1].ChevronBounds.Width, 0.5);

        picker.SetChevronColor(Colors.Red).SetChevronSize(8);
        surface.Frame();
        Assert.Equal(SKColors.Red, paints[^1].ChevronColor);
        Assert.Equal(8, paints[^1].ChevronSize);
    }

    [Fact]
    public void TheChevronIsDrawnInItsColor()
    {
        bool HasRed(SkUiTestSurface surface, SkUiPicker picker)
        {
            var bounds = SkUiDiagnostics.GetRootBounds(picker)!.Value;
            for (var x = (int)(bounds.Right - SkUiLook.Current.PickerChevronWidth); x < (int)bounds.Right; x++)
                for (var y = (int)bounds.Top; y < (int)bounds.Bottom; y++)
                    if (surface.Bitmap.GetPixel(x, y) is { Red: > 200, Green: < 60, Blue: < 60 })
                        return true;
            return false;
        }

        var picker = new SkUiPicker { ItemsSource = Fruits };
        var root = Root(picker);
        root.BackgroundColor = Colors.White;
        using var surface = new SkUiTestSurface(root, 360, 200);
        surface.Frame();
        Assert.False(HasRed(surface, picker));

        picker.ChevronColor = Colors.Red;
        surface.Frame();
        Assert.True(HasRed(surface, picker));
    }

    [Fact]
    public void ACorePickerTakesTheChevronsColorAndSize()
    {
        var core = new SkUiCorePicker().SetChevronColor(Colors.Gray).SetChevronSize(6);

        Assert.Equal(Colors.Gray, core.ChevronColor);
        Assert.Equal(6, core.ChevronSize);
    }

    #endregion

    #region LeadingView

    [Fact]
    public void TheLeadingViewSitsBeforeTheText()
    {
        var look = SkUiLook.Current;
        var picker = NewPicker();
        var root = Root(picker);
        var width = picker.Width;
        var icon = new SkUiBox { WidthRequest = 20, HeightRequest = 14 };

        picker.LeadingView = icon;
        SkUiTestHelpers.Arrange(root, 360, 700);

        Assert.Same(picker, icon.Parent);
        Assert.Equal(width + 20 + look.TextFieldAdornmentSpacing, picker.Width, 0.5);
        Assert.Equal(look.PickerFieldPadding.Left, icon.Frame.Left, 0.5);
        Assert.Equal(20, icon.Frame.Width, 0.5);
        // Centered as the text is.
        Assert.Equal(picker.Height / 2, icon.Frame.Center.Y, 0.5);

        picker.LeadingView = null;
        SkUiTestHelpers.Arrange(root, 360, 700);
        Assert.Null(icon.Parent);
        Assert.Equal(width, picker.Width, 0.5);
    }

    [Fact]
    public void TheFieldIsAsTallAsTheLeadingView()
    {
        var picker = NewPicker().SetLeadingView(new SkUiBox { WidthRequest = 20, HeightRequest = 70 });
        Root((SkUiView)picker);

        Assert.Equal(70, ((SkUiView)picker).Height, 0.5);
    }

    [Fact]
    public void TheLeadingViewComesBeforeTheSelectedItemTemplate()
    {
        var look = SkUiLook.Current;
        var icon = new SkUiBox { WidthRequest = 20, HeightRequest = 14 };
        var picker = new SkUiPicker
        {
            ItemsSource = Fruits, SelectedIndex = 1, LeadingView = icon,
            SelectedItemTemplate = new DataTemplate(() => new SkUiBox { WidthRequest = 60, HeightRequest = 18, HorizontalOptions = LayoutOptions.Start })
        };
        Root(picker);

        Assert.Equal(look.PickerFieldPadding.Left + 20 + look.TextFieldAdornmentSpacing, picker.FieldView!.Frame.Left, 0.5);
        var children = new List<Rendering.ISkUiRenderable>();
        ((Rendering.ISkUiRenderable)picker).GetRenderChildren(children);
        Assert.Equal([icon, picker.FieldView], children.Cast<object>());
        Assert.Equal([icon, picker.FieldView], picker.SkiaChildren.Cast<object>());
    }

    [Fact]
    public void RightToLeftPutsTheLeadingViewAtTheRight()
    {
        var icon = new SkUiBox { WidthRequest = 20, HeightRequest = 14 };
        var picker = NewPicker().SetLeadingView(icon);
        Root((SkUiView)picker, rightToLeft: true);

        var bounds = SkUiDiagnostics.GetRootBounds(icon)!.Value;
        var field = SkUiDiagnostics.GetRootBounds((SkUiView)picker)!.Value;
        Assert.Equal(field.Right - SkUiLook.Current.PickerFieldPadding.Left, bounds.Right, 0.5);
    }

    [Fact]
    public void ATapOnTheLeadingViewOpensThePicker()
    {
        var icon = new SkUiBox { WidthRequest = 20, HeightRequest = 14 };
        var picker = (SkUiPicker)NewPicker().SetLeadingView(icon);
        Root(picker);

        Assert.NotNull(SkUiDiagnostics.SimulateTap(icon));

        Assert.True(picker.IsOpen);
    }

    [Fact]
    public void AViewWithAParentCannotLead()
    {
        var icon = new SkUiBox();
        _ = new SkUiPicker { LeadingView = icon };

        Assert.ThrowsAny<Exception>(() => new SkUiPicker { LeadingView = icon });
    }

    [Fact]
    public void ACorePickerShowsALeadingNode()
    {
        var look = SkUiLook.Current;
        var core = new SkUiCorePicker().SetItemsSource(Fruits).SetSelectedIndex(0);
        var width = MeasureCore(core).Width;
        var flag = new SkUiCoreBox().SetColor(Colors.Orange);
        flag.SetWidth(16).SetHeight(16);
        var changed = new List<string?>();
        core.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        core.SetLeadingView(flag);
        var stack = new SkUiVerticalStackLayout();
        stack.Children.Add(new SkUiCoreHost { HorizontalOptions = LayoutOptions.Start }.SetContent(core));
        SkUiTestHelpers.Arrange(stack, 360, 200);

        Assert.Same(core, flag.Parent);
        Assert.Contains(nameof(SkUiCorePicker.LeadingView), changed);
        Assert.Equal(width + 16 + look.TextFieldAdornmentSpacing, core.Frame.Width, 0.5);
        Assert.Equal(look.PickerFieldPadding.Left, flag.Frame.Left, 0.5);
        Assert.Equal(core.Frame.Height / 2, flag.Frame.Center.Y, 0.5);

        // A tap on it opens the picker.
        Assert.NotNull(SkUiDiagnostics.SimulateTap(flag));
        Assert.True(core.IsOpen);
        core.IsOpen = false;

        // Another node takes its place: the first one leaves.
        var other = new SkUiCoreBox();
        core.LeadingView = other;
        Assert.Null(flag.Parent);
        Assert.Same(core, other.Parent);
        Assert.Throws<InvalidOperationException>(() => new SkUiCorePicker().SetLeadingView(other));
    }

    #endregion

    #region Popup rows

    /// <summary>An item whose templated row is <see cref="Height"/> tall and <see cref="Width"/> wide.</summary>
    public sealed record Sized(string Name, double Width, double Height);

    private static DataTemplate SizedRow() => new(() =>
    {
        var box = new SkUiBox();
        box.SetBinding(VisualElement.WidthRequestProperty, nameof(Sized.Width));
        box.SetBinding(VisualElement.HeightRequestProperty, nameof(Sized.Height));
        return box;
    });

    private SkUiPopupRequest Open(SkUiPicker picker)
    {
        var root = new SkUiVerticalStackLayout { Children = { new SkUiHorizontalStackLayout { Children = { picker } } } };
        SkUiTestHelpers.Arrange(root, 600, 900);
        picker.IsOpen = true;
        return _presenter.Current!;
    }

    [Fact]
    public void ThePopupIsAsTallAsEveryTemplatedRow()
    {
        List<Sized> items = [new("A", 100, 30), new("B", 100, 50), new("C", 100, 70)];
        var picker = new SkUiPicker
        {
            ItemsSource = items, ItemDisplayBinding = new Binding(nameof(Sized.Name)), PopupPlacement = SkUiPopupPlacement.Anchored, ItemTemplate = SizedRow()
        };

        Assert.Equal(150, Open(picker).PreferredSize.Height, 0.5);
    }

    [Fact]
    public void ALaterWiderRowWidensThePopup()
    {
        List<Sized> items = [new("A", 100, 30), new("B", 380, 30)];
        var picker = new SkUiPicker
        {
            ItemsSource = items, ItemDisplayBinding = new Binding(nameof(Sized.Name)), PopupPlacement = SkUiPopupPlacement.Anchored, ItemTemplate = SizedRow()
        };

        Assert.Equal(380, Open(picker).PreferredSize.Width, 0.5);
    }

    [Fact]
    public void RowsPastTheMeasuredOnesCountAsTheirAverage()
    {
        // At most 50 rows are measured (fewer once they fill the window): the rows after them count as tall as those, not as they are.
        var items = Enumerable.Range(0, 100).Select(index => new Sized($"Item {index}", 100, index >= 50 ? 1000 : 30)).ToList();
        var picker = new SkUiPicker
        {
            ItemsSource = items, ItemDisplayBinding = new Binding(nameof(Sized.Name)), PopupPlacement = SkUiPopupPlacement.Anchored, ItemTemplate = SizedRow()
        };

        Assert.Equal(100 * 30, Open(picker).PreferredSize.Height, 0.5);
    }

    [Fact]
    public void AWrappedRowMakesThePopupTaller()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        string[] names = ["Location", "Individual"];
        string[] hints = ["Once for each location", "Will be performed by every employee in these roles, one by one"];
        var picker = new SkUiPicker
        {
            ItemsSource = names, PopupPlacement = SkUiPopupPlacement.Anchored, MaxPopupWidth = 200,
            ItemTemplate = new DataTemplate(() =>
            {
                var hint = new SkUiLabel { FontFamily = SkUiTestHelpers.BundledFontFamily, FontSize = 12 };
                var stack = new SkUiVerticalStackLayout
                {
                    Padding = new Thickness(16, 10),
                    Children = { new SkUiLabel { FontFamily = SkUiTestHelpers.BundledFontFamily, Text = "Name" }, hint }
                };
                stack.BindingContextChanged += (_, _) => hint.Text = stack.BindingContext is string name ? hints[Array.IndexOf(names, name)] : string.Empty;
                return stack;
            })
        };
        var request = Open(picker);
        var content = Assert.IsType<SkUiPickerPopup>(request.Content);
        using var surface = new SkUiTestSurface(content, (int)request.PreferredSize.Width, (int)request.PreferredSize.Height);
        surface.Frame();

        // As tall as the two rows as the list lays them out: the second one's hint wraps.
        var first = ((IView)content.List.GetRealizedView(0)!).Frame.Height;
        var second = ((IView)content.List.GetRealizedView(1)!).Frame.Height;
        Assert.True(second > first + 5);
        Assert.Equal(first + second, request.PreferredSize.Height, 0.5);
    }

    #endregion
}

/// <summary>The picker field's padding, chevron and leading view from XAML.</summary>
[Collection(RuntimeXamlCollection.Name)]
public sealed class PickerFieldOptionsXamlTests
{
    [Fact]
    public void XamlSetsTheFieldOptions()
    {
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiPicker TextPadding="16,8" ChevronColor="#6B6F80" ChevronSize="8" ChevronAreaWidth="24">
                <sk:SkUiPicker.LeadingView>
                  <sk:SkUiBox WidthRequest="12" HeightRequest="12" />
                </sk:SkUiPicker.LeadingView>
              </sk:SkUiPicker>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var picker = (SkUiPicker)root.Content;
        Assert.Equal(new Thickness(16, 8), picker.TextPadding);
        Assert.Equal(Color.FromArgb("#6B6F80"), picker.ChevronColor);
        Assert.Equal(8, picker.ChevronSize);
        Assert.Equal(24, picker.ChevronAreaWidth);
        Assert.IsType<SkUiBox>(picker.LeadingView);
    }
}
