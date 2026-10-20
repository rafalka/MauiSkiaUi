using System.Windows.Input;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The disabled button fill (M4 in ImplementationPlan.md): a disabled button draws what the app set (a <c>Background</c>, a
/// <c>FillColor</c>, a <c>Disabled</c> visual state's setters), only the scheme's default fill turns into the scheme's
/// <c>Disabled</c> color, a button without a fill keeps none and dims its content, and <c>DisabledFillColor</c> replaces
/// all of it; one rule on both layers, checked pixel by pixel SkUi vs Core.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class DisabledButtonFillTests
{
    private const int Width = 100;
    private const int Height = 44;
    private static readonly SKPoint Center = new(Width / 2f, Height / 2f);

    private static SKBitmap Render(Action<SKCanvas> paint, int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        paint(canvas);
        return bitmap;
    }

    private static SKBitmap Render(SkUiView view, int width = Width, int height = Height)
    {
        view.WidthRequest = width;
        view.HeightRequest = height;
        SkUiTestHelpers.Arrange(view, width, height);
        return Render(view.Paint, width, height);
    }

    private static SKBitmap Render(SkUiCoreNode node, int width = Width, int height = Height)
    {
        node.SetWidth(width).SetHeight(height);
        node.Measure(width, height);
        node.Arrange(new Rect(0, 0, width, height));
        return Render(node.Paint, width, height);
    }

    private static SKColor Sk(Color color) => SkUiShapePainter.ToSkColor(color);

    private static SKColor CenterOf(SKBitmap bitmap) => bitmap.GetPixel((int)Center.X, (int)Center.Y);

    /// <summary>Renders both disabled, asserts they draw the same and returns the SkUi* rendering.</summary>
    private static SKBitmap RenderDisabled(SkUiView view, SkUiCoreNode node)
    {
        view.IsEnabled = false;
        node.SetIsEnabled(false);
        var expected = Render(view);
        using var actual = Render(node);
        Assert.Equal(expected.Pixels, actual.Pixels);
        return expected;
    }

    /// <summary>The Core twin of a text button in the bundled font, in black text or the scheme's.</summary>
    private static SkUiCoreButton TextButton(SkUiCoreButton button, bool blackText = true)
    {
        button.SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetText("MMMM");
        if (blackText) button.SetTextColor(Colors.Black);
        return button;
    }

    private static byte MaxAlpha(SKBitmap bitmap) => bitmap.Pixels.Max(pixel => pixel.Alpha);

    [Fact]
    public void TheSchemesDefaultFillTurnsIntoTheDisabledColor()
    {
        using var bitmap = RenderDisabled(new SkUiButton(), new SkUiCoreButton());
        Assert.Equal(Sk(SkUiColors.Disabled), CenterOf(bitmap));
    }

    [Fact]
    public void AFillColorOrBackgroundTheAppSetIsDrawnAsSet()
    {
        using (var bitmap = RenderDisabled(new SkUiButton { FillColor = Colors.Red }, new SkUiCoreButton().SetFillColor(Colors.Red)))
            Assert.Equal(SKColors.Red, CenterOf(bitmap));
        using (var bitmap = RenderDisabled(new SkUiButton { Background = Colors.Blue }, new SkUiCoreButton().SetBackground(new SolidPaint(Colors.Blue))))
            Assert.Equal(SKColors.Blue, CenterOf(bitmap));
        // Also the scheme's own color, once the app set it.
        using (var bitmap = RenderDisabled(new SkUiButton { FillColor = SkUiColors.ButtonBackground }, new SkUiCoreButton().SetFillColor(SkUiColors.ButtonBackground)))
            Assert.Equal(Sk(SkUiColors.ButtonBackground), CenterOf(bitmap));
    }

    [Fact]
    public void ATransparentButtonKeepsNoFillAndDimsItsText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        SkUiButton View() => new() { Text = "MMMM", FontFamily = SkUiTestHelpers.BundledFontFamily, TextColor = Colors.Black, FillColor = Colors.Transparent };
        SkUiCoreButton Node() => TextButton(new SkUiCoreButton().SetFillColor(Colors.Transparent));

        using var enabled = Render(View());
        using var disabled = RenderDisabled(View(), Node());
        Assert.Equal(0, disabled.GetPixel(2, 2).Alpha); // no grey box
        Assert.Equal(255, MaxAlpha(enabled));
        var expected = (byte)Math.Round(255 * SkUiLook.Current.DisabledButtonContentOpacity);
        Assert.InRange(MaxAlpha(disabled), expected - 1, expected + 1);

        // DisabledFillColor draws its fill and the content as set.
        using var filled = RenderDisabled(View().SetDisabledFillColor(Colors.Yellow), Node().SetDisabledFillColor(Colors.Yellow));
        Assert.Equal(SKColors.Yellow, filled.GetPixel(2, Height / 2));
        Assert.Contains(SKColors.Black, filled.Pixels);
    }

    [Fact]
    public void DisabledFillColorReplacesEveryFill()
    {
        var view = new SkUiButton { Background = Colors.Blue, DisabledFillColor = Colors.Orange };
        var node = new SkUiCoreButton().SetDisabledFillColor(Colors.Orange);
        node.SetBackground(new SolidPaint(Colors.Blue));
        using (var bitmap = Render(view))
            Assert.Equal(SKColors.Blue, CenterOf(bitmap)); // enabled: unchanged
        using (var bitmap = RenderDisabled(view, node))
            Assert.Equal(Sk(Colors.Orange), CenterOf(bitmap));
        using (var bitmap = RenderDisabled(new SkUiButton { DisabledFillColor = Colors.Orange }, new SkUiCoreButton().SetDisabledFillColor(Colors.Orange)))
            Assert.Equal(Sk(Colors.Orange), CenterOf(bitmap));
    }

    [Fact]
    public void ACommandThatCannotExecuteDrawsTheSameRule()
    {
        var command = new Command(() => { }, () => false);
        using var view = Render(new SkUiButton { FillColor = Colors.Red, Command = command });
        using var node = Render(new SkUiCoreButton().SetFillColor(Colors.Red).SetCommand(command));
        Assert.Equal(SKColors.Red, CenterOf(view));
        Assert.Equal(view.Pixels, node.Pixels);
        using var defaultFill = Render(new SkUiButton { Command = command });
        Assert.Equal(Sk(SkUiColors.Disabled), CenterOf(defaultFill));
    }

    [Fact]
    public void ADisabledVisualStatesFillShows()
    {
        var button = new SkUiButton();
        var group = new VisualStateGroup { Name = "CommonStates" };
        group.States.Add(new VisualState { Name = "Normal" });
        var disabledState = new VisualState { Name = "Disabled" };
        disabledState.Setters.Add(new Setter { Property = SkUiButton.FillColorProperty, Value = Colors.Green });
        group.States.Add(disabledState);
        VisualStateManager.SetVisualStateGroups(button, [group]);

        using (var enabled = Render(button))
            Assert.Equal(Sk(SkUiColors.ButtonBackground), CenterOf(enabled));
        button.IsEnabled = false;
        using (var disabled = Render(button))
            Assert.Equal(Sk(Colors.Green), CenterOf(disabled));
    }

    [Fact]
    public void ASchemeWithoutAButtonFillKeepsDisabledButtonsWithoutOne()
    {
        var previous = SkUiColorScheme.Current;
        try
        {
            SkUiColorScheme.Current = IosSkUiColorScheme.Light; // a system button: tinted text, no fill
            using var _ = SkUiTestHelpers.UseBundledFont();
            using var bitmap = RenderDisabled(new SkUiButton { Text = "MMMM", FontFamily = SkUiTestHelpers.BundledFontFamily },
                TextButton(new SkUiCoreButton(), blackText: false));
            Assert.Equal(0, bitmap.GetPixel(2, 2).Alpha);
            Assert.True(MaxAlpha(bitmap) < 128);
        }
        finally
        {
            SkUiColorScheme.Current = previous;
        }
    }

    [Fact]
    public void TheLookSetsTheDimmedContentsOpacity()
    {
        var previous = SkUiLook.Current;
        try
        {
            SkUiLook.Current = new HalfDimLook();
            using var _ = SkUiTestHelpers.UseBundledFont();
            using var bitmap = RenderDisabled(
                new SkUiButton { Text = "MMMM", FontFamily = SkUiTestHelpers.BundledFontFamily, TextColor = Colors.Black, FillColor = Colors.Transparent },
                TextButton(new SkUiCoreButton().SetFillColor(Colors.Transparent)));
            Assert.InRange(MaxAlpha(bitmap), 127, 128);
        }
        finally
        {
            SkUiLook.Current = previous;
        }
    }

    [Fact]
    public void ImageButtonsKeepTheirBackgroundUnlessDisabledFillColorIsSet()
    {
        var view = new SkUiImageButton { Background = Colors.Blue, CornerRadius = 8 };
        var node = new SkUiCoreImageButton().SetCornerRadius(8);
        node.SetBackground(new SolidPaint(Colors.Blue));
        using var kept = RenderDisabled(view, node);
        Assert.True(CenterOf(kept).Blue > 100 && CenterOf(kept).Red == 0, CenterOf(kept).ToString()); // the look's tint over blue

        view.DisabledFillColor = Colors.Red;
        node.SetDisabledFillColor(Colors.Red);
        using var replaced = RenderDisabled(view, node);
        Assert.True(CenterOf(replaced).Red > 100 && CenterOf(replaced).Blue == 0, CenterOf(replaced).ToString());

        // Square image buttons too (they draw the background as a plain view otherwise).
        using var square = RenderDisabled(new SkUiImageButton { Background = Colors.Blue, DisabledFillColor = Colors.Red },
            new SkUiCoreImageButton().SetDisabledFillColor(Colors.Red));
        Assert.True(CenterOf(square).Red > 100 && CenterOf(square).Blue == 0, CenterOf(square).ToString());
        view.IsEnabled = true;
        using var enabled = Render(view);
        Assert.Equal(SKColors.Blue, CenterOf(enabled));
    }

    private sealed class HalfDimLook : DefaultSkUiLook
    {
        public override double DisabledButtonContentOpacity => 0.5;
    }
}
