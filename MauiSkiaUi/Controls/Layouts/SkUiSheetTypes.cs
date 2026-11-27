using System.ComponentModel;
using System.Globalization;

namespace MauiSkiaUi;

/// <summary>The edge of the surface an <see cref="SkUiSheet"/> comes from.</summary>
public enum SkUiSheetEdge
{
    /// <summary>The bottom edge (a bottom sheet).</summary>
    Bottom,

    /// <summary>The leading edge: the left one, the right one in right-to-left layouts (a side sheet).</summary>
    Start,

    /// <summary>The trailing edge: the right one, the left one in right-to-left layouts (a side sheet).</summary>
    End
}

/// <summary>How an <see cref="SkUiSheet"/> shows.</summary>
public enum SkUiSheetPresentation
{
    /// <summary>A sheet from its <see cref="SkUiSheet.Edge"/>, resting on its detents, dragged by the user.</summary>
    Sheet,

    /// <summary>A card centered in the surface, at most the look's <see cref="SkUiLook.SheetDialogMaximumWidth"/> wide: no detents, no drag.</summary>
    Dialog,

    /// <summary>
    /// A sheet on compact widths and a dialog (the tablet form sheet) when the surface is at least the look's
    /// <see cref="SkUiLook.SheetDialogBreakpoint"/> wide.
    /// </summary>
    Auto
}

/// <summary>What an <see cref="SkUiSheetDetent"/> measures.</summary>
public enum SkUiSheetDetentKind
{
    /// <summary>A fraction of the tallest a sheet can be (<see cref="SkUiSheetDetent.Fraction"/>).</summary>
    Fraction,

    /// <summary>A height in DIPs (<see cref="SkUiSheetDetent.Height"/>).</summary>
    Height,

    /// <summary>As tall as the sheet's header, content and actions ask for (<see cref="SkUiSheetDetent.FitContent"/>).</summary>
    FitContent
}

/// <summary>
/// A height an <see cref="SkUiSheet"/> rests at: a fraction of the tallest it can be, a height in DIPs, or as tall as its content
/// asks for. Every detent is capped at the tallest a sheet can be (the surface less the window's top inset and the look's
/// <see cref="SkUiLook.SheetTopMargin"/>). In XAML, a list of detents is written as text: <c>"Fit, 50%, 100%, 320"</c> (percentages are
/// fractions, plain numbers DIPs).
/// </summary>
[TypeConverter(typeof(SkUiSheetDetentTypeConverter))]
public readonly record struct SkUiSheetDetent
{
    private SkUiSheetDetent(SkUiSheetDetentKind kind, double value)
    {
        Kind = kind;
        Value = value;
    }

    /// <summary>What the detent measures.</summary>
    public SkUiSheetDetentKind Kind { get; }

    /// <summary>The fraction (0 to 1) or the height in DIPs; 0 for <see cref="SkUiSheetDetentKind.FitContent"/>.</summary>
    public double Value { get; }

    /// <summary>A fraction of the tallest the sheet can be, greater than 0 and at most 1 (0.5: half, 1: full).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fraction"/> is not in (0, 1].</exception>
    public static SkUiSheetDetent Fraction(double fraction)
    {
        if (!(fraction > 0 && fraction <= 1))
            throw new ArgumentOutOfRangeException(nameof(fraction), fraction, "A sheet detent's fraction must be greater than 0 and at most 1.");
        return new SkUiSheetDetent(SkUiSheetDetentKind.Fraction, fraction);
    }

    /// <summary>A height in DIPs, greater than 0: what the sheet shows above the window's bottom inset (which is added below).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dips"/> is not a positive finite number.</exception>
    public static SkUiSheetDetent Height(double dips)
    {
        if (!(dips > 0) || double.IsInfinity(dips))
            throw new ArgumentOutOfRangeException(nameof(dips), dips, "A sheet detent's height must be a positive finite number of DIPs.");
        return new SkUiSheetDetent(SkUiSheetDetentKind.Height, dips);
    }

    /// <summary>As tall as the sheet's grabber, header, content and actions ask for at its width.</summary>
    public static SkUiSheetDetent FitContent => new(SkUiSheetDetentKind.FitContent, 0);

    /// <inheritdoc />
    public override string ToString() => Kind switch
    {
        SkUiSheetDetentKind.Fraction => (Value * 100).ToString("0.###", CultureInfo.InvariantCulture) + "%",
        SkUiSheetDetentKind.Height => Value.ToString("0.###", CultureInfo.InvariantCulture),
        _ => "Fit"
    };

    /// <summary>Reads one detent written as text (<c>"Fit"</c>, <c>"50%"</c>, <c>"320"</c>); <c>false</c> when it is not one.</summary>
    internal static bool TryParse(string text, out SkUiSheetDetent detent)
    {
        detent = default;
        var value = text.Trim();
        if (value.Equals("Fit", StringComparison.OrdinalIgnoreCase) || value.Equals("FitContent", StringComparison.OrdinalIgnoreCase))
        {
            detent = FitContent;
            return true;
        }
        if (value.EndsWith('%'))
        {
            if (!double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) || !(percent > 0 && percent <= 100))
                return false;
            detent = Fraction(percent / 100);
            return true;
        }
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dips) || !(dips > 0) || double.IsInfinity(dips))
            return false;
        detent = Height(dips);
        return true;
    }

    /// <summary>Reads a comma-separated list of detents; throws <see cref="FormatException"/> naming the part that is not one.</summary>
    internal static List<SkUiSheetDetent> ParseList(string text)
    {
        var detents = new List<SkUiSheetDetent>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryParse(part, out var detent))
                throw new FormatException($"'{part}' is not a sheet detent: use Fit, a percentage (50%) or a height in DIPs (320).");
            detents.Add(detent);
        }
        return detents;
    }
}

/// <summary>Converts text (<c>"Fit"</c>, <c>"50%"</c>, <c>"320"</c>) to an <see cref="SkUiSheetDetent"/> in XAML and styles.</summary>
public sealed class SkUiSheetDetentTypeConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string);

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(string);

    /// <inheritdoc />
    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string text && SkUiSheetDetent.TryParse(text, out var detent)
            ? detent
            : throw new FormatException($"'{value}' is not a sheet detent: use Fit, a percentage (50%) or a height in DIPs (320).");

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
        value is SkUiSheetDetent detent && destinationType == typeof(string) ? detent.ToString() : base.ConvertTo(context, culture, value, destinationType);
}

/// <summary>
/// Converts a comma-separated list (<c>"Fit, 50%, 100%"</c>) to the detents of an <see cref="SkUiSheet"/> in XAML and styles. MAUI's
/// bindings do not run type converters: a view model binds a list of <see cref="SkUiSheetDetent"/>s.
/// </summary>
public sealed class SkUiSheetDetentsTypeConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string);

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(string);

    /// <inheritdoc />
    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string text ? SkUiSheetDetent.ParseList(text) : throw new FormatException($"'{value}' is not a list of sheet detents.");

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
        value is IEnumerable<SkUiSheetDetent> detents && destinationType == typeof(string)
            ? string.Join(", ", detents)
            : base.ConvertTo(context, culture, value, destinationType);
}

/// <summary>An <see cref="SkUiSheet"/> closed: by <see cref="SkUiSheet.Close"/> with a result, or dismissed by the user or the app.</summary>
/// <param name="result">What <see cref="SkUiSheet.Close"/> was given; <c>null</c> when dismissed.</param>
/// <param name="isDismissed">Whether the sheet closed without a result (dragged away, the scrim tapped, Back, Escape, <c>IsOpen = false</c>).</param>
public sealed class SkUiSheetClosedEventArgs(object? result, bool isDismissed) : EventArgs
{
    /// <summary>What <see cref="SkUiSheet.Close"/> was given; <c>null</c> when dismissed.</summary>
    public object? Result { get; } = result;

    /// <summary>Whether the sheet closed without a result (dragged away, the scrim tapped, Back, Escape, <c>IsOpen = false</c>).</summary>
    public bool IsDismissed { get; } = isDismissed;
}
