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
        new ScrollFling(),
        new Spinners(),
        new NativeLabels(),
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
