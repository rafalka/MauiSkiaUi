using MauiSkiaUi;

namespace MauiSkiaUiSamples;

/// <summary>Renders <see cref="SampleInfo"/> text with drawn labels: <c>`code`</c> in backticks becomes a monospace span.</summary>
public static class SampleText
{
    /// <summary>A wrapping label for <paramref name="text"/>.</summary>
    public static SkUiLabel Paragraph(string text, double fontSize = 14, Color? color = null) => new()
    {
        FormattedText = Format(text, fontSize, color ?? SampleColors.Ink),
        LineBreakMode = LineBreakMode.WordWrap
    };

    /// <summary>Items with a hanging marker: "1." … for steps, "•" for notes.</summary>
    public static SkUiGrid List(IReadOnlyList<string> items, bool numbered)
    {
        var grid = new SkUiGrid { ColumnDefinitions = [new(new GridLength(22)), new(GridLength.Star)], RowSpacing = 6 };
        for (var index = 0; index < items.Count; index++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var marker = new SkUiLabel
            {
                Text = numbered ? $"{index + 1}." : "•",
                FontFamily = SampleFonts.Semibold,
                FontSize = 14,
                TextColor = SampleColors.Accent
            };
            Grid.SetRow(marker, index);
            var paragraph = Paragraph(items[index]);
            Grid.SetRow(paragraph, index);
            Grid.SetColumn(paragraph, 1);
            grid.Children.Add(marker);
            grid.Children.Add(paragraph);
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
