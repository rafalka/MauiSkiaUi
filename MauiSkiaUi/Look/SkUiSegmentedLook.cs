using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>How a look draws segmented controls (<see cref="DefaultSkUiLook.SegmentedStyle"/>).</summary>
public enum SkUiSegmentedStyle
{
    /// <summary>A filled, rounded track; the selected segment is a pill in the selection color that slides (the default look).</summary>
    Pill,

    /// <summary>A capsule track with a light fill; the selected segment is a raised thumb with a soft shadow that slides (iOS, the Mac).</summary>
    Thumb,

    /// <summary>Outlined segments with dividers; the selected one is filled and shows a check before its text, and the selection fades (Material 3).</summary>
    Outlined,

    /// <summary>An outlined track; the selected segment takes a tint of the selection color, and the selection fades (Material 2).</summary>
    Tinted,

    /// <summary>No track; a short bar in the selection color under the selected segment's text slides (Fluent's selector bar).</summary>
    Bar
}

/// <summary>How a look draws a tab view's strip (<see cref="DefaultSkUiLook.TabStripStyle"/>).</summary>
public enum SkUiTabStripStyle
{
    /// <summary>A plain strip with a hairline along its edge; the underline is as wide as the header.</summary>
    HeaderUnderline,

    /// <summary>A plain strip with a divider; the underline is as wide as the label, with rounded ends, and icons sit above the text (Material 3).</summary>
    LabelUnderline,

    /// <summary>A strip in the accent color with light headers; the underline is as wide as the header (Material 2).</summary>
    Filled,

    /// <summary>A plain strip; a short bar with rounded ends is centered under the label (Fluent).</summary>
    ShortBar
}

/// <summary>How a tab view marks its selected tab.</summary>
public enum SkUiTabIndicatorStyle
{
    /// <summary>A bar along the strip's edge facing the panes, sized by the look (<see cref="SkUiTabStripStyle"/>).</summary>
    Underline,

    /// <summary>A pill behind the selected header.</summary>
    Pill,

    /// <summary>No marker: the selected header's text color marks it.</summary>
    None
}

/// <summary>A look's sizes for segmented controls (<see cref="SkUiLook.SegmentedMetrics"/>).</summary>
/// <param name="Height">The control's height at the default font size, in DIPs.</param>
/// <param name="Inset">The room between the track's edges and the segments, in DIPs.</param>
/// <param name="Spacing">The gap between segments, in DIPs.</param>
/// <param name="SegmentPadding">The room on each side of a segment's content, in DIPs.</param>
/// <param name="CornerRadius">The track's corner radius when the control does not set one; half the height or more makes a capsule.</param>
/// <param name="IconSize">A segment icon's size, in DIPs.</param>
/// <param name="IconSpacing">The gap between a segment's icon and its text, in DIPs.</param>
/// <param name="CheckSize">The selected segment's check before its content, in DIPs (0: no check).</param>
public readonly record struct SkUiSegmentedMetrics(double Height, double Inset, double Spacing, double SegmentPadding, double CornerRadius,
    double IconSize, double IconSpacing, double CheckSize);

/// <summary>A look's colors for segmented controls (<see cref="SkUiLook.GetSegmentedColors"/>), each used when the control sets none.</summary>
/// <param name="Track">The track behind the segments.</param>
/// <param name="Selection">The selection marker (pill, thumb, fill, tint or bar).</param>
/// <param name="Text">An unselected segment's text and icon.</param>
/// <param name="SelectedText">The selected segment's text, icon and check.</param>
/// <param name="Border">Outlines and dividers.</param>
public readonly record struct SkUiSegmentedColors(Color Track, Color Selection, Color Text, Color SelectedText, Color Border);

/// <summary>
/// Everything a look needs to draw a segmented control's chrome (<see cref="SkUiLook.DrawSegmented"/>): the track, the
/// dividers, the selection marker, the selected segment's check, and the pressed, hovered, disabled and focused states. The
/// control draws the segments' texts and icons over it, in <see cref="SkUiLook.GetSegmentTextColor"/>, after
/// <see cref="SkUiLook.GetSegmentLeadingWidth"/>.
/// </summary>
public readonly record struct SkUiSegmentedPaint
{
    /// <summary>The control's rectangle.</summary>
    public SKRect Bounds { get; init; }

    /// <summary>Each segment's rectangle, in order (the first at the right end right to left).</summary>
    public IReadOnlyList<SKRect> Segments { get; init; }

    /// <summary>Each segment's content width (its text and icon, without padding), for a check drawn before the content.</summary>
    public IReadOnlyList<float> ContentWidths { get; init; }

    /// <summary>Whether each segment is enabled; <c>null</c>: all are.</summary>
    public IReadOnlyList<bool>? EnabledSegments { get; init; }

    /// <summary>The selection marker's rectangle now (between two segments while it slides); empty without a selection.</summary>
    public SKRect Selection { get; init; }

    /// <summary>The selected segment, or -1.</summary>
    public int SelectedIndex { get; init; }

    /// <summary>The segment the selection is leaving while it changes, or -1.</summary>
    public int PreviousIndex { get; init; }

    /// <summary>How far the selection change has come, 0–1 (1 once settled).</summary>
    public float Progress { get; init; }

    /// <summary>The track's color.</summary>
    public SKColor Track { get; init; }

    /// <summary>The selection marker's color.</summary>
    public SKColor SelectionColor { get; init; }

    /// <summary>An unselected segment's text color.</summary>
    public SKColor Text { get; init; }

    /// <summary>The selected segment's text color.</summary>
    public SKColor SelectedText { get; init; }

    /// <summary>The outline and dividers' color.</summary>
    public SKColor Border { get; init; }

    /// <summary>The focus ring's color (the color scheme's accent).</summary>
    public SKColor Accent { get; init; }

    /// <summary>The track's corner radius in DIPs.</summary>
    public float CornerRadius { get; init; }

    /// <summary>The segment being pressed, or -1.</summary>
    public int PressedIndex { get; init; }

    /// <summary>How pressed <see cref="PressedIndex"/> is, 0–1.</summary>
    public float Pressed { get; init; }

    /// <summary>The segment a hovering pointer is over, or -1.</summary>
    public int HoveredIndex { get; init; }

    /// <summary>How focused the control is, 0–1, animated by <see cref="SkUiTransitionKind.FieldFocus"/>.</summary>
    public float Focused { get; init; }

    /// <summary>The control is enabled.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>The control lays out right to left.</summary>
    public bool IsRightToLeft { get; init; }

    /// <summary>The number of segments.</summary>
    public int Count => Segments?.Count ?? 0;

    /// <summary>Whether segment <paramref name="index"/> can be selected (the control and the segment are enabled).</summary>
    public bool IsSegmentEnabled(int index) =>
        IsEnabled && (EnabledSegments is not { } enabled || index < 0 || index >= enabled.Count || enabled[index]);
}

/// <summary>A look's sizes for a tab view's strip (<see cref="SkUiLook.TabStripMetrics"/>).</summary>
/// <param name="Height">The strip's height with text headers, in DIPs.</param>
/// <param name="IconHeight">The strip's height when headers have icons and text, in DIPs.</param>
/// <param name="HeaderPadding">The room on each side of a header's content, in DIPs.</param>
/// <param name="IndicatorThickness">The underline's thickness, in DIPs.</param>
/// <param name="IconSize">A header icon's size, in DIPs.</param>
/// <param name="IconSpacing">The gap between a header's icon and its text, in DIPs.</param>
/// <param name="IconAbove">Whether a header's icon sits above its text (else at the start of it).</param>
public readonly record struct SkUiTabStripMetrics(double Height, double IconHeight, double HeaderPadding, double IndicatorThickness,
    double IconSize, double IconSpacing, bool IconAbove);

/// <summary>A look's colors for a tab view's strip (<see cref="SkUiLook.GetTabStripColors"/>), each used when the tab view sets none.</summary>
/// <param name="Background">The strip's background.</param>
/// <param name="Text">An unselected header's text and icon.</param>
/// <param name="SelectedText">The selected header's text and icon.</param>
/// <param name="Indicator">The indicator.</param>
/// <param name="Divider">The line along the strip's edge facing the panes.</param>
public readonly record struct SkUiTabStripColors(Color Background, Color Text, Color SelectedText, Color Indicator, Color Divider);

/// <summary>What a look needs to draw a tab view's strip behind its headers (<see cref="SkUiLook.DrawTabStrip"/>).</summary>
public readonly record struct SkUiTabStripPaint
{
    /// <summary>The strip's rectangle (its visible part when it scrolls).</summary>
    public SKRect Bounds { get; init; }

    /// <summary>The strip's background color.</summary>
    public SKColor Background { get; init; }

    /// <summary>The divider's color.</summary>
    public SKColor Divider { get; init; }

    /// <summary>Whether the strip is below the panes (its divider then runs along its top edge).</summary>
    public bool IsBottom { get; init; }

    /// <summary>The tab view is enabled.</summary>
    public bool IsEnabled { get; init; }
}

/// <summary>
/// What a look needs to draw a tab view's indicator (<see cref="SkUiLook.DrawTabIndicator"/>). The indicator is a node of its
/// own, moved with the panes on the render thread, so it is drawn once in its own bounds and not with the strip.
/// </summary>
public readonly record struct SkUiTabIndicatorPaint
{
    /// <summary>The indicator's rectangle in its own coordinates.</summary>
    public SKRect Bounds { get; init; }

    /// <summary>The indicator's color.</summary>
    public SKColor Color { get; init; }

    /// <summary>The tab view's indicator style (not <see cref="SkUiTabIndicatorStyle.None"/>, which draws nothing).</summary>
    public SkUiTabIndicatorStyle Style { get; init; }
}
