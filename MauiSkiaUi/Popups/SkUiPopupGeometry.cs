namespace MauiSkiaUi;

/// <summary>Where a popup goes, worked out once for its request (<see cref="SkUiPopupGeometry.Layout"/>).</summary>
internal readonly record struct SkUiPopupLayout(
    Rect AnchorBounds,
    Rect AnchorBoundsInPlatformAnchor,
    Size WindowSize,
    Thickness SafeAreaInsets,
    Thickness Margin,
    SkUiPopupPlacement Placement,
    LayoutOptions HorizontalOptions,
    LayoutOptions VerticalOptions,
    Size PreferredSize,
    Size MaximumSize,
    Rect Bounds,
    bool OpensAbove,
    double SpaceAbove,
    double SpaceBelow,
    bool IsRightToLeft);

/// <summary>What a popup is placed from: the anchor, the window and the content's wishes.</summary>
/// <param name="AnchorBounds">The anchor in window coordinates.</param>
/// <param name="AnchorBoundsInPlatformAnchor">The anchor relative to its surface.</param>
/// <param name="WindowSize">The window (zero: unknown, the anchor's surround is used).</param>
/// <param name="SafeAreaInsets">The window's safe area insets.</param>
/// <param name="Margin">The space kept from the safe area's edges.</param>
/// <param name="AnchorGap">The gap between an anchored popup and its anchor.</param>
/// <param name="Placement">The placement (resolved: not <see cref="SkUiPopupPlacement.Default"/>).</param>
/// <param name="HorizontalOptions">Horizontal alignment in the window.</param>
/// <param name="VerticalOptions">Vertical alignment in the window.</param>
/// <param name="PreferredSize">The content's measured size.</param>
/// <param name="MaximumHeight">The control's height limit (infinite: none).</param>
/// <param name="AbsoluteBounds">The app's rectangle for <see cref="SkUiPopupPlacement.Absolute"/>.</param>
/// <param name="IsRightToLeft">The anchor lays out right to left.</param>
/// <param name="MinimumWidth">The least width the app wants (a picker's <c>MinPopupWidth</c> / <c>PopupWidth</c>); NaN: none, so an anchored popup is at least the anchor's width.</param>
/// <param name="MaximumWidth">The most width the app allows (a picker's <c>MaxPopupWidth</c> / <c>PopupWidth</c>), within the safe area.</param>
internal readonly record struct SkUiPopupPlacementInput(
    Rect AnchorBounds,
    Rect AnchorBoundsInPlatformAnchor,
    Size WindowSize,
    Thickness SafeAreaInsets,
    Thickness Margin,
    double AnchorGap,
    SkUiPopupPlacement Placement,
    LayoutOptions HorizontalOptions,
    LayoutOptions VerticalOptions,
    Size PreferredSize,
    double MaximumHeight,
    Rect AbsoluteBounds,
    bool IsRightToLeft,
    double MinimumWidth = double.NaN,
    double MaximumWidth = double.PositiveInfinity);

/// <summary>
/// Places popups (pure geometry, window coordinates): anchored below the anchor or flipped above when the content fits there
/// better, start-aligned (the anchor's right edge in right-to-left layouts) and at least the anchor's width; window-aligned by
/// layout options; absolute at the app's rectangle. Every popup stays inside the safe area less the margin, but a bottom pane,
/// which spans the window's width down to its bottom edge (its content pads itself by the safe area).
/// </summary>
internal static class SkUiPopupGeometry
{
    /// <summary>
    /// Below this, anchored popups are not made any smaller to fit beside their anchor: they move into the safe area instead,
    /// over the anchor.
    /// </summary>
    private const double MinimumExtent = 48;

    public static SkUiPopupLayout Layout(in SkUiPopupPlacementInput input)
    {
        var area = Area(input);
        var maximum = new Size(Math.Max(0, Math.Min(area.Width, input.MaximumWidth)), Math.Max(0, Math.Min(area.Height, input.MaximumHeight)));
        var spaceAbove = Math.Max(0, input.AnchorBounds.Top - input.AnchorGap - area.Top);
        var spaceBelow = Math.Max(0, area.Bottom - input.AnchorBounds.Bottom - input.AnchorGap);
        var opensAbove = false;
        Rect bounds;
        switch (input.Placement)
        {
            case SkUiPopupPlacement.Absolute when input.AbsoluteBounds is { Width: > 0, Height: > 0 } absolute:
                bounds = absolute;
                break;
            case SkUiPopupPlacement.Anchored:
                bounds = Anchored(input, area, maximum, spaceAbove, spaceBelow, out opensAbove);
                break;
            case SkUiPopupPlacement.BottomPane:
                bounds = Pane(input, out maximum);
                break;
            default:
                bounds = Aligned(input, area, maximum);
                break;
        }
        var placement = input.Placement == SkUiPopupPlacement.Absolute && input.AbsoluteBounds is not { Width: > 0, Height: > 0 }
            ? SkUiPopupPlacement.Window
            : input.Placement;
        return new SkUiPopupLayout(input.AnchorBounds, input.AnchorBoundsInPlatformAnchor, input.WindowSize, input.SafeAreaInsets, input.Margin,
            placement, input.HorizontalOptions, input.VerticalOptions, input.PreferredSize, maximum, bounds, opensAbove, spaceAbove, spaceBelow,
            input.IsRightToLeft);
    }

    /// <summary>
    /// The safe area's insets a bottom pane's content pads itself by: the window's left, right and bottom ones (its top edge is
    /// inside the window).
    /// </summary>
    public static Thickness PaneContentPadding(Thickness safeAreaInsets) => new(safeAreaInsets.Left, 0, safeAreaInsets.Right, safeAreaInsets.Bottom);

    /// <summary>The window; around the anchor when its size is unknown.</summary>
    private static Rect Window(in SkUiPopupPlacementInput input) =>
        input.WindowSize is { Width: > 0, Height: > 0 } size
            ? new Rect(Point.Zero, size)
            : input.AnchorBounds.Union(new Rect(0, 0, input.AnchorBounds.Right + input.PreferredSize.Width, input.AnchorBounds.Bottom + input.PreferredSize.Height));

    /// <summary>The window's safe area less the margin; around the anchor when the window's size is unknown.</summary>
    private static Rect Area(in SkUiPopupPlacementInput input)
    {
        var window = Window(input);
        var safe = input.SafeAreaInsets;
        var margin = input.Margin;
        var left = window.Left + safe.Left + margin.Left;
        var top = window.Top + safe.Top + margin.Top;
        var right = window.Right - safe.Right - margin.Right;
        var bottom = window.Bottom - safe.Bottom - margin.Bottom;
        return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private static Rect Anchored(in SkUiPopupPlacementInput input, Rect area, Size maximum, double spaceAbove, double spaceBelow, out bool opensAbove)
    {
        var anchor = input.AnchorBounds;
        // As wide as the content wants (wider than the anchor when its items are), at least the anchor's width unless the app set
        // a minimum of its own, at most the app's maximum and the safe area.
        var minimum = double.IsNaN(input.MinimumWidth) ? anchor.Width : input.MinimumWidth;
        var width = Math.Min(Math.Max(minimum, input.PreferredSize.Width), maximum.Width);
        var wanted = Math.Min(input.PreferredSize.Height, maximum.Height);
        // Below when it fits there, else on the side with more room (as a combo box), never smaller than a few rows.
        opensAbove = wanted > spaceBelow && spaceAbove > spaceBelow;
        var room = opensAbove ? spaceAbove : spaceBelow;
        // Never taller than the safe area (less the margin), and kept inside it: with too little room beside the anchor, a
        // popup of the minimum extent covers the anchor rather than leaving the window.
        var height = Math.Min(Math.Max(Math.Min(wanted, room), Math.Min(wanted, MinimumExtent)), area.Height);
        var left = input.IsRightToLeft ? anchor.Right - width : anchor.Left;
        left = Clamp(left, area.Left, area.Right - width);
        var top = opensAbove ? anchor.Top - input.AnchorGap - height : anchor.Bottom + input.AnchorGap;
        top = Clamp(top, area.Top, area.Bottom - height);
        return new Rect(left, top, width, height);
    }

    /// <summary>
    /// A bottom pane: the window's width, at its bottom edge, as tall as the content (at most the control's maximum) and the safe
    /// area's bottom inset it pads itself by, never above the safe area's top less the margin.
    /// </summary>
    private static Rect Pane(in SkUiPopupPlacementInput input, out Size maximum)
    {
        var window = Window(input);
        maximum = new Size(window.Width, Math.Max(0, window.Height - input.SafeAreaInsets.Top - input.Margin.Top));
        var height = Math.Min(Math.Min(input.PreferredSize.Height, input.MaximumHeight) + input.SafeAreaInsets.Bottom, maximum.Height);
        return new Rect(window.Left, window.Bottom - height, window.Width, height);
    }

    private static Rect Aligned(in SkUiPopupPlacementInput input, Rect area, Size maximum)
    {
        var horizontal = input.HorizontalOptions.Alignment;
        var vertical = input.VerticalOptions.Alignment;
        var minimum = double.IsNaN(input.MinimumWidth) ? 0 : input.MinimumWidth;
        var width = horizontal == LayoutAlignment.Fill ? maximum.Width : Math.Min(Math.Max(minimum, input.PreferredSize.Width), maximum.Width);
        var height = vertical == LayoutAlignment.Fill ? maximum.Height : Math.Min(input.PreferredSize.Height, maximum.Height);
        // Start and End are mirrored in right-to-left layouts, as in a MAUI layout.
        if (input.IsRightToLeft && horizontal is LayoutAlignment.Start or LayoutAlignment.End)
            horizontal = horizontal == LayoutAlignment.Start ? LayoutAlignment.End : LayoutAlignment.Start;
        var left = horizontal switch
        {
            LayoutAlignment.Start or LayoutAlignment.Fill => area.Left,
            LayoutAlignment.End => area.Right - width,
            _ => area.Left + (area.Width - width) / 2
        };
        var top = vertical switch
        {
            LayoutAlignment.Start or LayoutAlignment.Fill => area.Top,
            LayoutAlignment.End => area.Bottom - height,
            _ => area.Top + (area.Height - height) / 2
        };
        return new Rect(left, top, width, height);
    }

    private static double Clamp(double value, double minimum, double maximum) => maximum < minimum ? minimum : Math.Clamp(value, minimum, maximum);
}
