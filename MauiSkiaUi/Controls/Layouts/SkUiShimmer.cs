using System.ComponentModel;
using System.Diagnostics;
using MauiSkiaUi.Rendering;
using Microsoft.Maui.Converters;

namespace MauiSkiaUi;

/// <summary>
/// A skeleton loading view (FR-31): while <see cref="IsLoading"/> (the default) it shows <see cref="SkeletonContent"/>, a
/// skeleton of drawn views (boxes, rounded rectangles, ellipses, borders: the bones), animated by <see cref="Effect"/>; once
/// it is no longer loading it shows <see cref="SkUiContentView.Content"/>.
/// </summary>
/// <remarks>
/// <para>
/// In a <see cref="SkUiShimmerMode.Silhouette"/> (default) the bones are recorded once and drawn in <see cref="BoneColor"/>
/// (their own colors are replaced; their shapes and opacity stay); with <see cref="SkUiShimmerMode.Bones"/> only the
/// <see cref="SkUiShimmerBone"/>s animate and the rest of the skeleton (cards, dividers, buttons) draws as it is. The effect
/// runs on the render thread: a band of <see cref="HighlightColor"/> moving across the bones
/// (<see cref="SkUiShimmerEffect.Wave"/>), an opacity pulse, or a color shift. Nothing is recorded and the UI thread does
/// nothing per frame; a silhouette costs one layer per frame however many bones it has, bones no layer, and every shimmer
/// with the same timing is in phase, on any surface. While the system reduces motion the bones stand still.
/// </para>
/// <para>
/// Screen readers read one "Loading" element (<see cref="SkUiStrings.LoadingText"/>) instead of the bones. With
/// <see cref="BeforeStateChangeAnimation"/> / <see cref="AfterStateChangeAnimation"/> the switch to the content (and back)
/// animates on the render thread (e.g. <c>"FadeOut 350 CubicInOut"</c>).
/// </para>
/// </remarks>
[ContentProperty(nameof(Content))]
public class SkUiShimmer : SkUiContentView, ISkUiShimmer
{
    private readonly SkUiContentSlot _skeletonSlot;
    private readonly SkUiShimmerStyles _styles;
    private SkUiDrawableSlot<SkUiShimmerStyle> _silhouette;
    private SkUiShimmerSettings _settings = SkUiShimmerSettings.Default;
    private bool _shown = true;  // the skeleton is on screen
    private bool _target = true; // the side animated changes go to
    private bool _animating;
    private bool _contentSkeleton; // registered as showing its content as a skeleton

    /// <summary>Bindable <see cref="SkeletonContent"/>.</summary>
    public static readonly BindableProperty SkeletonContentProperty = BindableProperty.Create(
        nameof(SkeletonContent), typeof(ISkUiView), typeof(SkUiShimmer), null,
        validateValue: (bindable, value) =>
        {
            if (value is ISkUiView child && !ReferenceEquals(((SkUiShimmer)bindable).SkeletonContent, child))
                ((SkUiShimmer)bindable).ValidateChild(child);
            return true;
        },
        propertyChanged: (bindable, _, newValue) => ((SkUiShimmer)bindable).OnSkeletonContentChanged((ISkUiView?)newValue));

    /// <summary>Bindable <see cref="SkeletonContentTemplate"/>.</summary>
    public static readonly BindableProperty SkeletonContentTemplateProperty = BindableProperty.Create(
        nameof(SkeletonContentTemplate), typeof(DataTemplate), typeof(SkUiShimmer), null,
        propertyChanged: (bindable, _, _) => ((SkUiShimmer)bindable).OnSkeletonContentTemplateChanged());

    /// <summary>
    /// Attached: how a view draws in a skeleton made from a shimmer's content (default <see cref="SkUiSkeleton.Default"/>:
    /// its own bones). XAML: <c>sk:SkUiShimmer.Skeleton="Keep"</c>.
    /// </summary>
    public static readonly BindableProperty SkeletonProperty = BindableProperty.CreateAttached(
        "Skeleton", typeof(SkUiSkeleton), typeof(SkUiShimmer), SkUiSkeleton.Default,
        validateValue: (_, value) => Enum.IsDefined((SkUiSkeleton)value),
        propertyChanged: (bindable, _, newValue) =>
        {
            if (bindable is SkUiView view)
                view.SetSkeletonValue(bindable.IsSet(SkeletonProperty) ? (SkUiSkeleton)newValue : null);
        });

    /// <summary>
    /// Attached: the placeholder lines an empty label (or button text) shows in a skeleton made from content, measured
    /// as that many lines of its font (default 1; 0: none, the label keeps its size).
    /// </summary>
    public static readonly BindableProperty SkeletonLinesProperty = BindableProperty.CreateAttached(
        "SkeletonLines", typeof(int), typeof(SkUiShimmer), 1,
        validateValue: (_, value) => (int)value >= 0,
        propertyChanged: (bindable, _, _) =>
        {
            if (bindable is SkUiView view && SkUiSkeletonScope.IsActive)
                view.OnSkeletonChanged();
        });

    /// <summary>
    /// Attached to a collection view: how many placeholder items it shows while it is empty in a skeleton made from content,
    /// made from its item template without an item (default -1: enough rows to fill it; 0: none, its empty view shows).
    /// </summary>
    public static readonly BindableProperty SkeletonItemCountProperty = BindableProperty.CreateAttached(
        "SkeletonItemCount", typeof(int), typeof(SkUiShimmer), -1,
        validateValue: (_, value) => (int)value >= -1,
        propertyChanged: (bindable, _, _) =>
        {
            if (bindable is SkUiView view && SkUiSkeletonScope.IsActive)
                view.OnSkeletonChanged();
        });

    /// <summary>Gets <see cref="SkeletonItemCountProperty"/> of <paramref name="view"/>.</summary>
    public static int GetSkeletonItemCount(BindableObject view) => (int)view.GetValue(SkeletonItemCountProperty);

    /// <summary>Sets <see cref="SkeletonItemCountProperty"/> on <paramref name="view"/> (-1: fill the list; 0: none).</summary>
    public static void SetSkeletonItemCount(BindableObject view, int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, -1);
        view.SetValue(SkeletonItemCountProperty, value);
    }

    /// <summary>Gets <see cref="SkeletonProperty"/> of <paramref name="view"/>.</summary>
    public static SkUiSkeleton GetSkeleton(BindableObject view) => (SkUiSkeleton)view.GetValue(SkeletonProperty);

    /// <summary>Sets <see cref="SkeletonProperty"/> on <paramref name="view"/>.</summary>
    public static void SetSkeleton(BindableObject view, SkUiSkeleton value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        view.SetValue(SkeletonProperty, value);
    }

    /// <summary>Gets <see cref="SkeletonLinesProperty"/> of <paramref name="view"/>.</summary>
    public static int GetSkeletonLines(BindableObject view) => (int)view.GetValue(SkeletonLinesProperty);

    /// <summary>Sets <see cref="SkeletonLinesProperty"/> on <paramref name="view"/>.</summary>
    public static void SetSkeletonLines(BindableObject view, int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        view.SetValue(SkeletonLinesProperty, value);
    }

    /// <summary>Bindable <see cref="IsLoading"/>.</summary>
    public static readonly BindableProperty IsLoadingProperty = BindableProperty.Create(nameof(IsLoading), typeof(bool), typeof(SkUiShimmer), true,
        propertyChanged: (bindable, _, newValue) => ((SkUiShimmer)bindable).OnIsLoadingChanged((bool)newValue));

    /// <summary>Bindable <see cref="Effect"/>.</summary>
    public static readonly BindableProperty EffectProperty = BindableProperty.Create(nameof(Effect), typeof(SkUiShimmerEffect), typeof(SkUiShimmer), SkUiShimmerEffect.Wave,
        validateValue: (_, value) => Enum.IsDefined((SkUiShimmerEffect)value),
        propertyChanged: (bindable, _, newValue) => ((SkUiShimmer)bindable).UpdateSettings(settings => settings with { Effect = (SkUiShimmerEffect)newValue }));

    /// <summary>Bindable <see cref="Mode"/>.</summary>
    public static readonly BindableProperty ModeProperty = BindableProperty.Create(nameof(Mode), typeof(SkUiShimmerMode), typeof(SkUiShimmer), SkUiShimmerMode.Silhouette,
        validateValue: (_, value) => Enum.IsDefined((SkUiShimmerMode)value),
        propertyChanged: (bindable, _, newValue) => ((SkUiShimmer)bindable).UpdateSettings(settings => settings with { Mode = (SkUiShimmerMode)newValue }));

    /// <summary>Bindable <see cref="BoneColor"/>.</summary>
    public static readonly BindableProperty BoneColorProperty = BindableProperty.Create(nameof(BoneColor), typeof(Color), typeof(SkUiShimmer), null,
        propertyChanged: (bindable, _, newValue) => ((SkUiShimmer)bindable).UpdateSettings(settings => settings with { BoneColor = (Color?)newValue }));

    /// <summary>Bindable <see cref="HighlightColor"/>.</summary>
    public static readonly BindableProperty HighlightColorProperty = BindableProperty.Create(nameof(HighlightColor), typeof(Color), typeof(SkUiShimmer), null,
        propertyChanged: (bindable, _, newValue) => ((SkUiShimmer)bindable).UpdateSettings(settings => settings with { HighlightColor = (Color?)newValue }));

    /// <summary>Bindable <see cref="WaveWidth"/>.</summary>
    public static readonly BindableProperty WaveWidthProperty = BindableProperty.Create(nameof(WaveWidth), typeof(double), typeof(SkUiShimmer), SkUiShimmerSettings.DefaultWaveWidth,
        validateValue: (_, value) => SkUiShimmerSettings.IsValidWaveWidth((double)value),
        propertyChanged: (bindable, _, newValue) => ((SkUiShimmer)bindable).UpdateSettings(settings => settings with { WaveWidth = (double)newValue }));

    /// <summary>Bindable <see cref="WaveDirection"/>.</summary>
    public static readonly BindableProperty WaveDirectionProperty = BindableProperty.Create(nameof(WaveDirection), typeof(SkUiShimmerWaveDirection), typeof(SkUiShimmer), SkUiShimmerWaveDirection.Default,
        validateValue: (_, value) => Enum.IsDefined((SkUiShimmerWaveDirection)value),
        propertyChanged: (bindable, _, newValue) => ((SkUiShimmer)bindable).UpdateSettings(settings => settings with { WaveDirection = (SkUiShimmerWaveDirection)newValue }));

    /// <summary>Bindable <see cref="WaveRegion"/>.</summary>
    public static readonly BindableProperty WaveRegionProperty = BindableProperty.Create(nameof(WaveRegion), typeof(SkUiShimmerWaveRegion), typeof(SkUiShimmer), SkUiShimmerWaveRegion.Shimmer,
        validateValue: (_, value) => Enum.IsDefined((SkUiShimmerWaveRegion)value),
        propertyChanged: (bindable, _, newValue) => ((SkUiShimmer)bindable).UpdateSettings(settings => settings with { WaveRegion = (SkUiShimmerWaveRegion)newValue }));

    /// <summary>Bindable <see cref="Duration"/>.</summary>
    public static readonly BindableProperty DurationProperty = BindableProperty.Create(nameof(Duration), typeof(double), typeof(SkUiShimmer), SkUiShimmerSettings.DefaultDuration,
        validateValue: (_, value) => SkUiShimmerSettings.IsValidDuration((double)value),
        propertyChanged: (bindable, _, newValue) => ((SkUiShimmer)bindable).UpdateSettings(settings => settings with { Duration = (double)newValue }));

    /// <summary>Bindable <see cref="RepeatMode"/>.</summary>
    public static readonly BindableProperty RepeatModeProperty = BindableProperty.Create(nameof(RepeatMode), typeof(SkUiShimmerRepeatMode), typeof(SkUiShimmer), SkUiShimmerRepeatMode.Default,
        validateValue: (_, value) => Enum.IsDefined((SkUiShimmerRepeatMode)value),
        propertyChanged: (bindable, _, newValue) => ((SkUiShimmer)bindable).UpdateSettings(settings => settings with { RepeatMode = (SkUiShimmerRepeatMode)newValue }));

    /// <summary>Bindable <see cref="Easing"/>.</summary>
    public static readonly BindableProperty EasingProperty = BindableProperty.Create(nameof(Easing), typeof(Easing), typeof(SkUiShimmer), null,
        propertyChanged: (bindable, _, newValue) => ((SkUiShimmer)bindable).UpdateSettings(settings => settings with { Easing = (Easing?)newValue }));

    /// <summary>Bindable <see cref="PulseOpacity"/>.</summary>
    public static readonly BindableProperty PulseOpacityProperty = BindableProperty.Create(nameof(PulseOpacity), typeof(double), typeof(SkUiShimmer), SkUiShimmerSettings.DefaultPulseOpacity,
        validateValue: (_, value) => SkUiShimmerSettings.IsValidPulseOpacity((double)value),
        propertyChanged: (bindable, _, newValue) => ((SkUiShimmer)bindable).UpdateSettings(settings => settings with { PulseOpacity = (double)newValue }));

    /// <summary>Bindable <see cref="BeforeStateChangeAnimation"/> (XAML: <c>"FadeOut"</c>).</summary>
    public static readonly BindableProperty BeforeStateChangeAnimationProperty = BindableProperty.Create(
        nameof(BeforeStateChangeAnimation), typeof(SkUiViewAnimation), typeof(SkUiShimmer), null,
        propertyChanged: (_, _, newValue) => (newValue as SkUiViewAnimation)?.Freeze());

    /// <summary>Bindable <see cref="AfterStateChangeAnimation"/> (XAML: <c>"FadeIn"</c>).</summary>
    public static readonly BindableProperty AfterStateChangeAnimationProperty = BindableProperty.Create(
        nameof(AfterStateChangeAnimation), typeof(SkUiViewAnimation), typeof(SkUiShimmer), null,
        propertyChanged: (_, _, newValue) => (newValue as SkUiViewAnimation)?.Freeze());

    /// <summary>Creates a loading shimmer (it shows its skeleton until <see cref="IsLoading"/> is set to <c>false</c>).</summary>
    public SkUiShimmer()
    {
        _skeletonSlot = new SkUiContentSlot(this, SkeletonContentProperty, SkeletonContentTemplateProperty);
        _styles = new SkUiShimmerStyles(this);
    }

    /// <summary>
    /// The skeleton shown while <see cref="IsLoading"/>: drawn views of either layer (an <c>SkUiCoreHost</c> for Core bones).
    /// Without one (nor a <see cref="SkeletonContentTemplate"/>), the content itself is the skeleton: every control in it
    /// draws its bones (<see cref="SkeletonProperty"/>).
    /// </summary>
    public ISkUiView? SkeletonContent
    {
        get => (ISkUiView?)GetValue(SkeletonContentProperty);
        set => SetValue(SkeletonContentProperty, value);
    }

    /// <summary>Creates <see cref="SkeletonContent"/> when it is not set, the first time the skeleton is shown.</summary>
    public DataTemplate? SkeletonContentTemplate
    {
        get => (DataTemplate?)GetValue(SkeletonContentTemplateProperty);
        set => SetValue(SkeletonContentTemplateProperty, value);
    }

    /// <summary>
    /// Shows the skeleton (<c>true</c>, the default: a shimmer is usually created while its data loads) or the content. With state change animations set, the
    /// value changes at once and the views follow.
    /// </summary>
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>How the skeleton animates (default <see cref="SkUiShimmerEffect.Wave"/>).</summary>
    public SkUiShimmerEffect Effect { get => (SkUiShimmerEffect)GetValue(EffectProperty); set => SetValue(EffectProperty, value); }

    /// <summary>
    /// What of the skeleton animates: its whole silhouette (default) or only its <see cref="SkUiShimmerBone"/>s, with
    /// everything else (cards, dividers, buttons) drawn as it is (<see cref="SkUiShimmerMode.Bones"/>).
    /// </summary>
    public SkUiShimmerMode Mode { get => (SkUiShimmerMode)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    /// <summary>
    /// The bones' color (<c>null</c>: the look's, from the color scheme, <see cref="SkUiLook.GetShimmerColors"/>); transparent
    /// keeps the bones' own colors.
    /// </summary>
    public Color? BoneColor { get => (Color?)GetValue(BoneColorProperty); set => SetValue(BoneColorProperty, value); }

    /// <summary>The wave's color, and the color a <see cref="SkUiShimmerEffect.Color"/> shift goes to (<c>null</c>: the look's).</summary>
    public Color? HighlightColor { get => (Color?)GetValue(HighlightColorProperty); set => SetValue(HighlightColorProperty, value); }

    /// <summary>The wave band's width in DIPs (default 200).</summary>
    public double WaveWidth { get => (double)GetValue(WaveWidthProperty); set => SetValue(WaveWidthProperty, value); }

    /// <summary>Where the wave moves (default: the reading direction, tilted by the look).</summary>
    public SkUiShimmerWaveDirection WaveDirection { get => (SkUiShimmerWaveDirection)GetValue(WaveDirectionProperty); set => SetValue(WaveDirectionProperty, value); }

    /// <summary>The area one wave crosses: this shimmer (default) or the whole surface, shared with the other shimmers on it.</summary>
    public SkUiShimmerWaveRegion WaveRegion { get => (SkUiShimmerWaveRegion)GetValue(WaveRegionProperty); set => SetValue(WaveRegionProperty, value); }

    /// <summary>
    /// The length of one pass of the effect in milliseconds (default 1000): a wave crossing the region, a
    /// pulse fading in (a reversing cycle takes two passes).
    /// </summary>
    public double Duration { get => (double)GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <summary>What a pass does when it ends (default: the effect's own; a wave restarts, a pulse and a color shift go back).</summary>
    public SkUiShimmerRepeatMode RepeatMode { get => (SkUiShimmerRepeatMode)GetValue(RepeatModeProperty); set => SetValue(RepeatModeProperty, value); }

    /// <summary>
    /// The curve of each pass (<c>null</c>: the effect's own; a wave moves evenly, a pulse and a color shift ease in and out
    /// with <see cref="Easing.SinInOut"/>). It runs on the render thread. In XAML, an easing name such as <c>CubicInOut</c>.
    /// </summary>
    [TypeConverter(typeof(EasingTypeConverter))]
    public Easing? Easing { get => (Easing?)GetValue(EasingProperty); set => SetValue(EasingProperty, value); }

    /// <summary>The bones' opacity at the low point of a <see cref="SkUiShimmerEffect.Pulse"/>, 0–1 (default 0.4).</summary>
    public double PulseOpacity { get => (double)GetValue(PulseOpacityProperty); set => SetValue(PulseOpacityProperty, value); }

    /// <summary>Runs on the shown content before each switch (render thread). With either animation set, switches animate.</summary>
    public SkUiViewAnimation? BeforeStateChangeAnimation
    {
        get => (SkUiViewAnimation?)GetValue(BeforeStateChangeAnimationProperty);
        set => SetValue(BeforeStateChangeAnimationProperty, value);
    }

    /// <summary>Runs on the new content after each switch (render thread).</summary>
    public SkUiViewAnimation? AfterStateChangeAnimation
    {
        get => (SkUiViewAnimation?)GetValue(AfterStateChangeAnimationProperty);
        set => SetValue(AfterStateChangeAnimationProperty, value);
    }

    /// <summary>
    /// Whether the skeleton is shown (it follows <see cref="IsLoading"/>, after a switch animation); raises
    /// <c>PropertyChanged</c>, so a skeleton without content can hide itself once its fade-out ended.
    /// </summary>
    public bool IsShowingSkeleton => _shown;

    /// <summary>
    /// Whether the content is shown as the skeleton: loading, without <see cref="SkeletonContent"/> or
    /// <see cref="SkeletonContentTemplate"/>. The content then takes no input.
    /// </summary>
    public bool ShowsContentAsSkeleton => _shown && !HasSkeleton;

    private bool HasSkeleton => SkeletonContent is not null || SkeletonContentTemplate is not null;

    /// <summary>Whether a switch is animating (the shown content still follows <see cref="IsLoading"/>).</summary>
    public bool IsSwitching => _animating;

    /// <summary>Sets <see cref="SkeletonContent"/> (same as the property setter).</summary>
    public SkUiShimmer SetSkeletonContent(ISkUiView? value)
    {
        if (value is not null && !ReferenceEquals(SkeletonContent, value)) ValidateChild(value);
        SkeletonContent = value;
        return this;
    }

    /// <summary>Sets <see cref="IsLoading"/> (same as the property setter).</summary>
    public SkUiShimmer SetIsLoading(bool value) { IsLoading = value; return this; }

    /// <summary>Sets <see cref="Effect"/>.</summary>
    public SkUiShimmer SetEffect(SkUiShimmerEffect value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        Effect = value;
        return this;
    }

    /// <summary>Sets <see cref="Mode"/>.</summary>
    public SkUiShimmer SetMode(SkUiShimmerMode value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        Mode = value;
        return this;
    }

    /// <summary>Sets <see cref="BoneColor"/> (<c>null</c>: the look's).</summary>
    public SkUiShimmer SetBoneColor(Color? value) { BoneColor = value; return this; }

    /// <summary>Sets <see cref="HighlightColor"/> (<c>null</c>: the look's).</summary>
    public SkUiShimmer SetHighlightColor(Color? value) { HighlightColor = value; return this; }

    /// <summary>Sets <see cref="WaveWidth"/>.</summary>
    public SkUiShimmer SetWaveWidth(double value)
    {
        SkUiShimmerSettings.ThrowIfInvalidWaveWidth(value, nameof(value));
        WaveWidth = value;
        return this;
    }

    /// <summary>Sets <see cref="WaveDirection"/>.</summary>
    public SkUiShimmer SetWaveDirection(SkUiShimmerWaveDirection value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        WaveDirection = value;
        return this;
    }

    /// <summary>Sets <see cref="WaveRegion"/>.</summary>
    public SkUiShimmer SetWaveRegion(SkUiShimmerWaveRegion value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        WaveRegion = value;
        return this;
    }

    /// <summary>Sets <see cref="Duration"/> in milliseconds.</summary>
    public SkUiShimmer SetDuration(double value)
    {
        SkUiShimmerSettings.ThrowIfInvalidDuration(value, nameof(value));
        Duration = value;
        return this;
    }

    /// <summary>Sets <see cref="RepeatMode"/>.</summary>
    public SkUiShimmer SetRepeatMode(SkUiShimmerRepeatMode value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        RepeatMode = value;
        return this;
    }

    /// <summary>Sets <see cref="Easing"/> (<c>null</c>: the effect's own).</summary>
    public SkUiShimmer SetEasing(Easing? value) { Easing = value; return this; }

    /// <summary>Sets <see cref="PulseOpacity"/>.</summary>
    public SkUiShimmer SetPulseOpacity(double value)
    {
        SkUiShimmerSettings.ThrowIfInvalidPulseOpacity(value, nameof(value));
        PulseOpacity = value;
        return this;
    }

    /// <summary>The effect settings (tests).</summary>
    internal SkUiShimmerSettings Settings => _settings;

    private void UpdateSettings(Func<SkUiShimmerSettings, SkUiShimmerSettings> change)
    {
        var mode = _settings.Mode;
        _settings = change(_settings);
        // A container's chrome draws in Bones mode only.
        if (_settings.Mode != mode && _contentSkeleton)
            SkUiSkeletonScope.InvalidateContent(this);
        UpdateStyle(new Size(Math.Max(0, Width), Math.Max(0, Height)));
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
        _styles.Update(_settings, new Size(Math.Max(0, Width), Math.Max(0, Height)), IsRightToLeft); // the look's colors may have changed
        return _styles.Current;
    }

    /// <inheritdoc />
    SkUiShimmerMode ISkUiShimmer.Mode => _settings.Mode;

    /// <summary>Registers the content as a skeleton while it is one, so its controls draw their bones.</summary>
    private void UpdateContentSkeleton() =>
        _contentSkeleton = SkUiSkeletonScope.SetActive(this, _contentSkeleton, ShowsContentAsSkeleton);

    /// <inheritdoc />
    internal override bool HitTestsChildren => !ShowsContentAsSkeleton;

    /// <inheritdoc />
    private protected override ISkUiView? ContentToShow => _shown && HasSkeleton ? SkeletonContent : Content;

    /// <inheritdoc />
    private protected override void EnsureContentToShow()
    {
        if (!_shown || !HasSkeleton)
            base.EnsureContentToShow();
        else if (IsContentLoaded && Parent is not null && CanPresentContent)
            _skeletonSlot.Ensure();
    }

    /// <inheritdoc />
    private protected override void ClearTemplateContent()
    {
        base.ClearTemplateContent();
        _skeletonSlot.Clear();
    }

    private void OnSkeletonContentChanged(ISkUiView? value)
    {
        _skeletonSlot.OnContentChanged();
        if (_shown && !(value is null && _skeletonSlot.IsSetting))
            RefreshContent();
        UpdateContentSkeleton();
    }

    private void OnSkeletonContentTemplateChanged()
    {
        _skeletonSlot.OnTemplateChanged();
        if (_shown)
            RefreshContent();
        UpdateContentSkeleton();
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (_skeletonSlot.Reselect() && _shown)
            RefreshContent();
    }

    private void OnIsLoadingChanged(bool value)
    {
        _target = value;
        if (_animating)
            return; // the running switch picks the new value up
        if ((BeforeStateChangeAnimation is not null || AfterStateChangeAnimation is not null) && RenderState.HasCommitted && IsContentLoaded)
            _ = RunSwitches();
        else
            Switch();
    }

    private void Switch()
    {
        if (_shown == _target)
            return;
        _shown = _target;
        RefreshContent();
        UpdateContentSkeleton();
        InvalidateRender(SkUiRenderDirty.Props);
        InvalidateSemantics();
        OnPropertyChanged(nameof(IsShowingSkeleton));
    }

    // One animated switch at a time, each to the newest value: a value set during the "before" animation replaces the
    // pending one; one set during the "after" animation follows when it ends.
    private async Task RunSwitches()
    {
        _animating = true;
        try
        {
            while (_target != _shown)
            {
                await SkUiStateTransition.RunAsync(this, () => LayoutChild is SkUiView view ? [view] : [], Switch,
                    BeforeStateChangeAnimation, AfterStateChangeAnimation, CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            // The content must still follow IsLoading: switch without animating.
            Trace.WriteLine($"SkiaUi: {nameof(SkUiShimmer)} could not animate the switch to the {(_target ? "skeleton" : "content")}: {exception.Message}");
            _animating = false;
            Switch();
        }
        finally
        {
            _animating = false;
        }
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        UpdateContentSkeleton(); // before the content measures: empty labels measure as placeholder lines
        return base.MeasureContent(widthConstraint, heightConstraint);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        base.ArrangeContent(size);
        UpdateStyle(size);
    }

    /// <inheritdoc />
    internal override void OnGetRenderProps(ref SkUiRenderProps props)
    {
        base.OnGetRenderProps(ref props);
        UpdateContentSkeleton(); // drawn again after a reset
        if (!_shown || LayoutChild is null)
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
        // Not drawn: other trees stop looking for skeletons (a reset re-records the content anyway).
        _contentSkeleton = SkUiSkeletonScope.SetActive(this, _contentSkeleton, false, invalidate: false);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(FlowDirection))
            UpdateStyle(new Size(Math.Max(0, Width), Math.Max(0, Height)));
    }

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged()
    {
        base.OnEffectiveFlowDirectionChanged();
        UpdateStyle(new Size(Math.Max(0, Width), Math.Max(0, Height)));
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
