using System.Collections.ObjectModel;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>
/// One view's <see cref="SkUiScrollEffect"/>s, on either layer: finds each effect's scroller, turns the effects into render
/// links (<see cref="SkUiRenderLink"/>) when what they are built from changes, writes the links and their UI-side values into
/// the view's render props, and follows the scrollers' reports (re-commit, semantics, reveals' range and rest).
/// </summary>
/// <remarks>
/// Scrollers are listened to while the view is drawn: from the first commit that finds them until the view leaves its drawn
/// parent or its surface goes (<see cref="Release"/>), or an effect stops using them. The view then holds no reference from
/// a scroller, so a page of effects is collected with its view.
/// </remarks>
internal sealed class SkUiScrollEffectSet
{
    /// <summary>A translation moves with the content when its output range matches its input range within this (DIPs).</summary>
    private const double WithContentTolerance = 0.5;

    private readonly ISkUiRenderable _owner;
    private readonly Action _invalidate;
    private readonly Func<bool> _isRightToLeft;
    private readonly List<ISkUiScrollHost> _listened = [];
    private readonly List<SkUiScrollReveal> _reveals = [];
    private readonly Action _onScrolled;
    private readonly Action _onRest;

    // What the links were built from.
    private bool _dirty = true;
    private bool _rightToLeft;
    private bool _reduced;
    private ISkUiScrollHost?[] _hosts = [];
    private SkUiRenderNode?[] _nodes = [];

    private SkUiRenderLink[]? _links;
    private ISkUiScrollHost?[] _linkHosts = [];

    public SkUiScrollEffectSet(ISkUiRenderable owner, Action invalidate, Func<bool> isRightToLeft)
    {
        _owner = owner;
        _invalidate = invalidate;
        _isRightToLeft = isRightToLeft;
        _onScrolled = OnScrolled;
        _onRest = OnRest;
        Effects = new EffectCollection(this);
    }

    /// <summary>The effects, in order (order never changes the result; overlapping <see cref="SkUiScrollEffect.SnapsToEnds"/> ranges act as one).</summary>
    public Collection<SkUiScrollEffect> Effects { get; }

    /// <summary>
    /// Writes the links and their values at the scrollers' current offsets (UI side) into <paramref name="props"/>; nothing
    /// when no effect is enabled or no scroller is found yet.
    /// </summary>
    public void Apply(ref SkUiRenderProps props)
    {
        if (Effects.Count == 0)
        {
            // The last effect went: let go of the scrollers (and their snap ranges) and the reveals.
            if (_links is not null || _listened.Count > 0 || _reveals.Count > 0)
                Release();
            return;
        }
        var rightToLeft = _isRightToLeft();
        var reduced = SkUiMotion.IsMotionReduced;
        var changed = _dirty || rightToLeft != _rightToLeft || reduced != _reduced || _hosts.Length != Effects.Count;
        if (_hosts.Length != Effects.Count)
        {
            _hosts = new ISkUiScrollHost?[Effects.Count];
            _nodes = new SkUiRenderNode?[Effects.Count];
        }
        for (var index = 0; index < Effects.Count; index++)
        {
            var host = Resolve(Effects[index]);
            var node = host?.RenderState.Node;
            if (!ReferenceEquals(host, _hosts[index]) || !ReferenceEquals(node, _nodes[index]))
            {
                _hosts[index] = host;
                _nodes[index] = node;
                changed = true;
            }
        }
        if (changed)
        {
            _dirty = false;
            _rightToLeft = rightToLeft;
            _reduced = reduced;
            Build();
        }
        if (_links is not { } links)
            return;
        var values = SkUiRenderLinkValues.Identity;
        for (var index = 0; index < links.Length; index++)
        {
            var link = links[index];
            float input;
            if (link.Reveal is { } reveal)
            {
                input = reveal.Progress;
            }
            else
            {
                var offset = _linkHosts[index]!.Scroller.LinkInput;
                input = link.Horizontal ? offset.X : offset.Y;
            }
            values.Add(link.Target, link.Evaluate(input));
        }
        // The reveals' requests: a Show / Hide asked while nothing else changed still commits (and so draws) a frame.
        foreach (var reveal in _reveals)
            values.RevealRequests += reveal.RequestStamp;
        props.Links = links;
        props.LinkValues = values;
    }

    /// <summary>The view left its drawn parent or its surface went: stop listening and build again when drawn again.</summary>
    public void Release()
    {
        foreach (var host in _listened)
            Unlisten(host);
        _listened.Clear();
        foreach (var reveal in _reveals)
            reveal.Detach(this);
        _reveals.Clear();
        _links = null;
        _linkHosts = [];
        _hosts = [];
        _nodes = [];
        _dirty = true;
    }

    /// <summary>An effect (or a reveal's settings) changed: build again on the next commit.</summary>
    internal void OnEffectsChanged()
    {
        _dirty = true;
        _invalidate();
    }

    /// <summary>A reveal moved without a scroll report (a show, hide or settle requested or ended): commit and refresh semantics.</summary>
    internal void OnRevealChanged()
    {
        _invalidate();
        SkUiSemantics.Invalidate(_owner);
    }

    private ISkUiScrollHost? Resolve(SkUiScrollEffect effect)
    {
        var source = effect.Reveal is { } reveal ? reveal.Source : effect.Source;
        if (source is not null)
            return source.ScrollEffectHost;
        // The nearest scrolling ancestor (a sticky header's is its collection view, which resolves to the list's scroller).
        for (var node = _owner.RenderParent; node is not null; node = node.RenderParent)
            if (node is ISkUiScrollEffectSource { ScrollEffectHost: { } host })
                return host;
        return null;
    }

    private void Build()
    {
        var links = new List<SkUiRenderLink>(Effects.Count);
        var hosts = new List<ISkUiScrollHost>(Effects.Count);
        var used = new List<ISkUiScrollHost>();
        var reveals = new List<SkUiScrollReveal>();
        for (var index = 0; index < Effects.Count; index++)
        {
            var effect = Effects[index];
            if (!effect.IsEnabled || _hosts[index] is not { } host)
                continue;
            if (!used.Contains(host))
                used.Add(host);
            if (effect.Reveal is { } reveal && !reveals.Contains(reveal))
                reveals.Add(reveal);
            links.Add(Link(effect, host));
            hosts.Add(host);
        }
        // Ranges that settle on their ends (SnapsToEnds), per scroller in use.
        foreach (var host in used)
            host.Scroller.SetEndSnapRanges(this, EndSnapRanges(host));
        // Listen to the scrollers in use, and only those.
        foreach (var host in _listened)
            if (!used.Contains(host))
                Unlisten(host);
        foreach (var host in used)
            if (!_listened.Contains(host))
                Listen(host);
        _listened.Clear();
        _listened.AddRange(used);
        foreach (var reveal in _reveals)
            if (!reveals.Contains(reveal))
                reveal.Detach(this);
        foreach (var reveal in reveals)
            reveal.Attach(this);
        _reveals.Clear();
        _reveals.AddRange(reveals);
        _links = links.Count > 0 ? links.ToArray() : null;
        _linkHosts = hosts.ToArray();
    }

    private SkUiRenderLink Link(SkUiScrollEffect effect, ISkUiScrollHost host)
    {
        var target = effect.Property switch
        {
            SkUiScrollEffectProperty.TranslationX => SkUiRenderLinkTarget.TranslationX,
            SkUiScrollEffectProperty.TranslationY => SkUiRenderLinkTarget.TranslationY,
            SkUiScrollEffectProperty.Opacity => SkUiRenderLinkTarget.Opacity,
            SkUiScrollEffectProperty.Scale => SkUiRenderLinkTarget.Scale,
            SkUiScrollEffectProperty.ScaleX => SkUiRenderLinkTarget.ScaleX,
            SkUiScrollEffectProperty.ScaleY => SkUiRenderLinkTarget.ScaleY,
            SkUiScrollEffectProperty.ClipHeight => SkUiRenderLinkTarget.ClipHeight,
            _ => SkUiRenderLinkTarget.ClipWidth
        };
        // Leading and trailing are physical on the render side: a clip across keeps the other side in right to left.
        var edge = effect.ClipEdge switch
        {
            SkUiClipEdge.Center => SkUiRenderLinkClipEdge.Center,
            SkUiClipEdge.End => effect.Property == SkUiScrollEffectProperty.ClipWidth && _rightToLeft ? SkUiRenderLinkClipEdge.Start : SkUiRenderLinkClipEdge.End,
            _ => effect.Property == SkUiScrollEffectProperty.ClipWidth && _rightToLeft ? SkUiRenderLinkClipEdge.End : SkUiRenderLinkClipEdge.Start
        };
        var shape = effect.ClipShape switch
        {
            SkUiClipShape.RoundRectangle => SkUiRenderLinkClipShape.RoundRectangle,
            SkUiClipShape.Capsule => SkUiRenderLinkClipShape.Capsule,
            _ => SkUiRenderLinkClipShape.Rectangle
        };
        var from = (float)effect.From;
        var to = (float)effect.To;
        var radius = (float)effect.ClipCornerRadius;
        var motion = _reduced ? ReducedMotion(effect) : SkUiScrollEffectReducedMotion.Run;
        if (effect.Reveal is { } reveal)
        {
            var render = reveal.Bind(host, this);
            if (motion == SkUiScrollEffectReducedMotion.HoldFrom)
                to = from;
            // Reduced motion: a timed show or hide (triggered, manual) jumps for what moves (translation, scale) and still fades.
            var scales = effect.Property is SkUiScrollEffectProperty.Scale or SkUiScrollEffectProperty.ScaleX or SkUiScrollEffectProperty.ScaleY;
            var step = motion == SkUiScrollEffectReducedMotion.Default && (!FollowsTheScroll(reveal) || scales);
            return new SkUiRenderLink(render, target, from, to, effect.Curve, edge, shape, radius, step);
        }
        if (motion != SkUiScrollEffectReducedMotion.Run)
            to = from; // held
        return new SkUiRenderLink(host.RenderState.Node, effect.Orientation == ScrollOrientation.Horizontal, target,
            (float)effect.InputStart, (float)effect.InputEnd, from, to, effect.Curve, edge, shape, radius);
    }

    /// <summary>
    /// What an effect does under reduced motion: <see cref="SkUiScrollEffectReducedMotion.Run"/>,
    /// <see cref="SkUiScrollEffectReducedMotion.HoldFrom"/>, or (a triggered reveal's translation or scale)
    /// <see cref="SkUiScrollEffectReducedMotion.Default"/> for a jump.
    /// </summary>
    private static SkUiScrollEffectReducedMotion ReducedMotion(SkUiScrollEffect effect)
    {
        if (effect.ReducedMotion != SkUiScrollEffectReducedMotion.Default)
            return effect.ReducedMotion;
        var moves = effect.Property is SkUiScrollEffectProperty.TranslationX or SkUiScrollEffectProperty.TranslationY;
        var scales = effect.Property is SkUiScrollEffectProperty.Scale or SkUiScrollEffectProperty.ScaleX or SkUiScrollEffectProperty.ScaleY;
        if (!moves && !scales)
            return SkUiScrollEffectReducedMotion.Run; // fades and clips
        if (effect.Reveal is { } reveal)
            return !FollowsTheScroll(reveal) || scales
                ? SkUiScrollEffectReducedMotion.Default // a jump halfway
                : SkUiScrollEffectReducedMotion.Run;     // follows the finger, as the content does
        if (scales)
            return SkUiScrollEffectReducedMotion.HoldFrom;
        // A translation that moves exactly with the content runs; parallax holds.
        var withContent = Math.Abs(Math.Abs(effect.To - effect.From) - Math.Abs(effect.InputEnd - effect.InputStart)) < WithContentTolerance;
        return withContent ? SkUiScrollEffectReducedMotion.Run : SkUiScrollEffectReducedMotion.HoldFrom;
    }

    /// <summary>Whether a reveal moves with the scroll (as the content does), not by timed moves only.</summary>
    private static bool FollowsTheScroll(SkUiScrollReveal reveal) =>
        reveal.Response is SkUiScrollRevealResponse.Tracked or SkUiScrollRevealResponse.WhileScrolling;

    private void Listen(ISkUiScrollHost host)
    {
        host.Scroller.Scrolled += _onScrolled;
        host.Scroller.CameToRest += _onRest;
    }

    private IEnumerable<(bool Horizontal, double Start, double End)> EndSnapRanges(ISkUiScrollHost host)
    {
        for (var index = 0; index < Effects.Count; index++)
            if (Effects[index] is { IsEnabled: true, SnapsToEnds: true, Reveal: null } effect && ReferenceEquals(_hosts[index], host))
                yield return (effect.Orientation == ScrollOrientation.Horizontal, effect.InputStart, effect.InputEnd);
    }

    private void Unlisten(ISkUiScrollHost host)
    {
        host.Scroller.SetEndSnapRanges(this, []);
        host.Scroller.Scrolled -= _onScrolled;
        host.Scroller.CameToRest -= _onRest;
    }

    private void OnScrolled()
    {
        foreach (var reveal in _reveals)
            reveal.OnScrolled();
        // The committed values stay current (a source that goes leaves them), and screen readers see the view move.
        _invalidate();
        SkUiSemantics.Invalidate(_owner);
    }

    /// <summary>The scroller came to rest (a drag or fling ended, or the wheel paused).</summary>
    private void OnRest()
    {
        foreach (var reveal in _reveals)
            reveal.OnRest();
    }

    private void Attach(SkUiScrollEffect effect)
    {
        if (effect.Owner is not null && !ReferenceEquals(effect.Owner, _owner))
            throw new InvalidOperationException("A scroll effect belongs to one view at a time; remove it from the other view first.");
        effect.Owner = _owner;
        effect.PropertyChanged += OnEffectPropertyChanged;
    }

    private void Detach(SkUiScrollEffect effect)
    {
        effect.PropertyChanged -= OnEffectPropertyChanged;
        effect.Owner = null;
    }

    private void OnEffectPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => OnEffectsChanged();

    /// <summary>The effects of one view: adding attaches (one view at a time), any change builds the links again.</summary>
    private sealed class EffectCollection(SkUiScrollEffectSet set) : Collection<SkUiScrollEffect>
    {
        protected override void InsertItem(int index, SkUiScrollEffect item)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (Contains(item))
                throw new InvalidOperationException("The scroll effect is already on this view.");
            set.Attach(item);
            base.InsertItem(index, item);
            set.OnEffectsChanged();
        }

        protected override void SetItem(int index, SkUiScrollEffect item)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (ReferenceEquals(this[index], item))
                return;
            if (Contains(item))
                throw new InvalidOperationException("The scroll effect is already on this view.");
            set.Attach(item);
            set.Detach(this[index]);
            base.SetItem(index, item);
            set.OnEffectsChanged();
        }

        protected override void RemoveItem(int index)
        {
            set.Detach(this[index]);
            base.RemoveItem(index);
            set.OnEffectsChanged();
        }

        protected override void ClearItems()
        {
            foreach (var item in this)
                set.Detach(item);
            base.ClearItems();
            set.OnEffectsChanged();
        }
    }
}
