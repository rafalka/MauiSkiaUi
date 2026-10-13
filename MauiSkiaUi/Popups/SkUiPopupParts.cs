namespace MauiSkiaUi;

/// <summary>
/// The parts a picker's popup content shares around its list or wheels: the title of window popups, and the footer of buttons:
/// Clear, and OK / Cancel for popups whose choice waits to be accepted (<see cref="SkUiPickerUpdateMode.WhenFinished"/>).
/// </summary>
internal static class SkUiPopupParts
{
    /// <summary>The space between the footer's buttons.</summary>
    private const double ButtonSpacing = 8;

    /// <summary>The title above the content (a heading for screen readers), in the picker's font, bold.</summary>
    public static SkUiLabel Title(string title, in SkUiPickerRowStyle style)
    {
        var padding = SkUiLook.Current.PopupItemPadding;
        var label = new SkUiLabel
        {
            Text = title,
            FontAttributes = FontAttributes.Bold,
            FontFamily = style.FontFamily,
            FontSize = style.FontSize,
            FontAutoScalingEnabled = style.FontAutoScalingEnabled,
            TextColor = style.TextColor ?? SkUiColors.DefaultForeground,
            Padding = new Thickness(padding.Left, 16, padding.Right, 8),
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        };
        SemanticProperties.SetHeadingLevel(label, SemanticHeadingLevel.Level1);
        return label;
    }

    /// <summary>
    /// The footer: a Clear button at the start side (<see cref="SkUiPickerPopupChrome.ShowsClear"/>, which calls
    /// <paramref name="clear"/>), and Cancel then OK at the end side (<see cref="SkUiPickerPopupChrome.RequiresAccept"/>; mirrored right
    /// to left), in the picker's font; they cancel or accept the request <paramref name="request"/> returns (the content's, once it
    /// has one).
    /// </summary>
    public static SkUiGrid Buttons(in SkUiPickerPopupChrome chrome, in SkUiPickerRowStyle style, Func<SkUiPopupRequest?> request, Action clear)
    {
        var padding = SkUiLook.Current.PopupItemPadding;
        var footer = new SkUiGrid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            Padding = new Thickness(padding.Left, 8, padding.Right, 12)
        };
        if (chrome.ShowsClear)
        {
            var clearButton = TextButton(chrome.ClearText ?? SkUiStrings.Current.ClearText, style);
            clearButton.Clicked += (_, _) => clear();
            footer.Children.Add(clearButton);
        }
        if (chrome.RequiresAccept)
        {
            var cancel = TextButton(chrome.CancelText, style);
            var accept = new SkUiButton { Text = chrome.AcceptText, FontFamily = style.FontFamily, FontAutoScalingEnabled = style.FontAutoScalingEnabled };
            cancel.Clicked += (_, _) => request()?.Cancel();
            accept.Clicked += (_, _) => request()?.Accept();
            var end = new SkUiHorizontalStackLayout { Spacing = ButtonSpacing, Children = { cancel, accept } };
            footer.Children.Add(end);
            Grid.SetColumn(end, 2);
        }
        return footer;
    }

    /// <summary>A button drawn as text in the accent color (Cancel, Clear).</summary>
    private static SkUiButton TextButton(string text, in SkUiPickerRowStyle style) => new()
    {
        Text = text,
        FillColor = Colors.Transparent,
        TextColor = SkUiColors.Accent,
        FontFamily = style.FontFamily,
        FontAutoScalingEnabled = style.FontAutoScalingEnabled
    };

    /// <summary>The content's root: <paramref name="body"/> alone, or with the title above and the footer below it.</summary>
    public static ISkUiView Compose(SkUiLabel? title, SkUiView body, SkUiView? footer)
    {
        if (title is null && footer is null)
            return body;
        var grid = new SkUiGrid();
        var row = 0;
        if (title is not null)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.Children.Add(title);
            Grid.SetRow(title, row++);
        }
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        grid.Children.Add(body);
        Grid.SetRow(body, row++);
        if (footer is not null)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.Children.Add(footer);
            Grid.SetRow(footer, row);
        }
        return grid;
    }

    /// <summary>The height the title and the footer add at <paramref name="width"/>.</summary>
    public static double ExtraHeight(SkUiLabel? title, SkUiView? footer, double width) =>
        (title?.Measure(width, double.PositiveInfinity).Height ?? 0) + (footer?.Measure(width, double.PositiveInfinity).Height ?? 0);
}
