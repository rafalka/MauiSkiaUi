using System.Globalization;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// An SVG icon for any image property of the SkUi* controls, shaped like MAUI's <c>FontImageSource</c>:
/// <c>&lt;sk:SkUiVectorImageSource Source="check.svg" Color="{AppThemeBinding Light=Black, Dark=White}" Size="24" /&gt;</c>.
/// Changing a property draws the icon again. MAUI's native <c>Image</c> does not know it.
/// </summary>
[ContentProperty(nameof(Source))]
public class SkUiVectorImageSource : ImageSource
{
    /// <summary>Bindable <see cref="Source"/>.</summary>
    public static readonly BindableProperty SourceProperty = BindableProperty.Create(nameof(Source), typeof(string), typeof(SkUiVectorImageSource),
        propertyChanged: static (source, _, _) => ((SkUiVectorImageSource)source).OnSourceChanged());

    /// <summary>Bindable <see cref="Color"/>.</summary>
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(nameof(Color), typeof(Color), typeof(SkUiVectorImageSource),
        propertyChanged: static (source, _, _) => ((SkUiVectorImageSource)source).OnSourceChanged());

    /// <summary>Bindable <see cref="Size"/>.</summary>
    public static readonly BindableProperty SizeProperty = BindableProperty.Create(nameof(Size), typeof(double), typeof(SkUiVectorImageSource), 0d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: static (source, _, _) => ((SkUiVectorImageSource)source).OnSourceChanged());

    /// <inheritdoc />
    public override bool IsEmpty => string.IsNullOrWhiteSpace(Source);

    /// <summary>The file: a name in the app package, an absolute path or an HTTP(S) URI, as <c>Image.Source</c>'s string.</summary>
    public string? Source
    {
        get => (string?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>The starting value of <c>currentColor</c> (<c>null</c>: black, as SVG says); colors written in the file stay, a <c>color</c> among them.</summary>
    public Color? Color
    {
        get => (Color?)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>The longest edge in DIPs; 0 (default): the file's size.</summary>
    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Sets <see cref="Source"/>.</summary>
    public SkUiVectorImageSource SetSource(string? value)
    {
        Source = value;
        return this;
    }

    /// <summary>Sets <see cref="Color"/>.</summary>
    public SkUiVectorImageSource SetColor(Color? value)
    {
        Color = value;
        return this;
    }

    /// <summary>Sets <see cref="Size"/>.</summary>
    public SkUiVectorImageSource SetSize(double value)
    {
        if (!SkUiValidate.NonNegative(this, value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "The size must be finite and not negative.");
        Size = value;
        return this;
    }
}

/// <summary>An SVG with a color and a size (see <see cref="SkUiImageSource.FromVector(SkUiImageSource, Color?, double)"/>): the bytes come from <see cref="Inner"/> or <see cref="Text"/>.</summary>
internal sealed record SkUiVectorSource : SkUiImageSource
{
    public SkUiVectorSource(SkUiImageSource? inner, string? text, Color? color, double size)
    {
        if (!double.IsFinite(size) || size < 0)
            throw new ArgumentOutOfRangeException(nameof(size), size, "The size must be finite and not negative.");
        Inner = inner;
        Text = text;
        Color = color;
        Size = size;
    }

    public SkUiImageSource? Inner { get; }

    public string? Text { get; }

    public Color? Color { get; }

    /// <summary>The longest edge in DIPs; 0: the file's size.</summary>
    public double Size { get; }

    /// <summary>The value of <c>currentColor</c> to draw with.</summary>
    public SKColor CurrentColor => Color is { } color
        ? new SKColor((byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255), (byte)(color.Alpha * 255))
        : SKColors.Black;

    /// <summary>What sets this source's images apart from others of the same bytes.</summary>
    public string DrawingKey => string.Create(CultureInfo.InvariantCulture, $"vector:{(uint)CurrentColor:X8}:{Size}");

    /// <summary>The identity of the bytes (see <see cref="SkUiImageLoader.SourceKey"/>): every color and size of a file shares it.</summary>
    internal override string? CacheKey => Inner is null ? "svg:" + Text : Inner.CacheKey;

    internal override SkUiImageSource BytesSource => Inner ?? this;

    /// <summary>Whether <paramref name="other"/> draws the same bytes.</summary>
    public bool SameFile(SkUiVectorSource other) => Equals(Inner, other.Inner) && Text == other.Text;

    internal override Task<SkUiDecodedImage> LoadAsync(SkUiImageLoadContext context, CancellationToken token)
    {
        context.Vector = this;
        return Inner is null ? context.DecodeVectorTextAsync(Text!, token) : Inner.LoadAsync(context, token);
    }
}
