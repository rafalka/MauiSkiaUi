using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// <see cref="SkUiSheet"/> under reduced motion. In the global collection: <see cref="SkUiMotion.ReduceMotion"/> is process-wide.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class SheetMotionTests
{
    [Fact]
    public void ReducedMotionOpensAndClosesAtOnce()
    {
        SkUiMotion.ReduceMotion = true;
        try
        {
            var sheet = new SkUiSheet { Content = new SkUiBox { HeightRequest = 200 } };
            var root = new SkUiGrid { Children = { new SkUiButton { Text = "Page" }, sheet } };
            using var surface = new SkUiTestSurface(root, 400, 800);
            surface.Frame(0);
            var opened = 0;
            sheet.Opened += (_, _) => opened++;
            sheet.IsOpen = true;
            surface.Frame(16);
            Assert.Equal(400, sheet.Position, 1);
            Assert.Equal(1, opened);
            sheet.IsOpen = false;
            Assert.Equal(0, sheet.Position, 1);
        }
        finally
        {
            SkUiMotion.ReduceMotion = null;
        }
    }
}
