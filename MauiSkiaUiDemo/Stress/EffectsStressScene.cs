using MauiSkiaUi;
using MauiSkiaUi.Core;
using MauiShapes = Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUiDemo;

/// <summary>The drawing features the effects stress test switches on, alone or together.</summary>
[Flags]
public enum StressEffects
{
    /// <summary>Plain white rectangular cards with a 1 DIP border.</summary>
    None = 0,
    /// <summary>A linear-gradient card background (instead of white).</summary>
    Gradient = 1,
    /// <summary>Rounded cards (<c>RoundRectangle 12</c>).</summary>
    RoundedCorners = 2,
    /// <summary>A ticket-shaped card (path with notches) with a dashed gradient stroke; replaces rounded corners.</summary>
    ShapedBorder = 4,
    /// <summary>A soft shadow on every card; the opaque card casts it from its outline (cheap path).</summary>
    CardShadow = 8,
    /// <summary>A shadow on every card's title text: cast from the glyphs (content shadow, rasterized once).</summary>
    TextShadow = 16,
    /// <summary>The avatar clipped to a circle (<c>Clip</c> geometry).</summary>
    Clip = 32,
    /// <summary>Cards at 90 % opacity: an opacity layer per card.</summary>
    Translucent = 64,
    /// <summary>
    /// A running activity indicator on every card: animated content that keeps the surface drawing every frame, also
    /// while nothing scrolls (the drawn layers spin it on the render thread).
    /// </summary>
    Spinner = 128,
    /// <summary>Every feature.</summary>
    All = Gradient | RoundedCorners | ShapedBorder | CardShadow | TextShadow | Clip | Translucent | Spinner
}

/// <summary>Which control layer the effects stress test builds.</summary>
public enum EffectsStressLayer
{
    /// <summary>MAUI-compatible SkiaUi views (<see cref="SkUiBorder"/>, <see cref="SkUiLabel"/>, …).</summary>
    SkUi,
    /// <summary>Core nodes under one <see cref="SkUiCoreHost"/>.</summary>
    Core,
    /// <summary>Stock MAUI controls (<see cref="Border"/>, <see cref="Label"/>, <see cref="ScrollView"/>), for reference.</summary>
    NativeMaui
}

/// <summary>
/// The effects stress scene: a scrolling list of cards (avatar, title, subtitle) on one of the three layers, with the
/// selected <see cref="StressEffects"/> applied the same way on each. Brushes and clip geometries are shared by the cards,
/// as app resources would be; shadows are per card (MAUI parents a shadow to its view).
/// </summary>
public static class EffectsStressScene
{
    /// <summary>Card height in DIPs.</summary>
    public const double CardHeight = 72;

    private const double AvatarSize = 40;
    private const double SpinnerSize = 24;
    private const double Spacing = 10;

    // Ticket outline with notches halfway down both sides, scaled into the card (Aspect = Fill).
    private const string Ticket = "M 0,8 A 8,8 0 0 0 8,0 L 192,0 A 8,8 0 0 0 200,8 L 200,26 A 10,10 0 0 0 200,46 L 200,64 " +
        "A 8,8 0 0 0 192,72 L 8,72 A 8,8 0 0 0 0,64 L 0,46 A 10,10 0 0 0 0,26 Z";

    private static readonly Color GradientStart = Color.FromArgb("#E0F2F1");
    private static readonly Color GradientEnd = Color.FromArgb("#E8EAF6");
    private static readonly Color StrokeStart = DemoColors.Accent;
    private static readonly Color StrokeEnd = Color.FromArgb("#6366F1");
    private static readonly Color[] AvatarColors = [DemoColors.SampleA, DemoColors.SampleB, DemoColors.Accent, Color.FromArgb("#B45309")];

    /// <summary>Builds the scene: an <see cref="SkUiScrollView"/> for the drawn layers, a MAUI <see cref="ScrollView"/> for native.</summary>
    public static View Build(EffectsStressLayer layer, int count, StressEffects effects, bool hwAccelerated) => layer switch
    {
        EffectsStressLayer.Core => BuildCore(count, effects, hwAccelerated),
        EffectsStressLayer.NativeMaui => BuildNative(count, effects),
        _ => BuildSkUi(count, effects, hwAccelerated)
    };

    /// <summary>A short name for the effects, for result tables.</summary>
    public static string Describe(StressEffects effects)
    {
        if (effects == StressEffects.None) return "baseline";
        if (effects == StressEffects.All) return "all";
        return string.Join(" + ", Enum.GetValues<StressEffects>()
            .Where(flag => flag is not (StressEffects.None or StressEffects.All) && effects.HasFlag(flag))
            .Select(flag => flag.ToString()));
    }

    // ---- SkUi* ------------------------------------------------------------------------------------------------

    private static SkUiScrollView BuildSkUi(int count, StressEffects effects, bool hwAccelerated)
    {
        var background = CardBrush(effects);
        var stroke = StrokeBrush(effects);
        var clip = effects.HasFlag(StressEffects.Clip) ? AvatarClip() : null;
        var list = new SkUiVerticalStackLayout { Spacing = Spacing, Padding = new Thickness(16, 12, 16, 24) };
        list.StartUpdating();
        try
        {
            for (var index = 0; index < count; index++)
            {
                var title = new SkUiLabel { Text = $"Order {index + 1:0000}", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = DemoColors.Ink };
                if (effects.HasFlag(StressEffects.TextShadow))
                    title.Shadow = TextShadow();
                var avatar = new SkUiBox
                {
                    Color = AvatarColors[index % AvatarColors.Length], WidthRequest = AvatarSize, HeightRequest = AvatarSize,
                    VerticalOptions = LayoutOptions.Center, Clip = clip,
                };
                var row = new SkUiHorizontalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        avatar,
                        new SkUiVerticalStackLayout
                        {
                            VerticalOptions = LayoutOptions.Center,
                            Children = { title, new SkUiLabel { Text = "Shipped · 3 items", FontSize = 13, TextColor = DemoColors.Caption } }
                        }
                    }
                };
                if (effects.HasFlag(StressEffects.Spinner))
                    row.Children.Add(new SkUiActivityIndicator
                    {
                        IsRunning = true, Color = DemoColors.Accent, WidthRequest = SpinnerSize, HeightRequest = SpinnerSize,
                        VerticalOptions = LayoutOptions.Center
                    });
                var card = new SkUiBorder
                {
                    HeightRequest = CardHeight,
                    Padding = new Thickness(14, 0),
                    Background = background,
                    Stroke = stroke,
                    StrokeThickness = effects.HasFlag(StressEffects.ShapedBorder) ? 2 : 1,
                    Content = row
                };
                if (effects.HasFlag(StressEffects.ShapedBorder))
                {
                    card.StrokeShape = new SkUiPath { Aspect = Stretch.Fill }.SetData(Ticket);
                    card.StrokeDashArray = [4, 2];
                }
                else if (effects.HasFlag(StressEffects.RoundedCorners))
                {
                    card.CornerRadius = 12;
                }
                if (effects.HasFlag(StressEffects.CardShadow))
                    card.Shadow = CardShadow();
                if (effects.HasFlag(StressEffects.Translucent))
                    card.Opacity = 0.9;
                list.Children.Add(card);
            }
        }
        finally
        {
            list.EndUpdating();
        }
        return new SkUiScrollView { Background = DemoColors.PageBackground, HwAccelerated = hwAccelerated, Content = list };
    }

    // ---- Core -------------------------------------------------------------------------------------------------

    private static SkUiScrollView BuildCore(int count, StressEffects effects, bool hwAccelerated)
    {
        Paint background = CardBrush(effects);
        Paint stroke = StrokeBrush(effects);
        var clip = effects.HasFlag(StressEffects.Clip) ? new SkUiCoreEllipse() : null;
        var cardShadow = new SkUiCoreShadow(Colors.Black, new Point(0, 4), 12, 0.18f);
        var textShadow = new SkUiCoreShadow(Colors.Black, new Point(1, 2), 3, 0.35f);
        var list = new SkUiCoreVerticalStackLayout().SetSpacing(Spacing).SetPadding(new Thickness(16, 12, 16, 24));
        list.StartUpdating();
        try
        {
            for (var index = 0; index < count; index++)
            {
                var title = new SkUiCoreLabel().SetText($"Order {index + 1:0000}").SetFontSize(16).SetFontAttributes(FontAttributes.Bold).SetTextColor(DemoColors.Ink);
                if (effects.HasFlag(StressEffects.TextShadow))
                    title.SetShadow(textShadow);
                var avatar = new SkUiCoreBox().SetColor(AvatarColors[index % AvatarColors.Length]);
                avatar.SetClip(clip).SetWidth(AvatarSize).SetHeight(AvatarSize).SetVerticalAlignment(LayoutAlignment.Center);
                var text = new SkUiCoreVerticalStackLayout()
                    .Add(title)
                    .Add(new SkUiCoreLabel().SetText("Shipped · 3 items").SetFontSize(13).SetTextColor(DemoColors.Caption));
                text.SetVerticalAlignment(LayoutAlignment.Center);
                var row = new SkUiCoreHorizontalStackLayout().SetSpacing(12).Add(avatar).Add(text);
                if (effects.HasFlag(StressEffects.Spinner))
                    row.Add(new SkUiCoreActivityIndicator().SetIsRunning(true).SetColor(DemoColors.Accent)
                        .SetWidth(SpinnerSize).SetHeight(SpinnerSize).SetVerticalAlignment(LayoutAlignment.Center));
                var card = new SkUiCoreBorder()
                    .SetStroke(stroke)
                    .SetStrokeThickness(effects.HasFlag(StressEffects.ShapedBorder) ? 2 : 1)
                    .SetPadding(new Thickness(14, 0))
                    .SetContent(row);
                if (effects.HasFlag(StressEffects.ShapedBorder))
                    card.SetStrokeShape(new SkUiCorePath().SetData(TicketGeometry()).SetAspect(SkUiCoreStretch.Fill)).SetStrokeDashArray(4, 2);
                else if (effects.HasFlag(StressEffects.RoundedCorners))
                    card.SetCornerRadius(12);
                card.SetBackground(background).SetHeight(CardHeight);
                if (effects.HasFlag(StressEffects.CardShadow))
                    card.SetShadow(cardShadow);
                if (effects.HasFlag(StressEffects.Translucent))
                    card.SetOpacity(0.9);
                list.Add(card);
            }
        }
        finally
        {
            list.EndUpdating();
        }
        return new SkUiScrollView
        {
            Background = DemoColors.PageBackground, HwAccelerated = hwAccelerated, Content = new SkUiCoreHost().SetContent(list)
        };
    }

    // ---- Native MAUI ------------------------------------------------------------------------------------------

    private static ScrollView BuildNative(int count, StressEffects effects)
    {
        var background = CardBrush(effects);
        var stroke = StrokeBrush(effects);
        var clip = effects.HasFlag(StressEffects.Clip) ? AvatarClip() : null;
        var list = new VerticalStackLayout { Spacing = Spacing, Padding = new Thickness(16, 12, 16, 24) };
        for (var index = 0; index < count; index++)
        {
            var title = new Label { Text = $"Order {index + 1:0000}", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = DemoColors.Ink };
            if (effects.HasFlag(StressEffects.TextShadow))
                title.Shadow = TextShadow();
            var card = new Border
            {
                HeightRequest = CardHeight,
                Padding = new Thickness(14, 0),
                Background = background,
                Stroke = stroke,
                StrokeThickness = effects.HasFlag(StressEffects.ShapedBorder) ? 2 : 1,
                StrokeShape = effects.HasFlag(StressEffects.ShapedBorder)
                    ? new MauiShapes.Path { Aspect = Stretch.Fill, Data = (MauiShapes.Geometry?)new MauiShapes.PathGeometryConverter().ConvertFromInvariantString(Ticket) }
                    : effects.HasFlag(StressEffects.RoundedCorners) ? new MauiShapes.RoundRectangle { CornerRadius = 12 } : new MauiShapes.Rectangle(),
                Content = new HorizontalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        new BoxView
                        {
                            Color = AvatarColors[index % AvatarColors.Length], WidthRequest = AvatarSize, HeightRequest = AvatarSize,
                            VerticalOptions = LayoutOptions.Center, Clip = clip,
                        },
                        new VerticalStackLayout
                        {
                            VerticalOptions = LayoutOptions.Center,
                            Children = { title, new Label { Text = "Shipped · 3 items", FontSize = 13, TextColor = DemoColors.Caption } }
                        }
                    }
                }
            };
            if (effects.HasFlag(StressEffects.Spinner))
                ((HorizontalStackLayout)card.Content).Add(new ActivityIndicator
                {
                    IsRunning = true, Color = DemoColors.Accent, WidthRequest = SpinnerSize, HeightRequest = SpinnerSize,
                    VerticalOptions = LayoutOptions.Center
                });
            if (effects.HasFlag(StressEffects.ShapedBorder))
                card.StrokeDashArray = [4, 2];
            if (effects.HasFlag(StressEffects.CardShadow))
                card.Shadow = CardShadow();
            if (effects.HasFlag(StressEffects.Translucent))
                card.Opacity = 0.9;
            list.Add(card);
        }
        return new ScrollView { BackgroundColor = DemoColors.PageBackground, Content = list };
    }

    // ---- Shared resources ---------------------------------------------------------------------------------------

    /// <summary>One brush for every card, as an app resource would be.</summary>
    private static Brush CardBrush(StressEffects effects) => effects.HasFlag(StressEffects.Gradient)
        ? new LinearGradientBrush([new GradientStop(GradientStart, 0), new GradientStop(GradientEnd, 1)], new Point(0, 0), new Point(1, 1))
        : new SolidColorBrush(Colors.White);

    private static Brush StrokeBrush(StressEffects effects) => effects.HasFlag(StressEffects.ShapedBorder)
        ? new LinearGradientBrush([new GradientStop(StrokeStart, 0), new GradientStop(StrokeEnd, 1)], new Point(0, 0), new Point(1, 0))
        : new SolidColorBrush(DemoColors.Border);

    /// <summary>
    /// The ticket as MAUI's geometry builds it (arcs flattened to lines, as on the SkUi* and native layers); Core's own
    /// markup parser keeps true arcs, which would move the dashes.
    /// </summary>
    private static PathF TicketGeometry()
    {
        var path = new PathF();
        ((MauiShapes.Geometry)new MauiShapes.PathGeometryConverter().ConvertFromInvariantString(Ticket)!).AppendPath(path);
        return path;
    }

    private static MauiShapes.EllipseGeometry AvatarClip() =>
        new() { Center = new Point(AvatarSize / 2, AvatarSize / 2), RadiusX = AvatarSize / 2, RadiusY = AvatarSize / 2 };

    // One shadow per view: MAUI makes a view the parent of its shadow.
    private static Shadow CardShadow() => new() { Brush = Colors.Black, Offset = new Point(0, 4), Radius = 12, Opacity = 0.18f };

    private static Shadow TextShadow() => new() { Brush = Colors.Black, Offset = new Point(1, 2), Radius = 3, Opacity = 0.35f };
}
