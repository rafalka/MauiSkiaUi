using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// <c>DefaultMinimumHeight</c> on both button layers (#65): a subclass sets its own minimum height, used while no minimum
/// height is set, as it sets <c>DefaultCornerRadius</c> and <c>DefaultPadding</c>; a set minimum, local or from a style, wins.
/// </summary>
[Collection(GlobalStateCollection.Name)] // reads the current look's button minimum
public class ButtonDefaultMinimumHeightTests
{
    /// <summary>Taller than any look's button minimum and than its one line of text.</summary>
    private const double HouseMinimum = 70;

    private sealed class HouseButton : SkUiButton
    {
        protected override double DefaultMinimumHeight => HouseMinimum;
    }

    private sealed class HouseCoreButton : SkUiCoreButton
    {
        protected override double DefaultMinimumHeight => HouseMinimum;
    }

    private static double MeasuredHeight(SkUiButton button)
    {
        button.Text = "Save";
        button.FontFamily = SkUiTestHelpers.BundledFontFamily;
        return button.Measure(300, double.PositiveInfinity).Height;
    }

    private static double MeasuredHeight(SkUiCoreButton button)
    {
        button.SetText("Save").SetFontFamily(SkUiTestHelpers.BundledFontFamily);
        button.Measure(300, double.PositiveInfinity);
        return button.DesiredSize.Height;
    }

    [Fact]
    public void ASubclassMinimumAppliesUntilAMinimumIsSet()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var lookMinimum = MeasuredHeight(new SkUiButton());
        Assert.True(lookMinimum < HouseMinimum, $"{lookMinimum}");
        Assert.Equal(HouseMinimum, MeasuredHeight(new HouseButton()));
        Assert.Equal(lookMinimum, MeasuredHeight(new SkUiCoreButton()), 3);
        Assert.Equal(HouseMinimum, MeasuredHeight(new HouseCoreButton()));

        // A set minimum wins, also a lower one: the text and padding give the height then.
        var local = new HouseButton { MinimumHeightRequest = 10 };
        Assert.True(MeasuredHeight(local) < HouseMinimum);
        var styled = new HouseButton { Style = new Style(typeof(HouseButton)) { Setters = { new Setter { Property = VisualElement.MinimumHeightRequestProperty, Value = 10.0 } } } };
        Assert.True(MeasuredHeight(styled) < HouseMinimum);
        var core = new HouseCoreButton();
        core.SetMinimumHeight(10);
        Assert.True(MeasuredHeight(core) < HouseMinimum);
    }
}
