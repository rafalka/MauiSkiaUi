using System.ComponentModel;
using System.Globalization;

namespace MauiSkiaUi;

/// <summary>
/// XAML conversion of text to a drawn view: an <see cref="SkUiLabel"/> of the text, as MAUI shows a string
/// <c>Header</c> / <c>Footer</c> (<c>Header="Contacts"</c>). Each conversion makes a new label, so set the text on the view
/// itself, not in a style shared by several views.
/// </summary>
public class SkUiTextViewTypeConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string text ? CreateLabel(text) : base.ConvertFrom(context, culture, value);

    /// <summary>The label made for <paramref name="text"/>.</summary>
    protected virtual SkUiLabel CreateLabel(string text) => new() { Text = text };
}

/// <summary>
/// XAML conversion of text to a drawn view centered in its space, as MAUI shows a string <c>EmptyView</c>
/// (<c>EmptyView="No results"</c>); see <see cref="SkUiTextViewTypeConverter"/>.
/// </summary>
public sealed class SkUiCenteredTextViewTypeConverter : SkUiTextViewTypeConverter
{
    /// <inheritdoc />
    protected override SkUiLabel CreateLabel(string text) => new()
    {
        Text = text,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center
    };
}
