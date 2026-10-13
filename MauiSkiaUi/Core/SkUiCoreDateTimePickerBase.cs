namespace MauiSkiaUi.Core;

/// <summary>
/// What the Core date and time pickers share (the Core analogue of <see cref="SkUiDateTimePickerBase"/>): the picker's field and
/// popup (<see cref="SkUiCorePickerBase"/>), and wheels for the parts of a date or a time, ordered, named and formatted by the
/// culture or by <see cref="WheelFormat"/>. The same engine as the SkiaUi pickers.
/// </summary>
/// <remarks>Core nodes have no enabled state: the picker always reacts (hide it, or remove it, to stop it).</remarks>
public abstract class SkUiCoreDateTimePickerBase : SkUiCorePickerBase
{
    private protected SkUiCoreDateTimePickerBase(SkUiDateTimeKind kind) => Dates = new SkUiDateTimeModel(Model, kind, value => ChooseValue(value));

    /// <summary>The engine of the field's text and the wheels.</summary>
    private protected SkUiDateTimeModel Dates { get; }

    /// <summary>The popup chose <paramref name="value"/> (<c>null</c>: cleared): the control's value takes it, by its rules.</summary>
    private protected abstract void ChooseValue(DateTime? value);

    /// <summary>Which wheels the popup shows, in which order and how (a .NET date and time format; see <see cref="SkUiDateTimePickerBase.WheelFormat"/>); <c>null</c> (default): the culture's.</summary>
    public string? WheelFormat { get => Dates.WheelFormat; set => SetWheelFormat(value); }

    /// <summary>Whether the wheels of the month, the day, the hour, the minute and the second loop (default <c>true</c>).</summary>
    public bool IsLooping { get => Dates.Loop; set => SetIsLooping(value); }

    /// <summary>Whether the wheels have headers ("Day", "Month", …; <see cref="SkUiStrings"/>' texts). Default <c>false</c>.</summary>
    public bool AreColumnHeadersVisible { get => Dates.ShowsHeaders; set => SetAreColumnHeadersVisible(value); }

    /// <summary>How many rows of each wheel show (default 5; 1–9).</summary>
    public int VisibleRows { get => Dates.VisibleRows; set => SetVisibleRows(value); }

    /// <summary>Sets <see cref="WheelFormat"/>.</summary>
    public SkUiCoreDateTimePickerBase SetWheelFormat(string? value) => Wheels(SetProperty(ref Dates.WheelFormat, value, nameof(WheelFormat)));

    /// <summary>Sets <see cref="IsLooping"/>.</summary>
    public SkUiCoreDateTimePickerBase SetIsLooping(bool value) => Wheels(SetProperty(ref Dates.Loop, value, nameof(IsLooping)));

    /// <summary>Sets <see cref="AreColumnHeadersVisible"/>.</summary>
    public SkUiCoreDateTimePickerBase SetAreColumnHeadersVisible(bool value) => Wheels(SetProperty(ref Dates.ShowsHeaders, value, nameof(AreColumnHeadersVisible)));

    /// <summary>Sets <see cref="VisibleRows"/> (1–9).</summary>
    public SkUiCoreDateTimePickerBase SetVisibleRows(int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, SkUiColumnsModel.MaximumVisibleRows);
        return Wheels(SetProperty(ref Dates.VisibleRows, value, nameof(VisibleRows)));
    }

    /// <summary>A setting of the wheels changed (<paramref name="changed"/>): an open popup closes, the next one has it.</summary>
    private protected SkUiCoreDateTimePickerBase Wheels(bool changed)
    {
        if (changed)
            Dates.WheelsChanged();
        return this;
    }
}
