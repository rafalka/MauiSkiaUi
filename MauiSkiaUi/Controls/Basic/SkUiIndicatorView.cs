using System.Collections;
using System.Collections.Specialized;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn row of indicators, one per item, with the current one selected: MAUI's <c>IndicatorView</c> API (XAML ports by
/// prefix) drawn by the look (<see cref="SkUiLook.DrawIndicators"/>: <see cref="DefaultSkUiLook.IndicatorStyle"/> dots or a
/// pill, or a painter of the app's), plus <see cref="IndicatorSpacing"/> and <see cref="Orientation"/>. Link it to a
/// <see cref="SkUiCarouselView"/> with the carousel's <see cref="SkUiCarouselView.IndicatorView"/>: it then shows the
/// carousel's items, its selection follows the carousel's scrolling (also across the wrap of a looping carousel), and a tap
/// scrolls the carousel. On its own, a tap on an indicator selects it and the selection moves with the look's
/// <see cref="SkUiTransitionKind.IndicatorPosition"/> transition.
/// </summary>
/// <remarks>
/// MAUI's <c>IndicatorTemplate</c> (an indicator per template view) is not supported: draw other indicators through the look
/// (<see cref="SkUiLook.IndicatorPainter"/>, or a look that overrides <c>DrawIndicatorsCore</c>). The row is centered in the
/// view and laid out by the control, so taps hit what is drawn; a tap anywhere in the view selects the nearest indicator
/// (give the view a height or padding for a larger touch target). Screen readers and the keyboard (arrow keys, Home, End)
/// adjust the position.
/// </remarks>
public class SkUiIndicatorView : SkUiView
{
    private readonly SkUiIndicatorModel _model;
    private readonly ItemsCounter _counter;

    /// <summary>Creates an indicator view without items.</summary>
    public SkUiIndicatorView()
    {
        _model = new SkUiIndicatorModel(this)
        {
            IndicatorColor = IndicatorColor,
            SelectedColor = SelectedIndicatorColor
        };
        _counter = new ItemsCounter(this);
    }

    #region Properties

    /// <summary>Bindable property for <see cref="Position"/> (two-way by default: a tap sets it).</summary>
    public static readonly BindableProperty PositionProperty = BindableProperty.Create(nameof(Position), typeof(int), typeof(SkUiIndicatorView), 0, BindingMode.TwoWay,
        validateValue: SkUiValidate.NonNegative,
        coerceValue: (view, value) => ((SkUiIndicatorView)view).CoercePosition((int)value),
        propertyChanged: (view, old, value) => ((SkUiIndicatorView)view).OnPositionChanged((int)old, (int)value));

    /// <summary>Bindable property for <see cref="Count"/>.</summary>
    public static readonly BindableProperty CountProperty = BindableProperty.Create(nameof(Count), typeof(int), typeof(SkUiIndicatorView), 0,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiIndicatorView)view).OnCountChanged((int)value));

    /// <summary>Bindable property for <see cref="ItemsSource"/>.</summary>
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(SkUiIndicatorView), null,
        propertyChanged: (view, _, value) => ((SkUiIndicatorView)view)._counter.Observe((IEnumerable?)value));

    /// <summary>Bindable property for <see cref="IndicatorColor"/>.</summary>
    public static readonly BindableProperty IndicatorColorProperty = BindableProperty.Create(nameof(IndicatorColor), typeof(Color), typeof(SkUiIndicatorView), null,
        defaultValueCreator: _ => SkUiColors.TrackOff, validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiIndicatorView)view).OnLookChanged(model => model.IndicatorColor = (Color)value));

    /// <summary>Bindable property for <see cref="SelectedIndicatorColor"/>.</summary>
    public static readonly BindableProperty SelectedIndicatorColorProperty = BindableProperty.Create(nameof(SelectedIndicatorColor), typeof(Color), typeof(SkUiIndicatorView), null,
        defaultValueCreator: _ => SkUiColors.Accent, validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiIndicatorView)view).OnLookChanged(model => model.SelectedColor = (Color)value));

    /// <summary>Bindable property for <see cref="IndicatorSize"/>.</summary>
    public static readonly BindableProperty IndicatorSizeProperty = BindableProperty.Create(nameof(IndicatorSize), typeof(double), typeof(SkUiIndicatorView), 6d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiIndicatorView)view).OnSizeChanged(model => model.IndicatorSize = (double)value));

    /// <summary>Bindable property for <see cref="IndicatorsShape"/>.</summary>
    public static readonly BindableProperty IndicatorsShapeProperty = BindableProperty.Create(nameof(IndicatorsShape), typeof(IndicatorShape), typeof(SkUiIndicatorView), IndicatorShape.Circle,
        validateValue: (_, value) => Enum.IsDefined((IndicatorShape)value),
        propertyChanged: (view, _, value) => ((SkUiIndicatorView)view).OnSizeChanged(model => model.Shape = (IndicatorShape)value));

    /// <summary>Bindable property for <see cref="MaximumVisible"/>.</summary>
    public static readonly BindableProperty MaximumVisibleProperty = BindableProperty.Create(nameof(MaximumVisible), typeof(int), typeof(SkUiIndicatorView), int.MaxValue,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiIndicatorView)view).OnSizeChanged(model => model.MaximumVisible = (int)value));

    /// <summary>Bindable property for <see cref="HideSingle"/>.</summary>
    public static readonly BindableProperty HideSingleProperty = BindableProperty.Create(nameof(HideSingle), typeof(bool), typeof(SkUiIndicatorView), true,
        propertyChanged: (view, _, value) => ((SkUiIndicatorView)view).OnLookChanged(model => model.HideSingle = (bool)value));

    /// <summary>Bindable property for <see cref="IndicatorSpacing"/>.</summary>
    public static readonly BindableProperty IndicatorSpacingProperty = BindableProperty.Create(nameof(IndicatorSpacing), typeof(double), typeof(SkUiIndicatorView), -1d,
        validateValue: SkUiValidate.Finite,
        propertyChanged: (view, _, value) => ((SkUiIndicatorView)view).OnSizeChanged(model => model.Spacing = (double)value));

    /// <summary>Bindable property for <see cref="Orientation"/>.</summary>
    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(nameof(Orientation), typeof(StackOrientation), typeof(SkUiIndicatorView), StackOrientation.Horizontal,
        validateValue: (_, value) => Enum.IsDefined((StackOrientation)value),
        propertyChanged: (view, _, value) => ((SkUiIndicatorView)view).OnSizeChanged(model => model.Orientation = (StackOrientation)value));

    /// <summary>
    /// The selected item (0 = the first; default 0). A tap on an indicator sets it; a linked carousel keeps it at its position.
    /// Within the items: beyond the last it becomes the last (also when <see cref="Count"/> drops); without items it is kept.
    /// </summary>
    public int Position { get => (int)GetValue(PositionProperty); set => SetValue(PositionProperty, value); }

    /// <summary>How many indicators there are (default 0); set by <see cref="ItemsSource"/> (0 when it is cleared) or a linked carousel.</summary>
    public int Count { get => (int)GetValue(CountProperty); set => SetValue(CountProperty, value); }

    /// <summary>
    /// Items to count (<see cref="Count"/> follows the number of items, also as an <see cref="INotifyCollectionChanged"/>
    /// source changes; listened to weakly). A linked carousel sets <see cref="Count"/> itself.
    /// </summary>
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>Unselected indicators' color (default: the color scheme's <see cref="SkUiColorScheme.TrackOff"/> when created).</summary>
    public Color IndicatorColor { get => (Color)GetValue(IndicatorColorProperty); set => SetValue(IndicatorColorProperty, value); }

    /// <summary>The selected indicator's color (default: the color scheme's accent when created).</summary>
    public Color SelectedIndicatorColor { get => (Color)GetValue(SelectedIndicatorColorProperty); set => SetValue(SelectedIndicatorColorProperty, value); }

    /// <summary>An indicator's size in DIPs (default 6, as MAUI's).</summary>
    public double IndicatorSize { get => (double)GetValue(IndicatorSizeProperty); set => SetValue(IndicatorSizeProperty, value); }

    /// <summary>The indicators' shape: circles (default) or squares.</summary>
    public IndicatorShape IndicatorsShape { get => (IndicatorShape)GetValue(IndicatorsShapeProperty); set => SetValue(IndicatorsShapeProperty, value); }

    /// <summary>The most indicators shown (default all): with more items, a window around the selected one shows.</summary>
    public int MaximumVisible { get => (int)GetValue(MaximumVisibleProperty); set => SetValue(MaximumVisibleProperty, value); }

    /// <summary>Whether a single indicator is hidden (default <c>true</c>): with one item nothing is drawn (the view keeps its size).</summary>
    public bool HideSingle { get => (bool)GetValue(HideSingleProperty); set => SetValue(HideSingleProperty, value); }

    /// <summary>The gap between indicators in DIPs; negative (default -1): the look's (<see cref="SkUiLook.DefaultIndicatorSpacing"/>, 8). SkiaUi extension.</summary>
    public double IndicatorSpacing { get => (double)GetValue(IndicatorSpacingProperty); set => SetValue(IndicatorSpacingProperty, value); }

    /// <summary>The row's direction (default horizontal; vertical for a vertical carousel). SkiaUi extension.</summary>
    public StackOrientation Orientation { get => (StackOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>Sets <see cref="Position"/> (same as the property setter).</summary>
    public SkUiIndicatorView SetPosition(int value) { ArgumentOutOfRangeException.ThrowIfNegative(value); Position = value; return this; }

    /// <summary>Sets <see cref="Count"/> (same as the property setter).</summary>
    public SkUiIndicatorView SetCount(int value) { ArgumentOutOfRangeException.ThrowIfNegative(value); Count = value; return this; }

    /// <summary>Sets <see cref="ItemsSource"/> (same as the property setter).</summary>
    public SkUiIndicatorView SetItemsSource(IEnumerable? value) { ItemsSource = value; return this; }

    /// <summary>Sets <see cref="IndicatorColor"/> (same as the property setter).</summary>
    public SkUiIndicatorView SetIndicatorColor(Color value) { ArgumentNullException.ThrowIfNull(value); IndicatorColor = value; return this; }

    /// <summary>Sets <see cref="SelectedIndicatorColor"/> (same as the property setter).</summary>
    public SkUiIndicatorView SetSelectedIndicatorColor(Color value) { ArgumentNullException.ThrowIfNull(value); SelectedIndicatorColor = value; return this; }

    /// <summary>Sets <see cref="IndicatorSize"/> (same as the property setter).</summary>
    public SkUiIndicatorView SetIndicatorSize(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); IndicatorSize = value; return this; }

    /// <summary>Sets <see cref="IndicatorsShape"/> (same as the property setter).</summary>
    public SkUiIndicatorView SetIndicatorsShape(IndicatorShape value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        IndicatorsShape = value;
        return this;
    }

    /// <summary>Sets <see cref="MaximumVisible"/> (same as the property setter).</summary>
    public SkUiIndicatorView SetMaximumVisible(int value) { ArgumentOutOfRangeException.ThrowIfNegative(value); MaximumVisible = value; return this; }

    /// <summary>Sets <see cref="HideSingle"/> (same as the property setter).</summary>
    public SkUiIndicatorView SetHideSingle(bool value) { HideSingle = value; return this; }

    /// <summary>Sets <see cref="IndicatorSpacing"/> (same as the property setter).</summary>
    public SkUiIndicatorView SetIndicatorSpacing(double value) { SkUiValidate.ThrowIfNotFinite(value, nameof(value)); IndicatorSpacing = value; return this; }

    /// <summary>Sets <see cref="Orientation"/> (same as the property setter).</summary>
    public SkUiIndicatorView SetOrientation(StackOrientation value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        Orientation = value;
        return this;
    }

    private void OnPositionChanged(int previous, int value)
    {
        _ = Carousel; // a collected carousel no longer drives the drawn position
        _model.Position = value;
        _model.PositionChanged(previous);
        InvalidatePaint();
        InvalidateSemantics();
    }

    /// <summary>A position within the items (kept while there are none: a position bound before the items).</summary>
    private object CoercePosition(int value) => Count > 0 && value >= Count ? Count - 1 : value;

    private void OnCountChanged(int value)
    {
        _model.Count = value;
        // Set, not CoerceValue: MAUI's CoerceValue runs the coercion without applying its result.
        if (value > 0 && Position >= value)
            Position = value - 1;
        InvalidateMeasureOverride();
        InvalidateSemantics();
    }

    private void OnLookChanged(Action<SkUiIndicatorModel> apply)
    {
        apply(_model);
        InvalidatePaint();
    }

    private void OnSizeChanged(Action<SkUiIndicatorModel> apply)
    {
        apply(_model);
        InvalidateMeasureOverride();
    }

    #endregion

    #region Carousel link

    // Weak: an indicator that outlives its carousel (kept by the app) keeps no carousel alive.
    private WeakReference<SkUiCarouselView>? _carousel;

    /// <summary>The carousel this view shows, or <c>null</c> (also once a carousel that never unlinked was collected).</summary>
    internal SkUiCarouselView? Carousel
    {
        get
        {
            if (_carousel is not { } link)
                return null;
            if (link.TryGetTarget(out var carousel))
                return carousel;
            Unlinked(); // collected without unlinking: the view is on its own again
            return null;
        }
    }

    /// <summary>A carousel links this view (or unlinks it, <c>null</c>): it then sets the count, the position and the drawn position.</summary>
    internal void Link(SkUiCarouselView? carousel)
    {
        if (carousel is null)
        {
            Unlinked();
            return;
        }
        _carousel = new WeakReference<SkUiCarouselView>(carousel);
        _model.IsDriven = true;
    }

    /// <summary>No carousel drives the view any more: it draws its own position, does not wrap, and counts its own items.</summary>
    private void Unlinked()
    {
        _carousel = null;
        _model.IsDriven = false;
        var changed = _model.Drive(null) | _model.Wraps;
        _model.Wraps = false;
        if (changed)
            InvalidatePaint();
        _counter.Recount(); // the items of its own source again
    }

    /// <summary>The linked carousel's scroll position (fractional, in items) and whether it loops.</summary>
    internal void Follow(double position, bool wraps)
    {
        var changed = _model.Wraps != wraps;
        _model.Wraps = wraps;
        if (_model.Drive(position) || changed)
            InvalidatePaint();
    }

    /// <summary>The selection drawn now (tests).</summary>
    internal double DisplayedPosition => _model.Displayed;

    #endregion

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => _model.Measure(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) => _model.Draw(canvas, (float)Width, (float)Height, IsRightToLeft, IsEnabled);

    /// <inheritdoc />
    protected override bool HandlesTap => !_model.IsHidden && _model.Count > 1;

    /// <inheritdoc />
    protected override void OnTapped(SkUiTappedEventArgs args)
    {
        RaiseTapped(args);
        ExecuteTappedCommand();
        var index = _model.HitTest(args.Position, (float)Width, (float)Height, IsRightToLeft);
        if (index >= 0)
            Position = index;
    }

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged()
    {
        base.OnEffectiveFlowDirectionChanged();
        InvalidatePaint(); // the row is mirrored
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        _model.PopulateSemantics(info);
    }

    /// <inheritdoc />
    protected override bool OnSemanticsAction(SkUiSemanticsActions action)
    {
        var target = _model.SemanticsTarget(action);
        if (target < 0)
            return base.OnSemanticsAction(action);
        Position = target;
        return true;
    }

    /// <inheritdoc />
    protected override bool OnSemanticsSetValue(double value)
    {
        var target = _model.SemanticsValue(value);
        if (target < 0 || target == _model.ClampedPosition)
            return false;
        Position = target;
        return true;
    }

    /// <inheritdoc />
    internal override bool TakesKeyboardFocus => true;

    /// <summary>Counts the items of <see cref="ItemsSource"/> into <see cref="Count"/>, listening to its changes without the source keeping the view alive.</summary>
    /// <remarks>While a carousel is linked, it sets the count; the source is counted again when it unlinks.</remarks>
    private sealed class ItemsCounter(SkUiIndicatorView owner)
    {
        private readonly WeakReference<SkUiIndicatorView> _owner = new(owner);
        private INotifyCollectionChanged? _observed;
        private IEnumerable? _source;
        private bool _counted; // the count is the source's: clearing the source clears it

        public void Observe(IEnumerable? source)
        {
            if (_observed is not null)
                _observed.CollectionChanged -= OnCollectionChanged;
            _source = source;
            _observed = source as INotifyCollectionChanged;
            if (_observed is not null)
                _observed.CollectionChanged += OnCollectionChanged;
            Recount();
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) => Recount();

        public void Recount()
        {
            if (!_owner.TryGetTarget(out var owner))
            {
                if (_observed is not null)
                    _observed.CollectionChanged -= OnCollectionChanged;
                _observed = null;
                return;
            }
            if (owner.Carousel is not null)
                return;
            if (_source is null)
            {
                // A source cleared: no items; a count the app set without a source stays.
                if (_counted)
                    owner.Count = 0;
                _counted = false;
                return;
            }
            _counted = true;
            owner.Count = _source switch
            {
                ICollection collection => collection.Count,
                _ => _source.Cast<object?>().Count()
            };
        }
    }
}
