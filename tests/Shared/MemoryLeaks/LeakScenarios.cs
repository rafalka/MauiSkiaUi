using System.Collections.ObjectModel;
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
        new("LabelsReshaped", Controls, "Wrapped, truncated, RTL, Arabic, emoji and Simple / Shaped labels; text and width changed repeatedly; formatted text (drawn and Core) with tappable spans bound to a long-lived command, tapped, restyled and replaced; HTML text with links (both layers), tapped and changed.", () => new LabelsRun()),
        new("ShapesRestyled", Controls, "Every shape (drawn and Core) and borders shaped by them, painted with long-lived shared brushes, dash arrays and a stroke shape; brushes, gradient stops, points, path data and stroke shapes changed; a border tapped.", () => new ShapesRun()),
        new("EffectsRestyled", Controls, "Gradient backgrounds, shadows (outline and content shadows, on both layers) and clips from long-lived shared brushes, geometries and Core shadows; brushes, gradient stops, shadows and clips changed while shown; shadowed cards scrolled and animated.", () => new EffectsRun()),
        new("ContentTemplated", Controls, "Buttons with images (a long-lived shared icon, edited while shown; stream images; content layouts changed) on both layers; radio buttons with text and view content, bordered, and with a long-lived shared ControlTemplate whose presenters show the content; tapped, content swapped, the template removed and applied again; Core radio buttons in their own rows grouped by `SkUiCoreRadioButtons.Group` with a long-lived callback, tapped.", () => new ContentRun()),
        new("ImagesReloaded", Controls, "Images decoded from streams, sources swapped, reloaded, aspect changed; cached sources shared by several views (both layers), transformations, placeholders and load events, an animated GIF playing at close, a slider thumb image; a long-lived icon source shared by images that are never disposed, edited while shown.", () => new ImagesRun()),
        new("LayoutsRelayout", Layouts, "Grid, stacks, absolute, flex, wrap and shrink layouts (drawn and Core) and a border with many children; resized, children added / removed / reordered, hidden, definitions changed.", () => new LayoutsRun()),
        new("BindableLayoutItems", Layouts, "MAUI BindableLayout on a wrap layout bound to a long-lived collection and on a stack with a template selector and an empty view: items added, inserted, replaced, moved and removed, the collection cleared to the empty view and refilled, the items source swapped.", () => new BindableLayoutRun()),
        new("StatesSwitched", Layouts, "SkUiStateContainer on a grid and a stack: loading (spinner running), error (retry button with a long-lived command) and empty states switched directly and with the fade, a change rejected while one runs; hidden state views removed; automatic state change animations (one shared long-lived animation) retargeted while running; closed while one runs.", () => new StatesRun()),
        new("ContentDeferred", Layouts, "Three tabs of SkUiContentView sections that load when shown (explicit content and templates with a long-lived command, some with a delay or a fade-in): tabs switched so some load, a waiting section removed, closed with sections still waiting and delay timers pending.", () => new DeferredRun()),
        new("ExpanderToggled", Layouts, "SkUiExpander sections (explicit, template and lazy content, a hosted Entry, a long-lived command, both directions): expanded and collapsed by header taps, animated and not, reversed mid-animation, content replaced while collapsed; closed mid-collapse.", () => new ExpanderRun()),
        new("AlternateSwitched", Layouts, "SkUiAlternateContentView cards (explicit content and templates with a long-lived command, shared long-lived animations): switched directly and animated, retargeted while switching, an alternate replaced while hidden; closed mid-switch.", () => new AlternateRun()),
        new("ScrollFling", Scrolling, "Vertical list with a nested carousel: drags, flings, an animated scroll; closed mid-fling.", () => new ScrollRun()),
        new("VirtualListScrolled", Scrolling, "SkUiVirtualScrollView bound to a long-lived collection (items of different heights, recycled template views): dragged, flung, scrolled to an index, items inserted and removed while shown; closed mid-fling.", () => new VirtualListRun()),
        new("CollectionViewUsed", Scrolling, "SkUiCollectionView bound to a long-lived collection, with long-lived selection, item-tap and refresh commands, a sticky header and an empty view: items tapped to select and deselect, flung, scrolled to an item, pulled to refresh, the selected item removed, the collection emptied and refilled; closed mid-fling.", () => new CollectionViewRun()),
        new("CollectionViewGrouped", Scrolling, "SkUiCollectionView over long-lived groups (a grid of two columns, sticky collapsible group headers), with a long-lived selected-items list and load-more command: headers tapped to collapse and expand, items selected, flung, scrolled to a group, groups and items added and removed, the template replaced; closed mid-fling.", () => new CollectionViewGroupedRun()),
        new("GesturesMixed", Input, "Tap, double tap, long press, swipe, pan and pinch recognizers (drawn and Core); closed with a finger still down.", () => new GesturesRun()),
        new("AnimationsRunning", Rendering, "Render-thread animations (fade-in from 0, move, rotate, scale), a spinner; closed while they run; a node detached mid-animation.", () => new AnimationsRun()),
        new("SurfaceReplaced", Rendering, "A page replaces its GPU surface with a software one and back; the discarded surfaces are disconnected.", () => new SurfaceReplacedRun()),
        new("MovedBetweenSurfaces", Rendering, "A subtree with gestures, a scroller and a native Entry moves between two surfaces and back.", () => new MoveRun()),
        new("AccessibleFocused", Input, "Drawn and Core controls with semantic properties, read through a semantics tree that reports changes; focused by Focus() and Tab, activated by Space / Enter and screen-reader actions, a slider stepped; a focused row removed; a button given a long-lived Tag.", () => new AccessibilityRun()),
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
        private SkUiLabel? _formatted;
        private SkUiCoreLabel? _coreFormatted;
        private SkUiLabel? _html;
        private SkUiCoreLabel? _coreHtml;
        private int _coreSpanTaps;

        public override View Build(LeakScenarioContext context)
        {
            var stack = new SkUiVerticalStackLayout { Spacing = 6, Padding = new Thickness(12) };
            _formatted = new SkUiLabel { FormattedText = Formatted("Tap the link"), HorizontalTextAlignment = TextAlignment.Center, TextColor = LeakColors.Ink };
            stack.Children.Add(_formatted);
            var link = new SkUiCoreSpan("Core link").SetTextDecorations(TextDecorations.Underline);
            link.Tapped += (_, _) => _coreSpanTaps++;
            _coreFormatted = new SkUiCoreLabel().SetSpans(new SkUiCoreSpan("Core spans: ").SetFontAttributes(FontAttributes.Bold), link)
                .SetHorizontalTextAlignment(TextAlignment.Center);
            _coreFormatted.SetHeight(24);
            stack.Children.Add(new SkUiCoreHost().SetContent(_coreFormatted));
            _html = new SkUiLabel
            {
                Text = "<a href='https://example.com'><b>Open</b> the docs</a>", TextType = TextType.Html,
                HorizontalTextAlignment = TextAlignment.Center, LinkTappedCommand = LeakCommands.Shared
            };
            stack.Children.Add(_html);
            _coreHtml = new SkUiCoreLabel().SetTextType(TextType.Html).SetText("<a href='core'>Core <i>link</i></a>").SetHorizontalTextAlignment(TextAlignment.Center);
            _coreHtml.LinkTapped += (_, _) => _coreSpanTaps++;
            _coreHtml.SetHeight(24);
            stack.Children.Add(new SkUiCoreHost().SetContent(_coreHtml));
            Add(stack, new SkUiLabel { Text = Long, LineBreakMode = LineBreakMode.WordWrap });
            Add(stack, new SkUiLabel { Text = Long, LineBreakMode = LineBreakMode.TailTruncation });
            Add(stack, new SkUiLabel { Text = "مرحبا بالعالم", FlowDirection = FlowDirection.RightToLeft });
            Add(stack, new SkUiLabel { Text = "שלום עולם, hello", HorizontalTextAlignment = TextAlignment.End });
            Add(stack, new SkUiLabel { Text = "Emoji 👍🏽 🎉 fallback", TextRendering = SkUiTextRendering.Shaped });
            Add(stack, new SkUiLabel { Text = "Simple path 0123456789", TextRendering = SkUiTextRendering.Simple });
            Add(stack, new SkUiLabel { Text = "System family, bold", FontFamily = "serif", FontAttributes = FontAttributes.Bold, FontSize = 18 });
            return _root = Root(stack);
        }

        /// <summary>One tappable span (bound to the long-lived command) between two styled ones.</summary>
        private static FormattedString Formatted(string link)
        {
            var tappable = new Span { Text = link, TextDecorations = TextDecorations.Underline, FontAttributes = FontAttributes.Bold };
            tappable.GestureRecognizers.Add(new TapGestureRecognizer { Command = LeakCommands.Shared, CommandParameter = link });
            return new FormattedString
            {
                Spans = { new Span { Text = "Spans: ", FontSize = 18 }, tappable, new Span { Text = " (formatted)", FontAttributes = FontAttributes.Italic } }
            };
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
            await context.TapAsync(_formatted!);
            await context.TapAsync(_coreFormatted!);
            var spans = _formatted!.FormattedText!.Spans;
            spans[0].TextColor = LeakColors.Border;
            spans[2].FontSize = 22;
            spans.Add(new Span { Text = " added" });
            await context.SettleAsync();
            var replaced = _formatted.FormattedText;
            _formatted.FormattedText = Formatted("Replaced link");
            context.TrackDetached(replaced, "replaced FormattedString");
            await context.SettleAsync();
            await context.TapAsync(_formatted);
            _coreFormatted!.Spans[0].SetTextColor(LeakColors.Border);
            _coreFormatted.AddSpan(new SkUiCoreSpan(" more"));
            await context.SettleAsync();
            await context.TapAsync(_html!);
            await context.TapAsync(_coreHtml!);
            _html!.Text = "<p>Changed <u>markup</u></p><ul><li>one</li><li><a href='b'>two</a></li></ul>";
            _coreHtml!.SetText("<s>gone</s> <a href='c'>again</a>");
            await context.SettleAsync();
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

    /// <summary>Resources that outlive every screen, as app-level styles and resources do.</summary>
    private static class LeakShapes
    {
        public static readonly SolidColorBrush Solid = new(LeakColors.Accent);
        public static readonly LinearGradientBrush Gradient = new(
            [new GradientStop(LeakColors.SampleA, 0), new GradientStop(LeakColors.SampleB, 1)], new Point(0, 0), new Point(1, 1));
        public static readonly Microsoft.Maui.Controls.Shapes.RoundRectangle StrokeShape = new() { CornerRadius = 12 };
        public static readonly DoubleCollection Dashes = [4, 2];
    }

    private sealed class ShapesRun : LeakScenarioRun
    {
        private readonly List<SkUiShape> _shapes = [];
        private SkUiBorder? _border;
        private SkUiPolygon? _polygon;
        private SkUiPath? _path;
        private SkUiCoreBorder? _coreBorder;
        private int _taps;

        public override View Build(LeakScenarioContext context)
        {
            var stack = new SkUiVerticalStackLayout { Spacing = 8, Padding = new Thickness(12) };
            var row = new SkUiHorizontalStackLayout { Spacing = 6, HeightRequest = 48 };
            _polygon = new SkUiPolygon([new(0, 40), new(20, 0), new(40, 40)]);
            _path = new SkUiPath { Aspect = Microsoft.Maui.Controls.Stretch.Uniform, WidthRequest = 48 }.SetData("M 0,20 C 10,0 30,40 40,20");
            SkUiShape[] shapes =
            [
                new SkUiEllipse { WidthRequest = 40 }, new SkUiRectangle { WidthRequest = 40, RadiusX = 6 },
                new SkUiRoundRectangle { WidthRequest = 40, CornerRadius = 10 }, new SkUiLine(0, 0, 40, 40),
                _polygon, new SkUiPolyline([new(0, 0), new(20, 40), new(40, 0)]), _path,
            ];
            foreach (var shape in shapes)
            {
                shape.Fill = LeakShapes.Gradient;
                shape.Stroke = LeakShapes.Solid;
                shape.StrokeThickness = 2;
                shape.StrokeDashArray = LeakShapes.Dashes;
                _shapes.Add(shape);
                row.Children.Add(shape);
            }
            _border = new SkUiBorder
            {
                StrokeShape = LeakShapes.StrokeShape, Stroke = LeakShapes.Gradient, StrokeThickness = 3, StrokeDashArray = LeakShapes.Dashes,
                Padding = new Thickness(8), Content = Text("Shaped border"),
            };
            _border.Tapped += (_, _) => _taps++;
            _coreBorder = new SkUiCoreBorder().SetStrokeShape(new SkUiCoreEllipse()).SetStroke(LeakColors.Accent).SetStrokeThickness(3)
                .SetContent(new SkUiCorePath("M 0,0 L 20,10 L 0,20 Z").SetFill(LeakColors.SampleA));
            _coreBorder.SetHeight(60);
            stack.Children.Add(row);
            stack.Children.Add(_border);
            stack.Children.Add(new SkUiCoreHost().SetContent(_coreBorder));
            return Root(stack);
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            await context.TapAsync(_border!);
            foreach (var shape in _shapes)
                shape.Fill = shape.Fill == LeakShapes.Gradient ? LeakShapes.Solid : LeakShapes.Gradient;
            LeakShapes.Gradient.GradientStops[0].Color = LeakColors.Surface; // a shared brush edited while shown
            _polygon!.Points.Add(new Point(30, 10));
            _path!.SetData("M 0,0 L 40,40 M 40,0 L 0,40");
            await context.SettleAsync();
            var replaced = new SkUiRoundRectangle { CornerRadius = 4 };
            _border!.StrokeShape = replaced;
            replaced.CornerRadius = 20;
            await context.SettleAsync();
            _border.StrokeShape = LeakShapes.StrokeShape;
            context.TrackDetached(replaced, "replaced stroke shape");
            _coreBorder!.SetStrokeShape(new SkUiCoreRoundRectangle().SetCornerRadius(10)).SetStrokeDashArray(2, 2);
            await context.SettleAsync();
            await context.TapAsync(_border);
            LeakShapes.Gradient.GradientStops[0].Color = LeakColors.SampleA;
        }

        public override string? CheckInteraction()
        {
            if (_taps != 2)
                return $"The border was tapped {_taps} times, expected 2.";
            return _shapes.Any(shape => shape.Width <= 0) ? "A shape was not laid out." : null;
        }
    }

    /// <summary>An app-level radio button template, as <c>&lt;ControlTemplate x:Key="Tile"&gt;</c> in a style: it outlives every screen.</summary>
    private static readonly ControlTemplate SharedRadioTemplate = new(() =>
    {
        var root = new SkUiBorder { Stroke = LeakColors.Accent, StrokeThickness = 2, Padding = new Thickness(6), Content = new SkUiContentPresenter() };
        var isChecked = new VisualState { Name = SkUiRadioButton.CheckedVisualState };
        isChecked.Setters.Add(new Setter { Property = SkUiBorder.StrokeThicknessProperty, Value = 4d });
        var isUnchecked = new VisualState { Name = SkUiRadioButton.UncheckedVisualState };
        var group = new VisualStateGroup { Name = "CheckedStates" };
        group.States.Add(isChecked);
        group.States.Add(isUnchecked);
        VisualStateManager.SetVisualStateGroups(root, [group]);
        return root;
    });

    private sealed class ContentRun : LeakScenarioRun
    {
        private readonly List<SkUiButton> _buttons = [];
        private readonly List<SkUiRadioButton> _radios = [];
        private SkUiCoreButton? _coreButton;
        private readonly SkUiCoreRadioButton[] _coreRadios = [new(), new(), new()];
        private int _clicks;

        public override View Build(LeakScenarioContext context)
        {
            var stack = new SkUiVerticalStackLayout { Spacing = 8, Padding = new Thickness(12) };
            var buttons = new SkUiHorizontalStackLayout { Spacing = 8 };
            foreach (var (image, position) in new (ImageSource, Button.ButtonContentLayout.ImagePosition)[]
                     {
                         (SharedIcon, Button.ButtonContentLayout.ImagePosition.Left), (LeakImages.Create(SKColors.Teal), Button.ButtonContentLayout.ImagePosition.Top),
                     })
            {
                var button = new SkUiButton { Text = "Icon", ImageSource = image, ContentLayout = new Button.ButtonContentLayout(position, 6), Command = LeakCommands.Shared };
                button.Clicked += (_, _) => _clicks++;
                _buttons.Add(button);
                buttons.Children.Add(button);
            }
            _coreButton = new SkUiCoreButton().SetImageSource(SkUiImageSource.FromStream(LeakImages.CachedStream(SKColors.Gold), "leak-gold"));
            _coreButton.SetText("Core");
            buttons.Children.Add(new SkUiCoreHost().SetContent(_coreButton));
            stack.Children.Add(buttons);

            var group = new SkUiVerticalStackLayout { Spacing = 4 };
            RadioButtonGroup.SetGroupName(group, "leak-content");
            _radios.Add(new SkUiRadioButton { Content = "Text content", Value = 1, BorderColor = LeakColors.Accent, BorderWidth = 1, CornerRadius = 6, Padding = new Thickness(4) });
            _radios.Add(new SkUiRadioButton { Content = Text("View content"), Value = 2 });
            _radios.Add(new SkUiRadioButton { Content = Text("Templated"), Value = 3, ControlTemplate = SharedRadioTemplate });
            _radios.Add(new SkUiRadioButton { Content = "Templated text", Value = 4, ControlTemplate = SharedRadioTemplate });
            foreach (var radio in _radios)
                group.Children.Add(radio);
            stack.Children.Add(group);

            // Core radio buttons in their own rows, grouped by the helper; its callback belongs to a long-lived object
            // and the group is never disposed: neither may keep the screen alive.
            var coreColumn = new SkUiCoreVerticalStackLayout();
            foreach (var radio in _coreRadios)
            {
                var row = new SkUiCoreHorizontalStackLayout().SetSpacing(6);
                row.Add(radio);
                row.Add(new SkUiCoreLabel().SetText("Core option"));
                coreColumn.Add(row);
            }
            SkUiCoreRadioButtons.Group(_coreRadios, LeakSelection.Shared.Record);
            stack.Children.Add(new SkUiCoreHost().SetContent(coreColumn));

            return Root(stack);
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            foreach (var button in _buttons)
                await context.WaitForAsync(button.ImageLoadingTask);
            await context.WaitForAsync(_coreButton!.ImageLoadingTask);
            await context.TapAsync(_buttons[0]);
            SharedIcon.Glyph = SharedIcon.Glyph == "+" ? "-" : "+"; // the shared source edited while shown
            _buttons[1].ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Right, 10);
            _buttons[1].ImageSource = LeakImages.Create(SKColors.Purple);
            await context.WaitForAsync(_buttons[1].ImageLoadingTask);
            foreach (var radio in _radios)
                await context.TapAsync(radio);
            await context.SettleAsync();

            var replaced = Text("Replaced");
            _radios[1].Content = replaced;
            _radios[1].Content = "Text again";
            context.TrackDetached(replaced, "replaced radio content");
            _radios[2].ControlTemplate = null; // the content moves back beside the circle
            await context.SettleAsync();
            _radios[2].ControlTemplate = SharedRadioTemplate;
            _radios[3].Content = Text("Templated view");
            await context.SettleAsync();
            foreach (var radio in _coreRadios)
                await context.TapAsync(radio);
            await context.TapAsync(_buttons[0]);
        }

        public override string? CheckInteraction()
        {
            if (_clicks != 2)
                return $"The image button was clicked {_clicks} times, expected 2.";
            if (_buttons.Concat<SkUiView>(_radios).Any(view => view.Width <= 0))
                return "A button or radio button was not laid out.";
            if (_radios[2].TemplateRoot is null || !_radios[3].IsChecked)
                return "The template was not applied again, or the last radio button is not checked.";
            if (!_coreRadios.Select(radio => radio.IsChecked).SequenceEqual([false, false, true]))
                return "The grouped Core radio buttons did not exclude each other.";
            return _buttons.Any(button => button.ImageLoadingTask is not { IsCompletedSuccessfully: true }) ? "A button image did not load." : null;
        }
    }

    /// <summary>App-level resources for <see cref="EffectsRun"/>: they outlive every screen (MAUI's <c>Shadow</c> is per view, its brush shared).</summary>
    private static class LeakEffects
    {
        public static readonly LinearGradientBrush Background = new(
            [new GradientStop(LeakColors.SampleA, 0), new GradientStop(LeakColors.SampleB, 1)], new Point(0, 0), new Point(1, 0));
        public static readonly SolidColorBrush ShadowBrush = new(Colors.Black);
        public static readonly Microsoft.Maui.Controls.Shapes.EllipseGeometry Clip = new() { Center = new Point(24, 24), RadiusX = 24, RadiusY = 24 };
        public static readonly SkUiCoreShadow CoreShadow = new(Colors.Black, new Point(0, 4), 10, 0.3f);
        public static readonly SkUiCoreEllipse CoreClip = new();
    }

    private sealed class EffectsRun : LeakScenarioRun
    {
        private readonly List<SkUiView> _cards = [];
        private SkUiScrollView? _scroll;
        private SkUiLabel? _clipped;
        private SkUiCoreBorder? _coreCard;

        public override View Build(LeakScenarioContext context)
        {
            var stack = new SkUiVerticalStackLayout { Spacing = 12, Padding = new Thickness(16) };
            for (var index = 0; index < 6; index++)
            {
                SkUiView card = index % 2 == 0
                    ? new SkUiBorder { StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 }, Background = LeakEffects.Background, HeightRequest = 56, Content = Text($"Card {index}") }
                    : new SkUiEllipse { Fill = LeakEffects.Background, StrokeThickness = 0, HeightRequest = 56 }; // a content shadow
                card.Shadow = new Shadow { Brush = LeakEffects.ShadowBrush, Offset = new Point(0, 4), Radius = 10, Opacity = 0.3f };
                _cards.Add(card);
                stack.Children.Add(card);
            }
            _clipped = new SkUiLabel { Text = "AB", WidthRequest = 48, HeightRequest = 48, Background = LeakEffects.Background, Clip = LeakEffects.Clip };
            stack.Children.Add(_clipped);
            _coreCard = new SkUiCoreBorder().SetCornerRadius(new CornerRadius(12)).SetContent(new SkUiCoreLabel().SetText("Core card"));
            _coreCard.SetBackground(new SolidPaint(LeakColors.Surface)).SetShadow(LeakEffects.CoreShadow).SetHeight(56);
            var coreClipped = new SkUiCoreBox().SetColor(LeakColors.Accent).SetClip(LeakEffects.CoreClip).SetWidth(48).SetHeight(48);
            stack.Children.Add(new SkUiCoreHost().SetContent(new SkUiCoreVerticalStackLayout().Add(_coreCard).Add(coreClipped)));
            _scroll = new SkUiScrollView { Content = stack };
            return Root(_scroll);
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            await context.SettleAsync();
            LeakEffects.Background.GradientStops[0].Color = LeakColors.Surface; // shared brushes edited while shown
            LeakEffects.ShadowBrush.Color = LeakColors.Accent;
            LeakEffects.Clip.RadiusX = 20;
            await context.SettleAsync();
            _scroll!.ScrollTo(0, 120);
            await context.WaitForAsync(_cards[1].AnimateAsync(SkUiAnimatableProperty.TranslationX, 12, 120));
            _cards[0].Shadow = new Shadow { Brush = LeakEffects.Background, Radius = 4 };
            _clipped!.Clip = null;
            _coreCard!.SetShadow(new SkUiCoreShadow(LeakColors.Accent, new Point(2, 2), 4));
            await context.SettleAsync();
            _clipped.Clip = LeakEffects.Clip;
            LeakEffects.Background.GradientStops[0].Color = LeakColors.SampleA;
            LeakEffects.ShadowBrush.Color = Colors.Black;
            LeakEffects.Clip.RadiusX = 24;
        }

        public override string? CheckInteraction() =>
            _cards.Any(card => card.Width <= 0) ? "A card was not laid out." : null;
    }

    /// <summary>An app-level image resource, as <c>&lt;FontImageSource x:Key="Icon" …/&gt;</c>: it outlives every screen.</summary>
    private static readonly FontImageSource SharedIcon = new() { Glyph = "+", Size = 20, Color = LeakColors.Accent };

    private sealed class ImagesRun : LeakScenarioRun
    {
        private readonly List<SkUiImage> _images = [];
        private readonly List<SkUiCoreImage> _coreImages = [];
        private SkUiSlider? _slider;
        private int _finished;

        public override View Build(LeakScenarioContext context)
        {
            var grid = new SkUiGrid { RowSpacing = 8, ColumnSpacing = 8, Padding = new Thickness(12) };
            grid.ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)];
            grid.RowDefinitions = [new RowDefinition(new GridLength(120)), new RowDefinition(new GridLength(120)), new RowDefinition(new GridLength(80)), new RowDefinition(new GridLength(40))];
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

            // One cached source in three views (memory-cache leases), circle-cropped, plus a playing animation.
            var shared = LeakImages.Cached(SKColors.Teal);
            var row = new SkUiHorizontalStackLayout { Spacing = 8 };
            for (var index = 0; index < 2; index++)
            {
                var avatar = new SkUiImage { Source = shared, WidthRequest = 64, HeightRequest = 64 };
                avatar.Transformations.Add(new SkUiCircleTransformation(2, Colors.White));
                _images.Add(avatar);
                row.Children.Add(avatar);
            }
            var animated = new SkUiImage { Source = ImageSource.FromStream(() => new MemoryStream(LeakImages.AnimatedGif())), IsAnimationPlaying = true, WidthRequest = 64, HeightRequest = 64, Aspect = Aspect.Fill };
            // Placeholders (an animated loading one) and load events on the first image.
            _images[0].LoadingPlaceholder = ImageSource.FromStream(() => new MemoryStream(LeakImages.AnimatedGif()));
            _images[0].ErrorPlaceholder = LeakImages.Create(SKColors.Black);
            _images[0].LoadingFinished += (_, args) => _finished += args.IsSuccess ? 1 : 0;
            _images.Add(animated);
            row.Children.Add(animated);
            var core = new SkUiCoreImage().SetTransformations(new SkUiRoundedTransformation(12)).SetSourceStream(LeakImages.CachedStream(SKColors.Teal), "leak-teal");
            core.SetWidth(64).SetHeight(64);
            _coreImages.Add(core);
            row.Children.Add(new SkUiCoreHost().SetContent(core));
            Grid.SetRow(row, 2);
            Grid.SetColumnSpan(row, 2);
            grid.Children.Add(row);

            // The shared icon: images and an image button that are never disposed, and a slider thumb.
            for (var index = 0; index < 2; index++)
                row.Children.Add(new SkUiImage { Source = SharedIcon, WidthRequest = 24, HeightRequest = 24 });
            row.Children.Add(new SkUiImageButton { Source = SharedIcon, WidthRequest = 32, HeightRequest = 32 });

            _slider = new SkUiSlider { Value = 0.5, ThumbImageSource = SharedIcon };
            Grid.SetRow(_slider, 3);
            Grid.SetColumnSpan(_slider, 2);
            grid.Children.Add(_slider);
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
            foreach (var core in _coreImages)
                await context.WaitForAsync(core.LoadingTask);
            _coreImages[0].SetTransformations(new SkUiGrayscaleTransformation());
            await context.WaitForAsync(_coreImages[0].LoadingTask);
            SharedIcon.Glyph = SharedIcon.Glyph == "+" ? "-" : "+"; // the shared source edited while shown
            await context.SettleAsync();
            _slider!.ThumbImageSource = LeakImages.Create(SKColors.Navy);
            _slider.ThumbImageSource = SharedIcon;
            _reload = _images[0].ReloadAsync();
            await context.WaitForAsync(_reload);
        }

        private Task? _reload;

        public override string? CheckInteraction()
        {
            if (_reload is not { IsCompletedSuccessfully: true })
                return "The reload did not complete.";
            var failed = _images.Where(image => !image.LoadingTask.IsCompletedSuccessfully || image.LoadError is not null || image.IsLoading).ToList();
            if (_finished == 0)
                return "The first image raised no successful LoadingFinished.";
            if (_coreImages.FirstOrDefault(image => image.LoadError is not null || image.IsLoading) is { } core)
                return $"A Core image did not load: {core.LoadError?.Message ?? "still loading"}.";
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
            // Width, not height: text measures empty on hosts without fonts (Linux CI), so label-only layouts are 0 tall.
            if (_flex.Width <= 0 || _wrap.Width <= 0 || _shrink.Width <= 0 || _coreWrap.Frame.Width <= 0 || _coreShrink.Frame.Width <= 0)
                return "The flex / wrap / shrink layouts were not laid out.";
            if (_grid!.Children.Count != 11 || _grid.ColumnDefinitions.Count != 3 || _grid.ColumnDefinitions[2].Width != GridLength.Auto)
                return "The grid changes were not applied.";
            if (_border!.Content is not SkUiLabel { Text: "Replaced content" })
                return "The border content was not replaced.";
            return _stack.Width > 0 && _stack.Height > 0 && _row!.Children[^1] is SkUiBox { Width: > 0 } ? null : "The layouts were not laid out.";
        }
    }

    // ---- Scrolling --------------------------------------------------------------------------------------------

    private sealed class BindableLayoutRun : LeakScenarioRun
    {
        private readonly ObservableCollection<string> _rows = [];
        private SkUiWrapLayout? _chips;
        private SkUiVerticalStackLayout? _list;
        private string? _problem;

        public override View Build(LeakScenarioContext context)
        {
            LeakItems.Shared.Clear();
            for (var index = 0; index < 6; index++)
                LeakItems.Shared.Add($"Chip {index}");
            _chips = new SkUiWrapLayout { Spacing = 4, RowSpacing = 4 };
            BindableLayout.SetItemTemplate(_chips, new DataTemplate(() =>
            {
                var chip = Text("", 12);
                chip.SetBinding(SkUiLabel.TextProperty, Binding.SelfPath);
                return chip;
            }));
            BindableLayout.SetItemsSource(_chips, LeakItems.Shared);
            _list = new SkUiVerticalStackLayout { Spacing = 2 };
            BindableLayout.SetItemTemplateSelector(_list, new RowTemplateSelector());
            BindableLayout.SetEmptyView(_list, Text("Nothing here"));
            BindableLayout.SetItemsSource(_list, _rows);
            return Root(new SkUiVerticalStackLayout { Spacing = 8, Padding = new Thickness(12), Children = { _chips, _list } });
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            // Removed item views must go while the layouts and the long-lived collection live on.
            void Removed(SkUiLayout layout, int index) => context.TrackDetached(layout.Children[index], $"item {index} of {layout.GetType().Name}");
            for (var round = 0; round < 2; round++)
            {
                for (var index = 0; index < 4; index++)
                    LeakItems.Shared.Add($"Added {round}.{index}");
                LeakItems.Shared.Insert(1, $"Inserted {round}");
                await context.SettleAsync();
                Removed(_chips!, 1);
                LeakItems.Shared.RemoveAt(1);
                LeakItems.Shared.Move(0, 3);
                LeakItems.Shared[2] = $"Replaced {round}"; // same template: the view is reused
                for (var index = 0; index < 3; index++)
                    _rows.Add(index % 2 == 0 ? $"Row {round}.{index}" : $"Accent {round}.{index}");
                await context.SettleAsync();
                foreach (var child in _list!.Children)
                    context.TrackDetached(child, "row before clear");
                _rows.Clear();
                await context.SettleAsync();
                if (_list.Children.Count != 1 || BindableLayout.GetEmptyView(_list) != _list.Children[0])
                    _problem = "the empty view was not shown after the rows were cleared";
            }
            // A new source reuses the first views (same template) and removes the rest.
            for (var index = 2; index < _chips!.Children.Count; index++)
                Removed(_chips, index);
            BindableLayout.SetItemsSource(_chips, new[] { "Swapped A", "Swapped B" });
            _rows.Add("Row after empty");
            await context.SettleAsync();
            if (_chips.Children.Count != 2 || ((SkUiLabel)_chips.Children[1]).Text != "Swapped B")
                _problem ??= "swapping the items source did not regenerate the chips";
        }

        public override string? CheckInteraction() => _problem;

        private sealed class RowTemplateSelector : DataTemplateSelector
        {
            private readonly DataTemplate _plain = Template(LeakColors.Surface);
            private readonly DataTemplate _accent = Template(LeakColors.SurfaceAlt);

            private static DataTemplate Template(Color background) => new(() =>
            {
                var row = Text("", 12);
                row.Background = background;
                row.SetBinding(SkUiLabel.TextProperty, Binding.SelfPath);
                return row;
            });

            protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
                item is string text && text.StartsWith("Accent", StringComparison.Ordinal) ? _accent : _plain;
        }
    }

    private sealed class StatesRun : LeakScenarioRun
    {
        private SkUiGrid? _grid;
        private SkUiVerticalStackLayout? _stack;
        private string? _problem;

        // Long-lived, shared by every run (and frozen by the first one).
        private static readonly SkUiViewAnimation StatesSlideIn = new([new(SkUiAnimatableProperty.TranslationY, from: 20), new(SkUiAnimatableProperty.Opacity, from: 0)], 160, Easing.CubicOut);

        private static T State<T>(T view, string key) where T : SkUiView
        {
            SkUiStateView.SetStateKey(view, key);
            return view;
        }

        public override View Build(LeakScenarioContext context)
        {
            _grid = new SkUiGrid { RowDefinitions = [new RowDefinition(40), new RowDefinition(40)], ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)] };
            for (var index = 0; index < 4; index++)
            {
                var cell = Text($"Cell {index}", 12);
                Grid.SetRow(cell, index / 2);
                Grid.SetColumn(cell, index % 2);
                _grid.Children.Add(cell);
            }
            var views = SkUiStateContainer.GetStateViews(_grid);
            views.Add(State(new SkUiActivityIndicator { IsRunning = true, HeightRequest = 32, WidthRequest = 32 }, "Loading"));
            views.Add(State(new SkUiButton { Text = "Retry", Command = LeakCommands.Shared }, "Error"));
            views.Add(State(Text("Nothing here"), "Empty"));
            _stack = new SkUiVerticalStackLayout { Spacing = 4, Children = { Text("Row A"), Text("Row B") } };
            SkUiStateContainer.GetStateViews(_stack).Add(State(Text("Loading rows"), "Loading"));
            return Root(new SkUiVerticalStackLayout { Spacing = 8, Padding = new Thickness(12), Children = { _grid, _stack } });
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            foreach (var state in new[] { "Loading", "Error", "Empty", null, "Error" })
            {
                SkUiStateContainer.SetCurrentState(_grid!, state);
                await context.SettleAsync();
            }
            await context.TapAsync(SkUiStateContainer.GetStateViews(_grid!)[1]);
            var fade = SkUiStateContainer.ChangeStateWithAnimation(_grid!, "Loading");
            try
            {
                SkUiStateContainer.SetCurrentState(_grid!, "Empty");
                _problem = "a state change was accepted while an animated one ran";
            }
            catch (SkUiStateContainerException)
            {
            }
            await context.WaitForAsync(fade);
            await context.WaitForAsync(SkUiStateContainer.ChangeStateWithAnimation(_stack!, "Loading"));
            await context.WaitForAsync(SkUiStateContainer.ChangeStateWithAnimation(_stack!, null));
            // Hidden state views that are removed must go while the layout lives on. (Edit the list: a list set with
            // SetStateViews replaces the default one, which MAUI keeps with the layout.)
            var stackStates = SkUiStateContainer.GetStateViews(_stack!);
            foreach (var view in stackStates)
                context.TrackDetached(view, $"removed state view {SkUiStateView.GetStateKey(view)}");
            stackStates.Clear();
            stackStates.Add(State(Text("New loading"), "Loading"));
            SkUiStateContainer.SetCurrentState(_stack!, "Loading");
            await context.SettleAsync();
            // Automatic state change animations, retargeted while running and left running at close.
            SkUiStateContainer.SetBeforeStateChangeAnimation(_stack!, SkUiViewAnimation.FadeOut(120));
            SkUiStateContainer.SetAfterStateChangeAnimation(_stack!, StatesSlideIn);
            SkUiStateContainer.SetCurrentState(_stack!, null);
            SkUiStateContainer.SetCurrentState(_stack!, "Loading");
            SkUiStateContainer.SetCurrentState(_stack!, null);
            for (var wait = 0; wait < 60 && !SkUiStateContainer.GetCanStateChange(_stack!); wait++)
                await context.SettleAsync();
            if (SkUiStateContainer.GetCurrentState(_stack!) is not null || _stack!.Children.Count != 2)
                _problem ??= "the stack did not end with its own children after automatic changes";
            SkUiStateContainer.SetCurrentState(_stack!, "Loading");
            if (SkUiStateContainer.GetCurrentState(_grid!) != "Loading" || _grid!.Children.Count != 1)
                _problem ??= "the grid did not end in the loading state";
        }

        public override string? CheckInteraction() => _problem;
    }

    private sealed class DeferredRun : LeakScenarioRun
    {
        private readonly List<SkUiVerticalStackLayout> _tabs = [];
        private readonly List<SkUiContentView> _sections = [];
        private string? _problem;

        // Long-lived, shared by every run (and frozen by the first one).
        private static readonly SkUiViewAnimation SectionFadeIn = SkUiViewAnimation.FadeIn(150);

        public override View Build(LeakScenarioContext context)
        {
            var host = new SkUiGrid();
            for (var tab = 0; tab < 3; tab++)
            {
                var pane = new SkUiVerticalStackLayout { Spacing = 8, Padding = new Thickness(12), IsVisible = tab == 0 };
                for (var index = 0; index < 4; index++)
                {
                    var label = $"Tab {tab} section {index}";
                    var section = new SkUiContentView
                    {
                        ContentLoading = SkUiContentLoading.WhenShown,
                        HeightRequest = 90,
                        ContentLoadingDelay = index % 2 == 1 ? TimeSpan.FromMilliseconds(120) : TimeSpan.Zero,
                        ContentLoadedAnimation = index % 2 == 0 ? SectionFadeIn : null
                    };
                    if (index % 2 == 0)
                        section.Content = new SkUiBorder { Stroke = LeakColors.Accent, StrokeThickness = 1, Content = Text(label) };
                    else
                        section.ContentTemplate = new DataTemplate(() => new SkUiVerticalStackLayout
                        {
                            Children = { Text(label), new SkUiButton { Text = "Open", Command = LeakCommands.Shared, CommandParameter = label } }
                        });
                    _sections.Add(section);
                    pane.Children.Add(section);
                }
                _tabs.Add(pane);
                host.Children.Add(pane);
            }
            return Root(host);
        }

        private void Show(int tab)
        {
            for (var index = 0; index < _tabs.Count; index++)
                _tabs[index].IsVisible = index == tab;
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            await context.SettleAsync();
            await context.WaitAsync(200); // the first tab's delays elapse
            Show(1);
            await context.SettleAsync(); // passed through: tab 1's delayed sections never load
            // A waiting section (in the tab never shown) must go once removed, with its IsShownChanged handler.
            var waiting = _tabs[2].Children[0] as SkUiContentView;
            if (waiting is null || waiting.IsContentLoaded)
            {
                _problem = "a section of a tab never shown loaded";
                return;
            }
            _sections.Remove(waiting);
            _tabs[2].Children.Remove(waiting);
            context.TrackDetached(waiting, "removed waiting section");
            context.TrackDetached(waiting.Content!, "removed waiting section's content");
            Show(0); // closed with tab 1's delay timers pending
        }

        public override string? CheckInteraction()
        {
            if (_problem is not null)
                return _problem;
            if (!_sections.Take(4).All(section => section.IsContentLoaded))
                return "the shown tab's sections did not all load";
            return _sections.Skip(8).Any(section => section.IsContentLoaded) ? "a section of a tab never shown loaded" : null;
        }
    }

    private sealed class AlternateRun : LeakScenarioRun
    {
        private readonly List<SkUiAlternateContentView> _cards = [];
        private string? _problem;

        // Long-lived, shared by every run (and frozen by the first one).
        private static readonly SkUiViewAnimation CardOut = SkUiViewAnimation.FadeOut(100);
        private static readonly SkUiViewAnimation CardIn = new([new(SkUiAnimatableProperty.TranslationY, from: 16), new(SkUiAnimatableProperty.Opacity, from: 0)], 140, Easing.CubicOut);

        public override View Build(LeakScenarioContext context)
        {
            var stack = new SkUiVerticalStackLayout { Spacing = 8, Padding = new Thickness(12) };
            for (var index = 0; index < 4; index++)
            {
                var label = $"Card {index}";
                var card = new SkUiAlternateContentView
                {
                    HeightRequest = 70,
                    ShowsAlternate = false,
                    Content = new SkUiBorder { Stroke = LeakColors.Accent, StrokeThickness = 1, Content = Text(label) },
                    AlternateContentTemplate = new DataTemplate(() => new SkUiHorizontalStackLayout
                    {
                        Children = { Text($"Editing {label}"), new SkUiButton { Text = "Save", Command = LeakCommands.Shared, CommandParameter = label } }
                    }),
                    BeforeStateChangeAnimation = index % 2 == 0 ? CardOut : null,
                    AfterStateChangeAnimation = index % 2 == 0 ? CardIn : null
                };
                _cards.Add(card);
                stack.Children.Add(card);
            }
            return Root(stack);
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            await context.SettleAsync();
            foreach (var card in _cards)
                card.ShowsAlternate = true;
            _cards[0].ShowsAlternate = null; // retargeted while switching
            _cards[0].ShowsAlternate = true;
            for (var wait = 0; wait < 60 && _cards.Any(card => card.IsSwitching); wait++)
                await context.SettleAsync();
            if (_cards.Any(card => card.AlternateContent is null || ((Element)card.AlternateContent).Parent != card))
                _problem = "a card does not show its alternate content after switching";
            await context.TapAsync(((SkUiHorizontalStackLayout)_cards[1].AlternateContent!).Children[1]);

            // An alternate replaced while hidden must go while the card lives on.
            _cards[2].ShowsAlternate = false;
            for (var wait = 0; wait < 60 && _cards[2].IsSwitching; wait++)
                await context.SettleAsync(); // a running switch holds the view it animates until it ends
            var replaced = _cards[2].AlternateContent!;
            _cards[2].AlternateContent = Text("Replaced");
            context.TrackDetached(replaced, "replaced alternate content");
            await context.SettleAsync();
            _cards[0].ShowsAlternate = false; // closed mid-switch
        }

        public override string? CheckInteraction() => _problem;
    }

    private sealed class ExpanderRun : LeakScenarioRun
    {
        private readonly List<SkUiExpander> _sections = [];
        private string? _problem;

        public override View Build(LeakScenarioContext context)
        {
            var stack = new SkUiVerticalStackLayout { Spacing = 8, Padding = new Thickness(12) };
            for (var index = 0; index < 4; index++)
            {
                var label = $"Section {index}";
                var section = new SkUiExpander
                {
                    Header = new SkUiBorder { Stroke = LeakColors.Accent, StrokeThickness = 1, Padding = new Thickness(8), Content = Text(label) },
                    AnimationLength = index == 3 ? 0u : 120u,
                    Direction = index == 3 ? SkUiExpandDirection.Up : SkUiExpandDirection.Down,
                    LazyContentExpansion = index is 1 or 2,
                    Command = LeakCommands.Shared,
                    CommandParameter = label
                };
                switch (index)
                {
                    case 1:
                        section.ContentTemplate = new DataTemplate(() => new SkUiHorizontalStackLayout
                        {
                            Children = { Text($"{label} details"), new SkUiButton { Text = "Open", Command = LeakCommands.Shared, CommandParameter = label } }
                        });
                        break;
                    case 2:
                        section.Content = new SkUiMauiContentView { HeightRequest = 44, Content = new Entry { Placeholder = label } };
                        break;
                    default:
                        section.Content = new SkUiVerticalStackLayout { Children = { Text($"{label} line 1"), Text($"{label} line 2") } };
                        break;
                }
                _sections.Add(section);
                stack.Children.Add(section);
            }
            return Root(new SkUiScrollView { Content = stack });
        }

        private async Task SettleAnimationsAsync(LeakScenarioContext context)
        {
            for (var wait = 0; wait < 60 && _sections.Any(section => section.IsAnimating); wait++)
                await context.SettleAsync();
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            await context.SettleAsync();
            foreach (var section in _sections)
                await context.TapAsync(section.Header!);
            _sections[0].IsExpanded = false; // reversed mid-animation
            _sections[0].IsExpanded = true;
            await SettleAnimationsAsync(context);
            if (_sections.Any(section => !section.IsExpanded || section.Content is not Element { Parent: not null }))
                _problem = "a section does not show its content after expanding";
            else
                await context.TapAsync(((SkUiHorizontalStackLayout)_sections[1].Content!).Children[1]);

            // Collapsed: lazy content leaves the tree, and replaced content must go while the section lives on.
            await context.TapAsync(_sections[2].Header!);
            await context.TapAsync(_sections[3].Header!);
            await SettleAnimationsAsync(context);
            if (_sections[2].Content is Element { Parent: not null })
                _problem ??= "lazy content stayed in the tree after collapsing";
            var replaced = _sections[3].Content!;
            _sections[3].Content = Text("Replaced");
            context.TrackDetached(replaced, "replaced expander content");
            await context.SettleAsync();
            _sections[0].IsExpanded = false; // closed mid-collapse
        }

        public override string? CheckInteraction() => _problem;
    }

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

    private sealed class VirtualListRun : LeakScenarioRun
    {
        private readonly List<WeakReference<SkUiLabel>> _created = [];
        private SkUiVirtualScrollView? _list;
        private int _realized;
        private int _released;
        private int _dropped;

        public override View Build(LeakScenarioContext context)
        {
            LeakItems.Shared.Clear();
            for (var index = 0; index < 2000; index++)
                LeakItems.Shared.Add($"Item {index}");
            _list = new SkUiVirtualScrollView
            {
                ItemsSource = LeakItems.Shared,
                // A small release distance, so scrolling releases views (the pool keeps some for recycling).
                ReleaseFactor = 0.5,
                ItemTemplate = new DataTemplate(() =>
                {
                    var row = context.Track(Text("", 13), "virtual item view");
                    row.SetBinding(SkUiLabel.TextProperty, Binding.SelfPath);
                    row.TappedCommand = LeakCommands.Shared;
                    _created.Add(new WeakReference<SkUiLabel>(row));
                    return row;
                })
            };
            _list.ItemRealized += (_, args) =>
            {
                _realized++;
                ((SkUiLabel)args.View).HeightRequest = 32 + args.Index % 4 * 12;
            };
            _list.ItemReleased += (_, _) => _released++;
            return Root(_list);
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            await context.DragAsync(_list!, 0, -300, durationMs: 400);
            await context.DragAsync(_list!, 0, -400, durationMs: 80); // fling
            await context.WaitAsync(300);
            await context.WaitForAsync(_list!.ScrollToIndex(1200, ScrollToPosition.Start, animated: true));
            LeakItems.Shared.Insert(0, "Inserted");
            LeakItems.Shared.RemoveAt(1201);
            await context.SettleAsync();
            await context.DragAsync(_list!, 0, 400, durationMs: 80); // fling towards the start through estimated items
            await context.WaitAsync(200);
            await context.DragAsync(_list!, 0, -400, durationMs: 60); // closes mid-fling
            TrackDroppedViews(context);
        }

        /// <summary>
        /// Views released while scrolling and not kept for recycling (the pool is capped) must be collectable while the
        /// list is still shown: neither the layout nor the shared command or collection may keep them.
        /// </summary>
        private void TrackDroppedViews(LeakScenarioContext context)
        {
            var items = _list!.Items;
            var kept = new HashSet<ISkUiView>(items.RecycledViews, ReferenceEqualityComparer.Instance);
            var (first, last) = items.RealizedRange;
            for (var index = first; index >= 0 && index <= last; index++)
                if (items.GetRealizedView(index) is { } view)
                    kept.Add(view);
            foreach (var reference in _created)
                if (reference.TryGetTarget(out var view) && !kept.Contains(view))
                {
                    context.TrackDetached(view, "released virtual item view");
                    _dropped++;
                }
        }

        public override string? CheckInteraction() =>
            _realized > 0 && _released > 0 && _dropped > 0 ? null
                : $"Items realized {_realized} times, released {_released} times; {_created.Count} views created, {_dropped} dropped from the pool.";
    }

    private sealed class CollectionViewRun : LeakScenarioRun
    {
        private SkUiCollectionView? _list;
        private int _selections;
        private int _taps;
        private int _refreshes;

        public override View Build(LeakScenarioContext context)
        {
            LeakItems.Shared.Clear();
            for (var index = 0; index < 1000; index++)
                LeakItems.Shared.Add($"Order {index}");
            _list = context.Track(new SkUiCollectionView
            {
                ItemsSource = LeakItems.Shared,
                SelectionMode = SkUiSelectionMode.SingleDeselect,
                SelectionChangedCommand = LeakCommands.Shared,
                ItemTappedCommand = LeakCommands.Shared,
                RefreshCommand = LeakCommands.Shared,
                IsPullToRefreshEnabled = true,
                IsStickyHeader = true,
                Header = Text("Orders", 15),
                EmptyView = Text("No orders", 13),
                ItemTemplate = new DataTemplate(() =>
                {
                    var row = context.Track(Text("", 13), "collection item view");
                    row.HeightRequest = 44;
                    row.SetBinding(SkUiLabel.TextProperty, Binding.SelfPath);
                    return row;
                })
            }, "collection view");
            _list.SelectionChanged += (_, _) => _selections++;
            _list.ItemTapped += (_, _) => _taps++;
            _list.Refreshing += (_, _) => _refreshes++;
            return Root(_list);
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            var list = _list!;
            await context.SettleAsync();
            await context.TapAsync(list.GetRealizedView(2)!);
            await context.TapAsync(list.GetRealizedView(2)!); // deselects
            await context.TapAsync(list.GetRealizedView(4)!);
            await context.DragAsync(list, 0, 300, durationMs: 400); // pull to refresh
            await context.SettleAsync();
            list.IsRefreshing = false;
            await context.DragAsync(list, 0, -400, durationMs: 80); // fling
            await context.WaitAsync(300);
            await context.WaitForAsync(list.ScrollToItem(LeakItems.Shared[600], ScrollToPosition.Center, animated: true));
            list.SelectedItem = LeakItems.Shared[600];
            LeakItems.Shared.RemoveAt(600); // clears the selection
            await context.SettleAsync();
            LeakItems.Shared.Clear(); // the empty view
            await context.SettleAsync();
            for (var index = 0; index < 200; index++)
                LeakItems.Shared.Add($"Again {index}");
            await context.SettleAsync();
            await context.DragAsync(list, 0, -400, durationMs: 60); // closes mid-fling
        }

        public override string? CheckInteraction() =>
            _taps >= 3 && _selections >= 4 && _refreshes == 1 && _list!.SelectedItem is null ? null
                : $"Tapped {_taps} times, selection changed {_selections} times, refreshed {_refreshes} times; selected {_list!.SelectedItem ?? "none"}.";
    }

    private sealed class CollectionViewGroupedRun : LeakScenarioRun
    {
        private SkUiCollectionView? _list;
        private int _collapsed;

        public override View Build(LeakScenarioContext context)
        {
            LeakGroups.Shared.Clear();
            LeakGroups.Selected.Clear();
            for (var group = 0; group < 12; group++)
                LeakGroups.Shared.Add(new LeakGroups.Group($"Group {group}", Enumerable.Range(0, 9).Select(item => $"Item {group}.{item}")));
            _list = context.Track(new SkUiCollectionView
            {
                IsGrouped = true,
                ItemsSource = LeakGroups.Shared,
                Span = 2,
                SpanSpacing = 4,
                IsStickyGroupHeader = true,
                AllowGroupExpandCollapse = true,
                SelectionMode = SkUiSelectionMode.Multiple,
                SelectedItems = LeakGroups.Selected,
                LoadMoreMode = SkUiLoadMoreMode.Auto,
                LoadMoreCommand = LeakCommands.Shared,
                GroupHeaderTemplate = new DataTemplate(() =>
                {
                    var header = context.Track(Text("", 15), "group header view");
                    header.HeightRequest = 32;
                    header.SetBinding(SkUiLabel.TextProperty, nameof(LeakGroups.Group.Name));
                    return header;
                }),
                ItemTemplate = new DataTemplate(() =>
                {
                    var cell = context.Track(Text("", 13), "grid cell view");
                    cell.HeightRequest = 44;
                    cell.SetBinding(SkUiLabel.TextProperty, Binding.SelfPath);
                    return cell;
                })
            }, "grouped collection view");
            _list.GroupCollapsed += (_, _) => _collapsed++;
            return Root(_list);
        }

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            var list = _list!;
            await context.SettleAsync();
            await context.TapAsync(list.GetRealizedView(0)!);
            await context.TapAsync(list.GetRealizedView(1)!);
            await context.DragAsync(list, 0, -400, durationMs: 80); // fling: the sticky header follows
            await context.WaitAsync(300);
            await context.WaitForAsync(list.ScrollToGroup(LeakGroups.Shared[8], ScrollToPosition.Start, animated: true));
            list.CollapseGroup(LeakGroups.Shared[8]);
            list.ExpandGroup(LeakGroups.Shared[8]);
            list.CollapseGroup(LeakGroups.Shared[2]);
            LeakGroups.Shared.Insert(0, new LeakGroups.Group("New", ["Fresh 1", "Fresh 2", "Fresh 3"]));
            LeakGroups.Shared[5].RemoveAt(0);
            LeakGroups.Shared[5].Add("Late");
            LeakGroups.Shared.RemoveAt(3);
            list.IsLoadMoreActive = false;
            await context.SettleAsync();
            list.ItemTemplate = new DataTemplate(() =>
            {
                var cell = context.Track(Text("", 12), "grid cell view (second template)");
                cell.HeightRequest = 52;
                cell.SetBinding(SkUiLabel.TextProperty, Binding.SelfPath);
                return cell;
            });
            await context.SettleAsync();
            await context.DragAsync(list, 0, -400, durationMs: 60); // closes mid-fling
        }

        public override string? CheckInteraction() =>
            _collapsed >= 2 && LeakGroups.Selected.Count == 2 ? null
                : $"Groups collapsed {_collapsed} times, {LeakGroups.Selected.Count} items selected.";
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

    private sealed class AccessibilityRun : LeakScenarioRun
    {
        /// <summary>Long-lived object kept in a tag: the tag must not keep the view alive.</summary>
        private static readonly object _sharedTag = new();
        private readonly List<SkUiButton> _buttons = [];
        private SkUiVerticalStackLayout? _rows;
        private SkUiSlider? _slider;
        private SkUiCoreButton? _core;
        private int _clicks;
        private int _reports;

        public override View Build(LeakScenarioContext context)
        {
            var stack = new SkUiVerticalStackLayout { Spacing = 8, Padding = new Thickness(12) };
            var heading = Text("Accessible form", 18);
            SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
            stack.Children.Add(heading);
            _rows = new SkUiVerticalStackLayout { Spacing = 4 };
            for (var index = 1; index <= 4; index++)
            {
                var button = new SkUiButton { Text = $"Row {index}", HeightRequest = 40, Tag = _sharedTag, TabIndex = index };
                SemanticProperties.SetHint(button, "Opens the row");
                button.Clicked += OnClicked;
                _buttons.Add(button);
                _rows.Children.Add(button);
            }
            stack.Children.Add(_rows);
            _slider = new SkUiSlider { Maximum = 10, Value = 5 };
            SemanticProperties.SetDescription(_slider, "Volume");
            stack.Children.Add(_slider);
            _core = new SkUiCoreButton { Text = "Core", Tag = _sharedTag };
            _core.SetSemanticHint("Core hint").SetHeight(40);
            _core.Tapped += (_, _) => _clicks++;
            stack.Children.Add(new SkUiCoreHost().SetContent(_core));
            return Root(stack);
        }

        private void OnClicked(object? sender, EventArgs e) => _clicks++;

        public override async Task InteractAsync(LeakScenarioContext context)
        {
            if (SkUiSemantics.RootOf(_buttons[0]) is not { } root)
                return;
            // A semantics tree reporting its changes, as a screen reader's bridge keeps one.
            var owner = root.SemanticsOwner ?? new SkUiSemanticsOwner(root);
            owner.Changed += OnSemanticsChanged;
            var focus = root.FocusManager;
            _buttons[0].Focus();
            focus.KeyDown(SkUiKey.Space);
            focus.KeyDown(SkUiKey.Tab);
            focus.KeyDown(SkUiKey.Enter);
            _slider!.Focus();
            focus.KeyDown(SkUiKey.Right);
            focus.KeyDown(SkUiKey.Left);
            _core!.Focus();
            focus.KeyDown(SkUiKey.Space);
            if (owner.Tree.Find(_buttons[3]) is { } element)
                owner.Perform(element.Id, SkUiSemanticsActions.Activate);
            _buttons[3].SetSemanticFocus();
            await context.SettleAsync();
            _buttons[2].Focus();
            owner.Invalidate();
            owner.Flush();
            // The reader goes away, the tree it read stays cached; then the focused row is removed. Focus is cleared, and
            // the row must be collectable while the surface lives (no cached tree may keep it).
            owner.Changed -= OnSemanticsChanged;
            _rows!.Children.Remove(_buttons[2]);
            context.TrackDetached(_buttons[2]);
            _buttons.RemoveAt(2);
            await context.SettleAsync();
        }

        private void OnSemanticsChanged(bool structure, IReadOnlyList<int> changed) => _reports++;

        public override string? CheckInteraction() =>
            _clicks < 4 ? $"Expected at least 4 activations, got {_clicks}." : _reports == 0 ? "The semantics tree reported no change." : null;
    }

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

/// <summary>A long-lived selection store, as a view model or service that outlives screens.</summary>
public sealed class LeakSelection
{
    public static LeakSelection Shared { get; } = new();

    public int Count { get; private set; }

    public void Record(SkUiCoreRadioButton radio) => Count++;
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

/// <summary>A collection that outlives every scenario, like a view model's list: bound layouts must not stay reachable from it.</summary>
public static class LeakItems
{
    public static ObservableCollection<string> Shared { get; } = [];
}

/// <summary>Long-lived groups and selection for the grouped collection view scenario.</summary>
public static class LeakGroups
{
    public static ObservableCollection<Group> Shared { get; } = [];

    public static ObservableCollection<object> Selected { get; } = [];

    public sealed class Group(string name, IEnumerable<string> items) : ObservableCollection<string>(items)
    {
        public string Name { get; } = name;
    }
}

/// <summary>Small in-memory PNG sources (no files or network, so they work headless too).</summary>
public static class LeakImages
{
    /// <summary>A stream source with a cache key: views of it share one decoded image.</summary>
    public static Func<CancellationToken, Task<Stream>> CachedStream(SKColor color)
    {
        var bytes = Png(color);
        return _ => Task.FromResult<Stream>(new MemoryStream(bytes));
    }

    /// <summary>A file source in the cache folder (absolute path: memory-cached by path).</summary>
    public static ImageSource Cached(SKColor color)
    {
        var path = Path.Combine(Path.GetTempPath(), $"skiaui-leak-{(uint)color:X8}.png");
        if (!File.Exists(path))
            File.WriteAllBytes(path, Png(color));
        return ImageSource.FromFile(path);
    }

    /// <summary>A looping 1×1 GIF of two frames (red, blue), 100 ms each.</summary>
    public static byte[] AnimatedGif()
    {
        static byte[] Frame(byte lzw) =>
        [
            0x21, 0xF9, 0x04, 0x00, 0x0A, 0x00, 0x00, 0x00,
            0x2C, 0, 0, 0, 0, 0x01, 0x00, 0x01, 0x00, 0x00,
            0x02, 0x02, lzw, 0x01, 0x00
        ];
        return
        [
            .. "GIF89a"u8.ToArray(),
            0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00,
            0xFF, 0x00, 0x00, 0x00, 0x00, 0xFF,
            0x21, 0xFF, 0x0B, .. "NETSCAPE2.0"u8.ToArray(), 0x03, 0x01, 0x00, 0x00, 0x00,
            .. Frame(0x44),
            .. Frame(0x4C),
            0x3B
        ];
    }

    private static byte[] Png(SKColor color)
    {
        using var bitmap = new SKBitmap(64, 48);
        bitmap.Erase(color);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

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
