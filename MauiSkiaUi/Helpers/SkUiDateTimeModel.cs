using System.Globalization;

namespace MauiSkiaUi;

/// <summary>
/// The engine the date and time pickers share, on either layer (<see cref="SkUiDatePicker"/>, <see cref="SkUiTimePicker"/>,
/// <see cref="SkUiDateTimePicker"/> and their Core twins): the field's text from the value and <see cref="Format"/>, and the
/// wheel popup (<see cref="SkUiColumnsPopup"/> with <see cref="SkUiDateTimeWheels"/>' rules) on <see cref="SkUiPickerModel"/>'s
/// popup session. The controls hold the properties and their rules (MAUI's coercion) and copy the values here; a time picker's
/// values are times of <see cref="SkUiDateTimeWheels.TimeDate"/>.
/// </summary>
internal sealed class SkUiDateTimeModel
{
    /// <summary>The default for <see cref="VisibleRows"/>.</summary>
    internal const int DefaultVisibleRows = SkUiColumnsModel.DefaultVisibleRows;

    /// <summary>MAUI's earliest date by default.</summary>
    internal static readonly DateTime DefaultMinimumDate = new(1900, 1, 1);

    /// <summary>MAUI's latest date by default.</summary>
    internal static readonly DateTime DefaultMaximumDate = new(2100, 12, 31);

    /// <summary>The latest time of a day a time picker can choose.</summary>
    internal static readonly TimeSpan LastTime = TimeSpan.FromDays(1) - TimeSpan.FromSeconds(1);

    private readonly SkUiPickerModel _model;
    private readonly Action<DateTime?> _choose;

    /// <param name="model">The field and popup engine of the control.</param>
    /// <param name="kind">What the control chooses.</param>
    /// <param name="choose">Sets the control's value from the popup (<c>null</c>: cleared).</param>
    public SkUiDateTimeModel(SkUiPickerModel model, SkUiDateTimeKind kind, Action<DateTime?> choose)
    {
        _model = model;
        _choose = choose;
        Kind = kind;
        Format = DefaultFormat(kind);
        Minimum = kind == SkUiDateTimeKind.Time ? SkUiDateTimeWheels.TimeDate : DefaultMinimumDate;
        Maximum = kind switch
        {
            SkUiDateTimeKind.Time => SkUiDateTimeWheels.TimeDate + LastTime,
            SkUiDateTimeKind.DateAndTime => DefaultMaximumDate + LastTime,
            _ => DefaultMaximumDate
        };
        model.UsesValueText = true;
        model.ClosedFieldSelects = false;
        model.ContentFactory = CreateContent;
    }

    /// <summary>What the control chooses.</summary>
    public SkUiDateTimeKind Kind { get; }

    /// <summary>The value (<c>null</c>: none).</summary>
    public DateTime? Value;

    /// <summary>The earliest value that can be chosen.</summary>
    public DateTime Minimum;

    /// <summary>The latest value that can be chosen.</summary>
    public DateTime Maximum;

    /// <summary>The field's .NET date and time format (MAUI's <c>Format</c>).</summary>
    public string Format;

    /// <summary>Which wheels show, in which order and how (a .NET date and time format); <c>null</c>: the culture's.</summary>
    public string? WheelFormat;

    public int HourInterval = 1;
    public int MinuteInterval = 1;
    public int SecondInterval = 1;

    /// <summary>Days that cannot be chosen (read at each opening).</summary>
    public IEnumerable<DateTime>? BlackoutDates;

    /// <summary>Whether a day can be chosen (the app's rule; <c>null</c>: every day).</summary>
    public Func<DateTime, bool>? DateEnabled;

    /// <summary>What the wheels show while there is no value; <c>null</c>: today, or now.</summary>
    public DateTime? Highlighted;

    public bool Loop = true;
    public bool ShowsHeaders;
    public int VisibleRows = DefaultVisibleRows;

    /// <summary>The field's format by default: MAUI's short date ("d"), short time ("t"), short date and time ("g").</summary>
    internal static string DefaultFormat(SkUiDateTimeKind kind) => kind switch
    {
        SkUiDateTimeKind.Date => "d",
        SkUiDateTimeKind.Time => "t",
        _ => "g"
    };

    /// <summary>The value changed: the field's text, and an open popup's wheels follow.</summary>
    public void ValueChanged()
    {
        _model.ValueText = FieldText();
        _model.SelectionChanged();
    }

    /// <summary>The field's format changed (or the culture): its text again.</summary>
    public void TextChanged()
    {
        _model.ValueText = FieldText();
        _model.TextChanged();
    }

    /// <summary>What the wheels are built from changed: an open popup closes (the next one has the change).</summary>
    public void WheelsChanged() => _model.Close();

    /// <summary>The value in <see cref="Format"/> and the current culture (a format it cannot use: the default one); <c>null</c> without a value.</summary>
    private string? FieldText()
    {
        if (Value is not { } value)
            return null;
        var culture = CultureInfo.CurrentCulture;
        try
        {
            return value.ToString(Format, culture);
        }
        catch (FormatException)
        {
            return value.ToString(DefaultFormat(Kind), culture);
        }
    }

    private ISkUiPickerPopupContent CreateContent(SkUiPickerPopupContext context)
    {
        var wheels = SkUiDateTimeWheels.Create(new SkUiDateTimeSettings(Kind, WheelFormat, Minimum, Maximum, Value, Highlighted, HourInterval, MinuteInterval,
            SecondInterval, BlackoutDates, DateEnabled, Loop, ShowsHeaders), CultureInfo.CurrentCulture, SkUiStrings.Current);
        var columns = wheels.Texts.Length;
        var widths = new double[columns];
        Array.Fill(widths, -1d);
        var parts = wheels.Headers.Select((header, column) => new SkUiColumnsPopup.ColumnParts(header, null, null, wheels.Names[column])).ToArray();
        // Weak: a request kept by a presenter (a popup still shown after its page went) keeps no picker alive.
        var weak = new WeakReference<SkUiDateTimeModel>(this);
        return new SkUiColumnsPopup(wheels.Texts, wheels.Loops, widths, wheels.IndicesOf(Value), Math.Max(1, VisibleRows), context.RowStyle, context.Title,
            context.RightToLeft, context.Chrome,
            indices =>
            {
                if (weak.TryGetTarget(out var model))
                    model._choose(wheels.ValueOf(indices));
            },
            () => weak.TryGetTarget(out var model) ? wheels.IndicesOf(model.Value) : [], parts: parts, extras: context.Extras, rules: wheels,
            positions: wheels.IndicesOf(wheels.Highlighted));
    }
}
