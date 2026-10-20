namespace MauiSkiaUi;

/// <summary>
/// A text field or picker's own chrome over the look's, on either layer (<see cref="SkUiInputView"/>, <see cref="SkUiPickerBase"/>,
/// <see cref="Core.SkUiCoreInputView"/>, <see cref="Core.SkUiCorePickerBase"/>). Each property is <c>null</c> by default: the look's
/// value for the field's style. Focus, error and disabled emphasis stay the look's.
/// </summary>
/// <remarks>
/// The field style is not part of it: text fields and pickers have their own (<see cref="SkUiTextFieldStyle"/>,
/// <see cref="SkUiPickerFieldStyle"/>). Setting a property is the same as setting it on the control: an invalid value is ignored with
/// a warning on the SkUi layer and throws on the Core layer.
/// </remarks>
public interface ISkUiFieldChromeElement
{
    /// <summary>The idle fill; <c>null</c>: the look's.</summary>
    Color? FillColor { get; set; }

    /// <summary>The idle outline or underline color; <c>null</c>: the look's.</summary>
    Color? BorderColor { get; set; }

    /// <summary>The idle outline or underline width in DIPs (0: none while idle); <c>null</c>: the look's.</summary>
    double? BorderWidth { get; set; }

    /// <summary>Per-corner radii in DIPs (a large radius gives a pill); <c>null</c>: the look's.</summary>
    CornerRadius? CornerRadii { get; set; }
}
