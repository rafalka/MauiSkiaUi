namespace MauiSkiaUi;

/// <summary>How SkiaUi controls write values they produce themselves to their own bindable properties.</summary>
internal static class SkUiWriteBack
{
    /// <summary>
    /// Writes a value <paramref name="element"/> produced itself (a drag, a tap, typing, a picker choice, a value it clamped
    /// or derived) to one of its own bindable properties, as MAUI's controls write user changes
    /// (<see cref="Element.SetValueFromRenderer(BindableProperty, object)"/>): two-way bindings get it, and the next value
    /// from a one-way binding, a style or the app replaces it. A plain <c>SetValue</c> (or the CLR setter) outranks a
    /// one-way binding for good, so the control would stop following it after the first user change. Skips the write
    /// when the property already holds <paramref name="value"/> (a value from the property itself stays where it is); the
    /// skip compares before coercion, so a caller whose coerce callback records the request coerces first.
    /// Inside the property's own change callback MAUI queues the write until the callback returns.
    /// </summary>
    internal static void WriteBack(this Element element, BindableProperty property, object? value)
    {
        if (!Equals(element.GetValue(property), value))
            element.SetValueFromRenderer(property, value);
    }
}
