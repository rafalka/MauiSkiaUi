using System.Globalization;
using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Controls;

/// <summary>
/// HOWTO: shorten text your own way when it does not fit, instead of the stock "...". A label's
/// <see cref="SkUiLabel.LineBreaker"/> receives the text, the available width and the label's settings
/// (<see cref="SkUiTextLineBreakContext"/>) and returns the lines to draw. The context measures exactly as the label draws
/// (shaping, fallback fonts, character spacing), and <c>context.Break()</c> falls back to the label's
/// <c>LineBreakMode</c>, so a breaker only handles its own case.
/// <para>
/// Drag the slider to change the width of the drawn column: each row reacts differently. The number drops decimals
/// (<see cref="SkUiTextLineBreakers.FirstFit"/>), the title uses its own ellipsis
/// (<see cref="SkUiTextLineBreakers.WithEllipsis"/>), the people row counts who was left out (a custom breaker), and the
/// description keeps two lines (<c>MaxLines</c> with tail truncation, as MAUI's Label).
/// </para>
/// </summary>
public sealed class TextThatFitsSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Text that fits",
        Summary: "A custom `LineBreaker` decides how text gets shorter: fewer decimals, another ellipsis, or a count of " +
            "what was left out. Drag the slider to narrow the column.",
        HowTo:
        [
            "Fewer decimals: `label.LineBreaker = SkUiTextLineBreakers.FirstFit(_ => forms)`, where `forms` lists the text " +
                "from longest to shortest. The first that fits is drawn.",
            "Another ellipsis: `label.LineBreaker = SkUiTextLineBreakers.WithEllipsis(\"…\")` (it keeps the label's " +
                "`LineBreakMode` and `MaxLines`).",
            "Anything else: a lambda `context => lines`. Use `context.Fits(text)` or `context.Measure(text)` to try forms, " +
                "and `context.Break()` to fall back to the label's `LineBreakMode`.",
            "Core labels take the same breakers: `coreLabel.SetLineBreaker(...)`."
        ],
        ThingsToKnow:
        [
            "The breaker runs when the text, a text property or the width changes. If it reads other state, call " +
                "`label.InvalidateTextLayout()` when that state changes.",
            "`context.Text` is the displayed text, after `TextTransform`. `context.Owner` is the label, e.g. to read its " +
                "`BindingContext`.",
            "Each returned string is one line; lines beyond `MaxLines` are dropped.",
            "Labels without a custom breaker keep the cached fast path; a custom breaker runs at each new width."
        ]);

    public TextThatFitsSample() : base(Info) => SampleContent = Build();

    private const double Distance = 384_400.123456; // km, Earth to Moon (mean), more digits than any width shows

    /// <summary>"Alice, Bob +3": as many names as fit, then how many more.</summary>
    private static readonly SkUiTextLineBreaker People = context =>
    {
        var names = context.Text.Split(", ");
        for (var shown = names.Length; shown > 0; shown--)
        {
            var candidate = string.Join(", ", names[..shown]) + (shown < names.Length ? $" +{names.Length - shown}" : "");
            if (context.Fits(candidate))
                return [candidate];
        }
        return context.Break(); // not even one name: the label's LineBreakMode (tail truncation)
    };

    private static View Build()
    {
        var distance = Row(Distance.ToString("N6", CultureInfo.CurrentCulture));
        // Longest form first; from two decimals down, the number is rounded rather than cut.
        distance.LineBreaker = SkUiTextLineBreakers.FirstFit(_ =>
            Enumerable.Range(0, 7).Reverse().Select(decimals => Distance.ToString($"N{decimals}", CultureInfo.CurrentCulture)));

        var title = Row("The Moon is drifting away from Earth by about four centimetres a year");
        title.LineBreaker = SkUiTextLineBreakers.WithEllipsis("…");

        var people = Row("Neil Armstrong, Buzz Aldrin, Michael Collins, Margaret Hamilton");
        people.LineBreaker = People;

        var description = Row("Tides are mostly the Moon's doing: its gravity pulls the oceans into two " +
            "bulges, and Earth turns underneath them twice a day.");
        description.MaxLines = 2;

        var column = new SkUiVerticalStackLayout { Spacing = 10, Padding = new Thickness(12) };
        foreach (var (caption, label) in new[] { ("Distance to the Moon (km)", distance), ("Title", title), ("Crew", people), ("Description", description) })
        {
            column.Children.Add(new SkUiLabel { Text = caption, FontSize = 12, TextColor = SampleColors.Caption });
            column.Children.Add(label);
        }
        var surface = new SkUiContentView { Content = column, BackgroundColor = SampleColors.Surface, WidthRequest = 320, HorizontalOptions = LayoutOptions.Start };

        var width = new Slider { Minimum = 80, Maximum = 360, Value = 320 };
        var widthCaption = new Label { Text = "Width: 320", FontSize = 13, TextColor = SampleColors.Caption };
        width.ValueChanged += (_, args) =>
        {
            surface.WidthRequest = args.NewValue;
            widthCaption.Text = $"Width: {args.NewValue:0}";
        };
        return new VerticalStackLayout { Spacing = 8, Children = { widthCaption, width, surface } };
    }

    /// <summary>A tail-truncating label: what each breaker falls back to.</summary>
    private static SkUiLabel Row(string text) => new()
    {
        Text = text,
        FontSize = 17,
        TextColor = SampleColors.Ink,
        LineBreakMode = LineBreakMode.TailTruncation
    };
}
