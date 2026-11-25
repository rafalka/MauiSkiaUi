using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUi.Benchmarks;

/// <summary>
/// 1.17's new controls, reached by name, so a <c>--baseline</c> build of a library without them still compiles; there the
/// scenario draws an empty page. Release builds trim, so what is reached by name is kept by <see cref="DynamicDependencyAttribute"/>.
/// </summary>
internal static class Types117
{
    private const DynamicallyAccessedMemberTypes Kept =
        DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods;

    /// <summary>An instance of <paramref name="name"/> in the library under test, or <c>null</c> without it.</summary>
    [DynamicDependency(Kept, "MauiSkiaUi.SkUiSegmentedControl", "MauiSkiaUi")]
    [DynamicDependency(Kept, "MauiSkiaUi.SkUiTabView", "MauiSkiaUi")]
    [DynamicDependency(Kept, "MauiSkiaUi.SkUiTabItem", "MauiSkiaUi")]
    [DynamicDependency(Kept, "MauiSkiaUi.Core.SkUiCoreSegmentedControl", "MauiSkiaUi")]
    [DynamicDependency(Kept, "MauiSkiaUi.Core.SkUiCoreTabView", "MauiSkiaUi")]
    [DynamicDependency(Kept, "MauiSkiaUi.Core.SkUiCoreTabItem", "MauiSkiaUi")]
    [DynamicDependency(Kept, "MauiSkiaUi.Core.SkUiCoreCarouselView", "MauiSkiaUi")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The types are kept by the dynamic dependencies.")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "The types are kept by the dynamic dependencies.")]
    public static object? Create(string name) =>
        typeof(SkUiView).Assembly.GetType(name) is { } type ? Activator.CreateInstance(type) : null;

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "The types are kept by the dynamic dependencies.")]
    public static object? Get(object target, string property) => target.GetType().GetProperty(property)!.GetValue(target);

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "The types are kept by the dynamic dependencies.")]
    public static MethodInfo Method(object target, string name) => target.GetType().GetMethod(name)!;
}

/// <summary>200 segmented controls of four segments (1.17 S4, S5), on either layer; update = the next segment of each selected.</summary>
public sealed class SegmentedControls(bool core) : BenchScenario
{
    private const int Count = 200;
    private static readonly string[] Texts = ["Day", "Week", "Month", "Year"];
    private readonly List<object> _controls = [];

    public override string Name => core ? "core-segmented" : "skui-segmented";
    public override string Description => $"{Count} {(core ? "SkUiCoreSegmentedControls in one host" : "SkUiSegmentedControls")} of {Texts.Length} segments in a scroll view; update = each control's next segment selected";

    public override View Build()
    {
        _controls.Clear();
        if (core)
        {
            var nodes = new SkUiCoreVerticalStackLayout().SetSpacing(4);
            while (_controls.Count < Count && Make("MauiSkiaUi.Core.SkUiCoreSegmentedControl") is { } made)
                nodes.Add((SkUiCoreNode)made);
            return Scenarios.Scroll(new SkUiCoreHost().SetContent(nodes));
        }
        var stack = new SkUiVerticalStackLayout { Spacing = 4 };
        while (_controls.Count < Count && Make("MauiSkiaUi.SkUiSegmentedControl") is { } made)
            stack.Children.Add((SkUiView)made);
        return Scenarios.Scroll(stack);
    }

    private object? Make(string type)
    {
        if (Types117.Create(type) is not { } control)
            return null;
        Types117.Method(control, "SetSegments").Invoke(control, [Texts]);
        TrySet(control, "SelectedIndex", _controls.Count % Texts.Length);
        _controls.Add(control);
        return control;
    }

    public override Action<View>? Update => _ =>
    {
        foreach (var control in _controls)
            TrySet(control, "SelectedIndex", ((int)Types117.Get(control, "SelectedIndex")! + 1) % Texts.Length);
    };
}

/// <summary>A tab view over five panes of lists (1.17 S7, S8), on either layer, scrolled to the next tab every 400 ms.</summary>
public sealed class TabViewSwipe(bool core) : BenchScenario
{
    private const int Tabs = 5;

    public override string Name => core ? "core-tabview-swipe" : "skui-tabview-swipe";
    public override string Description =>
        $"{(core ? "SkUiCoreTabView over five Core scroll views of 60 rows" : "SkUiTabView over five SkUiCollectionViews of 1,000 cards")}; motion = the next tab every 400 ms, animated";
    public override bool DeviceOnly => true;
    public override TimeSpan MotionDuration => TimeSpan.FromSeconds(3);

    public override View Build()
    {
        if (Types117.Create(core ? "MauiSkiaUi.Core.SkUiCoreTabView" : "MauiSkiaUi.SkUiTabView") is not { } tabs)
            return new SkUiContentView { Background = Colors.White };
        for (var index = 0; index < Tabs; index++)
        {
            var tab = Types117.Create(core ? "MauiSkiaUi.Core.SkUiCoreTabItem" : "MauiSkiaUi.SkUiTabItem")!;
            TrySet(tab, "Header", $"Tab {index}");
            TrySet(tab, "Content", core ? CorePane() : new SkUiCollectionView
            {
                ItemsSource = Enumerable.Range(0, 1000).ToList(), ItemTemplate = new DataTemplate(DeviceChecks.Card)
            });
            Types117.Method(tabs, "AddTab").Invoke(tabs, [tab]);
        }
        if (!core)
            return new SkUiContentView { Background = Colors.White, Content = (SkUiView)tabs };
        return new SkUiCoreHost { Background = Colors.White, HwAccelerated = true }.SetContent((SkUiCoreNode)tabs); // a host alone draws in software by default
    }

    private static SkUiCoreNode CorePane()
    {
        var rows = new SkUiCoreVerticalStackLayout().SetSpacing(4);
        for (var row = 0; row < 60; row++)
            rows.Add(new SkUiCoreLabel().SetText($"Row {row:00}").SetHeight(44));
        return new SkUiCoreScrollView().SetContent(rows);
    }

    public override Func<View, IDisposable?>? Motion => root =>
    {
        object? tabs = root is SkUiCoreHost host ? host.Content : ((SkUiContentView)root).Content;
        return tabs is null ? null : DeviceChecks.Repeat(root, TimeSpan.FromMilliseconds(400), step => TrySet(tabs, "SelectedIndex", (step + 1) % Tabs));
    };
}

/// <summary>The <c>carousel-swipe</c> scenario on Core (1.17 C5): 30 cards, the next one every 400 ms, animated.</summary>
public sealed class CoreCarouselSwipe : BenchScenario
{
    private const int Items = 30;

    public override string Name => "core-carousel-swipe";
    public override string Description => $"SkUiCoreCarouselView of {Items} cards (280 DIPs wide, peeking); motion = the next card every 400 ms, animated";
    public override bool DeviceOnly => true;
    public override TimeSpan MotionDuration => TimeSpan.FromSeconds(3);

    public override View Build()
    {
        if (Types117.Create("MauiSkiaUi.Core.SkUiCoreCarouselView") is not SkUiCoreNode carousel)
            return new SkUiContentView { Background = Colors.White };
        TrySet(carousel, "ItemExtent", 280d);
        TrySet(carousel, "ItemsSource", Enumerable.Range(0, Items).ToList());
        Types117.Method(carousel, "SetItemFactory").Invoke(carousel, [(Func<object?, SkUiCoreNode>)(static _ => CoreCarousels.Card()), (Action<SkUiCoreNode, object?>)CoreCarousels.Bind, null]);
        return new SkUiCoreHost { Background = Colors.White, HeightRequest = 220, HwAccelerated = true }.SetContent(carousel); // as carousel-swipe: a GPU surface of the carousel's size
    }

    public override Func<View, IDisposable?>? Motion => root =>
        root is SkUiCoreHost { Content: { } carousel }
            ? DeviceChecks.Repeat(root, TimeSpan.FromMilliseconds(400), step => TrySet(carousel, "Position", (step + 1) % Items))
            : null;
}
