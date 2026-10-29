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
/// <para>
/// The row is centered in the view and laid out by the control, so taps hit what is drawn; a tap anywhere in the view selects
/// the nearest indicator (give the view a height or padding for a larger touch target). Screen readers and the keyboard
/// (arrow keys, Home, End) adjust the position.
/// </para>
/// <para>
/// MAUI's <see cref="IndicatorTemplate"/> makes a drawn view per indicator instead, in a stack (<see cref="IndicatorLayout"/>),
/// as MAUI's model: each view's binding context is its item (a linked carousel's item; its index without <see cref="ItemsSource"/>), its background
/// the indicator color or, selected, the selected color, and the selected one is in the <c>Selected</c> visual state.
/// Drawing the indicators through the look (<see cref="SkUiLook.IndicatorPainter"/>, or a look that overrides
/// <c>DrawIndicatorsCore</c>) stays the cheaper path: no views, and the selection moves with the carousel's scrolling.
/// </para>
/// </remarks>
[ContentProperty(nameof(IndicatorLayout))]
public class SkUiIndicatorView : SkUiView, ISkUiKeyHandler
{
    /// <summary>Bindable property for <see cref="CascadeInputTransparent"/>.</summary>
    public static readonly BindableProperty CascadeInputTransparentProperty = CascadeInputTransparentShared;

    /// <summary>
    /// Whether <see cref="VisualElement.InputTransparent"/> covers the children too (<c>true</c>, the default, as in MAUI).
    /// <c>false</c>: pointers pass through the indicator view itself, and its children still take them.
    /// </summary>
    public bool CascadeInputTransparent { get => (bool)GetValue(CascadeInputTransparentProperty); set => SetValue(CascadeInputTransparentProperty, value); }

    /// <summary>Sets <see cref="CascadeInputTransparent"/>.</summary>
    public SkUiIndicatorView SetCascadeInputTransparent(bool value) { CascadeInputTransparent = value; return this; }

    /// <summary>Bindable property for <see cref="IsClippedToBounds"/>: MAUI's name for <see cref="SkUiView.ClipToBoundsProperty"/>.</summary>
    public static readonly BindableProperty IsClippedToBoundsProperty = ClipToBoundsProperty;

    /// <summary>MAUI's name for <see cref="SkUiView.ClipToBounds"/>: the same property.</summary>
    public bool IsClippedToBounds { get => ClipToBounds; set => ClipToBounds = value; }

    private SkUiIndicatorModel? _model;
    /// <summary>Created on first use: property callbacks can run before the constructor (see <see cref="SkUiView.StyledOr{T}"/>).</summary>
    private SkUiIndicatorModel Model => _model ??= new SkUiIndicatorModel(this)
    {
        IndicatorColor = IndicatorColor,
        SelectedColor = SelectedIndicatorColor
    };
    private ItemsCounter? _counter;
    private ItemsCounter Counter => _counter ??= new ItemsCounter(this);

    /// <summary>Creates an indicator view without items.</summary>
    public SkUiIndicatorView()
    {
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
        propertyChanged: (view, _, value) =>
        {
            var indicator = (SkUiIndicatorView)view;
            // The count follows the new items first; the template's views are then made and bound once.
            indicator._countingItems = true;
            try
            {
                indicator.Counter.Observe((IEnumerable?)value);
            }
            finally
            {
                indicator._countingItems = false;
            }
            indicator.UpdateIndicatorViews(rebuild: true);
        });

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

    /// <summary>Bindable property for <see cref="Padding"/>.</summary>
    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(nameof(Padding), typeof(Thickness), typeof(SkUiIndicatorView), default(Thickness),
        validateValue: SkUiValidate.OptionalThickness,
        propertyChanged: (view, _, value) => ((SkUiIndicatorView)view).OnSizeChanged(model => model.Padding = (Thickness)value));

    /// <summary>Bindable property for <see cref="Orientation"/>.</summary>
    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(nameof(Orientation), typeof(StackOrientation), typeof(SkUiIndicatorView), StackOrientation.Horizontal,
        validateValue: (_, value) => Enum.IsDefined((StackOrientation)value),
        propertyChanged: (view, _, value) => ((SkUiIndicatorView)view).OnSizeChanged(model => model.Orientation = (StackOrientation)value));

    /// <summary>Bindable property for <see cref="NavigationButtonVisibility"/>.</summary>
    public static readonly BindableProperty NavigationButtonVisibilityProperty = BindableProperty.Create(nameof(NavigationButtonVisibility),
        typeof(SkUiIndicatorButtonVisibility), typeof(SkUiIndicatorView), SkUiIndicatorButtonVisibility.Collapsed,
        validateValue: (_, value) => Enum.IsDefined((SkUiIndicatorButtonVisibility)value),
        propertyChanged: (view, _, value) => ((SkUiIndicatorView)view).OnSizeChanged(model => model.NavigationButtons = (SkUiIndicatorButtonVisibility)value));

    /// <summary>Space around the indicators, as MAUI's: added to the measured size; the row is centered inside it.</summary>
    public Thickness Padding { get => (Thickness)GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    /// <summary>Sets <see cref="Padding"/>.</summary>
    public SkUiIndicatorView SetPadding(Thickness value)
    {
        SkUiValidate.ThrowIfNotThickness(value, nameof(value));
        Padding = value;
        return this;
    }

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

    /// <summary>
    /// Previous / next buttons at the row's ends, as Fluent's pips pager has (a SkiaUi extension; default
    /// <see cref="SkUiIndicatorButtonVisibility.Collapsed"/>): a tap moves <see cref="Position"/> by one. They hide on the
    /// first and last item unless the selection wraps (a looping carousel), and the look draws them
    /// (<see cref="SkUiLook.IndicatorNavigationButtonSize"/>). Arrow keys move the position too, with or without them.
    /// </summary>
    public SkUiIndicatorButtonVisibility NavigationButtonVisibility
    {
        get => (SkUiIndicatorButtonVisibility)GetValue(NavigationButtonVisibilityProperty);
        set => SetValue(NavigationButtonVisibilityProperty, value);
    }

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

    /// <summary>Sets <see cref="NavigationButtonVisibility"/> (same as the property setter).</summary>
    public SkUiIndicatorView SetNavigationButtonVisibility(SkUiIndicatorButtonVisibility value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        NavigationButtonVisibility = value;
        return this;
    }

    private void OnPositionChanged(int previous, int value)
    {
        _ = Carousel; // a collected carousel no longer drives the drawn position
        Model.Position = value;
        Model.PositionChanged(previous);
        UpdateIndicatorStyles();
        InvalidatePaint();
        InvalidateSemantics();
    }

    /// <summary>A position within the items (kept while there are none: a position bound before the items).</summary>
    private object CoercePosition(int value) => Count > 0 && value >= Count ? Count - 1 : value;

    private void OnCountChanged(int value)
    {
        Model.Count = value;
        // Set, not CoerceValue: MAUI's CoerceValue runs the coercion without applying its result.
        if (value > 0 && Position >= value)
            Position = value - 1;
        UpdateIndicatorViews();
        InvalidateMeasureOverride();
        InvalidateSemantics();
    }

    private void OnLookChanged(Action<SkUiIndicatorModel> apply)
    {
        apply(Model);
        UpdateIndicatorStyles();
        InvalidatePaint();
    }

    private void OnSizeChanged(Action<SkUiIndicatorModel> apply)
    {
        apply(Model);
        // The template's stack runs along the orientation.
        if (_templateStack is { } stack && stack is SkUiVerticalStackLayout != (Orientation == StackOrientation.Vertical))
        {
            SetIndicatorLayout(CreateTemplateStack());
            stack.Children.Clear();
        }
        else
            UpdateIndicatorViews();
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
        Model.IsDriven = true;
    }

    /// <summary>No carousel drives the view any more: it draws its own position, does not wrap, and counts its own items.</summary>
    private void Unlinked()
    {
        _carousel = null;
        Model.IsDriven = false;
        var changed = Model.Drive(null) | Model.Wraps;
        Model.Wraps = false;
        if (changed)
            InvalidatePaint();
        Counter.Recount(); // the items of its own source again
        UpdateIndicatorViews(); // the template's views bind to them, not to the carousel's
    }

    /// <summary>The linked carousel's items changed (or it just linked): the count follows and the template's views bind to its items.</summary>
    internal void CarouselItemsChanged(int count)
    {
        if (Count != count)
            Count = count; // binds the views too
        else
            UpdateIndicatorViews(); // items replaced at the same count
    }

    /// <summary>The linked carousel's scroll position (fractional, in items) and whether it loops.</summary>
    internal void Follow(double position, bool wraps)
    {
        var changed = Model.Wraps != wraps;
        Model.Wraps = wraps;
        if (Model.Drive(position) || changed)
            InvalidatePaint();
    }

    /// <summary>The selection drawn now (tests).</summary>
    internal double DisplayedPosition => Model.Displayed;

    #endregion

    #region Indicator template

    /// <summary>MAUI's spacing between template views.</summary>
    private const double TemplateSpacing = 4;

    /// <summary>Bindable property for <see cref="IndicatorTemplate"/>.</summary>
    public static readonly BindableProperty IndicatorTemplateProperty = BindableProperty.Create(nameof(IndicatorTemplate), typeof(DataTemplate), typeof(SkUiIndicatorView), null,
        propertyChanged: (view, _, value) => ((SkUiIndicatorView)view).OnIndicatorTemplateChanged((DataTemplate?)value));

    private static readonly BindableProperty IndicatorLayoutProperty = BindableProperty.Create(nameof(IndicatorLayout), typeof(IBindableLayout), typeof(SkUiIndicatorView), null,
        validateValue: (_, value) => value is null or SkUiLayout, // drawn layouts only (MAUI ignores others with a warning)
        propertyChanged: (view, old, value) => ((SkUiIndicatorView)view).OnIndicatorLayoutChanged((SkUiLayout?)old, (SkUiLayout?)value));

    /// <summary>
    /// A drawn view per indicator (MAUI's <c>IndicatorTemplate</c>; a <see cref="DataTemplateSelector"/> chooses by item): set,
    /// the view shows them in a stack of its own (<see cref="IndicatorLayout"/>, centered, 4 DIPs apart, along
    /// <see cref="Orientation"/>) instead of drawing the indicators; <see cref="MaximumVisible"/>, <see cref="HideSingle"/>,
    /// <see cref="IndicatorColor"/> and <see cref="SelectedIndicatorColor"/> (the views' backgrounds) apply, and a tap on a
    /// view selects it. The templates must create drawn (SkUi*) views.
    /// </summary>
    public DataTemplate? IndicatorTemplate { get => (DataTemplate?)GetValue(IndicatorTemplateProperty); set => SetValue(IndicatorTemplateProperty, value); }

    /// <summary>
    /// The layout shown instead of the drawn indicators (MAUI's <c>IndicatorLayout</c>, the XAML content): the stack of
    /// <see cref="IndicatorTemplate"/>'s views, set with the template, or a drawn layout of the app's (shown as it is).
    /// </summary>
    public IBindableLayout? IndicatorLayout { get => (IBindableLayout?)GetValue(IndicatorLayoutProperty); set => SetValue(IndicatorLayoutProperty, value); }

    /// <summary>Sets <see cref="IndicatorTemplate"/> (same as the property setter).</summary>
    public SkUiIndicatorView SetIndicatorTemplate(DataTemplate? value) { IndicatorTemplate = value; return this; }

    /// <summary>The layout in use, the template's stack or the app's (<c>null</c>: the indicators are drawn).</summary>
    private SkUiLayout? _layout;
    /// <summary>The stack this view made for <see cref="IndicatorTemplate"/> (<c>null</c> while it shows the app's layout).</summary>
    private SkUiLayout? _templateStack;
    private bool _updatingLayout;
    private bool _countingItems; // ItemsSource changed: the views wait for the new count

    private void OnIndicatorTemplateChanged(DataTemplate? template)
    {
        if (template is null)
        {
            if (_templateStack is not null && ReferenceEquals(_layout, _templateStack))
                ClearValue(IndicatorLayoutProperty);
            return;
        }
        if (_templateStack is null)
            SetIndicatorLayout(CreateTemplateStack());
        else
            UpdateIndicatorViews(rebuild: true);
    }

    private SkUiLayout CreateTemplateStack()
    {
        SkUiLayout stack = Orientation == StackOrientation.Vertical
            ? new SkUiVerticalStackLayout { Spacing = TemplateSpacing }
            : new SkUiHorizontalStackLayout { Spacing = TemplateSpacing };
        stack.HorizontalOptions = LayoutOptions.Center;
        stack.VerticalOptions = LayoutOptions.Center;
        _templateStack = stack;
        return stack;
    }

    private void SetIndicatorLayout(SkUiLayout layout)
    {
        _updatingLayout = true;
        try { IndicatorLayout = layout; }
        finally { _updatingLayout = false; }
    }

    private void OnIndicatorLayoutChanged(SkUiLayout? previous, SkUiLayout? value)
    {
        if (previous is not null)
        {
            DetachChild(previous);
            if (ReferenceEquals(previous, _templateStack))
            {
                previous.Children.Clear();
                _templateStack = null;
            }
        }
        // The app's own layout replaces the template's stack (MAUI shows whichever is the content).
        if (!_updatingLayout && !ReferenceEquals(value, _templateStack))
            _templateStack = null;
        _layout = value;
        if (value is not null)
        {
            ValidateChild(value);
            AttachChild(value);
        }
        UpdateIndicatorViews(rebuild: true);
        InvalidatePaint();
        InvalidateSemantics();
    }

    /// <summary>
    /// The template's views follow the indicators: one per visible indicator, bound to its item, colored and marked selected.
    /// <paramref name="rebuild"/>: the template or the items changed, so the views are created again.
    /// </summary>
    private void UpdateIndicatorViews(bool rebuild = false)
    {
        if (_countingItems || _templateStack is not { } stack || IndicatorTemplate is not { } template)
            return;
        // First: reading the link of a collected carousel unlinks the view, which counts its own items again.
        var carousel = Carousel;
        var count = Math.Min(Count, MaximumVisible);
        var children = stack.Children;
        if (rebuild)
            children.Clear();
        // A linked carousel's items (MAUI binds the indicator's source to the carousel's), else its own; a source that is not
        // a list is enumerated once.
        var items = carousel is not null ? carousel.Items : ItemsSource as IList ?? ItemsSource?.Cast<object?>().Take(count).ToList();
        while (children.Count > count)
            children.RemoveAt(children.Count - 1);
        // The template each view was made from: a selector may pick another one for the item now at its index.
        var selector = template as DataTemplateSelector;
        if (selector is null || _viewTemplates.Count > children.Count)
            _viewTemplates.Clear();
        for (var index = 0; index < count; index++)
        {
            object? item = items is null ? index : index < items.Count ? items[index] : null;
            var selected = selector is not null ? selector.SelectTemplate(item, this) : template;
            if (index >= children.Count)
                children.Add(CreateIndicatorView(selected));
            else if (selector is not null && (index >= _viewTemplates.Count || !ReferenceEquals(_viewTemplates[index], selected)))
                children[index] = CreateIndicatorView(selected);
            if (selector is not null)
            {
                if (index < _viewTemplates.Count)
                    _viewTemplates[index] = selected;
                else
                    _viewTemplates.Add(selected);
            }
            ((BindableObject)children[index]).BindingContext = item;
        }
        UpdateIndicatorStyles();
    }

    /// <summary>For a <see cref="DataTemplateSelector"/>: the template each of the stack's views was made from, by index.</summary>
    private readonly List<DataTemplate?> _viewTemplates = [];

    private static ISkUiView CreateIndicatorView(DataTemplate? template) =>
        template?.CreateContent() as ISkUiView
        ?? throw new InvalidOperationException($"{nameof(IndicatorTemplate)} of {nameof(SkUiIndicatorView)} must create drawn (SkUi*) views; put native views inside an SkUiMauiContentView.");

    /// <summary>The views' backgrounds and the <c>Selected</c> visual state, as MAUI marks them; hidden with a single indicator per <see cref="HideSingle"/>.</summary>
    private void UpdateIndicatorStyles()
    {
        if (_templateStack is not { } stack)
            return;
        var selectedIndex = Math.Min(Position, MaximumVisible - 1);
        for (var index = 0; index < stack.Children.Count; index++)
            if (stack.Children[index] is SkUiView view)
            {
                var selected = index == selectedIndex;
                view.BackgroundColor = selected ? SelectedIndicatorColor : IndicatorColor;
                view.IsSelectedItem = selected;
            }
        stack.IsVisible = Count > 1 || !HideSingle;
    }

    /// <summary>The template view a tap at <paramref name="position"/> hit, or -1.</summary>
    private int TemplateViewAt(Point position)
    {
        if (_templateStack is not { } stack)
            return -1;
        var inStack = new Point(position.X - stack.Frame.X, position.Y - stack.Frame.Y);
        for (var index = 0; index < stack.Children.Count; index++)
            if (stack.Children[index] is View { Frame: var frame } && frame.Contains(inStack))
                return index;
        return -1;
    }

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren
    {
        get
        {
            if (_layout is not null)
                yield return _layout;
        }
    }

    #endregion

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        if (_layout is not IView layout)
            return Model.Measure(widthConstraint, heightConstraint);
        var padding = Padding;
        var size = layout.Measure(Math.Max(0, widthConstraint - padding.HorizontalThickness), Math.Max(0, heightConstraint - padding.VerticalThickness));
        return new Size(size.Width + padding.HorizontalThickness, size.Height + padding.VerticalThickness);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        if (_layout is not IView layout)
            return;
        var padding = Padding;
        layout.Arrange(new Rect(padding.Left, padding.Top, Math.Max(0, size.Width - padding.HorizontalThickness), Math.Max(0, size.Height - padding.VerticalThickness)));
    }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        if (_layout is null)
            Model.Draw(canvas, (float)Width, (float)Height, IsRightToLeft, IsEnabled);
    }

    /// <inheritdoc />
    protected override bool HandlesTap => _layout is null ? !Model.IsHidden && Model.Count > 1 : _templateStack is not null && Count > 1;

    /// <inheritdoc />
    protected override void OnTapped(SkUiTappedEventArgs args)
    {
        RaiseTapped(args);
        ExecuteTappedCommand();
        if (_layout is null && Model.HitTestButton(args.Position, (float)Width, (float)Height, IsRightToLeft) is not 0 and var step)
        {
            if (Model.Step(step) is var target and >= 0)
                Position = target;
            return;
        }
        var index = _layout is null ? Model.HitTest(args.Position, (float)Width, (float)Height, IsRightToLeft) : TemplateViewAt(args.Position);
        if (index >= 0)
            Position = index;
    }

    /// <inheritdoc />
    internal override bool TracksHover => _layout is null;

    /// <inheritdoc />
    internal override void OnHovered(Point? position)
    {
        if (Model.Hover(position, (float)Width, (float)Height, IsRightToLeft))
            InvalidatePaint();
    }

    bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key)
    {
        if (!IsEnabled || Model.KeyTarget(key, IsRightToLeft) is not (var target and >= 0) || target == Model.ClampedPosition)
            return false;
        Position = target;
        return true;
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
        Model.PopulateSemantics(info);
    }

    /// <inheritdoc />
    protected override bool OnSemanticsAction(SkUiSemanticsActions action)
    {
        var target = Model.SemanticsTarget(action);
        if (target < 0)
            return base.OnSemanticsAction(action);
        Position = target;
        return true;
    }

    /// <inheritdoc />
    protected override bool OnSemanticsSetValue(double value)
    {
        var target = Model.SemanticsValue(value);
        if (target < 0 || target == Model.ClampedPosition)
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
            owner.UpdateIndicatorViews(); // items replaced at the same count bind again
        }
    }
}
