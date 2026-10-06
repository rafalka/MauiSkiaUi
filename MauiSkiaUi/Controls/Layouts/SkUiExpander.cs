using System.Windows.Input;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>Where an <see cref="SkUiExpander"/>'s content opens, relative to its header.</summary>
public enum SkUiExpandDirection
{
    /// <summary>Below the header (default).</summary>
    Down,
    /// <summary>Above the header.</summary>
    Up
}

/// <summary>Data of <see cref="SkUiExpander.ExpandedChanged"/>.</summary>
public sealed class SkUiExpandedChangedEventArgs(bool isExpanded) : EventArgs
{
    /// <summary>The new <see cref="SkUiExpander.IsExpanded"/>.</summary>
    public bool IsExpanded { get; } = isExpanded;
}

/// <summary>
/// A <see cref="Header"/> that shows or hides its <see cref="Content"/> when tapped (Community Toolkit
/// <c>Expander</c>). The content opens below (<see cref="SkUiExpandDirection.Down"/>) or above the header, comes from
/// <see cref="ContentTemplate"/> when not set, and with <see cref="LazyContentExpansion"/> is in the tree only while
/// expanded. With <see cref="AnimationLength"/> the content grows from the header (scaled vertically) and the expander's
/// height follows it, so the views around it move along; a tap while it animates reverses from where it is.
/// </summary>
[ContentProperty(nameof(Content))]
public class SkUiExpander : SkUiView
{
    private readonly HeaderPart _headerPart;
    private readonly ContentPart _contentPart;
    private readonly SkUiContentSlot _slot;
    private readonly SkUiTween _reveal; // 0 collapsed, 1 expanded; in between while animating
    private Thickness _padding;
    private SkUiExpandDirection _direction;
    private bool _expanded;
    private bool _lazy;
    private uint _animationLength;
    private Easing? _animationEasing = Easing.CubicInOut;
    private bool _isAnimating;
    private static Func<bool, string> _expandedStateText = expanded => expanded ? "Expanded" : "Collapsed";

    /// <summary>Bindable <see cref="Header"/>.</summary>
    public static readonly BindableProperty HeaderProperty = BindableProperty.Create(
        nameof(Header), typeof(ISkUiView), typeof(SkUiExpander), null,
        validateValue: (bindable, value) =>
        {
            if (value is ISkUiView child && !ReferenceEquals(((SkUiExpander)bindable).Header, child))
                ((SkUiExpander)bindable).ValidateChild(child);
            return true;
        },
        propertyChanged: (bindable, _, newValue) => ((SkUiExpander)bindable)._headerPart.Content = (ISkUiView?)newValue);

    /// <summary>Bindable <see cref="Content"/>.</summary>
    public static readonly BindableProperty ContentProperty = BindableProperty.Create(
        nameof(Content), typeof(ISkUiView), typeof(SkUiExpander), null,
        validateValue: (bindable, value) =>
        {
            if (value is ISkUiView child && !ReferenceEquals(((SkUiExpander)bindable).Content, child))
                ((SkUiExpander)bindable).ValidateChild(child);
            return true;
        },
        propertyChanged: (bindable, _, newValue) => ((SkUiExpander)bindable).OnContentChanged((ISkUiView?)newValue));

    /// <summary>Bindable <see cref="ContentTemplate"/>.</summary>
    public static readonly BindableProperty ContentTemplateProperty = BindableProperty.Create(
        nameof(ContentTemplate), typeof(DataTemplate), typeof(SkUiExpander), null,
        propertyChanged: (bindable, _, _) => ((SkUiExpander)bindable).OnContentTemplateChanged());

    /// <summary>Bindable <see cref="IsExpanded"/> (two-way by default: a header tap changes it).</summary>
    public static readonly BindableProperty IsExpandedProperty = BindableProperty.Create(
        nameof(IsExpanded), typeof(bool), typeof(SkUiExpander), false, BindingMode.TwoWay,
        propertyChanged: (bindable, _, newValue) => ((SkUiExpander)bindable).OnIsExpandedChanged((bool)newValue));

    /// <summary>Bindable <see cref="Direction"/>.</summary>
    public static readonly BindableProperty DirectionProperty = BindableProperty.Create(
        nameof(Direction), typeof(SkUiExpandDirection), typeof(SkUiExpander), SkUiExpandDirection.Down,
        validateValue: (_, value) => Enum.IsDefined((SkUiExpandDirection)value),
        propertyChanged: (bindable, _, newValue) => ((SkUiExpander)bindable).OnDirectionChanged((SkUiExpandDirection)newValue));

    /// <summary>Bindable <see cref="Command"/>.</summary>
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(SkUiExpander));

    /// <summary>Bindable <see cref="CommandParameter"/>.</summary>
    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(SkUiExpander));

    /// <summary>Bindable <see cref="LazyContentExpansion"/>.</summary>
    public static readonly BindableProperty LazyContentExpansionProperty = BindableProperty.Create(
        nameof(LazyContentExpansion), typeof(bool), typeof(SkUiExpander), false,
        propertyChanged: (bindable, _, newValue) => ((SkUiExpander)bindable).OnLazyContentExpansionChanged((bool)newValue));

    /// <summary>Bindable <see cref="AnimationLength"/>.</summary>
    public static readonly BindableProperty AnimationLengthProperty = BindableProperty.Create(
        nameof(AnimationLength), typeof(uint), typeof(SkUiExpander), 0u,
        propertyChanged: (bindable, _, newValue) => ((SkUiExpander)bindable)._animationLength = (uint)newValue);

    /// <summary>Bindable <see cref="AnimationEasing"/> (XAML: an easing name, e.g. <c>"SpringOut"</c>).</summary>
    public static readonly BindableProperty AnimationEasingProperty = BindableProperty.Create(
        nameof(AnimationEasing), typeof(Easing), typeof(SkUiExpander), Easing.CubicInOut,
        propertyChanged: (bindable, _, newValue) => ((SkUiExpander)bindable)._animationEasing = (Easing?)newValue);

    /// <summary>Bindable <see cref="Padding"/>.</summary>
    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(
        nameof(Padding), typeof(Thickness), typeof(SkUiExpander), default(Thickness),
        propertyChanged: (bindable, _, newValue) => ((SkUiExpander)bindable).OnPaddingChanged((Thickness)newValue));

    /// <summary>Creates a collapsed expander (GPU-backed when it is a surface of its own, as content views are).</summary>
    public SkUiExpander()
    {
        HwAccelerated = true;
        _slot = new SkUiContentSlot(this, ContentProperty, ContentTemplateProperty);
        _reveal = new SkUiTween(new RevealHost(this));
        _headerPart = new HeaderPart(this);
        _contentPart = new ContentPart { IsVisible = false, AnchorY = 0 };
        AttachChild(_headerPart);
        AttachChild(_contentPart);
    }

    /// <summary>The always shown part; tapping it toggles <see cref="IsExpanded"/> (tappable views inside it keep their taps).</summary>
    public ISkUiView? Header
    {
        get => (ISkUiView?)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>The part shown while expanded.</summary>
    public ISkUiView? Content
    {
        get => (ISkUiView?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Creates <see cref="Content"/> when it is not set: a template of drawn views (a <see cref="DataTemplateSelector"/>
    /// chooses by the binding context, again when it changes). It runs once the expander is in a tree, or with
    /// <see cref="LazyContentExpansion"/> when it first expands; a new template replaces the content.
    /// </summary>
    public DataTemplate? ContentTemplate
    {
        get => (DataTemplate?)GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    /// <summary>Whether the content is shown. A header tap toggles it; with an animation, it changes at once and the content follows.</summary>
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>Where the content opens: <see cref="SkUiExpandDirection.Down"/> (default, below the header) or above it.</summary>
    public SkUiExpandDirection Direction
    {
        get => (SkUiExpandDirection)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    /// <summary>Executed with <see cref="CommandParameter"/> when <see cref="IsExpanded"/> changes (before <see cref="ExpandedChanged"/>), if it can execute.</summary>
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

    /// <summary>
    /// When <c>true</c>, the content (set, or from <see cref="ContentTemplate"/>) is in the tree only while the expander
    /// is expanded: it is added (a template runs the first time) when it expands and removed when it has collapsed, so
    /// collapsed content binds, loads and draws nothing. Template content is kept for the next expand. When <c>false</c>
    /// (default) the content is in the tree from the start and only hidden while collapsed.
    /// </summary>
    public bool LazyContentExpansion
    {
        get => (bool)GetValue(LazyContentExpansionProperty);
        set => SetValue(LazyContentExpansionProperty, value);
    }

    /// <summary>
    /// Length of the expand and collapse animation in milliseconds; 0 (default) shows and hides the content at once. The
    /// content is scaled vertically from the header's side and the expander's height follows it, with
    /// <see cref="AnimationEasing"/>. Not animated while
    /// the system reduces motion (<see cref="SkUiMotion"/>) or before the expander is first drawn.
    /// </summary>
    public uint AnimationLength
    {
        get => (uint)GetValue(AnimationLengthProperty);
        set => SetValue(AnimationLengthProperty, value);
    }

    /// <summary>
    /// Easing of the expand and collapse animation (default <see cref="Easing.CubicInOut"/>; <c>null</c> is linear),
    /// applied the same way in both directions. Easings that overshoot (<see cref="Easing.SpringOut"/>,
    /// <see cref="Easing.BounceOut"/>) stretch the content past its size for a moment; the content never shows less
    /// than nothing.
    /// </summary>
    public Easing? AnimationEasing
    {
        get => (Easing?)GetValue(AnimationEasingProperty);
        set => SetValue(AnimationEasingProperty, value);
    }

    /// <summary>Inset around the header and content.</summary>
    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>Whether the content is expanding or collapsing (raises <c>PropertyChanged</c> when it starts and ends).</summary>
    public bool IsAnimating => _isAnimating;

    /// <summary>
    /// The value screen readers read after a header's text, for expanded (<c>true</c>) and collapsed: "Expanded" /
    /// "Collapsed" by default. Set it once at startup to localize (it is called each time the semantics are read, so it
    /// may follow the current culture).
    /// </summary>
    public static Func<bool, string> ExpandedStateText
    {
        get => _expandedStateText;
        set => _expandedStateText = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Raised when <see cref="IsExpanded"/> changes (when it changes, not when an animation ends), after
    /// <see cref="Command"/>; also when the command throws, so the app always learns of the change.
    /// </summary>
    public event EventHandler<SkUiExpandedChangedEventArgs>? ExpandedChanged;

    /// <summary>Sets <see cref="Header"/> (same as the property setter).</summary>
    public SkUiExpander SetHeader(ISkUiView? value)
    {
        if (!ReferenceEquals(Header, value) && value is not null) ValidateChild(value);
        Header = value;
        return this;
    }

    /// <summary>Sets <see cref="Content"/> (same as the property setter).</summary>
    public SkUiExpander SetContent(ISkUiView? value)
    {
        if (!ReferenceEquals(Content, value) && value is not null) ValidateChild(value);
        Content = value;
        return this;
    }

    /// <summary>Sets <see cref="IsExpanded"/> (same as the property setter).</summary>
    public SkUiExpander SetIsExpanded(bool value) { IsExpanded = value; return this; }

    /// <summary>Sets <see cref="Direction"/> (same as the property setter).</summary>
    public SkUiExpander SetDirection(SkUiExpandDirection value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        Direction = value;
        return this;
    }

    /// <summary>Sets <see cref="Padding"/> (same as the property setter).</summary>
    public SkUiExpander SetPadding(Thickness value) { Padding = value; return this; }

    /// <summary>The header's host (tests).</summary>
    internal SkUiView HeaderHost => _headerPart;

    /// <summary>The content's host, scaled while animating (tests).</summary>
    internal SkUiView ContentHost => _contentPart;

    /// <summary>How far the content is shown, from 0 (collapsed) to 1 (expanded); past 1 while an easing overshoots, never below 0.</summary>
    internal double Reveal => Math.Max(0, _reveal.Value);

    // The content is in the tree (non-lazy), or expanded or still collapsing (lazy); visible while expanded or collapsing.
    private bool ShowsContent => _expanded || _reveal.Value > 0;

    /// <summary>Attaches or detaches the content and shows or hides its host for the current state.</summary>
    private void RefreshContent()
    {
        var attach = !_lazy || ShowsContent;
        if (attach && Parent is not null)
            _slot.Ensure();
        _contentPart.Content = attach ? Content : null;
        _contentPart.IsVisible = ShowsContent;
    }

    private void OnContentChanged(ISkUiView? value)
    {
        _slot.OnContentChanged();
        // Cleared by the app: the template (if any) provides the content again.
        if (value is null && !_slot.IsSetting)
            RefreshContent();
        else
            _contentPart.Content = !_lazy || ShowsContent ? value : null;
    }

    private void OnContentTemplateChanged()
    {
        _slot.OnTemplateChanged();
        RefreshContent();
    }

    private void OnLazyContentExpansionChanged(bool value)
    {
        _lazy = value;
        RefreshContent();
    }

    private void OnIsExpandedChanged(bool value)
    {
        _expanded = value;
        RefreshContent();
        var length = SkUiMotion.IsMotionReduced ? 0 : _animationLength;
        var target = value ? 1f : 0f;
        _reveal.AnimateTo(target, length == 0 ? SkUiTransition.None : SkUiTransition.FromMilliseconds(length, _animationEasing ?? Easing.Linear),
            _ =>
            {
                RefreshContent();
                UpdateIsAnimating();
            }, durationScale: Math.Abs(target - _reveal.Value));
        UpdateIsAnimating();
        _headerPart.OnExpandedChanged();
        // The value and the views have changed: the app is told even when its command throws.
        try
        {
            if (Command is { } command && command.CanExecute(CommandParameter))
                command.Execute(CommandParameter);
        }
        finally
        {
            ExpandedChanged?.Invoke(this, new SkUiExpandedChangedEventArgs(value));
        }
    }

    private void UpdateIsAnimating()
    {
        if (_isAnimating == _reveal.IsRunning)
            return;
        _isAnimating = _reveal.IsRunning;
        OnPropertyChanged(nameof(IsAnimating));
    }

    private void OnDirectionChanged(SkUiExpandDirection value)
    {
        _direction = value;
        _contentPart.AnchorY = value == SkUiExpandDirection.Down ? 0 : 1; // the content grows from the header's side
        InvalidateRender(SkUiRenderDirty.Children);
        InvalidateMeasureOverride();
    }

    private void OnPaddingChanged(Thickness value)
    {
        _padding = value;
        InvalidateMeasureOverride();
    }

    // Each animation frame: the content's scale (composite-time; it also requests the frame) and the expander's height:
    // a relayout of the expander and its ancestors that re-records only what changed size. The expander draws nothing
    // that depends on the reveal, so its own picture is not invalidated (its size change re-records it), and as a
    // surface root it is laid out in place, asking the native layout only when its size changes.
    private void OnRevealChanged()
    {
        _contentPart.ScaleY = Reveal;
        InvalidateMeasureFromChild();
    }

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        base.OnParentSet();
        // Template content is created once the expander is in a tree, so every property set before applies first.
        if (Parent is not null)
            RefreshContent();
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (_slot.Reselect())
            RefreshContent();
    }

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren
    {
        get
        {
            // Paint and focus order follow the screen order.
            if (_direction == SkUiExpandDirection.Down)
            {
                yield return _headerPart;
                yield return _contentPart;
            }
            else
            {
                yield return _contentPart;
                yield return _headerPart;
            }
        }
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        // As the toolkit's two Auto grid rows: each part is measured with an unbounded height. The content is measured
        // at its full size (the same constraint every animation frame, so its cached measure is reused) and counts by
        // how far it is shown.
        var width = Math.Max(0, widthConstraint - _padding.HorizontalThickness);
        var header = ((IView)_headerPart).Measure(width, double.PositiveInfinity);
        var body = _contentPart.IsVisible ? ((IView)_contentPart).Measure(width, double.PositiveInfinity) : Size.Zero;
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
        var headerHeight = ((IView)_headerPart).DesiredSize.Height;
        var bodyHeight = _contentPart.IsVisible ? ((IView)_contentPart).DesiredSize.Height : 0;
        var shown = bodyHeight * Reveal;
        if (_direction == SkUiExpandDirection.Down)
        {
            ((IView)_headerPart).Arrange(new Rect(x, _padding.Top, width, headerHeight));
            ((IView)_contentPart).Arrange(new Rect(x, _padding.Top + headerHeight, width, bodyHeight));
        }
        else
        {
            ((IView)_contentPart).Arrange(new Rect(x, _padding.Top + shown - bodyHeight, width, bodyHeight));
            ((IView)_headerPart).Arrange(new Rect(x, _padding.Top + shown, width, headerHeight));
        }
        // Native views in the content are clipped to the expander while it animates (they cannot be scaled): follow
        // the clip as the height changes, and drop it after the last frame.
        if (_contentPart.IsVisible && SurfaceHostsNativeViews)
            _contentPart.NotifyMoved();
    }

    /// <inheritdoc />
    internal override bool ClipsHostedViews => base.ClipsHostedViews || _reveal.IsRunning;

    private sealed class RevealHost(SkUiExpander owner) : ISkUiTransitionHost
    {
        public SkUiAnimationClock? TransitionClock => ((ISkUiTransitionHost)owner).TransitionClock;

        public void InvalidateTransition() => owner.OnRevealChanged();
    }

    /// <summary>Hosts the header and toggles the expander when tapped; read as a button with an expanded / collapsed value.</summary>
    private sealed class HeaderPart(SkUiExpander owner) : SkUiContentView
    {
        protected override bool HandlesTap => Content is not null;

        protected override void OnTapped(SkUiTappedEventArgs args)
        {
            RaiseTapped(args);
            owner.SetValue(IsExpandedProperty, !owner.IsExpanded);
        }

        protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
        {
            base.OnPopulateSemantics(info);
            if (Content is null)
                return;
            info.Role = SkUiSemanticsRole.Button;
            info.Value = ExpandedStateText(owner.IsExpanded);
        }

        public void OnExpandedChanged() => InvalidateSemantics();
    }

    /// <summary>Hosts the content; the expander owns its scale and anchor, so the content's own are never touched.</summary>
    private sealed class ContentPart : SkUiContentView;
}
