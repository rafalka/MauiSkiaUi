using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A look that mimics MAUI's controls on Mac Catalyst in the iPad idiom (MAUI's default, <c>UIDeviceFamily</c> 2): there
/// Catalyst draws the iOS controls and scales the whole window to 77%, so this is <see cref="IosSkUiLook"/>, which the same
/// scaling shrinks alike, with the Mac's focus halo and overlay scroll bars. It draws the same on every platform;
/// <see cref="DefaultSkUiLook.ForCurrentPlatform"/> picks it on Mac Catalyst.
/// </summary>
/// <remarks>
/// Popups open centered as on an iPad (no bottom sheet: Mac Catalyst is not a phone). Apps that switch to the Mac idiom
/// (<c>UIDeviceFamily</c> 6, no scaling, AppKit-like controls) need their own look with AppKit's sizes.
/// </remarks>
public class MacSkUiLook : IosSkUiLook
{
    /// <summary>A shared instance (set it as <see cref="SkUiLook.Current"/>).</summary>
    public new static MacSkUiLook Instance { get; } = new();

    /// <inheritdoc />
    /// <remarks>The window is drawn at 77%, and CoreText tracks text at the size it draws.</remarks>
    public override double FontTrackingSizeScale => 0.77;

    /// <inheritdoc />
    /// <remarks>Measured: from 20 pt up, Catalyst's text is as wide as iOS's.</remarks>
    public override double FontTrackingScaledBelow => 20;

    /// <inheritdoc />
    /// <remarks>Catalyst rounds a system button to 35.</remarks>
    public override double DefaultButtonMinimumHeight => 35;

    /// <inheritdoc />
    /// <remarks>The Mac's focus halo is thicker.</remarks>
    public override float FocusRingThickness => 3;

    /// <inheritdoc />
    public override SkUiFocusRingStyle FocusRingStyle => SkUiFocusRingStyle.Halo;

    /// <inheritdoc />
    /// <remarks>Overlay scrollers that grow when hovered or dragged.</remarks>
    public override double ScrollBarThickness => 7;

    /// <inheritdoc />
    public override double ScrollBarExpandedThickness => 11;

    /// <inheritdoc />
    public override SkUiColorScheme GetColorScheme(bool isDark) => isDark ? MacSkUiColorScheme.Dark : MacSkUiColorScheme.Light;

    // Calendars (1.15): AppKit's graphical date picker. Compact rows, today's number in the accent (as iOS), the selected day a
    // solid accent rounded square with a white number, small chevrons.

    /// <inheritdoc />
    public override SkUiCalendarSelectionShape CalendarSelectionShape => SkUiCalendarSelectionShape.RoundedSquare;

    /// <inheritdoc />
    public override SKColor CalendarSelectionColor(SKColor accent) => accent;

    /// <inheritdoc />
    public override double CalendarRowHeight => 32;

    /// <inheritdoc />
    public override double CalendarDayNamesHeight => 24;

    /// <inheritdoc />
    public override double CalendarHeaderHeight => 36;

    /// <inheritdoc />
    public override double CalendarNavigationButtonSize => 24;

    /// <inheritdoc />
    public override double CalendarFontSize => 13;

    /// <inheritdoc />
    public override double CalendarHeaderFontSize => 13;

    /// <inheritdoc />
    public override double CalendarDayNameFontSize => 11;
}
