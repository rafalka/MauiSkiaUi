using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// The parts a picker's popup content shares around its list or wheels: the title of window popups, and the footer of buttons:
/// Clear, and OK / Cancel for popups whose choice waits to be accepted (<see cref="SkUiPickerUpdateMode.WhenFinished"/>); or, in a
/// bottom pane, a toolbar on top with the title, a close button (×) and those buttons.
/// </summary>
internal static class SkUiPopupParts
{
    /// <summary>The space between the footer's buttons.</summary>
    private const double ButtonSpacing = 8;

    /// <summary>The least height of a bottom pane's toolbar (iOS's).</summary>
    private const double ToolbarHeight = 44;

    /// <summary>The thickness of the line under a bottom pane's toolbar.</summary>
    private const double ToolbarDividerThickness = 0.5;

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
    /// <paramref name="clear"/>), and Cancel and OK at the end side (<see cref="SkUiPickerPopupChrome.RequiresAccept"/>; ordered by the
    /// look's <see cref="SkUiLook.PopupAcceptButtonFirst"/>, mirrored right to left), in the picker's font, styled by the look's
    /// <see cref="SkUiLook.StylePopupButton"/>; they cancel or accept the request <paramref name="request"/> returns (the content's, once it
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
            var clearButton = Button(chrome.ClearText ?? SkUiStrings.Current.ClearText, style, SkUiPopupButton.Clear);
            clearButton.Clicked += (_, _) => clear();
            footer.Children.Add(clearButton);
        }
        if (chrome.RequiresAccept)
        {
            var cancel = Button(chrome.CancelText, style, SkUiPopupButton.Cancel);
            var accept = Button(chrome.AcceptText, style, SkUiPopupButton.Accept);
            cancel.Clicked += (_, _) => request()?.Cancel();
            accept.Clicked += (_, _) => request()?.Accept();
            var end = new SkUiHorizontalStackLayout { Spacing = ButtonSpacing };
            // The look orders them (Windows: OK first).
            if (SkUiLook.Current.PopupAcceptButtonFirst)
            {
                end.Children.Add(accept);
                end.Children.Add(cancel);
            }
            else
            {
                end.Children.Add(cancel);
                end.Children.Add(accept);
            }
            footer.Children.Add(end);
            Grid.SetColumn(end, 2);
        }
        return footer;
    }

    /// <summary>
    /// The buttons of a popup's content: the toolbar of a bottom pane (<see cref="SkUiPickerPopupChrome.IsToolbar"/>, with
    /// <paramref name="title"/>), the footer when it has buttons, else <c>null</c>.
    /// </summary>
    public static SkUiView? Buttons(in SkUiPickerPopupChrome chrome, in SkUiPickerRowStyle style, string? title, Func<SkUiPopupRequest?> request, Action clear) =>
        chrome.IsToolbar ? Toolbar(chrome, style, title, request, clear)
        : chrome.HasButtons ? Buttons(chrome, style, request, clear)
        : null;

    /// <summary>
    /// A bottom pane's toolbar: Cancel (<see cref="SkUiPickerPopupChrome.RequiresAccept"/>) and Clear
    /// (<see cref="SkUiPickerPopupChrome.ShowsClear"/>) at the start side, the title centered in the pane whatever the sides hold
    /// (moved aside only when it would overlap them), a close button (×, read as the accept text; it accepts what the popup shows)
    /// at the end side, over a hairline; mirrored right to left.
    /// </summary>
    public static SkUiGrid Toolbar(in SkUiPickerPopupChrome chrome, in SkUiPickerRowStyle style, string? title, Func<SkUiPopupRequest?> request, Action clear)
    {
        var padding = SkUiLook.Current.PopupItemPadding;
        var bar = new SkUiGrid
        {
            Padding = new Thickness(padding.Left / 2, 0, padding.Right / 2, 0),
            MinimumHeightRequest = ToolbarHeight
        };
        var row = new SkUiHorizontalAlignStackLayout { Spacing = ButtonSpacing };
        if (chrome.RequiresAccept)
        {
            var cancel = Button(chrome.CancelText, style, SkUiPopupButton.Cancel);
            cancel.HorizontalOptions = LayoutOptions.Start;
            cancel.VerticalOptions = LayoutOptions.Center;
            cancel.Clicked += (_, _) => request()?.Cancel();
            row.Children.Add(cancel);
        }
        if (chrome.ShowsClear)
        {
            var clearButton = Button(chrome.ClearText ?? SkUiStrings.Current.ClearText, style, SkUiPopupButton.Clear);
            clearButton.HorizontalOptions = LayoutOptions.Start;
            clearButton.VerticalOptions = LayoutOptions.Center;
            clearButton.Clicked += (_, _) => clear();
            row.Children.Add(clearButton);
        }
        if (title is not null)
        {
            var label = Title(title, style);
            label.Padding = new Thickness(0);
            label.MaxLines = 1;
            label.HorizontalOptions = LayoutOptions.Center;
            label.VerticalOptions = LayoutOptions.Center;
            row.Children.Add(label);
        }
        var close = new SkUiPopupCloseButton(() => request()?.Accept()) { HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center };
        SemanticProperties.SetDescription(close, chrome.AcceptText);
        row.Children.Add(close);
        bar.Children.Add(row);
        var divider = new SkUiBox
        {
            Color = SkUiColors.Muted.WithAlpha(0.3f),
            HeightRequest = ToolbarDividerThickness,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(-padding.Left / 2, 0, -padding.Right / 2, 0),
            InputTransparent = true
        };
        AutomationProperties.SetIsInAccessibleTree(divider, false);
        bar.Children.Add(divider);
        return bar;
    }

    /// <summary>A footer button with its text in the picker's font, styled by the look.</summary>
    private static SkUiButton Button(string text, in SkUiPickerRowStyle style, SkUiPopupButton kind)
    {
        var button = new SkUiButton { Text = text, FontFamily = style.FontFamily, FontAutoScalingEnabled = style.FontAutoScalingEnabled };
        SkUiLook.Current.StylePopupButton(button, kind);
        return button;
    }

    /// <summary>
    /// The content's root: <paramref name="body"/> alone, or in a column under the title and the app's header, over the app's footer
    /// and the buttons (each when there is one; <paramref name="buttonsOnTop"/>: a toolbar above all); the body takes the room left.
    /// </summary>
    public static ISkUiView Compose(SkUiView? title, SkUiView? header, SkUiView body, SkUiView? footer, SkUiView? buttons, bool buttonsOnTop = false)
    {
        if (title is null && header is null && footer is null && buttons is null)
            return body;
        var grid = new SkUiGrid();
        var row = 0;
        if (buttonsOnTop)
        {
            Add(grid, buttons, ref row, GridLength.Auto);
            buttons = null;
        }
        Add(grid, title, ref row, GridLength.Auto);
        Add(grid, header, ref row, GridLength.Auto);
        Add(grid, body, ref row, GridLength.Star);
        Add(grid, footer, ref row, GridLength.Auto);
        Add(grid, buttons, ref row, GridLength.Auto);
        return grid;
    }

    private static void Add(SkUiGrid grid, SkUiView? view, ref int row, GridLength height)
    {
        if (view is null)
            return;
        grid.RowDefinitions.Add(new RowDefinition(height));
        grid.Children.Add(view);
        Grid.SetRow(view, row++);
    }

    /// <summary>The height <paramref name="parts"/> add at <paramref name="width"/>.</summary>
    public static double ExtraHeight(double width, params SkUiView?[] parts) =>
        parts.Sum(part => part?.Measure(width, double.PositiveInfinity).Height ?? 0);
}

/// <summary>
/// The close button (×) of a bottom pane's toolbar: a touch target as tall as the toolbar, drawn by the look
/// (<see cref="SkUiLook.DrawPopupCloseButton"/>), a button for screen readers (named by the toolbar); a tap runs <paramref name="close"/>.
/// </summary>
internal sealed class SkUiPopupCloseButton(Action close) : SkUiView
{
    /// <summary>The touch target's side (iOS's least).</summary>
    internal const double TargetSize = 44;

    /// <summary>The circle's opacity over the muted color.</summary>
    private const float CircleAlpha = 0.16f;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => new(TargetSize, TargetSize);

    /// <inheritdoc />
    protected override bool HandlesTap => true;

    /// <inheritdoc />
    protected override void OnTapped(SkUiTappedEventArgs args)
    {
        RaiseTapped(args);
        close();
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        info.Role = SkUiSemanticsRole.Button;
    }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) =>
        SkUiLook.Current.DrawPopupCloseButton(canvas, new SkUiClearButtonPaint(new SKRect(0, 0, (float)Width, (float)Height),
            ToSkColor(SkUiColors.DefaultForeground), ToSkColor(SkUiColors.Muted.WithAlpha(CircleAlpha)), IsPressed, IsEnabled));
}
