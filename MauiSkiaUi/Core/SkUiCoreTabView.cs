using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core tab view (analogue of <see cref="SkUiTabView"/>, on the same strip and the carousel's layout engine): a strip of headers
/// over panes swiped sideways. Panes are created when they first come into view and kept after that. <see cref="SelectedIndex"/>
/// is the one source of truth for taps, swipes and keys, and the indicator moves with the panes on the render thread. Fluent
/// <c>Set*</c> setters and <see cref="INotifyPropertyChanged"/>.
/// </summary>
public partial class SkUiCoreTabView : SkUiCoreNode, ISkUiFontElement, ISkUiKeyHandler, ISkUiScrollStepper, ISkUiTabStripOwner
{
    private readonly ObservableCollection<SkUiCoreTabItem> _tabs = [];
    private readonly HashSet<SkUiCoreTabItem> _attached = [];
    private readonly TabStrip _strip;
    private readonly SkUiCoreScrollView _stripScroller;
    private readonly PaneHost _panes;
    private readonly PaneScroller _paneScroller;
    private readonly SkUiScrollController _controller;
    private SkUiCoreTabItem? _selectedTab;
    private int _selectedIndex;
    private int _requested;
    private int _applied = -1;
    private int _announced = -1; // the index the last event told of
    private bool _requestingScroll; // starting a scroll stops a running one, which reports a rest: not a swipe's
    private int _scrollingTo = -1;  // the tab a scroll goes to: the selection stays there until it ends or a drag takes over
    private SkUiTabStripPlacement _placement;
    private SkUiSegmentWidthMode _widthMode;
    private SkUiTabIndicatorStyle _indicatorStyle;
    private bool _isSwipeEnabled = true;
    private Color? _headerTextColor, _selectedHeaderTextColor, _indicatorColor, _stripBackgroundColor;
    private string? _fontFamily;
    private double _fontSize = double.NaN;
    private FontAttributes _fontAttributes;
    private bool _fontAutoScaling = true;

    /// <summary>Creates a tab view without tabs.</summary>
    public SkUiCoreTabView()
    {
        _strip = new TabStrip(this);
        _stripScroller = NewScroller(new SkUiCoreScrollView(), _strip);
        _panes = new PaneHost(this);
        _paneScroller = NewScroller(new PaneScroller(this), _panes);
        _paneScroller.SetSnapPointsType(SnapPointsType.MandatorySingle);
        _controller = ((ISkUiScrollHost)_paneScroller).Scroller;
        _controller.Stepper = this;
        _controller.DraggingChanged += dragging =>
        {
            if (dragging)
                _scrollingTo = -1; // a drag takes over from a scroll to a tab
        };
        _controller.MovingChanged += _ => OnPanesMovingChanged();
        _tabs.CollectionChanged += OnTabsChanged;

        T NewScroller<T>(T scroller, SkUiCoreNode content) where T : SkUiCoreScrollView
        {
            scroller.SetOrientation(ScrollOrientation.Horizontal);
            scroller.SetHorizontalScrollBarVisibility(ScrollBarVisibility.Never);
            scroller.SetVerticalScrollBarVisibility(ScrollBarVisibility.Never);
            scroller.SetContent(content);
            scroller.AttachTo(this);
            return scroller;
        }
    }

    #region Properties

    /// <summary>The tabs: each <see cref="SkUiCoreTabItem"/> has a header and a pane.</summary>
    public IList<SkUiCoreTabItem> Tabs => _tabs;

    /// <summary>The selected tab's index (default 0); coerced to the tabs (kept as set while there are none). See <see cref="SkUiTabView.SelectedIndex"/>.</summary>
    public int SelectedIndex { get => _selectedIndex; set => SetSelectedIndex(value); }

    /// <summary>Whether the strip sits above the panes (<see cref="SkUiTabStripPlacement.Top"/>, default) or below them.</summary>
    public SkUiTabStripPlacement TabStripPlacement { get => _placement; set => SetTabStripPlacement(value); }

    /// <summary>How the headers share the strip: equally (default), or by their content in a strip that scrolls when they do not fit.</summary>
    public SkUiSegmentWidthMode TabWidthMode { get => _widthMode; set => SetTabWidthMode(value); }

    /// <summary>How the selected tab is marked: an underline (default), a pill behind its header, or none.</summary>
    public SkUiTabIndicatorStyle IndicatorStyle { get => _indicatorStyle; set => SetIndicatorStyle(value); }

    /// <summary>Whether drags swipe between the panes (default <c>true</c>); taps and keys always select.</summary>
    public bool IsSwipeEnabled { get => _isSwipeEnabled; set => SetIsSwipeEnabled(value); }

    /// <summary>An unselected header's text color; <c>null</c> (default): the look's.</summary>
    public Color? HeaderTextColor { get => _headerTextColor; set => SetHeaderTextColor(value); }

    /// <summary>The selected header's text color; <c>null</c> (default): the look's.</summary>
    public Color? SelectedHeaderTextColor { get => _selectedHeaderTextColor; set => SetSelectedHeaderTextColor(value); }

    /// <summary>The indicator's color; <c>null</c> (default): the look's.</summary>
    public Color? IndicatorColor { get => _indicatorColor; set => SetIndicatorColor(value); }

    /// <summary>The strip's background; <c>null</c> (default): the look's.</summary>
    public Color? StripBackgroundColor { get => _stripBackgroundColor; set => SetStripBackgroundColor(value); }

    /// <summary>The headers' font family.</summary>
    public string? FontFamily { get => _fontFamily; set => SetFontFamily(value); }

    /// <summary>The headers' font size in DIPs (default: the look's).</summary>
    public double FontSize { get => SkUiLook.FontSizeOrDefault(_fontSize); set => SetFontSize(value); }

    /// <summary>Bold and / or italic headers.</summary>
    public FontAttributes FontAttributes { get => _fontAttributes; set => SetFontAttributes(value); }

    /// <summary>Whether the headers follow the system text size (default <c>true</c>).</summary>
    public bool FontAutoScalingEnabled { get => _fontAutoScaling; set => SetFontAutoScalingEnabled(value); }

    /// <summary>Raised after <see cref="SelectedIndex"/> changes, from the user or code.</summary>
    public event EventHandler<SkUiSelectedIndexChangedEventArgs>? SelectionChanged;

    /// <summary>Adds <paramref name="tab"/> to <see cref="Tabs"/>.</summary>
    public SkUiCoreTabView AddTab(SkUiCoreTabItem tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        _tabs.Add(tab);
        return this;
    }

    /// <summary>Sets <see cref="SelectedIndex"/>; beyond the last tab it becomes the last.</summary>
    public SkUiCoreTabView SetSelectedIndex(int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, -1);
        if (_tabs.Count > 0)
            value = Math.Clamp(value, 0, _tabs.Count - 1);
        _requested = value;
        Apply(value, scroll: true, animate: true);
        return this;
    }

    /// <summary>Sets <see cref="TabStripPlacement"/>.</summary>
    public SkUiCoreTabView SetTabStripPlacement(SkUiTabStripPlacement value) => SetEnum(ref _placement, value, nameof(TabStripPlacement));

    /// <summary>Sets <see cref="TabWidthMode"/>.</summary>
    public SkUiCoreTabView SetTabWidthMode(SkUiSegmentWidthMode value) => SetEnum(ref _widthMode, value, nameof(TabWidthMode));

    /// <summary>Sets <see cref="IndicatorStyle"/>.</summary>
    public SkUiCoreTabView SetIndicatorStyle(SkUiTabIndicatorStyle value) => SetEnum(ref _indicatorStyle, value, nameof(IndicatorStyle));

    /// <summary>Sets <see cref="IsSwipeEnabled"/>.</summary>
    public SkUiCoreTabView SetIsSwipeEnabled(bool value)
    {
        if (!SetProperty(ref _isSwipeEnabled, value, nameof(IsSwipeEnabled)))
            return this;
        _controller.IsUserScrollEnabled = value;
        if (!value)
            _paneScroller.CancelGestures();
        return this;
    }

    /// <summary>Sets <see cref="HeaderTextColor"/>.</summary>
    public SkUiCoreTabView SetHeaderTextColor(Color? value) => SetColor(ref _headerTextColor, value, nameof(HeaderTextColor));

    /// <summary>Sets <see cref="SelectedHeaderTextColor"/>.</summary>
    public SkUiCoreTabView SetSelectedHeaderTextColor(Color? value) => SetColor(ref _selectedHeaderTextColor, value, nameof(SelectedHeaderTextColor));

    /// <summary>Sets <see cref="IndicatorColor"/>.</summary>
    public SkUiCoreTabView SetIndicatorColor(Color? value) => SetColor(ref _indicatorColor, value, nameof(IndicatorColor));

    /// <summary>Sets <see cref="StripBackgroundColor"/>.</summary>
    public SkUiCoreTabView SetStripBackgroundColor(Color? value) => SetColor(ref _stripBackgroundColor, value, nameof(StripBackgroundColor));

    /// <summary>Sets <see cref="FontFamily"/>.</summary>
    public SkUiCoreTabView SetFontFamily(string? value) => SetStripLayout(ref _fontFamily, value, nameof(FontFamily));

    /// <summary>Sets <see cref="FontSize"/>.</summary>
    public SkUiCoreTabView SetFontSize(double value)
    {
        SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value));
        return SetStripLayout(ref _fontSize, value, nameof(FontSize));
    }

    /// <summary>Sets <see cref="FontAttributes"/>.</summary>
    public SkUiCoreTabView SetFontAttributes(FontAttributes value) => SetStripLayout(ref _fontAttributes, value, nameof(FontAttributes));

    /// <summary>Sets <see cref="FontAutoScalingEnabled"/>.</summary>
    public SkUiCoreTabView SetFontAutoScalingEnabled(bool value) => SetStripLayout(ref _fontAutoScaling, value, nameof(FontAutoScalingEnabled));

    private SkUiCoreTabView SetEnum<T>(ref T field, T value, string propertyName) where T : struct, Enum
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        return SetStripLayout(ref field, value, propertyName);
    }

    private SkUiCoreTabView SetStripLayout<T>(ref T field, T value, string propertyName)
    {
        if (!SetProperty(ref field, value, propertyName))
            return this;
        InvalidateRender(SkUiRenderDirty.Children); // the placement: the strip's and panes' order
        _strip.TabsChanged();
        InvalidateMeasure();
        return this;
    }

    private SkUiCoreTabView SetColor(ref Color? field, Color? value, string propertyName)
    {
        if (SetProperty(ref field, value, propertyName))
            _strip.ColorsChanged();
        return this;
    }

    #endregion

    #region Tabs

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        foreach (var tab in _attached.ToArray())
            if (!_tabs.Contains(tab))
            {
                tab.PropertyChanged -= OnTabPropertyChanged;
                _attached.Remove(tab);
                _panes.Forget(tab);
            }
        foreach (var tab in _tabs)
            if (_attached.Add(tab))
                tab.PropertyChanged += OnTabPropertyChanged;
        var count = _tabs.Count;
        _strip.TabsChanged();
        // An index asked for beyond the tabs waits for them to come; once reached, the tab is what stays selected.
        var waiting = _applied != _requested;
        var found = waiting || _selectedTab is null ? -1 : _tabs.IndexOf(_selectedTab);
        // Panes that move with their tab go on from where they are: a swipe or a scroll ends on a tab.
        var past = found >= 0 ? _panes.PastTab(_applied) : 0;
        if (found >= 0)
            _requested = found;
        else if (!waiting && _selectedTab is not null && count > 0)
            _requested = Math.Min(_applied, count - 1); // the selected tab went: the one in its place
        var selected = count == 0 ? _selectedIndex : Math.Clamp(_requested, 0, count - 1);
        if (found < 0)
            StopScrolling();
        else if (_scrollingTo >= 0)
            _scrollingTo = selected;
        _panes.SetCount(count, selected + past);
        Apply(selected, scroll: found < 0, animate: false);
    }

    /// <summary>Stops the panes where they are; the selection is not taken from there.</summary>
    private void StopScrolling()
    {
        _requestingScroll = true;
        try { _controller.StopMotion(); }
        finally { _requestingScroll = false; }
    }

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is not SkUiCoreTabItem tab)
            return;
        if (args.PropertyName is nameof(SkUiCoreTabItem.Content) or nameof(SkUiCoreTabItem.ContentFactory))
        {
            _panes.ContentChanged(tab);
            return;
        }
        if (args.PropertyName is nameof(SkUiCoreTabItem.AutomationId) or nameof(SkUiCoreTabItem.SemanticDescription))
        {
            _strip.SemanticsChanged();
            return;
        }
        _strip.TabsChanged();
        _paneScroller.SemanticsChanged();
    }

    string? ISkUiTabStripOwner.DescriptionOf(int index) => _tabs[index].SemanticDescription;

    int ISkUiTabStripOwner.TabCount => _tabs.Count;

    string? ISkUiTabStripOwner.HeaderOf(int index) => _tabs[index].Header;

    ImageSource? ISkUiTabStripOwner.IconOf(int index) => _tabs[index].Icon;

    string? ISkUiTabStripOwner.BadgeOf(int index) => _tabs[index].BadgeText;

    string? ISkUiTabStripOwner.AutomationIdOf(int index) => _tabs[index].AutomationId;

    bool ISkUiTabStripOwner.IsTabItemEnabled(int index) => _tabs[index].IsEnabled;

    bool ISkUiTabStripOwner.IsRightToLeft => IsRightToLeft;

    bool ISkUiTabStripOwner.SelectByUser(int index) => SelectByUser(index);

    #endregion

    #region Selection

    /// <summary>
    /// <paramref name="value"/> is the selected tab now: the strip shows it, the panes scroll there (unless a swipe brought
    /// them, <paramref name="scroll"/>), and <see cref="SelectionChanged"/> is raised when it changed.
    /// </summary>
    private void Apply(int value, bool scroll, bool animate)
    {
        _applied = value;
        _selectedTab = value >= 0 && value < _tabs.Count ? _tabs[value] : null;
        var changed = _selectedIndex != value;
        _selectedIndex = value;
        _strip.SelectionChanged(value);
        _paneScroller.SemanticsChanged();
        if (scroll && _selectedTab is not null)
            ScrollToTab(value, animate);
        MoveFocusFromPanes();
        InvalidateSemantics();
        // Everything is in place: a handler may set the index again, and what it set then is what is told.
        if (changed)
            OnPropertyChanged(nameof(SelectedIndex));
        var previous = _announced;
        if (_applied != value || previous == value)
            return;
        _announced = value;
        SelectionChanged?.Invoke(this, new SkUiSelectedIndexChangedEventArgs(previous, value));
    }

    /// <summary>Keyboard focus in a pane that is no longer selected goes to the strip: it would stay on something out of view.</summary>
    private void MoveFocusFromPanes()
    {
        if (_selectedTab is { } tab && SkUiSemantics.FocusedInside(_panes) is { } focused && !ReferenceEquals(_panes.TabOf(focused), tab))
            SkUiSemantics.RootOf(this)?.FocusManager.Focus(_strip);
    }

    /// <summary>A node inside a pane took focus: its tab is selected, so the panes rest on it and not where it just shows.</summary>
    Point? ISkUiScrollStepper.Reveal(ISkUiRenderable node)
    {
        if (_panes.TabOf(node) is not { } tab || _tabs.IndexOf(tab) is not (>= 0 and var index))
            return null;
        if (index != _selectedIndex)
            SetSelectedIndex(index);
        return new Point(_panes.OffsetOf(index), _controller.Y);
    }

    private bool SelectByUser(int index)
    {
        if (!_strip.Model.IsTabEnabled(index) || index == _selectedIndex)
            return false;
        SetSelectedIndex(index);
        return true;
    }

    /// <summary>
    /// The panes came to rest: after a swipe, the tab at the snap point is selected. Where a change of the tabs or the layout
    /// stopped them on the way, they go on to the tab.
    /// </summary>
    private void OnPanesMovingChanged()
    {
        if (_requestingScroll || _controller.IsMoving || !_panes.Engine.HasGeometry || _tabs.Count == 0)
            return;
        var target = _scrollingTo >= 0 ? _scrollingTo : _panes.CurrentIndex(_applied); // a scroll to a tab: it is selected already
        _scrollingTo = -1;
        if (target < 0 || target >= _tabs.Count)
            return;
        if (target != _selectedIndex)
        {
            _requested = target;
            Apply(target, scroll: false, animate: false);
        }
        if (_panes.TakeDisturbed() && _applied == target && !_controller.IsMoving && !_panes.RestsOn(target))
            ScrollToTab(target, animate: true);
    }

    private void ScrollToTab(int index, bool animate)
    {
        _scrollingTo = index;
        _requestingScroll = true;
        try
        {
            if (_panes.ScrollToIndex(index, animate && IsShownNow() && !SkUiMotion.IsMotionReduced).IsCompleted)
                _scrollingTo = -1;
        }
        finally
        {
            _requestingScroll = false;
        }
    }

    // Screen readers page the panes tab by tab, never to a place between two; keys inside a pane do not move the panes.
    bool ISkUiScrollStepper.CanStep(bool forward) => _strip.Model.PageTarget(forward) >= 0;

    bool ISkUiScrollStepper.Step(bool forward, bool page) => page && _strip.Model.PageTarget(forward) is var target and >= 0 && SelectByUser(target);

    bool ISkUiScrollStepper.StepToEdge(bool end) => false;

    /// <summary>Ctrl+Tab / Ctrl+Shift+Tab from anywhere inside the tab view: the next / previous enabled tab, around the ends.</summary>
    bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key) => _strip.Model.OnTabViewKeyDown(key);

    #endregion

    #region Layout

    private bool IsTop => _placement == SkUiTabStripPlacement.Top;

    /// <inheritdoc />
    /// <remarks>In reading order (they do not overlap): screen readers and Tab meet the strip first when it is on top.</remarks>
    internal override IReadOnlyList<IVisualTreeElement> VisualChildren => IsTop ? [_stripScroller, _paneScroller] : [_paneScroller, _stripScroller];

    /// <inheritdoc />
    internal override void AddRenderChildren(List<ISkUiRenderable> children)
    {
        children.Add(IsTop ? _stripScroller : _paneScroller);
        children.Add(IsTop ? _paneScroller : _stripScroller);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var strip = _strip.Model.DesiredHeight;
        var bounded = double.IsFinite(widthConstraint);
        MeasuredWidth = widthConstraint;
        if (bounded)
        {
            SetStripViewport(widthConstraint);
            _stripScroller.Measure(widthConstraint, strip);
        }
        _panes.SetViewport(widthConstraint, arranging: false);
        var panes = _paneScroller.Measure(widthConstraint, Math.Max(0, heightConstraint - strip));
        var width = bounded ? widthConstraint : Math.Max(_strip.Model.DesiredWidth, _panes.Engine.MeasuredViewport(widthConstraint));
        return new Size(width, strip + panes.Height);
    }

    /// <summary>The width offered to the last measure.</summary>
    private double MeasuredWidth { get; set; } = double.NaN;

    /// <summary>The strip is laid out again for <paramref name="width"/> (it and its scroller are measured right after).</summary>
    private void SetStripViewport(double width)
    {
        if (width == _strip.ViewportWidth) // exact: a layout offering the same width needs nothing
            return;
        _strip.ViewportWidth = width;
        _strip.ForgetMeasure();
        _stripScroller.ForgetMeasure();
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var strip = Math.Min(_strip.Model.DesiredHeight, size.Height);
        SetStripViewport(size.Width);
        _stripScroller.Measure(size.Width, strip);
        _stripScroller.Arrange(new Rect(0, IsTop ? 0 : size.Height - strip, size.Width, strip));
        _strip.AfterArrange();
        _panes.SetViewport(size.Width, arranging: true);
        var height = Math.Max(0, size.Height - strip);
        _paneScroller.Measure(size.Width, height);
        _paneScroller.Arrange(new Rect(0, IsTop ? strip : 0, size.Width, height));
    }

    /// <inheritdoc />
    protected override void OnIsEnabledChanged()
    {
        base.OnIsEnabledChanged();
        _strip.TabsChanged();
    }

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged()
    {
        base.OnEffectiveFlowDirectionChanged();
        _strip.TabsChanged();
        StopScrolling();
        _panes.SetCount(_tabs.Count, _panes.Position); // the offset is the other direction's: only the selection says where the panes are
    }

    /// <inheritdoc />
    private protected override void ForgetLookDefaults()
    {
        base.ForgetLookDefaults();
        _strip.TabsChanged();
    }

    /// <summary>The panes' and the strip's scrollers, a header's rectangle, the strip, its indicator and whether a pane was created (tests).</summary>
    internal SkUiCoreScrollView PaneScrollViewForTests => _paneScroller;

    internal SkUiCoreScrollView StripScrollViewForTests => _stripScroller;

    internal SkUiCoreNode StripNode => _strip;

    internal SkUiCoreNode IndicatorNode => _strip.Indicator;

    internal Rect HeaderBounds(int index) => _strip.Model.HeaderBounds(index);

    internal bool IsPaneCreated(int index) => _panes.IsCreated(_tabs[index]);

    #endregion
}
