namespace MauiSkiaUi;

/// <summary>A typed value changed (MAUI's <see cref="ValueChangedEventArgs"/> for values that are not <see cref="double"/>).</summary>
/// <typeparam name="T">The value's type.</typeparam>
public sealed class SkUiValueChangedEventArgs<T>(T oldValue, T newValue) : EventArgs
{
    /// <summary>The value before the change.</summary>
    public T OldValue { get; } = oldValue;

    /// <summary>The value after the change.</summary>
    public T NewValue { get; } = newValue;
}
