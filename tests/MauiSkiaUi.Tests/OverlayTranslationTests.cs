using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>Native overlays follow the translation of their drawn ancestors (issue #100).</summary>
[Collection(nameof(OverlayScrollTests))] // uses the static capture hook
public class OverlayTranslationTests
{
    /// <summary>A sheet (a box 20 high above the native view) in a root that shows native views.</summary>
    private static (SkUiContentView Root, SkUiVerticalStackLayout Sheet, SkUiMauiContentView Overlay) Form(double translationY)
    {
        var overlay = new SkUiMauiContentView { Content = new BoxView(), HeightRequest = 40, ScrollMode = SkUiOverlayScrollMode.Live };
        var sheet = new SkUiVerticalStackLayout { TranslationY = translationY };
        sheet.Children.Add(new SkUiBox { HeightRequest = 20 });
        sheet.Children.Add(overlay);
        var root = new SkUiContentView { Content = sheet, Background = Colors.White, HostsNativeViewsForTests = true };
        return (root, sheet, overlay);
    }

    private static SKImage Magenta()
    {
        using var bitmap = new SKBitmap(10, 10);
        bitmap.Erase(SKColors.Magenta);
        return SKImage.FromBitmap(bitmap);
    }

    [Fact]
    public void AnAncestorTranslationChangePlacesTheNativeViewAgain()
    {
        var (root, sheet, overlay) = Form(800);
        var outer = new SkUiContentView { Content = root, HostsNativeViewsForTests = true };
        SkUiTestHelpers.Arrange(outer, 200, 300);
        var placed = new List<Rect>();
        overlay.PlacedForTests = () => placed.Add(overlay.ComputeRootRelativeFrame());

        sheet.TranslationY = 0;
        Assert.Equal(new Rect(0, 20, 200, 40), placed[^1]);

        // Any drawn ancestor, along either axis.
        placed.Clear();
        root.TranslationX = 30;
        Assert.Equal(new Rect(30, 20, 200, 40), placed[^1]);
    }

    [Fact]
    public void ATranslationChangeOnASurfaceWithoutNativeViewsDoesNotWalkTheTree()
    {
        var (root, sheet, overlay) = Form(800);
        root.HostsNativeViewsForTests = false;
        SkUiTestHelpers.Arrange(root, 200, 300);
        var placed = 0;
        overlay.PlacedForTests = () => placed++;

        sheet.TranslationY = 0;
        Assert.Equal(0, placed);
    }

    [Fact]
    public async Task ASnapshotFollowsAnAncestorAnimationAndTheLiveViewComesBackPlaced()
    {
        SkUiMauiContentView.CaptureOverride = (_, done) => { done(Magenta()); return true; };
        try
        {
            var (root, sheet, overlay) = Form(100);
            using var surface = new SkUiTestSurface(root, 200, 300);
            surface.Frame(0);
            var placed = new List<Rect>();
            overlay.PlacedForTests = () => placed.Add(overlay.ComputeRootRelativeFrame());

            var slide = sheet.AnimateAsync(SkUiAnimatableProperty.TranslationY, 0, 100);
            // A live native view cannot follow a render-thread animation: a snapshot stands in, also in Live scroll mode.
            Assert.True(overlay.IsShowingSnapshot);
            Assert.Equal(SKColors.Magenta, surface.Frame(1000).GetPixel(100, 140)); // drawn at y 120..160
            Assert.True(overlay.IsShowingSnapshot);

            var end = surface.Frame(1200); // the last frame; its end reaches the UI thread
            Assert.Equal(SKColors.Magenta, end.GetPixel(100, 40)); // drawn at y 20..60, with the sheet
            Assert.True(await slide.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(0, sheet.TranslationY);
            Assert.False(overlay.IsShowingSnapshot);
            Assert.Equal(new Rect(0, 20, 200, 40), placed[^1]);
            Assert.NotEqual(SKColors.Magenta, surface.Frame(1300).GetPixel(100, 40));
        }
        finally
        {
            SkUiMauiContentView.CaptureOverride = null;
        }
    }

    [Fact]
    public void AnEndingAnimationKeepsTheSnapshotOfAScrollerStillMoving()
    {
        using var clock = new ManualGestureClock();
        SkUiMauiContentView.CaptureOverride = (_, done) => { done(Magenta()); return true; };
        try
        {
            var (root, _, overlay) = Form(0);
            overlay.ScrollMode = SkUiOverlayScrollMode.Snapshot;
            SkUiTestHelpers.Arrange(root, 200, 300);

            overlay.NotifyAncestorAnimation(true);
            overlay.NotifyAncestorScrollMotion(true);
            overlay.NotifyAncestorAnimation(false);
            Assert.True(overlay.IsShowingSnapshot); // the scroller still moves
            overlay.NotifyAncestorScrollMotion(false);
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.False(overlay.IsShowingSnapshot);

            // In Live mode a scroller does not keep it: the end of the animation restores the live view at once.
            overlay.ScrollMode = SkUiOverlayScrollMode.Live;
            overlay.NotifyAncestorAnimation(true);
            overlay.NotifyAncestorScrollMotion(true);
            Assert.True(overlay.IsShowingSnapshot);
            overlay.NotifyAncestorAnimation(false);
            Assert.False(overlay.IsShowingSnapshot);
            overlay.NotifyAncestorScrollMotion(false);
        }
        finally
        {
            SkUiMauiContentView.CaptureOverride = null;
        }
    }

    [Fact]
    public void AnAnimationOnASurfaceWithoutNativeViewsTakesNoSnapshot()
    {
        var captures = 0;
        SkUiMauiContentView.CaptureOverride = (_, done) => { captures++; done(Magenta()); return true; };
        try
        {
            var (root, sheet, overlay) = Form(100);
            root.HostsNativeViewsForTests = false;
            using var surface = new SkUiTestSurface(root, 200, 300);
            surface.Frame(0);

            _ = sheet.AnimateAsync(SkUiAnimatableProperty.TranslationY, 0, 100);
            Assert.False(overlay.IsShowingSnapshot);
            Assert.Equal(0, captures);
            surface.Frame(1000);
            surface.Frame(1200);
        }
        finally
        {
            SkUiMauiContentView.CaptureOverride = null;
        }
    }
}
