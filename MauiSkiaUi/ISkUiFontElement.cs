namespace MauiSkiaUi;

/// <summary>
/// A drawn control with text in one font, on either layer: labels and buttons, radio buttons, avatars, text fields, pickers and
/// steppers (<c>SkUi*</c> and <c>SkUiCore*</c>). Code that styles text works on any of them through this interface.
/// </summary>
/// <remarks>
/// Setting a property is the same as setting it on the control: an invalid value is ignored with a warning on the SkUi layer (as
/// MAUI does) and throws on the Core layer, as the control's <c>SetX</c> methods do.
/// </remarks>
public interface ISkUiFontElement
{
    /// <summary>The font family (system font, or a name registered via <see cref="SkUiFonts.Register"/>); <c>null</c>: the default font.</summary>
    string? FontFamily { get; set; }

    /// <summary>The font size in DIPs.</summary>
    double FontSize { get; set; }

    /// <summary>Bold and italic flags.</summary>
    FontAttributes FontAttributes { get; set; }

    /// <summary>Whether the text follows the system text size (<see cref="SkUiFontScaling"/>), as MAUI's.</summary>
    bool FontAutoScalingEnabled { get; set; }
}
