using System.Collections;
using System.Diagnostics;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core state container (the twin of <see cref="SkUiStateContainer"/>): shows <see cref="Content"/>, or, while
/// <see cref="CurrentState"/> names one of its <see cref="States"/>, that state's node (loading, empty, error…). Core nodes
/// are not bindable, so the states are keyed nodes (or factories that build them when first shown) instead of attached
/// properties. With <see cref="BeforeStateChangeAnimation"/> / <see cref="AfterStateChangeAnimation"/> set, every change of
/// <see cref="CurrentState"/> animates on the render thread.
/// </summary>
/// <remarks>
/// The nodes that are not shown stay owned by the container, detached from the drawn tree (their drawing resources are
/// released). A state's node is any Core node: a <see cref="SkUiCoreShimmer"/> makes a skeleton loading state.
/// </remarks>
public class SkUiCoreStateContainer : SkUiCoreNode
{
    private const string CanStateChangeName = nameof(CanStateChange);
    private const string CurrentStateName = nameof(CurrentState);
    private SkUiCoreNode? _content;
    private SkUiCoreNode? _attached;
    private Thickness _padding;
    private string? _currentState;
    private string? _shownState;
    private bool _canStateChange = true;
    private bool _animating;
    private bool _explicitChange;
    private SkUiViewAnimation? _beforeStateChangeAnimation;
    private SkUiViewAnimation? _afterStateChangeAnimation;

    /// <summary>Creates a state container showing its content; it does not clip its child by default.</summary>
    public SkUiCoreStateContainer()
    {
        InitClipToBounds(false);
        States = new SkUiCoreStates(this);
    }

    /// <summary>The node shown while <see cref="CurrentState"/> is <c>null</c>.</summary>
    public SkUiCoreNode? Content { get => _content; set => SetContent(value); }

    /// <summary>The states by key: a node, or a factory called when the state is first named (its node is kept).</summary>
    public SkUiCoreStates States { get; }

    /// <summary>Inset around the shown node in DIPs.</summary>
    public Thickness Padding { get => _padding; set => SetPadding(value); }

    /// <summary>
    /// The state shown, or being animated to: a key of <see cref="States"/>, or <c>null</c> (empty is <c>null</c>) for
    /// <see cref="Content"/>. With state change animations, the value changes at once and the nodes follow (a newer value
    /// while they animate wins). Naming a state that is not there throws <see cref="SkUiStateContainerException"/>.
    /// </summary>
    public string? CurrentState { get => _currentState; set => SetCurrentState(value); }

    /// <summary>The state on screen (<c>null</c>: the content); it follows <see cref="CurrentState"/>, after the animations.</summary>
    public string? ShownState => _shownState;

    /// <summary>Whether the state can change now: <c>false</c> while an animated change runs; raises <c>PropertyChanged</c>.</summary>
    public bool CanStateChange => _canStateChange;

    /// <summary>Runs on the shown node before each change of <see cref="CurrentState"/> (render thread). With either animation set, changes animate.</summary>
    public SkUiViewAnimation? BeforeStateChangeAnimation { get => _beforeStateChangeAnimation; set => SetBeforeStateChangeAnimation(value); }

    /// <summary>Runs on the new node after each change of <see cref="CurrentState"/> (render thread).</summary>
    public SkUiViewAnimation? AfterStateChangeAnimation { get => _afterStateChangeAnimation; set => SetAfterStateChangeAnimation(value); }

    /// <summary>A container in a skeleton made from content, also while empty: its children are the bones.</summary>
    internal override bool IsSkeletonContainer => true;

    /// <summary>The node shown now (tests).</summary>
    internal SkUiCoreNode? ShownChild => _attached;

    /// <summary>Sets <see cref="Content"/>.</summary>
    public SkUiCoreStateContainer SetContent(SkUiCoreNode? value)
    {
        if (ReferenceEquals(_content, value)) return this;
        if (value is not null)
        {
            ValidateNode(value);
            if (States.Owns(value))
                throw new InvalidOperationException("The content cannot also be a state's node.");
        }
        _content = value;
        OnPropertyChanged(nameof(Content));
        if (_shownState is null)
            Attach(value);
        return this;
    }

    /// <summary>Sets <see cref="Padding"/>; invalidates measure when the value changes.</summary>
    public SkUiCoreStateContainer SetPadding(Thickness value)
    {
        if (SetProperty(ref _padding, value, nameof(Padding))) InvalidateMeasure();
        return this;
    }

    /// <summary>Adds a state (see <see cref="SkUiCoreStates.Add(string, SkUiCoreNode)"/>).</summary>
    public SkUiCoreStateContainer AddState(string key, SkUiCoreNode node)
    {
        States.Add(key, node);
        return this;
    }

    /// <summary>Adds a state whose node is built when it is first named (see <see cref="SkUiCoreStates.Add(string, Func{SkUiCoreNode})"/>).</summary>
    public SkUiCoreStateContainer AddState(string key, Func<SkUiCoreNode> factory)
    {
        States.Add(key, factory);
        return this;
    }

    /// <summary>
    /// Shows the state <paramref name="value"/>, or the content for <c>null</c> / empty; animates with the state change
    /// animations once drawn. Throws <see cref="SkUiStateContainerException"/> for a state that is not in <see cref="States"/>
    /// and while an explicit <see cref="ChangeStateWithAnimation(string?, CancellationToken)"/> runs, before anything changes.
    /// </summary>
    public SkUiCoreStateContainer SetCurrentState(string? value)
    {
        value = string.IsNullOrEmpty(value) ? null : value;
        if (value == _currentState) return this;
        if (_explicitChange)
            ThrowIfCannotChange();
        if (value is not null)
            States.Resolve(value); // throws for an unknown state; builds a factory's node
        _currentState = value;
        OnPropertyChanged(CurrentStateName);
        if (_animating)
            return this; // the running loop picks the new target up
        if ((_beforeStateChangeAnimation is not null || _afterStateChangeAnimation is not null) && RenderState.HasCommitted)
            _ = RunAutomaticChanges();
        else
            Switch(value);
        return this;
    }

    /// <summary>Sets <see cref="BeforeStateChangeAnimation"/> (it becomes read-only: one animation can be shared).</summary>
    public SkUiCoreStateContainer SetBeforeStateChangeAnimation(SkUiViewAnimation? value)
    {
        value?.Freeze();
        SetProperty(ref _beforeStateChangeAnimation, value, nameof(BeforeStateChangeAnimation));
        return this;
    }

    /// <summary>Sets <see cref="AfterStateChangeAnimation"/> (it becomes read-only: one animation can be shared).</summary>
    public SkUiCoreStateContainer SetAfterStateChangeAnimation(SkUiViewAnimation? value)
    {
        value?.Freeze();
        SetProperty(ref _afterStateChangeAnimation, value, nameof(AfterStateChangeAnimation));
        return this;
    }

    /// <summary>
    /// Changes the state with a fade: the shown node fades out, the state changes, the new node fades in to its own
    /// <see cref="SkUiCoreNode.Opacity"/> (as <see cref="SkUiStateContainer.ChangeStateWithAnimation(BindableObject, string?, CancellationToken)"/>).
    /// </summary>
    public Task ChangeStateWithAnimation(string? state, CancellationToken token = default) =>
        ChangeStateWithAnimation(state, SkUiViewAnimation.FadeOut(), SkUiViewAnimation.FadeIn(), token);

    /// <summary>
    /// Changes the state with render-thread animations: <paramref name="beforeStateChange"/> runs on the shown node before
    /// the change, <paramref name="afterStateChange"/> on the new node after it (at least one is required). The node that
    /// leaves gets its own opacity, translation, rotation and scale back; the new one ends at the animation's end values.
    /// Off screen the state changes without animating. <see cref="CurrentState"/> takes the new value at the end; a
    /// cancelled change still ends in the new state. Throws <see cref="SkUiStateContainerException"/> while
    /// <see cref="CanStateChange"/> is <c>false</c>.
    /// </summary>
    public async Task ChangeStateWithAnimation(string? state, SkUiViewAnimation? beforeStateChange, SkUiViewAnimation? afterStateChange,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (beforeStateChange is null && afterStateChange is null)
            throw new ArgumentException($"Animation required. Parameters {nameof(beforeStateChange)} and {nameof(afterStateChange)} cannot both be null");
        ThrowIfCannotChange();
        state = string.IsNullOrEmpty(state) ? null : state;
        if (state is not null)
            States.Resolve(state);
        _explicitChange = true;
        SetCanStateChange(false);
        try
        {
            await SkUiStateTransition.RunAsync(this, ShownNodes, () => Switch(state), beforeStateChange, afterStateChange, token);
        }
        finally
        {
            _explicitChange = false;
            SetCanStateChange(true);
            if (_shownState != state)
                Switch(state); // cancelled before the change
            if (_currentState != state)
            {
                _currentState = state;
                OnPropertyChanged(CurrentStateName);
            }
        }
    }

    private IEnumerable<ISkUiAnimationTarget> ShownNodes() => _attached is { } shown ? [shown] : [];

    // One transition at a time, each to the newest target (a target set during the "before" animation replaces the pending
    // one; one set during the "after" animation follows it), as SkUiStateContainer's automatic changes.
    private async Task RunAutomaticChanges()
    {
        _animating = true;
        SetCanStateChange(false);
        try
        {
            while (_currentState != _shownState)
            {
                await SkUiStateTransition.RunAsync(this, ShownNodes, () => Switch(_currentState),
                    _beforeStateChangeAnimation, _afterStateChangeAnimation, CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            // The nodes must still follow CurrentState: switch without animating.
            Trace.WriteLine($"SkiaUi: {nameof(SkUiCoreStateContainer)} could not animate to state '{_currentState}': {exception.Message}");
            _animating = false;
            Switch(_currentState);
        }
        finally
        {
            _animating = false;
            SetCanStateChange(true);
        }
    }

    /// <summary>Shows <paramref name="state"/>: its node (nothing while its key is missing from <see cref="States"/>), or the content.</summary>
    private void Switch(string? state)
    {
        var changed = _shownState != state;
        _shownState = state;
        Attach(state is null ? _content : States.TryResolve(state));
        if (changed)
            OnPropertyChanged(nameof(ShownState));
    }

    /// <summary>A state's node was added, replaced or removed: the shown state shows its new node (or nothing).</summary>
    internal void OnStateChanged(string key)
    {
        if (_shownState == key)
            Attach(States.TryResolve(key));
    }

    /// <summary>Throws when <paramref name="node"/> cannot be a child of this container.</summary>
    internal void ValidateNode(SkUiCoreNode node)
    {
        if (node.Parent is not null && !ReferenceEquals(node.Parent, this))
            throw new InvalidOperationException("Child already has a parent.");
        if (WouldCreateParentCycle(node))
            throw new InvalidOperationException("Content cannot create a parent cycle.");
    }

    /// <summary>Whether <paramref name="node"/> is the content.</summary>
    internal bool IsContent(SkUiCoreNode node) => ReferenceEquals(_content, node);

    /// <summary>Attaches the node to show, detaching the previous one (its drawing resources are released).</summary>
    private void Attach(SkUiCoreNode? value)
    {
        if (ReferenceEquals(_attached, value))
            return;
        if (_attached is { } previous)
        {
            previous.AttachTo(null);
            SkUiDiagnostics.NotifyChildRemoved(this, previous, 0);
        }
        _attached = value;
        if (value is not null)
        {
            value.AttachTo(this);
            SkUiDiagnostics.NotifyChildAdded(this, value, 0);
        }
        InvalidateRender(SkUiRenderDirty.Children | SkUiRenderDirty.Props);
        InvalidateMeasure();
        InvalidateSemantics();
    }

    private void SetCanStateChange(bool value) => SetProperty(ref _canStateChange, value, CanStateChangeName);

    private void ThrowIfCannotChange()
    {
        if (!_canStateChange)
            throw new SkUiStateContainerException($"{CanStateChangeName} is false. {CurrentStateName} cannot be changed while a state change is in progress. To avoid this exception, first verify {CanStateChangeName} is {true} before changing {CurrentStateName}.");
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var size = _attached?.Measure(
            Math.Max(0, widthConstraint - _padding.HorizontalThickness),
            Math.Max(0, heightConstraint - _padding.VerticalThickness)) ?? Size.Zero;
        return new Size(size.Width + _padding.HorizontalThickness, size.Height + _padding.VerticalThickness);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size) =>
        _attached?.Arrange(new Rect(_padding.Left, _padding.Top,
            Math.Max(0, size.Width - _padding.HorizontalThickness), Math.Max(0, size.Height - _padding.VerticalThickness)));

    /// <inheritdoc />
    internal override IReadOnlyList<IVisualTreeElement> VisualChildren => _attached is null ? [] : [_attached];

    /// <inheritdoc />
    internal override void AddRenderChildren(List<ISkUiRenderable> children)
    {
        if (_attached is not null)
            children.Add(_attached);
    }
}

/// <summary>
/// The states of a <see cref="SkUiCoreStateContainer"/>: keyed nodes, or factories called when their state is first named
/// (the node they build is kept until the state is removed or replaced). Keys are case-sensitive and not empty. A node
/// belongs to one state of one container and cannot be its content. Supports collection initializers:
/// <c>States = { { "Loading", spinner }, { "Error", () => BuildError() } }</c>.
/// </summary>
public sealed class SkUiCoreStates : IReadOnlyCollection<string>
{
    private readonly SkUiCoreStateContainer _owner;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    internal SkUiCoreStates(SkUiCoreStateContainer owner) => _owner = owner;

    /// <summary>The number of states.</summary>
    public int Count => _entries.Count;

    /// <summary>
    /// The node of state <paramref name="key"/> (a factory's state builds it now); setting adds or replaces the state, and a
    /// shown state shows the new node.
    /// </summary>
    public SkUiCoreNode this[string key]
    {
        get => _entries.ContainsKey(key) ? Resolve(key) : throw new KeyNotFoundException($"State {key} not defined.");
        set => Put(key, new Entry(Validate(key, value), null), replace: true);
    }

    /// <summary>Adds state <paramref name="key"/> with its node; throws when the key is already there.</summary>
    public void Add(string key, SkUiCoreNode node) => Put(key, new Entry(Validate(key, node), null), replace: false);

    /// <summary>Adds state <paramref name="key"/> with a factory called when the state is first named; throws when the key is already there.</summary>
    public void Add(string key, Func<SkUiCoreNode> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Put(key, new Entry(null, factory), replace: false);
    }

    /// <summary>
    /// Removes state <paramref name="key"/>. While it is shown, the container shows nothing until a state with that key is
    /// added again (<see cref="SkUiCoreStateContainer.CurrentState"/> still names it).
    /// </summary>
    public bool Remove(string key)
    {
        if (!_entries.Remove(key))
            return false;
        _owner.OnStateChanged(key);
        return true;
    }

    /// <summary>Removes every state (see <see cref="Remove"/>).</summary>
    public void Clear()
    {
        foreach (var key in _entries.Keys.ToList())
            Remove(key);
    }

    /// <summary>Whether state <paramref name="key"/> is there.</summary>
    public bool Contains(string key) => _entries.ContainsKey(key);

    /// <summary>The node of state <paramref name="key"/>, once there is one (a factory's state gets it when first named).</summary>
    public bool TryGetNode(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out SkUiCoreNode? node)
    {
        node = _entries.TryGetValue(key, out var entry) ? entry.Node : null;
        return node is not null;
    }

    /// <inheritdoc />
    public IEnumerator<string> GetEnumerator() => _entries.Keys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>The node of state <paramref name="key"/>, built by its factory when needed; throws for a missing state.</summary>
    internal SkUiCoreNode Resolve(string key)
    {
        if (!_entries.TryGetValue(key, out var entry))
            throw new SkUiStateContainerException($"State {key} not defined in {nameof(SkUiCoreStateContainer.States)}.");
        if (entry.Node is { } node)
            return node;
        SkUiCoreNode built;
        try
        {
            built = entry.Factory!() ?? throw new InvalidOperationException("The factory returned null.");
        }
        catch (Exception exception) when (exception is not SkUiStateContainerException)
        {
            throw new SkUiStateContainerException($"The factory of state {key} failed: {exception.Message}", exception);
        }
        _entries[key] = new Entry(Validate(key, built), null);
        return built;
    }

    /// <summary>The node of state <paramref name="key"/>, built when needed; <c>null</c> (and a trace line when its factory fails) without one.</summary>
    internal SkUiCoreNode? TryResolve(string key)
    {
        if (!_entries.ContainsKey(key))
            return null;
        try
        {
            return Resolve(key);
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"SkiaUi: {nameof(SkUiCoreStateContainer)} cannot show state '{key}': {exception.Message}");
            return null;
        }
    }

    /// <summary>Whether <paramref name="node"/> is a state's node.</summary>
    internal bool Owns(SkUiCoreNode node)
    {
        foreach (var entry in _entries.Values)
            if (ReferenceEquals(entry.Node, node))
                return true;
        return false;
    }

    private SkUiCoreNode Validate(string key, SkUiCoreNode node)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(node);
        _owner.ValidateNode(node);
        if (_owner.IsContent(node))
            throw new InvalidOperationException("A state's node cannot also be the content.");
        foreach (var (other, entry) in _entries)
            if (other != key && ReferenceEquals(entry.Node, node))
                throw new InvalidOperationException($"The node is already the node of state {other}.");
        return node;
    }

    private void Put(string key, Entry entry, bool replace)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (!replace && _entries.ContainsKey(key))
            throw new ArgumentException($"State {key} is already defined.", nameof(key));
        _entries[key] = entry;
        _owner.OnStateChanged(key);
    }

    private readonly record struct Entry(SkUiCoreNode? Node, Func<SkUiCoreNode>? Factory);
}
