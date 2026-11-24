using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Core;
using MauiSkiaUi;
using MauiSkiaUi.CommunityToolkit;
using MauiSkiaUi.Core;
using MauiSkiaUi.Markdown;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace MauiSkiaUiDemo;

/// <summary>
/// Every control on one page, grouped like the flyout's Controls entries: the control's name, a small live sample beside it
/// (under it on narrow pages) and a "Try it" button that opens the control's demo page.
/// </summary>
public sealed class ControlsGalleryPage : ContentPage
{
    /// <summary>The page width from which samples sit beside their names: room for the widest samples (300) next to the name column.</summary>
    internal const double WideMinWidth = 680;

    private const double NameWidth = 220;
    private const string TryItText = "Try it ›";

    /// <summary>Shown in place of a sample for a control added to the catalog without one.</summary>
    internal const string NoSampleText = "Open the demo to see it";

    private readonly List<Row> _rows = [];
    private bool? _wide;
    private bool _navigating;

    public ControlsGalleryPage()
    {
        Title = "All controls";
        Background = DemoColors.PageBackground;
        var stack = new SkUiVerticalStackLayout { Spacing = 8, Padding = new Thickness(16, 8, 16, 24) };
        foreach (var category in ComponentCategoryInfo.Order)
        {
            stack.Children.Add(new SkUiLabel
            {
                Text = ComponentCategoryInfo.Title(category), TextColor = DemoColors.Ink, FontFamily = DemoFonts.OpenSansSemibold, FontSize = 18,
                Margin = new Thickness(0, stack.Children.Count == 0 ? 4 : 16, 0, 0), AutomationId = "GallerySection" + category
            });
            foreach (var demo in Demos.Where(demo => demo.Category == category))
            {
                var row = new Row(demo, Sample(demo.ComponentType), () => Navigate(demo.Route));
                _rows.Add(row);
                stack.Children.Add(row.Card);
            }
        }
        Content = new SkUiScrollView { Content = stack, AutomationId = "ControlsGallery" };
        SizeChanged += (_, _) => UpdateLayout(Width);
    }

    /// <summary>
    /// One demo per control, in catalog order: its main page (the one without a <see cref="ComponentDemo.Key"/>), else its
    /// first page, so scenario pages of a control (and controls used only as a scenario's host) add no rows of their own.
    /// </summary>
    public static IReadOnlyList<ComponentDemo> Demos { get; } = ComponentDemos.All
        .Select((demo, index) => (Demo: demo, Index: index))
        .GroupBy(entry => entry.Demo.ComponentType)
        .Select(group => group.FirstOrDefault(entry => entry.Demo.Key is null, group.First()))
        .OrderBy(entry => entry.Index)
        .Select(entry => entry.Demo)
        .ToList();

    /// <summary>Whether samples sit beside their names (<see langword="null"/> before the first size).</summary>
    internal bool? IsWide => _wide;

    internal int RowCount => _rows.Count;

    internal void UpdateLayout(double width)
    {
        if (width <= 0) return;
        var wide = width >= WideMinWidth;
        if (_wide == wide) return;
        _wide = wide;
        foreach (var row in _rows)
            row.Apply(wide);
    }

    private async Task Navigate(string route)
    {
        if (_navigating) return;
        _navigating = true;
        try { await Shell.Current.GoToAsync(route); }
        finally { _navigating = false; }
    }

    /// <summary>A card: the name and Try it on the first row, the sample beside the name (wide) or on a row of its own (narrow).</summary>
    private sealed class Row
    {
        private readonly SkUiLabel _name;
        private readonly SkUiView _sample;
        private readonly SkUiGrid _grid;

        public Row(ComponentDemo demo, SkUiView sample, Func<Task> open)
        {
            _name = new SkUiLabel
            {
                Text = demo.DisplayName, TextColor = DemoColors.Ink, FontFamily = DemoFonts.OpenSansSemibold, FontSize = 15,
                VerticalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.WordWrap
            };
            _sample = sample;
            _sample.HorizontalOptions = LayoutOptions.Start;
            _sample.VerticalOptions = LayoutOptions.Center;
            var tryIt = new SkUiButton
            {
                Text = TryItText, FontSize = 13, TextColor = Colors.White, Background = DemoColors.Accent, CornerRadius = 14,
                Padding = new Thickness(12, 4), MinimumHeightRequest = 28, VerticalOptions = LayoutOptions.Center, AutomationId = "TryIt" + demo.Name
            };
            tryIt.Clicked += async (_, _) => await open();
            Grid.SetColumn(tryIt, 2);
            _grid = new SkUiGrid
            {
                ColumnSpacing = 12, RowSpacing = 8,
                ColumnDefinitions = [new(NameWidth), new(GridLength.Star), new(GridLength.Auto)],
                RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto)],
                Children = { _name, _sample, tryIt }
            };
            Card = new SkUiBorder
            {
                Background = Colors.White, Stroke = DemoColors.Border, StrokeThickness = 1, CornerRadius = 6, Padding = new Thickness(12, 10),
                Content = _grid, AutomationId = "GalleryRow" + demo.Name
            };
            Apply(wide: false);
        }

        public SkUiBorder Card { get; }

        public void Apply(bool wide)
        {
            // Wide: name | sample | Try it. Narrow: the name takes the width Try it leaves (a fixed column would push Try it
            // past a 360-wide phone's card) and the sample the whole second row.
            _grid.ColumnDefinitions[0].Width = wide ? new GridLength(NameWidth) : GridLength.Star;
            _grid.ColumnDefinitions[1].Width = wide ? GridLength.Star : new GridLength(0);
            Grid.SetColumnSpan(_name, wide ? 1 : 2);
            Grid.SetRow(_sample, wide ? 0 : 1);
            Grid.SetColumn(_sample, wide ? 1 : 0);
            Grid.SetColumnSpan(_sample, wide ? 1 : 3);
            _grid.RowSpacing = wide ? 0 : 8;
        }
    }

    // ---- Samples: small, live instances; the bigger previews reuse their demo page's builder in a clipped frame. ----

    private static readonly string[] Words = ["Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta"];
    private static readonly Color[] Palette = [DemoColors.Accent, DemoColors.SampleA, DemoColors.SampleB];

    internal static SkUiView Sample(Type type) => type switch
    {
        _ when type == typeof(SkUiView) => new SkUiView { Background = Gradient(), WidthRequest = 120, HeightRequest = 40 },
        _ when type == typeof(SkUiLabel) => new SkUiLabel
        {
            FormattedText = new FormattedString
            {
                Spans =
                {
                    new Span { Text = "Rich ", FontAttributes = FontAttributes.Bold }, new Span { Text = "text", TextColor = DemoColors.Accent },
                    new Span { Text = " with spans", FontAttributes = FontAttributes.Italic }
                }
            },
            TextColor = DemoColors.Ink, FontSize = 15
        },
        _ when type == typeof(SkUiButton) => new SkUiButton { Text = "Button" },
        _ when type == typeof(SkUiImage) => new SkUiImage { Source = DemoAssets.DotnetBotImage, WidthRequest = 96, HeightRequest = 60 },
        _ when type == typeof(SkUiMarkdownView) => new SkUiMarkdownView { Text = "**Markdown** with *emphasis*, `code` and a [link](https://dotnet.microsoft.com)." },
        _ when type == typeof(SkUiImageButton) => new SkUiImageButton
        {
            Source = new SkUiVectorImageSource { Source = DemoAssets.VectorGear, Color = Colors.White }, Background = DemoColors.Accent,
            CornerRadius = 8, Padding = new Thickness(8), WidthRequest = 44, HeightRequest = 44
        },
        _ when type == typeof(SkUiAvatarView) => new SkUiAvatarView { Text = "ZS", WidthRequest = 44, HeightRequest = 44 },
        _ when type == typeof(SkUiActivityIndicator) => new SkUiActivityIndicator { IsRunning = true, WidthRequest = 32, HeightRequest = 32 },
        _ when type == typeof(SkUiSwitch) => new SkUiSwitch { IsToggled = true },
        _ when type == typeof(SkUiCheckBox) => new SkUiCheckBox { IsChecked = true },
        _ when type == typeof(SkUiSlider) => new SkUiSlider { Value = 0.4, WidthRequest = 200 },
        _ when type == typeof(SkUiStepper) => new SkUiStepper { Value = 3, Maximum = 10 },
        _ when type == typeof(SkUiStepper<decimal>) => new SkUiStepper<decimal> { Maximum = 10, Increment = 0.25m, Value = 1.5m },
        _ when type == typeof(SkUiProgressBar) => new SkUiProgressBar { Progress = 0.6, WidthRequest = 200 },
        _ when type == typeof(SkUiEntry) => new SkUiEntry { Placeholder = "Type here", WidthRequest = 220 },
        _ when type == typeof(SkUiEditor) => new SkUiEditor { Placeholder = "Several lines", WidthRequest = 220, HeightRequest = 72 },
        _ when type == typeof(SkUiSearchBar) => new SkUiSearchBar { Placeholder = "Search", WidthRequest = 220 },
        _ when type == typeof(SkUiNumericEntry) => new SkUiNumericEntry { Value = 42, ButtonPlacement = SkUiStepButtonPlacement.End, WidthRequest = 160 },
        _ when type == typeof(SkUiNumericEntry<decimal>) => new SkUiNumericEntry<decimal>
        {
            Value = 9.99m, Format = "C2", Culture = System.Globalization.CultureInfo.GetCultureInfo("en-US"),
            ButtonPlacement = SkUiStepButtonPlacement.Split, HorizontalTextAlignment = TextAlignment.Center, WidthRequest = 160
        },
        _ when type == typeof(SkUiPicker) => new SkUiPicker { Title = "Pick a letter", ItemsSource = Words, WidthRequest = 200 },
        _ when type == typeof(SkUiMultiColumnPicker) => MultiColumnPicker(),
        _ when type == typeof(SkUiPickerWheels) => PickerWheels(),
        _ when type == typeof(SkUiDatePicker) => new SkUiDatePicker { WidthRequest = 200 },
        _ when type == typeof(SkUiCalendar) => new SkUiCalendar { WidthRequest = 280, HeightRequest = 280 },
        _ when type == typeof(SkUiDateRangePicker) => new SkUiDateRangePicker { Placeholder = "Pick your dates", WidthRequest = 240 },
        _ when type == typeof(SkUiTimePicker) => new SkUiTimePicker { WidthRequest = 160 },
        _ when type == typeof(SkUiDateTimePicker) => new SkUiDateTimePicker { Placeholder = "When?", WidthRequest = 240 },
        _ when type == typeof(SkUiRadioButton) => RadioButtonDemoPage.CreateSkiaGroup(out _, out _),
        _ when type == typeof(SkUiContentPresenter) => RadioButtonTemplateDemoPage.CreateSkiaGroup(out _),

        _ when type == typeof(SkUiContentView) => new SkUiContentView { Content = Chip("Content", 0), Padding = new Thickness(8), Background = DemoColors.SoftSurface },
        _ when type == typeof(SkUiZoomView) => Framed(new SkUiZoomView { Content = new SkUiLabel { Text = "Pinch or wheel to zoom", TextColor = DemoColors.Ink, Padding = 12 } }, 240, 72),
        _ when type == typeof(SkUiAlternateContentView) => AlternateContent(),
        _ when type == typeof(SkUiShimmer) => Framed(ShimmerDemoPage.Create(), 280, 120),
        _ when type == typeof(SkUiShimmerBone) => Framed(ShimmerBonesDemoPage.Create(), 280, 160),
        _ when type == typeof(SkUiExpander) => new SkUiExpander
        {
            Header = new SkUiLabel { Text = "Details (tap me) ›", TextColor = DemoColors.Ink, FontAttributes = FontAttributes.Bold, Padding = new Thickness(0, 4) },
            Content = new SkUiLabel { Text = "Expanded content slides in below the header.", TextColor = DemoColors.Caption, WidthRequest = 240 },
            AnimationLength = 250
        },
        _ when type == typeof(SkUiSwipeView) => Framed(SwipeView(), 260, 56),
        _ when type == typeof(SkUiSwipeItemView) => Framed(SwipeItemView(), 260, 56),
        _ when type == typeof(SkUiMauiContentView) => new SkUiMauiContentView { Content = new Entry { Placeholder = "A native Entry", WidthRequest = 200 } },
        _ when type == typeof(SkUiBorder) => new SkUiBorder
        {
            Stroke = DemoColors.Accent, StrokeThickness = 2, CornerRadius = 12, Padding = new Thickness(12, 6),
            Content = new SkUiLabel { Text = "Border", TextColor = DemoColors.Ink }
        },
        _ when type == typeof(SkUiLayout) => new SkUiLayout { Children = { Placed(Box(0, 40, 40), new Thickness(0, 0, 32, 16)), Placed(Box(1, 32, 32), new Thickness(32, 16, 0, 0)) } },
        _ when type == typeof(SkUiGrid) => GridSample(),
        _ when type == typeof(SkUiDragger) => Framed(DraggerDemoPage.CreatePreview(out _, out _), 280, 160),
        _ when type == typeof(SkUiGridSplitter) => Framed(GridSplitterDemoPage.CreatePreview(out _, out _, out _, out _), 280, 160),
        _ when type == typeof(SkUiVerticalStackLayout) => new SkUiVerticalStackLayout { Spacing = 4, Children = { Chip(Words[0], 0), Chip(Words[1], 1), Chip(Words[2], 2) } },
        _ when type == typeof(SkUiHorizontalStackLayout) => new SkUiHorizontalStackLayout { Spacing = 4, Children = { Chip(Words[0], 0), Chip(Words[1], 1), Chip(Words[2], 2) } },
        _ when type == typeof(SkUiAbsoluteLayout) => AbsoluteSample(),
        _ when type == typeof(SkUiFlexLayout) => Chips(new SkUiFlexLayout { Wrap = FlexWrap.Wrap, WidthRequest = 240 }, 6),
        _ when type == typeof(SkUiDockLayout) => DockSample(),
        _ when type == typeof(SkUiUniformItemsLayout) => Chips(new SkUiUniformItemsLayout { MaxColumns = 3, WidthRequest = 240 }, 6),
        _ when type == typeof(SkUiWrapLayout) => Chips(new SkUiWrapLayout { Spacing = 4, RowSpacing = 4, WidthRequest = 240 }, 8),
        _ when type == typeof(SkUiTileLayout) => TileSample(),
        _ when type == typeof(SkUiHorizontalShrinkLayout) => HorizontalShrinkSample(),
        _ when type == typeof(SkUiVerticalShrinkLayout) => VerticalShrinkSample(),
        _ when type == typeof(SkUiHorizontalAlignStackLayout) => AlignStackSample(),

        _ when type == typeof(SkUiBox) => new SkUiBox { Color = DemoColors.Accent, CornerRadius = 8, WidthRequest = 64, HeightRequest = 40 },
        _ when type == typeof(SkUiEllipse) => Shape(new SkUiEllipse()),
        _ when type == typeof(SkUiLine) => new SkUiLine(0, 30, 120, 0) { Stroke = Brush(DemoColors.Accent), StrokeThickness = 4, WidthRequest = 120, HeightRequest = 30 },
        _ when type == typeof(SkUiRectangle) => Shape(new SkUiRectangle()),
        _ when type == typeof(SkUiRoundRectangle) => Shape(new SkUiRoundRectangle { CornerRadius = 12 }),
        _ when type == typeof(SkUiPolygon) => Shape(new SkUiPolygon { Points = [new(40, 0), new(80, 40), new(0, 40)] }),
        _ when type == typeof(SkUiPolyline) => new SkUiPolyline
        {
            Points = [new(0, 40), new(30, 5), new(60, 35), new(90, 0), new(120, 30)], Stroke = Brush(DemoColors.SampleB), StrokeThickness = 3,
            WidthRequest = 120, HeightRequest = 40
        },
        _ when type == typeof(SkUiPath) => Shape(new SkUiPath
        {
            Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString("M 0 20 C 20 -10, 40 50, 60 20 S 100 -10, 120 20")!,
            Fill = Brush(Colors.Transparent)
        }),

        _ when type == typeof(SkUiScrollView) => Framed(new SkUiScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            Content = Chips(new SkUiHorizontalStackLayout { Spacing = 4, Padding = new Thickness(4) }, Words.Length)
        }, 240, 44),
        _ when type == typeof(SkUiCollectionView) => Framed(new SkUiCollectionView
        {
            ItemsSource = Words, ItemTemplate = new DataTemplate(() => new ItemLabel()), SelectionMode = SkUiSelectionMode.Single
        }, 240, 120),
        _ when type == typeof(SkUiRefreshView) => Framed(RefreshSample(), 240, 120),
        _ when type == typeof(SkUiCarouselView) => CarouselSample(),
        _ when type == typeof(SkUiIndicatorView) => new SkUiIndicatorView { Count = 5, Position = 1 },
        _ when type == typeof(SkUiVirtualScrollView) => Framed(new SkUiVirtualScrollView
        {
            ItemsSource = Enumerable.Range(1, 10_000).Select(index => $"Item {index:N0}").ToList(), ItemTemplate = new DataTemplate(() => new ItemLabel())
        }, 240, 120),
        _ when type == typeof(SkUiVirtualVerticalStackLayout) => Framed(new SkUiScrollView
        {
            Content = new SkUiVirtualVerticalStackLayout
            {
                ItemsSource = Enumerable.Range(1, 1_000).Select(index => $"Row {index:N0}").ToList(), ItemTemplate = new DataTemplate(() => new ItemLabel())
            }
        }, 240, 120),

        _ when type == typeof(SkUiCoreGrid) => Framed(CoreGridDemoPage.CreateHost(out _, out _, out _), 280, 160),
        _ when type == typeof(SkUiCoreDragger) => Framed(CoreDraggerDemoPage.CreateHost(out _, out _), 280, 160),
        _ when type == typeof(SkUiCoreGridSplitter) => Framed(CoreGridSplitterDemoPage.CreateHost(out _, out _), 300, 160),
        _ when type == typeof(SkUiCoreTable) => Framed(CoreTableDemoPage.CreateHost(out _), 300, 200),
        _ when type == typeof(SkUiCoreWrapLayout) => Framed(CoreWrapLayoutDemoPage.CreateHost(out _), 240, 120),
        _ when type == typeof(SkUiCoreTileLayout) => Framed(CoreTileLayoutDemoPage.CreateHost(out _), 300, 200),
        _ when type == typeof(SkUiCoreHorizontalShrinkLayout) => Framed(CoreHorizontalShrinkLayoutDemoPage.CreateHost(out _, out _, out _), 280, 60),
        _ when type == typeof(SkUiCoreVerticalShrinkLayout) => Framed(CoreVerticalShrinkLayoutDemoPage.CreateHost(out _, out _), 240, 160),
        _ when type == typeof(SkUiCoreHorizontalAlignStackLayout) => Framed(CoreHorizontalAlignStackLayoutDemoPage.CreateHost(out _, out _, out _, out _), 300, AlignStackDemo.AreaHeight),
        _ when type == typeof(SkUiCoreScrollView) => Framed(CoreScrollViewDemoPage.CreateHost(out _, out _, out _, out _, out _), 300, 200),
        _ when type == typeof(SkUiCoreCarouselView) => Framed(CoreCarouselViewDemoPage.CreateSample(), 300, 200),
        _ when type == typeof(SkUiCoreBorder) => Framed(CorePressEffectDemoPage.CreateHost(out _, out _, out _), 300, 200),
        _ when type == typeof(SkUiCoreStateContainer) => Framed(CoreStateContainerDemoPage.CreateHost(out _), 260, 160),
        _ when type == typeof(SkUiCoreExpander) => Framed(CoreExpanderDemoPage.CreateHost(out _), 280, 180),
        _ when type == typeof(SkUiCoreShimmer) => Framed(CoreShimmerDemoPage.CreateHost(out _), 280, 160),
        _ when type == typeof(SkUiCoreShimmerBone) => Framed(CoreShimmerBonesDemoPage.CreateHost(out _), 280, 160),
        // A control added to the catalog without a sample yet: its row still opens the demo.
        _ => new SkUiLabel { Text = NoSampleText, TextColor = DemoColors.Caption, FontSize = 12 }
    };

    /// <summary>A fixed-size, clipped frame for previews that fill whatever space they get.</summary>
    private static SkUiContentView Framed(SkUiView content, double width, double height) => new()
    {
        Content = content, WidthRequest = width, HeightRequest = height, ClipToBounds = true, Background = DemoColors.PageBackground
    };

    private static SolidColorBrush Brush(Color color) => new(color);

    private static LinearGradientBrush Gradient() =>
        new([new GradientStop(DemoColors.Accent, 0), new GradientStop(DemoColors.SampleB, 1)], new Point(0, 0), new Point(1, 1));

    private static SkUiLabel Chip(string text, int index) => new()
    {
        Text = text, TextColor = Colors.White, FontSize = 13, Padding = new Thickness(8, 3), Background = Palette[index % Palette.Length],
        VerticalTextAlignment = TextAlignment.Center
    };

    private static SkUiBox Box(int index, double width, double height) => new()
    {
        Color = Palette[index % Palette.Length], WidthRequest = width, HeightRequest = height, CornerRadius = 4
    };

    // SkUiLayout stacks its children on each other: margins place them.
    private static SkUiBox Placed(SkUiBox box, Thickness margin)
    {
        box.Margin = margin;
        return box;
    }

    private static T Chips<T>(T layout, int count) where T : SkUiLayout
    {
        for (var index = 0; index < count; index++)
            layout.Children.Add(Chip(Words[index % Words.Length], index));
        return layout;
    }

    private static T Shape<T>(T shape) where T : SkUiShape
    {
        shape.Fill ??= Brush(DemoColors.SoftSurface);
        shape.Stroke = Brush(DemoColors.Accent);
        shape.StrokeThickness = 3;
        shape.WidthRequest = shape is SkUiPath ? 120 : 80;
        shape.HeightRequest = 44;
        return shape;
    }

    private static SkUiMultiColumnPicker MultiColumnPicker()
    {
        var picker = new SkUiMultiColumnPicker { Title = "Pick a time", WidthRequest = 200 };
        picker.Columns.Add(new SkUiPickerColumn { ItemsSource = Enumerable.Range(0, 24).Select(hour => hour.ToString("00")).ToList(), SelectedIndex = 9 });
        picker.Columns.Add(new SkUiPickerColumn { ItemsSource = Enumerable.Range(0, 12).Select(step => (step * 5).ToString("00")).ToList(), SelectedIndex = 6 });
        return picker;
    }

    private static SkUiPickerWheels PickerWheels()
    {
        var wheels = new SkUiPickerWheels { VisibleRows = 3, WidthRequest = 220, HeightRequest = 108 };
        wheels.Columns.Add(new SkUiPickerColumn { ItemsSource = Enumerable.Range(1, 12).Select(count => count.ToString()).ToList(), SelectedIndex = 1 });
        wheels.Columns.Add(new SkUiPickerColumn { ItemsSource = new[] { "days", "weeks", "months" }, SelectedIndex = 1 });
        return wheels;
    }

    private static SkUiAlternateContentView AlternateContent()
    {
        SkUiAlternateContentView view = null!;
        view = new SkUiAlternateContentView
        {
            Content = new SkUiButton { Text = "Viewing · tap to edit" },
            AlternateContentTemplate = new DataTemplate(() =>
            {
                // Back to the read side when editing ends.
                var entry = new SkUiEntry { Placeholder = "Editing · Return to finish", WidthRequest = 220 };
                entry.Completed += (_, _) => view.ShowsAlternate = false;
                entry.Unfocused += (_, _) => view.ShowsAlternate = false;
                return entry;
            }),
            BeforeStateChangeAnimation = SkUiViewAnimation.FadeOut(), AfterStateChangeAnimation = SkUiViewAnimation.FadeIn(),
            ShowsAlternate = false
        };
        ((SkUiButton)view.Content).Clicked += (_, _) => view.ShowsAlternate = true;
        return view;
    }

    private static SkUiSwipeView SwipeView()
    {
        var swipe = new SkUiSwipeView
        {
            Content = new SkUiLabel
            {
                Text = "Swipe me left or right", TextColor = DemoColors.Ink, Background = Colors.White, Padding = new Thickness(12),
                VerticalTextAlignment = TextAlignment.Center
            }
        };
        swipe.LeftItems = [new SwipeItem { Text = "Archive", BackgroundColor = DemoColors.SampleB }];
        swipe.RightItems = [new SwipeItem { Text = "Delete", BackgroundColor = DemoColors.SampleA }];
        return swipe;
    }

    private static SkUiSwipeView SwipeItemView()
    {
        var swipe = new SkUiSwipeView
        {
            Content = new SkUiLabel
            {
                Text = "Swipe for custom items", TextColor = DemoColors.Ink, Background = Colors.White, Padding = new Thickness(12),
                VerticalTextAlignment = TextAlignment.Center
            }
        };
        swipe.LeftItems = [new SkUiSwipeItemView { Content = Chip("↩ Reply", 0), WidthRequest = 96 }];
        swipe.RightItems = [new SkUiSwipeItemView { Content = Chip("Mute", 1), WidthRequest = 96 }];
        return swipe;
    }

    private static SkUiGrid GridSample()
    {
        var grid = new SkUiGrid
        {
            ColumnSpacing = 4, RowSpacing = 4, ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)],
            RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto)], WidthRequest = 200
        };
        for (var index = 0; index < 4; index++)
        {
            var chip = Chip(Words[index], index);
            Grid.SetRow(chip, index / 2);
            Grid.SetColumn(chip, index % 2);
            grid.Children.Add(chip);
        }
        return grid;
    }

    private static SkUiAbsoluteLayout AbsoluteSample()
    {
        var layout = new SkUiAbsoluteLayout { WidthRequest = 200, HeightRequest = 60, Background = DemoColors.SoftSurface };
        var first = Box(0, 48, 32);
        var second = Box(1, 48, 32);
        SkUiAbsoluteLayout.SetLayoutBounds(first, new Rect(0.1, 0.2, 48, 32));
        SkUiAbsoluteLayout.SetLayoutFlags(first, AbsoluteLayoutFlags.PositionProportional);
        SkUiAbsoluteLayout.SetLayoutBounds(second, new Rect(0.8, 0.8, 48, 32));
        SkUiAbsoluteLayout.SetLayoutFlags(second, AbsoluteLayoutFlags.PositionProportional);
        layout.Children.Add(first);
        layout.Children.Add(second);
        return layout;
    }

    private static SkUiDockLayout DockSample()
    {
        var dock = new SkUiDockLayout { WidthRequest = 220, HeightRequest = 90, Background = DemoColors.SoftSurface };
        dock.Add(Chip("Top", 0), DockPosition.Top);
        dock.Add(Chip("Bottom", 1), DockPosition.Bottom);
        dock.Add(Chip("Left", 2), DockPosition.Left);
        dock.Add(new SkUiLabel { Text = "Center", TextColor = DemoColors.Ink, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center });
        return dock;
    }

    private static SkUiTileLayout TileSample()
    {
        var tiles = new SkUiTileLayout
        {
            TileSize = 44, ColumnSpacing = 4, RowSpacing = 4, MaximumRows = 1, WidthRequest = 240,
            OverflowTemplate = new DataTemplate(() => new SkUiLabel
            {
                Text = "+", TextColor = Colors.White, Background = Color.FromRgba(0, 0, 0, 0.5), HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            })
        };
        for (var index = 0; index < 9; index++)
            tiles.Children.Add(Box(index, -1, -1));
        return tiles;
    }

    private static SkUiHorizontalShrinkLayout HorizontalShrinkSample()
    {
        var row = new SkUiHorizontalShrinkLayout { Spacing = 4, WidthRequest = 240 };
        var first = Chip("A first, shrinkable label", 0);
        var second = Chip("A longer second label that shrinks too", 1);
        first.LineBreakMode = second.LineBreakMode = LineBreakMode.TailTruncation;
        SkUiShrinkLayout.SetShrink(first, SkUiShrinkFactor.Auto);
        SkUiShrinkLayout.SetShrink(second, SkUiShrinkFactor.Auto);
        row.Children.Add(first);
        row.Children.Add(second);
        row.Children.Add(Chip("Fixed", 2));
        return row;
    }

    private static SkUiContentView VerticalShrinkSample()
    {
        var column = new SkUiVerticalShrinkLayout { Spacing = 4, VerticalOptions = LayoutOptions.Start };
        var body = new SkUiScrollView
        {
            Content = new SkUiLabel
            {
                Text = "A shrinkable scroll view between a header and a footer: it gets the height that is left.", TextColor = DemoColors.Ink,
                Padding = new Thickness(8), Background = DemoColors.SoftSurface, HeightRequest = 120
            }
        };
        SkUiShrinkLayout.SetShrink(body, SkUiShrinkFactor.Auto);
        column.Children.Add(Chip("Header", 0));
        column.Children.Add(body);
        column.Children.Add(Chip("Footer", 0));
        return Framed(column, 240, 110);
    }

    private static SkUiHorizontalAlignStackLayout AlignStackSample() => new()
    {
        Spacing = 8, Padding = new Thickness(8, 0), Background = DemoColors.SoftSurface, HeightRequest = 40, WidthRequest = 280,
        Children =
        {
            new SkUiLabel
            {
                Text = "Centered title", TextColor = DemoColors.Ink, FontAttributes = FontAttributes.Bold, MaxLines = 1,
                LineBreakMode = LineBreakMode.TailTruncation, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
            },
            new SkUiButton { Text = "Cancel", FontSize = 12, Padding = new Thickness(8, 2), HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center },
            new SkUiButton { Text = "×", FontSize = 12, Padding = new Thickness(8, 2), HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center }
        }
    };

    private static SkUiRefreshView RefreshSample()
    {
        var rows = new SkUiVerticalStackLayout { Spacing = 2, Padding = new Thickness(4) };
        foreach (var word in Words)
            rows.Children.Add(new ItemLabel { Text = "Pull down · " + word });
        var refresh = new SkUiRefreshView { Content = new SkUiScrollView { Content = rows } };
        refresh.Refreshing += async (_, _) =>
        {
            await Task.Delay(800);
            refresh.IsRefreshing = false;
        };
        return refresh;
    }

    private static SkUiVerticalStackLayout CarouselSample()
    {
        var indicator = new SkUiIndicatorView { HorizontalOptions = LayoutOptions.Center };
        var carousel = new SkUiCarouselView
        {
            ItemsSource = new ObservableCollection<string>(Words.Take(5)), IndicatorView = indicator, HeightRequest = 72,
            ItemTemplate = new DataTemplate(() => new ItemLabel { FontSize = 18, HorizontalTextAlignment = TextAlignment.Center, Colored = true })
        };
        return new SkUiVerticalStackLayout { Spacing = 6, WidthRequest = 240, Children = { carousel, indicator } };
    }

    /// <summary>A list row showing its item's text (no binding path, so nothing for the trimmer to keep).</summary>
    private sealed class ItemLabel : SkUiLabel
    {
        public ItemLabel()
        {
            TextColor = DemoColors.Ink;
            FontSize = 14;
            Padding = new Thickness(10, 6);
            VerticalTextAlignment = TextAlignment.Center;
        }

        /// <summary>Fills the row with a palette color picked by its text.</summary>
        public bool Colored { get; init; }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not string text) return;
            Text = text;
            if (!Colored) return;
            TextColor = Colors.White;
            Background = Palette[text.Length % Palette.Length];
        }
    }
}
