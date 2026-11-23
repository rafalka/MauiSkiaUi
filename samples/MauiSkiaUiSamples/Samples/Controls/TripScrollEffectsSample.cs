using MauiSkiaUi;
using Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUiSamples.Samples.Controls;

/// <summary>
/// HOWTO: build a screen whose parts react to scrolling, with scroll-linked effects (<see cref="SkUiScrollEffect"/>,
/// <see cref="SkUiScrollReveal"/>). A trip's page: a landscape header in layers that move at different speeds, collapses into
/// a toolbar (and grows when the page is pulled down), a title that moves and shrinks into the toolbar, a toolbar that fades
/// in with a reading-progress line, section chips that stick under it, a carousel whose pictures move inside their cards,
/// itinerary days that slide in as they scroll into view, a banner with its own parallax, a bottom bar and an extended
/// Book button that get out of the way while you read, and a back-to-top button that appears further down.
/// <para>
/// Every effect is evaluated on the render thread in the frame that draws the scroll position: nothing lags the content
/// during a fling and nothing is recorded again while the page scrolls. The views keep their layout; effects move, scale,
/// fade and clip them. Ranges that depend on where things are laid out (the chips, the days, the banner, the end of the
/// page) are set once the page knows its layout (<see cref="UpdateRanges"/>).
/// </para>
/// </summary>
public sealed class TripScrollEffectsSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Scroll effects",
        Summary: "A trip's page whose header, title, toolbar, chips, pictures, cards and buttons follow the scroll: " +
            "collapsing, parallax in layers, sticky chips, slide-in cards and bars that hide while you read.",
        HowTo:
        [
            "Put the scroller first in a grid and the parts that react to it over it (header, toolbar, chips, bars).",
            "Add `SkUiScrollEffect`s to those views with `Source` set to the scroller: a property, an offset range " +
                "(`InputStart`…`InputEnd`) and the values at its ends (`From`, `To`).",
            "Collapse a header with a `ClipHeight` effect on the header itself, and move what is inside it with " +
                "`TranslationY` effects: a clip also clips the view's children.",
            "For bars that hide while you scroll down and come back when you scroll up, share one `SkUiScrollReveal` " +
                "between their effects (`Reveal`): `From` is the shown value, `To` the hidden one.",
            "Set ranges that depend on the layout (a section's position, the end of the page) when the layout is known."
        ],
        ThingsToKnow:
        [
            "Effects combine with the view's own values and with each other: translations add, scales and opacities " +
                "multiply, clips take the smaller.",
            "`SnapsToEnds` keeps the header from stopping half collapsed; a range below 0 follows a pull past the top.",
            "A view faded to 0 or clipped away takes no taps and is hidden from screen readers.",
            "Under reduced motion fades and clips still run, while parallax and scaling hold `From`. The days' slide-in " +
                "runs its range backwards so that `From` is where they end up.",
            "Effects never change layout: the content leaves room for the header with its own top padding."
        ]);

    private const double Hero = 320;
    private const double Bar = 56;
    private const double Collapse = Hero - Bar;
    private const double ChipsHeight = 48;
    private const double BottomBar = 72;
    private const double CardWidth = 220;
    private const double CardGap = 12;

    private static readonly Color Night = Color.FromArgb("#0B3B4A");
    private static readonly Color Dusk = Color.FromArgb("#F59E8B");
    private static readonly Color Gold = Color.FromArgb("#FCD34D");
    private static readonly Color FarRidge = Color.FromArgb("#2F6B78");
    private static readonly Color NearRidge = Color.FromArgb("#174652");

    private const string Overview = "Overview";
    private const string Itinerary = "Itinerary";
    private const string Stay = "Stay";
    private const string Reviews = "Reviews";

    private readonly SkUiScrollView _scroll;
    private readonly SkUiVerticalStackLayout _content = new() { Padding = new Thickness(0, Hero, 0, BottomBar + 48), Spacing = 0, BackgroundColor = SampleColors.Page };
    private readonly SkUiScrollReveal _reveal = new() { Response = SkUiScrollRevealResponse.Triggered };
    private readonly Dictionary<string, SkUiView> _sections = [];
    private readonly List<(SkUiView Card, SkUiScrollEffect Fade, SkUiScrollEffect Slide)> _days = [];
    private readonly SkUiContentView _chips;
    private readonly SkUiScrollEffect _chipsMove;
    private readonly SkUiScrollEffect _chipsDivider;
    private readonly SkUiScrollEffect _progress;
    private SkUiView? _banner;
    private SkUiScrollEffect? _bannerParallax;

    public TripScrollEffectsSample() : base(Info, fillsPage: true)
    {
        _scroll = new SkUiScrollView { Content = _content, Overscroll = SkUiOverscrollMode.Stretch, BackgroundColor = SampleColors.Page };
        _reveal.Source = _scroll;
        (_chips, _chipsMove, _chipsDivider) = Chips();
        BuildContent();
        _progress = new SkUiScrollEffect(SkUiScrollEffectProperty.ScaleX, 0, 1, 0, 1) { Source = _scroll };

        var root = new SkUiGrid { BackgroundColor = SampleColors.Page };
        root.Children.Add(_scroll);
        root.Children.Add(Header());
        root.Children.Add(ToolbarButtons());
        root.Children.Add(BottomSummary());
        root.Children.Add(BookButton());
        root.Children.Add(BackToTop());
        SampleContent = root;

        // Ranges that depend on the layout: once it is known, and again when it changes (a rotation, a resize).
        _content.SizeChanged += (_, _) => AfterLayout();
        _scroll.SizeChanged += (_, _) => AfterLayout();
    }

    #region Header and toolbar

    /// <summary>
    /// The landscape in layers, clipped from 320 to the toolbar's 56 DIPs over the first 264 of scroll. The clip is on the
    /// header (it stays in place); the layers inside it move up at their own speeds, and grow when the page is pulled down.
    /// </summary>
    private SkUiGrid Header()
    {
        var scenery = new SkUiGrid { AnchorY = 0, IsClippedToBounds = false };
        scenery.AddScrollEffect(new SkUiScrollEffect(SkUiScrollEffectProperty.Scale, 0, -200, 1, 1.35) { Source = _scroll });
        scenery.Children.Add(Layer(new SkUiBox { Background = new LinearGradientBrush([new(Night, 0), new(Dusk, 0.85f), new(Gold, 1)], new Point(0, 0), new Point(0, 1)) }, rate: 0.25));
        var sun = new SkUiEllipse { Fill = Gold, WidthRequest = 72, HeightRequest = 72, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, 110, 56, 0) };
        sun.AddScrollEffect(new SkUiScrollEffect(SkUiScrollEffectProperty.Opacity, 0, 160, 1, 0) { Source = _scroll });
        scenery.Children.Add(Layer(sun, rate: 0.15));
        scenery.Children.Add(Layer(Ridge(FarRidge, "0,140 60,70 120,110 190,40 260,100 330,55 400,120 400,180 0,180", 150), rate: 0.4));
        scenery.Children.Add(Layer(Ridge(NearRidge, "0,120 80,60 150,105 230,35 310,95 400,70 400,160 0,160", 110), rate: 0.65));
        // Legibility under the title, and the toolbar's color fading in as the header reaches it.
        scenery.Children.Add(new SkUiBox { Background = new LinearGradientBrush([new(Colors.Transparent, 0.55f), new(Color.FromRgba(0, 0, 0, 0.45), 1)], new Point(0, 0), new Point(0, 1)), InputTransparent = true });

        var toolbar = new SkUiBox { Color = SampleColors.Accent, HeightRequest = Bar, VerticalOptions = LayoutOptions.Start };
        toolbar.AddScrollEffect(new SkUiScrollEffect(SkUiScrollEffectProperty.Opacity, Collapse - 70, Collapse, 0, 1) { Source = _scroll });
        var progress = new SkUiBox { Color = Gold, HeightRequest = 3, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, Bar - 3, 0, 0), AnchorX = 0 };
        progress.AddScrollEffects(_progress, new SkUiScrollEffect(SkUiScrollEffectProperty.Opacity, Collapse - 70, Collapse, 0, 1) { Source = _scroll });

        // The title moves into the toolbar (beside the back button) and shrinks to its size; the subtitle fades.
        var title = new SkUiLabel
        {
            Text = "Lofoten Islands", FontSize = 30, FontAttributes = FontAttributes.Bold, TextColor = Colors.White,
            VerticalOptions = LayoutOptions.End, Margin = new Thickness(20, 0, 20, 52), AnchorX = 0, AnchorY = 0
        };
        title.AddScrollEffects(
            new SkUiScrollEffect(SkUiScrollEffectProperty.TranslationY, 0, Collapse, 0, -(Hero - 52 - 40 - 12)) { Source = _scroll },
            new SkUiScrollEffect(SkUiScrollEffectProperty.TranslationX, 0, Collapse, 0, 40) { Source = _scroll },
            new SkUiScrollEffect(SkUiScrollEffectProperty.Scale, 0, Collapse, 1, 0.62) { Source = _scroll });
        var subtitle = new SkUiLabel
        {
            Text = "Norway · 12 days · Arctic summer", FontSize = 15, TextColor = Color.FromRgba(255, 255, 255, 0.9),
            VerticalOptions = LayoutOptions.End, Margin = new Thickness(20, 0, 20, 24)
        };
        subtitle.AddScrollEffects(
            new SkUiScrollEffect(SkUiScrollEffectProperty.Opacity, 0, 90, 1, 0) { Source = _scroll },
            new SkUiScrollEffect(SkUiScrollEffectProperty.TranslationY, 0, Collapse, 0, -Collapse) { Source = _scroll });

        var header = new SkUiGrid { HeightRequest = Hero, VerticalOptions = LayoutOptions.Start, InputTransparent = true, IsClippedToBounds = false };
        header.Children.Add(scenery);
        header.Children.Add(toolbar);
        header.Children.Add(progress);
        header.Children.Add(title);
        header.Children.Add(subtitle);
        // 320 → 56 over the first 264 DIPs, on at the same slope into a pull (so the growing landscape is not cut), and
        // never left half collapsed.
        header.AddScrollEffects(
            new SkUiScrollEffect(SkUiScrollEffectProperty.ClipHeight, -200, Collapse, Hero + 200, Bar) { Source = _scroll },
            new SkUiScrollEffect(SkUiScrollEffectProperty.ClipHeight, 0, Collapse, Hero, Bar) { Source = _scroll, SnapsToEnds = true });
        return header;
    }

    /// <summary>A scenery layer moving up at <paramref name="rate"/> times the scroll speed.</summary>
    private SkUiView Layer(SkUiView view, double rate)
    {
        view.AddScrollEffect(new SkUiScrollEffect(SkUiScrollEffectProperty.TranslationY, 0, Collapse, 0, -rate * Collapse) { Source = _scroll });
        return view;
    }

    private static SkUiPolygon Ridge(Color color, string points, double height) => new()
    {
        Fill = color, StrokeThickness = 0, Aspect = Stretch.Fill, HeightRequest = height, VerticalOptions = LayoutOptions.End,
        Points = (PointCollection)new PointCollectionConverter().ConvertFromInvariantString(points)!
    };

    /// <summary>Back-to-top and favorite buttons on the toolbar: their round backdrops fade out as the toolbar's color fades in.</summary>
    private SkUiGrid ToolbarButtons()
    {
        var (back, _) = RoundButton("‹", "Back to the top", LayoutOptions.Start);
        back.Tapped += async (_, _) => await _scroll.ScrollToAsync(0, 0, animated: true);
        var (favorite, glyph) = RoundButton("♡", "Favorite", LayoutOptions.End);
        favorite.Tapped += (_, _) => glyph.Text = glyph.Text == "♡" ? "♥" : "♡";
        var bar = new SkUiGrid { HeightRequest = Bar, VerticalOptions = LayoutOptions.Start, Padding = new Thickness(8, 0), InputTransparent = true, CascadeInputTransparent = false };
        bar.Children.Add(back);
        bar.Children.Add(favorite);
        return bar;
    }

    private (SkUiGrid Button, SkUiLabel Glyph) RoundButton(string glyph, string description, LayoutOptions side)
    {
        var backdrop = new SkUiEllipse { Fill = Color.FromRgba(0, 0, 0, 0.35) };
        backdrop.AddScrollEffect(new SkUiScrollEffect(SkUiScrollEffectProperty.Opacity, Collapse - 70, Collapse, 1, 0) { Source = _scroll });
        var label = new SkUiLabel { Text = glyph, FontSize = 22, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        var button = new SkUiGrid { WidthRequest = 40, HeightRequest = 40, HorizontalOptions = side, VerticalOptions = LayoutOptions.Center, Children = { backdrop, label } };
        SemanticProperties.SetDescription(button, description);
        return (button, label);
    }

    #endregion

    #region Content

    private void BuildContent()
    {
        _content.Children.Add(Section(Overview, new SkUiVerticalStackLayout
        {
            Padding = new Thickness(20, 20, 20, 8), Spacing = 10,
            Children =
            {
                new SkUiLabel { Text = "★ 4.9 · 2,184 reviews · from Svolvær", FontSize = 14, TextColor = SampleColors.Caption },
                Paragraph("Jagged peaks rise straight out of the sea, fishing villages sit on stilts in quiet bays and in June the sun never sets. " +
                    "Twelve days of hiking, kayaking and slow mornings between Svolvær and Å.")
            }
        }));
        _content.Children.Add(_chips);
        _content.Children.Add(Heading("Highlights"));
        _content.Children.Add(Highlights());
        _sections[Itinerary] = Heading(Itinerary);
        _content.Children.Add(_sections[Itinerary]);
        string[] days =
        [
            "Arrive in Svolvær, harbor walk", "Reinebringen at dawn", "Kayak to Kjerkfjorden", "Nusfjord fishing village",
            "Kvalvika beach camp", "Ryten summit", "Henningsvær by bike", "Rest day and sauna", "Whale safari from Andenes",
            "Haukland beach", "Midnight sun on Offersøykammen", "Back to Bodø"
        ];
        for (var index = 0; index < days.Length; index++)
        {
            var card = Day(index + 1, days[index]);
            _content.Children.Add(card);
            if (index == 5)
                _content.Children.Add(_banner = Banner());
        }
        _sections[Stay] = Heading("Where you'll stay");
        _content.Children.Add(_sections[Stay]);
        _content.Children.Add(Section(Stay + "-text", new SkUiVerticalStackLayout
        {
            Padding = new Thickness(20, 0, 20, 8),
            Children = { Paragraph("Red fishermen's cabins (rorbuer) over the water, a farmhouse in Flakstad and two nights under canvas at Kvalvika.") }
        }));
        _sections[Reviews] = Heading(Reviews);
        _content.Children.Add(_sections[Reviews]);
        (string Name, string Text)[] reviews =
        [
            ("Ingrid", "The best two weeks of my year. The guides knew every path."), ("Marco", "Kvalvika at midnight: unreal."),
            ("Aiko", "Small group, great food, perfect pace."), ("Sam", "Bring good boots. Then bring better boots.")
        ];
        for (var index = 0; index < reviews.Length; index++)
            _content.Children.Add(Review(index + 1, reviews[index].Name, reviews[index].Text));
    }

    private SkUiView Section(string name, SkUiView view)
    {
        _sections[name] = view;
        return view;
    }

    private static SkUiLabel Heading(string text) => new()
    {
        Text = text, FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Ink, Padding = new Thickness(20, 24, 20, 12)
    };

    private static SkUiLabel Paragraph(string text) => new() { Text = text, FontSize = 16, TextColor = SampleColors.Ink, LineHeight = 1.3 };

    /// <summary>A carousel whose pictures move inside their cards as it scrolls (horizontal parallax).</summary>
    private static SkUiScrollView Highlights()
    {
        (string Title, Color From, Color To)[] items =
        [
            ("Reinebringen", Color.FromArgb("#1E3A8A"), Color.FromArgb("#F472B6")), ("Kvalvika", Color.FromArgb("#065F46"), Color.FromArgb("#FDE68A")),
            ("Nusfjord", Color.FromArgb("#7C2D12"), Color.FromArgb("#FDBA74")), ("Henningsvær", Color.FromArgb("#0F172A"), Color.FromArgb("#38BDF8")),
            ("Andenes", Color.FromArgb("#312E81"), Color.FromArgb("#A5B4FC")), ("Haukland", Color.FromArgb("#134E4A"), Color.FromArgb("#5EEAD4"))
        ];
        var row = new SkUiHorizontalStackLayout { Spacing = CardGap, Padding = new Thickness(20, 0, 20, 8) };
        var carousel = new SkUiScrollView { Orientation = ScrollOrientation.Horizontal, Content = row, HorizontalScrollBarVisibility = ScrollBarVisibility.Never };
        for (var index = 0; index < items.Length; index++)
        {
            var (title, from, to) = items[index];
            // As wide as the card, scaled up so that 40 DIPs of travel either way never uncover its edges (a view is laid
            // out within its card: a wider request with a negative margin would be cut to the card's width).
            var picture = new SkUiBox
            {
                Background = new LinearGradientBrush([new(from, 0), new(to, 1)], new Point(0, 0), new Point(1, 1)),
                ScaleX = (CardWidth + 80) / CardWidth
            };
            // While the card crosses the carousel, its picture slides 80 DIPs the other way.
            var left = 20 + index * (CardWidth + CardGap);
            picture.AddScrollEffect(new SkUiScrollEffect(SkUiScrollEffectProperty.TranslationX, left - 400, left + CardWidth, -40, 40)
            {
                Source = carousel, Orientation = ScrollOrientation.Horizontal
            });
            var card = new SkUiBorder
            {
                WidthRequest = CardWidth, HeightRequest = 150, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Content = new SkUiGrid
                {
                    Children =
                    {
                        picture,
                        new SkUiLabel { Text = title, FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, Margin = new Thickness(14), VerticalOptions = LayoutOptions.End }
                    }
                }
            };
            row.Children.Add(card);
        }
        return carousel;
    }

    /// <summary>A day of the itinerary; it fades and slides in as it scrolls into view (ranges set in <see cref="UpdateRanges"/>).</summary>
    private SkUiBorder Day(int number, string text)
    {
        var card = new SkUiBorder
        {
            Margin = new Thickness(16, 6), Padding = new Thickness(14), BackgroundColor = SampleColors.Surface, Stroke = SampleColors.Border,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Content = new SkUiHorizontalStackLayout
            {
                Spacing = 14,
                Children =
                {
                    new SkUiBorder
                    {
                        WidthRequest = 36, HeightRequest = 36, BackgroundColor = SampleColors.Accent, StrokeThickness = 0, StrokeShape = new Ellipse(),
                        Content = new SkUiLabel { Text = number.ToString(System.Globalization.CultureInfo.InvariantCulture), TextColor = Colors.White, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
                    },
                    new SkUiLabel { Text = text, FontSize = 16, TextColor = SampleColors.Ink, VerticalOptions = LayoutOptions.Center }
                }
            }
        };
        var fade = new SkUiScrollEffect { Property = SkUiScrollEffectProperty.Opacity, Source = _scroll, From = 1, To = 1 };
        var slide = new SkUiScrollEffect { Property = SkUiScrollEffectProperty.TranslationY, Source = _scroll, From = 0, To = 0 };
        card.AddScrollEffects(fade, slide);
        _days.Add((card, fade, slide));
        return card;
    }

    /// <summary>A banner whose picture moves inside it as it passes (ranges set in <see cref="UpdateRanges"/>).</summary>
    private SkUiBorder Banner()
    {
        // Fills the banner, stretched down from its top so that 140 DIPs of travel up never uncover its bottom.
        var picture = new SkUiBox
        {
            Background = new LinearGradientBrush([new(Night, 0), new(Dusk, 0.6f), new(Gold, 1)], new Point(0, 0), new Point(0, 1)),
            AnchorY = 0, ScaleY = 1.8
        };
        _bannerParallax = new SkUiScrollEffect { Property = SkUiScrollEffectProperty.TranslationY, Source = _scroll };
        picture.AddScrollEffect(_bannerParallax);
        return new SkUiBorder
        {
            HeightRequest = 180, Margin = new Thickness(16, 18), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Content = new SkUiGrid
            {
                Children =
                {
                    picture,
                    new SkUiLabel { Text = "Midnight sun", FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, Margin = new Thickness(18, 0, 18, 40), VerticalOptions = LayoutOptions.End },
                    new SkUiLabel { Text = "24 hours of daylight from late May to mid July", FontSize = 14, TextColor = Colors.White, Margin = new Thickness(18, 0, 18, 18), VerticalOptions = LayoutOptions.End }
                }
            }
        };
    }

    private static SkUiGrid Review(int photo, string name, string text)
    {
        var avatar = new SkUiImage { Source = $"contact_photo_{photo}.png", WidthRequest = 40, HeightRequest = 40, DownsampleWidth = 40, VerticalOptions = LayoutOptions.Start };
        avatar.Transformations.Add(new SkUiCircleTransformation());
        var words = new SkUiVerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new SkUiLabel { Text = name, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Ink },
                new SkUiLabel { Text = text, FontSize = 15, TextColor = SampleColors.Caption }
            }
        };
        Grid.SetColumn(words, 1);
        return new SkUiGrid
        {
            Padding = new Thickness(20, 8), ColumnSpacing = 12, ColumnDefinitions = [new ColumnDefinition(40), new ColumnDefinition(GridLength.Star)],
            Children = { avatar, words }
        };
    }

    /// <summary>
    /// Section chips that scroll with the content and then stick under the toolbar. They stay in the content (drags on them
    /// scroll the page, taps reach them) and, once the content has carried them to the toolbar's bottom, an effect moves them
    /// down by exactly as much as the content moves up (set in <see cref="UpdateRanges"/>). <c>ZIndex</c> draws them over
    /// the rows that follow them.
    /// </summary>
    private (SkUiContentView Chips, SkUiScrollEffect Hold, SkUiScrollEffect Divider) Chips()
    {
        var row = new SkUiHorizontalStackLayout { Spacing = 8, Padding = new Thickness(16, 8), VerticalOptions = LayoutOptions.Center };
        foreach (var name in new[] { Overview, Itinerary, Stay, Reviews })
        {
            var chip = new SkUiBorder
            {
                Padding = new Thickness(14, 6), StrokeShape = new RoundRectangle { CornerRadius = 16 }, Stroke = SampleColors.Border, BackgroundColor = SampleColors.Surface,
                Content = new SkUiLabel { Text = name, FontSize = 14, TextColor = SampleColors.Ink }
            };
            chip.Tapped += async (_, _) => await ScrollToSection(name);
            row.Children.Add(chip);
        }
        var divider = new SkUiBox { HeightRequest = 1, Color = SampleColors.Border, VerticalOptions = LayoutOptions.End };
        var dividerFade = new SkUiScrollEffect(SkUiScrollEffectProperty.Opacity, 0, 1, 0, 1) { Source = _scroll };
        divider.AddScrollEffect(dividerFade);
        var chips = new SkUiContentView
        {
            HeightRequest = ChipsHeight, BackgroundColor = SampleColors.Page, ZIndex = 1,
            Content = new SkUiGrid { Children = { row, divider } }
        };
        var hold = new SkUiScrollEffect { Property = SkUiScrollEffectProperty.TranslationY, Source = _scroll };
        chips.AddScrollEffect(hold);
        return (chips, hold, dividerFade);
    }

    private async Task ScrollToSection(string name)
    {
        if (_sections.TryGetValue(name, out var section))
            await _scroll.ScrollToAsync(0, Math.Max(0, section.Frame.Y - Bar - ChipsHeight), animated: true);
    }

    #endregion

    #region Bars and buttons

    /// <summary>The price bar: slides out while you scroll down, back when you scroll up (the shared reveal).</summary>
    private SkUiContentView BottomSummary()
    {
        var bar = new SkUiContentView
        {
            HeightRequest = BottomBar, VerticalOptions = LayoutOptions.End, BackgroundColor = SampleColors.Surface, Padding = new Thickness(20, 0),
            Shadow = new Shadow { Brush = Colors.Black, Opacity = 0.12f, Radius = 10, Offset = new Point(0, -2) },
            Content = new SkUiVerticalStackLayout
            {
                VerticalOptions = LayoutOptions.Center, Spacing = 2,
                Children =
                {
                    new SkUiLabel { Text = "€1,290 per person", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Ink },
                    new SkUiLabel { Text = "12 days · next departure 14 June", FontSize = 13, TextColor = SampleColors.Caption }
                }
            }
        };
        bar.AddScrollEffect(SkUiScrollEffect.HideOnScroll(_reveal, BottomBar + 12));
        return bar;
    }

    /// <summary>
    /// An extended Book button over the bar: while you read it shrinks to its icon at its trailing end (a capsule clip), its
    /// label fades and its icon follows the leading edge; it also drops to where the bar was.
    /// </summary>
    private SkUiBorder BookButton()
    {
        const double wide = 136;
        const double narrow = 56;
        var icon = new SkUiLabel { Text = "✈", FontSize = 20, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center };
        icon.AddScrollEffect(new SkUiScrollEffect { Property = SkUiScrollEffectProperty.TranslationX, From = 0, To = wide - narrow, Reveal = _reveal });
        var label = new SkUiLabel { Text = "Book", FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center };
        label.AddScrollEffect(new SkUiScrollEffect { Property = SkUiScrollEffectProperty.Opacity, From = 1, To = 0, Reveal = _reveal });
        var content = new SkUiGrid { ColumnDefinitions = [new ColumnDefinition(narrow), new ColumnDefinition(GridLength.Star)] };
        content.Children.Add(icon);
        Grid.SetColumn(label, 1);
        content.Children.Add(label);
        var button = new SkUiBorder
        {
            WidthRequest = wide, HeightRequest = narrow, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(16, 0, 16, BottomBar + 12), BackgroundColor = SampleColors.Accent, StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = narrow / 2 },
            Shadow = new Shadow { Brush = Colors.Black, Opacity = 0.3f, Radius = 8, Offset = new Point(0, 3) },
            Content = content
        };
        SemanticProperties.SetDescription(button, "Book this trip");
        button.AddScrollEffects(
            new SkUiScrollEffect { Property = SkUiScrollEffectProperty.ClipWidth, ClipEdge = SkUiClipEdge.End, ClipShape = SkUiClipShape.Capsule, From = wide, To = narrow, Reveal = _reveal },
            new SkUiScrollEffect { Property = SkUiScrollEffectProperty.TranslationY, From = 0, To = BottomBar, Reveal = _reveal });
        button.Tapped += (_, _) => _reveal.Show();
        return button;
    }

    /// <summary>A back-to-top button that appears (fades and grows in) after 900 DIPs of scroll.</summary>
    private SkUiContentView BackToTop()
    {
        var button = new SkUiContentView
        {
            WidthRequest = 44, HeightRequest = 44, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(16, 0, 0, BottomBar + 18),
            Content = new SkUiGrid
            {
                Children =
                {
                    new SkUiEllipse { Fill = Color.FromRgba(31, 41, 51, 0.85) },
                    new SkUiLabel { Text = "↑", FontSize = 20, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
                }
            }
        };
        button.AddScrollEffects(
            new SkUiScrollEffect(SkUiScrollEffectProperty.Opacity, 900, 1100, 0, 1) { Source = _scroll },
            new SkUiScrollEffect(SkUiScrollEffectProperty.Scale, 900, 1100, 0.5, 1) { Source = _scroll },
            new SkUiScrollEffect { Property = SkUiScrollEffectProperty.TranslationY, From = 0, To = BottomBar, Reveal = _reveal });
        SemanticProperties.SetDescription(button, "Back to the top");
        button.Tapped += async (_, _) => await _scroll.ScrollToAsync(0, 0, animated: true);
        return button;
    }

    #endregion

    #region Layout-dependent ranges

    private void AfterLayout()
    {
        // Children are arranged after their stack reports its size: read their places on the next turn.
        if (Microsoft.Maui.Dispatching.Dispatcher.GetForCurrentThread() is { } dispatcher)
            dispatcher.Dispatch(UpdateRanges);
        else
            UpdateRanges();
    }

    /// <summary>Sets the ranges that depend on where things are laid out and on the viewport.</summary>
    private void UpdateRanges()
    {
        var viewport = _scroll.Height;
        if (viewport <= 0 || _content.Height <= 0)
            return;
        // Reading progress over the whole page.
        _progress.InputEnd = Math.Max(1, _content.Height - viewport);

        // The chips: once the content has carried them to the toolbar's bottom, down by as much as it moves on (a range
        // as long as any page; 1:1, so they hold still, in step with the content on the render thread).
        var stick = _chips.Frame.Y - Bar;
        _chipsMove.SetInput(stick, stick + 100_000).SetOutput(0, 100_000);
        _chipsDivider.SetInput(stick - 8, stick);

        // The days: they fade and slide in over the first 140 DIPs of scrolling into view. The range runs backwards (from
        // fully in to just entering) so that From is the state they end in: what reduced motion holds.
        foreach (var (card, fade, slide) in _days)
        {
            var entering = card.Frame.Y - viewport;
            fade.SetInput(entering + 140, entering).SetOutput(1, 0.2);
            slide.SetInput(entering + 140, entering).SetOutput(0, 40);
        }

        // The banner's picture: 140 DIPs of travel inside it while it crosses the viewport.
        if (_banner is not null && _bannerParallax is not null)
            _bannerParallax.SetInput(_banner.Frame.Y - viewport, _banner.Frame.Y + _banner.Height).SetOutput(-140, 0);
    }

    #endregion
}
