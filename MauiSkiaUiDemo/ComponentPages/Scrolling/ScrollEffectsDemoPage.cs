using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// Scroll-linked effects (1.16): the docs' recipes on a list. A header that collapses with the offset (a parallax picture,
/// stretching when pulled past the top); a collection view's sticky header and footer moved by scroll direction; a floating
/// button that hides while the list scrolls down; an extended button that shrinks to its icon; a toolbar title that fades in;
/// Core nodes following the same list. Everything runs on the render thread: nothing is recorded again while it scrolls.
/// </summary>
public sealed class ScrollEffectsDemoPage : ComponentDemoPage
{
    private const string Collapsing = "Collapsing header";
    private const string Direction = "Header and footer by direction";
    private const string Fab = "Floating button";
    private const string Extended = "Extended button";
    private const string Toolbar = "Fading toolbar";
    private const string CoreNodes = "Core nodes";

    private static readonly Color Hero = Color.FromArgb("#0F766E");
    private static readonly Color HeroLight = Color.FromArgb("#CCFBF1");

    private readonly SkUiContentView _root;
    private readonly SkUiScrollReveal _reveal = new();

    /// <summary>The floating button's reveal: hidden in step with any scroll, back (animated) when the list comes to rest.</summary>
    private readonly SkUiScrollReveal _fabReveal = new() { Response = SkUiScrollRevealResponse.WhileScrolling, Distance = 88 };

    /// <summary>The reveal the shown recipe uses; <c>null</c> when it uses none (the Show / Hide editors say so).</summary>
    private SkUiScrollReveal? _activeReveal;
    private string _recipe = Collapsing;
    private SkUiCollectionView? _list;
    private bool _reduceMotion;
    private bool _snapsToEnds = true;

    public ScrollEffectsDemoPage() : base("Scroll effects", new SkUiContentView(), widthRange: (240, 640, 360), heightRange: (320, 800, 560))
    {
        _root = (SkUiContentView)SkiaControl;
        // A view hidden past the preview's edge leaves it, as one past a page's edge leaves the window.
        _root.ClipToBounds = true;
        DocumentTypes(typeof(SkUiScrollEffect), typeof(SkUiScrollReveal));
        _reveal.PropertyChanged += (_, _) => Status();
        _fabReveal.PropertyChanged += (_, _) => Status();
        Choice("Recipe", [Collapsing, Direction, Fab, Extended, Toolbar, CoreNodes], Collapsing, Show, () => _recipe);
        Choice(nameof(SkUiScrollReveal.Response), Enum.GetValues<SkUiScrollRevealResponse>(), SkUiScrollRevealResponse.Tracked,
            value => _reveal.Response = value, () => _reveal.Response);
        Number(nameof(SkUiScrollReveal.Distance), 0, 200, 56, value => _reveal.Distance = value, () => _reveal.Distance, whole: true);
        Toggle(nameof(SkUiScrollReveal.ShowsNearStart), true, value => _reveal.ShowsNearStart = value, () => _reveal.ShowsNearStart);
        Toggle("Reduce motion", false, value =>
        {
            _reduceMotion = value;
            SkUiMotion.ReduceMotion = value ? true : null;
            Show(_recipe); // read when the effects next commit
        }, () => _reduceMotion);
        Toggle(nameof(SkUiScrollEffect.SnapsToEnds), true, value => { _snapsToEnds = value; Show(_recipe); }, () => _snapsToEnds);
        ActionButton("Reveal: show", () => DriveReveal(show: true));
        ActionButton("Reveal: hide", () => DriveReveal(show: false));
        Show(Collapsing);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_reduceMotion)
            SkUiMotion.ReduceMotion = null;
    }

    private void Show(string recipe)
    {
        _recipe = recipe;
        // A reveal follows one list: the old page's effects let it go when they leave the tree.
        _reveal.Source = null;
        _fabReveal.Source = null;
        _activeReveal = recipe switch
        {
            Direction or Extended or CoreNodes => _reveal,
            Fab => _fabReveal,
            _ => null
        };
        _root.Content = recipe switch
        {
            Direction => HeaderAndFooter(),
            Fab => FloatingButton(),
            Extended => ExtendedButton(),
            Toolbar => FadingToolbar(),
            CoreNodes => CoreRecipe(),
            _ => CollapsingHeader()
        };
        Status();
    }

    private void Status()
    {
        if (_list is null)
            return;
        Feedback(_activeReveal is { } reveal
            ? $"Offset {_list.ScrollY:F0}; {reveal.Response} reveal {(reveal.IsShown ? "shown" : "hidden")}"
            : $"Offset {_list.ScrollY:F0}; this recipe uses no reveal");
    }

    /// <summary>The Show / Hide editors: <see cref="SkUiScrollReveal.Show"/> or <see cref="SkUiScrollReveal.Hide"/> on the recipe's reveal.</summary>
    private void DriveReveal(bool show)
    {
        if (_activeReveal is not { } reveal)
        {
            Feedback("This recipe uses no reveal: Show and Hide drive the header and footer, floating button, extended button and Core recipes.");
            return;
        }
        if (show)
            reveal.Show();
        else
            reveal.Hide();
    }

    private SkUiCollectionView NewList(Thickness insets = default)
    {
        var rows = Enumerable.Range(1, 80).Select(index => $"Row {index}").ToList();
        _list = new SkUiCollectionView
        {
            ItemsSource = rows,
            ViewportInsets = insets,
            Overscroll = SkUiOverscrollMode.Stretch,
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new SkUiLabel { Padding = new Thickness(16, 14), FontSize = 15, TextColor = Ink };
                label.SetBinding(SkUiLabel.TextProperty, Binding.SelfPath);
                return label;
            })
        };
        _list.Scrolled += (_, _) => Status();
        return _list;
    }

    /// <summary>
    /// Recipe 1: a 200 DIP header over the list, clipped to 56 as it scrolls (the clip is on the header, which stays put; it
    /// clips what is inside it), its picture moving up at half speed inside, its title moving with the content to the bottom
    /// of the collapsed bar, and the picture growing when the list is pulled past its top.
    /// </summary>
    private SkUiGrid CollapsingHeader()
    {
        var list = NewList(new Thickness(0, 200, 0, 0));
        var picture = new SkUiBox { Color = Hero, HeightRequest = 200, VerticalOptions = LayoutOptions.Start, AnchorY = 0 };
        picture.AddScrollEffects(
            new SkUiScrollEffect(SkUiScrollEffectProperty.TranslationY, 0, 144, 0, -72) { Source = list },
            new SkUiScrollEffect(SkUiScrollEffectProperty.Scale, -200, 0, 1.4, 1) { Source = list });
        var title = new SkUiLabel { Text = "Collapsing header", TextColor = Colors.White, FontSize = 20, Margin = new Thickness(16, 0, 0, 14), VerticalOptions = LayoutOptions.End };
        title.AddScrollEffect(new SkUiScrollEffect(SkUiScrollEffectProperty.TranslationY, 0, 144, 0, -144) { Source = list });
        var header = new SkUiGrid { HeightRequest = 200, VerticalOptions = LayoutOptions.Start, InputTransparent = true, ClipToBounds = false };
        header.Children.Add(picture);
        header.Children.Add(title);
        // 200 → 56 over the first 144 DIPs; the same slope on into a pull (400 at -200), so the growing picture is not cut.
        header.AddScrollEffect(new SkUiScrollEffect(SkUiScrollEffectProperty.ClipHeight, -200, 144, 400, 56) { Source = list });
        // Never left half open (the collapsing header and the Core bar).
        header.AddScrollEffect(new SkUiScrollEffect(SkUiScrollEffectProperty.ClipHeight, 0, 144, 200, 56) { Source = list, SnapsToEnds = _snapsToEnds });
        return Stack(list, header);
    }

    /// <summary>Recipe 2: the sticky header shrinks from 120 to 56 by scroll direction, the sticky footer slides out (shared reveal).</summary>
    private SkUiCollectionView HeaderAndFooter()
    {
        var list = NewList();
        var header = new SkUiContentView
        {
            HeightRequest = 120, Background = HeroLight,
            Content = new SkUiLabel { Text = "Search, filters…", FontSize = 16, TextColor = Ink, Margin = new Thickness(16, 18), VerticalOptions = LayoutOptions.Start }
        };
        header.AddScrollEffect(new SkUiScrollEffect { Property = SkUiScrollEffectProperty.ClipHeight, From = 120, To = 56, Reveal = _reveal });
        var footer = new SkUiContentView
        {
            HeightRequest = 56, Background = Hero,
            Content = new SkUiLabel { Text = "Bottom bar", TextColor = Colors.White, FontSize = 16, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
        };
        footer.AddScrollEffect(SkUiScrollEffect.HideOnScroll(_reveal, 56));
        list.SetHeader(header, sticky: true);
        list.Footer = footer;
        list.IsStickyFooter = true;
        return list;
    }

    /// <summary>
    /// Recipe 3: a floating button that slides out and fades while the list scrolls, either way, in step with it (88 DIPs of
    /// scrolling hide it fully), and comes back, animated, when the list comes to rest: a
    /// <see cref="SkUiScrollRevealResponse.WhileScrolling"/> reveal, on the render thread.
    /// </summary>
    private SkUiGrid FloatingButton()
    {
        var list = NewList();
        _fabReveal.Source = list;
        var button = new SkUiButton
        {
            Text = "+", FontSize = 26, WidthRequest = 56, HeightRequest = 56, CornerRadius = 28, Margin = new Thickness(16),
            HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End
        };
        button.AddScrollEffects(
            SkUiScrollEffect.HideOnScroll(_fabReveal, 88),
            new SkUiScrollEffect { Property = SkUiScrollEffectProperty.Opacity, From = 1, To = 0, Reveal = _fabReveal });
        return Stack(list, button);
    }

    /// <summary>Recipe 4: a 160 DIP capsule shrinking to its 56 DIP icon at the trailing end; the label fades, the icon follows the leading edge.</summary>
    private SkUiGrid ExtendedButton()
    {
        var list = NewList();
        _reveal.Source = list;
        var icon = new SkUiLabel { Text = "✎", FontSize = 22, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center };
        icon.AddScrollEffect(new SkUiScrollEffect { Property = SkUiScrollEffectProperty.TranslationX, From = 0, To = 104, Reveal = _reveal });
        var label = new SkUiLabel { Text = "Compose", FontSize = 16, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center };
        label.AddScrollEffect(new SkUiScrollEffect { Property = SkUiScrollEffectProperty.Opacity, From = 1, To = 0, Reveal = _reveal });
        var content = new SkUiGrid { ColumnDefinitions = [new ColumnDefinition(56), new ColumnDefinition(GridLength.Star)] };
        content.Children.Add(icon);
        Grid.SetColumn(label, 1);
        content.Children.Add(label);
        var button = new SkUiBorder
        {
            WidthRequest = 160, HeightRequest = 56, Margin = new Thickness(16), HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End,
            Background = Hero, StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 28 },
            Shadow = new Shadow { Brush = Colors.Black, Opacity = 0.3f, Radius = 8, Offset = new Point(0, 3) },
            Content = content
        };
        button.AddScrollEffect(new SkUiScrollEffect
        {
            Property = SkUiScrollEffectProperty.ClipWidth, ClipEdge = SkUiClipEdge.End, ClipShape = SkUiClipShape.Capsule, From = 160, To = 56, Reveal = _reveal
        });
        return Stack(list, button);
    }

    /// <summary>A toolbar whose title fades in once the list's own big title (its first row) has scrolled under it.</summary>
    private SkUiGrid FadingToolbar()
    {
        var list = NewList(new Thickness(0, 56, 0, 0));
        var title = new SkUiLabel { Text = "Inbox", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Ink, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        title.AddScrollEffect(SkUiScrollEffect.FadeIn(20, 60).SetSource(list));
        var bar = new SkUiContentView { HeightRequest = 56, VerticalOptions = LayoutOptions.Start, Background = Colors.White, Content = title };
        var line = new SkUiBox { HeightRequest = 1, Color = Colors.LightGray, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, 56, 0, 0) };
        line.AddScrollEffect(SkUiScrollEffect.FadeIn(0, 20).SetSource(list));
        list.SetHeader(new SkUiLabel { Text = "Inbox", FontSize = 30, FontAttributes = FontAttributes.Bold, TextColor = Ink, Padding = new Thickness(16, 8) }, sticky: false);
        return Stack(list, bar, line);
    }

    /// <summary>Core nodes following the SkUi* list: a header bar clipped and a chip faded by offset, a dot hidden by direction.</summary>
    private SkUiGrid CoreRecipe()
    {
        var list = NewList(new Thickness(0, 96, 0, 0));
        _reveal.Source = list;
        var bar = new SkUiCoreBox().SetColor(Hero).SetHeight(96);
        bar.AddScrollEffect(SkUiScrollEffect.CollapseTo(96, 40).SetSource(list).SetSnapsToEnds(_snapsToEnds));
        var chip = new SkUiCoreLabel().SetText("Core").SetTextColor(Colors.White).SetFontSize(14);
        chip.SetMargin(new Thickness(16, 12)).SetHorizontalAlignment(LayoutAlignment.Start)
            .SetVerticalAlignment(LayoutAlignment.Start);
        chip.AddScrollEffect(new SkUiScrollEffect(SkUiScrollEffectProperty.Opacity, 0, 40, 1, 0) { Source = list });
        var dot = new SkUiCoreBox().SetColor(Accent).SetCornerRadius(new CornerRadius(14)).SetWidth(28).SetHeight(28);
        dot.SetMargin(new Thickness(16)).SetHorizontalAlignment(LayoutAlignment.End)
            .SetVerticalAlignment(LayoutAlignment.End);
        dot.AddScrollEffect(SkUiScrollEffect.HideOnScroll(_reveal, 60));
        var grid = new SkUiCoreGrid();
        grid.Add(bar.SetVerticalAlignment(LayoutAlignment.Start));
        grid.Add(chip);
        grid.Add(dot);
        return Stack(list, new SkUiCoreHost { InputTransparent = true }.SetContent(grid));
    }

    private static SkUiGrid Stack(params SkUiView[] layers)
    {
        var grid = new SkUiGrid();
        foreach (var layer in layers)
            grid.Children.Add(layer);
        return grid;
    }
}
