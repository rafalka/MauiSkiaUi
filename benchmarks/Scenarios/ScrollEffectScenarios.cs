using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUi.Benchmarks;

/// <summary>
/// 1.16 scroll-linked effects: a 1,000-row list with a collapsing header over it (a clipped header, a parallax picture, a
/// title moving with the content, a fading caption), against the same page without effects (<c>-none</c>), on both layers;
/// and a list whose sticky header, sticky footer and floating button hide by scroll direction (reveals). Update = a scroll
/// step of 150 DIPs: what a frame of scrolling costs on the UI thread and in pictures (none should come from the effects);
/// motion = a fling (device frame stats). Effects are made by reflection, so a <c>--baseline</c> build of a library without
/// them (before 1.16) runs the same page without effects.
/// </summary>
public sealed class ScrollEffectsScenario(string variant) : BenchScenario
{
    /// <summary>The variants: <c>skui</c>, <c>skui-none</c>, <c>core</c>, <c>core-none</c>, <c>reveal</c>.</summary>
    public static IEnumerable<BenchScenario> All() =>
        [new ScrollEffectsScenario("skui"), new ScrollEffectsScenario("skui-none"), new ScrollEffectsScenario("core"),
            new ScrollEffectsScenario("core-none"), new ScrollEffectsScenario("reveal")];

    private const int Rows = 1_000;
    private const double Header = 200;
    private const double Collapsed = 56;
    private const double Step = 150;

    private double _offset;

    private bool Core => variant.StartsWith("core", StringComparison.Ordinal);
    private bool Effects => !variant.EndsWith("-none", StringComparison.Ordinal);

    public override string Name => variant switch
    {
        "reveal" => "skui-scroll-reveal",
        _ => $"{(Core ? "core" : "skui")}-scroll-effects{(Effects ? "" : "-none")}"
    };

    public override string Description => variant switch
    {
        "reveal" => "SkUiCollectionView over 1,000 rows with a sticky header (clipped by scroll direction), a sticky footer and a floating button (hidden by direction, one reveal); update = scroll 150 DIPs; motion = a fling",
        _ => $"{(Core ? "SkUiCoreScrollView over 1,000 Core rows" : "SkUiCollectionView over 1,000 rows")} under a 200 DIP header "
            + (Effects ? "collapsing to 56 (clip, parallax, title, fading caption)" : "without effects (reference)") + "; update = scroll 150 DIPs; motion = a fling"
    };

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods,
        "MauiSkiaUi.SkUiScrollEffect", "MauiSkiaUi")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, "MauiSkiaUi.SkUiScrollReveal", "MauiSkiaUi")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods, typeof(SkUiView))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(SkUiCoreNode))]
    public override View Build()
    {
        _offset = 0;
        return variant == "reveal" ? BuildReveal() : Core ? BuildCore() : BuildSkUi();
    }

    public override Action<View>? Update => root =>
    {
        _offset = _offset + Step > 30_000 ? 0 : _offset + Step;
        switch (FindScroller(root))
        {
            case SkUiCollectionView list:
                _ = list.ScrollToAsync(_offset, animated: false);
                break;
            case SkUiCoreScrollView scroll:
                scroll.ScrollTo(0, _offset);
                break;
        }
    };

    public override Func<View, IDisposable?>? Motion => root =>
    {
        switch (FindScroller(root))
        {
            case SkUiCollectionView list:
                _ = list.ScrollToAsync(30_000, animated: true);
                break;
            case SkUiCoreScrollView scroll:
                _ = scroll.ScrollToAsync(0, 30_000);
                break;
        }
        return null;
    };

    private static object? FindScroller(View root) => root switch
    {
        SkUiGrid grid => grid.Children.OfType<SkUiCollectionView>().FirstOrDefault()
            ?? (object?)grid.Children.OfType<SkUiCoreHost>().Select(host => (host.Content as SkUiCoreGrid)?.Children.OfType<SkUiCoreScrollView>().FirstOrDefault()).FirstOrDefault(scroll => scroll is not null),
        SkUiCollectionView list => list,
        _ => null
    };

    private static SkUiCollectionView List(double insetTop) => new()
    {
        Background = Colors.White,
        ViewportInsets = new Thickness(0, insetTop, 0, 0),
        ItemsSource = Enumerable.Range(0, Rows).ToList(),
        ItemTemplate = new DataTemplate(() =>
        {
            var label = new SkUiLabel { Padding = new Thickness(16, 12), FontSize = 15, TextColor = Colors.Black };
            label.SetBinding(SkUiLabel.TextProperty, Binding.SelfPath, stringFormat: "Row {0:0000}");
            return label;
        })
    };

    private View BuildSkUi()
    {
        var list = List(Header);
        var picture = new SkUiBox { Color = Color.FromArgb("#0F766E"), HeightRequest = Header, VerticalOptions = LayoutOptions.Start };
        var title = new SkUiLabel { Text = "Collapsing header", FontSize = 20, TextColor = Colors.White, VerticalOptions = LayoutOptions.End, Margin = new Thickness(16, 0, 0, 14) };
        var caption = new SkUiLabel { Text = "Caption", FontSize = 13, TextColor = Colors.White, Margin = new Thickness(16, 16) };
        var header = new SkUiGrid { HeightRequest = Header, VerticalOptions = LayoutOptions.Start, InputTransparent = true };
        header.Children.Add(picture);
        header.Children.Add(title);
        header.Children.Add(caption);
        if (Effects)
        {
            var range = Header - Collapsed;
            TryAddEffect(header, "ClipHeight", 0, range, Header, Collapsed, list);
            TryAddEffect(picture, "TranslationY", 0, range, 0, -range / 2, list);
            TryAddEffect(title, "TranslationY", 0, range, 0, -range, list);
            TryAddEffect(caption, "Opacity", 0, 60, 1, 0, list);
        }
        var root = new SkUiGrid();
        root.Children.Add(list);
        root.Children.Add(header);
        return root;
    }

    private View BuildCore()
    {
        var stack = new SkUiCoreVerticalStackLayout();
        stack.SetPadding(new Thickness(0, Header, 0, 0));
        for (var index = 0; index < Rows; index++)
            stack.Add(new SkUiCoreLabel().SetText($"Row {index:0000}").SetFontSize(15).SetTextColor(Colors.Black).SetPadding(new Thickness(16, 12)));
        var scroll = new SkUiCoreScrollView();
        scroll.SetContent(stack);
        var picture = new SkUiCoreBox().SetColor(Color.FromArgb("#0F766E")).SetHeight(Header);
        var title = new SkUiCoreLabel().SetText("Collapsing header").SetFontSize(20).SetTextColor(Colors.White);
        title.SetMargin(new Thickness(16, 0, 0, 14)).SetVerticalAlignment(LayoutAlignment.End);
        var caption = new SkUiCoreLabel().SetText("Caption").SetFontSize(13).SetTextColor(Colors.White);
        caption.SetMargin(new Thickness(16, 16));
        var header = new SkUiCoreGrid();
        header.SetHeight(Header).SetVerticalAlignment(LayoutAlignment.Start);
        header.Add(picture);
        header.Add(title);
        header.Add(caption);
        if (Effects)
        {
            var range = Header - Collapsed;
            TryAddEffect(header, "ClipHeight", 0, range, Header, Collapsed, scroll);
            TryAddEffect(picture, "TranslationY", 0, range, 0, -range / 2, scroll);
            TryAddEffect(title, "TranslationY", 0, range, 0, -range, scroll);
            TryAddEffect(caption, "Opacity", 0, 60, 1, 0, scroll);
        }
        var grid = new SkUiCoreGrid();
        grid.Add(scroll);
        grid.Add(header);
        var root = new SkUiGrid { Background = Colors.White };
        root.Children.Add(new SkUiCoreHost().SetContent(grid));
        return root;
    }

    private static View BuildReveal()
    {
        var list = List(0);
        var reveal = TryCreateReveal(list, triggered: true);
        var header = new SkUiContentView
        {
            HeightRequest = 120, Background = Color.FromArgb("#CCFBF1"),
            Content = new SkUiLabel { Text = "Search, filters…", FontSize = 16, Margin = new Thickness(16, 18), VerticalOptions = LayoutOptions.Start }
        };
        var footer = new SkUiContentView
        {
            HeightRequest = 56, Background = Color.FromArgb("#0F766E"),
            Content = new SkUiLabel { Text = "Bottom bar", TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
        };
        var button = new SkUiButton { Text = "+", WidthRequest = 56, HeightRequest = 56, CornerRadius = 28, Margin = new Thickness(16), HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End };
        if (reveal is not null)
        {
            TryAddEffect(header, "ClipHeight", 0, 1, 120, 56, null, reveal);
            TryAddEffect(footer, "TranslationY", 0, 1, 0, 56, null, reveal);
            TryAddEffect(button, "TranslationY", 0, 1, 0, 88, null, reveal);
            TryAddEffect(button, "Opacity", 0, 1, 1, 0, null, reveal);
        }
        list.Header = header;
        list.IsStickyHeader = true;
        list.Footer = footer;
        list.IsStickyFooter = true;
        var root = new SkUiGrid();
        root.Children.Add(list);
        root.Children.Add(button);
        return root;
    }

    private static readonly Type? EffectType = Type.GetType("MauiSkiaUi.SkUiScrollEffect, MauiSkiaUi");
    private static readonly Type? RevealType = Type.GetType("MauiSkiaUi.SkUiScrollReveal, MauiSkiaUi");

    /// <summary>A scroll effect on <paramref name="target"/> (a SkUi* view or a Core node) when the library has them; else nothing.</summary>
    private static void TryAddEffect(object target, string property, double inputStart, double inputEnd, double from, double to, object? source, object? reveal = null)
    {
        if (EffectType is null || Activator.CreateInstance(EffectType) is not { } effect)
            return;
        TrySetOption(effect, "Property", property);
        TrySetOption(effect, "InputStart", inputStart);
        TrySetOption(effect, "InputEnd", inputEnd);
        TrySetOption(effect, "From", from);
        TrySetOption(effect, "To", to);
        if (source is not null)
            TrySetOption(effect, "Source", source);
        if (reveal is not null)
            TrySetOption(effect, "Reveal", reveal);
        if (target is SkUiView view && view.GetType().GetProperty("ScrollEffects", BindingFlags.Instance | BindingFlags.Public)?.GetValue(view) is IList effects)
            effects.Add(effect);
        else
            target.GetType().GetMethod("AddScrollEffect", BindingFlags.Instance | BindingFlags.Public, [EffectType])?.Invoke(target, [effect]);
    }

    private static object? TryCreateReveal(object source, bool triggered)
    {
        if (RevealType is null || Activator.CreateInstance(RevealType) is not { } reveal)
            return null;
        TrySetOption(reveal, "Source", source);
        if (triggered)
            TrySetOption(reveal, "Response", "Triggered");
        return reveal;
    }
}
