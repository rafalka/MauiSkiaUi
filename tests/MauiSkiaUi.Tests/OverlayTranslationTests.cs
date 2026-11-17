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
        root.HostsNativeViewsForTests = false; // only the outermost root's flag counts
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

            var animation = new object();
            overlay.BeginAncestorAnimation(animation);
            overlay.NotifyAncestorScrollMotion(true);
            overlay.EndAncestorAnimation(animation);
            Assert.True(overlay.IsShowingSnapshot); // the scroller still moves
            overlay.NotifyAncestorScrollMotion(false);
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.False(overlay.IsShowingSnapshot);

            // In Live mode a scroller does not keep it: the end of the animation restores the live view at once.
            overlay.ScrollMode = SkUiOverlayScrollMode.Live;
            overlay.BeginAncestorAnimation(animation);
            overlay.NotifyAncestorScrollMotion(true);
            Assert.True(overlay.IsShowingSnapshot);
            overlay.EndAncestorAnimation(animation);
            Assert.False(overlay.IsShowingSnapshot);
            overlay.NotifyAncestorScrollMotion(false);
        }
        finally
        {
            SkUiMauiContentView.CaptureOverride = null;
        }
    }

    [Fact]
    public async Task AnAnimationOfTheHostItselfShowsASnapshot()
    {
        SkUiMauiContentView.CaptureOverride = (_, done) => { done(Magenta()); return true; };
        try
        {
            var (root, _, overlay) = Form(0);
            using var surface = new SkUiTestSurface(root, 200, 300);
            surface.Frame(0);

            var move = overlay.AnimateAsync(SkUiAnimatableProperty.TranslationX, 30, 100);
            Assert.True(overlay.IsShowingSnapshot);
            surface.Frame(1000);
            surface.Frame(1200);
            Assert.True(await move.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(overlay.IsShowingSnapshot);
            Assert.Equal(new Rect(30, 20, 200, 40), overlay.ComputeRootRelativeFrame());
        }
        finally
        {
            SkUiMauiContentView.CaptureOverride = null;
        }
    }

    [Fact]
    public async Task AnAnimationSupersededByTheUiShowsTheLiveViewAtTheUiValue()
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
            surface.Frame(1000);
            sheet.TranslationY = 50; // a pan takes over
            surface.Frame(1050);
            Assert.False(await slide.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(50, sheet.TranslationY);
            Assert.False(overlay.IsShowingSnapshot);
            Assert.Equal(new Rect(0, 70, 200, 40), placed[^1]);
        }
        finally
        {
            SkUiMauiContentView.CaptureOverride = null;
        }
    }

    [Fact]
    public async Task AContainerMovedOutOfTheAnimatedViewMeanwhileShowsLiveAtTheEnd()
    {
        SkUiMauiContentView.CaptureOverride = (_, done) => { done(Magenta()); return true; };
        try
        {
            // The overlay sits in a container of the sheet; the container moves elsewhere while the sheet animates (the
            // overlay's own parent stays): the end of the animation still releases the overlay.
            var overlay = new SkUiMauiContentView { Content = new BoxView(), HeightRequest = 40 };
            var container = new SkUiVerticalStackLayout();
            container.Children.Add(overlay);
            var sheet = new SkUiVerticalStackLayout { TranslationY = 100 };
            sheet.Children.Add(container);
            var page = new SkUiVerticalStackLayout();
            page.Children.Add(sheet);
            var root = new SkUiContentView { Content = page, HostsNativeViewsForTests = true };
            using var surface = new SkUiTestSurface(root, 200, 300);
            surface.Frame(0);

            var slide = sheet.AnimateAsync(SkUiAnimatableProperty.TranslationY, 0, 100);
            Assert.True(overlay.IsShowingSnapshot);
            surface.Frame(1000);
            sheet.Children.Remove(container);
            page.Children.Add(container);
            Assert.Same(container, overlay.Parent);
            surface.Frame(1200);
            Assert.True(await slide.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(overlay.IsShowingSnapshot);
        }
        finally
        {
            SkUiMauiContentView.CaptureOverride = null;
        }
    }

    [Fact]
    public async Task AnAnimationEndingAfterTheHostMovedDoesNotEndTheAnimationOfItsNewAncestor()
    {
        SkUiMauiContentView.CaptureOverride = (_, done) => { done(Magenta()); return true; };
        try
        {
            var (root, sheet, overlay) = Form(100);
            var other = new SkUiVerticalStackLayout { TranslationX = 50 };
            var page = new SkUiVerticalStackLayout();
            root.Content = page;
            page.Children.Add(sheet);
            page.Children.Add(other);
            using var surface = new SkUiTestSurface(root, 200, 300);
            surface.Frame(0);

            var first = sheet.AnimateAsync(SkUiAnimatableProperty.TranslationY, 0, 100);
            surface.Frame(1000);
            sheet.Children.Remove(overlay); // leaving its parent forgets the sheet's animation
            Assert.False(overlay.IsShowingSnapshot);
            other.Children.Add(overlay);
            var second = other.AnimateAsync(SkUiAnimatableProperty.TranslationX, 0, 500);
            Assert.True(overlay.IsShowingSnapshot);
            surface.Frame(1050);

            surface.Frame(1200);
            Assert.True(await first.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(overlay.IsShowingSnapshot); // its new ancestor still animates
            surface.Frame(1600);
            Assert.True(await second.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(overlay.IsShowingSnapshot);
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
