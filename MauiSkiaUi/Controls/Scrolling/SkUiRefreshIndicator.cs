using MauiSkiaUi.Core;

namespace MauiSkiaUi;

/// <summary>
/// The area over pulled content where the refresh indicator (<see cref="SkUiCoreRefreshIndicator"/>) shows, for every owner
/// of <see cref="SkUiPullToRefresh"/>: input passes through, and it clips the indicator. It measures to nothing; its owner
/// arranges it over the pulled area.
/// </summary>
internal sealed class SkUiRefreshLayer : SkUiCoreHost
{
    public SkUiRefreshLayer()
    {
        InputTransparent = true;
        ClipToBounds = true;
        SetContent(Indicator);
    }

    /// <summary>The indicator (the owners' public <c>RefreshIndicator</c>).</summary>
    public SkUiCoreRefreshIndicator Indicator { get; } = new();

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        Indicator.Measure(double.PositiveInfinity, double.PositiveInfinity);
        return Size.Zero;
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var desired = Indicator.DesiredSize;
        Indicator.Arrange(new Rect((size.Width - desired.Width) / 2, 0, desired.Width, desired.Height));
    }

    /// <summary>
    /// Places the indicator for a pull showing <paramref name="gap"/> DIPs past the top (composite-time: nothing is
    /// re-recorded). An overlay badge comes down with the pull (up to 1.5 × the trigger distance) and rests with its bottom at
    /// <paramref name="rest"/> while refreshing; an inline indicator slides down with the content from
    /// <paramref name="top"/> (where the pulled scroller starts in this layer) and stays centered in the first
    /// <paramref name="rest"/> DIPs.
    /// </summary>
    public void Show(double gap, double top, SkUiRefreshStyle style, double trigger, double rest, bool refreshing)
    {
        gap = Math.Max(0, gap);
        var size = Indicator.DesiredSize.Height is > 0 and var measured ? measured : SkUiLook.Current.RefreshIndicatorSize;
        Indicator.SetStyle(style);
        Indicator.SetIsRefreshing(refreshing);
        if (!refreshing)
            Indicator.SetPullProgress(trigger > 0 ? gap / trigger : 0);
        Indicator.SetTranslationY(style == SkUiRefreshStyle.Overlay
            ? (refreshing ? rest : Math.Min(gap, trigger * 1.5)) - size
            : top + Math.Min(gap, rest) - (rest + size) / 2);
    }
}
