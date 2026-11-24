using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core segmented control (analogue of <see cref="SkUiSegmentedControl"/>, on the same engine and look): a row of mutually
/// exclusive options drawn as one node, the selected one marked by the look. Options come from <see cref="Segments"/> or
/// <see cref="ItemsSource"/> (texts from <see cref="ItemDisplay"/>); <see cref="SelectedIndex"/> and <see cref="SelectedItem"/>
/// stay in step. Fluent <c>Set*</c> setters and <see cref="INotifyPropertyChanged"/>.
/// </summary>
public class SkUiCoreSegmentedControl : SkUiCoreNode, ISkUiFontElement, ISkUiSegmentedOwner, ISkUiKeyHandler
{
    private readonly ObservableCollection<SkUiCoreSegment> _segments = [];
    private readonly HashSet<SkUiCoreSegment> _attached = [];
    private SkUiWeakListener<SkUiCoreSegmentedControl>? _itemsListener;
    private IEnumerable? _itemsSource;
    private Func<object?, string>? _itemDisplay;
    private IList? _items;
    private int _selectedIndex = -1;
    private object? _selectedItem;
    private object? _selectedSource;
    private int _requested = -1;
    private int _applied = -1;
    private SkUiSegmentWidthMode _widthMode;
    private ICommand? _command;
    private object? _commandParameter;

    /// <summary>Creates a segmented control without segments.</summary>
    public SkUiCoreSegmentedControl()
    {
        Model = new SkUiSegmentedModel(this);
        _segments.CollectionChanged += OnSegmentsChanged;
        Focused += (_, _) => Model.SetFocused(true);
        Unfocused += (_, _) => Model.SetFocused(false);
    }

    /// <summary>The engine shared with <see cref="SkUiSegmentedControl"/>.</summary>
    internal SkUiSegmentedModel Model { get; }

    #region Properties

    /// <summary>The options: each <see cref="SkUiCoreSegment"/> shows its text and / or icon. While it is empty, <see cref="ItemsSource"/> gives the options.</summary>
    public IList<SkUiCoreSegment> Segments => _segments;

    /// <summary>Items shown as options while <see cref="Segments"/> is empty; <see cref="INotifyCollectionChanged"/> sources are followed (weakly).</summary>
    public IEnumerable? ItemsSource { get => _itemsSource; set => SetItemsSource(value); }

    /// <summary>Each item's text; <c>null</c> (default): the item's <c>ToString()</c>.</summary>
    public Func<object?, string>? ItemDisplay { get => _itemDisplay; set => SetItemDisplay(value); }

    /// <summary>The selected option's index (default -1: none); coerced to the options (kept while there are none). See <see cref="SkUiSegmentedControl.SelectedIndex"/>.</summary>
    public int SelectedIndex { get => _selectedIndex; set => SetSelectedIndex(value); }

    /// <summary>The selected option: its <see cref="SkUiCoreSegment"/>, or its item of <see cref="ItemsSource"/>.</summary>
    public object? SelectedItem { get => _selectedItem; set => SetSelectedItem(value); }

    /// <summary>How the segments share the width (default <see cref="SkUiSegmentWidthMode.Equal"/>).</summary>
    public SkUiSegmentWidthMode SegmentWidthMode { get => _widthMode; set => SetSegmentWidthMode(value); }

    /// <summary>An unselected segment's text and icon color; <c>null</c> (default): the look's.</summary>
    public Color? TextColor { get => Model.TextColor; set => SetTextColor(value); }

    /// <summary>The selected segment's text color; <c>null</c> (default): the look's.</summary>
    public Color? SelectedTextColor { get => Model.SelectedTextColor; set => SetSelectedTextColor(value); }

    /// <summary>The selection marker's color; <c>null</c> (default): the look's.</summary>
    public Color? SelectionColor { get => Model.SelectionColor; set => SetSelectionColor(value); }

    /// <summary>The track's color; <c>null</c> (default): the look's.</summary>
    public Color? TrackColor { get => Model.TrackColor; set => SetTrackColor(value); }

    /// <summary>The track's corner radius in DIPs; -1 (default): the look's.</summary>
    public double CornerRadius { get => Model.CornerRadius; set => SetCornerRadius(value); }

    /// <summary>The segments' font family.</summary>
    public string? FontFamily { get => Model.FontFamily; set => SetFontFamily(value); }

    /// <summary>The segments' font size in DIPs (default: the look's).</summary>
    public double FontSize { get => SkUiLook.FontSizeOrDefault(Model.FontSize); set => SetFontSize(value); }

    /// <summary>Bold and / or italic segment texts.</summary>
    public FontAttributes FontAttributes { get => Model.FontAttributes; set => SetFontAttributes(value); }

    /// <summary>Whether the texts follow the system text size (default <c>true</c>).</summary>
    public bool FontAutoScalingEnabled { get => Model.FontAutoScalingEnabled; set => SetFontAutoScalingEnabled(value); }

    /// <summary>Runs after the user selects a segment, when it can execute; not when code sets the selection.</summary>
    public ICommand? Command { get => _command; set => SetCommand(value); }

    /// <summary>The parameter of <see cref="Command"/>.</summary>
    public object? CommandParameter { get => _commandParameter; set => SetCommandParameter(value); }

    /// <summary>Raised after <see cref="SelectedIndex"/> changes, from the user or code.</summary>
    public event EventHandler<SkUiSelectedIndexChangedEventArgs>? SelectionChanged;

    /// <summary>The number of options.</summary>
    public int SegmentCount => Model.Count;

    /// <summary>Replaces <see cref="Segments"/> with one text segment per text.</summary>
    public SkUiCoreSegmentedControl SetSegments(params string[] texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        _segments.Clear();
        foreach (var text in texts)
            _segments.Add(new SkUiCoreSegment(text));
        return this;
    }

    /// <summary>Sets <see cref="ItemsSource"/>.</summary>
    public SkUiCoreSegmentedControl SetItemsSource(IEnumerable? value)
    {
        if (ReferenceEquals(_itemsSource, value))
            return this;
        _itemsSource = value;
        (_itemsListener ??= new SkUiWeakListener<SkUiCoreSegmentedControl>(this, static (control, change) =>
        {
            if (change.Kind == SkUiChangeKind.Collection)
                control.Rebuild();
        })).Listen(value as INotifyCollectionChanged);
        OnPropertyChanged(nameof(ItemsSource));
        Rebuild();
        return this;
    }

    /// <summary>Sets <see cref="ItemDisplay"/>.</summary>
    public SkUiCoreSegmentedControl SetItemDisplay(Func<object?, string>? value)
    {
        if (ReferenceEquals(_itemDisplay, value))
            return this;
        _itemDisplay = value;
        OnPropertyChanged(nameof(ItemDisplay));
        Rebuild();
        return this;
    }

    /// <summary>Sets <see cref="SelectedIndex"/>; beyond the last option it becomes the last.</summary>
    public SkUiCoreSegmentedControl SetSelectedIndex(int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, -1);
        _requested = value;
        var count = Model.Count;
        Select(count > 0 ? Math.Min(value, count - 1) : value, animate: true);
        return this;
    }

    /// <summary>Sets <see cref="SelectedItem"/>; a value that is not an option is ignored, <c>null</c> selects none.</summary>
    public SkUiCoreSegmentedControl SetSelectedItem(object? value)
    {
        if (Model.Count == 0)
            return this;
        var index = IndexOfOption(value);
        if (value is not null && index < 0)
            return this;
        return SetSelectedIndex(index);
    }

    /// <summary>Sets <see cref="SegmentWidthMode"/>.</summary>
    public SkUiCoreSegmentedControl SetSegmentWidthMode(SkUiSegmentWidthMode value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (SetProperty(ref _widthMode, value, nameof(SegmentWidthMode)))
            Model.WidthMode = value;
        return this;
    }

    /// <summary>Sets <see cref="TextColor"/>.</summary>
    public SkUiCoreSegmentedControl SetTextColor(Color? value) => SetColor(ref Model.TextColor, value, nameof(TextColor));

    /// <summary>Sets <see cref="SelectedTextColor"/>.</summary>
    public SkUiCoreSegmentedControl SetSelectedTextColor(Color? value) => SetColor(ref Model.SelectedTextColor, value, nameof(SelectedTextColor));

    /// <summary>Sets <see cref="SelectionColor"/>.</summary>
    public SkUiCoreSegmentedControl SetSelectionColor(Color? value) => SetColor(ref Model.SelectionColor, value, nameof(SelectionColor));

    /// <summary>Sets <see cref="TrackColor"/>.</summary>
    public SkUiCoreSegmentedControl SetTrackColor(Color? value) => SetColor(ref Model.TrackColor, value, nameof(TrackColor));

    /// <summary>Sets <see cref="CornerRadius"/> (-1: the look's).</summary>
    public SkUiCoreSegmentedControl SetCornerRadius(double value)
    {
        // Exact: -1 is the look's.
        if (!double.IsFinite(value) || (value < 0 && value != -1))
            throw new ArgumentOutOfRangeException(nameof(value), value, "A corner radius is -1 (the look's) or finite and not negative.");
        if (SetProperty(ref Model.CornerRadius, value, nameof(CornerRadius)))
            Model.ColorsChanged();
        return this;
    }

    /// <summary>Sets <see cref="FontFamily"/>.</summary>
    public SkUiCoreSegmentedControl SetFontFamily(string? value) => SetTextStyle(ref Model.FontFamily, value, nameof(FontFamily));

    /// <summary>Sets <see cref="FontSize"/>.</summary>
    public SkUiCoreSegmentedControl SetFontSize(double value)
    {
        SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value));
        return SetTextStyle(ref Model.FontSize, value, nameof(FontSize));
    }

    /// <summary>Sets <see cref="FontAttributes"/>.</summary>
    public SkUiCoreSegmentedControl SetFontAttributes(FontAttributes value) => SetTextStyle(ref Model.FontAttributes, value, nameof(FontAttributes));

    /// <summary>Sets <see cref="FontAutoScalingEnabled"/>.</summary>
    public SkUiCoreSegmentedControl SetFontAutoScalingEnabled(bool value) => SetTextStyle(ref Model.FontAutoScalingEnabled, value, nameof(FontAutoScalingEnabled));

    /// <summary>Sets <see cref="Command"/>.</summary>
    public SkUiCoreSegmentedControl SetCommand(ICommand? value)
    {
        SetProperty(ref _command, value, nameof(Command));
        return this;
    }

    /// <summary>Sets <see cref="CommandParameter"/>.</summary>
    public SkUiCoreSegmentedControl SetCommandParameter(object? value)
    {
        SetProperty(ref _commandParameter, value, nameof(CommandParameter));
        return this;
    }

    private SkUiCoreSegmentedControl SetColor(ref Color? field, Color? value, string propertyName)
    {
        if (SetProperty(ref field, value, propertyName))
            Model.ColorsChanged();
        return this;
    }

    private SkUiCoreSegmentedControl SetTextStyle<TField>(ref TField field, TField value, string propertyName)
    {
        if (SetProperty(ref field, value, propertyName))
            Model.TextStyleChanged();
        return this;
    }

    #endregion

    #region Options

    private void OnSegmentsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        foreach (var segment in _attached.ToArray())
            if (!_segments.Contains(segment))
            {
                segment.PropertyChanged -= OnSegmentPropertyChanged;
                _attached.Remove(segment);
            }
        foreach (var segment in _segments)
            if (_attached.Add(segment))
                segment.PropertyChanged += OnSegmentPropertyChanged;
        Rebuild();
    }

    private void OnSegmentPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is not SkUiCoreSegment segment)
            return;
        if (args.PropertyName is nameof(SkUiCoreSegment.AutomationId) or nameof(SkUiCoreSegment.SemanticDescription))
        {
            InvalidateSemantics();
            return;
        }
        var index = _segments.IndexOf(segment);
        if (index >= 0)
            Model.SetSegment(index, segment.Text, segment.Icon, segment.IsEnabled);
    }

    private bool ShowsItems => _segments.Count == 0 && _itemsSource is not null;

    private object? OptionAt(int index) =>
        index < 0 ? null
        : ShowsItems ? _items is { } items && index < items.Count ? items[index] : null
        : index < _segments.Count ? _segments[index] : null;

    private int IndexOfOption(object? option)
    {
        if (option is null)
            return -1;
        if (!ShowsItems)
            return option is SkUiCoreSegment segment ? _segments.IndexOf(segment) : -1;
        if (_items is not { } items)
            return -1;
        for (var index = 0; index < items.Count; index++)
            if (Equals(items[index], option))
                return index;
        return -1;
    }

    /// <summary>The options changed: shown again; the selected one stays selected where it moved, else the index asked for, within them.</summary>
    private void Rebuild()
    {
        if (ShowsItems)
        {
            _items = _itemsSource as IList ?? _itemsSource!.Cast<object?>().ToList();
            Model.SetCount(_items.Count);
            for (var index = 0; index < _items.Count; index++)
                Model.SetSegment(index, _itemDisplay is { } display ? display(_items[index]) : _items[index]?.ToString(), null, true);
        }
        else
        {
            _items = null;
            Model.SetCount(_segments.Count);
            for (var index = 0; index < _segments.Count; index++)
                Model.SetSegment(index, _segments[index].Text, _segments[index].Icon, _segments[index].IsEnabled);
        }
        var count = Model.Count;
        var found = _applied == _requested ? IndexOfOption(_selectedSource) : -1;
        if (found >= 0)
            _requested = found;
        var selected = found >= 0 ? found : count == 0 ? _selectedIndex : Math.Min(_requested, count - 1);
        Select(selected, animate: false);
    }

    #endregion

    #region Selection

    /// <summary>
    /// <paramref name="value"/> is the selection now: the marker moves there, the selected item follows, and
    /// <see cref="SelectionChanged"/> is raised when it changed.
    /// </summary>
    private void Select(int value, bool animate)
    {
        var count = Model.Count;
        var shown = count > 0 && value < count ? value : -1;
        Model.Select(shown, animate);
        _selectedSource = OptionAt(shown);
        var previous = _applied;
        _applied = value;
        SetProperty(ref _selectedIndex, value, nameof(SelectedIndex));
        if (count > 0)
            SetProperty(ref _selectedItem, _selectedSource, nameof(SelectedItem));
        if (previous != value)
            SelectionChanged?.Invoke(this, new SkUiSelectedIndexChangedEventArgs(previous, value));
        InvalidateSemantics();
    }

    /// <summary>The user chose segment <paramref name="index"/>: it is selected, then <see cref="Command"/> runs.</summary>
    internal bool SelectByUser(int index)
    {
        if (!Model.IsSegmentEnabled(index) || index == _selectedIndex)
            return false;
        SetSelectedIndex(index);
        if (_command is { } command && command.CanExecute(_commandParameter))
            command.Execute(_commandParameter);
        return true;
    }

    #endregion

    #region Node

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => Model.Measure(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) => Model.Draw(canvas, (float)Frame.Width, (float)Frame.Height);

    /// <inheritdoc />
    internal override bool HasIntrinsicTap => IsEnabled && Model.Count > 0;

    /// <inheritdoc />
    internal override void OnIntrinsicTap(SkUiTappedEventArgs args) => SelectByUser(Model.HitTest(args.Position));

    /// <inheritdoc />
    protected override void OnPressedChanged()
    {
        base.OnPressedChanged();
        if (IsPressed)
            Model.Press(Model.HitTest(PressPosition));
        else
            Model.Release();
    }

    /// <inheritdoc />
    internal override bool TracksHover => true;

    /// <inheritdoc />
    internal override void OnHovered(Point? position)
    {
        if (Model.Hover(position))
            InvalidatePaint();
    }

    /// <inheritdoc />
    protected override void OnIsEnabledChanged()
    {
        base.OnIsEnabledChanged();
        Model.IsEnabled = IsEnabled;
        InvalidatePaint();
        InvalidateSemantics();
    }

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged()
    {
        base.OnEffectiveFlowDirectionChanged();
        Model.TextStyleChanged();
    }

    /// <inheritdoc />
    private protected override void ForgetLookDefaults()
    {
        base.ForgetLookDefaults();
        Model.TextStyleChanged();
    }

    /// <inheritdoc />
    internal override void ReleaseDrawingResources()
    {
        base.ReleaseDrawingResources();
        Model.ReleaseDrawingResources();
    }

    /// <summary>Segment <paramref name="index"/>'s rectangle in the control (tests).</summary>
    internal Rect SegmentBounds(int index) => Model.SegmentBounds(index);

    /// <inheritdoc />
    internal override bool TakesKeyboardFocus => Model.Count > 0;

    /// <inheritdoc />
    /// <remarks>The look draws the focus around the track.</remarks>
    internal override bool ShowsFocusRing => false;

    bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key) => Model.OnKeyDown(key);

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        Model.PopulateSemantics(info);
    }

    /// <inheritdoc />
    protected override bool OnSemanticsAction(SkUiSemanticsActions action) => Model.PerformSemanticsAction(action) || base.OnSemanticsAction(action);

    /// <inheritdoc />
    internal override bool OnSemanticsPartAction(int part, SkUiSemanticsActions action) => Model.PerformSemanticsPartAction(part, action);

    #endregion

    #region Owner

    Size ISkUiSegmentedOwner.ControlSize => Frame.Size;

    bool ISkUiSegmentedOwner.IsRightToLeft => IsRightToLeft;

    string? ISkUiSegmentedOwner.DescriptionOf(int index) => !ShowsItems && index < _segments.Count ? _segments[index].SemanticDescription : null;

    string ISkUiSegmentedOwner.AutomationIdOf(int index) =>
        !ShowsItems && index < _segments.Count && _segments[index].AutomationId is { Length: > 0 } id
            ? id : index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    bool ISkUiSegmentedOwner.SelectByUser(int index) => SelectByUser(index);

    void ISkUiSegmentedOwner.InvalidateMeasure() => InvalidateMeasure();

    void ISkUiSegmentedOwner.InvalidatePaint() => InvalidatePaint();

    void ISkUiSegmentedOwner.InvalidateSemantics() => InvalidateSemantics();

    #endregion
}
