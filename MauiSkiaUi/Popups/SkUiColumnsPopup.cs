namespace MauiSkiaUi;

/// <summary>
/// The look of a wheel (<see cref="SkUiColumnsPopup"/>): items fade and tilt away from the drum's front as they leave the middle
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
/// A multi-column picker's popup content: one wheel per column side by side (a vertical, snapping <see cref="SkUiCarouselView"/>,
/// looping when the column loops) behind a band that marks the middle row, an optional title above (window popups). Scrolling or
/// tapping a wheel chooses its item (<see cref="SkUiSelectionPopupRequest.SetSelection"/>: at once, or as a draft that the OK button
/// below the wheels accepts with <c>UpdateMode</c> <see cref="SkUiPickerUpdateMode.WhenFinished"/>); the popup stays open. With
/// keyboard focus (it takes it when shown): Left / Right change the column, Up / Down, Page Up / Page Down, Home / End and typing
/// move its wheel, Enter or Space accept, Escape cancels. Holds the texts it was opened with, never the picker.
/// </summary>
internal sealed class SkUiColumnsPopup : SkUiContentView, ISkUiKeyHandler, ISkUiPickerPopupContent
{
    /// <summary>Items whose texts are measured for a column's width (more would cost too much on a long list).</summary>
    private const int MeasuredItems = 200;

    /// <summary>Room left and right of a wheel's texts.</summary>
    private const double TextPadding = 16;

    /// <summary>The size of the columns' headers relative to the picker's font.</summary>
    private const double HeaderFontScale = 0.8;

    /// <summary>Raised with the column whose wheel moved its middle row (its items follow).</summary>
    private event Action<int>? WheelPositionChanged;

    /// <summary>The object a column's item template binds to: the column's item, else its text.</summary>
    private object? ItemOf(int column, int index, string text) =>
        _parts[column].ItemsSource is { } source && index < source.Count ? source[index] : text;

    private readonly string[][] _texts;
    private readonly Entry[][] _entries;
    private readonly bool[] _loops;
    private readonly double[] _widths;
    private readonly int[] _initial;
    private readonly int _rows;
    private readonly SkUiPickerRowStyle _style;
    private readonly bool _rightToLeft;
    private readonly Action<IReadOnlyList<int>> _apply;
    private readonly Func<IReadOnlyList<int>> _current;
    private readonly SkUiLabel? _title;
    private readonly SkUiView? _footer;
    private readonly SkUiView? _appHeader;
    private readonly SkUiView? _appFooter;
    private readonly SkUiPickerPopupChrome _chrome;
    private readonly SkUiCarouselView[] _wheels;
    private readonly IReadOnlyList<bool>?[] _states;
    private readonly ColumnParts[] _parts;
    private readonly SkUiLabel?[] _headers;
    private readonly double _rowHeight;
    private SkUiTypeAhead _typeAhead;
    private int _active;
    private bool _syncing;
    private bool _focusedOnce;

    /// <summary>What a column shows besides its texts: a header, an item template and the items it binds to.</summary>
    internal readonly record struct ColumnParts(string? Header, DataTemplate? ItemTemplate, System.Collections.IList? ItemsSource);

    public SkUiColumnsPopup(string[][] texts, bool[] loops, double[] widths, IReadOnlyList<int> selected, int rows, SkUiPickerRowStyle style, string? title,
        bool rightToLeft, SkUiPickerPopupChrome chrome, Action<IReadOnlyList<int>> apply, Func<IReadOnlyList<int>> current,
        IReadOnlyList<bool>?[]? states = null, ColumnParts[]? parts = null, SkUiPopupExtras extras = default)
    {
        _chrome = chrome;
        _texts = texts;
        _states = states ?? new IReadOnlyList<bool>?[texts.Length];
        _parts = parts ?? new ColumnParts[texts.Length];
        _entries = [.. texts.Select((column, c) => column.Select((text, index) => new Entry(index, text, ItemOf(c, index, text))
        {
            IsEnabled = SkUiItemStates.IsEnabled(_states[c], index)
        }).ToArray())];
        _loops = loops;
        _widths = widths;
        _initial = [.. selected];
        _rows = rows;
        _style = style;
        _rightToLeft = rightToLeft;
        _apply = apply;
        _current = current;
        var look = SkUiLook.Current;
        _rowHeight = look.PickerWheelRowHeight;
        FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        BackgroundColor = SkUiColors.DefaultBackground;
        AutomationProperties.SetIsInAccessibleTree(this, false);

        var wheels = new SkUiGrid { Padding = new Thickness(look.PopupItemPadding.Left / 2, 0) };
        var band = new SkUiBox
        {
            Color = look.PickerWheelBandColor,
            CornerRadius = look.PickerWheelBandCornerRadius,
            HeightRequest = _rowHeight,
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true
        };
        wheels.Children.Add(band);
        var widthOf = ColumnWidths();
        _wheels = new SkUiCarouselView[texts.Length];
        _headers = new SkUiLabel?[texts.Length];
        // Headers above the wheels when a column has one.
        var hasHeaders = _parts.Any(part => part.Header is not null);
        if (hasHeaders)
            wheels.RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)];
        for (var column = 0; column < texts.Length; column++)
        {
            wheels.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(Math.Max(1, widthOf[column]), GridUnitType.Star)));
            var wheel = CreateWheel(column);
            _wheels[column] = wheel;
            wheels.Children.Add(wheel);
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
            _headers[column] = label;
            wheels.Children.Add(label);
            Grid.SetColumn(label, column);
        }
        Grid.SetColumnSpan(band, Math.Max(1, texts.Length));
        if (hasHeaders)
            Grid.SetRow(band, 1);
        // The band sits behind the wheels; the wheels are transparent, so the chosen texts stay clear.
        _title = title is null ? null : SkUiPopupParts.Title(title, style);
        _footer = chrome.HasButtons ? SkUiPopupParts.Buttons(chrome, style, () => Request, () => Request?.Clear()) : null;
        _appHeader = extras.CreateHeader();
        _appFooter = extras.CreateFooter();
        Content = SkUiPopupParts.Compose(_title, _appHeader, wheels, _appFooter, _footer);
        // Handlers last: setting the starting positions is not a choice.
        foreach (var wheel in _wheels)
        {
            wheel.PositionChanged += OnWheelPositionChanged;
            wheel.PropertyChanged += OnWheelPropertyChanged;
        }
    }

    private SkUiCarouselView CreateWheel(int column)
    {
        var count = _texts[column].Length;
        var loops = _loops[column] && count > 1;
        var padding = (_rows - 1) / 2d * _rowHeight;
        var wheel = new SkUiCarouselView
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
            VerticalOptions = LayoutOptions.Center
        };
        if (_initial[column] > 0 && _initial[column] < count)
            wheel.Position = _initial[column];
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

    /// <summary>The request this content changes the selection of (set when it is created; cleared once it has gone).</summary>
    public SkUiPopupRequest? Request { get; private set; }

    /// <summary>The wheel of a column (tests).</summary>
    internal SkUiCarouselView Wheel(int column) => _wheels[column];

    /// <summary>The column the keyboard moves (tests).</summary>
    internal int ActiveColumn => _active;

    /// <inheritdoc />
    public SkUiContentView Surface => this;

    /// <inheritdoc />
    public SkUiPopupRequest CreateRequest(View anchor, View? platformAnchor, in SkUiPopupLayout layout, in SkUiPopupRequestOptions options)
    {
        var chrome = options.Chrome;
        var request = new SkUiColumnsPopupRequest(anchor, platformAnchor, this, layout, _texts, _loops, _initial, indices => _apply(indices),
            [.. _parts.Select(part => part.Header)])
        {
            Title = options.Title,
            OverlayColor = options.OverlayColor,
            IsAnimated = options.IsAnimated,
            RequiresAccept = chrome.RequiresAccept,
            AcceptText = chrome.AcceptText,
            CancelText = chrome.CancelText
        };
        for (var column = 0; column < _states.Length; column++)
            request.SetItemStates(column, _states[column]);
        Request = request;
        return request;
    }

    /// <inheritdoc />
    public Size PreferredSize(double minimumWidth, double maximumWidth)
    {
        var padding = SkUiLook.Current.PopupItemPadding.Left;
        var total = padding + ColumnWidths().Sum();
        var width = Math.Clamp(Math.Ceiling(total), Math.Min(minimumWidth, maximumWidth), maximumWidth);
        var headers = _headers.Max(header => header?.Measure(width / Math.Max(1, _headers.Length), double.PositiveInfinity).Height ?? 0);
        var height = _rows * _rowHeight + headers + SkUiPopupParts.ExtraHeight(width, _title, _appHeader, _appFooter, _footer);
        return new Size(width, height);
    }

    /// <summary>A wheel's item reached the middle row (scrolling, a tap, the keyboard): that column chooses it.</summary>
    private void OnWheelPositionChanged(object? sender, PositionChangedEventArgs args)
    {
        if (sender is not SkUiCarouselView wheel || Array.IndexOf(_wheels, wheel) is not (>= 0 and var column))
            return;
        WheelPositionChanged?.Invoke(column);
        if (!_syncing)
            Choose(column, args.CurrentPosition);
    }

    /// <summary>A wheel stopped: on a disabled item it rolls on to the nearest one that can be chosen, and chooses it.</summary>
    private void OnWheelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(SkUiCarouselView.IsScrolling) || sender is not SkUiCarouselView { IsScrolling: false } wheel)
            return;
        var column = Array.IndexOf(_wheels, wheel);
        if (column >= 0)
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

    private void Choose(int column, int index)
    {
        if (Request is not SkUiSelectionPopupRequest request || index < 0 || index >= _texts[column].Length || request.SelectedIndices[column] == index
            || !SkUiItemStates.IsEnabled(_states[column], index))
            return;
        var next = request.SelectedIndices.ToArray();
        next[column] = index;
        request.SetSelection(next);
    }

    /// <summary>A tap on an item: its wheel scrolls it to the middle, and chooses it (also when it is the one shown).</summary>
    private void OnItemTapped(int column, int index)
    {
        _active = column;
        if (index < 0 || index >= _texts[column].Length || !SkUiItemStates.IsEnabled(_states[column], index))
            return;
        _ = _wheels[column].ScrollToIndex(index);
        Choose(column, index);
    }

    /// <inheritdoc />
    /// <remarks>A wheel whose item became disabled moves to the nearest earlier one that can be chosen (as a date's day in a shorter month).</remarks>
    public void ItemStatesChanged(int column, IReadOnlyList<bool>? states)
    {
        if (column < 0 || column >= _states.Length)
            return;
        _states[column] = states;
        foreach (var entry in _entries[column])
            entry.IsEnabled = SkUiItemStates.IsEnabled(states, entry.Index);
        (Request as SkUiColumnsPopupRequest)?.SetItemStates(column, states);
        if (Request is SkUiSelectionPopupRequest request)
        {
            var chosen = request.SelectedIndices[column];
            MoveOffDisabled(column, chosen >= 0 ? chosen : _wheels[column].Position, step: -1);
        }
    }

    /// <summary>The picker's selection changed while the popup is open (the app set it): the wheels follow.</summary>
    public void SelectionChanged(int index)
    {
        var chosen = _current();
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
        (Request as SkUiSelectionPopupRequest)?.Refresh(chosen);
    }

    /// <inheritdoc />
    public bool OnKeyDown(in SkUiKeyEvent key)
    {
        if (Request is not { } request || request.CloseRequested.IsCancellationRequested)
            return false;
        switch (key.Key)
        {
            case SkUiKey.Escape:
                request.Cancel();
                return true;
            case SkUiKey.Enter:
            case SkUiKey.Space when !key.IsRepeat:
                request.Accept();
                return true;
            case SkUiKey.Tab:
                return true; // focus stays in the popup
        }
        if (_wheels.Length == 0 || key.HasCommandModifier)
            return false;
        var column = _active;
        var count = _texts[column].Length;
        var current = Request is SkUiSelectionPopupRequest selection ? selection.SelectedIndices[column] : -1;
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
            _active = Math.Clamp(column + step, 0, _wheels.Length - 1);
            return true;
        }
        if (target == int.MinValue || count == 0)
            return false;
        // Disabled items are passed by in the key's direction.
        var direction = key.Key is SkUiKey.Up or SkUiKey.PageUp or SkUiKey.End ? -1 : 1;
        target = SkUiItemStates.Nearest(_states[column], count, target, direction, _loops[column] && count > 1);
        if (target < 0)
            return true;
        _ = _wheels[column].ScrollToIndex(target, animated: false);
        Choose(column, target);
        return true;
    }

    /// <inheritdoc />
    internal override bool TakesKeyboardFocus => true;

    /// <inheritdoc />
    internal override bool ShowsFocusRing => false;

    /// <inheritdoc />
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler is null || _focusedOnce)
            return;
        _focusedOnce = true;
        // Shown: keys go to the wheels (the field's surface loses them).
        Dispatcher.Dispatch(() =>
        {
            if (Request is not null)
                Focus();
        });
    }

    /// <summary>The popup has gone: lets go of the request and the wheels' items.</summary>
    public void Release()
    {
        Request = null;
        foreach (var wheel in _wheels)
        {
            wheel.PositionChanged -= OnWheelPositionChanged;
            wheel.PropertyChanged -= OnWheelPropertyChanged;
            wheel.ItemsSource = null;
        }
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
    /// <c>Selected</c> state while it is in the middle row, in <c>Disabled</c> when it cannot be chosen); a tap chooses it.
    /// </summary>
    private sealed class WheelItem : SkUiContentView
    {
        /// <summary>How much a disabled item without a template shows (Material's disabled text; the wheel's fade multiplies it).</summary>
        private const double DisabledOpacity = 0.38;

        private readonly SkUiColumnsPopup _owner;
        private readonly int _column;
        private readonly DataTemplate? _template;
        private readonly SkUiLabel? _label;
        private DataTemplate? _shownTemplate;
        private Entry? _entry;

        public WheelItem(SkUiPickerRowStyle style, SkUiColumnsPopup owner, int column, DataTemplate? template)
        {
            _owner = owner;
            _column = column;
            _template = template;
            // Owned: the popup and its items go together.
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
