using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// The indicator of pull-to-refresh, drawn by the look (<see cref="SkUiLook.DrawRefreshIndicator"/>; by default the look's
/// activity indicator, on a badge as an overlay): the one <see cref="SkUiRefreshView"/> and
/// <see cref="SkUiCollectionView"/> show (their <c>RefreshIndicator</c>), and a control of its own for pulls built on a
/// scroller's overscroll events (<see cref="SkUiScrollView.Overscrolled"/>): set <see cref="PullProgress"/> as the content
/// is pulled and <see cref="IsRefreshing"/> while the work runs, and place it with <see cref="SkUiCoreNode.TranslationY"/>.
/// </summary>
/// <remarks>
/// A pull re-records nothing: the look's feedback (<see cref="SkUiLook.GetRefreshPullFeedback"/>) multiplies the node's
/// <see cref="SkUiCoreNode.Opacity"/> and adds to its <see cref="SkUiCoreNode.Rotation"/> at composite time (unless the
/// look draws the pull itself, <see cref="SkUiLook.RefreshIndicatorDrawsPullProgress"/>). While refreshing, the compositor
/// spins the picture on the render thread. Its size is <see cref="SkUiLook.RefreshIndicatorSize"/>; the look gives the
/// shadow (<see cref="SkUiLook.GetRefreshIndicatorShadow"/>) unless one is set.
/// </remarks>
public class SkUiCoreRefreshIndicator : SkUiCoreNode
{
    private Color? _color;
    private SkUiRefreshStyle _style;
    private double _pullProgress;
    private bool _isRefreshing;
    private IShadow? _lookShadow;

    /// <summary>The arc's color; <c>null</c> (default): the accent color.</summary>
    public Color? Color
    {
        get => _color;
        set => SetColor(value);
    }

    /// <summary>How the indicator shows (<see cref="SkUiRefreshStyle.Default"/>: <see cref="SkUiLook.DefaultRefreshStyle"/>).</summary>
    public SkUiRefreshStyle Style
    {
        get => _style;
        set => SetStyle(value);
    }

    /// <summary>
    /// How far the content was pulled, as a part of the trigger distance (0: not pulled, 1: far enough to refresh, more past
    /// it). Ignored while <see cref="IsRefreshing"/>.
    /// </summary>
    public double PullProgress
    {
        get => _pullProgress;
        set => SetPullProgress(value);
    }

    /// <summary>Whether a refresh runs: the indicator shows fully and spins.</summary>
    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetIsRefreshing(value);
    }

    /// <summary>The opacity shown: <see cref="SkUiCoreNode.Opacity"/>, times the look's pull feedback unless refreshing (tests).</summary>
    internal double ShownOpacity => _isRefreshing ? Opacity
        : Opacity * Math.Clamp(SkUiLook.Current.GetRefreshPullFeedback(_pullProgress, EffectiveStyle).Opacity, 0, 1);

    /// <summary>The style drawn: <see cref="Style"/>, or the look's.</summary>
    public SkUiRefreshStyle EffectiveStyle => Resolve(_style);

    /// <summary>The style a control's <paramref name="style"/> stands for: itself, or the look's (overlay when the look says default too).</summary>
    internal static SkUiRefreshStyle Resolve(SkUiRefreshStyle style) =>
        style != SkUiRefreshStyle.Default ? style
        : SkUiLook.Current.DefaultRefreshStyle is var look && look != SkUiRefreshStyle.Default ? look
        : SkUiRefreshStyle.Overlay;

    /// <summary>Sets <see cref="Color"/>.</summary>
    public SkUiCoreRefreshIndicator SetColor(Color? value)
    {
        if (!SetProperty(ref _color, value, nameof(Color))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets <see cref="Style"/>.</summary>
    public SkUiCoreRefreshIndicator SetStyle(SkUiRefreshStyle value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (!SetProperty(ref _style, value, nameof(Style))) return this;
        ApplyLookShadow();
        InvalidatePaint();
        InvalidateRender(SkUiRenderDirty.Props);
        return this;
    }

    /// <summary>Sets <see cref="PullProgress"/>.</summary>
    public SkUiCoreRefreshIndicator SetPullProgress(double value)
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        if (!SetProperty(ref _pullProgress, value, nameof(PullProgress))) return this;
        if (!_isRefreshing)
        {
            if (SkUiLook.Current.RefreshIndicatorDrawsPullProgress)
                InvalidatePaint();
            InvalidateRender(SkUiRenderDirty.Props);
        }
        return this;
    }

    /// <summary>Sets <see cref="IsRefreshing"/>.</summary>
    public SkUiCoreRefreshIndicator SetIsRefreshing(bool value)
    {
        if (!SetProperty(ref _isRefreshing, value, nameof(IsRefreshing))) return this;
        InvalidatePaint();
        InvalidateRender(SkUiRenderDirty.Props);
        InvalidateSemantics();
        return this;
    }

    /// <summary>The look's shadow for the style, unless the app set one of its own.</summary>
    private void ApplyLookShadow()
    {
        if (Shadow is not null && !ReferenceEquals(Shadow, _lookShadow))
            return;
        _lookShadow = SkUiLook.Current.GetRefreshIndicatorShadow(EffectiveStyle);
        SetShadow(_lookShadow);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        // Measured again when the look changes: its shadow follows too.
        ApplyLookShadow();
        var size = Math.Max(0, SkUiLook.Current.RefreshIndicatorSize);
        return new Size(size, size);
    }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var look = SkUiLook.Current;
        var progress = !_isRefreshing && look.RefreshIndicatorDrawsPullProgress ? _pullProgress : 0;
        look.DrawRefreshIndicator(canvas, new SkUiRefreshIndicatorPaint(
            new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height), ToSkColor(_color ?? SkUiColors.Accent), EffectiveStyle, _isRefreshing, progress));
    }

    /// <inheritdoc />
    internal override void OnGetRenderProps(ref SkUiRenderProps props)
    {
        var look = SkUiLook.Current;
        if (_isRefreshing)
        {
            props.ContentSpinPeriod = (float)Math.Max(0, look.RefreshIndicatorSpinPeriod);
            return;
        }
        var feedback = look.GetRefreshPullFeedback(_pullProgress, EffectiveStyle);
        props.Opacity *= (float)Math.Clamp(feedback.Opacity, 0, 1);
        props.Rotation += (float)feedback.Rotation;
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        // Refreshing: busy, read like a native indeterminate progress indicator.
        if (_isRefreshing)
            info.Role = SkUiSemanticsRole.ProgressBar;
    }
}
