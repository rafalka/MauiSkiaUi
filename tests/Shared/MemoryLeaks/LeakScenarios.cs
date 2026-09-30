using System.Windows.Input;
using MauiSkiaUi;
using MauiSkiaUi.Core;
using Microsoft.Maui.Layouts;
using SkiaSharp;

namespace MauiSkiaUi.LeakTests;

/// <summary>
/// The leak scenario catalog. Each one exercises its controls the way a screen would before it is closed (clicks,
/// re-layout, scrolling and flings, gestures, animations, native overlays, surface switches), because leaks usually
/// come from state that only exists after interaction: captured pointers, gesture timers, render-thread animations,
/// overlay registrations, command subscriptions.
/// </summary>
public static class LeakScenarios
{
    public const string Controls = "Controls";
    public const string Layouts = "Layouts";
    public const string Scrolling = "Scrolling";
    public const string Input = "Input";
    public const string Rendering = "Rendering";
    public const string Core = "Core";
    public const string Native = "Native";

    public static IReadOnlyList<LeakScenario> All { get; } =
    [
        new("ButtonsClicked", Controls, "Buttons with a long-lived command, clicked twice each, disabled and re-enabled, a cancelled press; an image button.", () => new ButtonsRun()),
        new("TogglesTapped", Controls, "Switch, check boxes (one three-state) and a radio group, each tapped several times.", () => new TogglesRun()),
        new("SlidersAndProgress", Controls, "Horizontal, vertical and Core sliders dragged and tapped; progress bars animating and indeterminate at close.", () => new SlidersRun()),
        new("LabelsReshaped", Controls, "Wrapped, truncated, RTL, Arabic, emoji and Simple / Shaped labels; text and width changed repeatedly.", () => new LabelsRun()),
        new("ImagesReloaded", Controls, "Images decoded from streams, sources swapped, reloaded, aspect changed.", () => new ImagesRun()),
        new("LayoutsRelayout", Layouts, "Grid, stacks, absolute, flex, wrap and shrink layouts (drawn and Core) and a border with many children; resized, children added / removed / reordered, hidden, definitions changed.", () => new LayoutsRun()),
        new("ScrollFling", Scrolling, "Vertical list with a nested carousel: drags, flings, an animated scroll; closed mid-fling.", () => new ScrollRun()),
        new("GesturesMixed", Input, "Tap, double tap, long press, swipe, pan and pinch recognizers (drawn and Core); closed with a finger still down.", () => new GesturesRun()),
        new("AnimationsRunning", Rendering, "Render-thread animations (fade-in from 0, move, rotate, scale), a spinner; closed while they run; a node detached mid-animation.", () => new AnimationsRun()),
        new("SurfaceReplaced", Rendering, "A page replaces its GPU surface with a software one and back; the discarded surfaces are disconnected.", () => new SurfaceReplacedRun()),
        new("MovedBetweenSurfaces", Rendering, "A subtree with gestures, a scroller and a native Entry moves between two surfaces and back.", () => new MoveRun()),
        new("CoreControls", Core, "Core buttons (long-lived command), toggles, a table and a scroll view: clicked, scrolled, rows removed, columns changed.", () => new CoreRun()),
        new("NativeOverlays", Native, "Entry / Editor overlays in a drawn scroller (snapshot mode): scrolled, an Entry focused, overlay content replaced.", () => new OverlaysRun()),
        new("NativeNesting", Native, "GPU and software surfaces inside a native ScrollView: drawn list, carousel and Core scroller dragged.", () => new NestingRun())
    ];

    /// <summary>A scenario that deliberately leaks its root: the checker must report it (detector self-test).</summary>
    public static LeakScenario DeliberateLeak { get; } =
        new("DeliberateLeak", "Self-test", "Keeps its surface root in a static list; the leak check must fail.", () => new DeliberateLeakRun());

    public static LeakScenario Find(string name) =>
        All.FirstOrDefault(scenario => scenario.Name == name)
        ?? (name == DeliberateLeak.Name ? DeliberateLeak : throw new ArgumentException($"Unknown leak scenario '{name}'.", nameof(name)));

    /// <summary>Automation id of the root <see cref="DeliberateLeak"/> retains: survivor reports name it.</summary>
    public const string DeliberateLeakMarker = "DeliberateLeakRoot";

    /// <summary>Clears what <see cref="DeliberateLeak"/> retained.</summary>
    public static void ReleaseDeliberateLeak() => DeliberateLeakRun.Retained.Clear();

    private static SkUiContentView Root(ISkUiView content) => new() { Content = content, Background = Colors.White };

    private static SkUiLabel Text(string text, double size = 14) =>
        new() { Text = text, FontSize = size, TextColor = LeakColors.Ink, Padding = new Thickness(8, 4) };

    // ---- Controls ---------------------------------------------------------------------------------------------

    private sealed class ButtonsRun : LeakScenarioRun
    {
        private readonly List<SkUiButton> _buttons = [];
        private SkUiImageButton? _imageButton;
        private int _clicks;

        public override View Build(LeakScenarioContext context)
        {
            var stack = new SkUiVerticalStackLayout { Spacing = 8, Padding = new Thickness(12) };
            for (var index = 1; index <= 6; index++)
            {
                var button = new SkUiButton { Text = $"Button {index}", FontSize = 15, Command = LeakCommands.Shared, CommandParameter = index };
                button.Clicked += OnClicked;
                _buttons.Add(button);
                stack.Children.Add(button);
            }
            _imageButton = new SkUiImageButton { Source = LeakImages.Create(SKColors.Teal), Command = LeakCommands.Shared, HeightRequest = 56, WidthRequest = 120 };
            _imageButton.Clicked += OnClicked;
            stack.Children.Add(_imageButton);
            return Root(stack);
        }

        private void OnClicked(object? sender, EventArgs e) => _clicks++;

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            foreach (var button in _buttons)
            {
                await context.TapAsync(button);
                await context.TapAsync(button);
            }
            _buttons[0].IsEnabled = false;
            await context.SettleAsync();
            _buttons[0].IsEnabled = true;
            LeakCommands.Shared.RaiseCanExecuteChanged();
            await context.WaitForAsync(_imageButton!.LoadingTask);
            await context.TapAsync(_imageButton);
            // Press, then drag off the button: the press is cancelled, nothing clicks.
            await context.DragAsync(_buttons[1], 0, 120);
        }

        public override string? CheckInteraction() => _clicks >= 13 ? null : $"{_clicks} clicks, expected 13.";
    }

    private sealed class TogglesRun : LeakScenarioRun
    {
        private readonly List<SkUiToggleControl> _toggles = [];
        private int _changes;

        public override View Build(LeakScenarioContext context)
        {
            var stack = new SkUiVerticalStackLayout { Spacing = 10, Padding = new Thickness(12) };
            _toggles.Add(new SkUiSwitch { HeightRequest = 32, WidthRequest = 56 });
            _toggles.Add(new SkUiCheckBox { HeightRequest = 32, WidthRequest = 32 });
            _toggles.Add(new SkUiCheckBox { HeightRequest = 32, WidthRequest = 32, IsThreeState = true, CheckState = SkUiCheckState.Indeterminate });
            for (var index = 0; index < 3; index++)
                _toggles.Add(new SkUiRadioButton { GroupName = "leak-radio", HeightRequest = 32, WidthRequest = 32 });
            foreach (var toggle in _toggles)
            {
                toggle.CheckedChanged += (_, _) => _changes++;
                stack.Children.Add(toggle);
            }
            return Root(stack);
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            for (var round = 0; round < 3; round++)
                foreach (var toggle in _toggles)
                    await context.TapAsync(toggle);
        }

        public override string? CheckInteraction() => _changes >= 6 ? null : $"{_changes} toggle changes, expected at least 6.";
    }

    private sealed class LabelsRun : LeakScenarioRun
    {
        private const string Long = "The quick brown fox jumps over the lazy dog while the drawn label wraps its text across several lines.";
        private readonly List<SkUiLabel> _labels = [];
        private readonly HashSet<double> _widths = [];
        private SkUiContentView? _root;

        public override View Build(LeakScenarioContext context)
        {
            var stack = new SkUiVerticalStackLayout { Spacing = 6, Padding = new Thickness(12) };
            Add(stack, new SkUiLabel { Text = Long, LineBreakMode = LineBreakMode.WordWrap });
            Add(stack, new SkUiLabel { Text = Long, LineBreakMode = LineBreakMode.TailTruncation });
            Add(stack, new SkUiLabel { Text = "مرحبا بالعالم", FlowDirection = FlowDirection.RightToLeft });
            Add(stack, new SkUiLabel { Text = "שלום עולם, hello", HorizontalTextAlignment = TextAlignment.End });
            Add(stack, new SkUiLabel { Text = "Emoji 👍🏽 🎉 fallback", TextRendering = SkUiTextRendering.Shaped });
            Add(stack, new SkUiLabel { Text = "Simple path 0123456789", TextRendering = SkUiTextRendering.Simple });
            Add(stack, new SkUiLabel { Text = "System family, bold", FontFamily = "serif", FontAttributes = FontAttributes.Bold, FontSize = 18 });
            return _root = Root(stack);
        }

        private void Add(SkUiVerticalStackLayout stack, SkUiLabel label)
        {
            label.TextColor = LeakColors.Ink;
            _labels.Add(label);
            stack.Children.Add(label);
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            string[] texts = ["Short", Long + " " + Long, "مرحبا 123 hello", "🎉🎉🎉", Long];
            foreach (var text in texts)
            {
                foreach (var label in _labels)
                    label.Text = text;
                await context.SettleAsync();
            }
            foreach (var width in new[] { 300.0, 200, 360, -1 })
            {
                _root!.WidthRequest = width;
                await context.SettleAsync();
                _widths.Add(Math.Round(_labels[0].Width));
            }
        }

        public override string? CheckInteraction()
        {
            if (_labels.Any(label => label.Text != Long))
                return "Not every label shows the last text.";
            // Width, not height: text measures empty on hosts without fonts (Linux CI), labels still stretch.
            if (_labels.Any(label => label.Width <= 0))
                return "A label was not laid out.";
            return _widths.Count >= 3 ? null : $"The labels were laid out at {_widths.Count} widths, expected at least 3.";
        }
    }

    private sealed class ImagesRun : LeakScenarioRun
    {
        private readonly List<SkUiImage> _images = [];

        public override View Build(LeakScenarioContext context)
        {
            var grid = new SkUiGrid { RowSpacing = 8, ColumnSpacing = 8, Padding = new Thickness(12) };
            grid.ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)];
            grid.RowDefinitions = [new RowDefinition(new GridLength(120)), new RowDefinition(new GridLength(120))];
            SKColor[] colors = [SKColors.Coral, SKColors.SteelBlue, SKColors.Olive];
            for (var index = 0; index < 3; index++)
            {
                var image = new SkUiImage { Source = LeakImages.Create(colors[index]), Aspect = Aspect.AspectFit };
                Grid.SetRow(image, index / 2);
                Grid.SetColumn(image, index % 2);
                _images.Add(image);
                grid.Children.Add(image);
            }
            var button = new SkUiImageButton { Source = LeakImages.Create(SKColors.Gold) };
            Grid.SetRow(button, 1);
            Grid.SetColumn(button, 1);
            _images.Add(button);
            grid.Children.Add(button);
            return Root(grid);
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            foreach (var image in _images)
                await context.WaitForAsync(image.LoadingTask);
            foreach (var aspect in new[] { Aspect.AspectFill, Aspect.Fill, Aspect.Center })
            {
                foreach (var image in _images)
                {
                    image.Source = LeakImages.Create(SKColors.Purple);
                    image.Aspect = aspect;
                }
                foreach (var image in _images)
                    await context.WaitForAsync(image.LoadingTask);
            }
            _reload = _images[0].ReloadAsync();
            await context.WaitForAsync(_reload);
        }

        private Task? _reload;

        public override string? CheckInteraction()
        {
            if (_reload is not { IsCompletedSuccessfully: true })
                return "The reload did not complete.";
            var failed = _images.Where(image => !image.LoadingTask.IsCompletedSuccessfully || image.LoadError is not null || image.IsLoading).ToList();
            return failed.Count == 0 ? null : $"{failed.Count} image(s) did not load: {failed[0].LoadError?.Message ?? "still loading"}.";
        }
    }

    private sealed class SlidersRun : LeakScenarioRun
    {
        private SkUiSlider? _horizontal;
        private SkUiSlider? _vertical;
        private SkUiCoreSlider? _core;
        private SkUiProgressBar? _bar;
        private int _changes;
        private int _drags;

        public override View Build(LeakScenarioContext context)
        {
            _horizontal = new SkUiSlider { Maximum = 100, HeightRequest = 40, DragCompletedCommand = LeakCommands.Shared };
            _vertical = new SkUiSlider { Orientation = StackOrientation.Vertical, HeightRequest = 160, HorizontalOptions = LayoutOptions.Start };
            _horizontal.ValueChanged += (_, _) => _changes++;
            _vertical.ValueChanged += (_, _) => _changes++;
            _horizontal.DragCompleted += (_, _) => _drags++;
            _core = new SkUiCoreSlider();
            _core.SetHeight(40);
            _core.ValueChanged += (_, _) => _changes++;
            _bar = new SkUiProgressBar { HeightRequest = 8 };
            var indeterminate = new SkUiProgressBar { IsIndeterminate = true, HeightRequest = 8 };
            var coreBar = new SkUiCoreProgressBar();
            coreBar.SetIsIndeterminate(true).SetHeight(8);
            var coreStack = new SkUiCoreVerticalStackLayout().SetSpacing(8);
            coreStack.Add(_core);
            coreStack.Add(coreBar);
            var host = new SkUiCoreHost { HeightRequest = 60 };
            host.SetContent(coreStack);
            return Root(new SkUiVerticalStackLayout
            {
                Spacing = 12, Padding = new Thickness(12),
                Children = { _horizontal, _vertical, _bar, indeterminate, host }
            });
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            await context.DragAsync(_horizontal!, 80, 0, durationMs: 200);
            await context.TapAsync(_horizontal!);
            await context.DragAsync(_vertical!, 0, -50, durationMs: 200);
            await context.DragAsync(_core!, 60, 0, durationMs: 200);
            await context.WaitForAsync(_bar!.ProgressTo(0.5, 200));
            _ = _bar.ProgressTo(1, 5000); // still animating when the page closes
            await context.WaitAsync(200);
            // A drag still in progress when the page closes.
            await context.DragAsync(_horizontal!, -40, 0, release: false);
        }

        public override string? CheckInteraction() =>
            _changes < 4 ? $"{_changes} slider value changes, expected at least 4."
            : _drags < 1 ? "No slider drag completed."
            : _bar!.Progress < 0.49 ? $"ProgressTo did not run (progress {_bar.Progress:F2})."
            : null;
    }

    // ---- Layouts ----------------------------------------------------------------------------------------------

    private sealed class LayoutsRun : LeakScenarioRun
    {
        private SkUiContentView? _root;
        private SkUiGrid? _grid;
        private SkUiVerticalStackLayout? _stack;
        private SkUiHorizontalStackLayout? _row;
        private SkUiAbsoluteLayout? _absolute;
        private SkUiBorder? _border;
        private SkUiFlexLayout? _flex;
        private SkUiWrapLayout? _wrap;
        private SkUiHorizontalShrinkLayout? _shrink;
        private SkUiCoreWrapLayout? _coreWrap;
        private SkUiCoreHorizontalShrinkLayout? _coreShrink;

        public override View Build(LeakScenarioContext context)
        {
            _grid = new SkUiGrid { RowSpacing = 2, ColumnSpacing = 2 };
            _grid.ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(2, GridUnitType.Star))];
            _grid.RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)];
            for (var index = 0; index < 12; index++)
            {
                var cell = Text($"Cell {index}", 12);
                cell.Background = index % 2 == 0 ? LeakColors.Surface : LeakColors.SurfaceAlt;
                Grid.SetRow(cell, index / 3);
                Grid.SetColumn(cell, index % 3);
                _grid.Children.Add(cell);
            }
            _row = new SkUiHorizontalStackLayout { Spacing = 4 };
            for (var index = 0; index < 8; index++)
                _row.Children.Add(new SkUiBox { Color = LeakColors.Accent, WidthRequest = 24, HeightRequest = 24 });
            _absolute = new SkUiAbsoluteLayout { HeightRequest = 80 };
            for (var index = 0; index < 4; index++)
            {
                var box = new SkUiBox { Color = LeakColors.SampleB };
                SkUiAbsoluteLayout.SetLayoutBounds(box, new Rect(0.1 + index * 0.25, 0.5, 0.2, 0.6));
                SkUiAbsoluteLayout.SetLayoutFlags(box, AbsoluteLayoutFlags.All);
                _absolute.Children.Add(box);
            }
            _border = new SkUiBorder { Stroke = LeakColors.Accent, StrokeThickness = 2, CornerRadius = 8, Content = Text("Bordered content") };
            _flex = new SkUiFlexLayout { Wrap = FlexWrap.Wrap, JustifyContent = FlexJustify.SpaceBetween };
            for (var index = 0; index < 8; index++)
            {
                var chip = Text($"Flex {index}", 12);
                SkUiFlexLayout.SetGrow(chip, index % 2);
                _flex.Children.Add(chip);
            }
            _wrap = new SkUiWrapLayout { Spacing = 4, RowSpacing = 4 };
            for (var index = 0; index < 8; index++)
                _wrap.Children.Add(Text($"Wrap chip {index}", 12));
            _shrink = new SkUiHorizontalShrinkLayout { Spacing = 4 };
            _shrink.Children.Add(new SkUiBox { Color = LeakColors.Accent, WidthRequest = 24, HeightRequest = 24 });
            for (var index = 0; index < 3; index++)
            {
                var label = Text($"A shrinkable label that is too long for the row {index}", 12);
                label.LineBreakMode = LineBreakMode.TailTruncation;
                SkUiShrinkLayout.SetShrink(label, SkUiShrinkFactor.Auto);
                _shrink.Children.Add(label);
            }
            _coreWrap = new SkUiCoreWrapLayout().SetSpacing(4).SetRowSpacing(4);
            for (var index = 0; index < 6; index++)
                _coreWrap.Add(new SkUiCoreLabel().SetText($"Core chip {index}").SetFontSize(12));
            _coreShrink = new SkUiCoreHorizontalShrinkLayout();
            for (var index = 0; index < 3; index++)
                _coreShrink.Add(new SkUiCoreLabel().SetText($"A Core shrinkable label that is too long {index}").SetFontSize(12)
                    .SetLineBreakMode(LineBreakMode.TailTruncation), index > 0 ? SkUiShrinkFactor.Auto : SkUiShrinkFactor.None);
            var core = new SkUiCoreVerticalStackLayout().SetSpacing(4).Add(_coreWrap).Add(_coreShrink);
            _stack = new SkUiVerticalStackLayout
            {
                Spacing = 8, Padding = new Thickness(12),
                Children = { _grid, _row, _absolute, _border, _flex, _wrap, _shrink, new SkUiCoreHost().SetContent(core) }
            };
            return _root = Root(_stack);
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            foreach (var width in new[] { 360.0, 280, 420, -1 })
            {
                _root!.WidthRequest = width;
                await context.SettleAsync();
            }
            // Add and remove children: the removed ones must go while the layout lives on.
            for (var round = 0; round < 2; round++)
            {
                var added = Enumerable.Range(0, 10).Select(index => Text($"Added {round}.{index}", 12)).ToList();
                foreach (var child in added)
                    _stack!.Children.Add(child);
                await context.SettleAsync();
                foreach (var child in added)
                {
                    _stack!.Children.Remove(child);
                    context.TrackDetached(child);
                }
                await context.SettleAsync();
            }
            // Reorder, hide, change spacing and definitions.
            var first = _row!.Children[0];
            _row.Children.RemoveAt(0);
            _row.Children.Add(first);
            ((SkUiView)_row.Children[2]).IsVisible = false;
            _row.Spacing = 10;
            _grid!.ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)];
            _grid.RowSpacing = 6;
            await context.SettleAsync();
            var removedCell = (SkUiView)_grid.Children[^1];
            _grid.Children.Remove(removedCell);
            context.TrackDetached(removedCell);
            var oldContent = _border!.Content!;
            _border.Content = Text("Replaced content");
            context.TrackDetached(oldContent);
            _stack!.Padding = new Thickness(20);
            await context.SettleAsync();
            // Flex / wrap / shrink: removed children must not stay in the layouts' own per-child state.
            var flexChild = (SkUiView)_flex!.Children[1];
            _flex.Children.Remove(flexChild);
            context.TrackDetached(flexChild);
            FlexLayout.SetOrder((BindableObject)_flex.Children[0], 3);
            _flex.Direction = FlexDirection.Column;
            var wrapChild = (SkUiView)_wrap!.Children[0];
            _wrap.Children.RemoveAt(0);
            _wrap.Children.Add(wrapChild);
            var wrapRemoved = (SkUiView)_wrap.Children[2];
            _wrap.Children.Remove(wrapRemoved);
            context.TrackDetached(wrapRemoved);
            var shrinkRemoved = (SkUiView)_shrink!.Children[^1];
            _shrink.Children.Remove(shrinkRemoved);
            context.TrackDetached(shrinkRemoved);
            SkUiShrinkLayout.SetShrink((BindableObject)_shrink.Children[1], SkUiShrinkFactor.None);
            var coreWrapRemoved = _coreWrap!.Children[^1];
            _coreWrap.Remove(coreWrapRemoved);
            context.TrackDetached(coreWrapRemoved);
            var coreShrinkRemoved = _coreShrink!.Children[^1];
            _coreShrink.Remove(coreShrinkRemoved);
            context.TrackDetached(coreShrinkRemoved);
            await context.SettleAsync();
        }

        public override string? CheckInteraction()
        {
            if (_stack!.Children.Count != 8)
                return $"The stack has {_stack.Children.Count} children after adding and removing, expected 8.";
            if (_flex!.Children.Count != 7 || _wrap!.Children.Count != 7 || _shrink!.Children.Count != 3
                || _coreWrap!.Children.Count != 5 || _coreShrink!.Children.Count != 2)
                return "The flex / wrap / shrink changes were not applied.";
            if (_flex.Height <= 0 || _wrap.Height <= 0 || _shrink.Height <= 0 || _coreWrap.Frame.Height <= 0)
                return "The flex / wrap / shrink layouts were not laid out.";
            if (_grid!.Children.Count != 11 || _grid.ColumnDefinitions.Count != 3 || _grid.ColumnDefinitions[2].Width != GridLength.Auto)
                return "The grid changes were not applied.";
            if (_border!.Content is not SkUiLabel { Text: "Replaced content" })
                return "The border content was not replaced.";
            return _stack.Width > 0 && _stack.Height > 0 && _row!.Children[^1] is SkUiBox { Width: > 0 } ? null : "The layouts were not laid out.";
        }
    }

    // ---- Scrolling --------------------------------------------------------------------------------------------

    private sealed class ScrollRun : LeakScenarioRun
    {
        private SkUiScrollView? _scroll;
        private SkUiScrollView? _carousel;
        private int _scrolls;
        private int _carouselScrolls;

        public override View Build(LeakScenarioContext context)
        {
            var rows = new SkUiVerticalStackLayout { Spacing = 4, Padding = new Thickness(8) };
            var cards = new SkUiHorizontalStackLayout { Spacing = 8, Padding = new Thickness(8) };
            for (var index = 0; index < 30; index++)
                cards.Children.Add(new SkUiButton { Text = $"Card {index}", WidthRequest = 110, HeightRequest = 70, FontSize = 13 });
            _carousel = new SkUiScrollView { Orientation = ScrollOrientation.Horizontal, Content = cards, HeightRequest = 90 };
            rows.Children.Add(_carousel);
            for (var index = 0; index < 50; index++)
            {
                var row = new SkUiButton { Text = $"Row {index}", HeightRequest = 44, FontSize = 13, FillColor = index % 2 == 0 ? LeakColors.Surface : LeakColors.SurfaceAlt, TextColor = LeakColors.Ink };
                rows.Children.Add(row);
            }
            _scroll = new SkUiScrollView { Content = rows };
            _scroll.Scrolled += (_, _) => _scrolls++;
            _carousel.Scrolled += (_, _) => _carouselScrolls++;
            return Root(_scroll);
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            await context.DragAsync(_scroll!, 0, -200, durationMs: 400);
            await context.DragAsync(_scroll!, 0, -300, durationMs: 80); // fling
            await context.WaitAsync(300);
            await context.WaitForAsync(_scroll!.ScrollToAsync(0, 0, animated: true));
            await context.DragAsync(_carousel!, -250, 0, durationMs: 80); // horizontal fling in the nested carousel
            await context.WaitAsync(200);
            await context.DragAsync(_scroll!, 0, -400, durationMs: 60); // closes mid-fling
        }

        public override string? CheckInteraction() =>
            _scrolls > 0 && _carouselScrolls > 0 ? null : $"List scrolled {_scrolls} times, carousel {_carouselScrolls} times.";
    }

    // ---- Input ------------------------------------------------------------------------------------------------

    private sealed class GesturesRun : LeakScenarioRun
    {
        private readonly HashSet<string> _seen = [];
        private SkUiLabel? _target;
        private SkUiCoreBox? _coreTarget;

        public override View Build(LeakScenarioContext context)
        {
            _target = new SkUiLabel
            {
                Text = "Gesture target", HeightRequest = 180, Background = LeakColors.Surface, TextColor = LeakColors.Ink,
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center,
                SwipeDirections = SwipeDirection.Left | SwipeDirection.Right,
                TappedCommand = LeakCommands.Shared, LongPressedCommand = LeakCommands.Shared
            };
            _target.Tapped += (_, _) => _seen.Add("tap");
            _target.DoubleTapped += (_, _) => _seen.Add("double tap");
            _target.LongPressed += (_, _) => _seen.Add("long press");
            _target.Swiped += (_, _) => _seen.Add("swipe");
            _target.PinchUpdated += (_, _) => _seen.Add("pinch");
            _coreTarget = new SkUiCoreBox();
            _coreTarget.SetColor(LeakColors.SampleB).SetHeight(140);
            _coreTarget.Tapped += (_, _) => _seen.Add("core tap");
            _coreTarget.PanUpdated += (_, _) => _seen.Add("core pan");
            var host = new SkUiCoreHost { HeightRequest = 140 };
            host.SetContent(_coreTarget);
            return Root(new SkUiVerticalStackLayout { Spacing = 12, Padding = new Thickness(12), Children = { _target, host } });
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            await context.TapAsync(_target!);
            await context.WaitAsync(SkUiGestureSettings.DoubleTapTimeout.TotalMilliseconds + 50);
            await context.DoubleTapAsync(_target!);
            await context.WaitAsync(SkUiGestureSettings.DoubleTapTimeout.TotalMilliseconds + 50);
            await context.LongPressAsync(_target!);
            await context.DragAsync(_target!, 160, 0, durationMs: 80); // swipe
            await context.PinchAsync(_target!);
            await context.TapAsync(_coreTarget!);
            await context.DragAsync(_coreTarget!, 60, 40, durationMs: 300); // pan
            // A finger still down (long press pending) when the page closes.
            await context.LongPressAsync(_target!, release: false);
            await context.DragAsync(_coreTarget!, 30, 0, release: false);
        }

        public override string? CheckInteraction()
        {
            string[] expected = ["tap", "double tap", "long press", "swipe", "pinch", "core tap", "core pan"];
            var missing = expected.Where(kind => !_seen.Contains(kind)).ToList();
            return missing.Count == 0 ? null : $"Not raised: {string.Join(", ", missing)}.";
        }
    }

    // ---- Rendering --------------------------------------------------------------------------------------------

    private sealed class AnimationsRun : LeakScenarioRun
    {
        private readonly List<SkUiBox> _boxes = [];
        private SkUiCoreBox? _coreBox;
        private SkUiVerticalStackLayout? _stack;

        public override View Build(LeakScenarioContext context)
        {
            _stack = new SkUiVerticalStackLayout { Spacing = 8, Padding = new Thickness(12) };
            for (var index = 0; index < 4; index++)
            {
                var box = new SkUiBox { Color = LeakColors.Accent, HeightRequest = 40, WidthRequest = 80, Opacity = index == 0 ? 0 : 1 };
                _boxes.Add(box);
                _stack.Children.Add(box);
            }
            _stack.Children.Add(new SkUiActivityIndicator { IsRunning = true, HeightRequest = 40, WidthRequest = 40 });
            _coreBox = new SkUiCoreBox();
            _coreBox.SetColor(LeakColors.SampleA).SetHeight(40);
            var host = new SkUiCoreHost { HeightRequest = 60 };
            host.SetContent(_coreBox);
            _stack.Children.Add(host);
            return Root(_stack);
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            _fadeIn = _boxes[0].AnimateAsync(SkUiAnimatableProperty.Opacity, 1, 400); // fade-in from 0
            _ = _boxes[1].AnimateAsync(SkUiAnimatableProperty.TranslationX, 120, 5000);
            _ = _boxes[2].AnimateAsync(SkUiAnimatableProperty.Rotation, 360, 5000);
            _ = _boxes[3].AnimateAsync(SkUiAnimatableProperty.Scale, 1.5, 5000);
            _ = _coreBox!.AnimateAsync(SkUiAnimatableProperty.TranslationX, 100, 5000);
            await context.WaitAsync(250);
            // A node detached while its animation is queued and while another one runs.
            var queued = new SkUiBox { Color = LeakColors.SampleB, HeightRequest = 20 };
            _stack!.Children.Add(queued);
            await context.SettleAsync();
            _ = queued.AnimateAsync(SkUiAnimatableProperty.Opacity, 0, 5000);
            _stack.Children.Remove(queued);
            context.TrackDetached(queued);
            var rotating = _boxes[2];
            _boxes.RemoveAt(2);
            _stack.Children.Remove(rotating);
            context.TrackDetached(rotating);
            await context.WaitForAsync(_fadeIn);
        }

        private Task<bool>? _fadeIn;

        public override string? CheckInteraction() =>
            _fadeIn is { IsCompletedSuccessfully: true, Result: true } && _boxes[0].Opacity > 0.99
                ? null
                : $"The fade-in did not run to completion (opacity {_boxes[0].Opacity:F2}).";
    }

    private sealed class SurfaceReplacedRun : LeakScenarioRun
    {
        private ContentView? _holder;

        public override View Build(LeakScenarioContext context)
        {
            _holder = new ContentView { Content = Surface(hardware: true) };
            return _holder;
        }

        private static SkUiContentView Surface(bool hardware)
        {
            var stack = new SkUiVerticalStackLayout { Spacing = 8, Padding = new Thickness(12) };
            stack.Children.Add(Text(hardware ? "GPU surface" : "Software surface", 18));
            stack.Children.Add(new SkUiButton { Text = "Button", HeightRequest = 44, Command = LeakCommands.Shared });
            stack.Children.Add(new SkUiActivityIndicator { IsRunning = true, HeightRequest = 40, WidthRequest = 40 });
            var root = Root(stack);
            root.HwAccelerated = hardware;
            return root;
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            if (!context.IsDevice)
                return; // about handlers and platform surfaces; headless hosts only the first surface
            foreach (var hardware in new[] { false, true })
            {
                var old = (SkUiContentView)_holder!.Content;
                await context.TapAsync(((SkUiVerticalStackLayout)old.Content!).Children[1]);
                // The discarded surface, its handler, platform view and what the handler owned must go while the page lives.
                var discarded = LeakTracker.TrackTree(old);
                _holder.Content = Surface(hardware);
                old.DisconnectHandlers();
                await context.WaitAsync(300);
                foreach (var entry in discarded)
                    if (entry.Reference.Target is { } target)
                        context.TrackDetached(target, entry.Label + " (replaced)");
            }
        }
    }

    private sealed class MoveRun : LeakScenarioRun
    {
        private SkUiContentView? _first;
        private SkUiContentView? _second;
        private SkUiVerticalStackLayout? _moving;
        private SkUiButton? _button;
        private int _clicks;

        public override View Build(LeakScenarioContext context)
        {
            _button = new SkUiButton { Text = "Moving button", HeightRequest = 44, Command = LeakCommands.Shared };
            _button.Clicked += (_, _) => _clicks++;
            var list = new SkUiVerticalStackLayout { Spacing = 2 };
            for (var index = 0; index < 20; index++)
                list.Children.Add(Text($"Moving row {index}", 12));
            var entryHost = new SkUiMauiContentView { Content = new Entry { Placeholder = "Moving entry" }, HeightRequest = 44 };
            _moving = new SkUiVerticalStackLayout
            {
                Spacing = 6, Padding = new Thickness(8),
                Children = { _button, entryHost, new SkUiScrollView { Content = list, HeightRequest = 120 } }
            };
            _first = new SkUiContentView { Content = _moving, HeightRequest = 280, Background = Colors.White };
            _second = new SkUiContentView { HeightRequest = 280, Background = LeakColors.Surface };
            return new VerticalStackLayout { Spacing = 8, Children = { _first, _second } };
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            await context.TapAsync(_button!);
            for (var round = 0; round < 2; round++)
            {
                _first!.Content = null;
                _second!.Content = _moving;
                await context.WaitAsync(150);
                await context.TapAsync(_button!);
                _second.Content = null;
                _first.Content = _moving;
                await context.WaitAsync(150);
                await context.TapAsync(_button!);
            }
        }

        public override string? CheckInteraction() => _clicks >= 5 ? null : $"{_clicks} clicks, expected 5.";
    }

    // ---- Core -------------------------------------------------------------------------------------------------

    private sealed class CoreRun : LeakScenarioRun
    {
        private readonly List<SkUiCoreButton> _buttons = [];
        private readonly List<SkUiCoreToggleControl> _toggles = [];
        private SkUiCoreTable? _table;
        private SkUiCoreScrollView? _scroll;
        private SkUiCoreVerticalStackLayout? _rows;
        private int _clicks;

        public override View Build(LeakScenarioContext context)
        {
            var stack = new SkUiCoreVerticalStackLayout().SetSpacing(6).SetPadding(new Thickness(10));
            for (var index = 0; index < 4; index++)
            {
                var button = new SkUiCoreButton();
                button.SetText($"Core button {index}").SetHeight(40);
                button.SetCommand(LeakCommands.Shared).SetCommandParameter(index);
                button.SetClicked(() => _clicks++);
                _buttons.Add(button);
                stack.Add(button);
            }
            _toggles.Add(new SkUiCoreSwitch());
            _toggles.Add(new SkUiCoreCheckBox());
            foreach (var toggle in _toggles)
            {
                toggle.SetHeight(32).SetWidth(56);
                toggle.CheckedChanged += (_, _) => _clicks++;
                stack.Add(toggle);
            }
            _table = new SkUiCoreTable()
                .SetRowDefinitions(Enumerable.Range(0, 4).Select(_ => new SkUiCoreRowDefinition(SkUiCoreGridLength.Auto)))
                .SetColumnDefinitions(Enumerable.Range(0, 4).Select(_ => new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star)))
                .SetRowSeparatorColor(LeakColors.Border).SetColumnSeparatorColor(LeakColors.Border);
            for (var row = 0; row < 4; row++)
                for (var column = 0; column < 4; column++)
                    _table.Add(new SkUiCoreLabel().SetText($"{row},{column}").SetFontSize(12), row, column);
            _table.SetCellBackground(1, 1, LeakColors.Surface, 2, 2);
            stack.Add(_table);
            _rows = new SkUiCoreVerticalStackLayout().SetSpacing(2);
            // Fixed row heights: text measures empty on hosts without fonts (Linux CI), which would leave nothing to scroll.
            for (var index = 0; index < 30; index++)
                _rows.Add(new SkUiCoreLabel().SetText($"Core row {index}").SetFontSize(13).SetHeight(24));
            _scroll = new SkUiCoreScrollView();
            _scroll.SetContent(_rows);
            _scroll.SetHeight(200);
            stack.Add(_scroll);
            var host = new SkUiCoreHost();
            host.SetContent(stack);
            return host;
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            foreach (var button in _buttons)
                await context.TapAsync(button);
            foreach (var toggle in _toggles)
            {
                await context.TapAsync(toggle);
                await context.TapAsync(toggle);
            }
            await context.DragAsync(_scroll!, 0, -150, durationMs: 300);
            await context.DragAsync(_scroll!, 0, -200, durationMs: 60);
            await context.WaitAsync(300);
            for (var index = 0; index < 5; index++)
            {
                var row = _rows!.Children[0];
                _rows.Remove(row);
                context.TrackDetached(row);
            }
            _table!.SetColumnDefinitions([new SkUiCoreColumnDefinition(SkUiCoreGridLength.Auto), new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star),
                new SkUiCoreColumnDefinition(new SkUiCoreGridLength(60)), new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star)]);
            LeakCommands.Shared.RaiseCanExecuteChanged();
            await context.SettleAsync();
        }

        public override string? CheckInteraction() =>
            _clicks < 8 ? $"{_clicks} Core clicks / toggles, expected 8." : _scroll!.ScrollY <= 0 ? "The Core scroll view did not scroll." : null;
    }

    // ---- Native -----------------------------------------------------------------------------------------------

    private sealed class OverlaysRun : LeakScenarioRun
    {
        private SkUiScrollView? _scroll;
        private SkUiMauiContentView? _replaced;
        private Entry? _focusEntry;

        public override View Build(LeakScenarioContext context)
        {
            var stack = new SkUiVerticalStackLayout { Spacing = 8, Padding = new Thickness(12) };
            for (var index = 0; index < 4; index++)
            {
                stack.Children.Add(Text($"Entry {index}"));
                var entry = new Entry { Placeholder = $"Entry {index}" };
                _focusEntry ??= entry;
                stack.Children.Add(Overlay(entry, 44));
                stack.Children.Add(new SkUiBox { Color = LeakColors.Surface, HeightRequest = 120 });
            }
            stack.Children.Add(Overlay(new Editor { Text = "Native editor", HeightRequest = 100 }, 100));
            _replaced = Overlay(new Entry { Placeholder = "Replaced entry" }, 44);
            stack.Children.Add(_replaced);
            stack.Children.Add(new SkUiBox { Color = LeakColors.Surface, HeightRequest = 400 });
            _scroll = new SkUiScrollView { Content = stack };
            return Root(_scroll);
        }

        private static SkUiMauiContentView Overlay(View content, double height) =>
            new() { Content = content, HeightRequest = height, ScrollMode = SkUiOverlayScrollMode.Snapshot };

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            await context.DragAsync(_scroll!, 0, -250, durationMs: 400);
            await context.DragAsync(_scroll!, 0, -300, durationMs: 70); // fling: snapshots while moving
            await context.WaitAsync(600);
            await context.WaitForAsync(_scroll!.ScrollToAsync(0, 0, animated: true));
            if (context.IsDevice)
                await context.FocusAsync(_focusEntry!);
            // Replace an overlay's native content: the old control, its handler and platform view must go.
            var old = _replaced!.Content!;
            var oldHandler = old.Handler;
            var oldPlatformView = oldHandler?.PlatformView;
            _replaced.Content = new Entry { Placeholder = "New entry" };
            await context.WaitAsync(200);
            context.TrackDetached(old);
            if (oldHandler is not null)
                context.TrackDetached(oldHandler, $"{oldHandler.GetType().Name} (replaced overlay)");
            if (oldPlatformView is not null)
                context.TrackDetached(oldPlatformView, $"{oldPlatformView.GetType().Name} (replaced overlay)");
            await context.DragAsync(_scroll, 0, -200, durationMs: 60); // closes mid-fling, snapshots showing
        }

        public override string? CheckInteraction() => _scroll!.ScrollY > 0 ? null : "The overlay list did not scroll.";
    }

    private sealed class NestingRun : LeakScenarioRun
    {
        private SkUiScrollView? _list;
        private SkUiScrollView? _carousel;
        private SkUiCoreScrollView? _core;

        public override View Build(LeakScenarioContext context)
        {
            var rows = new SkUiVerticalStackLayout { Spacing = 4, Padding = new Thickness(6) };
            // Fixed row heights: text measures empty on hosts without fonts (Linux CI), which would leave nothing to scroll.
            for (var index = 0; index < 20; index++)
            {
                var row = Text($"List row {index}", 13);
                row.HeightRequest = 28;
                rows.Children.Add(row);
            }
            _list = new SkUiScrollView { Content = rows };
            var cards = new SkUiHorizontalStackLayout { Spacing = 8, Padding = new Thickness(8) };
            for (var index = 0; index < 30; index++)
                cards.Children.Add(new SkUiButton { Text = $"Card {index}", WidthRequest = 110 });
            _carousel = new SkUiScrollView { Orientation = ScrollOrientation.Horizontal, Content = cards };
            var coreRows = new SkUiCoreVerticalStackLayout().SetSpacing(4).SetPadding(new Thickness(8));
            for (var index = 0; index < 12; index++)
                coreRows.Add(new SkUiCoreLabel().SetText($"Core row {index}").SetHeight(28));
            _core = new SkUiCoreScrollView();
            _core.SetContent(coreRows);
            var stack = new VerticalStackLayout { Spacing = 12, Padding = new Thickness(12) };
            stack.Children.Add(Surface(_list, 200, hardware: true));
            stack.Children.Add(new Label { Text = "Native filler", HeightRequest = 200 });
            stack.Children.Add(Surface(_carousel, 90, hardware: false));
            stack.Children.Add(Surface(new SkUiCoreHost().SetContent(_core), 180, hardware: true));
            stack.Children.Add(new Label { Text = "More native filler", HeightRequest = 400 });
            return new ScrollView { Content = stack };
        }

        private static SkUiContentView Surface(ISkUiView content, double height, bool hardware)
        {
            var surface = new SkUiContentView { HeightRequest = height, Background = Colors.White, Content = content };
            surface.HwAccelerated = hardware;
            return surface;
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            await context.DragAsync(_list!, 0, -150, durationMs: 300);
            await context.DragAsync(_carousel!, -200, 0, durationMs: 70);
            await context.DragAsync(_core!, 0, -120, durationMs: 300);
            await context.WaitAsync(300);
        }

        public override string? CheckInteraction() =>
            _list!.ScrollY > 0 && _carousel!.ScrollX > 0 && _core!.ScrollY > 0
                ? null
                : $"Offsets: list {_list.ScrollY:F0}, carousel {_carousel!.ScrollX:F0}, core {_core!.ScrollY:F0}.";
    }

    // ---- Self-test ----------------------------------------------------------------------------------------------

    private sealed class DeliberateLeakRun : LeakScenarioRun
    {
        internal static readonly List<object> Retained = [];

        public override View Build(LeakScenarioContext context)
        {
            var root = Root(Text("This surface is retained on purpose"));
            root.AutomationId = DeliberateLeakMarker;
            Retained.Add(root);
            return root;
        }
    }
}

/// <summary>A command that outlives every page, like one on an app-wide view model.</summary>
public static class LeakCommands
{
    public static SharedCommand Shared { get; } = new();

    public sealed class SharedCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public int Executions { get; private set; }

        /// <summary>Subscribed CanExecuteChanged listeners (weak listeners of collected controls are pruned on the next raise).</summary>
        public int ListenerCount => CanExecuteChanged?.GetInvocationList().Length ?? 0;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Executions++;

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Small in-memory PNG sources (no files or network, so they work headless too).</summary>
public static class LeakImages
{
    public static ImageSource Create(SKColor color)
    {
        using var bitmap = new SKBitmap(64, 48);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(color);
            using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
            canvas.DrawCircle(32, 24, 14, paint);
        }
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = data.ToArray();
        return ImageSource.FromStream(() => new MemoryStream(bytes));
    }
}
