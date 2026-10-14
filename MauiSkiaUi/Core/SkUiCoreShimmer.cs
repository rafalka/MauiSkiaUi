using System.Diagnostics;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core skeleton loading view (the twin of <see cref="SkUiShimmer"/>, FR-31): while <see cref="IsLoading"/> (the default) it
/// shows <see cref="SkeletonContent"/>, a skeleton of Core nodes (boxes, borders, shapes: the bones), animated by
/// <see cref="Effect"/>; once it is no longer loading it shows <see cref="Content"/>. The same pixels and the same render-thread effect as
/// <see cref="SkUiShimmer"/>: the bones are recorded once and nothing is recorded per frame.
/// </summary>
/// <remarks>
/// The recommended way to build loading screens: plain Core bones (no bindings, no bindable store) inside one shimmer per
/// skeleton, hosted in an <see cref="SkUiCoreHost"/>. The content that is not shown stays owned by the shimmer, detached
/// from the drawn tree (its drawing resources are released).
/// </remarks>
public class SkUiCoreShimmer : SkUiCoreNode, ISkUiShimmer
{
    private readonly SkUiShimmerStyles _styles;
    private SkUiDrawableSlot<SkUiShimmerStyle> _silhouette;
    private SkUiShimmerSettings _settings = SkUiShimmerSettings.Default;
    private SkUiCoreNode? _content;
    private SkUiCoreNode? _skeletonContent;
    private SkUiCoreNode? _attached;
    private Thickness _padding;
    private bool _isLoading = true;
    private bool _shown = true;
    private bool _animating;
    private SkUiViewAnimation? _beforeStateChangeAnimation;
    private SkUiViewAnimation? _afterStateChangeAnimation;

    /// <summary>Creates a loading shimmer; it does not clip its child by default.</summary>
    public SkUiCoreShimmer()
    {
        InitClipToBounds(false);
        _styles = new SkUiShimmerStyles(this);
    }

    /// <summary>The content shown once the shimmer is no longer loading.</summary>
    public SkUiCoreNode? Content { get => _content; set => SetContent(value); }

    /// <summary>The skeleton shown while <see cref="IsLoading"/>: Core nodes whose shapes are the bones.</summary>
    public SkUiCoreNode? SkeletonContent { get => _skeletonContent; set => SetSkeletonContent(value); }

    /// <summary>Inset around the shown child in DIPs.</summary>
    public Thickness Padding { get => _padding; set => SetPadding(value); }

    /// <summary>Shows the skeleton (<c>true</c>, the default) or the content (see <see cref="SkUiShimmer.IsLoading"/>).</summary>
    public bool IsLoading { get => _isLoading; set => SetIsLoading(value); }

    /// <summary>How the skeleton animates (default <see cref="SkUiShimmerEffect.Wave"/>).</summary>
    public SkUiShimmerEffect Effect { get => _settings.Effect; set => SetEffect(value); }

    /// <summary>
    /// What of the skeleton animates: its whole silhouette (default) or only its <see cref="SkUiCoreShimmerBone"/>s, with
    /// everything else drawn as it is (<see cref="SkUiShimmerMode.Bones"/>).
    /// </summary>
    public SkUiShimmerMode Mode { get => _settings.Mode; set => SetMode(value); }

    /// <summary>The bones' color (<c>null</c>: the look's); transparent keeps the bones' own colors.</summary>
    public Color? BoneColor { get => _settings.BoneColor; set => SetBoneColor(value); }

    /// <summary>The wave's color, and the color a <see cref="SkUiShimmerEffect.Color"/> shift goes to (<c>null</c>: the look's).</summary>
    public Color? HighlightColor { get => _settings.HighlightColor; set => SetHighlightColor(value); }

    /// <summary>The wave band's width in DIPs (default 200).</summary>
    public double WaveWidth { get => _settings.WaveWidth; set => SetWaveWidth(value); }

    /// <summary>Where the wave moves (default: the reading direction, tilted by the look).</summary>
    public SkUiShimmerWaveDirection WaveDirection { get => _settings.WaveDirection; set => SetWaveDirection(value); }

    /// <summary>The area one wave crosses: this shimmer (default) or the whole surface, shared with the other shimmers on it.</summary>
    public SkUiShimmerWaveRegion WaveRegion { get => _settings.WaveRegion; set => SetWaveRegion(value); }

    /// <summary>The length of one pass of the effect in milliseconds (default 1000).</summary>
    public double Duration { get => _settings.Duration; set => SetDuration(value); }

    /// <summary>What a pass does when it ends (default: the effect's own).</summary>
    public SkUiShimmerRepeatMode RepeatMode { get => _settings.RepeatMode; set => SetRepeatMode(value); }

    /// <summary>The curve of each pass (<c>null</c>: the effect's own); runs on the render thread.</summary>
    public Easing? Easing { get => _settings.Easing; set => SetEasing(value); }

    /// <summary>The bones' opacity at the low point of a <see cref="SkUiShimmerEffect.Pulse"/>, 0–1 (default 0.4).</summary>
    public double PulseOpacity { get => _settings.PulseOpacity; set => SetPulseOpacity(value); }

    /// <summary>Runs on the shown child before each switch (render thread; uniform <c>Scale</c> only). With either animation set, switches animate.</summary>
    public SkUiViewAnimation? BeforeStateChangeAnimation { get => _beforeStateChangeAnimation; set => SetBeforeStateChangeAnimation(value); }

    /// <summary>Runs on the new child after each switch (render thread; uniform <c>Scale</c> only).</summary>
    public SkUiViewAnimation? AfterStateChangeAnimation { get => _afterStateChangeAnimation; set => SetAfterStateChangeAnimation(value); }

    /// <summary>Whether the skeleton is shown (it follows <see cref="IsLoading"/>, after a switch animation); raises <c>PropertyChanged</c>.</summary>
    public bool IsShowingSkeleton => _shown;

    /// <summary>Whether a switch is animating.</summary>
    public bool IsSwitching => _animating;

    /// <summary>The effect settings (tests).</summary>
    internal SkUiShimmerSettings Settings => _settings;

    /// <summary>Sets the content shown once no longer loading.</summary>
    public SkUiCoreShimmer SetContent(SkUiCoreNode? value)
    {
        if (ReferenceEquals(_content, value)) return this;
        ValidateChild(value, _skeletonContent);
        _content = value;
        OnPropertyChanged(nameof(Content));
        AttachShown();
        return this;
    }

    /// <summary>Sets the skeleton shown while loading.</summary>
    public SkUiCoreShimmer SetSkeletonContent(SkUiCoreNode? value)
    {
        if (ReferenceEquals(_skeletonContent, value)) return this;
        ValidateChild(value, _content);
        _skeletonContent = value;
        OnPropertyChanged(nameof(SkeletonContent));
        AttachShown();
        return this;
    }

    /// <summary>Sets the padding; invalidates measure when the value changes.</summary>
    public SkUiCoreShimmer SetPadding(Thickness value)
    {
        if (SetProperty(ref _padding, value, nameof(Padding))) InvalidateMeasure();
        return this;
    }

    /// <summary>Shows the skeleton (<c>true</c>) or the content; animates with the state change animations once drawn.</summary>
    public SkUiCoreShimmer SetIsLoading(bool value)
    {
        if (!SetProperty(ref _isLoading, value, nameof(IsLoading))) return this;
        if (_animating)
            return this; // the running switch picks the new value up
        if ((_beforeStateChangeAnimation is not null || _afterStateChangeAnimation is not null) && RenderState.HasCommitted)
            _ = RunSwitches();
        else
            Switch();
        return this;
    }

    /// <summary>Sets <see cref="Effect"/>.</summary>
    public SkUiCoreShimmer SetEffect(SkUiShimmerEffect value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        return UpdateSettings(_settings with { Effect = value }, nameof(Effect));
    }

    /// <summary>Sets <see cref="Mode"/>.</summary>
    public SkUiCoreShimmer SetMode(SkUiShimmerMode value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        return UpdateSettings(_settings with { Mode = value }, nameof(Mode));
    }

    /// <summary>Sets <see cref="BoneColor"/> (<c>null</c>: the look's).</summary>
    public SkUiCoreShimmer SetBoneColor(Color? value) => UpdateSettings(_settings with { BoneColor = value }, nameof(BoneColor));

    /// <summary>Sets <see cref="HighlightColor"/> (<c>null</c>: the look's).</summary>
    public SkUiCoreShimmer SetHighlightColor(Color? value) => UpdateSettings(_settings with { HighlightColor = value }, nameof(HighlightColor));

    /// <summary>Sets <see cref="WaveWidth"/>.</summary>
    public SkUiCoreShimmer SetWaveWidth(double value)
    {
        SkUiShimmerSettings.ThrowIfInvalidWaveWidth(value, nameof(value));
        return UpdateSettings(_settings with { WaveWidth = value }, nameof(WaveWidth));
    }

    /// <summary>Sets <see cref="WaveDirection"/>.</summary>
    public SkUiCoreShimmer SetWaveDirection(SkUiShimmerWaveDirection value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        return UpdateSettings(_settings with { WaveDirection = value }, nameof(WaveDirection));
    }

    /// <summary>Sets <see cref="WaveRegion"/>.</summary>
    public SkUiCoreShimmer SetWaveRegion(SkUiShimmerWaveRegion value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        return UpdateSettings(_settings with { WaveRegion = value }, nameof(WaveRegion));
    }

    /// <summary>Sets <see cref="Duration"/> in milliseconds.</summary>
    public SkUiCoreShimmer SetDuration(double value)
    {
        SkUiShimmerSettings.ThrowIfInvalidDuration(value, nameof(value));
        return UpdateSettings(_settings with { Duration = value }, nameof(Duration));
    }

    /// <summary>Sets <see cref="RepeatMode"/>.</summary>
    public SkUiCoreShimmer SetRepeatMode(SkUiShimmerRepeatMode value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        return UpdateSettings(_settings with { RepeatMode = value }, nameof(RepeatMode));
    }

    /// <summary>Sets <see cref="Easing"/> (<c>null</c>: the effect's own).</summary>
    public SkUiCoreShimmer SetEasing(Easing? value) => UpdateSettings(_settings with { Easing = value }, nameof(Easing));

    /// <summary>Sets <see cref="PulseOpacity"/>.</summary>
    public SkUiCoreShimmer SetPulseOpacity(double value)
    {
        SkUiShimmerSettings.ThrowIfInvalidPulseOpacity(value, nameof(value));
        return UpdateSettings(_settings with { PulseOpacity = value }, nameof(PulseOpacity));
    }

    /// <summary>Sets <see cref="BeforeStateChangeAnimation"/>.</summary>
    public SkUiCoreShimmer SetBeforeStateChangeAnimation(SkUiViewAnimation? value)
    {
        ValidateAnimation(value, nameof(value));
        SetProperty(ref _beforeStateChangeAnimation, value, nameof(BeforeStateChangeAnimation));
        return this;
    }

    /// <summary>Sets <see cref="AfterStateChangeAnimation"/>.</summary>
    public SkUiCoreShimmer SetAfterStateChangeAnimation(SkUiViewAnimation? value)
    {
        ValidateAnimation(value, nameof(value));
        SetProperty(ref _afterStateChangeAnimation, value, nameof(AfterStateChangeAnimation));
        return this;
    }

    private static void ValidateAnimation(SkUiViewAnimation? animation, string name)
    {
        if (animation is null)
            return;
        animation.Freeze();
        foreach (var property in animation.Properties)
            if (property.Property is SkUiAnimatableProperty.ScaleX or SkUiAnimatableProperty.ScaleY)
                throw new ArgumentException($"Core nodes scale uniformly: animate {nameof(SkUiAnimatableProperty.Scale)} instead of {property.Property}.", name);
    }

    private SkUiCoreShimmer UpdateSettings(SkUiShimmerSettings value, string propertyName)
    {
        if (value == _settings) return this;
        _settings = value;
        OnPropertyChanged(propertyName);
        UpdateStyle(Frame.Size);
        return this;
    }

    private void ValidateChild(SkUiCoreNode? value, SkUiCoreNode? other)
    {
        if (value is null)
            return;
        if (ReferenceEquals(value, other))
            throw new InvalidOperationException("The content and the skeleton must be different nodes.");
        if (value.Parent is not null && !ReferenceEquals(value.Parent, this))
            throw new InvalidOperationException("Child already has a parent.");
        if (WouldCreateParentCycle(value))
            throw new InvalidOperationException("Content cannot create a parent cycle.");
    }

    /// <summary>The child shown now: the skeleton while it is shown, else the content.</summary>
    internal SkUiCoreNode? ShownChild => _attached;

    private void Switch()
    {
        if (_shown == _isLoading)
            return;
        _shown = _isLoading;
        AttachShown();
        InvalidateSemantics();
        OnPropertyChanged(nameof(IsShowingSkeleton));
    }

    /// <summary>Attaches the side to show, detaching the other one (its drawing resources are released).</summary>
    private void AttachShown()
    {
        var value = _shown ? _skeletonContent : _content;
        if (ReferenceEquals(_attached, value))
        {
            InvalidateRender(SkUiRenderDirty.Props);
            return;
        }
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
    }

    // One animated switch at a time, each to the newest value (see SkUiShimmer).
    private async Task RunSwitches()
    {
        _animating = true;
        try
        {
            while (_isLoading != _shown)
            {
                await SkUiStateTransition.RunAsync(this, () => _attached is { } shown ? [shown] : [], Switch,
                    _beforeStateChangeAnimation, _afterStateChangeAnimation, CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"SkiaUi: {nameof(SkUiCoreShimmer)} could not animate the switch to the {(_isLoading ? "skeleton" : "content")}: {exception.Message}");
            _animating = false;
            Switch();
        }
        finally
        {
            _animating = false;
        }
    }

    /// <summary>Makes the style for the current settings, size and direction; a new one reaches the drawables of the shimmer and its bones.</summary>
    private void UpdateStyle(Size size)
    {
        if (!_styles.Update(_settings, size, IsRightToLeft))
            return;
        InvalidateRender(SkUiRenderDirty.Props);
        SkUiShimmerStyles.InvalidateBones(this);
    }

    /// <inheritdoc />
    SkUiShimmerStyle? ISkUiShimmer.GetBoneStyle()
    {
        if (_settings.Mode != SkUiShimmerMode.Bones)
            return null;
        _styles.Update(_settings, Frame.Size, IsRightToLeft); // the look's colors may have changed
        return _styles.Current;
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
    protected override void ArrangeContent(Size size)
    {
        _attached?.Arrange(new Rect(_padding.Left, _padding.Top,
            Math.Max(0, size.Width - _padding.HorizontalThickness), Math.Max(0, size.Height - _padding.VerticalThickness)));
        UpdateStyle(size);
    }

    /// <inheritdoc />
    internal override void OnGetRenderProps(ref SkUiRenderProps props)
    {
        base.OnGetRenderProps(ref props);
        if (!_shown || _attached is null)
            return;
        // The space the wave crosses; in a silhouette, the skeleton as one group with the effect drawn over it.
        props.DrawableSpace = true;
        if (_settings.Mode != SkUiShimmerMode.Silhouette)
            return;
        _styles.Update(_settings, new Size(props.Width, props.Height), IsRightToLeft); // the look's colors may have changed
        props.ChildrenLayer = true;
        props.Drawable = _silhouette.Get(_styles.Current!, _styles.Current!, static style => new SkUiShimmerSilhouette(style));
    }

    /// <inheritdoc />
    internal override void ReleaseDrawingResources()
    {
        base.ReleaseDrawingResources();
        _silhouette.Forget();
    }

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged()
    {
        base.OnEffectiveFlowDirectionChanged();
        UpdateStyle(Frame.Size);
    }

    /// <inheritdoc />
    internal override IReadOnlyList<IVisualTreeElement> VisualChildren => _attached is null ? [] : [_attached];

    /// <inheritdoc />
    internal override void AddRenderChildren(List<ISkUiRenderable> children)
    {
        if (_attached is not null)
            children.Add(_attached);
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        if (!_shown)
            return;
        // One element instead of the bones.
        info.Role = SkUiSemanticsRole.ProgressBar;
        info.Text = SkUiStrings.Current.LoadingText;
        info.ChildrenHidden = true;
    }
}
