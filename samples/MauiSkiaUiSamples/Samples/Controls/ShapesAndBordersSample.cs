using MauiSkiaUi;
using MauiSkiaUi.Core;
using Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUiSamples.Samples.Controls;

/// <summary>
/// HOWTO: draw MAUI's shapes and shaped borders on a drawn surface. The shapes (<see cref="SkUiEllipse"/>,
/// <see cref="SkUiRectangle"/>, <see cref="SkUiRoundRectangle"/>, <see cref="SkUiLine"/>, <see cref="SkUiPolygon"/>,
/// <see cref="SkUiPolyline"/>, <see cref="SkUiPath"/>) take MAUI's <c>Shape</c> API, so MAUI shape XAML ports by changing
/// the prefix: <c>Fill</c> and <c>Stroke</c> brushes, <c>StrokeThickness</c>, dashes, caps, joins and <c>Aspect</c>.
/// <see cref="SkUiBorder.StrokeShape"/> takes any of them (or MAUI's own, or MAUI's <c>"RoundRectangle 10"</c> markup) and
/// clips its content to the shape.
/// <para>
/// The page shows an event ticket (a border shaped by a path, with a gradient stroke and a dashed tear line), a progress
/// ring made of two stroke-only circles (the progress is a dash as long as the arc), a circular avatar frame, a star
/// rating of polygons and a sparkline polyline; the slider drives the ring. The last row is the same ring on the Core
/// layer.
/// </para>
/// </summary>
public sealed class ShapesAndBordersSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Shapes and borders",
        Summary: "MAUI's shapes with brushes, dashes and stretch, and borders of any shape: a ticket, a progress ring, an " +
            "avatar frame, stars and a sparkline. Drag the slider to fill the ring.",
        HowTo:
        [
            "Draw a shape: `new SkUiEllipse { Fill = Colors.Teal, Stroke = Colors.Black, StrokeThickness = 2 }`, or the " +
                "same XAML as MAUI's `Ellipse` with the `sk:` prefix.",
            "Gradients: set `Fill` or `Stroke` to a `LinearGradientBrush` / `RadialGradientBrush`.",
            "Dashes: `StrokeDashArray = [4, 2]` (in multiples of `StrokeThickness`), `StrokeDashOffset`, and " +
                "`StrokeLineCap = PenLineCap.Round` for rounded dashes and dots (`[0, 2]`).",
            "Any geometry: `new SkUiPath().SetData(\"M 0,0 L 20,10 Z\")` (MAUI's path markup), and `Aspect = Stretch.Uniform` " +
                "to scale it into the view.",
            "Shape a border: `border.StrokeShape = new SkUiRoundRectangle { CornerRadius = 12 }`, an `SkUiEllipse`, an " +
                "`SkUiPath`, or `StrokeShape=\"RoundRectangle 12\"` in XAML. The content is clipped to the shape.",
            "Core: `new SkUiCoreEllipse().SetStroke(Colors.Teal).SetStrokeThickness(6)`, and `SkUiCoreBorder.SetStrokeShape(…)`."
        ],
        ThingsToKnow:
        [
            "A shape without `Fill` and `Stroke` draws nothing; the default `StrokeThickness` is 1, as in MAUI.",
            "Rectangles and ellipses fill the space they get (`Aspect` is `Fill`); lines, paths and polygons keep their " +
                "coordinates (`None`) and size themselves to their far points.",
            "The stroke stays inside the bounds: the geometry is inset by half the stroke.",
            "A border's content sits inside `Padding` plus `StrokeThickness`, as MAUI's Border.",
            "Shared brushes (resources) can be edited while shown, and never keep a shape alive. Taps hit the rectangular bounds."
        ]);

    // A ticket outline: notches halfway down both sides, scaled into the border (Aspect = Fill).
    private const string Ticket = "M 0,8 A 8,8 0 0 0 8,0 L 192,0 A 8,8 0 0 0 200,8 L 200,42 A 10,10 0 0 0 200,62 L 200,92 " +
        "A 8,8 0 0 0 192,100 L 8,100 A 8,8 0 0 0 0,92 L 0,62 A 10,10 0 0 0 0,42 Z";

    public ShapesAndBordersSample() : base(Info) => SampleContent = Build();

    private static View Build()
    {
        var column = new SkUiVerticalStackLayout { Spacing = 20, Padding = new Thickness(16) };
        column.Children.Add(TicketCard());

        // A progress ring: a faint track, and over it an arc that is one dash as long as the progress.
        const double ringSize = 96, ringThickness = 10;
        var progress = new SkUiEllipse
        {
            WidthRequest = ringSize, HeightRequest = ringSize, Stroke = SampleColors.Accent, StrokeThickness = ringThickness,
            StrokeLineCap = PenLineCap.Round, Rotation = 90, // the circle starts at the left; turn the start to the top
        };
        var percent = new SkUiLabel { FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Ink, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        var ring = new SkUiGrid
        {
            WidthRequest = ringSize, HeightRequest = ringSize,
            Children =
            {
                new SkUiEllipse { Stroke = SampleColors.Border, StrokeThickness = ringThickness },
                progress,
                percent,
            }
        };
        void SetProgress(double value)
        {
            progress.StrokeDashArray = RingDashes(value, ringSize, ringThickness);
            progress.IsVisible = value > 0; // a zero-length dash would still draw its round caps
            percent.Text = $"{value:P0}";
        }
        var slider = new SkUiSlider { Minimum = 0, Maximum = 1, Value = 0.65, WidthRequest = 180, VerticalOptions = LayoutOptions.Center };
        slider.ValueChanged += (_, e) => SetProgress(e.NewValue);
        SetProgress(slider.Value);
        column.Children.Add(Row(ring, slider));

        // A circular avatar frame: the border's ellipse clips the initials' background too.
        var avatar = new SkUiBorder
        {
            StrokeShape = new SkUiEllipse(), StrokeThickness = 3, WidthRequest = 72, HeightRequest = 72,
            Stroke = new LinearGradientBrush([new GradientStop(Color.FromArgb("#F59E0B"), 0), new GradientStop(Color.FromArgb("#DB2777"), 1)], new Point(0, 0), new Point(1, 1)),
            Content = new SkUiLabel
            {
                Text = "MH", FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, BackgroundColor = SampleColors.Accent,
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center,
            },
        };
        column.Children.Add(Row(avatar, Stars(3.5)));

        // A sparkline: an open polyline scaled into its box, rounded joins.
        var sparkline = new SkUiPolyline
        {
            Points = [new(0, 30), new(10, 22), new(20, 26), new(30, 12), new(40, 16), new(50, 4), new(60, 10), new(70, 0)],
            Stroke = SampleColors.Accent, StrokeThickness = 3, StrokeLineJoin = PenLineJoin.Round, StrokeLineCap = PenLineCap.Round,
            Aspect = Stretch.Fill, WidthRequest = 220, HeightRequest = 48, HorizontalOptions = LayoutOptions.Start,
        };
        column.Children.Add(sparkline);

        // The same ring on the Core layer.
        var coreRing = new SkUiCoreOverlayLayout()
            .Add(new SkUiCoreEllipse().SetStroke(SampleColors.Border).SetStrokeThickness(8))
            .Add(new SkUiCoreEllipse().SetStroke(SampleColors.Code).SetStrokeThickness(8).SetStrokeLineCap(LineCap.Round)
                .SetStrokeDashArray([.. RingDashes(0.4, 64, 8)]));
        coreRing.SetWidth(64);
        coreRing.SetHeight(64);
        coreRing.SetRotation(90);
        column.Children.Add(Row(new SkUiCoreHost { WidthRequest = 64, HeightRequest = 64 }.SetContent(coreRing),
            new SkUiLabel { Text = "Core ring (40 %)", TextColor = SampleColors.Caption, VerticalOptions = LayoutOptions.Center }));

        return new SkUiContentView { Content = new SkUiScrollView { Content = column }, BackgroundColor = SampleColors.Surface };
    }

    /// <summary>An event ticket: the border is shaped by a path with notches, its content clipped to it.</summary>
    private static SkUiBorder TicketCard()
    {
        var tear = new SkUiLine(0, 0, 1, 0)
        {
            Stroke = SampleColors.Border, StrokeThickness = 2, StrokeDashArray = [0, 3], StrokeLineCap = PenLineCap.Round,
            Aspect = Stretch.Fill, HeightRequest = 2, Margin = new Thickness(0, 4),
        };
        return new SkUiBorder
        {
            StrokeShape = new SkUiPath { Aspect = Stretch.Fill }.SetData(Ticket),
            StrokeThickness = 2,
            Stroke = new LinearGradientBrush([new GradientStop(SampleColors.Accent, 0), new GradientStop(Color.FromArgb("#6366F1"), 1)], new Point(0, 0), new Point(1, 0)),
            BackgroundColor = Color.FromArgb("#F0FDFA"),
            Padding = new Thickness(20, 12),
            HeightRequest = 118,
            Content = new SkUiVerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new SkUiLabel { Text = "MAUI Conf", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Ink },
                    new SkUiLabel { Text = "Hall B · Row 7 · Seat 12", TextColor = SampleColors.Caption },
                    tear,
                    new SkUiLabel { Text = "Admit one", FontSize = 13, TextColor = SampleColors.Accent, FontAttributes = FontAttributes.Bold },
                }
            }
        };
    }

    /// <summary>Five stars; the one at the fraction is half filled with a hard-stop gradient.</summary>
    private static SkUiHorizontalStackLayout Stars(double rating)
    {
        var row = new SkUiHorizontalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
        var gold = Color.FromArgb("#F59E0B");
        for (var index = 0; index < 5; index++)
        {
            var filled = Math.Clamp(rating - index, 0, 1);
            Brush fill = filled >= 1 ? gold : filled <= 0 ? Colors.Transparent
                : new LinearGradientBrush([new GradientStop(gold, (float)filled), new GradientStop(Colors.Transparent, (float)filled)], new Point(0, 0), new Point(1, 0));
            row.Children.Add(new SkUiPolygon
            {
                Points = [new(12, 0), new(15.5, 8), new(24, 8.5), new(17.5, 14), new(19.5, 23), new(12, 18), new(4.5, 23), new(6.5, 14), new(0, 8.5), new(8.5, 8)],
                Fill = fill, Stroke = gold, StrokeThickness = 1.5, StrokeLineJoin = PenLineJoin.Round,
            });
        }
        return row;
    }

    /// <summary>One dash as long as <paramref name="progress"/> of the ring's centerline, then a gap for the rest (in thicknesses).</summary>
    private static DoubleCollection RingDashes(double progress, double size, double thickness)
    {
        var circumference = Math.PI * (size - thickness);
        // A round cap adds half the thickness at each end: shorten the dash by one thickness.
        var dash = Math.Max(0, progress * circumference - thickness) / thickness;
        return [dash, circumference / thickness * 2];
    }

    private static SkUiHorizontalStackLayout Row(params ISkUiView[] views)
    {
        var row = new SkUiHorizontalStackLayout { Spacing = 20 };
        foreach (var view in views)
            row.Children.Add(view);
        return row;
    }
}
