using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MauiSkiaUi;

/// <summary>
/// Tabbed content inside a page: a strip of tab headers (drawn by the look on the segmented control's engine) and one pane per
/// tab, swiped sideways on the carousel's layout engine or chosen by a tap on a header. Panes are created when they first come
/// into view and kept after that, so their state survives. <see cref="SelectedIndex"/> is the one source of truth: taps,
/// swipes and keys all go through it. The indicator moves with the panes on the render thread. MAUI has no counterpart inside
/// a page; see <c>docs/controls/SkUiTabView.md</c>.
/// </summary>
[ContentProperty(nameof(Tabs))]
public partial class SkUiTabView : SkUiView, ISkUiFontElement, ISkUiKeyHandler, ISkUiScrollStepper, ISkUiTabStripOwner
{
    private ObservableCollection<SkUiTabItem>? _tabs;
    private readonly HashSet<SkUiTabItem> _attached = [];
    private PaneScroller? _paneScroller;
    private PaneHost? _panes;
    private SkUiScrollView? _stripScroller;
    private TabStrip? _strip;
    private SkUiTabItem? _selectedTab;
    private int _requested;
    private int _applied = -1;
    private bool _coercing;
    private bool _fromSwipe;
    private bool _requestingScroll; // starting a scroll stops a running one, which reports a rest: not a swipe's
    private int _scrollingTo = -1;   // the tab a scroll goes to: the selection stays there until it ends or a drag takes over

    /// <summary>Creates a tab view without tabs.</summary>
    public SkUiTabView()
    {
        HwAccelerated = true; // as a root: swipes and the indicator run on the render thread
        AttachChild(StripScroller);
        AttachChild(PaneScrollView);
    }

    #region Bindable properties

    /// <summary>Bindable property for <see cref="SelectedIndex"/> (two-way by default: taps and swipes set it).</summary>
    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(SkUiTabView), 0,
        BindingMode.TwoWay, validateValue: static (_, value) => value is >= -1,
        propertyChanged: (view, _, value) => ((SkUiTabView)view).OnSelectedIndexChanged((int)value));

    /// <summary>Bindable property for <see cref="TabStripPlacement"/>.</summary>
    public static readonly BindableProperty TabStripPlacementProperty = BindableProperty.Create(nameof(TabStripPlacement), typeof(SkUiTabStripPlacement),
        typeof(SkUiTabView), SkUiTabStripPlacement.Top, validateValue: static (_, value) => Enum.IsDefined((SkUiTabStripPlacement)value),
        propertyChanged: (view, _, _) => ((SkUiTabView)view).OnStripLayoutChanged());

    /// <summary>Bindable property for <see cref="TabWidthMode"/>.</summary>
    public static readonly BindableProperty TabWidthModeProperty = BindableProperty.Create(nameof(TabWidthMode), typeof(SkUiSegmentWidthMode), typeof(SkUiTabView),
        SkUiSegmentWidthMode.Equal, validateValue: static (_, value) => Enum.IsDefined((SkUiSegmentWidthMode)value),
        propertyChanged: (view, _, _) => ((SkUiTabView)view).OnStripLayoutChanged());

    /// <summary>Bindable property for <see cref="IndicatorStyle"/>.</summary>
    public static readonly BindableProperty IndicatorStyleProperty = BindableProperty.Create(nameof(IndicatorStyle), typeof(SkUiTabIndicatorStyle), typeof(SkUiTabView),
        SkUiTabIndicatorStyle.Underline, validateValue: static (_, value) => Enum.IsDefined((SkUiTabIndicatorStyle)value),
        propertyChanged: (view, _, _) => ((SkUiTabView)view).OnStripLayoutChanged());

    /// <summary>Bindable property for <see cref="IsSwipeEnabled"/>.</summary>
    public static readonly BindableProperty IsSwipeEnabledProperty = BindableProperty.Create(nameof(IsSwipeEnabled), typeof(bool), typeof(SkUiTabView), true,
        propertyChanged: (view, _, value) => ((SkUiTabView)view).OnSwipeEnabledChanged((bool)value));

    /// <summary>Bindable property for <see cref="HeaderTextColor"/>.</summary>
    public static readonly BindableProperty HeaderTextColorProperty = BindableProperty.Create(nameof(HeaderTextColor), typeof(Color), typeof(SkUiTabView), null,
        propertyChanged: (view, _, _) => ((SkUiTabView)view).OnStripColorChanged());

    /// <summary>Bindable property for <see cref="SelectedHeaderTextColor"/>.</summary>
    public static readonly BindableProperty SelectedHeaderTextColorProperty = BindableProperty.Create(nameof(SelectedHeaderTextColor), typeof(Color), typeof(SkUiTabView),
        null, propertyChanged: (view, _, _) => ((SkUiTabView)view).OnStripColorChanged());

    /// <summary>Bindable property for <see cref="IndicatorColor"/>.</summary>
    public static readonly BindableProperty IndicatorColorProperty = BindableProperty.Create(nameof(IndicatorColor), typeof(Color), typeof(SkUiTabView), null,
        propertyChanged: (view, _, _) => ((SkUiTabView)view).OnStripColorChanged());

    /// <summary>Bindable property for <see cref="StripBackgroundColor"/>.</summary>
    public static readonly BindableProperty StripBackgroundColorProperty = BindableProperty.Create(nameof(StripBackgroundColor), typeof(Color), typeof(SkUiTabView),
        null, propertyChanged: (view, _, _) => ((SkUiTabView)view).OnStripColorChanged());

    /// <summary>Bindable property for <see cref="FontFamily"/>.</summary>
    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(SkUiTabView), null,
        propertyChanged: (view, _, _) => ((SkUiTabView)view).OnStripLayoutChanged());

    /// <summary>Bindable property for <see cref="FontSize"/>.</summary>
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(double), typeof(SkUiTabView), 0d,
        defaultValueCreator: static _ => SkUiLook.Current.DefaultFontSize, validateValue: SkUiValidate.FinitePositive,
        propertyChanged: (view, _, _) => ((SkUiTabView)view).OnStripLayoutChanged());

    /// <summary>Bindable property for <see cref="FontAttributes"/>.</summary>
    public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(nameof(FontAttributes), typeof(FontAttributes), typeof(SkUiTabView),
        FontAttributes.None, propertyChanged: (view, _, _) => ((SkUiTabView)view).OnStripLayoutChanged());

    /// <summary>Bindable property for <see cref="FontAutoScalingEnabled"/>.</summary>
    public static readonly BindableProperty FontAutoScalingEnabledProperty = BindableProperty.Create(nameof(FontAutoScalingEnabled), typeof(bool), typeof(SkUiTabView),
        true, propertyChanged: (view, _, _) => ((SkUiTabView)view).OnStripLayoutChanged());

    #endregion

    #region Properties

    /// <summary>The tabs (the XAML content): each <see cref="SkUiTabItem"/> has a header and a pane. They take the tab view's binding context.</summary>
    public IList<SkUiTabItem> Tabs => _tabs ??= CreateTabs();

    /// <summary>
    /// The selected tab's index (default 0; -1 only without tabs), the one source of truth: a tap on a header, a swipe and a
    /// key set it, and setting it scrolls the panes there. Outside the tabs it is coerced to them (kept while there are none).
    /// </summary>
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    /// <summary>Whether the strip sits above the panes (<see cref="SkUiTabStripPlacement.Top"/>, default) or below them.</summary>
    public SkUiTabStripPlacement TabStripPlacement { get => (SkUiTabStripPlacement)GetValue(TabStripPlacementProperty); set => SetValue(TabStripPlacementProperty, value); }

    /// <summary>
    /// How the headers share the strip: <see cref="SkUiSegmentWidthMode.Equal"/> (default) splits it equally;
    /// <see cref="SkUiSegmentWidthMode.Content"/> sizes each to its content and makes the strip scroll when they do not fit.
    /// </summary>
    public SkUiSegmentWidthMode TabWidthMode { get => (SkUiSegmentWidthMode)GetValue(TabWidthModeProperty); set => SetValue(TabWidthModeProperty, value); }

    /// <summary>How the selected tab is marked: an underline (default), a pill behind its header, or none.</summary>
    public SkUiTabIndicatorStyle IndicatorStyle { get => (SkUiTabIndicatorStyle)GetValue(IndicatorStyleProperty); set => SetValue(IndicatorStyleProperty, value); }

    /// <summary>Whether drags swipe between the panes (default <c>true</c>); taps and keys always select.</summary>
    public bool IsSwipeEnabled { get => (bool)GetValue(IsSwipeEnabledProperty); set => SetValue(IsSwipeEnabledProperty, value); }

    /// <summary>An unselected header's text and icon color; <c>null</c> (default): the look's.</summary>
    public Color? HeaderTextColor { get => (Color?)GetValue(HeaderTextColorProperty); set => SetValue(HeaderTextColorProperty, value); }

    /// <summary>The selected header's text and icon color; <c>null</c> (default): the look's.</summary>
    public Color? SelectedHeaderTextColor { get => (Color?)GetValue(SelectedHeaderTextColorProperty); set => SetValue(SelectedHeaderTextColorProperty, value); }

    /// <summary>The indicator's color; <c>null</c> (default): the look's.</summary>
    public Color? IndicatorColor { get => (Color?)GetValue(IndicatorColorProperty); set => SetValue(IndicatorColorProperty, value); }

    /// <summary>The strip's background; <c>null</c> (default): the look's.</summary>
    public Color? StripBackgroundColor { get => (Color?)GetValue(StripBackgroundColorProperty); set => SetValue(StripBackgroundColorProperty, value); }

    /// <summary>The headers' font family.</summary>
    public string? FontFamily { get => (string?)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <summary>The headers' font size in DIPs (default: the look's).</summary>
    [TypeConverter(typeof(FontSizeConverter))]
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>Bold and / or italic headers.</summary>
    public FontAttributes FontAttributes { get => (FontAttributes)GetValue(FontAttributesProperty); set => SetValue(FontAttributesProperty, value); }

    /// <summary>Whether the headers follow the system text size (default <c>true</c>).</summary>
    public bool FontAutoScalingEnabled { get => (bool)GetValue(FontAutoScalingEnabledProperty); set => SetValue(FontAutoScalingEnabledProperty, value); }

    /// <summary>Raised after <see cref="SelectedIndex"/> changes, from the user or the app.</summary>
    public event EventHandler<SkUiSelectedIndexChangedEventArgs>? SelectionChanged;

    /// <summary>Adds <paramref name="tab"/> to <see cref="Tabs"/>.</summary>
    public SkUiTabView AddTab(SkUiTabItem tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        Tabs.Add(tab);
        return this;
    }

    /// <summary>Sets <see cref="SelectedIndex"/> (same as the property setter).</summary>
    public SkUiTabView SetSelectedIndex(int value) { ArgumentOutOfRangeException.ThrowIfLessThan(value, -1); SelectedIndex = value; return this; }

    /// <summary>Sets <see cref="TabStripPlacement"/> (same as the property setter).</summary>
    public SkUiTabView SetTabStripPlacement(SkUiTabStripPlacement value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        TabStripPlacement = value;
        return this;
    }

    /// <summary>Sets <see cref="TabWidthMode"/> (same as the property setter).</summary>
    public SkUiTabView SetTabWidthMode(SkUiSegmentWidthMode value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        TabWidthMode = value;
        return this;
    }

    /// <summary>Sets <see cref="IndicatorStyle"/> (same as the property setter).</summary>
    public SkUiTabView SetIndicatorStyle(SkUiTabIndicatorStyle value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        IndicatorStyle = value;
        return this;
    }

    /// <summary>Sets <see cref="IsSwipeEnabled"/> (same as the property setter).</summary>
    public SkUiTabView SetIsSwipeEnabled(bool value) { IsSwipeEnabled = value; return this; }

    /// <summary>Sets <see cref="HeaderTextColor"/> (same as the property setter).</summary>
    public SkUiTabView SetHeaderTextColor(Color? value) { HeaderTextColor = value; return this; }

    /// <summary>Sets <see cref="SelectedHeaderTextColor"/> (same as the property setter).</summary>
    public SkUiTabView SetSelectedHeaderTextColor(Color? value) { SelectedHeaderTextColor = value; return this; }

    /// <summary>Sets <see cref="IndicatorColor"/> (same as the property setter).</summary>
    public SkUiTabView SetIndicatorColor(Color? value) { IndicatorColor = value; return this; }

    /// <summary>Sets <see cref="StripBackgroundColor"/> (same as the property setter).</summary>
    public SkUiTabView SetStripBackgroundColor(Color? value) { StripBackgroundColor = value; return this; }

    /// <summary>Sets <see cref="FontFamily"/> (same as the property setter).</summary>
    public SkUiTabView SetFontFamily(string? value) { FontFamily = value; return this; }

    /// <summary>Sets <see cref="FontSize"/> (same as the property setter).</summary>
    public SkUiTabView SetFontSize(double value) { SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value)); FontSize = value; return this; }

    /// <summary>Sets <see cref="FontAttributes"/> (same as the property setter).</summary>
    public SkUiTabView SetFontAttributes(FontAttributes value) { FontAttributes = value; return this; }

    /// <summary>Sets <see cref="FontAutoScalingEnabled"/> (same as the property setter).</summary>
    public SkUiTabView SetFontAutoScalingEnabled(bool value) { FontAutoScalingEnabled = value; return this; }

    #endregion

    #region Parts

    private PaneScroller PaneScrollView
    {
        get
        {
            if (_paneScroller is null)
                CreateParts();
            return _paneScroller!;
        }
    }

    private PaneHost Panes
    {
        get
        {
            if (_panes is null)
                CreateParts();
            return _panes!;
        }
    }

    private SkUiScrollView StripScroller
    {
        get
        {
            if (_stripScroller is null)
                CreateParts();
            return _stripScroller!;
        }
    }

    private TabStrip Strip
    {
        get
        {
            if (_strip is null)
                CreateParts();
            return _strip!;
        }
    }

    private void CreateParts()
    {
        _strip = new TabStrip(this);
        _stripScroller = new SkUiScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = _strip
        };
        _panes = new PaneHost(this);
        _paneScroller = new PaneScroller(this)
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            SnapPointsType = SnapPointsType.MandatorySingle,
            Content = _panes
        };
        _paneScroller.RegisterScrollListener(_panes);
        ((ISkUiScrollHost)_paneScroller).Scroller.Stepper = this;
        ((ISkUiScrollHost)_paneScroller).Scroller.DraggingChanged += dragging =>
        {
            if (dragging)
                _scrollingTo = -1; // a drag takes over from a scroll to a tab
        };
        // The controller's event, not the view's PropertyChanged: that one allocates its arguments for every offset of a fling.
        ((ISkUiScrollHost)_paneScroller).Scroller.MovingChanged += _ => OnPanesMovingChanged();
    }

    /// <summary>The panes' scroll controller.</summary>
    private SkUiScrollController PaneController => ((ISkUiScrollHost)PaneScrollView).Scroller;

    /// <summary>The panes' scroller (tests).</summary>
    internal SkUiScrollView PaneScrollViewForTests => PaneScrollView;

    /// <summary>The strip's scroller (tests).</summary>
    internal SkUiScrollView StripScrollViewForTests => StripScroller;

    /// <summary>The header's rectangle in the strip (tests).</summary>
    internal Rect HeaderBounds(int index) => Strip.Model.HeaderBounds(index);

    /// <summary>Whether the pane of tab <paramref name="index"/> has been created (tests).</summary>
    internal bool IsPaneCreated(int index) => index >= 0 && index < Tabs.Count && Panes.IsCreated(Tabs[index]);

    /// <summary>The strip (tests: drawing, semantics, keys).</summary>
    internal SkUiView StripView => Strip;

    /// <summary>The indicator (tests).</summary>
    internal SkUiView IndicatorView => Strip.Indicator;

    #endregion

    #region Tabs

    private ObservableCollection<SkUiTabItem> CreateTabs()
    {
        var tabs = new ObservableCollection<SkUiTabItem>();
        tabs.CollectionChanged += OnTabsChanged;
        return tabs;
    }

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        var tabs = _tabs!;
        foreach (var tab in _attached.ToArray())
            if (!tabs.Contains(tab))
            {
                tab.PropertyChanged -= OnTabPropertyChanged;
                _attached.Remove(tab);
                if (ReferenceEquals(tab.Parent, this))
                    tab.Parent = null;
                Panes.Forget(tab);
            }
        foreach (var tab in tabs)
            if (_attached.Add(tab))
            {
                tab.Parent = this;
                SetInheritedBindingContext(tab, BindingContext);
                tab.PropertyChanged += OnTabPropertyChanged;
            }
        Rebuild();
    }

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is not SkUiTabItem tab)
            return;
        switch (args.PropertyName)
        {
            case nameof(SkUiTabItem.Content) or nameof(SkUiTabItem.ContentTemplate):
                Panes.ContentChanged(tab);
                break;
            case nameof(SkUiTabItem.IsEnabled):
                Strip.TabsChanged();
                InvalidateSemantics();
                break;
            case nameof(SkUiTabItem.Header) or nameof(SkUiTabItem.Icon) or nameof(SkUiTabItem.BadgeText):
                Strip.TabsChanged();
                PaneScrollView.SemanticsChanged();
                break;
        }
    }

    /// <summary>The tabs changed: the strip and panes follow; the selected tab stays selected where it moved, else the index asked for, within them.</summary>
    private void Rebuild()
    {
        var count = Tabs.Count;
        Strip.TabsChanged();
        Panes.SetCount(count);
        var found = _applied == _requested && _selectedTab is not null ? Tabs.IndexOf(_selectedTab) : -1;
        if (found >= 0)
            _requested = found;
        var selected = found >= 0 ? found : count == 0 ? SelectedIndex : Math.Clamp(_requested, 0, count - 1);
        if (selected != SelectedIndex)
        {
            _coercing = true;
            try { WriteBack(SelectedIndexProperty, selected); }
            finally { _coercing = false; }
        }
        else
        {
            Apply(selected, scroll: true, animate: false);
        }
    }

    #region Strip owner

    int ISkUiTabStripOwner.TabCount => _tabs?.Count ?? 0;

    string? ISkUiTabStripOwner.HeaderOf(int index) => Tabs[index].Header;

    ImageSource? ISkUiTabStripOwner.IconOf(int index) => Tabs[index].Icon;

    string? ISkUiTabStripOwner.BadgeOf(int index) => Tabs[index].BadgeText;

    string? ISkUiTabStripOwner.AutomationIdOf(int index) => Tabs[index].AutomationId;

    bool ISkUiTabStripOwner.IsTabItemEnabled(int index) => Tabs[index].IsEnabled;

    bool ISkUiTabStripOwner.IsRightToLeft => IsRightToLeft;

    // The current look's default, not the first one read (the store keeps that).
    double ISkUiTabStripOwner.FontSize => IsSet(FontSizeProperty) ? FontSize : SkUiLook.Current.DefaultFontSize;

    bool ISkUiTabStripOwner.SelectByUser(int index) => SelectByUser(index);

    #endregion

    #endregion

    #region Selection

    private void OnSelectedIndexChanged(int value)
    {
        if (!_coercing)
            _requested = value;
        var count = Tabs.Count;
        if (count > 0 && (value < 0 || value >= count))
        {
            // Coerced before the write: a one-way binding is not cut.
            _coercing = true;
            try { WriteBack(SelectedIndexProperty, Math.Clamp(value, 0, count - 1)); }
            finally { _coercing = false; }
            return;
        }
        Apply(value, scroll: !_fromSwipe, animate: true);
    }

    /// <summary>
    /// <paramref name="value"/> is the selected tab now: the strip shows it, the panes scroll there (unless a swipe brought
    /// them, <paramref name="scroll"/>), and <see cref="SelectionChanged"/> is raised when it changed.
    /// </summary>
    private void Apply(int value, bool scroll, bool animate)
    {
        var previous = _applied;
        _applied = value;
        var count = Tabs.Count;
        _selectedTab = value >= 0 && value < count ? Tabs[value] : null;
        Strip.SelectionChanged(value);
        PaneScrollView.SemanticsChanged();
        if (scroll && value >= 0 && value < count)
        {
            _scrollingTo = value;
            _requestingScroll = true;
            try
            {
                if (Panes.ScrollToIndex(value, animate && IsShown && !SkUiMotion.IsMotionReduced).IsCompleted)
                    _scrollingTo = -1;
            }
            finally
            {
                _requestingScroll = false;
            }
        }
        if (previous != value)
            SelectionChanged?.Invoke(this, new SkUiSelectedIndexChangedEventArgs(previous, value));
        InvalidateSemantics();
    }

    /// <summary>The user chose tab <paramref name="index"/> (a tap, a key, a screen reader): it is selected; returns whether it changed.</summary>
    internal bool SelectByUser(int index)
    {
        if (!Strip.Model.IsTabEnabled(index) || index == SelectedIndex)
            return false;
        WriteBack(SelectedIndexProperty, index);
        return true;
    }

    /// <summary>The panes came to rest after a swipe (or a scroll): the tab at the snap point is selected.</summary>
    private void OnPanesMovingChanged()
    {
        if (_requestingScroll || PaneController.IsMoving || !Panes.HasGeometry || Tabs.Count == 0)
            return;
        if (_scrollingTo >= 0)
        {
            // A scroll to a tab ended: the selection is already that tab.
            _scrollingTo = -1;
            return;
        }
        var current = Panes.CurrentIndex(_applied);
        if (current == SelectedIndex || current < 0)
            return;
        _fromSwipe = true;
        try { WriteBack(SelectedIndexProperty, current); }
        finally { _fromSwipe = false; }
    }

    // Screen readers page the panes tab by tab, never to a place between two. Arrow keys inside a pane do not move the panes
    // (they are the strip's), and neither do Home / End.

    bool ISkUiScrollStepper.CanStep(bool forward) => Strip.Model.PageTarget(forward) >= 0;

    bool ISkUiScrollStepper.Step(bool forward, bool page) => page && Strip.Model.PageTarget(forward) is var target and >= 0 && SelectByUser(target);

    bool ISkUiScrollStepper.StepToEdge(bool end) => false;

    /// <summary>Ctrl+Tab / Ctrl+Shift+Tab from anywhere inside the tab view: the next / previous enabled tab, around the ends.</summary>
    bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key) => Strip.Model.OnTabViewKeyDown(key);

    #endregion

    #region Layout

    private void OnStripLayoutChanged()
    {
        InvalidateRender(Rendering.SkUiRenderDirty.Children); // their order
        Strip.TabsChanged();
        InvalidateMeasureOverride();
    }

    private void OnStripColorChanged() => Strip.ColorsChanged();

    private void OnSwipeEnabledChanged(bool value)
    {
        PaneController.IsUserScrollEnabled = value;
        if (!value)
            PaneScrollView.CancelGestures();
    }

    /// <summary>The strip's height: the look's, taller with icons.</summary>
    private double StripHeight => Strip.Model.DesiredHeight;

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren
    {
        get
        {
            // In reading order (they do not overlap): screen readers and Tab meet the strip first when it is on top.
            var top = TabStripPlacement == SkUiTabStripPlacement.Top;
            yield return top ? StripScroller : PaneScrollView;
            yield return top ? PaneScrollView : StripScroller;
        }
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var strip = StripHeight;
        Strip.ViewportWidth = double.IsFinite(widthConstraint) ? widthConstraint : double.NaN;
        var stripSize = ((IView)StripScroller).Measure(widthConstraint, strip);
        Panes.SetViewport(widthConstraint);
        var panes = ((IView)PaneScrollView).Measure(widthConstraint, Math.Max(0, heightConstraint - strip));
        var width = double.IsFinite(widthConstraint) ? widthConstraint : Math.Max(stripSize.Width, Math.Min(panes.Width, Panes.ViewportLength));
        return new Size(width, strip + panes.Height);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var strip = Math.Min(StripHeight, size.Height);
        var top = TabStripPlacement == SkUiTabStripPlacement.Top;
        var stripBounds = new Rect(0, top ? 0 : size.Height - strip, size.Width, strip);
        var paneBounds = new Rect(0, top ? strip : 0, size.Width, Math.Max(0, size.Height - strip));
        Strip.ViewportWidth = size.Width;
        ((IView)StripScroller).Measure(stripBounds.Width, stripBounds.Height);
        ((IView)StripScroller).Arrange(stripBounds);
        Strip.AfterArrange();
        Panes.SetViewport(size.Width);
        ((IView)PaneScrollView).Measure(paneBounds.Width, paneBounds.Height);
        ((IView)PaneScrollView).Arrange(paneBounds);
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        foreach (var tab in _attached)
            SetInheritedBindingContext(tab, BindingContext);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled))
        {
            Strip.TabsChanged();
            InvalidateSemantics();
        }
    }

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged()
    {
        base.OnEffectiveFlowDirectionChanged();
        Strip.TabsChanged();
        Panes.KeepPlace();
    }

    /// <inheritdoc />
    private protected override void ForgetLookDefaults()
    {
        base.ForgetLookDefaults();
        Strip.TabsChanged();
    }

    #endregion
}
