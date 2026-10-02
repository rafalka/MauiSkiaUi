using MauiSkiaUi;
using MauiSkiaUi.Core;
using Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUiSamples.Samples.Controls;

/// <summary>
/// HOWTO: gradient backgrounds, drop shadows and clips on drawn views, with MAUI's own properties: <c>Background</c> takes
/// a <see cref="LinearGradientBrush"/> or <see cref="RadialGradientBrush"/> on every view, <c>Shadow</c> a MAUI
/// <see cref="Shadow"/>, and <c>Clip</c> a MAUI geometry. MAUI XAML that uses them ports by changing the prefix.
/// <para>
/// The page shows a gradient header card with a soft shadow, cards in a scroll view (scrolling moves the shadows without
/// redrawing them), an avatar clipped to a circle, a floating gradient button that lifts on tap, and a card on the Core
/// layer.
/// </para>
/// </summary>
public sealed class CardsWithShadowsSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Cards with shadows",
        Summary: "Gradient backgrounds, soft shadows and clips on drawn views: a header card, a scrolling list of cards, a round " +
            "avatar and a floating button that lifts when tapped.",
        HowTo:
        [
            "Gradient: `view.Background = new LinearGradientBrush(stops, new Point(0, 0), new Point(1, 1))`, as in MAUI; it " +
                "fills a label's or button's rounded chrome, a border's shape and a box's corners too.",
            "Shadow: `view.Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 6), Radius = 16, Opacity = 0.3f }`, " +
                "or `Shadow=\"0 6 16 Black 0.3\"` in XAML.",
            "Clip: `view.Clip = new EllipseGeometry { Center = new Point(28, 28), RadiusX = 28, RadiusY = 28 }` (in the view's " +
                "coordinates, as MAUI).",
            "Core: `node.SetBackground(gradientPaint)`, `node.SetShadow(new SkUiCoreShadow(Colors.Black, new Point(0, 6), 16, 0.3f))` " +
                "and `node.SetClip(new SkUiCoreEllipse())` (a Core shape fills the node's bounds)."
        ],
        ThingsToKnow:
        [
            "A view with an opaque fill (a border or card background, a button) casts its shadow from the fill's shape, which is " +
                "cheap; any other view from what it draws (text, images, transparent shapes), rasterized once and reused while it " +
                "does not change.",
            "Shadows draw outside the view and change neither layout nor taps. A parent with `ClipToBounds` cuts its children's " +
                "shadows: give a list room (padding) for them.",
            "Moving, fading, scaling and scrolling shadowed or clipped views is composited: nothing is redrawn or blurred again.",
            "`Clip` affects drawing only; taps still hit the rectangular bounds."
        ]);

    // One Shadow per view: MAUI makes a view the parent of its shadow, so a shared Shadow would hold on to the last view.
    private static Shadow CardShadow() => new() { Brush = Colors.Black, Offset = new Point(0, 4), Radius = 12, Opacity = 0.18f };

    public CardsWithShadowsSample() : base(Info) => SampleContent = Build();

    private static View Build()
    {
        var column = new SkUiVerticalStackLayout { Spacing = 18, Padding = new Thickness(16, 16, 16, 32) };
        column.Children.Add(Header());
        foreach (var (title, price) in new[] { ("Trail runner", "$129"), ("City backpack", "$89"), ("Rain shell", "$159"), ("Wool beanie", "$29") })
            column.Children.Add(ProductCard(title, price));
        column.Children.Add(CoreCard());

        var scroll = new SkUiScrollView { Content = column };
        var fab = FloatingButton();
        return new SkUiContentView
        {
            BackgroundColor = SampleColors.Surface,
            Content = new SkUiGrid { Children = { scroll, fab } }
        };
    }

    /// <summary>A gradient card with a soft, colored shadow and a round, clipped avatar.</summary>
    private static SkUiBorder Header()
    {
        var avatar = new SkUiLabel
        {
            Text = "AK", FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Colors.White,
            WidthRequest = 56, HeightRequest = 56, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center,
            Background = new RadialGradientBrush([new GradientStop(Color.FromArgb("#FDBA74"), 0), new GradientStop(Color.FromArgb("#EA580C"), 1)], new Point(0.3, 0.3), 0.8),
            Clip = new EllipseGeometry { Center = new Point(28, 28), RadiusX = 28, RadiusY = 28 },
        };
        var text = new SkUiVerticalStackLayout
        {
            Spacing = 2, VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new SkUiLabel { Text = "Good morning, Alex", FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                new SkUiLabel { Text = "3 orders on the way", TextColor = Colors.White.WithAlpha(0.85f) },
            }
        };
        return new SkUiBorder
        {
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            StrokeThickness = 0,
            Padding = new Thickness(18),
            Background = new LinearGradientBrush([new GradientStop(Color.FromArgb("#4F46E5"), 0), new GradientStop(Color.FromArgb("#0D9488"), 1)], new Point(0, 0), new Point(1, 1)),
            Shadow = new Shadow { Brush = Color.FromArgb("#4F46E5"), Offset = new Point(0, 10), Radius = 20, Opacity = 0.45f },
            Content = new SkUiHorizontalStackLayout { Spacing = 14, Children = { avatar, text } },
        };
    }

    /// <summary>A white card with a soft shadow: cheap, because its opaque background is its silhouette.</summary>
    private static SkUiBorder ProductCard(string title, string price) => new()
    {
        StrokeShape = new RoundRectangle { CornerRadius = 14 },
        StrokeThickness = 0,
        BackgroundColor = Colors.White,
        Padding = new Thickness(16, 12),
        Shadow = CardShadow(),
        Content = new SkUiGrid
        {
            ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)],
            Children =
            {
                new SkUiLabel { Text = title, FontSize = 17, TextColor = SampleColors.Ink, VerticalOptions = LayoutOptions.Center },
                Column(new SkUiLabel { Text = price, FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Accent }, 1),
            }
        }
    };

    /// <summary>A round gradient button that lifts (render-thread translation: the shadow moves along, nothing is blurred again).</summary>
    private static SkUiButton FloatingButton()
    {
        var fab = new SkUiButton
        {
            Text = "+", FontSize = 28, TextColor = Colors.White, CornerRadius = 28, Padding = 0,
            WidthRequest = 56, HeightRequest = 56, Margin = new Thickness(0, 0, 20, 20),
            HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End,
            Background = new LinearGradientBrush([new GradientStop(Color.FromArgb("#F43F5E"), 0), new GradientStop(Color.FromArgb("#F97316"), 1)], new Point(0, 0), new Point(1, 1)),
            Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 6), Radius = 14, Opacity = 0.35f },
        };
        var lifted = false;
        fab.Clicked += async (_, _) =>
        {
            lifted = !lifted;
            await fab.AnimateAsync(SkUiAnimatableProperty.TranslationY, lifted ? -12 : 0, 180, Easing.CubicOut);
        };
        return fab;
    }

    /// <summary>The same card on the Core layer: a Core border with a gradient background and a <see cref="SkUiCoreShadow"/>.</summary>
    private static SkUiCoreHost CoreCard()
    {
        var card = new SkUiCoreBorder()
            .SetCornerRadius(new CornerRadius(14))
            .SetStrokeThickness(0)
            .SetPadding(new Thickness(16, 12))
            .SetContent(new SkUiCoreLabel().SetText("Core card").SetTextColor(Colors.White).SetFontSize(17));
        card.SetBackground(new LinearGradientPaint(
            [new PaintGradientStop(0, Color.FromArgb("#0EA5E9")), new PaintGradientStop(1, Color.FromArgb("#6366F1"))], new Point(0, 0), new Point(1, 0)));
        card.SetShadow(new SkUiCoreShadow(Colors.Black, new Point(0, 4), 12, 0.25f));
        return new SkUiCoreHost().SetContent(card);
    }

    private static T Column<T>(T view, int column) where T : SkUiView
    {
        Grid.SetColumn(view, column);
        return view;
    }
}
