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

    /// <summary>A popup this little past its room's bottom (rounding) counts as inside it.</summary>
    private const double RoomTolerance = 0.5;

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
    /// A popup that <paramref name="opened"/> where <see cref="Layout"/> put it, fitted to <paramref name="available"/>: the part of
    /// the window its presenter can show it in now (window coordinates, the safe area included; empty: the whole window), which
    /// shrinks while the soft keyboard covers the window's bottom. <paramref name="height"/> is the height it wants (an anchored list
    /// that a search filters: its rows' and parts', at most the height it opened with; else the height it opened with). It stays
    /// inside the room's safe area less the margin:
    /// <list type="bullet">
    /// <item>Anchored: where <see cref="Resized"/> puts it while that fits; else, below its anchor, shorter while it keeps
    /// <paramref name="minimumHeight"/> (a few rows), else flipped above its anchor when that room is there; above its anchor (which
    /// the room's bottom then covers), or with too little room on either side, on the room's bottom, over the anchor.
    /// <paramref name="bottomStays"/>: it resizes against its bottom edge (above its anchor or on the room's bottom).</item>
    /// <item>Window: aligned as it opened, at most the room's height.</item>
    /// <item>Bottom pane: at the room's bottom, as wide as the window, at most up to the safe area's top less the margin (its content
    /// pads itself by <see cref="PaneContentPadding(Thickness, Size, Rect)"/>).</item>
    /// <item>Absolute: the app's rectangle.</item>
    /// </list>
    /// </summary>
    public static Rect Fitted(in SkUiPopupLayout opened, double height, Rect available, double minimumHeight, out bool bottomStays)
    {
        bottomStays = false;
        var room = Room(opened, available);
        switch (opened.Placement)
        {
            case SkUiPopupPlacement.Anchored:
                return FittedAnchored(opened, height, room, minimumHeight, out bottomStays);
            case SkUiPopupPlacement.BottomPane:
            {
                var window = Window(opened.WindowSize, opened.AnchorBounds, opened.PreferredSize);
                var bottom = IsKnown(available) ? Math.Min(window.Bottom, available.Bottom) : window.Bottom;
                var content = height - opened.SafeAreaInsets.Bottom + PaneContentPadding(opened.SafeAreaInsets, opened.WindowSize, available).Bottom;
                var paneHeight = Math.Clamp(content, 0, Math.Max(0, bottom - window.Top - opened.SafeAreaInsets.Top - opened.Margin.Top));
                return new Rect(window.Left, bottom - paneHeight, window.Width, paneHeight);
            }
            case SkUiPopupPlacement.Window:
            {
                var bounds = opened.Bounds;
                var aligned = Math.Min(height, room.Height);
                var top = opened.VerticalOptions.Alignment switch
                {
                    LayoutAlignment.End => room.Bottom - aligned,
                    LayoutAlignment.Center => room.Top + (room.Height - aligned) / 2,
                    _ => room.Top
                };
                return new Rect(bounds.Left, top, bounds.Width, aligned);
            }
            default:
                return opened.Bounds;
        }
    }

    private static Rect FittedAnchored(in SkUiPopupLayout opened, double height, Rect room, double minimumHeight, out bool bottomStays)
    {
        var bounds = Resized(opened.Bounds, opened.OpensAbove, height);
        bottomStays = opened.OpensAbove;
        if (bounds.Bottom <= room.Bottom + RoomTolerance)
            return bounds;
        height = bounds.Height;
        var least = Math.Min(height, minimumHeight);
        if (!opened.OpensAbove)
        {
            var below = room.Bottom - bounds.Top;
            if (below >= least)
                return new Rect(bounds.Left, bounds.Top, bounds.Width, below);
            var aboveBottom = opened.AnchorBounds.Top - opened.AnchorGap;
            var above = aboveBottom - room.Top;
            if (aboveBottom <= room.Bottom && above >= least)
            {
                bottomStays = true;
                var flipped = Math.Min(height, above);
                return new Rect(bounds.Left, aboveBottom - flipped, bounds.Width, flipped);
            }
        }
        bottomStays = true;
        var onBottom = Math.Min(height, room.Height);
        return new Rect(bounds.Left, room.Bottom - onBottom, bounds.Width, onBottom);
    }

    /// <summary>The safe area less the margin, inside <paramref name="available"/> (less the margin too) when it is known.</summary>
    private static Rect Room(in SkUiPopupLayout opened, Rect available)
    {
        var area = Area(opened.WindowSize, opened.AnchorBounds, opened.PreferredSize, opened.SafeAreaInsets, opened.Margin);
        if (!IsKnown(available))
            return area;
        var margin = opened.Margin;
        var left = Math.Max(area.Left, available.Left + margin.Left);
        var top = Math.Max(area.Top, available.Top + margin.Top);
        var right = Math.Min(area.Right, available.Right - margin.Right);
        var bottom = Math.Min(area.Bottom, available.Bottom - margin.Bottom);
        return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private static bool IsKnown(Rect available) => available is { Width: > 0, Height: > 0 };

    /// <summary>
    /// The safe area's insets a bottom pane's content pads itself by at the bottom of <paramref name="available"/> (see
    /// <see cref="Fitted"/>) in a window of <paramref name="windowSize"/>: as <see cref="PaneContentPadding(Thickness)"/>, but at the
    /// bottom only the part of the inset the room still holds (none over the soft keyboard, which covers the home indicator).
    /// </summary>
    public static Thickness PaneContentPadding(Thickness safeAreaInsets, Size windowSize, Rect available)
    {
        var padding = PaneContentPadding(safeAreaInsets);
        if (!IsKnown(available) || windowSize is not { Width: > 0, Height: > 0 } || available.Bottom >= windowSize.Height)
            return padding;
        return padding with { Bottom = Math.Max(0, safeAreaInsets.Bottom - (windowSize.Height - available.Bottom)) };
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
