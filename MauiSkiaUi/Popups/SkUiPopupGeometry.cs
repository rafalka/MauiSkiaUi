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
    bool IsRightToLeft,
    double AnchorGap = 0);

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

    /// <summary>A popup this little over the soft keyboard's top (rounding) counts as above it.</summary>
    private const double KeyboardTolerance = 0.5;

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
            input.IsRightToLeft, input.AnchorGap);
    }

    /// <summary>
    /// An anchored popup at another <paramref name="height"/> while it shows (its content shrank or grew back): the width and side
    /// it opened with (<paramref name="opened"/>), against the anchor (below: its top stays; above: its bottom), never taller than
    /// it opened.
    /// </summary>
    public static Rect Resized(Rect opened, bool opensAbove, double height)
    {
        height = Math.Clamp(height, 0, opened.Height);
        return new Rect(opened.Left, opensAbove ? opened.Bottom - height : opened.Top, opened.Width, height);
    }

    /// <summary>
    /// An anchored popup of <paramref name="height"/> (at most the height it <paramref name="opened"/> with) kept above the soft
    /// keyboard, whose top is at <paramref name="keyboardTop"/> (positive infinity: none), less the margin: where
    /// <see cref="Resized"/> puts it while the keyboard leaves it alone. Over the keyboard, a popup below its anchor gets shorter
    /// while it keeps <paramref name="minimumHeight"/> (a few rows), else flips above its anchor when that room is there; one above
    /// its anchor (which the keyboard covers then), or with too little room on either side, sits on the keyboard, over the anchor.
    /// <paramref name="bottomStays"/>: the popup's bottom edge is the one it resizes against (above the anchor or on the keyboard).
    /// </summary>
    public static Rect AboveKeyboard(in SkUiPopupLayout opened, double height, double keyboardTop, double minimumHeight, out bool bottomStays)
    {
        var bounds = Resized(opened.Bounds, opened.OpensAbove, height);
        bottomStays = opened.OpensAbove;
        if (double.IsPositiveInfinity(keyboardTop))
            return bounds;
        var area = Area(opened.WindowSize, opened.AnchorBounds, opened.PreferredSize, opened.SafeAreaInsets, opened.Margin);
        var limit = Math.Min(area.Bottom, keyboardTop - opened.Margin.Bottom);
        if (bounds.Bottom <= limit + KeyboardTolerance)
            return bounds;
        height = bounds.Height;
        var least = Math.Min(height, minimumHeight);
        if (!opened.OpensAbove)
        {
            var below = limit - bounds.Top;
            if (below >= least)
                return new Rect(bounds.Left, bounds.Top, bounds.Width, below);
            var aboveBottom = opened.AnchorBounds.Top - opened.AnchorGap;
            var above = aboveBottom - area.Top;
            if (aboveBottom <= limit && above >= least)
            {
                bottomStays = true;
                var flipped = Math.Min(height, above);
                return new Rect(bounds.Left, aboveBottom - flipped, bounds.Width, flipped);
            }
        }
        bottomStays = true;
        var onKeyboard = Math.Min(height, Math.Max(0, limit - area.Top));
        return new Rect(bounds.Left, limit - onKeyboard, bounds.Width, onKeyboard);
    }

    /// <summary>
    /// The safe area's insets a bottom pane's content pads itself by: the window's left, right and bottom ones (its top edge is
    /// inside the window).
    /// </summary>
    public static Thickness PaneContentPadding(Thickness safeAreaInsets) => new(safeAreaInsets.Left, 0, safeAreaInsets.Right, safeAreaInsets.Bottom);

    /// <summary>The window; around the anchor when its size is unknown.</summary>
    private static Rect Window(in SkUiPopupPlacementInput input) => Window(input.WindowSize, input.AnchorBounds, input.PreferredSize);

    private static Rect Window(Size windowSize, Rect anchorBounds, Size preferredSize) =>
        windowSize is { Width: > 0, Height: > 0 } size
            ? new Rect(Point.Zero, size)
            : anchorBounds.Union(new Rect(0, 0, anchorBounds.Right + preferredSize.Width, anchorBounds.Bottom + preferredSize.Height));

    /// <summary>The window's safe area less the margin; around the anchor when the window's size is unknown.</summary>
    private static Rect Area(in SkUiPopupPlacementInput input) =>
        Area(input.WindowSize, input.AnchorBounds, input.PreferredSize, input.SafeAreaInsets, input.Margin);

    private static Rect Area(Size windowSize, Rect anchorBounds, Size preferredSize, Thickness safe, Thickness margin)
    {
        var window = Window(windowSize, anchorBounds, preferredSize);
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
