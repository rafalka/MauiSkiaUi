using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The look's colors for unchecked check boxes and radio buttons (issue #90): <see cref="SkUiLook.CheckBoxBorderColor"/>,
/// <see cref="SkUiLook.CheckBoxBackgroundColor"/> and <see cref="SkUiLook.RadioButtonRingColor"/> reach the painter on both
/// layers, dimmed while disabled; the defaults stay the scheme's muted color and white.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class ToggleLookColorTests
{
    private const int Size = 24;

    private static readonly Color Border = Color.FromRgb(255, 0, 0);
    private static readonly Color Fill = Color.FromRgb(0, 0, 255);
    private static readonly Color Ring = Color.FromRgb(0, 255, 0);

    /// <summary>Square corners and wide strokes, so single pixels sample the outline and the fill.</summary>
    private sealed class ColoredLook : DefaultSkUiLook
    {
        public override Color CheckBoxBorderColor => Border;
        public override Color CheckBoxBackgroundColor => Fill;
        public override Color RadioButtonRingColor => Ring;
        public override float CheckBoxBorderWidth => 4;
        public override float GetCheckBoxCornerRadius(float size) => 0;
        public override float RadioRingFraction => 0.2f;
        public override Size DefaultCheckBoxSize => new(Size, Size);
        public override Size DefaultRadioButtonSize => new(Size, Size);
    }

    private static SKBitmap Render(Action<SKCanvas> paint)
    {
        var bitmap = new SKBitmap(Size, Size);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        paint(canvas);
        return bitmap;
    }

    private static SKBitmap Render(SkUiView view)
    {
        view.WidthRequest = Size;
        view.HeightRequest = Size;
        SkUiTestHelpers.Arrange(view, Size, Size);
        return Render(view.Paint);
    }

    private static SKBitmap Render(SkUiCoreNode node)
    {
        node.SetWidth(Size).SetHeight(Size);
        node.Measure(Size, Size);
        node.Arrange(new Rect(0, 0, Size, Size));
        return Render(node.Paint);
    }

    /// <summary>Renders both layers, asserts they draw the same and returns the SkUi* rendering.</summary>
    private static SKBitmap RenderBoth(SkUiView view, SkUiCoreNode node, bool enabled = true)
    {
        view.IsEnabled = enabled;
        node.SetIsEnabled(enabled);
        var expected = Render(view);
        using var actual = Render(node);
        Assert.Equal(expected.Pixels, actual.Pixels);
        return expected;
    }

    private static void WithLook(Action test)
    {
        var previous = SkUiLook.Current;
        try
        {
            SkUiLook.Current = new ColoredLook();
            test();
        }
        finally
        {
            SkUiLook.Current = previous;
        }
    }

    private static SKColor Sk(Color color) => SkUiShapePainter.ToSkColor(color);

    [Fact]
    public void AnUncheckedCheckBoxDrawsTheLooksOutlineAndFill() => WithLook(() =>
    {
        using var bitmap = RenderBoth(new SkUiCheckBox(), new SkUiCoreCheckBox());
        Assert.Equal(Sk(Border), bitmap.GetPixel(Size / 2, 1));
        Assert.Equal(Sk(Fill), bitmap.GetPixel(Size / 2, Size / 2));
    });

    [Fact]
    public void ADisabledCheckBoxDimsTheLooksColors() => WithLook(() =>
    {
        using var bitmap = RenderBoth(new SkUiCheckBox(), new SkUiCoreCheckBox(), enabled: false);
        var border = bitmap.GetPixel(Size / 2, 1);
        var fill = bitmap.GetPixel(Size / 2, Size / 2);
        // The half-alpha outline is drawn over the half-alpha fill: about 3/4 opaque, mostly red.
        Assert.InRange(border.Alpha, 188, 193);
        Assert.True(border.Red > border.Blue && border.Green == 0, border.ToString());
        Assert.InRange(fill.Alpha, 126, 129);
        Assert.True(fill.Blue > 200 && fill.Red == 0 && fill.Green == 0, fill.ToString());
    });

    [Fact]
    public void ACheckedCheckBoxStillTakesItsColor() => WithLook(() =>
    {
        using var bitmap = RenderBoth(new SkUiCheckBox { IsChecked = true, Color = Colors.Black },
            new SkUiCoreCheckBox().SetColor(Colors.Black).SetIsChecked(true));
        Assert.Equal(SKColors.Black, bitmap.GetPixel(Size / 2, 1));
    });

    [Fact]
    public void AnUnselectedRadioButtonDrawsTheLooksRing() => WithLook(() =>
    {
        using var bitmap = RenderBoth(new SkUiRadioButton(), new SkUiCoreRadioButton());
        Assert.Equal(Sk(Ring), bitmap.GetPixel(Size / 2, 2));
        Assert.Equal(0, bitmap.GetPixel(Size / 2, Size / 2).Alpha);
    });

    [Fact]
    public void ADisabledRadioButtonDimsTheLooksRing() => WithLook(() =>
    {
        using var bitmap = RenderBoth(new SkUiRadioButton(), new SkUiCoreRadioButton(), enabled: false);
        var ring = bitmap.GetPixel(Size / 2, 2);
        Assert.InRange(ring.Alpha, 126, 129);
        Assert.True(ring.Green > 200 && ring.Red == 0 && ring.Blue == 0, ring.ToString());
    });

    [Fact]
    public void TheDefaultsAreTheSchemesMutedColorAndWhite()
    {
        var look = new DefaultSkUiLook();
        Assert.Equal(SkUiColors.Muted, look.CheckBoxBorderColor);
        Assert.Equal(Colors.White, look.CheckBoxBackgroundColor);
        Assert.Equal(SkUiColors.Muted, look.RadioButtonRingColor);
    }
}
