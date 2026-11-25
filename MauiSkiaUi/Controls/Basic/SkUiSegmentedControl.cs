using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A row of mutually exclusive options drawn as one control, the selected one marked by the look (a sliding pill, a raised
/// thumb, a filled segment with a check, a bar): a view switch, a filter, a mode. Options come from <see cref="Segments"/>
/// (the XAML content) or <see cref="ItemsSource"/>; <see cref="SelectedIndex"/> and <see cref="SelectedItem"/> (two-way) say
/// which is selected. MAUI has no counterpart. The Core twin is <see cref="Core.SkUiCoreSegmentedControl"/>; see
/// <c>docs/controls/SkUiSegmentedControl.md</c>.
/// </summary>
[ContentProperty(nameof(Segments))]
public class SkUiSegmentedControl : SkUiView, ISkUiFontElement, ISkUiSegmentedOwner, ISkUiKeyHandler
{
    /// <summary>The engine shared with the Core control, created on first use: property callbacks can run before the constructor.</summary>
    internal SkUiSegmentedModel Model => field ??= new SkUiSegmentedModel(this);

    private ObservableCollection<SkUiSegment>? _segments;
    private SkUiWeakListener<SkUiSegmentedControl>? _itemsListener;
    private SkUiDisplayBinding? _itemDisplay;
    private IList? _items;
    private object? _selectedSource; // the segment or item selected, found again when the options change
    private int _requested = -1;     // the index the app or the user asked for last: it waits while there are too few options
    private int _applied = -1;       // the index applied last (the previous index of the next change)
    private bool _syncing;
    private bool _coercing;

    /// <summary>Creates a segmented control without segments.</summary>
    public SkUiSegmentedControl() { }

    #region Bindable properties

    /// <summary>Bindable property for <see cref="ItemsSource"/>.</summary>
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(SkUiSegmentedControl), null,
        propertyChanged: (view, _, value) => ((SkUiSegmentedControl)view).OnItemsSourceChanged((IEnumerable?)value));

    /// <summary>Bindable property for <see cref="SelectedIndex"/> (two-way by default: a tap sets it).</summary>
    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(SkUiSegmentedControl), -1,
        BindingMode.TwoWay, validateValue: static (_, value) => value is >= -1,
        propertyChanged: (view, _, value) => ((SkUiSegmentedControl)view).OnSelectedIndexChanged((int)value));

    /// <summary>Bindable property for <see cref="SelectedItem"/> (two-way by default).</summary>
    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(SkUiSegmentedControl), null,
        BindingMode.TwoWay, propertyChanged: (view, _, value) => ((SkUiSegmentedControl)view).OnSelectedItemChanged(value));

    /// <summary>Bindable property for <see cref="SegmentWidthMode"/>.</summary>
    public static readonly BindableProperty SegmentWidthModeProperty = BindableProperty.Create(nameof(SegmentWidthMode), typeof(SkUiSegmentWidthMode),
        typeof(SkUiSegmentedControl), SkUiSegmentWidthMode.Equal, validateValue: static (_, value) => Enum.IsDefined((SkUiSegmentWidthMode)value),
        propertyChanged: (view, _, value) => ((SkUiSegmentedControl)view).Model.WidthMode = (SkUiSegmentWidthMode)value);

    /// <summary>Bindable property for <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(SkUiSegmentedControl), null,
        propertyChanged: (view, _, value) => ((SkUiSegmentedControl)view).OnColorChanged(model => model.TextColor = (Color?)value));

    /// <summary>Bindable property for <see cref="SelectedTextColor"/>.</summary>
    public static readonly BindableProperty SelectedTextColorProperty = BindableProperty.Create(nameof(SelectedTextColor), typeof(Color), typeof(SkUiSegmentedControl), null,
        propertyChanged: (view, _, value) => ((SkUiSegmentedControl)view).OnColorChanged(model => model.SelectedTextColor = (Color?)value));

    /// <summary>Bindable property for <see cref="SelectionColor"/>.</summary>
    public static readonly BindableProperty SelectionColorProperty = BindableProperty.Create(nameof(SelectionColor), typeof(Color), typeof(SkUiSegmentedControl), null,
        propertyChanged: (view, _, value) => ((SkUiSegmentedControl)view).OnColorChanged(model => model.SelectionColor = (Color?)value));

    /// <summary>Bindable property for <see cref="TrackColor"/>.</summary>
    public static readonly BindableProperty TrackColorProperty = BindableProperty.Create(nameof(TrackColor), typeof(Color), typeof(SkUiSegmentedControl), null,
        propertyChanged: (view, _, value) => ((SkUiSegmentedControl)view).OnColorChanged(model => model.TrackColor = (Color?)value));

    /// <summary>Bindable property for <see cref="CornerRadius"/>.</summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(SkUiSegmentedControl), -1d,
        validateValue: static (_, value) => value is double radius && IsCornerRadius(radius),
        propertyChanged: (view, _, value) => ((SkUiSegmentedControl)view).OnColorChanged(model => model.CornerRadius = (double)value));

    /// <summary>Bindable property for <see cref="FontFamily"/>.</summary>
    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(SkUiSegmentedControl), null,
        propertyChanged: (view, _, value) => ((SkUiSegmentedControl)view).OnTextStyleChanged(model => model.FontFamily = (string?)value));

    /// <summary>Bindable property for <see cref="FontSize"/>.</summary>
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(double), typeof(SkUiSegmentedControl), 0d,
        defaultValueCreator: static _ => SkUiLook.Current.DefaultFontSize, validateValue: SkUiValidate.FinitePositive,
        propertyChanged: (view, _, value) => ((SkUiSegmentedControl)view).OnTextStyleChanged(model => model.FontSize = view.IsSet(FontSizeProperty) ? (double)value : double.NaN));

    /// <summary>Bindable property for <see cref="FontAttributes"/>.</summary>
    public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(nameof(FontAttributes), typeof(FontAttributes), typeof(SkUiSegmentedControl),
        FontAttributes.None, propertyChanged: (view, _, value) => ((SkUiSegmentedControl)view).OnTextStyleChanged(model => model.FontAttributes = (FontAttributes)value));

    /// <summary>Bindable property for <see cref="FontAutoScalingEnabled"/>.</summary>
    public static readonly BindableProperty FontAutoScalingEnabledProperty = BindableProperty.Create(nameof(FontAutoScalingEnabled), typeof(bool), typeof(SkUiSegmentedControl),
        true, propertyChanged: (view, _, value) => ((SkUiSegmentedControl)view).OnTextStyleChanged(model => model.FontAutoScalingEnabled = (bool)value));

    /// <summary>Bindable property for <see cref="Command"/>.</summary>
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(SkUiSegmentedControl));

    /// <summary>Bindable property for <see cref="CommandParameter"/>.</summary>
    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(SkUiSegmentedControl));

    private static bool IsCornerRadius(double radius) => double.IsFinite(radius) && (radius >= 0 || radius == -1); // exact: -1 is the look's

    #endregion

    #region Properties

    /// <summary>
    /// The options (the XAML content): each <see cref="SkUiSegment"/> shows its text and / or icon. They take the control's
    /// binding context. While it is empty, <see cref="ItemsSource"/> gives the options.
    /// </summary>
    public IList<SkUiSegment> Segments => _segments ??= CreateSegments();

    /// <summary>
    /// Items shown as options while <see cref="Segments"/> is empty, their texts through <see cref="ItemDisplayBinding"/> (else
    /// <c>ToString()</c>). <see cref="System.Collections.Specialized.INotifyCollectionChanged"/> sources are followed (weakly).
    /// </summary>
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>The binding that gives each item's text (as MAUI's <c>Picker.ItemDisplayBinding</c>); <c>null</c>: <c>ToString()</c>.</summary>
    public BindingBase? ItemDisplayBinding
    {
        get => _itemDisplay?.Binding;
        set
        {
            if (ReferenceEquals(_itemDisplay?.Binding, value))
                return;
            _itemDisplay = value is null ? null : new SkUiDisplayBinding(value);
            OnPropertyChanged();
            Rebuild();
        }
    }

    /// <summary>
    /// The selected option's index (default -1: none). A tap sets it; set by the app, the marker moves there. Outside the
    /// options it is coerced to -1 … count − 1 (kept while there are none, applied once they come).
    /// </summary>
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    /// <summary>The selected option: its <see cref="SkUiSegment"/>, or its item of <see cref="ItemsSource"/>; kept in step with <see cref="SelectedIndex"/>.</summary>
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }

    /// <summary>
    /// How the segments share the width: <see cref="SkUiSegmentWidthMode.Equal"/> (default; every segment as wide as the widest)
    /// or <see cref="SkUiSegmentWidthMode.Content"/> (each as its content).
    /// </summary>
    public SkUiSegmentWidthMode SegmentWidthMode { get => (SkUiSegmentWidthMode)GetValue(SegmentWidthModeProperty); set => SetValue(SegmentWidthModeProperty, value); }

    /// <summary>An unselected segment's text color; <c>null</c> (default): the look's. Icons draw as they are, as opaque as the text.</summary>
    public Color? TextColor { get => (Color?)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }

    /// <summary>The selected segment's text color; <c>null</c> (default): the look's.</summary>
    public Color? SelectedTextColor { get => (Color?)GetValue(SelectedTextColorProperty); set => SetValue(SelectedTextColorProperty, value); }

    /// <summary>The selection marker's color; <c>null</c> (default): the look's.</summary>
    public Color? SelectionColor { get => (Color?)GetValue(SelectionColorProperty); set => SetValue(SelectionColorProperty, value); }

    /// <summary>The track's color; <c>null</c> (default): the look's.</summary>
    public Color? TrackColor { get => (Color?)GetValue(TrackColorProperty); set => SetValue(TrackColorProperty, value); }

    /// <summary>The track's corner radius in DIPs; -1 (default): the look's. 0 squares it; half the height or more makes a capsule.</summary>
    public double CornerRadius { get => (double)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>The segments' font family.</summary>
    public string? FontFamily { get => (string?)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <summary>The segments' font size in DIPs (default: the look's).</summary>
    [TypeConverter(typeof(FontSizeConverter))]
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>Bold and / or italic segment texts.</summary>
    public FontAttributes FontAttributes { get => (FontAttributes)GetValue(FontAttributesProperty); set => SetValue(FontAttributesProperty, value); }

    /// <summary>Whether the texts follow the system text size (default <c>true</c>).</summary>
    public bool FontAutoScalingEnabled { get => (bool)GetValue(FontAutoScalingEnabledProperty); set => SetValue(FontAutoScalingEnabledProperty, value); }

    /// <summary>Runs after the user selects a segment (a tap, a key, a screen reader), when it can execute; not when the app sets the selection.</summary>
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    /// <summary>The parameter of <see cref="Command"/>.</summary>
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    /// <summary>Raised after <see cref="SelectedIndex"/> changes, from the user or the app.</summary>
    public event EventHandler<SkUiSelectedIndexChangedEventArgs>? SelectionChanged;

    /// <summary>The number of options.</summary>
    public int SegmentCount => Model.Count;

    /// <summary>Replaces <see cref="Segments"/> with one text segment per text.</summary>
    public SkUiSegmentedControl SetSegments(params string[] texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        var segments = Segments;
        segments.Clear();
        foreach (var text in texts)
            segments.Add(new SkUiSegment(text));
        return this;
    }

    /// <summary>Sets <see cref="ItemsSource"/> (same as the property setter).</summary>
    public SkUiSegmentedControl SetItemsSource(IEnumerable? value) { ItemsSource = value; return this; }

    /// <summary>Sets <see cref="ItemDisplayBinding"/> (same as the property setter).</summary>
    public SkUiSegmentedControl SetItemDisplayBinding(BindingBase? value) { ItemDisplayBinding = value; return this; }

    /// <summary>Sets <see cref="SelectedIndex"/> (same as the property setter).</summary>
    public SkUiSegmentedControl SetSelectedIndex(int value) { ArgumentOutOfRangeException.ThrowIfLessThan(value, -1); SelectedIndex = value; return this; }

    /// <summary>Sets <see cref="SelectedItem"/> (same as the property setter).</summary>
    public SkUiSegmentedControl SetSelectedItem(object? value) { SelectedItem = value; return this; }

    /// <summary>Sets <see cref="SegmentWidthMode"/> (same as the property setter).</summary>
    public SkUiSegmentedControl SetSegmentWidthMode(SkUiSegmentWidthMode value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        SegmentWidthMode = value;
        return this;
    }

    /// <summary>Sets <see cref="TextColor"/> (same as the property setter).</summary>
    public SkUiSegmentedControl SetTextColor(Color? value) { TextColor = value; return this; }

    /// <summary>Sets <see cref="SelectedTextColor"/> (same as the property setter).</summary>
    public SkUiSegmentedControl SetSelectedTextColor(Color? value) { SelectedTextColor = value; return this; }

    /// <summary>Sets <see cref="SelectionColor"/> (same as the property setter).</summary>
    public SkUiSegmentedControl SetSelectionColor(Color? value) { SelectionColor = value; return this; }

    /// <summary>Sets <see cref="TrackColor"/> (same as the property setter).</summary>
    public SkUiSegmentedControl SetTrackColor(Color? value) { TrackColor = value; return this; }

    /// <summary>Sets <see cref="CornerRadius"/> (same as the property setter).</summary>
    public SkUiSegmentedControl SetCornerRadius(double value)
    {
        if (!IsCornerRadius(value)) throw new ArgumentOutOfRangeException(nameof(value), value, "A corner radius is -1 (the look's) or finite and not negative.");
        CornerRadius = value;
        return this;
    }

    /// <summary>Sets <see cref="FontFamily"/> (same as the property setter).</summary>
    public SkUiSegmentedControl SetFontFamily(string? value) { FontFamily = value; return this; }

    /// <summary>Sets <see cref="FontSize"/> (same as the property setter).</summary>
    public SkUiSegmentedControl SetFontSize(double value) { SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value)); FontSize = value; return this; }

    /// <summary>Sets <see cref="FontAttributes"/> (same as the property setter).</summary>
    public SkUiSegmentedControl SetFontAttributes(FontAttributes value) { FontAttributes = value; return this; }

    /// <summary>Sets <see cref="FontAutoScalingEnabled"/> (same as the property setter).</summary>
    public SkUiSegmentedControl SetFontAutoScalingEnabled(bool value) { FontAutoScalingEnabled = value; return this; }

    /// <summary>Sets <see cref="Command"/> (same as the property setter).</summary>
    public SkUiSegmentedControl SetCommand(ICommand? value) { Command = value; return this; }

    /// <summary>Sets <see cref="CommandParameter"/> (same as the property setter).</summary>
    public SkUiSegmentedControl SetCommandParameter(object? value) { CommandParameter = value; return this; }

    #endregion

    #region Options

    private ObservableCollection<SkUiSegment> CreateSegments()
    {
        var segments = new ObservableCollection<SkUiSegment>();
        segments.CollectionChanged += OnSegmentsChanged;
        return segments;
    }

    private void OnSegmentsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.OldItems is { } removed)
            foreach (SkUiSegment segment in removed)
                Detach(segment);
        if (args.Action == NotifyCollectionChangedAction.Reset)
            foreach (var segment in _attached.ToArray())
                if (!_segments!.Contains(segment))
                    Detach(segment);
        foreach (var segment in _segments!)
            Attach(segment);
        Rebuild();
    }

    private readonly HashSet<SkUiSegment> _attached = [];

    private void Attach(SkUiSegment segment)
    {
        if (!_attached.Add(segment))
            return;
        segment.Parent = this;
        SetInheritedBindingContext(segment, BindingContext);
        segment.PropertyChanged += OnSegmentPropertyChanged;
    }

    private void Detach(SkUiSegment segment)
    {
        if (!_attached.Remove(segment))
            return;
        segment.PropertyChanged -= OnSegmentPropertyChanged;
        if (ReferenceEquals(segment.Parent, this))
            segment.Parent = null;
        if (segment.Icon is { } icon)
            ReadoptIcon(icon);
    }

    /// <summary>A segment let <paramref name="icon"/> go: another one that shows it takes it, so its bindings go on.</summary>
    internal void ReadoptIcon(ImageSource icon)
    {
        foreach (var segment in _attached)
            if (ReferenceEquals(segment.Icon, icon))
            {
                segment.AdoptIcon();
                return;
            }
    }

    private void OnSegmentPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(AutomationId) or SkUiSemantics.DescriptionProperty)
        {
            InvalidateSemantics();
            return;
        }
        if (args.PropertyName is not (nameof(SkUiSegment.Text) or nameof(SkUiSegment.Icon) or nameof(SkUiSegment.IsEnabled))
            || sender is not SkUiSegment segment || _segments is not { } segments)
            return;
        var index = segments.IndexOf(segment);
        if (index >= 0)
            Model.SetSegment(index, segment.Text, segment.Icon, segment.IsEnabled);
    }

    private void OnItemsSourceChanged(IEnumerable? value)
    {
        (_itemsListener ??= new SkUiWeakListener<SkUiSegmentedControl>(this, static (control, change) =>
        {
            if (change.Kind == SkUiChangeKind.Collection)
                control.Rebuild();
        })).Listen(value as INotifyCollectionChanged);
        Rebuild();
    }

    /// <summary>Whether the options are the items of <see cref="ItemsSource"/> (no segments).</summary>
    private bool ShowsItems => (_segments is null || _segments.Count == 0) && ItemsSource is not null;

    /// <summary>The option at <paramref name="index"/>: its segment or item.</summary>
    private object? OptionAt(int index) =>
        index < 0 ? null
        : ShowsItems ? _items is { } items && index < items.Count ? items[index] : null
        : _segments is { } segments && index < segments.Count ? segments[index] : null;

    private int IndexOfOption(object? option)
    {
        if (option is null)
            return -1;
        if (!ShowsItems)
            return option is SkUiSegment segment && _segments is { } segments ? segments.IndexOf(segment) : -1;
        if (_items is not { } items)
            return -1;
        for (var index = 0; index < items.Count; index++)
            if (Equals(items[index], option))
                return index;
        return -1;
    }

    /// <summary>The options changed: shown again; the selected one stays selected where it moved, else the index stays within them.</summary>
    private void Rebuild()
    {
        var model = Model;
        if (ShowsItems)
        {
            _items = ItemsSource as IList ?? ItemsSource!.Cast<object?>().ToList();
            var texts = (_itemDisplay ?? SkUiDisplayBinding.ItemText).GetTexts(_items);
            model.SetCount(texts.Length);
            for (var index = 0; index < texts.Length; index++)
                model.SetSegment(index, texts[index], null, true);
        }
        else
        {
            _items = null;
            var segments = _segments;
            model.SetCount(segments?.Count ?? 0);
            if (segments is not null)
                for (var index = 0; index < segments.Count; index++)
                    model.SetSegment(index, segments[index].Text, segments[index].Icon, segments[index].IsEnabled);
        }
        var count = model.Count;
        // An index asked for beyond the options waits for them to come (XAML adds segments one by one after setting
        // SelectedIndex); once reached, the option is what stays selected: where it is while that place still shows it (equal
        // items), else where it moved. An item selected before the options came waits too.
        var waiting = _applied != _requested;
        if (_pendingItem is not null && IndexOfOption(_pendingItem) is >= 0 and var pending)
        {
            _pendingItem = null;
            (_requested, waiting) = (pending, true);
        }
        var found = waiting || _selectedSource is null ? -1 : Equals(OptionAt(_applied), _selectedSource) ? _applied : IndexOfOption(_selectedSource);
        if (found >= 0)
            _requested = found;
        else if (!waiting && _selectedSource is not null && count > 0)
            _requested = Math.Min(_applied, count - 1); // the selected option went: the one in its place
        var selected = count == 0 ? SelectedIndex : Math.Min(_requested, count - 1);
        if (count > 0 && selected >= 0)
            _pendingItem = null; // another option is selected: an item that comes later does not take over
        if (selected != SelectedIndex)
        {
            _coercing = true;
            try { WriteBack(SelectedIndexProperty, selected); } // the callback applies it
            finally { _coercing = false; }
        }
        else
        {
            Apply(selected, animate: false);
        }
    }

    private object? _pendingItem; // selected before there were options

    #endregion

    #region Selection

    private void OnSelectedIndexChanged(int value)
    {
        if (_syncing)
            return;
        // Read once: a handler that sets the index again asks for itself (MAUI sets it after this callback).
        var coerced = _coercing;
        _coercing = false;
        if (!coerced)
            (_requested, _pendingItem) = (value, null);
        var count = Model.Count;
        if (count > 0 && value >= count)
        {
            WriteBack(SelectedIndexProperty, count - 1); // a one-way binding is not cut
            return;
        }
        Apply(value, animate: !coerced); // the options changed: the marker is at its new place at once
    }

    /// <summary>
    /// <paramref name="value"/> is the selection now: the marker moves there (animated when <paramref name="animate"/>), the
    /// selected item follows, and <see cref="SelectionChanged"/> is raised when it changed (from the index applied before, not
    /// a value coerced on the way).
    /// </summary>
    private void Apply(int value, bool animate)
    {
        var previous = _applied;
        _applied = value;
        var count = Model.Count;
        var shown = count > 0 && value < count ? value : -1;
        Model.Select(shown, animate);
        _selectedSource = OptionAt(shown);
        // An item waiting for its option is not cleared meanwhile.
        if (count > 0 && !Equals(SelectedItem, _selectedSource) && (_pendingItem is null || _selectedSource is not null))
        {
            _syncing = true;
            try { WriteBack(SelectedItemProperty, _selectedSource); }
            finally { _syncing = false; }
        }
        if (previous != value)
            SelectionChanged?.Invoke(this, new SkUiSelectedIndexChangedEventArgs(previous, value));
        InvalidateSemantics();
    }

    private void OnSelectedItemChanged(object? value)
    {
        if (_syncing)
            return;
        if (Model.Count == 0)
        {
            _pendingItem = value; // until the options come
            return;
        }
        _pendingItem = null;
        var index = IndexOfOption(value);
        if (value is not null && index < 0)
        {
            // Not an option: the selected item stays.
            _syncing = true;
            try { WriteBack(SelectedItemProperty, _selectedSource); }
            finally { _syncing = false; }
            return;
        }
        if (index != SelectedIndex)
            WriteBack(SelectedIndexProperty, index);
    }

    /// <summary>The user chose segment <paramref name="index"/> (a tap, a key, a screen reader): it is selected, then <see cref="Command"/> runs.</summary>
    internal bool SelectByUser(int index)
    {
        if (!Model.IsSegmentEnabled(index) || index == SelectedIndex)
            return false;
        WriteBack(SelectedIndexProperty, index);
        if (Command is { } command && command.CanExecute(CommandParameter))
            command.Execute(CommandParameter);
        return true;
    }

    #endregion

    #region View

    private void OnColorChanged(Action<SkUiSegmentedModel> apply)
    {
        apply(Model);
        Model.ColorsChanged();
    }

    private void OnTextStyleChanged(Action<SkUiSegmentedModel> apply)
    {
        apply(Model);
        Model.TextStyleChanged();
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => Model.Measure(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) => Model.Draw(canvas, (float)Width, (float)Height);

    /// <inheritdoc />
    protected override bool HandlesTap => IsEnabled && Model.Count > 0;

    /// <inheritdoc />
    protected override void OnTapped(SkUiTappedEventArgs args)
    {
        RaiseTapped(args);
        ExecuteTappedCommand();
        SelectByUser(Model.HitTest(args.Position));
    }

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
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        switch (propertyName)
        {
            case nameof(IsEnabled):
                Model.IsEnabled = IsEnabled;
                InvalidatePaint();
                InvalidateSemantics();
                break;
            case nameof(IsFocused):
                Model.SetFocused(IsFocused);
                break;
        }
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        foreach (var segment in _attached)
            SetInheritedBindingContext(segment, BindingContext);
        if (ShowsItems)
            Rebuild(); // texts through a binding may read the context
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

    Size ISkUiSegmentedOwner.ControlSize => new(Width, Height);

    bool ISkUiSegmentedOwner.IsRightToLeft => IsRightToLeft;

    string? ISkUiSegmentedOwner.DescriptionOf(int index) =>
        !ShowsItems && _segments is { } segments && index < segments.Count ? SemanticProperties.GetDescription(segments[index]) : null;

    string ISkUiSegmentedOwner.AutomationIdOf(int index) =>
        !ShowsItems && _segments is { } segments && index < segments.Count && segments[index].AutomationId is { Length: > 0 } id
            ? id : index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    bool ISkUiSegmentedOwner.SelectByUser(int index) => SelectByUser(index);

    void ISkUiSegmentedOwner.InvalidateMeasure() => InvalidateMeasureOverride();

    void ISkUiSegmentedOwner.InvalidatePaint() => InvalidatePaint();

    void ISkUiSegmentedOwner.InvalidateSemantics() => InvalidateSemantics();

    #endregion
}
