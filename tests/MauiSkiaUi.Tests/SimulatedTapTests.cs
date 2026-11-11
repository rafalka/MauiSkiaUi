using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary><see cref="SkUiDiagnostics.SimulateTapAt"/>: what took a synthetic tap, and cancelling one that is not a tap.</summary>
public class SimulatedTapTests
{
    private static SkUiContentView Surface(SkUiView content, double width = 100, double height = 100)
    {
        var root = new SkUiContentView { Content = content };
        SkUiTestHelpers.Arrange(root, width, height);
        return root;
    }

    private static SkUiScrollView ScrollerWithLabel(out SkUiLabel label)
    {
        label = new SkUiLabel { Text = "row", HeightRequest = 40 };
        var stack = new SkUiVerticalStackLayout { label, new SkUiLabel { HeightRequest = 400 } };
        return new SkUiScrollView { Content = stack };
    }

    [Fact]
    public void ATapOnAButtonIsTakenByItsTap()
    {
        var button = new SkUiButton { Text = "OK" };
        var root = Surface(button);
        var clicks = 0;
        button.Clicked += (_, _) => clicks++;

        var result = SkUiDiagnostics.SimulateTap(button, releaseOnlyIfTap: true);

        Assert.Same(button, result.Hit);
        Assert.NotNull(result.Winner);
        Assert.Same(button, result.Winner!.Owner);
        Assert.True(result.IsTap);
        Assert.True(result.Released);
        Assert.False(result.PassedToNative);
        Assert.Equal(1, clicks);
        Assert.Equal(0, root.Router.ActiveArenaCount);
    }

    [Fact]
    public void ALabelInAScrollerIsCancelledWithoutSideEffects()
    {
        var scroll = ScrollerWithLabel(out var label);
        var root = Surface(scroll);

        var result = SkUiDiagnostics.SimulateTap(label, releaseOnlyIfTap: true);

        Assert.Same(label, result.Hit);
        Assert.NotNull(result.Winner);
        Assert.Same(scroll, result.Winner!.Owner);
        Assert.False(result.IsTap);
        Assert.False(result.Released);
        Assert.True(result.PassedToNative);
        Assert.False(result.Winner.IsTracking);
        Assert.Equal(0, root.Router.ActiveArenaCount);
    }

    [Fact]
    public void WithoutReleaseOnlyIfTapTheScrollerPressIsReleased()
    {
        var root = Surface(ScrollerWithLabel(out var label));

        var result = SkUiDiagnostics.SimulateTap(label, releaseOnlyIfTap: false);

        Assert.Null(result.Winner); // released without moving, the scroller resigns: nothing won
        Assert.False(result.IsTap);
        Assert.True(result.PassedToNative);
        Assert.True(result.Released);
        Assert.Equal(0, root.Router.ActiveArenaCount);
    }

    [Fact]
    public void ADisabledButtonTakesNoTap()
    {
        var button = new SkUiButton { Text = "OK", IsEnabled = false };
        var root = Surface(button);
        var clicks = 0;
        button.Clicked += (_, _) => clicks++;

        var result = SkUiDiagnostics.SimulateTapAt(root, new Point(50, 50));

        Assert.NotNull(result.Hit);
        Assert.Null(result.Winner);
        Assert.False(result.IsTap);
        Assert.False(result.Released);
        Assert.True(result.PassedToNative);
        Assert.Equal(0, clicks);
        Assert.Equal(0, root.Router.ActiveArenaCount);
    }

    [Fact]
    public void PlainContentPassesTheTapToNativeAncestors()
    {
        var label = new SkUiLabel { Text = "plain" };
        var root = Surface(label);

        var result = SkUiDiagnostics.SimulateTap(label, releaseOnlyIfTap: true);

        Assert.NotNull(result.Hit);
        Assert.Null(result.Winner);
        Assert.False(result.IsTap);
        Assert.True(result.PassedToNative);
        Assert.Equal(0, root.Router.ActiveArenaCount);
    }

    [Fact]
    public void OutsideTheSurfaceNothingIsPressed()
    {
        var root = Surface(new SkUiButton { Text = "OK" });

        Assert.Equal(default, SkUiDiagnostics.SimulateTapAt(root, new Point(500, 500)));
        Assert.Equal(default, SkUiDiagnostics.SimulateTap(new SkUiButton(), releaseOnlyIfTap: true));
    }

    [Fact]
    public void TheElementOverloadStillReturnsTheHitAndAlwaysReleases()
    {
        var scroll = ScrollerWithLabel(out var label);
        var root = Surface(scroll);

        Assert.Same(label, SkUiDiagnostics.SimulateTap(label));
        Assert.Equal(0, root.Router.ActiveArenaCount);
    }
}
