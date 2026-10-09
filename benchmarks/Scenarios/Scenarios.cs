using MauiSkiaUi.Core;

namespace MauiSkiaUi.Benchmarks;

/// <summary>
/// Scenario catalog. Add a scenario by subclassing <see cref="BenchScenario"/> and listing it in <see cref="All"/>;
/// both runners and <c>scripts/bench.sh</c> pick it up by <see cref="BenchScenario.Name"/>.
/// </summary>
public static class Scenarios
{
    public const int Count = 1000;
    private const double Row = 36;

    public static IReadOnlyList<BenchScenario> All { get; } =
    [
        new CoreLabels(),
        new SkUiLabels(),
        new CoreButtons(),
        new SkUiButtons(),
        new MixedScriptLabels(),
        new CoreLabelsSimpleMode(),
        new CoreLabelsUpdate(),
        new SkUiLabelsUpdate(),
        new CoreLabelsFit(),
        new CoreLabelsFit(idle: true),
        new SkUiLabelsFit(),
        new CoreLabelsGrow(),
        new ScrollFling(),
        new VirtualListFling(),
        new Spinners(),
        new ToggleTransitions(),
        new ToggleTransitions(busy: true),
        new NativeLabels(),
        new NestedExpanders(),
        new NestedExpanders(scroll: true),
        new NestedExpanders(variant: "noshadow"),
        new NestedExpanders(variant: "square"),
        new NestedExpanders(variant: "plain"),
    ];

    public static BenchScenario? Find(string name) =>
        All.FirstOrDefault(scenario => string.Equals(scenario.Name, name, StringComparison.OrdinalIgnoreCase));

    internal static SkUiScrollView Scroll(ISkUiView content) => new() { Background = Colors.White, Content = content };

    internal static SkUiCoreGrid CoreGrid(int count)
    {
        var rows = new SkUiCoreRowDefinition[(count + 1) / 2];
        for (var r = 0; r < rows.Length; r++)
            rows[r] = new SkUiCoreRowDefinition(new SkUiCoreGridLength(Row));
        return new SkUiCoreGrid()
            .SetPadding(new Thickness(8)).SetRowSpacing(4).SetColumnSpacing(4)
            .SetColumnDefinitions([new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star), new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star)])
            .SetRowDefinitions(rows);
    }

    internal static SkUiGrid SkUiGrid(int count)
    {
        var grid = new SkUiGrid { Padding = new Thickness(8), RowSpacing = 4, ColumnSpacing = 4 };
        grid.ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star));
        var rows = new RowDefinitionCollection();
        for (var r = 0; r < (count + 1) / 2; r++)
            rows.Add(new RowDefinition(new GridLength(Row)));
        grid.RowDefinitions = rows;
        return grid;
    }

    internal static SkUiCoreHost Host(SkUiCoreNode content)
    {
        var host = new SkUiCoreHost();
        host.SetContent(content);
        return host;
    }
}

/// <summary>1,000 plain Core labels in a two-column Core grid.</summary>
public sealed class CoreLabels : BenchScenario
{
    public override string Name => "core-labels";
    public override string Description => "1,000 SkUiCoreLabel (plain Latin text) in a 2-column SkUiCoreGrid";

    public override View Build()
    {
        var grid = Scenarios.CoreGrid(Scenarios.Count);
        grid.StartUpdating();
        for (var i = 0; i < Scenarios.Count; i++)
        {
            var label = new SkUiCoreLabel();
            label.SetText($"Label {i:0000}");
            label.SetFontSize(14);
            grid.Add(label, i / 2, i % 2);
        }
        grid.EndUpdating();
        return Scenarios.Scroll(Scenarios.Host(grid));
    }
}

/// <summary>1,000 plain SkUiLabels in a two-column SkUiGrid.</summary>
public sealed class SkUiLabels : BenchScenario
{
    public override string Name => "skui-labels";
    public override string Description => "1,000 SkUiLabel (plain Latin text) in a 2-column SkUiGrid";

    public override View Build()
    {
        var grid = Scenarios.SkUiGrid(Scenarios.Count);
        grid.StartUpdating();
        for (var i = 0; i < Scenarios.Count; i++)
        {
            var label = new SkUiLabel { Text = $"Label {i:0000}", FontSize = 14 };
            Grid.SetRow(label, i / 2);
            Grid.SetColumn(label, i % 2);
            grid.Children.Add(label);
        }
        grid.EndUpdating();
        return Scenarios.Scroll(grid);
    }
}

/// <summary>Stress-page equivalent: 1,000 Core buttons.</summary>
public sealed class CoreButtons : BenchScenario
{
    public override string Name => "core-buttons";
    public override string Description => "1,000 SkUiCoreButton in a 2-column SkUiCoreGrid (stress page, Core layer)";

    public override View Build()
    {
        var grid = Scenarios.CoreGrid(Scenarios.Count);
        grid.StartUpdating();
        for (var i = 0; i < Scenarios.Count; i++)
        {
            var button = new SkUiCoreButton();
            button.SetText($"Item {i:0000}");
            button.SetFontSize(14);
            button.SetPadding(new Thickness(6));
            grid.Add(button, i / 2, i % 2);
        }
        grid.EndUpdating();
        return Scenarios.Scroll(Scenarios.Host(grid));
    }
}

/// <summary>Stress-page equivalent: 1,000 SkUi buttons.</summary>
public sealed class SkUiButtons : BenchScenario
{
    public override string Name => "skui-buttons";
    public override string Description => "1,000 SkUiButton in a 2-column SkUiGrid (stress page, SkUi* layer)";

    public override View Build()
    {
        var grid = Scenarios.SkUiGrid(Scenarios.Count);
        grid.StartUpdating();
        for (var i = 0; i < Scenarios.Count; i++)
        {
            var button = new SkUiButton { Text = $"Item {i:0000}", FontSize = 14, Padding = new Thickness(6) };
            Grid.SetRow(button, i / 2);
            Grid.SetColumn(button, i % 2);
            grid.Children.Add(button);
        }
        grid.EndUpdating();
        return Scenarios.Scroll(grid);
    }
}

/// <summary>Complex scripts, RTL and emoji: the HarfBuzz / bidi / fallback path.</summary>
public sealed class MixedScriptLabels : BenchScenario
{
    private static readonly string[] Samples =
    [
        "مرحبا بالعالم ١٢٣", "שלום עולם 42", "नमस्ते दुनिया", "Launch 🚀 ready 👍🏽", "Order #12 — הזמנה (שולם)", "地球は私たちの家です",
    ];

    public override string Name => "mixed-script-labels";
    public override string Description => "600 SkUiLabel with Arabic / Hebrew / Devanagari / emoji / CJK text (shaping path)";

    public override View Build()
    {
        var grid = Scenarios.SkUiGrid(600);
        grid.StartUpdating();
        for (var i = 0; i < 600; i++)
        {
            var label = new SkUiLabel { Text = Samples[i % Samples.Length], FontSize = 14 };
            Grid.SetRow(label, i / 2);
            Grid.SetColumn(label, i % 2);
            grid.Children.Add(label);
        }
        grid.EndUpdating();
        return Scenarios.Scroll(grid);
    }
}

/// <summary>Dense plain text with the renderer forced to the simple (non-shaping) mode.</summary>
public sealed class CoreLabelsSimpleMode : BenchScenario
{
    public override string Name => "core-labels-simple";
    public override string Description => "1,000 SkUiCoreLabel with TextRendering = Simple (no-op on libraries without the option)";

    public override View Build()
    {
        var grid = Scenarios.CoreGrid(Scenarios.Count);
        grid.StartUpdating();
        for (var i = 0; i < Scenarios.Count; i++)
        {
            var label = new SkUiCoreLabel();
            label.SetText($"{i * 37 % 10000:0000}.{i % 100:00}");
            label.SetFontSize(14);
            TrySet(label, "TextRendering", "Simple");
            grid.Add(label, i / 2, i % 2);
        }
        grid.EndUpdating();
        return Scenarios.Scroll(Scenarios.Host(grid));
    }
}

/// <summary>Steady-state: every Core label changes text after the first frame.</summary>
public sealed class CoreLabelsUpdate : BenchScenario
{
    private int _round;

    public override string Name => "core-labels-update";
    public override string Description => "1,000 SkUiCoreLabel; update = change every label's text (re-layout + re-record)";

    public override View Build() => new CoreLabels().Build();

    public override Action<View>? Update => root =>
    {
        _round++;
        var grid = (SkUiCoreGrid)((SkUiCoreHost)((SkUiScrollView)root).Content!).Content!;
        grid.StartUpdating();
        var index = 0;
        foreach (var child in grid.Children)
            ((SkUiCoreLabel)child).SetText($"Value {index++ + _round:0000}");
        grid.EndUpdating();
    };
}

/// <summary>Steady-state: every SkUi label changes text after the first frame.</summary>
public sealed class SkUiLabelsUpdate : BenchScenario
{
    private int _round;

    public override string Name => "skui-labels-update";
    public override string Description => "1,000 SkUiLabel; update = change every label's text (re-layout + re-record)";

    public override View Build() => new SkUiLabels().Build();

    public override Action<View>? Update => root =>
    {
        _round++;
        var grid = (SkUiGrid)((SkUiScrollView)root).Content!;
        grid.StartUpdating();
        var index = 0;
        foreach (var child in grid.Children)
            ((SkUiLabel)child).Text = $"Value {index++ + _round:0000}";
        grid.EndUpdating();
    };
}

/// <summary>
/// Text that fits its label (C3): shrink to fit with tightening on texts that fit, shrink a little, shrink a lot and do not
/// fit even at the minimum (about 190 DIPs a cell headless); update = new texts, fitted again. Libraries without the
/// options draw the same labels truncated, so a comparison with them is the cost of fitting.
/// </summary>
public static class FitTexts
{
    private static readonly string[] Tails = ["", " · express delivery", " · express delivery to Kraków, gate 4", " · express delivery to Kraków, gate 4, pick up before noon"];

    public static string Text(int index, int round, bool idle) => $"Order {(index + round) % 10000:0000}" + (idle ? "" : Tails[index % Tails.Length]);

    public static void Apply(object label)
    {
        BenchScenario.TrySetOption(label, "ShrinkToFit", true);
        BenchScenario.TrySetOption(label, "AllowsTightening", true);
    }
}

/// <summary>1,000 Core labels that shrink and tighten to fit (or, idle, already fit).</summary>
public sealed class CoreLabelsFit(bool idle = false) : BenchScenario
{
    private int _round;

    public override string Name => idle ? "core-labels-fit-idle" : "core-labels-fit";
    public override string Description => idle
        ? "1,000 SkUiCoreLabel with ShrinkToFit + AllowsTightening whose text fits; update = new texts (no-op without the options)"
        : "1,000 SkUiCoreLabel with ShrinkToFit + AllowsTightening, texts fitting to too long; update = new texts (no-op without the options)";

    public override View Build()
    {
        var grid = Scenarios.CoreGrid(Scenarios.Count);
        grid.StartUpdating();
        for (var i = 0; i < Scenarios.Count; i++)
        {
            var label = new SkUiCoreLabel();
            label.SetText(FitTexts.Text(i, 0, idle));
            label.SetFontSize(14);
            label.SetLineBreakMode(LineBreakMode.TailTruncation);
            FitTexts.Apply(label);
            grid.Add(label, i / 2, i % 2);
        }
        grid.EndUpdating();
        return Scenarios.Scroll(Scenarios.Host(grid));
    }

    public override Action<View>? Update => root =>
    {
        _round++;
        var grid = (SkUiCoreGrid)((SkUiCoreHost)((SkUiScrollView)root).Content!).Content!;
        grid.StartUpdating();
        var index = 0;
        foreach (var child in grid.Children)
            ((SkUiCoreLabel)child).SetText(FitTexts.Text(index++, _round, idle));
        grid.EndUpdating();
    };
}

/// <summary>1,000 SkUi labels that shrink and tighten to fit.</summary>
public sealed class SkUiLabelsFit : BenchScenario
{
    private int _round;

    public override string Name => "skui-labels-fit";
    public override string Description => "1,000 SkUiLabel with ShrinkToFit + AllowsTightening, texts fitting to too long; update = new texts (no-op without the options)";

    public override View Build()
    {
        var grid = Scenarios.SkUiGrid(Scenarios.Count);
        grid.StartUpdating();
        for (var i = 0; i < Scenarios.Count; i++)
        {
            var label = new SkUiLabel { Text = FitTexts.Text(i, 0, idle: false), FontSize = 14, LineBreakMode = LineBreakMode.TailTruncation };
            FitTexts.Apply(label);
            Grid.SetRow(label, i / 2);
            Grid.SetColumn(label, i % 2);
            grid.Children.Add(label);
        }
        grid.EndUpdating();
        return Scenarios.Scroll(grid);
    }

    public override Action<View>? Update => root =>
    {
        _round++;
        var grid = (SkUiGrid)((SkUiScrollView)root).Content!;
        grid.StartUpdating();
        var index = 0;
        foreach (var child in grid.Children)
            ((SkUiLabel)child).Text = FitTexts.Text(index++, _round, idle: false);
        grid.EndUpdating();
    };
}

/// <summary>1,000 Core labels whose numbers grow to fill their cells (36 DIPs high).</summary>
public sealed class CoreLabelsGrow : BenchScenario
{
    private int _round;

    public override string Name => "core-labels-grow";
    public override string Description => "1,000 SkUiCoreLabel with GrowToFill (up to 3x) filling their cells; update = new numbers (no-op without the options)";

    private static string Number(int index, int round) => $"{(index * 37 + round) % 100000:N0}";

    public override View Build()
    {
        var grid = Scenarios.CoreGrid(Scenarios.Count);
        grid.StartUpdating();
        for (var i = 0; i < Scenarios.Count; i++)
        {
            var label = new SkUiCoreLabel();
            label.SetText(Number(i, 0));
            label.SetFontSize(14);
            label.SetLineBreakMode(LineBreakMode.NoWrap);
            TrySetOption(label, "GrowToFill", true);
            TrySetOption(label, "MaximumFontScale", 3d);
            grid.Add(label, i / 2, i % 2);
        }
        grid.EndUpdating();
        return Scenarios.Scroll(Scenarios.Host(grid));
    }

    public override Action<View>? Update => root =>
    {
        _round++;
        var grid = (SkUiCoreGrid)((SkUiCoreHost)((SkUiScrollView)root).Content!).Content!;
        grid.StartUpdating();
        var index = 0;
        foreach (var child in grid.Children)
            ((SkUiCoreLabel)child).SetText(Number(index++, _round));
        grid.EndUpdating();
    };
}

/// <summary>Render-thread scrolling of a long list.</summary>
public sealed class ScrollFling : BenchScenario
{
    public override string Name => "scroll-fling";
    public override string Description => "400 SkUiButton in a scroll view; motion = animated scroll towards the end (render-thread frame stats)";
    public override bool DeviceOnly => true;

    public override View Build()
    {
        var stack = new SkUiVerticalStackLayout { Spacing = 4, Padding = new Thickness(8) };
        for (var i = 0; i < 400; i++)
            stack.Children.Add(new SkUiButton { Text = $"Row {i:000}", HeightRequest = 44 });
        return Scenarios.Scroll(stack);
    }

    public override Func<View, IDisposable?>? Motion => root =>
    {
        var scroll = (SkUiScrollView)root;
        // Longer than MotionDuration so the whole sampling window is in motion. AnimateScrollTo (render-thread
        // animation) is looked up by reflection so older libraries fall back to the UI-thread ScrollToAsync.
        var duration = MotionDuration + TimeSpan.FromSeconds(0.5);
        var animate = typeof(SkUiScrollView).GetMethod("AnimateScrollTo", [typeof(double), typeof(double), typeof(TimeSpan)]);
        if (animate is not null)
            return animate.Invoke(scroll, [0d, scroll.ContentSize.Height, duration]) as IDisposable;
        _ = scroll.ScrollToAsync(0, scroll.ContentSize.Height, true);
        return null;
    };
}

/// <summary>A long virtualized list: only the rows near the viewport exist (FR-21).</summary>
public sealed class VirtualListFling : BenchScenario
{
    public override string Name => "virtual-fling";
    public override string Description => "SkUiVirtualScrollView over 10,000 rows of different heights (template SkUiButton, recycled); motion = animated scroll towards the end";

    // Release builds trim: keep the reflected list's constructor and properties.
    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors
        | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties, "MauiSkiaUi.SkUiVirtualScrollView", "MauiSkiaUi")]
    public override View Build()
    {
        // Created by name: baseline commits without virtual lists build an empty scroll view instead.
        var type = typeof(SkUiScrollView).Assembly.GetType("MauiSkiaUi.SkUiVirtualScrollView");
        if (type is null)
            return Scenarios.Scroll(new SkUiVerticalStackLayout());
        var list = (SkUiScrollView)Activator.CreateInstance(type)!;
        list.Background = Colors.White;
        TrySet(list, "ItemsSource", Enumerable.Range(0, 10_000).ToList());
        TrySet(list, "ItemSpacing", 4d);
        TrySet(list, "Spacing", 4d); // the name before ItemSpacing, for baselines built from older commits
        TrySet(list, "ItemTemplate", new DataTemplate(() => new Row()));
        return list;
    }

    public override Func<View, IDisposable?>? Motion => root =>
    {
        var scroll = (SkUiScrollView)root;
        return scroll.AnimateScrollTo(0, scroll.ContentSize.Height, MotionDuration + TimeSpan.FromSeconds(0.5));
    };

    private sealed class Row : SkUiButton
    {
        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not int index)
                return;
            Text = $"Row {index:00000}";
            HeightRequest = 40 + index % 3 * 12;
        }
    }
}

/// <summary>Many activity indicators spinning (content spin on the render thread).</summary>
public sealed class Spinners : BenchScenario
{
    public override string Name => "spinners";
    public override string Description => "120 SkUiActivityIndicator running; motion = sample their animation (render-thread frame stats)";
    public override bool DeviceOnly => true;

    public override View Build()
    {
        var grid = new SkUiGrid { Padding = new Thickness(8) };
        grid.ColumnDefinitions = new ColumnDefinitionCollection(Enumerable.Range(0, 6).Select(_ => new ColumnDefinition(GridLength.Star)).ToArray());
        grid.RowDefinitions = new RowDefinitionCollection(Enumerable.Range(0, 20).Select(_ => new RowDefinition(new GridLength(44))).ToArray());
        for (var i = 0; i < 120; i++)
        {
            var spinner = new SkUiActivityIndicator { IsRunning = true, WidthRequest = 32, HeightRequest = 32 };
            Grid.SetRow(spinner, i / 6);
            Grid.SetColumn(spinner, i % 6);
            grid.Children.Add(spinner);
        }
        return new SkUiContentView { Background = Colors.White, Content = grid };
    }

    public override Func<View, IDisposable?>? Motion => _ => null;
}

/// <summary>Reference point: the same label grid with native MAUI controls.</summary>
public sealed class NativeLabels : BenchScenario
{
    public override string Name => "native-labels";
    public override string Description => "Reference: 1,000 native MAUI Label in a MAUI Grid + ScrollView";
    public override bool DeviceOnly => true;

    public override View Build()
    {
        var grid = new Grid { Padding = new Thickness(8), RowSpacing = 4, ColumnSpacing = 4 };
        grid.ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star));
        for (var r = 0; r < Scenarios.Count / 2; r++)
            grid.RowDefinitions.Add(new RowDefinition(new GridLength(36)));
        for (var i = 0; i < Scenarios.Count; i++)
            grid.Add(new Label { Text = $"Label {i:0000}", FontSize = 14 }, i % 2, i / 2);
        return new ScrollView { Content = grid };
    }
}

/// <summary>
/// State-change transitions (FR-26): switches and check boxes re-toggled every 120 ms, so transitions (UI-thread clock,
/// one re-recorded picture per animating control per frame) run through the whole sampling window. The busy variant
/// also blocks the UI thread for 25 ms every 100 ms, like an app doing work while the user taps.
/// </summary>
public sealed class ToggleTransitions(bool busy = false) : BenchScenario
{
    public override string Name => busy ? "toggle-transitions-busy" : "toggle-transitions";
    public override string Description => busy
        ? "48 SkUiSwitch + 48 SkUiCheckBox re-toggled every 120 ms while the UI thread is blocked 25 ms every 100 ms"
        : "48 SkUiSwitch + 48 SkUiCheckBox re-toggled every 120 ms; motion = their transitions (frame stats)";
    public override bool DeviceOnly => true;

    public override View Build()
    {
        var grid = new SkUiGrid { Padding = new Thickness(8), RowSpacing = 6, ColumnSpacing = 6 };
        grid.ColumnDefinitions = new ColumnDefinitionCollection(Enumerable.Range(0, 6).Select(_ => new ColumnDefinition(GridLength.Star)).ToArray());
        grid.RowDefinitions = new RowDefinitionCollection(Enumerable.Range(0, 16).Select(_ => new RowDefinition(new GridLength(36))).ToArray());
        for (var i = 0; i < 96; i++)
        {
            SkUiView toggle = i % 2 == 0 ? new SkUiSwitch() : new SkUiCheckBox();
            Grid.SetRow(toggle, i / 6);
            Grid.SetColumn(toggle, i % 6);
            grid.Children.Add(toggle);
        }
        return new SkUiContentView { Background = Colors.White, Content = grid };
    }

    public override Func<View, IDisposable?>? Motion => root =>
    {
        var toggles = root.GetVisualTreeDescendants().OfType<SkUiToggleControl>().ToList();
        void ToggleAll()
        {
            foreach (var toggle in toggles)
                toggle.IsChecked = !toggle.IsChecked;
        }
        ToggleAll();
        var toggleTimer = root.Dispatcher.CreateTimer();
        toggleTimer.Interval = TimeSpan.FromMilliseconds(120);
        toggleTimer.Tick += (_, _) => ToggleAll();
        toggleTimer.Start();
        IDispatcherTimer? busyTimer = null;
        if (busy)
        {
            busyTimer = root.Dispatcher.CreateTimer();
            busyTimer.Interval = TimeSpan.FromMilliseconds(100);
            busyTimer.Tick += (_, _) => Thread.Sleep(25);
            busyTimer.Start();
        }
        return new Stop(() =>
        {
            toggleTimer.Stop();
            busyTimer?.Stop();
        });
    };

    private sealed class Stop(Action stop) : IDisposable
    {
        public void Dispose() => stop();
    }
}

/// <summary>
/// Bordered expanders three levels deep (12 sections, controls in each level) in a scroll view, as on the demo's nested
/// expander page. Motion: three sections expand and collapse continuously (300 ms animation, the height of everything
/// below follows every frame), or with <c>scroll</c> everything is expanded and the view scrolls. <c>SkUiExpander</c> is
/// created by reflection so the catalog builds against libraries without it (they get plain stacks and no animation).
/// </summary>
/// Variants (cost breakdown): <c>noshadow</c> without card shadows, <c>square</c> with square corners (rectangular
/// clips), <c>plain</c> without borders.
public sealed class NestedExpanders(bool scroll = false, string? variant = null) : BenchScenario
{
    private static readonly Type? ExpanderType = typeof(SkUiView).Assembly.GetType("MauiSkiaUi.SkUiExpander");

    public override string Name => (scroll ? "expanders-scroll" : "expanders") + (variant is null ? "" : "-" + variant);
    public override string Description => scroll
        ? "36 nested SkUiExpander (12 bordered sections x 3 levels, controls inside) all expanded; motion = animated scroll"
        : "36 nested SkUiExpander (12 bordered sections x 3 levels, controls inside); motion = 3 sections expanding / collapsing (300 ms)";
    public override bool DeviceOnly => true;

    // Release builds trim: keep the reflected expander's constructor and properties.
    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors
        | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties, "MauiSkiaUi.SkUiExpander", "MauiSkiaUi")]
    public override View Build()
    {
        var stack = new SkUiVerticalStackLayout { Spacing = 10, Padding = new Thickness(10) };
        for (var index = 0; index < 12; index++)
            stack.Children.Add(Section($"Section {index + 1}", 1));
        return Scenarios.Scroll(stack);
    }

    private SkUiView Section(string title, int level)
    {
        var content = new SkUiVerticalStackLayout { Spacing = 8, Padding = new Thickness(12, 4, 12, 12) };
        content.Children.Add(new SkUiLabel { Text = $"{title}: level {level} of 3. Everything below moves while this opens.", FontSize = 13, LineBreakMode = LineBreakMode.WordWrap });
        content.Children.Add(new SkUiSlider { Maximum = 1, Value = 0.4 });
        content.Children.Add(new SkUiProgressBar { Progress = 0.4 });
        content.Children.Add(new SkUiHorizontalStackLayout
        {
            Spacing = 8,
            Children = { new SkUiSwitch(), new SkUiLabel { Text = "Notifications" }, new SkUiCheckBox { IsChecked = true }, new SkUiLabel { Text = "Sync" } }
        });
        content.Children.Add(new SkUiButton { Text = $"Action ({title})", FontSize = 13, HorizontalOptions = LayoutOptions.Start });
        if (level < 3)
            content.Children.Add(Section($"{title}.{level + 1}", level + 1));
        var header = new SkUiLabel { Text = title, FontSize = 16, Padding = new Thickness(12, 10), Background = level == 1 ? Colors.LightSteelBlue : Colors.Transparent };
        ISkUiView body;
        if (ExpanderType is not null)
        {
            var expander = (SkUiView)Activator.CreateInstance(ExpanderType)!;
            TrySet(expander, "Header", header);
            TrySet(expander, "Content", content);
            TrySet(expander, "AnimationLength", 300u);
            TrySet(expander, "IsExpanded", scroll);
            body = expander;
        }
        else
            body = new SkUiVerticalStackLayout { Children = { header, content } };
        if (variant == "plain")
            return new SkUiContentView { Background = Colors.White, Content = body };
        var border = new SkUiBorder
        {
            Stroke = Colors.SteelBlue, StrokeThickness = 1, CornerRadius = variant == "square" ? 0 : 10, Background = Colors.White, Content = body
        };
        if (level == 1 && variant != "noshadow")
            border.Shadow = new Shadow { Brush = Colors.Black, Opacity = 0.15f, Radius = 6, Offset = new Point(0, 2) };
        return border;
    }

    public override Func<View, IDisposable?>? Motion => root =>
    {
        var scrollView = (SkUiScrollView)root;
        if (scroll)
        {
            var duration = MotionDuration + TimeSpan.FromSeconds(0.5);
            var animate = typeof(SkUiScrollView).GetMethod("AnimateScrollTo", [typeof(double), typeof(double), typeof(TimeSpan)]);
            return animate?.Invoke(scrollView, [0d, scrollView.ContentSize.Height, duration]) as IDisposable;
        }
        // Sections 1, 4 and 7 (and their first nested level) toggle every 400 ms: an animation is always running.
        var sections = ((SkUiVerticalStackLayout)scrollView.Content!).Children.OfType<SkUiContentView>().Where((_, index) => index % 3 == 0).Take(3)
            .Select(border => border.Content as SkUiView).Where(view => view?.GetType() == ExpanderType).ToList();
        var expanded = false;
        void ToggleAll()
        {
            expanded = !expanded;
            foreach (var section in sections)
                TrySet(section!, "IsExpanded", expanded);
        }
        ToggleAll();
        var timer = root.Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(400);
        timer.Tick += (_, _) => ToggleAll();
        timer.Start();
        return new Stop(timer);
    };

    private sealed class Stop(IDispatcherTimer timer) : IDisposable
    {
        public void Dispose() => timer.Stop();
    }
}
