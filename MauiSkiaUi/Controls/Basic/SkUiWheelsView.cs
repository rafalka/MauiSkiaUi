namespace MauiSkiaUi;

/// <summary>
/// The look of a wheel (<see cref="SkUiWheelsView"/>): items fade and tilt away from the drum's front as they leave the middle
/// row. Pure, as <see cref="SkUiCarouselEffect"/> requires: nothing but the position decides.
/// </summary>
internal sealed class SkUiWheelEffect : SkUiCarouselEffect
{
    public static readonly SkUiWheelEffect Instance = new();

    private const double FadePerRow = 0.32;
    private const double MinimumOpacity = 0.15;
    private const double TiltPerRow = 26;
    private const double ShrinkPerRow = 0.06;

    public override SkUiCarouselItemTransform GetItemTransform(double position, in SkUiCarouselItemMetrics metrics)
    {
        var distance = Math.Abs(position);
        return new SkUiCarouselItemTransform
        {
            Opacity = Math.Clamp(1 - FadePerRow * distance, MinimumOpacity, 1),
            Scale = 1 - ShrinkPerRow * Math.Min(distance, 3),
            Tilt = Math.Clamp(position, -2.5, 2.5) * TiltPerRow,
            ZIndex = -distance
        };
    }
}

/// <summary>
/// The rules of wheels whose columns depend on each other (a date's day on its month and year, a range across columns): which
/// items can be chosen with the others' choice, and where a choice that cannot be made moves. Wheels with rules
/// (<see cref="SkUiWheelsView"/>) ask them after every change, in both update modes, and the request a popup creates with them is
/// typed to what the wheels choose (<see cref="SkUiDatePopupRequest"/>).
/// </summary>
internal interface ISkUiColumnsRules
{
    /// <summary>The choice <paramref name="selection"/> (one index per column, none -1) leads to: itself, or the nearest one that can be made.</summary>
    int[] Coerce(IReadOnlyList<int> selection);

    /// <summary>Which items of <paramref name="column"/> can be chosen next to the other columns of <paramref name="selection"/> (<c>null</c>: all).</summary>
    IReadOnlyList<bool>? ItemStates(int column, IReadOnlyList<int> selection);

    /// <summary>The request that carries the wheels.</summary>
    SkUiColumnsPopupRequest CreateRequest(View anchor, View? platformAnchor, View content, in SkUiPopupLayout layout, in SkUiPopupRequestOptions options,
        IReadOnlyList<int> selected, Action<IReadOnlyList<int>> apply);
}

/// <summary>
/// Wheels side by side, one per column (a vertical, snapping <see cref="SkUiCarouselView"/>, looping when the column loops), behind a
/// band that marks the middle row, with optional headers above. Scrolling, tapping an item, the keys it is handed
/// (<see cref="OnKeyDown"/>: Left / Right change the column, Up / Down, Page Up / Page Down, Home / End and typing move its wheel) and
/// screen readers (each wheel one adjustable element: its column's name, the item in its middle row and its place, increment /
/// decrement for the next / previous item that can be chosen) choose through its host: <c>chosen</c> says what is chosen (<c>null</c>:
/// the wheels choose nothing now), <c>select</c> takes a new choice (one index per column). Items that cannot be chosen are dimmed and
/// passed; wheels with rules (<see cref="ISkUiColumnsRules"/>) coerce every choice and update the items' states. Holds the texts it was
/// created with, never a picker. The content of <see cref="SkUiColumnsPopup"/>.
/// </summary>
internal sealed class SkUiWheelsView : SkUiContentView
{
    /// <summary>Items whose texts are measured for a column's width (more would cost too much on a long list).</summary>
    private const int MeasuredItems = 200;

    /// <summary>Room left and right of a wheel's texts.</summary>
    internal const double TextPadding = 16;

    /// <summary>The size of the columns' headers relative to the picker's font.</summary>
    private const double HeaderFontScale = 0.8;

    /// <summary>
    /// What a column shows besides its texts: a header, an item template and the items it binds to; what screen readers call its
    /// wheel (<c>null</c>: the header) and read after it.
    /// </summary>
    internal readonly record struct ColumnParts(string? Header, DataTemplate? ItemTemplate, System.Collections.IList? ItemsSource,
        string? Name = null, string? Hint = null);

    /// <summary>Raised with the column whose wheel moved its middle row (its items follow).</summary>
    private event Action<int>? WheelPositionChanged;

    /// <summary>The object a column's item template binds to: the column's item, else its text.</summary>
    private object? ItemOf(int column, int index, string text) =>
        _parts[column].ItemsSource is { } source && index < source.Count ? source[index] : text;

    private readonly string[][] _texts;
    private readonly Entry[][] _entries;
    private readonly bool[] _loops;
    private readonly double[] _widths;
    private readonly int _rows;
    private readonly SkUiPickerRowStyle _style;
    private readonly bool _rightToLeft;
    private readonly Func<IReadOnlyList<int>?> _chosen;
    private readonly Action<int[]> _select;
    private readonly Action<int, IReadOnlyList<bool>?>? _statesChanged;
    private readonly WheelView[] _wheels;
    private readonly IReadOnlyList<bool>?[] _states;
    private readonly ColumnParts[] _parts;
    private readonly SkUiLabel?[] _headers;
    private readonly double _rowHeight;
    private readonly double[] _columnWidths;
    private readonly ISkUiColumnsRules? _rules;
    private readonly int[] _start;
    private SkUiTypeAhead _typeAhead;
    private int _active;
    private bool _syncing;
    private bool _focusRing;

    /// <param name="texts">The item texts of each column.</param>
    /// <param name="loops">Whether each column's wheel loops (with more than one item).</param>
    /// <param name="widths">Each column's relative width (-1: its widest text).</param>
    /// <param name="selected">The chosen index of each column (-1: none).</param>
    /// <param name="rows">How many rows each wheel shows.</param>
    /// <param name="style">The items' text style.</param>
    /// <param name="rightToLeft">Whether the columns run right to left (Left / Right follow).</param>
    /// <param name="chosen">What is chosen now, one index per column; <c>null</c> while the wheels choose nothing.</param>
    /// <param name="select">Takes a choice, one index per column.</param>
    /// <param name="states">Which items of each column can be chosen (<c>null</c>: all); ignored with <paramref name="rules"/>.</param>
    /// <param name="parts">What each column shows besides its texts.</param>
    /// <param name="rules">The rules of columns that depend on each other.</param>
    /// <param name="positions">Where wheels with nothing chosen start (-1: the first item).</param>
    /// <param name="statesChanged">Told when the rules changed which items of a column can be chosen.</param>
    /// <param name="rowHeight">The height of a row (-1: the look's <see cref="SkUiLook.PickerWheelRowHeight"/>).</param>
    public SkUiWheelsView(string[][] texts, bool[] loops, double[] widths, IReadOnlyList<int> selected, int rows, SkUiPickerRowStyle style, bool rightToLeft,
        Func<IReadOnlyList<int>?> chosen, Action<int[]> select, IReadOnlyList<bool>?[]? states = null, ColumnParts[]? parts = null,
        ISkUiColumnsRules? rules = null, IReadOnlyList<int>? positions = null, Action<int, IReadOnlyList<bool>?>? statesChanged = null,
        double rowHeight = -1)
    {
        _texts = texts;
        _rules = rules;
        // Where each wheel starts: its chosen item, else the item the picker shows while nothing is chosen, else the first.
        _start = new int[texts.Length];
        for (var column = 0; column < _start.Length; column++)
            _start[column] = column < selected.Count && selected[column] >= 0 ? selected[column]
                : positions is not null && column < positions.Count && positions[column] >= 0 ? positions[column] : 0;
        _states = states ?? new IReadOnlyList<bool>?[texts.Length];
        if (rules is not null)
            for (var column = 0; column < _states.Length; column++)
                _states[column] = rules.ItemStates(column, _start);
        _parts = parts ?? new ColumnParts[texts.Length];
        _entries = [.. texts.Select((column, c) => column.Select((text, index) => new Entry(index, text, ItemOf(c, index, text))
        {
            IsEnabled = SkUiItemStates.IsEnabled(_states[c], index)
        }).ToArray())];
        _loops = loops;
        _widths = widths;
        _rows = rows;
        _style = style;
        _rightToLeft = rightToLeft;
        _chosen = chosen;
        _select = select;
        _statesChanged = statesChanged;
        var look = SkUiLook.Current;
        _rowHeight = rowHeight > 0 ? rowHeight : look.PickerWheelRowHeight;
        // Not an element itself: screen readers read its wheels.
        AutomationProperties.SetIsInAccessibleTree(this, false);

        var grid = new SkUiGrid { Padding = new Thickness(look.PopupItemPadding.Left / 2, 0) };
        var band = new SkUiBox
        {
            Color = look.PickerWheelBandColor,
            CornerRadius = look.PickerWheelBandCornerRadius,
            HeightRequest = _rowHeight,
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true
        };
        grid.Children.Add(band);
        // Measured once: the texts do not change for the wheels' lifetime (new texts build new wheels).
        _columnWidths = ColumnWidths();
        var widthOf = _columnWidths;
        _wheels = new WheelView[texts.Length];
        _headers = new SkUiLabel?[texts.Length];
        // Headers above the wheels when a column has one.
        var hasHeaders = _parts.Any(part => part.Header is not null);
        if (hasHeaders)
            grid.RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)];
        for (var column = 0; column < texts.Length; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(Math.Max(1, widthOf[column]), GridUnitType.Star)));
            var wheel = CreateWheel(column);
            _wheels[column] = wheel;
            grid.Children.Add(wheel);
            Grid.SetColumn(wheel, column);
            if (!hasHeaders)
                continue;
            Grid.SetRow(wheel, 1);
            if (_parts[column].Header is not { } header)
                continue;
            var label = new SkUiLabel
            {
                Text = header,
                FontFamily = style.FontFamily,
                FontSize = style.FontSize * HeaderFontScale,
                FontAutoScalingEnabled = style.FontAutoScalingEnabled,
                TextColor = SkUiColors.Muted,
                HorizontalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1,
                Padding = new Thickness(4, 8, 4, 4)
            };
            // The wheel is read with its column's name.
            AutomationProperties.SetIsInAccessibleTree(label, false);
            _headers[column] = label;
            grid.Children.Add(label);
            Grid.SetColumn(label, column);
        }
        Grid.SetColumnSpan(band, Math.Max(1, texts.Length));
        if (hasHeaders)
            Grid.SetRow(band, 1);
        // The band sits behind the wheels; the wheels are transparent, so the chosen texts stay clear.
        Content = grid;
        // Handlers last: setting the starting positions is not a choice.
        foreach (var wheel in _wheels)
        {
            wheel.PositionChanged += OnWheelPositionChanged;
            wheel.PropertyChanged += OnWheelPropertyChanged;
        }
    }

    private WheelView CreateWheel(int column)
    {
        var count = _texts[column].Length;
        var loops = _loops[column] && count > 1;
        var padding = (_rows - 1) / 2d * _rowHeight;
        var wheel = new WheelView(this, column)
        {
            Orientation = ItemsLayoutOrientation.Vertical,
            ItemExtent = _rowHeight,
            SnapPointsType = SnapPointsType.Mandatory,
            SnapPointsAlignment = SnapPointsAlignment.Center,
            Loop = loops,
            // A wheel that stops at its ends has room above its first item and below its last.
            PeekAreaInsets = loops ? default : new Thickness(0, padding, 0, padding),
            ItemEffect = SkUiLook.Current.PickerWheelEffect,
            ItemTemplate = new DataTemplate(() => new WheelItem(_style, this, column, _parts[column].ItemTemplate)),
            ItemsSource = _entries[column],
            HeightRequest = _rows * _rowHeight,
            VerticalOptions = LayoutOptions.Center,
            // A drag on a wheel turns it, also at its first or last row: it never scrolls the page or sheet around it.
            KeepsDrags = true
        };
        if (_start[column] > 0 && _start[column] < count)
            wheel.Position = _start[column];
        SemanticProperties.SetDescription(wheel, _parts[column].Name ?? _parts[column].Header);
        SemanticProperties.SetHint(wheel, _parts[column].Hint);
        return wheel;
    }

    /// <summary>The relative widths of the columns: the set width, else the widest text (the first items') and its room.</summary>
    private double[] ColumnWidths()
    {
        var widths = new double[_texts.Length];
        var layout = new SkUiTextLayout();
        var textStyle = new SkUiTextStyle(SkUiTypefaces.Resolve(_style.FontFamily, _style.FontAttributes),
            SkUiFontScaling.ScaleFontSize(_style.FontSize, _style.FontAutoScalingEnabled), LineBreakMode.NoWrap,
            CharacterSpacing: _style.CharacterSpacing, FontAttributes: _style.FontAttributes);
        for (var column = 0; column < widths.Length; column++)
        {
            if (_widths[column] >= 0)
            {
                widths[column] = _widths[column];
                continue;
            }
            var widest = 0d;
            var measured = Math.Min(_texts[column].Length, MeasuredItems);
            for (var index = 0; index < measured; index++)
            {
                widest = Math.Max(widest, layout.Measure(_texts[column][index], textStyle, default, double.PositiveInfinity).Width);
                layout.Invalidate();
            }
            widths[column] = Math.Ceiling(widest + 2 * TextPadding);
        }
        layout.Release();
        return widths;
    }

    /// <summary>The number of columns.</summary>
    public int ColumnCount => _wheels.Length;

    /// <summary>The wheel of a column.</summary>
    internal SkUiCarouselView Wheel(int column) => _wheels[column];

    /// <summary>The column the keyboard moves (the last one tapped or moved).</summary>
    internal int ActiveColumn => _active;

    /// <summary>The keyboard moves <paramref name="column"/> from now on; the focus ring (if shown) follows.</summary>
    private void SetActive(int column)
    {
        _active = column;
        ShowFocusRing(_focusRing);
    }

    /// <summary>
    /// Draws the look's focus ring on the active column's wheel (<paramref name="visible"/>), or on none: the host has keyboard focus
    /// from the keyboard (the inline wheels; a popup shows none).
    /// </summary>
    public void ShowFocusRing(bool visible)
    {
        _focusRing = visible;
        for (var column = 0; column < _wheels.Length; column++)
            _wheels[column].SetFocusRingVisible(visible && column == _active);
    }

    /// <summary>Whether a wheel is turning (dragged, flung or scrolled to an item); see <see cref="ScrollingChanged"/>.</summary>
    public bool IsScrolling => _wheels.Any(wheel => wheel.IsScrolling);

    /// <summary>Raised when a wheel starts or stops turning.</summary>
    public event EventHandler? ScrollingChanged;

    /// <summary>
    /// Whether a row passing the middle of a turning wheel gives a light tick (<see cref="SkUiHaptics"/>); <c>null</c> (default): the
    /// app-wide <see cref="SkUiGestureSettings.IsWheelHapticFeedbackEnabled"/>, read at each row.
    /// </summary>
    public bool? IsHapticFeedbackEnabled { get; set; }

    private bool HapticsOn => IsHapticFeedbackEnabled ?? SkUiGestureSettings.IsWheelHapticFeedbackEnabled;

    /// <summary>Which items of <paramref name="column"/> can be chosen now (<c>null</c>: all).</summary>
    public IReadOnlyList<bool>? StatesOf(int column) => _states[column];

    /// <summary>The width the wheels want: their texts' (or set) widths and the room beside them.</summary>
    public double PreferredWidth() => SkUiLook.Current.PopupItemPadding.Left + _columnWidths.Sum();

    /// <summary>The height the wheels want at <paramref name="width"/>: their rows and the tallest header.</summary>
    public double PreferredHeight(double width)
    {
        // Without columns there are no headers (and no wheels).
        var headers = _headers.Select(header => header?.Measure(width / Math.Max(1, _headers.Length), double.PositiveInfinity).Height ?? 0).DefaultIfEmpty(0).Max();
        return _rows * _rowHeight + headers;
    }

    /// <summary>A wheel's item reached the middle row (scrolling, a tap, the keyboard): that column chooses it.</summary>
    private void OnWheelPositionChanged(object? sender, PositionChangedEventArgs args)
    {
        if (sender is not SkUiCarouselView wheel || Array.IndexOf(_wheels, wheel) is not (>= 0 and var column))
            return;
        WheelPositionChanged?.Invoke(column);
        if (_syncing)
            return;
        // A row passing the middle of a turning wheel (a drag, a fling, a tap's scroll): the native picker's tick. Keys and screen
        // readers move the wheel at once and give none.
        if (wheel.IsScrolling && HapticsOn)
            SkUiHaptics.SelectionTick(this);
        Choose(column, args.CurrentPosition);
    }

    /// <summary>A wheel stopped: on a disabled item it rolls on to the nearest one that can be chosen, and chooses it.</summary>
    private void OnWheelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(SkUiCarouselView.IsScrolling) || sender is not SkUiCarouselView wheel)
            return;
        if (wheel.IsScrolling && HapticsOn)
            SkUiHaptics.Prepare();
        if (!wheel.IsScrolling && Array.IndexOf(_wheels, wheel) is var column and >= 0)
            Stopped(wheel, column);
        // After a stop's own correction (which may turn the wheel again): the host sees where it settled.
        ScrollingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Stopped(SkUiCarouselView wheel, int column)
    {
        if (_rules is not null)
        {
            // A wheel that stopped on an item that cannot be chosen, or that was moved while it turned: the rules decide.
            if (_chosen() is { } chosen && (chosen[column] != wheel.Position || !SkUiItemStates.IsEnabled(_states[column], wheel.Position)))
                Choose(column, wheel.Position, coerce: true);
            return;
        }
        MoveOffDisabled(column, wheel.Position, step: -1);
    }

    /// <summary>When the wheel of <paramref name="column"/> shows a disabled item at <paramref name="position"/>: to the nearest enabled one.</summary>
    private void MoveOffDisabled(int column, int position, int step)
    {
        if (SkUiItemStates.IsEnabled(_states[column], position))
            return;
        var target = SkUiItemStates.Nearest(_states[column], _texts[column].Length, position, step, _loops[column]);
        if (target < 0)
            return;
        _ = _wheels[column].ScrollToIndex(target);
        Choose(column, target);
    }

    private void Choose(int column, int index, bool coerce = false)
    {
        if (_rules is not null)
        {
            ChooseByRules(column, index, coerce);
            return;
        }
        // The item already chosen is chosen again only while another column has nothing chosen (it then takes its wheel's item).
        if (_chosen() is not { } chosen || index < 0 || index >= _texts[column].Length
            || (chosen[column] == index && !chosen.Contains(-1))
            || !SkUiItemStates.IsEnabled(_states[column], index))
            return;
        // The other columns with nothing chosen take what their wheels show: a time is never half chosen ("01:").
        var next = Shown(chosen);
        next[column] = index;
        _select(next);
    }

    /// <summary>
    /// A choice with rules: the item at <paramref name="index"/> with what the other wheels show, moved by the rules where it
    /// cannot be made (<paramref name="coerce"/>: also when the item itself cannot be chosen, a wheel that stopped on it; else
    /// a wheel passing over such items chooses nothing). The items' states follow, and the wheels move to the choice.
    /// </summary>
    private void ChooseByRules(int column, int index, bool coerce)
    {
        if (_rules is not { } rules || _chosen() is not { } chosen || index < 0 || index >= _texts[column].Length
            || (!coerce && !SkUiItemStates.IsEnabled(_states[column], index)))
            return;
        var next = Shown(chosen);
        next[column] = index;
        next = rules.Coerce(next);
        ApplyStates(next);
        if (!next.SequenceEqual(chosen))
            _select(next);
        FollowWheels(next);
    }

    /// <summary>The rules' item states for <paramref name="selection"/>: the items dim or brighten, and the host is told (its request refuses the dimmed ones).</summary>
    public void ApplyStates(IReadOnlyList<int> selection)
    {
        if (_rules is not { } rules)
            return;
        for (var column = 0; column < _states.Length; column++)
        {
            var states = rules.ItemStates(column, selection);
            _states[column] = states;
            foreach (var entry in _entries[column])
                entry.IsEnabled = SkUiItemStates.IsEnabled(states, entry.Index);
            _statesChanged?.Invoke(column, states);
        }
    }

    /// <summary>The wheels that are still show <paramref name="indices"/> (a turning wheel follows once it stops).</summary>
    private void FollowWheels(IReadOnlyList<int> indices)
    {
        _syncing = true;
        try
        {
            for (var column = 0; column < _wheels.Length && column < indices.Count; column++)
                if (indices[column] >= 0 && indices[column] < _texts[column].Length && _wheels[column].Position != indices[column] && !_wheels[column].IsScrolling)
                    _ = _wheels[column].ScrollToIndex(indices[column], animated: false);
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>The choice changed from outside (the app set it): the wheels move to <paramref name="chosen"/> without choosing.</summary>
    public void ShowChoice(IReadOnlyList<int> chosen)
    {
        _syncing = true;
        try
        {
            for (var column = 0; column < _wheels.Length && column < chosen.Count; column++)
                if (chosen[column] >= 0 && chosen[column] < _texts[column].Length && _wheels[column].Position != chosen[column])
                    _ = _wheels[column].ScrollToIndex(chosen[column], animated: false);
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary><paramref name="chosen"/>, with the columns that have nothing chosen at the item their wheel shows (one that can be chosen).</summary>
    public int[] Shown(IReadOnlyList<int> chosen)
    {
        var shown = chosen.ToArray();
        for (var column = 0; column < shown.Length && column < _wheels.Length; column++)
        {
            if (shown[column] >= 0 || _texts[column].Length == 0)
                continue;
            var position = _wheels[column].Position;
            shown[column] = SkUiItemStates.IsEnabled(_states[column], position)
                ? position
                : SkUiItemStates.Nearest(_states[column], _texts[column].Length, position, -1, _loops[column]);
        }
        return shown;
    }

    /// <summary>A tap on an item: its wheel scrolls it to the middle, and chooses it (also when it is the one shown).</summary>
    private void OnItemTapped(int column, int index)
    {
        SetActive(column);
        if (index < 0 || index >= _texts[column].Length || !SkUiItemStates.IsEnabled(_states[column], index))
            return;
        _ = _wheels[column].ScrollToIndex(index);
        Choose(column, index);
    }

    /// <summary>
    /// Which items of <paramref name="column"/> can be chosen changed (the app's): they dim or brighten, and a wheel whose item
    /// became disabled moves to the nearest earlier one that can be chosen (as a date's day in a shorter month).
    /// </summary>
    public void ItemStatesChanged(int column, IReadOnlyList<bool>? states)
    {
        if (column < 0 || column >= _states.Length)
            return;
        _states[column] = states;
        foreach (var entry in _entries[column])
            entry.IsEnabled = SkUiItemStates.IsEnabled(states, entry.Index);
        if (_chosen() is { } chosen)
            MoveOffDisabled(column, chosen[column] >= 0 ? chosen[column] : _wheels[column].Position, step: -1);
    }

    /// <summary>
    /// A key for the wheels: Left / Right change the column, Up / Down, Page Up / Page Down, Home / End and typing move its wheel to
    /// the next item that can be chosen in the key's direction, and choose it. Returns whether the key was used.
    /// </summary>
    public bool OnKeyDown(in SkUiKeyEvent key)
    {
        if (_wheels.Length == 0 || key.HasCommandModifier)
            return false;
        var column = _active;
        var count = _texts[column].Length;
        var current = _chosen() is { } chosen ? chosen[column] : -1;
        var wheelPosition = _wheels[column].Position;
        // A column with nothing chosen starts from the wheel's middle row.
        var from = current >= 0 ? current : wheelPosition;
        var page = Math.Max(1, _rows - 1);
        var target = key.Key switch
        {
            SkUiKey.Down => current < 0 ? from : from + 1,
            SkUiKey.Up => current < 0 ? from : from - 1,
            SkUiKey.PageDown => from + page,
            SkUiKey.PageUp => from - page,
            SkUiKey.Home => 0,
            SkUiKey.End => count - 1,
            SkUiKey.Character => SkUiTypeAhead.Next(_texts[column], from, key.Character, ref _typeAhead, _states[column]),
            _ => int.MinValue
        };
        if (key.Key is SkUiKey.Left or SkUiKey.Right)
        {
            var step = key.Key == SkUiKey.Right ? 1 : -1;
            if (_rightToLeft)
                step = -step;
            SetActive(Math.Clamp(column + step, 0, _wheels.Length - 1));
            return true;
        }
        if (target == int.MinValue || count == 0)
            return false;
        // Disabled items are passed by in the key's direction.
        var direction = key.Key is SkUiKey.Up or SkUiKey.PageUp or SkUiKey.End ? -1 : 1;
        target = SkUiItemStates.Nearest(_states[column], count, target, direction, _loops[column] && count > 1);
        if (target >= 0)
            MoveTo(column, target);
        return true;
    }

    /// <summary>The keyboard or a screen reader moves the wheel of <paramref name="column"/> to <paramref name="index"/> at once, and chooses it.</summary>
    private void MoveTo(int column, int index)
    {
        SetActive(column);
        _ = _wheels[column].ScrollToIndex(index, animated: false);
        Choose(column, index);
    }

    #region Screen readers

    /// <summary>
    /// The next item that can be chosen from the middle row of <paramref name="column"/>'s wheel towards <paramref name="step"/>
    /// (+1: down the wheel), around the end of a looping one; -1 when there is none.
    /// </summary>
    private int StepTarget(int column, int step)
    {
        var count = _texts[column].Length;
        var loops = _loops[column] && count > 1;
        var position = _wheels[column].Position;
        for (var offset = 1; offset < count; offset++)
        {
            var index = position + step * offset;
            if (loops)
                index = (index % count + count) % count;
            else if (index < 0 || index >= count)
                return -1;
            if (SkUiItemStates.IsEnabled(_states[column], index))
                return index;
        }
        return -1;
    }

    /// <summary>
    /// A wheel as screen readers read it, as a native wheel picker's column: adjustable, named by its column (set on the wheel as
    /// its description), with the item in its middle row and its place as the value, and stepping to the next or previous item
    /// that can be chosen. Its items are not elements of their own.
    /// </summary>
    private void PopulateWheelSemantics(int column, SkUiSemanticsInfo info)
    {
        info.Role = SkUiSemanticsRole.Slider;
        info.ChildrenHidden = true;
        var count = _texts[column].Length;
        if (count == 0)
            return;
        var position = Math.Clamp(_wheels[column].Position, 0, count - 1);
        info.Range = new SkUiSemanticsRange(0, count - 1, position);
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        info.Value = $"{_texts[column][position]}, {string.Format(culture, SkUiStrings.Current.WheelPositionFormat, position + 1, count)}";
        if (StepTarget(column, 1) >= 0)
            info.Actions |= SkUiSemanticsActions.Increment;
        if (StepTarget(column, -1) >= 0)
            info.Actions |= SkUiSemanticsActions.Decrement;
    }

    /// <summary>A screen reader's increment (the next item) or decrement (the previous one) on <paramref name="column"/>'s wheel.</summary>
    private bool PerformWheelAction(int column, SkUiSemanticsActions action)
    {
        var step = action switch
        {
            SkUiSemanticsActions.Increment => 1,
            SkUiSemanticsActions.Decrement => -1,
            _ => 0
        };
        if (step == 0 || _chosen() is null || StepTarget(column, step) is not (>= 0 and var target))
            return false;
        MoveTo(column, target);
        return true;
    }

    /// <summary>A screen reader sets <paramref name="column"/>'s wheel to an item (its index; Android's set progress, UI Automation's range value).</summary>
    private bool SetWheelValue(int column, double value)
    {
        var count = _texts[column].Length;
        if (_chosen() is null || count == 0 || !double.IsFinite(value))
            return false;
        var position = _wheels[column].Position;
        var index = Math.Clamp((int)Math.Round(value), 0, count - 1);
        index = SkUiItemStates.Nearest(_states[column], count, index, index >= position ? 1 : -1);
        if (index < 0 || index == position)
            return false;
        MoveTo(column, index);
        return true;
    }

    #endregion

    /// <summary>The wheels have gone (their popup closed): lets go of the items and stops choosing.</summary>
    public void Release()
    {
        foreach (var wheel in _wheels)
        {
            wheel.PositionChanged -= OnWheelPositionChanged;
            wheel.PropertyChanged -= OnWheelPropertyChanged;
            wheel.ItemsSource = null;
        }
    }

    /// <summary>A column's wheel: a vertical carousel that screen readers read as one adjustable element (<see cref="PopulateWheelSemantics"/>).</summary>
    private sealed class WheelView(SkUiWheelsView owner, int column) : SkUiCarouselView
    {
        protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
        {
            base.OnPopulateSemantics(info);
            owner.PopulateWheelSemantics(column, info);
        }

        protected override bool OnSemanticsAction(SkUiSemanticsActions action) =>
            owner.PerformWheelAction(column, action) || base.OnSemanticsAction(action);

        protected override bool OnSemanticsSetValue(double value) => owner.SetWheelValue(column, value);

        /// <summary>The ring is rounded as the band.</summary>
        internal override CornerRadius FocusRingCornerRadii => SkUiLook.Current.PickerWheelBandCornerRadius;
    }

    /// <summary>What a wheel shows for an item: its index (equal texts stay apart), text and whether it can be chosen.</summary>
    private sealed class Entry(int index, string text, object? item)
    {
        public object? Item { get; } = item;

        private bool _isEnabled = true;

        public int Index { get; } = index;

        public string Text { get; } = text;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value)
                    return;
                _isEnabled = value;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Raised when <see cref="IsEnabled"/> changed (the item's view dims).</summary>
        public event EventHandler? Changed;
    }

    /// <summary>
    /// One item of a wheel: its text in the picker's font, centered, or the column's item template bound to the item (its root in the
    /// <c>Selected</c> state while it is in the middle row, in <c>Disabled</c> and dimmed when it cannot be chosen); a tap chooses it.
    /// </summary>
    private sealed class WheelItem : SkUiContentView
    {
        /// <summary>How much a disabled item shows (Material's disabled text; the wheel's fade multiplies it).</summary>
        private const double DisabledOpacity = 0.38;

        private readonly SkUiWheelsView _owner;
        private readonly int _column;
        private readonly DataTemplate? _template;
        private readonly SkUiLabel? _label;
        private DataTemplate? _shownTemplate;
        private Entry? _entry;

        public WheelItem(SkUiPickerRowStyle style, SkUiWheelsView owner, int column, DataTemplate? template)
        {
            _owner = owner;
            _column = column;
            _template = template;
            // Owned: the wheels and their items go together.
            owner.WheelPositionChanged += OnWheelPositionChanged;
            if (template is not null)
                return;
            _label = new SkUiLabel
            {
                FontFamily = style.FontFamily,
                FontSize = style.FontSize,
                FontAttributes = style.FontAttributes,
                FontAutoScalingEnabled = style.FontAutoScalingEnabled,
                CharacterSpacing = style.CharacterSpacing,
                TextColor = style.TextColor ?? SkUiColors.DefaultForeground,
                Padding = new Thickness(TextPadding, 0),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1
            };
            Content = _label;
        }

        protected override bool HandlesTap => true;

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (_entry is not null)
                _entry.Changed -= OnEntryChanged;
            _entry = BindingContext as Entry;
            if (_entry is not null)
                _entry.Changed += OnEntryChanged;
            if (_label is not null)
            {
                _label.Text = _entry?.Text ?? string.Empty;
            }
            else
            {
                // A template selector picks per item: a new view when it picks another template.
                var template = _template is DataTemplateSelector selector && _entry is not null ? selector.SelectTemplate(_entry.Item, this) : _template;
                if (!ReferenceEquals(template, _shownTemplate) || Content is null)
                {
                    _shownTemplate = template;
                    Content = template?.CreateContent() as ISkUiView;
                }
                if (Content is BindableObject content)
                    content.BindingContext = _entry?.Item;
            }
            ShowState();
        }

        private void OnEntryChanged(object? sender, EventArgs args) => ShowState();

        private void OnWheelPositionChanged(int column)
        {
            if (column == _column)
                ShowState();
        }

        private void ShowState()
        {
            var enabled = _entry?.IsEnabled != false;
            if (_label is not null)
            {
                _label.Opacity = enabled ? 1 : DisabledOpacity;
                return;
            }
            if (Content is not SkUiView root)
                return;
            root.IsEnabled = enabled;
            // Dimmed too, unless the app styles the Disabled state itself (the wheel's fade multiplies it).
            Opacity = enabled || SkUiPickerPopup.HasDisabledState(root) ? 1 : DisabledOpacity;
            root.IsSelectedItem = _entry is not null && _owner._wheels.Length > _column && _owner._wheels[_column] is { } wheel && wheel.Position == _entry.Index;
        }

        protected override void OnTapped(SkUiTappedEventArgs args)
        {
            RaiseTapped(args);
            if (BindingContext is Entry entry)
                _owner.OnItemTapped(_column, entry.Index);
        }
    }
}
