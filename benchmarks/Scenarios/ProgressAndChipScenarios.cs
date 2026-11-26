namespace MauiSkiaUi.Benchmarks;

/// <summary>
/// 200 progress rings (1.18 P), every other one indeterminate (turned on the render thread); the update moves every ring's
/// progress, so the determinate ones re-record. The ring is created by name, so a baseline without it builds an empty grid.
/// </summary>
public sealed class ProgressRings : BenchScenario
{
    private const int Count = 200;
    private static readonly Type? RingType = typeof(SkUiView).Assembly.GetType("MauiSkiaUi.SkUiProgressRing");

    public override string Name => "skui-rings";
    public override string Description => "200 SkUiProgressRing (half indeterminate) in a 5-column grid; update = move every ring's Progress";

    public override View Build()
    {
        var grid = new SkUiGrid { Padding = new Thickness(8), RowSpacing = 4, ColumnSpacing = 4 };
        grid.ColumnDefinitions = new ColumnDefinitionCollection(Enumerable.Range(0, 5).Select(_ => new ColumnDefinition(GridLength.Star)).ToArray());
        grid.RowDefinitions = new RowDefinitionCollection(Enumerable.Range(0, Count / 5).Select(_ => new RowDefinition(new GridLength(56))).ToArray());
        grid.StartUpdating();
        for (var i = 0; RingType is not null && i < Count; i++)
        {
            var ring = (SkUiView)Activator.CreateInstance(RingType)!;
            ring.WidthRequest = 48;
            ring.HeightRequest = 48;
            TrySet(ring, "Progress", i % 10 / 10d);
            TrySet(ring, "IsIndeterminate", i % 2 == 1);
            Grid.SetRow(ring, i / 5);
            Grid.SetColumn(ring, i % 5);
            grid.Children.Add(ring);
        }
        grid.EndUpdating();
        return Scenarios.Scroll(grid);
    }

    public override Action<View>? Update => root =>
    {
        var grid = (SkUiGrid)((SkUiScrollView)root).Content!;
        grid.StartUpdating();
        foreach (var child in grid.Children)
            if (child.GetType().GetProperty("Progress") is { } progress)
                progress.SetValue(child, ((double)progress.GetValue(child)! + 0.05) % 1);
        grid.EndUpdating();
    };

    public override Func<View, IDisposable?>? Motion => _ => null;
}

/// <summary>
/// 500 chips (1.18 K) in a wrap layout: filter, input (with ×) and badged chips; the update selects or clears every chip, so filter
/// chips gain or lose their check (measure and re-record). Chips are created by name, so a baseline without them builds an empty layout.
/// </summary>
public sealed class Chips : BenchScenario
{
    private const int Count = 500;
    private static readonly Type? ChipType = typeof(SkUiView).Assembly.GetType("MauiSkiaUi.SkUiChip");

    public override string Name => "skui-chips";
    public override string Description => "500 SkUiChip (filter, input, badged) in a wrap layout; update = toggle IsSelected on every chip";

    public override View Build()
    {
        var wrap = new SkUiWrapLayout { Padding = new Thickness(8), Spacing = 8, RowSpacing = 8 };
        wrap.StartUpdating();
        for (var i = 0; ChipType is not null && i < Count; i++)
        {
            var chip = (SkUiView)Activator.CreateInstance(ChipType)!;
            TrySet(chip, "Text", $"Chip {i}");
            TrySet(chip, "Kind", (i % 4) switch { 0 or 1 => "Filter", 2 => "Input", _ => "Action" });
            if (i % 4 == 3)
                TrySet(chip, "BadgeText", (i % 20).ToString(System.Globalization.CultureInfo.InvariantCulture));
            wrap.Children.Add(chip);
        }
        wrap.EndUpdating();
        return Scenarios.Scroll(wrap);
    }

    public override Action<View>? Update => root =>
    {
        var wrap = (SkUiWrapLayout)((SkUiScrollView)root).Content!;
        wrap.StartUpdating();
        foreach (var child in wrap.Children)
            if (child.GetType().GetProperty("IsSelected") is { } selected)
                selected.SetValue(child, !(bool)selected.GetValue(child)!);
        wrap.EndUpdating();
    };
}
