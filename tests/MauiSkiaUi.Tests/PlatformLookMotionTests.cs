using MauiSkiaUi;
using Xunit;

namespace MauiSkiaUi.Tests;

public class PlatformLookMotionTests
{
    [Theory]
    [InlineData(0.2, 0, 0, 1)]
    [InlineData(0.32, 0.72, 0, 1)]
    public void CubicBezierEasingRunsFromZeroToOneAndEasesOut(double x1, double y1, double x2, double y2)
    {
        var easing = SkUiEasings.CubicBezier(x1, y1, x2, y2);
        Assert.Equal(0, easing.Ease(0), 4);
        Assert.Equal(1, easing.Ease(1), 4);
        Assert.True(easing.Ease(0.5) > 0.5);
        Assert.True(easing.Ease(0.25) < easing.Ease(0.75));
    }

    [Fact]
    public void PlatformLooksChooseTheirOwnMotion()
    {
        var def = DefaultSkUiLook.Instance.GetTransition(SkUiTransitionKind.Switch);
        var android = AndroidSkUiLook.Instance.GetTransition(SkUiTransitionKind.Switch);
        var ios = IosSkUiLook.Instance.GetTransition(SkUiTransitionKind.Switch);
        var windows = WindowsSkUiLook.Instance.GetTransition(SkUiTransitionKind.Switch);
        Assert.Equal(TimeSpan.FromMilliseconds(200), def.Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(300), android.Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(300), ios.Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(167), windows.Duration);
        // Only rippling looks spend frames on the ripple.
        Assert.NotEqual(TimeSpan.Zero, AndroidSkUiLook.Instance.GetTransition(SkUiTransitionKind.Ripple).Duration);
        Assert.Equal(TimeSpan.Zero, IosSkUiLook.Instance.GetTransition(SkUiTransitionKind.Ripple).Duration);
    }
}
