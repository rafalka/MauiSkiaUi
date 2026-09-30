namespace MauiSkiaUiSamples;

/// <summary>Renders <see cref="SampleInfo"/> text with native labels: <c>`code`</c> in backticks becomes a monospace span.</summary>
public static class SampleText
{
    /// <summary>A wrapping label for <paramref name="text"/>.</summary>
    public static Label Paragraph(string text, double fontSize = 14, Color? color = null) => new()
    {
        FormattedText = Format(text, fontSize, color ?? SampleColors.Ink),
        LineBreakMode = LineBreakMode.WordWrap
    };

    /// <summary>Items with a hanging marker: "1." … for steps, "•" for notes.</summary>
    public static View List(IReadOnlyList<string> items, bool numbered)
    {
        var grid = new Grid { ColumnDefinitions = [new(new GridLength(22)), new(GridLength.Star)], RowSpacing = 6 };
        for (var index = 0; index < items.Count; index++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.Add(new Label
            {
                Text = numbered ? $"{index + 1}." : "•",
                FontFamily = SampleFonts.Semibold,
                FontSize = 14,
                TextColor = SampleColors.Accent
            }, 0, index);
            grid.Add(Paragraph(items[index]), 1, index);
        }
        return grid;
    }

    /// <summary>Splits <paramref name="text"/> at backticks: odd segments are code.</summary>
    public static FormattedString Format(string text, double fontSize, Color color)
    {
        var formatted = new FormattedString();
        var parts = text.Split('`');
        for (var index = 0; index < parts.Length; index++)
        {
            if (parts[index].Length == 0)
                continue;
            var code = index % 2 == 1;
            formatted.Spans.Add(new Span
            {
                Text = parts[index],
                FontFamily = code ? SampleFonts.Mono : SampleFonts.Regular,
                FontSize = code ? fontSize - 1 : fontSize,
                TextColor = code ? SampleColors.Code : color
            });
        }
        return formatted;
    }
}
