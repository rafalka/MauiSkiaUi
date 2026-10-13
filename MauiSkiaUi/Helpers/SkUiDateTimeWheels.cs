using System.Globalization;

namespace MauiSkiaUi;

/// <summary>What a date or time picker chooses.</summary>
internal enum SkUiDateTimeKind
{
    /// <summary>A day (<see cref="SkUiDatePicker"/>): the time of its values is midnight.</summary>
    Date,

    /// <summary>A time of day (<see cref="SkUiTimePicker"/>): the date of its values is <see cref="SkUiDateTimeWheels.TimeDate"/>.</summary>
    Time,

    /// <summary>A day and a time of it (<see cref="SkUiDateTimePicker"/>).</summary>
    DateAndTime
}

/// <summary>A part of a date or a time that one wheel chooses, the most significant first.</summary>
internal enum SkUiDateTimeUnit
{
    Year,
    Month,
    Day,

    /// <summary>AM or PM, with a 12-hour <see cref="Hour"/>.</summary>
    Half,

    /// <summary>The hour: 0–23, or 0–11 of the <see cref="Half"/> with a 12-hour clock (0 shows as 12).</summary>
    Hour,
    Minute,
    Second
}

/// <summary>What a date or time picker's wheels are built from (one opening's settings).</summary>
internal readonly record struct SkUiDateTimeSettings(
    SkUiDateTimeKind Kind,
    string? WheelFormat,
    DateTime Minimum,
    DateTime Maximum,
    DateTime? Value,
    DateTime? Highlighted,
    int HourInterval = 1,
    int MinuteInterval = 1,
    int SecondInterval = 1,
    IEnumerable<DateTime>? BlackoutDates = null,
    Func<DateTime, bool>? DateEnabled = null,
    bool Loop = true,
    bool ShowsHeaders = false);

/// <summary>
/// The wheels of a date or time picker for one opening, and the rules between them (<see cref="ISkUiColumnsRules"/>): which
/// columns the wheel format asks for and in which order, their items in the culture's texts, which items can be chosen with the
/// others' choice (the days a month has, the range, blackout dates), and how a choice that cannot be made moves to the nearest one
/// that can (the 31st of a month turned February to its last day, a date before the minimum to the minimum). Holds no control:
/// a popup or request that keeps it keeps nothing else alive.
/// </summary>
/// <remarks>
/// <para>
/// A value is built from its parts: the shown columns' items; the parts more significant than the smallest shown one that have
/// no column (a time picker's date, a date and time picker without a year wheel) keep the value's, else the highlighted one's;
/// the parts below the smallest shown one are their least (a month and year picker chooses the 1st, a picker without seconds
/// a whole minute). The values that can be built this way are the wheels' grid; the range is narrowed to the grid
/// (<see cref="Minimum"/>, <see cref="Maximum"/>).
/// </para>
/// <para>
/// The wheels use the Gregorian calendar (the field's text follows the culture's calendar).
/// </para>
/// </remarks>
internal sealed class SkUiDateTimeWheels : ISkUiColumnsRules
{
    /// <summary>The date of a time picker's values (its wheels never show it).</summary>
    internal static readonly DateTime TimeDate = new(2000, 1, 1);

    /// <summary>How far (in days) a choice on a date that cannot be chosen looks for one that can.</summary>
    private const int NearestDateSearch = 3660;

    private const int UnitCount = 7;

    /// <summary>A column: the unit it chooses, its values (ascending) and their texts, whether it loops, its header.</summary>
    internal sealed record Column(SkUiDateTimeUnit Unit, int[] Values, string[] Texts, bool Loop, string? Header);

    /// <summary>A unit of the format: which, the letter (h or H) and how many times it is repeated.</summary>
    private readonly record struct Token(SkUiDateTimeUnit Unit, char Letter, int Length);

    private readonly Column[] _columns;
    private readonly int[] _columnOf = new int[UnitCount];
    private readonly SkUiDateTimeUnit[] _order;
    private readonly int[] _fixed = new int[UnitCount];
    private readonly bool[] _kept = new bool[UnitCount];
    private readonly bool _twelveHour;
    private readonly HashSet<DateTime>? _blackout;
    private readonly Func<DateTime, bool>? _dateEnabled;
    private readonly bool _datesRuled;
    private readonly int[] _low = new int[UnitCount];
    private readonly int[] _high = new int[UnitCount];

    private SkUiDateTimeWheels(SkUiDateTimeSettings settings, CultureInfo culture, SkUiStrings strings)
    {
        Kind = settings.Kind;
        Value = settings.Value;
        HourInterval = settings.HourInterval;
        MinuteInterval = settings.MinuteInterval;
        SecondInterval = settings.SecondInterval;
        var info = culture.DateTimeFormat;
        var tokens = Tokens(settings.WheelFormat, settings.Kind, info);
        _twelveHour = tokens.Any(token => token is { Unit: SkUiDateTimeUnit.Hour, Letter: 'h' });
        _order = _twelveHour
            ? [SkUiDateTimeUnit.Year, SkUiDateTimeUnit.Month, SkUiDateTimeUnit.Day, SkUiDateTimeUnit.Half, SkUiDateTimeUnit.Hour, SkUiDateTimeUnit.Minute, SkUiDateTimeUnit.Second]
            : [SkUiDateTimeUnit.Year, SkUiDateTimeUnit.Month, SkUiDateTimeUnit.Day, SkUiDateTimeUnit.Hour, SkUiDateTimeUnit.Minute, SkUiDateTimeUnit.Second];
        _columns = [.. tokens.Select(token => CreateColumn(token, settings, culture, strings))];
        Array.Fill(_columnOf, -1);
        for (var column = 0; column < _columns.Length; column++)
            _columnOf[(int)_columns[column].Unit] = column;

        // Parts without a column: above the smallest shown one they keep the value's (else the highlighted one's), below it their least.
        var highlighted = settings.Highlighted ?? DefaultHighlight(settings.Kind);
        var basis = Decompose(settings.Value ?? highlighted);
        var smallest = _order.Where(unit => _columnOf[(int)unit] >= 0).Select(unit => Array.IndexOf(_order, unit)).DefaultIfEmpty(-1).Max();
        for (var level = 0; level < _order.Length; level++)
        {
            var unit = (int)_order[level];
            if (_columnOf[unit] >= 0)
                continue;
            _kept[unit] = level < smallest;
            _fixed[unit] = level < smallest ? basis[unit] : FloorOf(_order[level]);
        }

        _datesRuled = settings.Kind != SkUiDateTimeKind.Time && _columnOf[(int)SkUiDateTimeUnit.Day] >= 0;
        if (settings.BlackoutDates is { } blackout)
            _blackout = [.. blackout.Select(date => date.Date)];
        _dateEnabled = settings.DateEnabled;

        // The range on the grid; a range with no value of the grid in it stays as it was (the wheels then show what is near it).
        var minimum = Search(settings.Minimum, up: true);
        var maximum = Search(settings.Maximum, up: false);
        (Minimum, Maximum) = minimum is { } low && maximum is { } high && low <= high ? (low, high) : (settings.Minimum, settings.Maximum);
        Highlighted = Coerce(ValueOf(IndicesOf(highlighted)) ?? Minimum);
        Texts = [.. _columns.Select(column => column.Texts)];
        Loops = [.. _columns.Select(column => column.Loop)];
        Headers = [.. _columns.Select(column => column.Header)];
    }

    /// <summary>The wheels for <paramref name="settings"/>, with the texts of <paramref name="culture"/> and the headers of <paramref name="strings"/>.</summary>
    public static SkUiDateTimeWheels Create(in SkUiDateTimeSettings settings, CultureInfo culture, SkUiStrings strings) => new(settings, culture, strings);

    /// <summary>What the picker chooses.</summary>
    public SkUiDateTimeKind Kind { get; }

    /// <summary>The value the popup opened with.</summary>
    public DateTime? Value { get; }

    /// <summary>The least value the wheels can choose (the range's start on the grid).</summary>
    public DateTime Minimum { get; }

    /// <summary>The most value the wheels can choose.</summary>
    public DateTime Maximum { get; }

    /// <summary>What the wheels show while nothing is chosen (on the grid, in the range).</summary>
    public DateTime Highlighted { get; }

    /// <summary>The columns, in the format's order.</summary>
    internal IReadOnlyList<Column> Columns => _columns;

    /// <summary>The texts of each column's items.</summary>
    public string[][] Texts { get; }

    /// <summary>Whether each column loops.</summary>
    public bool[] Loops { get; }

    /// <summary>Each column's header (<c>null</c>: none).</summary>
    public string?[] Headers { get; }

    /// <summary>Whether the hours are shown 0–23 (else 1–12 with AM / PM).</summary>
    public bool Is24Hour => !_twelveHour;

    /// <summary>Whether a wheel chooses the seconds.</summary>
    public bool IncludesSeconds => _columnOf[(int)SkUiDateTimeUnit.Second] >= 0;

    /// <summary>The steps of the hour, minute and second wheels.</summary>
    public int HourInterval { get; }

    /// <inheritdoc cref="HourInterval"/>
    public int MinuteInterval { get; }

    /// <inheritdoc cref="HourInterval"/>
    public int SecondInterval { get; }

    #region Values and indices

    /// <summary>The value the wheels' items at <paramref name="indices"/> make; <c>null</c> when a column has nothing chosen.</summary>
    public DateTime? ValueOf(IReadOnlyList<int> indices)
    {
        if (indices.Count != _columns.Length || indices.Any(index => index < 0) || _columns.Length == 0)
            return null;
        return Compose(PartsOf(indices));
    }

    /// <summary>
    /// The items that show <paramref name="value"/> (each column's last item at or before its part: a time between the minute
    /// wheel's steps shows the step before it); all -1 for <c>null</c>.
    /// </summary>
    public int[] IndicesOf(DateTime? value)
    {
        var indices = new int[_columns.Length];
        if (value is not { } date)
        {
            Array.Fill(indices, -1);
            return indices;
        }
        var parts = Decompose(date);
        for (var column = 0; column < indices.Length; column++)
        {
            var values = _columns[column].Values;
            var part = parts[(int)_columns[column].Unit];
            var index = 0;
            for (var item = 0; item < values.Length && values[item] <= part; item++)
                index = item;
            indices[column] = index;
        }
        return indices;
    }

    /// <summary>
    /// The items that choose <paramref name="value"/> (on the grid, rounded down), or <c>null</c> when it cannot be chosen: outside
    /// the range, a date that cannot (a blackout date), or one whose parts without a wheel above the smallest wheel differ from those
    /// the wheels keep (another year for a picker without a year wheel).
    /// </summary>
    public int[]? IndicesOfChoice(DateTime value)
    {
        var indices = IndicesOf(value);
        if (ValueOf(indices) is not { } date || date < Minimum || date > Maximum || (_datesRuled && !IsDateAllowed(date.Date)))
            return null;
        var wanted = Decompose(value);
        var chosen = Decompose(date);
        for (var unit = 0; unit < UnitCount; unit++)
            if (_kept[unit] && wanted[unit] != chosen[unit])
                return null;
        return indices;
    }

    /// <summary>Whether the day <paramref name="date"/> can be chosen: in the range, not a blackout date, allowed by the app.</summary>
    public bool IsDateEnabled(DateTime date) =>
        date.Date >= Minimum.Date && date.Date <= Maximum.Date && (Kind == SkUiDateTimeKind.Time || IsDateAllowed(date.Date));

    /// <summary><paramref name="value"/> in the range, on a date that can be chosen (the nearest one, earlier first).</summary>
    public DateTime Coerce(DateTime value)
    {
        value = value < Minimum ? Minimum : value > Maximum ? Maximum : value;
        if (!_datesRuled || IsDateAllowed(value.Date))
            return value;
        for (var offset = 1; offset <= NearestDateSearch; offset++)
        {
            foreach (var sign in (ReadOnlySpan<int>)[-1, 1])
            {
                var days = sign * offset;
                if ((days < 0 && value - DateTime.MinValue < TimeSpan.FromDays(-days)) || (days > 0 && DateTime.MaxValue - value < TimeSpan.FromDays(days)))
                    continue;
                var candidate = value.AddDays(days);
                if (candidate >= Minimum && candidate <= Maximum && IsDateAllowed(candidate.Date))
                    return candidate;
            }
        }
        return value;
    }

    private bool IsDateAllowed(DateTime date) => (_blackout is null || !_blackout.Contains(date)) && (_dateEnabled?.Invoke(date) ?? true);

    #endregion

    #region ISkUiColumnsRules

    /// <inheritdoc />
    public int[] Coerce(IReadOnlyList<int> selection)
    {
        if (ValueOf(selection) is not { } value)
            return [.. selection];
        return IndicesOf(Coerce(value));
    }

    /// <inheritdoc />
    public IReadOnlyList<bool>? ItemStates(int column, IReadOnlyList<int> selection)
    {
        if (column < 0 || column >= _columns.Length || selection.Count != _columns.Length || selection.Any(index => index < 0))
            return null;
        var unit = _columns[column].Unit;
        var values = _columns[column].Values;
        var parts = PartsOf(selection);
        var states = new bool[values.Length];
        var all = true;
        for (var item = 0; item < values.Length; item++)
        {
            parts[(int)unit] = values[item];
            var enabled = unit == SkUiDateTimeUnit.Year
                // The years' ranges follow each other: the years of the range's ends and those between them.
                ? values[item] >= Minimum.Year && values[item] <= Maximum.Year
                : Overlaps(parts, unit);
            if (enabled && unit == SkUiDateTimeUnit.Day)
            {
                var year = parts[(int)SkUiDateTimeUnit.Year];
                var month = parts[(int)SkUiDateTimeUnit.Month];
                enabled = values[item] <= DateTime.DaysInMonth(year, month) && (!_datesRuled || IsDateAllowed(new DateTime(year, month, values[item])));
            }
            states[item] = enabled;
            all &= enabled;
        }
        return all ? null : states;
    }

    /// <inheritdoc />
    public SkUiColumnsPopupRequest CreateRequest(View anchor, View? platformAnchor, View content, in SkUiPopupLayout layout, in SkUiPopupRequestOptions options,
        IReadOnlyList<int> selected, Action<IReadOnlyList<int>> apply)
    {
        var chrome = options.Chrome;
        return Kind == SkUiDateTimeKind.Time
            ? new SkUiTimePopupRequest(anchor, platformAnchor, content, layout, this, selected, apply)
            {
                Title = options.Title,
                OverlayColor = options.OverlayColor,
                IsAnimated = options.IsAnimated,
                RequiresAccept = chrome.RequiresAccept,
                AcceptText = chrome.AcceptText,
                CancelText = chrome.CancelText
            }
            : new SkUiDatePopupRequest(anchor, platformAnchor, content, layout, this, selected, apply)
            {
                Title = options.Title,
                OverlayColor = options.OverlayColor,
                IsAnimated = options.IsAnimated,
                RequiresAccept = chrome.RequiresAccept,
                AcceptText = chrome.AcceptText,
                CancelText = chrome.CancelText
            };
    }

    /// <summary>
    /// Whether some value of the grid with <paramref name="parts"/> down to <paramref name="unit"/> (and any parts below it) is
    /// in the range.
    /// </summary>
    private bool Overlaps(int[] parts, SkUiDateTimeUnit unit)
    {
        parts.CopyTo(_low, 0);
        parts.CopyTo(_high, 0);
        for (var level = Array.IndexOf(_order, unit) + 1; level < _order.Length; level++)
        {
            var below = (int)_order[level];
            _low[below] = Lowest(_order[level]);
            _high[below] = Highest(_order[level], _high);
        }
        return Compose(_low) <= Maximum && Compose(_high) >= Minimum;
    }

    private int Lowest(SkUiDateTimeUnit unit) => _columnOf[(int)unit] is var column and >= 0 ? _columns[column].Values[0] : _fixed[(int)unit];

    private int Highest(SkUiDateTimeUnit unit, int[] parts)
    {
        if (_columnOf[(int)unit] is not (var column and >= 0))
            return _fixed[(int)unit];
        var last = _columns[column].Values[^1];
        return unit == SkUiDateTimeUnit.Day ? Math.Min(last, DaysIn(parts)) : last;
    }

    #endregion

    #region Parts

    /// <summary>The parts of the value at <paramref name="indices"/>: the columns' items, the others fixed or least.</summary>
    private int[] PartsOf(IReadOnlyList<int> indices)
    {
        var parts = (int[])_fixed.Clone();
        for (var column = 0; column < _columns.Length; column++)
            parts[(int)_columns[column].Unit] = _columns[column].Values[Math.Clamp(indices[column], 0, _columns[column].Values.Length - 1)];
        return parts;
    }

    private int[] Decompose(DateTime value)
    {
        var parts = new int[UnitCount];
        parts[(int)SkUiDateTimeUnit.Year] = value.Year;
        parts[(int)SkUiDateTimeUnit.Month] = value.Month;
        parts[(int)SkUiDateTimeUnit.Day] = value.Day;
        parts[(int)SkUiDateTimeUnit.Half] = value.Hour / 12;
        parts[(int)SkUiDateTimeUnit.Hour] = _twelveHour ? value.Hour % 12 : value.Hour;
        parts[(int)SkUiDateTimeUnit.Minute] = value.Minute;
        parts[(int)SkUiDateTimeUnit.Second] = value.Second;
        return parts;
    }

    /// <summary>The value of <paramref name="parts"/>; a day the month does not have is its last.</summary>
    private DateTime Compose(int[] parts)
    {
        var year = Math.Clamp(parts[(int)SkUiDateTimeUnit.Year], 1, 9999);
        var month = Math.Clamp(parts[(int)SkUiDateTimeUnit.Month], 1, 12);
        var day = Math.Clamp(parts[(int)SkUiDateTimeUnit.Day], 1, DateTime.DaysInMonth(year, month));
        var hour = _twelveHour ? parts[(int)SkUiDateTimeUnit.Half] * 12 + parts[(int)SkUiDateTimeUnit.Hour] : parts[(int)SkUiDateTimeUnit.Hour];
        return new DateTime(year, month, day, Math.Clamp(hour, 0, 23), Math.Clamp(parts[(int)SkUiDateTimeUnit.Minute], 0, 59),
            Math.Clamp(parts[(int)SkUiDateTimeUnit.Second], 0, 59));
    }

    private static int DaysIn(int[] parts) =>
        DateTime.DaysInMonth(Math.Clamp(parts[(int)SkUiDateTimeUnit.Year], 1, 9999), Math.Clamp(parts[(int)SkUiDateTimeUnit.Month], 1, 12));

    private static int FloorOf(SkUiDateTimeUnit unit) => unit is SkUiDateTimeUnit.Year or SkUiDateTimeUnit.Month or SkUiDateTimeUnit.Day ? 1 : 0;

    /// <summary>
    /// The first value of the grid at or after <paramref name="target"/> (<paramref name="up"/>), or the last at or before it;
    /// <c>null</c> when there is none (beyond the year wheel).
    /// </summary>
    private DateTime? Search(DateTime target, bool up)
    {
        var goal = Decompose(target);
        var parts = (int[])_fixed.Clone();
        return Next(0, tight: true);

        DateTime? Next(int level, bool tight)
        {
            if (level == _order.Length)
                return Compose(parts);
            var unit = _order[level];
            var candidates = Candidates(unit, parts);
            var limit = goal[(int)unit];
            for (var step = 0; step < candidates.Length; step++)
            {
                var value = candidates[up ? step : candidates.Length - 1 - step];
                if (tight && (up ? value < limit : value > limit))
                    continue;
                parts[(int)unit] = value;
                if (Next(level + 1, tight && value == limit) is { } found)
                    return found;
            }
            return null;
        }
    }

    /// <summary>The values <paramref name="unit"/> can take after the more significant <paramref name="parts"/> (ascending).</summary>
    private int[] Candidates(SkUiDateTimeUnit unit, int[] parts)
    {
        if (_columnOf[(int)unit] is not (var column and >= 0))
            return unit == SkUiDateTimeUnit.Day ? [Math.Min(_fixed[(int)unit], DaysIn(parts))] : [_fixed[(int)unit]];
        var values = _columns[column].Values;
        if (unit != SkUiDateTimeUnit.Day)
            return values;
        var days = DaysIn(parts);
        return values[^1] <= days ? values : [.. values.Where(day => day <= days)];
    }

    #endregion

    #region Columns

    private static DateTime DefaultHighlight(SkUiDateTimeKind kind) => kind switch
    {
        SkUiDateTimeKind.Date => DateTime.Today,
        SkUiDateTimeKind.Time => TimeDate + DateTime.Now.TimeOfDay,
        _ => DateTime.Now
    };

    /// <summary>The format's units the kind of picker has a wheel for, in its order; the culture's default when it names none.</summary>
    private static List<Token> Tokens(string? format, SkUiDateTimeKind kind, DateTimeFormatInfo info)
    {
        var tokens = string.IsNullOrEmpty(format) ? [] : Filter(Parse(Expand(format, info)), kind);
        if (tokens.Count == 0)
            tokens = Filter(DefaultTokens(kind, info), kind);
        return tokens;
    }

    /// <summary>
    /// The culture's units: its short date pattern's order with month names (abbreviated next to a time) and four-digit years,
    /// and its short time pattern.
    /// </summary>
    private static List<Token> DefaultTokens(SkUiDateTimeKind kind, DateTimeFormatInfo info)
    {
        var tokens = new List<Token>();
        if (kind != SkUiDateTimeKind.Time)
            tokens.AddRange(Parse(info.ShortDatePattern).Select(token => token.Unit switch
            {
                SkUiDateTimeUnit.Month => token with { Length = kind == SkUiDateTimeKind.Date ? 4 : 3 },
                SkUiDateTimeUnit.Year => token with { Length = 4 },
                _ => token
            }));
        if (kind != SkUiDateTimeKind.Date)
            tokens.AddRange(Parse(info.ShortTimePattern));
        return tokens;
    }

    /// <summary>A standard format (one letter: "d", "t", "g", …) as the culture's pattern; a custom format as it is.</summary>
    private static string Expand(string format, DateTimeFormatInfo info)
    {
        if (format.Length != 1)
            return format;
        try
        {
            return info.GetAllDateTimePatterns(format[0]) is [var pattern, ..] ? pattern : format;
        }
        catch (ArgumentException)
        {
            return format;
        }
    }

    /// <summary>The units of a custom date and time format, in order (quoted and escaped text skipped; a weekday is no unit).</summary>
    private static List<Token> Parse(string pattern)
    {
        var tokens = new List<Token>();
        for (var index = 0; index < pattern.Length;)
        {
            var letter = pattern[index];
            if (letter is '\'' or '"')
            {
                var close = pattern.IndexOf(letter, index + 1);
                index = close < 0 ? pattern.Length : close + 1;
                continue;
            }
            if (letter == '\\')
            {
                index += 2;
                continue;
            }
            var length = 1;
            while (index + length < pattern.Length && pattern[index + length] == letter)
                length++;
            SkUiDateTimeUnit? unit = letter switch
            {
                'y' => SkUiDateTimeUnit.Year,
                'M' => SkUiDateTimeUnit.Month,
                'd' when length <= 2 => SkUiDateTimeUnit.Day,
                't' => SkUiDateTimeUnit.Half,
                'h' or 'H' => SkUiDateTimeUnit.Hour,
                'm' => SkUiDateTimeUnit.Minute,
                's' => SkUiDateTimeUnit.Second,
                _ => null
            };
            if (unit is { } found)
                tokens.Add(new Token(found, letter, length));
            index += length;
        }
        return tokens;
    }

    /// <summary>
    /// The tokens the kind of picker has wheels for, each unit once (the first): a 12-hour clock gets an AM / PM wheel (after
    /// its time's wheels when the format has none), a 24-hour one none.
    /// </summary>
    private static List<Token> Filter(List<Token> tokens, SkUiDateTimeKind kind)
    {
        var kept = new List<Token>();
        foreach (var token in tokens)
        {
            var isDate = token.Unit <= SkUiDateTimeUnit.Day;
            if ((kind == SkUiDateTimeKind.Date && !isDate) || (kind == SkUiDateTimeKind.Time && isDate) || kept.Any(other => other.Unit == token.Unit))
                continue;
            kept.Add(token);
        }
        var hour = kept.FindIndex(token => token.Unit == SkUiDateTimeUnit.Hour);
        var half = kept.FindIndex(token => token.Unit == SkUiDateTimeUnit.Half);
        if (hour < 0 || kept[hour].Letter == 'H')
        {
            if (half >= 0)
                kept.RemoveAt(half);
        }
        else if (half < 0)
        {
            var lastTime = kept.FindLastIndex(token => token.Unit > SkUiDateTimeUnit.Day);
            kept.Insert(lastTime + 1, new Token(SkUiDateTimeUnit.Half, 't', 2));
        }
        return kept;
    }

    private static Column CreateColumn(Token token, in SkUiDateTimeSettings settings, CultureInfo culture, SkUiStrings strings)
    {
        var info = culture.DateTimeFormat;
        var twoDigits = token.Length >= 2;
        int[] values;
        Func<int, string> text;
        string? header;
        switch (token.Unit)
        {
            case SkUiDateTimeUnit.Year:
                var first = settings.Minimum.Year;
                values = [.. Enumerable.Range(first, Math.Max(1, settings.Maximum.Year - first + 1))];
                text = token.Length == 2 ? year => (year % 100).ToString("00", culture) : year => year.ToString(new string('0', Math.Max(1, token.Length)), culture);
                header = strings.YearText;
                break;
            case SkUiDateTimeUnit.Month:
                values = [.. Enumerable.Range(1, 12)];
                text = token.Length switch
                {
                    1 => month => month.ToString(culture),
                    2 => month => month.ToString("00", culture),
                    3 => month => info.AbbreviatedMonthNames[month - 1],
                    _ => month => info.MonthNames[month - 1]
                };
                header = strings.MonthText;
                break;
            case SkUiDateTimeUnit.Day:
                values = [.. Enumerable.Range(1, 31)];
                text = day => day.ToString(twoDigits ? "00" : "0", culture);
                header = strings.DayText;
                break;
            case SkUiDateTimeUnit.Half:
                values = [0, 1];
                var am = info.AMDesignator is { Length: > 0 } amText ? amText : "AM";
                var pm = info.PMDesignator is { Length: > 0 } pmText ? pmText : "PM";
                text = half => half == 0 ? am : pm;
                header = null;
                break;
            case SkUiDateTimeUnit.Hour:
                var twelve = token.Letter == 'h';
                values = Steps(twelve ? 12 : 24, settings.HourInterval);
                text = twelve
                    ? hour => (hour == 0 ? 12 : hour).ToString(twoDigits ? "00" : "0", culture)
                    : hour => hour.ToString(twoDigits ? "00" : "0", culture);
                header = strings.HourText;
                break;
            case SkUiDateTimeUnit.Minute:
                values = Steps(60, settings.MinuteInterval);
                text = minute => minute.ToString(twoDigits ? "00" : "0", culture);
                header = strings.MinuteText;
                break;
            default:
                values = Steps(60, settings.SecondInterval);
                text = second => second.ToString(twoDigits ? "00" : "0", culture);
                header = strings.SecondText;
                break;
        }
        var loops = settings.Loop && token.Unit is not (SkUiDateTimeUnit.Year or SkUiDateTimeUnit.Half);
        return new Column(token.Unit, values, [.. values.Select(text)], loops, settings.ShowsHeaders ? header : null);
    }

    /// <summary>0, <paramref name="step"/>, 2 × <paramref name="step"/>, … below <paramref name="count"/>.</summary>
    private static int[] Steps(int count, int step)
    {
        step = Math.Max(1, step);
        var values = new int[(count + step - 1) / step];
        for (var index = 0; index < values.Length; index++)
            values[index] = index * step;
        return values;
    }

    #endregion
}
