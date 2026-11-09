using System.Windows.Input;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core expander (the twin of <see cref="SkUiExpander"/>): a <see cref="Header"/> that shows or hides its
/// <see cref="Content"/> when tapped. The content opens below (<see cref="SkUiExpandDirection.Down"/>) or above the header,
/// and with <see cref="LazyContentExpansion"/> is in the tree only while expanded. With <see cref="AnimationLength"/> the
/// content grows from the header (scaled vertically) and the expander's height follows it, so the nodes around it move
/// along; a tap while it animates reverses from where it is. Each frame re-records only the nodes whose size changed.
/// </summary>
public class SkUiCoreExpander : SkUiCoreNode
{
    private readonly HeaderPart _headerPart;
    // The content's host: the expander owns its scale and anchor, so the content's own are never touched.
    private readonly SkUiCoreContentView _contentPart = new();
    private SkUiCoreNode? _content;
    // 0 collapsed, 1 expanded; in between while animating
    private SkUiTween? _reveal;
    private Thickness _padding;
    private SkUiExpandDirection _direction;
    private bool _expanded;
    private bool _lazy;
    private uint _animationLength;
    private Easing? _animationEasing = Easing.CubicInOut;
    private bool _isAnimating;
    private ICommand? _command;
    private object? _commandParameter;

    /// <summary>Creates a collapsed expander; it does not clip its children.</summary>
    public SkUiCoreExpander()
    {
        InitClipToBounds(false);
        _headerPart = new HeaderPart(this);
        _contentPart.SetIsVisible(false).SetAnchorY(0);
        _headerPart.AttachTo(this);
        _contentPart.AttachTo(this);
    }

    /// <summary>The always shown part; tapping it toggles <see cref="IsExpanded"/> (tappable nodes inside it keep their taps).</summary>
    public SkUiCoreNode? Header { get => _headerPart.Content; set => SetHeader(value); }

    /// <summary>The part shown while expanded.</summary>
    public SkUiCoreNode? Content { get => _content; set => SetContent(value); }

    /// <summary>Whether the content is shown. A header tap toggles it; with an animation, it changes at once and the content follows.</summary>
    public bool IsExpanded { get => _expanded; set => SetIsExpanded(value); }

    /// <summary>Where the content opens: <see cref="SkUiExpandDirection.Down"/> (default, below the header) or above it.</summary>
    public SkUiExpandDirection Direction { get => _direction; set => SetDirection(value); }

    /// <summary>Executed with <see cref="CommandParameter"/> when <see cref="IsExpanded"/> changes (before <see cref="ExpandedChanged"/>), if it can execute.</summary>
    public ICommand? Command { get => _command; set => SetCommand(value); }

    /// <summary>The parameter of <see cref="Command"/>.</summary>
    public object? CommandParameter { get => _commandParameter; set => SetCommandParameter(value); }

    /// <summary>
    /// When <c>true</c>, the content is in the tree only while the expander is expanded: it is added when it expands and
    /// removed when it has collapsed, so collapsed content draws nothing and holds no drawing resources. When <c>false</c>
    /// (default) the content is in the tree from the start and only hidden while collapsed.
    /// </summary>
    public bool LazyContentExpansion { get => _lazy; set => SetLazyContentExpansion(value); }

    /// <summary>
    /// Length of the expand and collapse animation in milliseconds; 0 (default) shows and hides the content at once. Not
    /// animated while the system reduces motion (<see cref="SkUiMotion"/>) or before the expander is first drawn.
    /// </summary>
    public uint AnimationLength { get => _animationLength; set => SetAnimationLength(value); }

    /// <summary>Easing of the expand and collapse animation (default <see cref="Easing.CubicInOut"/>; <c>null</c> is linear).</summary>
    public Easing? AnimationEasing { get => _animationEasing; set => SetAnimationEasing(value); }

    /// <summary>Inset around the header and content.</summary>
    public Thickness Padding { get => _padding; set => SetPadding(value); }

    /// <summary>Whether the content is expanding or collapsing (raises <c>PropertyChanged</c> when it starts and ends).</summary>
    public bool IsAnimating => _isAnimating;

    /// <summary>
    /// Raised when <see cref="IsExpanded"/> changes (when it changes, not when an animation ends), after
    /// <see cref="Command"/>; also when the command throws, so the app always learns of the change.
    /// </summary>
    public event EventHandler<SkUiExpandedChangedEventArgs>? ExpandedChanged;

    /// <summary>A container in a skeleton made from content, also while empty: its children are the bones.</summary>
    internal override bool IsSkeletonContainer => true;

    /// <summary>The header's host (tests).</summary>
    internal SkUiCoreNode HeaderHost => _headerPart;

    /// <summary>The content's host, scaled while animating (tests).</summary>
    internal SkUiCoreNode ContentHost => _contentPart;

    /// <summary>How far the content is shown, from 0 (collapsed) to 1 (expanded); past 1 while an easing overshoots, never below 0.</summary>
    internal double Reveal => Math.Max(0, RevealTween.Value);

    private SkUiTween RevealTween => _reveal ??= new SkUiTween(new RevealHost(this));

    // The content is in the tree (non-lazy), or expanded or still collapsing (lazy); visible while expanded or collapsing.
    private bool ShowsContent => _expanded || RevealTween.Value > 0;

    /// <summary>Sets <see cref="Header"/>.</summary>
    public SkUiCoreExpander SetHeader(SkUiCoreNode? value)
    {
        if (ReferenceEquals(Header, value)) return this;
        if (value is not null && ReferenceEquals(value, _content))
            throw new InvalidOperationException("The header and the content must be different nodes.");
        _headerPart.SetContent(value);
        OnPropertyChanged(nameof(Header));
        return this;
    }

    /// <summary>Sets <see cref="Content"/>.</summary>
    public SkUiCoreExpander SetContent(SkUiCoreNode? value)
    {
        if (ReferenceEquals(_content, value)) return this;
        if (value is not null)
        {
            if (ReferenceEquals(value, Header))
                throw new InvalidOperationException("The header and the content must be different nodes.");
            if (value.Parent is not null && !ReferenceEquals(value.Parent, _contentPart))
                throw new InvalidOperationException("Child already has a parent.");
            if (WouldCreateParentCycle(value))
                throw new InvalidOperationException("Content cannot create a parent cycle.");
        }
        _content = value;
        OnPropertyChanged(nameof(Content));
        RefreshContent();
        return this;
    }

    /// <summary>Sets <see cref="IsExpanded"/>; runs <see cref="Command"/> and raises <see cref="ExpandedChanged"/> when it changes.</summary>
    public SkUiCoreExpander SetIsExpanded(bool value)
    {
        if (!SetProperty(ref _expanded, value, nameof(IsExpanded))) return this;
        RefreshContent();
        var length = SkUiMotion.IsMotionReduced ? 0 : _animationLength;
        var target = value ? 1f : 0f;
        RevealTween.AnimateTo(target, length == 0 ? SkUiTransition.None : SkUiTransition.FromMilliseconds(length, _animationEasing ?? Easing.Linear),
            _ =>
            {
                RefreshContent();
                UpdateIsAnimating();
            }, durationScale: Math.Abs(target - RevealTween.Value));
        UpdateIsAnimating();
        _headerPart.OnExpandedChanged();
        // The value and the nodes have changed: the app is told even when its command throws.
        try
        {
            if (_command is { } command && command.CanExecute(_commandParameter))
                command.Execute(_commandParameter);
        }
        finally
        {
            ExpandedChanged?.Invoke(this, new SkUiExpandedChangedEventArgs(value));
        }
        return this;
    }

    /// <summary>Sets <see cref="Direction"/>.</summary>
    public SkUiCoreExpander SetDirection(SkUiExpandDirection value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (!SetProperty(ref _direction, value, nameof(Direction))) return this;
        _contentPart.SetAnchorY(value == SkUiExpandDirection.Down ? 0 : 1); // the content grows from the header's side
        InvalidateRender(SkUiRenderDirty.Children);
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets <see cref="Command"/>.</summary>
    public SkUiCoreExpander SetCommand(ICommand? value)
    {
        SetProperty(ref _command, value, nameof(Command));
        return this;
    }

    /// <summary>Sets <see cref="CommandParameter"/>.</summary>
    public SkUiCoreExpander SetCommandParameter(object? value)
    {
        SetProperty(ref _commandParameter, value, nameof(CommandParameter));
        return this;
    }

    /// <summary>Sets <see cref="LazyContentExpansion"/>; turning it on while collapsed removes the content at once.</summary>
    public SkUiCoreExpander SetLazyContentExpansion(bool value)
    {
        if (SetProperty(ref _lazy, value, nameof(LazyContentExpansion))) RefreshContent();
        return this;
    }

    /// <summary>Sets <see cref="AnimationLength"/> in milliseconds.</summary>
    public SkUiCoreExpander SetAnimationLength(uint value)
    {
        SetProperty(ref _animationLength, value, nameof(AnimationLength));
        return this;
    }

    /// <summary>Sets <see cref="AnimationEasing"/> (<c>null</c>: linear).</summary>
    public SkUiCoreExpander SetAnimationEasing(Easing? value)
    {
        SetProperty(ref _animationEasing, value, nameof(AnimationEasing));
        return this;
    }

    /// <summary>Sets <see cref="Padding"/>; invalidates measure when the value changes.</summary>
    public SkUiCoreExpander SetPadding(Thickness value)
    {
        if (SetProperty(ref _padding, value, nameof(Padding))) InvalidateMeasure();
        return this;
    }

    /// <summary>Attaches or detaches the content and shows or hides its host for the current state.</summary>
    private void RefreshContent()
    {
        var attach = !_lazy || ShowsContent;
        _contentPart.SetContent(attach ? _content : null);
        _contentPart.SetIsVisible(ShowsContent);
    }

    private void UpdateIsAnimating()
    {
        if (_isAnimating == RevealTween.IsRunning)
            return;
        _isAnimating = RevealTween.IsRunning;
        OnPropertyChanged(nameof(IsAnimating));
    }

    // Each animation frame: the content's scale (composite-time; it also requests the frame) and the expander's height:
    // a relayout of the expander and its ancestors that re-records only what changed size (R2). The expander draws nothing
    // that depends on the reveal, so its own picture is not invalidated; its size change re-records it.
    private void OnRevealChanged()
    {
        _contentPart.SetScaleY(Reveal);
        InvalidateMeasureFromChild();
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        // As SkUiExpander: each part is measured with an unbounded height; the content at its full size (the same
        // constraint every animation frame, so its cached measure is reused), counted by how far it is shown.
        var width = Math.Max(0, widthConstraint - _padding.HorizontalThickness);
        var header = _headerPart.Measure(width, double.PositiveInfinity);
        var body = _contentPart.IsVisible ? _contentPart.Measure(width, double.PositiveInfinity) : Size.Zero;
        return new Size(
            Math.Max(header.Width, body.Width) + _padding.HorizontalThickness,
            header.Height + body.Height * Reveal + _padding.VerticalThickness);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        // The content is arranged at its full height and scaled by the reveal about its header-side edge, so it fills
        // exactly the band between the header and the expander's edge.
        var x = _padding.Left;
        var width = Math.Max(0, size.Width - _padding.HorizontalThickness);
        var headerHeight = _headerPart.DesiredSize.Height;
        var bodyHeight = _contentPart.IsVisible ? _contentPart.DesiredSize.Height : 0;
        var shown = bodyHeight * Reveal;
        if (_direction == SkUiExpandDirection.Down)
        {
            _headerPart.Arrange(new Rect(x, _padding.Top, width, headerHeight));
            _contentPart.Arrange(new Rect(x, _padding.Top + headerHeight, width, bodyHeight));
        }
        else
        {
            _contentPart.Arrange(new Rect(x, _padding.Top + shown - bodyHeight, width, bodyHeight));
            _headerPart.Arrange(new Rect(x, _padding.Top + shown, width, headerHeight));
        }
    }

    /// <inheritdoc />
    internal override IReadOnlyList<IVisualTreeElement> VisualChildren =>
        _direction == SkUiExpandDirection.Down ? [_headerPart, _contentPart] : [_contentPart, _headerPart];

    /// <inheritdoc />
    internal override void AddRenderChildren(List<ISkUiRenderable> children)
    {
        // Paint and focus order follow the screen order.
        if (_direction == SkUiExpandDirection.Down)
        {
            children.Add(_headerPart);
            children.Add(_contentPart);
        }
        else
        {
            children.Add(_contentPart);
            children.Add(_headerPart);
        }
    }

    /// <summary>The expander's parts, for walks that do not go through the render children.</summary>
    internal (SkUiCoreNode Header, SkUiCoreNode Content) Parts => (_headerPart, _contentPart);

    private sealed class RevealHost(SkUiCoreExpander owner) : ISkUiTransitionHost
    {
        public SkUiAnimationClock? TransitionClock => ((ISkUiTransitionHost)owner).TransitionClock;

        public SkUiLook TransitionLook => ((ISkUiTransitionHost)owner).TransitionLook;

        public void InvalidateTransition() => owner.OnRevealChanged();
    }

    /// <summary>Hosts the header and toggles the expander when tapped; read as a button with an expanded / collapsed value.</summary>
    private sealed class HeaderPart(SkUiCoreExpander owner) : SkUiCoreContentView
    {
        internal override bool HasIntrinsicTap => Content is not null;

        internal override void OnIntrinsicTap(SkUiTappedEventArgs args) => owner.SetIsExpanded(!owner.IsExpanded);

        protected override void OnContentChanged() => OnTapStateChanged();

        public void OnExpandedChanged() => InvalidateSemantics();

        protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
        {
            base.OnPopulateSemantics(info);
            if (Content is null)
                return;
            info.AutomationIdRoot = Content; // the app's header carries the id
            info.Role = SkUiSemanticsRole.Button;
            info.Value = SkUiStrings.Current.ExpansionStateText(owner.IsExpanded);
        }
    }
}
