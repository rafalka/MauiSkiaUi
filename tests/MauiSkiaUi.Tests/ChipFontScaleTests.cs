using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>A chip's badge follows the system text size (<see cref="SkUiFontScaling.Factor"/>, global state).</summary>
[Collection(GlobalStateCollection.Name)]
public class ChipFontScaleTests
{
    private static SkUiChip Chip(string? badge) => new()
    {
        Text = "Inbox", BadgeText = badge, FontFamily = SkUiTestHelpers.BundledFontFamily, BadgeFontFamily = SkUiTestHelpers.BundledFontFamily,
        HorizontalOptions = LayoutOptions.Start
    };

    private static double Width(SkUiVerticalStackLayout root)
    {
        var chip = (SkUiChip)root.Children[0]; // IView.Width is the requested width
        chip.InvalidateMeasure();
        SkUiTestHelpers.Arrange(root, 400, 200);
        return chip.Width;
    }

    [Fact]
    public void TheBadgeIsMeasuredAgainWhenTheTextSizeChanges()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        try
        {
            SkUiFontScaling.Factor = 1;
            // The same badged chip at both sizes: its badge must not keep the pill measured at the first.
            var badged = new SkUiVerticalStackLayout { Children = { Chip("12") } };
            var plain = new SkUiVerticalStackLayout { Children = { Chip(null) } };
            var roomAt1 = Width(badged) - Width(plain);

            SkUiFontScaling.Factor = 2;
            var roomAt2 = Width(badged) - Width(plain);
            Assert.True(roomAt2 > roomAt1 + 4, $"badge room {roomAt1} → {roomAt2}");
        }
        finally
        {
            SkUiFontScaling.Factor = null;
        }
    }
}
