using System.ComponentModel;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>How a <see cref="SkUiScrollReveal"/> answers its scroller.</summary>
public enum SkUiScrollRevealResponse
{
    /// <summary>The views move with the scroll, <see cref="SkUiScrollReveal.Distance"/> DIPs from shown to hidden (a toolbar that follows the finger).</summary>
    Tracked,

    /// <summary>
    /// After <see cref="SkUiScrollReveal.Threshold"/> DIPs in one direction, the views move fully in or out over
    /// <see cref="SkUiScrollReveal.Duration"/>, whatever the scroll does after that (a floating button, a bottom bar).
    /// </summary>
    Triggered,

    /// <summary>
    /// The scroll does not move the views: only <see cref="SkUiScrollReveal.Show"/> and <see cref="SkUiScrollReveal.Hide"/> do,
    /// animated over <see cref="SkUiScrollReveal.Duration"/>, for rules of the app's own (hide while the list scrolls and show
    /// when it stops, from <c>IsScrolling</c>; hide while a field has focus). <see cref="SkUiScrollReveal.ShowsNearStart"/> does
    /// not apply.
    /// </summary>
    Manual,

    /// <summary>
    /// The views hide while the list scrolls, either way, in step with it: <see cref="SkUiScrollReveal.Distance"/> DIPs of
    /// scrolling hide them fully. When the scroller comes to rest (a drag released, a fling ended), they come back, animated
    /// over <see cref="SkUiScrollReveal.Duration"/> (a floating button out of the way while you scroll).
    /// <see cref="SkUiScrollReveal.ShowsNearStart"/> does not apply.
    /// </summary>
    WhileScrolling
}

/// <summary>
/// A scroll-direction input for <see cref="SkUiScrollEffect.Reveal"/>: views hide while the list scrolls down (or right) and
/// come back when it scrolls up, anywhere in it, and are always shown at its start. Its progress, 0 (shown) to 1 (hidden),
/// lives on the render thread, so the views follow flings with no UI-thread work.
/// </summary>
/// <remarks>
/// Share one reveal between the effects of several views (a resource) so they move as one. What is not a scroll does not
/// move it: rows inserted above, a pull past the ends; a jump of more than two viewports shows the views again.
/// </remarks>
public sealed class SkUiScrollReveal : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs IsShownChangedArgs = new(nameof(IsShown));
    private static readonly PropertyChangedEventArgs SettingsChangedArgs = new(string.Empty);

    private readonly List<SkUiScrollEffectSet> _sets = [];
    private SkUiRenderReveal? _render;
    private ISkUiScrollHost? _host;

    /// <summary>The view whose scroller the render state follows: while it is attached, other views share that one.</summary>
    private SkUiScrollEffectSet? _boundSet;

    /// <summary>A request made before the reveal was first drawn: applied when it is.</summary>
    private SkUiRenderRevealRequest? _pending;

    /// <summary>Raised when <see cref="IsShown"/> flips or a setting changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The scroller it follows; <c>null</c> (default): the nearest scrolling ancestor of the first view it drives.</summary>
    public ISkUiScrollEffectSource? Source
    {
        get;
        set => Set(ref field, value);
    }

    /// <summary>Which offset drives it: <see cref="ScrollOrientation.Vertical"/> (default) or <see cref="ScrollOrientation.Horizontal"/>.</summary>
    public ScrollOrientation Orientation
    {
        get;
        set
        {
            if (value is not (ScrollOrientation.Vertical or ScrollOrientation.Horizontal))
                throw new ArgumentOutOfRangeException(nameof(value), value, "A reveal follows one axis: Vertical or Horizontal.");
            Set(ref field, value);
        }
    } = ScrollOrientation.Vertical;

    /// <summary>How it answers the scroll (default <see cref="SkUiScrollRevealResponse.Tracked"/>).</summary>
    public SkUiScrollRevealResponse Response
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
            Set(ref field, value);
        }
    }

    /// <summary>Tracked: DIPs of scroll from shown to hidden; both: the band at the start in which views are on their way back (default 56).</summary>
    public double Distance
    {
        get;
        set => Set(ref field, NonNegative(value));
    } = 56;

    /// <summary>Triggered: DIPs in one direction that start a show or hide (default 24).</summary>
    public double Threshold
    {
        get;
        set => Set(ref field, NonNegative(value));
    } = 24;

    /// <summary>A full show or hide (triggered, settling, <see cref="Show"/>, <see cref="Hide"/>); default 200 ms.</summary>
    public TimeSpan Duration
    {
        get;
        set
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), value, "A duration is not negative.");
            Set(ref field, value);
        }
    } = TimeSpan.FromMilliseconds(200);

    /// <summary>Tracked: when the scroller comes to rest halfway, finish showing or hiding (default on).</summary>
    public bool SettlesWhenIdle
    {
        get;
        set => Set(ref field, value);
    } = true;

    /// <summary>Fully shown at the start of the list: within <see cref="Distance"/> of it, never more hidden than the offset allows (default on).</summary>
    public bool ShowsNearStart
    {
        get;
        set => Set(ref field, value);
    } = true;

    /// <summary>Whether the views are nearer shown than hidden (UI side, a frame behind the drawing at most).</summary>
    public bool IsShown { get; private set; } = true;

    /// <summary>Sets <see cref="Source"/>.</summary>
    public SkUiScrollReveal SetSource(ISkUiScrollEffectSource? value) { Source = value; return this; }

    /// <summary>Sets <see cref="Orientation"/>.</summary>
    public SkUiScrollReveal SetOrientation(ScrollOrientation value) { Orientation = value; return this; }

    /// <summary>Sets <see cref="Response"/>.</summary>
    public SkUiScrollReveal SetResponse(SkUiScrollRevealResponse value) { Response = value; return this; }

    /// <summary>Sets <see cref="Distance"/>.</summary>
    public SkUiScrollReveal SetDistance(double value) { Distance = value; return this; }

    /// <summary>Sets <see cref="Threshold"/>.</summary>
    public SkUiScrollReveal SetThreshold(double value) { Threshold = value; return this; }

    /// <summary>Sets <see cref="Duration"/>.</summary>
    public SkUiScrollReveal SetDuration(TimeSpan value) { Duration = value; return this; }

    /// <summary>Sets <see cref="SettlesWhenIdle"/>.</summary>
    public SkUiScrollReveal SetSettlesWhenIdle(bool value) { SettlesWhenIdle = value; return this; }

    /// <summary>Sets <see cref="ShowsNearStart"/>.</summary>
    public SkUiScrollReveal SetShowsNearStart(bool value) { ShowsNearStart = value; return this; }

    /// <summary>Shows the views, animated; the next scroll takes over again.</summary>
    public void Show() => Request(SkUiRenderRevealRequest.Show);

    /// <summary>Hides the views, animated (held near the start while <see cref="ShowsNearStart"/>); the next scroll takes over again.</summary>
    public void Hide() => Request(SkUiRenderRevealRequest.Hide);

    /// <summary>The progress last drawn, 0 (shown) to 1 (hidden).</summary>
    internal float Progress => _render?.Progress ?? 0;

    /// <summary>Counts <see cref="Show"/>, <see cref="Hide"/> and settle requests: the views' commits carry it, so a request
    /// reaches the render thread with a frame even when nothing else changed.</summary>
    internal int RequestStamp { get; private set; }

    /// <summary>
    /// The render-thread state for <paramref name="host"/>, the scroller <paramref name="set"/>'s view found (made again when
    /// that scroller's render node changes). The first view to bind keeps it while it is attached: a view under another
    /// scroller shares that state instead of replacing it.
    /// </summary>
    internal SkUiRenderReveal Bind(ISkUiScrollHost host, SkUiScrollEffectSet set)
    {
        if (_render is { } bound && _boundSet is { } first && !ReferenceEquals(first, set) && _sets.Contains(first))
            return bound;
        var node = host.RenderState.Node;
        var horizontal = Orientation == ScrollOrientation.Horizontal;
        if (_render is not { } render || !ReferenceEquals(render.Source, node) || render.Horizontal != horizontal)
        {
            if (_render is { } old)
                old.MoveEnded -= OnMoveEnded;
            render = _render = new SkUiRenderReveal(node, horizontal) { Settings = RenderSettings() };
            render.MoveEnded += OnMoveEnded;
            if (_pending is { } pending)
                render.Request(pending);
            _pending = null;
        }
        _host = host;
        _boundSet = set;
        UpdateRange();
        return render;
    }

    /// <summary>A view's effects started (or stopped) following it: they are told when it moves without a scroll report.</summary>
    internal void Attach(SkUiScrollEffectSet set)
    {
        if (!_sets.Contains(set))
            _sets.Add(set);
    }

    internal void Detach(SkUiScrollEffectSet set)
    {
        _sets.Remove(set);
        if (_sets.Count > 0)
            return;
        // No view follows it any more: let go of the scroller (its page can be collected) and start again when drawn again.
        if (_render is { } render)
            render.MoveEnded -= OnMoveEnded;
        _render = null;
        _host = null;
        _boundSet = null;
    }

    /// <summary>Its scroller reported a scroll: the range may have changed, and the progress drawn.</summary>
    internal void OnScrolled()
    {
        UpdateRange();
        UpdateIsShown();
    }

    /// <summary>Its scroller came to rest.</summary>
    internal void OnRest()
    {
        if (Response == SkUiScrollRevealResponse.WhileScrolling)
            Request(SkUiRenderRevealRequest.Show);
        else if (Response == SkUiScrollRevealResponse.Tracked && SettlesWhenIdle)
            Request(SkUiRenderRevealRequest.Settle);
    }

    private void Request(SkUiRenderRevealRequest request)
    {
        RequestStamp++;
        if (_render is not { } render)
        {
            // Not drawn yet: acted on when it is (a view that starts hidden).
            _pending = request;
            return;
        }
        render.Request(request);
        // A frame acts on it.
        foreach (var set in _sets)
            set.OnRevealChanged();
    }

    private void OnMoveEnded(SkUiRenderReveal render)
    {
        if (!ReferenceEquals(render, _render))
            return;
        UpdateIsShown();
        foreach (var set in _sets.ToArray())
            set.OnRevealChanged();
    }

    private void UpdateRange()
    {
        if (_render is { } render && _host is { } host)
            render.MaxOffset = (float)(render.Horizontal ? host.Scroller.MaxX : host.Scroller.MaxY);
    }

    private void UpdateIsShown()
    {
        var shown = Progress < 0.5f;
        if (shown == IsShown)
            return;
        IsShown = shown;
        PropertyChanged?.Invoke(this, IsShownChangedArgs);
    }

    private SkUiRenderRevealSettings RenderSettings() => new(
        Response switch
        {
            SkUiScrollRevealResponse.Triggered => SkUiRenderRevealResponse.Triggered,
            SkUiScrollRevealResponse.Manual => SkUiRenderRevealResponse.Manual,
            SkUiScrollRevealResponse.WhileScrolling => SkUiRenderRevealResponse.WhileScrolling,
            _ => SkUiRenderRevealResponse.Tracked
        },
        (float)Distance, (float)Threshold, (float)Duration.TotalSeconds, ShowsNearStart);

    private static double NonNegative(double value) =>
        double.IsFinite(value) && value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "A finite number of DIPs, at least 0.");

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        if (_render is { } render)
            render.Settings = RenderSettings();
        // The effects re-resolve (a new source or axis) and commit again.
        foreach (var set in _sets)
            set.OnEffectsChanged();
        PropertyChanged?.Invoke(this, SettingsChangedArgs);
    }
}
