using System.Windows.Input;
using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>
/// Pull-to-refresh around drawn content (MAUI's <c>RefreshView</c>): pulling the top of the <see cref="Content"/> down and
/// releasing it sets <see cref="IsRefreshing"/>, which raises <see cref="Refreshing"/> and runs <see cref="Command"/>; the
/// app sets <see cref="IsRefreshing"/> back to <c>false</c> when the refresh is done (or the view does, with
/// <see cref="SkUiRefreshCompletion.Automatic"/> completion). XAML ports by prefix.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is pulled.</b> A drag that starts in a vertical drawn scroller of the content (<see cref="SkUiScrollView"/>,
/// <see cref="SkUiVirtualScrollView"/>, <see cref="SkUiCollectionView"/>, a Core scroll view) pulls through that scroller
/// once it is at its top, also when its content does not overflow or overscroll is off, and also when the same drag first
/// scrolled it up to the top; with nested vertical scrollers, the outermost one under the finger is pulled. A downward
/// drag elsewhere in the content (a header above a list, content that does not scroll) pulls the view itself, while the
/// content's first vertical scroller is at its top. Horizontal scrollers keep their drags.
/// </para>
/// <para>
/// <b>Indicator.</b> <see cref="RefreshIndicator"/>, drawn by the look, as on <see cref="SkUiCollectionView"/>: as an
/// overlay badge it comes down over the content; inline (iOS) the pulled content moves down and the indicator shows above
/// it, and while refreshing the content stays down (<see cref="RefreshStyle"/>; the default look follows the platform). A
/// release past <see cref="RefreshTriggerDistance"/> starts a refresh; the indicator spins on the render thread while
/// <see cref="IsRefreshing"/> is <c>true</c>. Nothing is re-recorded while dragging. Touch and pen drags pull, mouse drags
/// only with <see cref="IsMousePullEnabled"/>.
/// </para>
/// <para>
/// As MAUI's: <see cref="IsRefreshEnabled"/> turns the pull off while the content stays interactive, and a
/// <see cref="Command"/> that cannot execute does too (MAUI also reports <see cref="IsRefreshEnabled"/> as <c>false</c>
/// then; here it keeps the value set). <see cref="IsRefreshing"/> cannot become <c>true</c> while the view is disabled or
/// the pull is off, and is reset when the view is disabled or <see cref="IsRefreshEnabled"/> turns off. The content is
/// clipped to the view.
/// </para>
/// <para>
/// Do not combine it with <see cref="SkUiCollectionView.IsPullToRefreshEnabled"/> on the list it contains: both would
/// refresh. A collection view refreshes by itself; the refresh view is for other content (and for MAUI pages ported as they
/// are).
/// </para>
/// </remarks>
[ContentProperty(nameof(Content))]
public class SkUiRefreshView : SkUiView, ISkUiRefreshOwner
{
    /// <summary>Length of the indicator's return when a pull of the view itself is released before the trigger, in milliseconds.</summary>
    internal const uint PullBackLength = 200;

    private readonly ContentPart _contentPart;
    private readonly SkUiPullToRefresh _refresh;
    private readonly SkUiRefreshLayer _layer;
    private readonly SkUiTween _pullBack;
    private PullGesture? _gesture;
    private SkUiWeakListener<SkUiRefreshView>? _commandListener;
    private ScrollerLink? _link; // to the content's scroller this view pulls through (set on a press in it)
    private double _ownPull; // distance shown by a pull of the view itself (or held while refreshing inline), DIPs
    private SkUiPointerDevice _ownDevice; // what pulls the view itself
    private PullSource _releasedBy; // what released the pull that is starting a refresh
    private SkUiScrollController? _heldScroller; // the scroller holding its content down while an inline refresh runs
    private bool _commandCanExecute = true; // read again when the command, its parameter or its CanExecute change

    /// <summary>Bindable <see cref="Content"/>.</summary>
    public static readonly BindableProperty ContentProperty = BindableProperty.Create(
        nameof(Content), typeof(ISkUiView), typeof(SkUiRefreshView), null,
        validateValue: (bindable, value) =>
        {
            if (value is ISkUiView child && !ReferenceEquals(((SkUiRefreshView)bindable).Content, child))
                ((SkUiRefreshView)bindable).ValidateChild(child);
            return true;
        },
        propertyChanged: (bindable, _, newValue) => ((SkUiRefreshView)bindable).OnContentChanged((ISkUiView?)newValue));

    /// <summary>Bindable <see cref="IsRefreshing"/> (two-way by default: a pull sets it).</summary>
    public static readonly BindableProperty IsRefreshingProperty = BindableProperty.Create(
        nameof(IsRefreshing), typeof(bool), typeof(SkUiRefreshView), false, BindingMode.TwoWay,
        coerceValue: (bindable, value) => (bool)value && !((SkUiRefreshView)bindable).CanRefresh ? false : value,
        propertyChanged: (bindable, _, newValue) => ((SkUiRefreshView)bindable).OnIsRefreshingChanged((bool)newValue));

    /// <summary>Bindable <see cref="Command"/>.</summary>
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(SkUiRefreshView), null,
        propertyChanged: (bindable, _, newValue) => ((SkUiRefreshView)bindable).OnCommandChanged((ICommand?)newValue));

    /// <summary>Bindable <see cref="CommandParameter"/>.</summary>
    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(SkUiRefreshView), null,
        propertyChanged: (bindable, _, _) => ((SkUiRefreshView)bindable).OnCanRefreshChanged());

    /// <summary>Bindable <see cref="RefreshColor"/>.</summary>
    public static readonly BindableProperty RefreshColorProperty = BindableProperty.Create(
        nameof(RefreshColor), typeof(Color), typeof(SkUiRefreshView), null,
        propertyChanged: (bindable, _, newValue) => ((SkUiRefreshView)bindable)._refresh.Indicator.SetColor((Color?)newValue));

    /// <summary>Bindable <see cref="IsRefreshEnabled"/>.</summary>
    public static readonly BindableProperty IsRefreshEnabledProperty = BindableProperty.Create(
        nameof(IsRefreshEnabled), typeof(bool), typeof(SkUiRefreshView), true,
        propertyChanged: (bindable, _, newValue) =>
        {
            var view = (SkUiRefreshView)bindable;
            if (!(bool)newValue && view.IsRefreshing)
                view.IsRefreshing = false;
            view.OnCanRefreshChanged();
        });

    /// <summary>Bindable <see cref="RefreshStyle"/>.</summary>
    public static readonly BindableProperty RefreshStyleProperty = BindableProperty.Create(
        nameof(RefreshStyle), typeof(SkUiRefreshStyle), typeof(SkUiRefreshView), SkUiRefreshStyle.Default,
        validateValue: (_, value) => Enum.IsDefined((SkUiRefreshStyle)value),
        propertyChanged: (bindable, _, _) => ((SkUiRefreshView)bindable).OnPullSettingsChanged());

    /// <summary>Bindable <see cref="RefreshTriggerDistance"/>.</summary>
    public static readonly BindableProperty RefreshTriggerDistanceProperty = BindableProperty.Create(
        nameof(RefreshTriggerDistance), typeof(double), typeof(SkUiRefreshView), 0d, validateValue: SkUiValidate.NonNegative,
        propertyChanged: (bindable, _, _) => ((SkUiRefreshView)bindable).OnPullSettingsChanged());

    /// <summary>Bindable <see cref="IsMousePullEnabled"/>.</summary>
    public static readonly BindableProperty IsMousePullEnabledProperty = BindableProperty.Create(
        nameof(IsMousePullEnabled), typeof(bool), typeof(SkUiRefreshView), false,
        propertyChanged: (bindable, _, _) => ((SkUiRefreshView)bindable).OnPullSettingsChanged());

    /// <summary>Bindable <see cref="RefreshCompletion"/>.</summary>
    public static readonly BindableProperty RefreshCompletionProperty = BindableProperty.Create(
        nameof(RefreshCompletion), typeof(SkUiRefreshCompletion), typeof(SkUiRefreshView), SkUiRefreshCompletion.Manual,
        validateValue: (_, value) => Enum.IsDefined((SkUiRefreshCompletion)value));

    /// <summary>Creates an empty refresh view (GPU-backed when it is a surface of its own, as content views are).</summary>
    public SkUiRefreshView()
    {
        HwAccelerated = true;
        ClipToBounds = true; // as MAUI's RefreshView (IsClippedToBounds)
        _pullBack = new SkUiTween(new PullBackHost(this));
        _contentPart = new ContentPart();
        _refresh = new SkUiPullToRefresh(this);
        _layer = _refresh.Layer;
        AttachChild(_contentPart);
        AttachChild(_layer);
    }

    /// <summary>The refreshed content; drags in its vertical drawn scrollers pull at their top.</summary>
    public ISkUiView? Content
    {
        get => (ISkUiView?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Whether a refresh runs: the indicator spins at the top. Set to <c>true</c> (by a pull, or by the app) it raises
    /// <see cref="Refreshing"/> and runs <see cref="Command"/>; set it back to <c>false</c> when the refresh is done (with
    /// <see cref="SkUiRefreshCompletion.Automatic"/> completion the view does).
    /// </summary>
    public bool IsRefreshing
    {
        get => (bool)GetValue(IsRefreshingProperty);
        set => SetValue(IsRefreshingProperty, value);
    }

    /// <summary>Runs with <see cref="CommandParameter"/> when <see cref="IsRefreshing"/> becomes <c>true</c>; while it cannot execute, the pull is off.</summary>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>The parameter of <see cref="Command"/>.</summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <summary>The refresh indicator's color; <c>null</c> (default): the accent color.</summary>
    public Color? RefreshColor
    {
        get => (Color?)GetValue(RefreshColorProperty);
        set => SetValue(RefreshColorProperty, value);
    }

    /// <summary>Whether a pull can start a refresh (default <c>true</c>); when <c>false</c>, the content stays interactive.</summary>
    public bool IsRefreshEnabled
    {
        get => (bool)GetValue(IsRefreshEnabledProperty);
        set => SetValue(IsRefreshEnabledProperty, value);
    }

    /// <summary>
    /// How the refresh indicator shows: <see cref="SkUiRefreshStyle.Overlay"/> (a badge over the content),
    /// <see cref="SkUiRefreshStyle.Inline"/> (above the pulled content, which moves down), or the look's
    /// (<see cref="SkUiRefreshStyle.Default"/>, default; the default look follows the platform). SkiaUi extension.
    /// </summary>
    public SkUiRefreshStyle RefreshStyle
    {
        get => (SkUiRefreshStyle)GetValue(RefreshStyleProperty);
        set => SetValue(RefreshStyleProperty, value);
    }

    /// <summary>
    /// How far the top must be pulled (shown past the edge, DIPs) for a release to refresh; 0 (default): the look's
    /// (<see cref="SkUiLook.RefreshTriggerDistance"/>, 64). SkiaUi extension.
    /// </summary>
    public double RefreshTriggerDistance
    {
        get => (double)GetValue(RefreshTriggerDistanceProperty);
        set => SetValue(RefreshTriggerDistanceProperty, value);
    }

    /// <summary>
    /// Whether mouse drags pull too (default <c>false</c>: touch and pen only, as desktop apps expect; the touch screens of
    /// laptops pull). SkiaUi extension.
    /// </summary>
    public bool IsMousePullEnabled
    {
        get => (bool)GetValue(IsMousePullEnabledProperty);
        set => SetValue(IsMousePullEnabledProperty, value);
    }

    /// <summary>
    /// Who sets <see cref="IsRefreshing"/> back to <c>false</c>: the app (<see cref="SkUiRefreshCompletion.Manual"/>,
    /// default, MAUI's rule) or the view when the refresh's work is done (<see cref="SkUiRefreshCompletion.Automatic"/>: an
    /// async command, deferrals taken in <see cref="Refreshing"/>). SkiaUi extension.
    /// </summary>
    public SkUiRefreshCompletion RefreshCompletion
    {
        get => (SkUiRefreshCompletion)GetValue(RefreshCompletionProperty);
        set => SetValue(RefreshCompletionProperty, value);
    }

    /// <summary>The refresh indicator, drawn by the look (style it: <see cref="SkUiCoreRefreshIndicator.Color"/>, a shadow).</summary>
    public SkUiCoreRefreshIndicator RefreshIndicator => _refresh.Indicator;

    /// <summary>
    /// Raised when <see cref="IsRefreshing"/> becomes <c>true</c>, before <see cref="Command"/> runs; with automatic
    /// completion, <see cref="SkUiRefreshingEventArgs.GetDeferral"/> keeps the refresh running until the deferral completes.
    /// Handlers written for MAUI's (<c>EventArgs</c>) still attach.
    /// </summary>
    public event EventHandler<SkUiRefreshingEventArgs>? Refreshing;

    /// <summary>Sets <see cref="Content"/> (same as the property setter).</summary>
    public SkUiRefreshView SetContent(ISkUiView? value)
    {
        if (!ReferenceEquals(Content, value) && value is not null) ValidateChild(value);
        Content = value;
        return this;
    }

    /// <summary>Sets <see cref="Command"/> and <see cref="CommandParameter"/>.</summary>
    public SkUiRefreshView SetCommand(ICommand? command, object? parameter = null)
    {
        CommandParameter = parameter;
        Command = command;
        return this;
    }

    /// <summary>The scroller the view pulls through, if a press found one (tests).</summary>
    internal ISkUiScrollHost? PulledScroller => _link?.Host;

    /// <summary>A pull may start a refresh: enabled, <see cref="IsRefreshEnabled"/>, and a command (if any) that can execute.</summary>
    private bool CanRefresh => IsEnabled && IsRefreshEnabled && _commandCanExecute;

    private void OnContentChanged(ISkUiView? value)
    {
        // Replaced during an inline refresh: the old scroller lets its content back up, and the view holds the new content
        // down itself (no scroller of it was pulled) until the refresh ends.
        if (IsRefreshing)
            ReleaseHold();
        DetachScroller();
        _contentPart.Content = value;
        if (IsRefreshing)
            Hold();
    }

    private void OnCommandChanged(ICommand? value)
    {
        (_commandListener ??= new SkUiWeakListener<SkUiRefreshView>(this, static (view, change) =>
        {
            if (change.Kind == SkUiChangeKind.CanExecute)
                view.OnCanRefreshChanged();
        })).Listen(value);
        OnCanRefreshChanged();
    }

    /// <summary>
    /// The pull was turned on or off: a scroller pulls no more for an off view, and a pull in progress no longer shows. While
    /// a refresh runs (its command busy, say) nothing is let go: that waits for its end.
    /// </summary>
    private void OnCanRefreshChanged()
    {
        _commandCanExecute = Command?.CanExecute(CommandParameter) ?? true;
        if (!CanRefresh && !IsRefreshing)
        {
            DetachScroller();
            _gesture?.Cancel();
            _pullBack.Jump(0);
            _ownPull = 0;
        }
        UpdateIndicator();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled))
        {
            // As MAUI's: disabling the view ends a refresh.
            if (!IsEnabled && IsRefreshing)
                IsRefreshing = false;
            OnCanRefreshChanged();
        }
    }

    private void OnIsRefreshingChanged(bool value)
    {
        if (!value)
        {
            // Done: the content comes back up, the indicator goes (or follows a pull still in progress), and a pull turned off
            // meanwhile lets go now.
            ReleaseHold();
            _refresh.OnIsRefreshingChanged(false);
            OnCanRefreshChanged();
            return;
        }
        Hold();
        UpdateIndicator();
        _refresh.OnIsRefreshingChanged(true);
    }

    /// <summary>
    /// A refresh starts: what was pulled (the scroller, or the view itself; the scroller pulled last when the app starts it)
    /// holds its content down while an inline refresh runs; an overlay badge needs no room.
    /// </summary>
    private void Hold()
    {
        var source = _releasedBy != PullSource.None ? _releasedBy : _link?.Host is not null ? PullSource.Scroller : PullSource.View;
        _releasedBy = PullSource.None;
        var inline = _refresh.Style == SkUiRefreshStyle.Inline;
        if (source == PullSource.Scroller && _link?.Host is { } host)
        {
            AnimateOwnPull(0);
            if (inline)
            {
                _heldScroller = host.Scroller;
                _heldScroller.SetTopRest(SkUiPullToRefresh.RestDistance);
            }
        }
        else if (inline)
        {
            AnimateOwnPull(SkUiPullToRefresh.RestDistance);
        }
        else
        {
            _pullBack.Jump(0);
            _ownPull = 0;
        }
    }

    /// <summary>The refresh ended (or its style changed): held content goes back up.</summary>
    private void ReleaseHold()
    {
        if (_heldScroller is { } scroller)
        {
            _heldScroller = null;
            scroller.SetTopRest(0);
        }
        if (_gesture?.IsPulling != true)
            AnimateOwnPull(0);
    }

    /// <summary>The style, trigger distance or mouse setting changed: the pulled scroller, a held refresh and the indicator follow.</summary>
    private void OnPullSettingsChanged()
    {
        if (_link?.Host is { } host)
            host.Scroller.PullsForRefreshView = _refresh.PullInput;
        if (IsRefreshing)
        {
            ReleaseHold();
            Hold();
        }
        UpdateIndicator();
    }

    bool ISkUiRefreshOwner.CanStartRefresh => CanRefresh;

    ICommand? ISkUiRefreshOwner.RefreshCommand => Command;

    object? ISkUiRefreshOwner.RefreshCommandParameter => CommandParameter;

    void ISkUiRefreshOwner.RaiseRefreshing(SkUiRefreshingEventArgs args) => Refreshing?.Invoke(this, args);

    /// <summary>
    /// Shows the pull (the view's own or its scroller's, whichever shows more) or the running refresh; inline, the view's own
    /// pull moves the content and the indicator starts at the pulled scroller's top.
    /// </summary>
    private void UpdateIndicator()
    {
        var inline = _refresh.Style == SkUiRefreshStyle.Inline;
        _contentPart.TranslationY = inline ? _ownPull : 0;
        var gap = _ownPull;
        var top = 0d;
        var device = _ownDevice;
        if (_link?.Host is { } host && -host.Scroller.OverscrollY > gap)
        {
            gap = -host.Scroller.OverscrollY;
            device = host.Scroller.DragDevice;
            if (inline)
                top = TopOf(host);
        }
        _refresh.ShowPull(IsRefreshing || CanRefresh ? gap : 0, top, device);
    }

    /// <summary>Where <paramref name="host"/> starts below this view's top, in DIPs (0 when not on a surface).</summary>
    private double TopOf(ISkUiScrollHost host) =>
        SkUiDiagnostics.GetRootBounds(host) is { } bounds && SkUiDiagnostics.GetRootBounds(this) is { } own ? bounds.Top - own.Top : 0;

    /// <summary>What a pull pulled: a scroller of the content, or the view itself.</summary>
    private enum PullSource
    {
        None,
        Scroller,
        View
    }

    #region Scrollers

    /// <summary>Pulls through <paramref name="host"/> from now on (the previous scroller pulls no more).</summary>
    private void AttachScroller(ISkUiScrollHost host)
    {
        if (_link is { } link && ReferenceEquals(link.Host, host))
            return;
        DetachScroller();
        _link = new ScrollerLink(this, host, _refresh.PullInput);
    }

    private void DetachScroller()
    {
        if (_link is not { } link)
            return;
        _link = null;
        link.Detach();
        UpdateIndicator();
    }

    private void OnPullReleased(ScrollerLink link, ISkUiScrollHost host, double distance)
    {
        if (!ReferenceEquals(_link, link))
            return;
        if (!Contains(host))
        {
            // The scroller left the content (moved elsewhere): it pulls for this view no more.
            DetachScroller();
            return;
        }
        _releasedBy = PullSource.Scroller;
        _refresh.Release(distance, host.Scroller.DragDevice);
        _releasedBy = PullSource.None;
    }

    private void OnOverscrollChanged(ScrollerLink link, ISkUiScrollHost host)
    {
        if (!ReferenceEquals(_link, link))
            return;
        if (Contains(host))
            UpdateIndicator();
        else
            DetachScroller();
    }

    /// <summary>
    /// A pulled scroller's subscription. It holds the view and the scroller weakly: a scroller that left the content (kept
    /// by the app, or dropped) and the view keep each other alive in neither direction; the link ends itself when the view
    /// is gone, or when the view finds the scroller outside its content.
    /// </summary>
    private sealed class ScrollerLink
    {
        private readonly WeakReference<SkUiRefreshView> _view;
        private readonly WeakReference<ISkUiScrollHost> _host;

        public ScrollerLink(SkUiRefreshView view, ISkUiScrollHost host, SkUiPullInput input)
        {
            _view = new WeakReference<SkUiRefreshView>(view);
            _host = new WeakReference<ISkUiScrollHost>(host);
            var controller = host.Scroller;
            controller.PullsForRefreshView = input;
            controller.PullReleased += OnPullReleased;
            controller.OverscrollChanged += OnOverscrollChanged;
        }

        public ISkUiScrollHost? Host => _host.TryGetTarget(out var host) ? host : null;

        public void Detach()
        {
            if (Host is not { } host)
                return;
            var controller = host.Scroller;
            controller.PullsForRefreshView = SkUiPullInput.None;
            controller.PullReleased -= OnPullReleased;
            controller.OverscrollChanged -= OnOverscrollChanged;
        }

        private void OnPullReleased(SkUiScrollEdges edge, double distance)
        {
            if (!_view.TryGetTarget(out var view))
                Detach();
            else if (edge == SkUiScrollEdges.Top && Host is { } host)
                view.OnPullReleased(this, host, distance);
        }

        private void OnOverscrollChanged()
        {
            if (!_view.TryGetTarget(out var view))
                Detach();
            else if (Host is { } host)
                view.OnOverscrollChanged(this, host);
        }
    }

    /// <summary>Whether <paramref name="node"/> is in this view's content, not inside a refresh view nested in it.</summary>
    private bool Contains(ISkUiRenderable node)
    {
        for (var parent = node.RenderParent; parent is not null; parent = parent.RenderParent)
        {
            if (ReferenceEquals(parent, this))
                return true;
            if (parent is SkUiRefreshView)
                return false;
        }
        return false;
    }

    /// <summary>
    /// The outermost vertical scroller of the content among the scrollers competing for a press (they are under the
    /// pointer); <c>null</c> when the press is outside every one. <paramref name="nested"/>: it lies in a nested refresh
    /// view, which pulls instead.
    /// </summary>
    private ISkUiScrollHost? ScrollerUnderPress(SkUiGestureArena arena, out bool nested)
    {
        ISkUiScrollHost? found = null;
        foreach (var member in arena.Members)
        {
            if (ReferenceEquals(member, _gesture))
                break; // later members belong to ancestors
            if (member is SkUiScrollGestureRecognizer && member.Node is ISkUiScrollHost host && host.Scroller.Vertical)
                found = host;
        }
        nested = found is not null && !Contains(found);
        return found;
    }

    /// <summary>The first vertical drawn scroller of the content, breadth first (not inside a nested refresh view).</summary>
    private SkUiScrollController? FirstContentScroller()
    {
        // Never re-entered: the search lists are reused per thread and emptied afterwards (they keep no node alive).
        var queue = _searchQueue ??= new Queue<ISkUiRenderable>();
        var children = _searchChildren ??= [];
        try
        {
            queue.Enqueue(_contentPart);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (node is ISkUiScrollHost host && host.Scroller.Vertical)
                    return host.Scroller;
                if (node is SkUiRefreshView)
                    continue;
                children.Clear();
                node.GetRenderChildren(children);
                foreach (var child in children)
                    queue.Enqueue(child);
            }
            return null;
        }
        finally
        {
            queue.Clear();
            children.Clear();
        }
    }

    [ThreadStatic] private static Queue<ISkUiRenderable>? _searchQueue;
    [ThreadStatic] private static List<ISkUiRenderable>? _searchChildren;

    #endregion

    #region Pulling the view itself

    /// <inheritdoc />
    internal override void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
    {
        base.CollectGestureRecognizers(recognizers);
        if (CanRefresh && !IsRefreshing)
            recognizers.Add(_gesture ??= new PullGesture(this));
    }

    /// <inheritdoc />
    internal override void CancelGestures()
    {
        base.CancelGestures();
        _gesture?.Cancel();
    }

    private void PullTo(double distance, SkUiPointerDevice device)
    {
        _pullBack.Jump(0);
        _ownDevice = device;
        _ownPull = SkUiOverscroll.RubberBand(Math.Max(0, distance), Height);
        UpdateIndicator();
    }

    /// <summary>
    /// A pull of the view itself ended: past the trigger, a release (not a cancel) starts a refresh (an inline one settles at
    /// its rest); otherwise the pull goes back.
    /// </summary>
    private void EndPull(bool released)
    {
        if (released)
        {
            _releasedBy = PullSource.View;
            _refresh.Release(_ownPull, _ownDevice);
            _releasedBy = PullSource.None;
        }
        if (!IsRefreshing)
            AnimateOwnPull(0);
    }

    /// <summary>Moves the view's own pull to <paramref name="target"/> on the UI clock (at once with reduced motion or no clock).</summary>
    private void AnimateOwnPull(double target)
    {
        if (_ownPull == target && !_pullBack.IsRunning)
            return;
        var length = SkUiMotion.IsMotionReduced ? 0 : PullBackLength;
        _pullBack.Jump((float)_ownPull);
        _pullBack.AnimateTo((float)target, length == 0 ? SkUiTransition.None : SkUiTransition.FromMilliseconds(length, Easing.CubicOut));
        if (length == 0 || !_pullBack.IsRunning)
        {
            _ownPull = target;
            UpdateIndicator();
        }
    }

    /// <inheritdoc />
    protected override void OnAnimationRootChanged(bool subtreeDetached = false)
    {
        base.OnAnimationRootChanged(subtreeDetached);
        if (!subtreeDetached)
            return;
        // Left the tree: its scroller pulls for it no more, and a pull in progress (or held content) is dropped.
        if (_heldScroller is { } held)
        {
            _heldScroller = null;
            held.SetTopRest(0);
        }
        DetachScroller();
        _pullBack.Jump(0);
        _ownPull = 0;
        UpdateIndicator();
    }

    /// <summary>
    /// On a press, chooses what is pulled: a vertical scroller of the content under the pointer pulls by itself (this
    /// recognizer leaves the arena); elsewhere this recognizer claims a downward drag while the content's first vertical
    /// scroller is at its top, and moves the indicator with it.
    /// </summary>
    private sealed class PullGesture(SkUiRefreshView owner) : SkUiGestureRecognizer
    {
        private long? _pointer;
        private bool _active;
        private SkUiScrollController? _contentScroller;
        private SkUiPointerDevice _device;

        /// <summary>A drag of the view itself is in progress.</summary>
        public bool IsPulling => _active;

        protected internal override bool IsExclusive => true;

        protected internal override bool OnPointerPressed(SkUiPointer pointer)
        {
            if (_pointer is not null || !owner.CanRefresh || owner.IsRefreshing || ArenaOf(pointer.Id) is not { } arena)
                return false;
            if (owner.ScrollerUnderPress(arena, out var nested) is { } scroller)
            {
                if (!nested)
                    owner.AttachScroller(scroller);
                return false;
            }
            if (pointer.Device == SkUiPointerDevice.Mouse && !owner.IsMousePullEnabled)
                return false;
            _contentScroller = owner.FirstContentScroller();
            _pointer = pointer.Id;
            _device = pointer.Device;
            _active = false;
            return true;
        }

        protected internal override void OnPointerMoved(SkUiPointer pointer)
        {
            if (_pointer != pointer.Id)
                return;
            var dy = pointer.TotalY;
            if (!_active)
            {
                if (ExceedsSlop(pointer.TotalX, dy, SkUiPanAxis.Vertical))
                {
                    if (dy < 0 || _contentScroller?.CanScroll(0, -1) == true)
                    {
                        Resign();
                        return;
                    }
                    Claim();
                    if (_pointer != pointer.Id)
                        return; // lost the arena
                    _active = true;
                }
                else
                {
                    if (ExceedsSlop(pointer.TotalX, dy, SkUiPanAxis.Horizontal))
                        Resign();
                    return;
                }
            }
            owner.PullTo(dy - SkUiGestureSettings.TouchSlop, _device);
        }

        protected internal override void OnPointerReleased(SkUiPointer pointer)
        {
            if (_pointer != pointer.Id)
                return;
            var active = _active;
            End();
            if (active)
                owner.EndPull(released: true);
            else
                Resign();
        }

        protected internal override void OnRejected(long pointerId)
        {
            if (_pointer != pointerId)
                return;
            var active = _active;
            End();
            if (active)
                owner.EndPull(released: false);
        }

        private void End()
        {
            _pointer = null;
            _active = false;
            _contentScroller = null;
        }
    }

    private sealed class PullBackHost(SkUiRefreshView owner) : ISkUiTransitionHost
    {
        public SkUiAnimationClock? TransitionClock => ((ISkUiTransitionHost)owner).TransitionClock;

        public void InvalidateTransition()
        {
            owner._ownPull = Math.Max(0, owner._pullBack.Value);
            owner.UpdateIndicator();
        }
    }

    #endregion

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren
    {
        get
        {
            yield return _contentPart;
            yield return _layer;
        }
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        ((IView)_layer).Measure(widthConstraint, heightConstraint);
        return ((IView)_contentPart).Measure(widthConstraint, heightConstraint);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var bounds = new Rect(Point.Zero, size);
        ((IView)_contentPart).Arrange(bounds);
        ((IView)_layer).Arrange(bounds);
    }

    /// <summary>Hosts the content.</summary>
    private sealed class ContentPart : SkUiContentView;
}
