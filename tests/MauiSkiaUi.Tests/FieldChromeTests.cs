using MauiSkiaUi.Core;
using MauiSkiaUiSamples.Samples.Customisation;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Per-field chrome (M5 in docs/archive/ImplementationPlan-1.0.md): a text field's or picker's own <c>FieldStyle</c> wins over the look's, its
/// <c>FillColor</c>, <c>BorderColor</c>, <c>BorderWidth</c> and <c>CornerRadii</c> replace the style's idle chrome while focus,
/// error and disabled emphasis stay the look's, and <c>TextPadding</c> replaces the look's text padding; the same on both
/// layers, checked pixel by pixel SkUi vs Core.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class FieldChromeTests
{
    private const int Width = 160;
    private const int Height = 44;
    private const int MidY = Height / 2;

    private static SKBitmap Render(Action<SKCanvas> paint)
    {
        var bitmap = new SKBitmap(Width, Height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        paint(canvas);
        return bitmap;
    }

    private static SKBitmap Render(SkUiView view)
    {
        view.WidthRequest = Width;
        view.HeightRequest = Height;
        SkUiTestHelpers.Arrange(view, Width, Height);
        return Render(view.Paint);
    }

    private static SKBitmap Render(SkUiCoreNode node)
    {
        node.SetWidth(Width).SetHeight(Height);
        node.Measure(Width, Height);
        node.Arrange(new Rect(0, 0, Width, Height));
        return Render(node.Paint);
    }

    /// <summary>Renders both, asserts they draw the same and returns the SkUi* rendering.</summary>
    private static SKBitmap RenderBoth(SkUiView view, SkUiCoreNode node)
    {
        var expected = Render(view);
        using var actual = Render(node);
        Assert.Equal(expected.Pixels, actual.Pixels);
        return expected;
    }

    private static SKColor Sk(Color color) => SkUiShapePainter.ToSkColor(color);

    /// <summary>Runs <paramref name="test"/> under a fresh <see cref="DefaultSkUiLook"/> set up by <paramref name="setUp"/>.</summary>
    private static void WithLook(Action<DefaultSkUiLook> setUp, Action test)
    {
        var previous = SkUiLook.Current;
        var look = new DefaultSkUiLook();
        setUp(look);
        SkUiLook.Current = look;
        try
        {
            test();
        }
        finally
        {
            SkUiLook.Current = previous;
        }
    }

    [Fact]
    public void AFieldsOwnStyleWinsOverTheLooks()
    {
        SKColor[] lookWide = [], picker = [];
        WithLook(look =>
        {
            look.TextFieldStyle = SkUiTextFieldStyle.Underline;
            look.PickerFieldStyle = SkUiPickerFieldStyle.Filled;
        }, () =>
        {
            using var entry = Render(new SkUiEntry());
            lookWide = entry.Pixels;
            using var field = Render(new SkUiPicker());
            picker = field.Pixels;
        });
        WithLook(look =>
        {
            look.TextFieldStyle = SkUiTextFieldStyle.Outline;
            look.PickerFieldStyle = SkUiPickerFieldStyle.Outline;
        }, () =>
        {
            using var entry = RenderBoth(new SkUiEntry { FieldStyle = SkUiTextFieldStyle.Underline },
                new SkUiCoreEntry().SetFieldStyle(SkUiTextFieldStyle.Underline));
            Assert.Equal(lookWide, entry.Pixels);
            using var field = RenderBoth(new SkUiPicker { FieldStyle = SkUiPickerFieldStyle.Filled },
                new SkUiCorePicker().SetFieldStyle(SkUiPickerFieldStyle.Filled));
            Assert.Equal(picker, field.Pixels);
            using var outlined = Render(new SkUiEntry());
            Assert.NotEqual(lookWide, outlined.Pixels);
        });
    }

    [Theory]
    [InlineData(SkUiTextFieldStyle.Outline)]
    [InlineData(SkUiTextFieldStyle.Underline)]
    [InlineData(SkUiTextFieldStyle.Filled)]
    [InlineData(SkUiTextFieldStyle.TextBox)]
    [InlineData(SkUiTextFieldStyle.None)]
    public void FillColorFillsTheFieldAsSetAlsoWhileDisabled(SkUiTextFieldStyle style)
    {
        WithLook(_ => { }, () =>
        {
            using var enabled = RenderBoth(new SkUiEntry { FieldStyle = style, FillColor = Colors.Red },
                new SkUiCoreEntry().SetFieldStyle(style).SetFillColor(Colors.Red));
            Assert.Equal(SKColors.Red, enabled.GetPixel(Width / 2, MidY));
            using var disabled = RenderBoth(new SkUiEntry { FieldStyle = style, FillColor = Colors.Red, IsEnabled = false },
                (SkUiCoreNode)new SkUiCoreEntry().SetFieldStyle(style).SetFillColor(Colors.Red).SetIsEnabled(false));
            Assert.Equal(SKColors.Red, disabled.GetPixel(Width / 2, MidY));
        });
    }

    [Theory]
    [InlineData(SkUiPickerFieldStyle.Outline)]
    [InlineData(SkUiPickerFieldStyle.Underline)]
    [InlineData(SkUiPickerFieldStyle.Filled)]
    [InlineData(SkUiPickerFieldStyle.PopUpButton)]
    [InlineData(SkUiPickerFieldStyle.ComboBox)]
    [InlineData(SkUiPickerFieldStyle.None)]
    public void APickersFillColorFillsItsField(SkUiPickerFieldStyle style)
    {
        WithLook(_ => { }, () =>
        {
            using var bitmap = RenderBoth(new SkUiPicker { FieldStyle = style, FillColor = Colors.Red },
                new SkUiCorePicker().SetFieldStyle(style).SetFillColor(Colors.Red));
            Assert.Equal(SKColors.Red, bitmap.GetPixel(Width / 4, MidY));
        });
    }

    [Fact]
    public void BorderColorAndWidthReplaceTheIdleOutline()
    {
        WithLook(_ => { }, () =>
        {
            using var none = RenderBoth(new SkUiEntry { BorderWidth = 0 }, new SkUiCoreEntry().SetBorderWidth(0));
            Assert.All(none.Pixels, pixel => Assert.Equal(0, pixel.Alpha));

            using var thick = RenderBoth(new SkUiEntry { BorderWidth = 3, BorderColor = Colors.Blue },
                new SkUiCoreEntry().SetBorderWidth(3).SetBorderColor(Colors.Blue));
            Assert.Equal(SKColors.Blue, thick.GetPixel(1, MidY));
            Assert.Equal(SKColors.Blue, thick.GetPixel(2, MidY));
            Assert.Equal(0, thick.GetPixel(4, MidY).Alpha);

            using var picker = RenderBoth(new SkUiPicker { BorderWidth = 3, BorderColor = Colors.Blue },
                new SkUiCorePicker().SetBorderWidth(3).SetBorderColor(Colors.Blue));
            Assert.Equal(SKColors.Blue, picker.GetPixel(1, MidY));
        });
    }

    [Fact]
    public void AnErrorStillShowsWithoutAnIdleBorder()
    {
        WithLook(_ => { }, () =>
        {
            using var bitmap = RenderBoth(new SkUiEntry { BorderWidth = 0, HasError = true, ErrorColor = Colors.Red },
                new SkUiCoreEntry().SetBorderWidth(0).SetHasError(true).SetErrorColor(Colors.Red));
            Assert.Equal(SKColors.Red, bitmap.GetPixel(0, MidY));
        });
    }

    [Fact]
    public void CornerRadiiShapeTheChrome()
    {
        WithLook(_ => { }, () =>
        {
            using var square = RenderBoth(new SkUiEntry { FillColor = Colors.Red, BorderWidth = 0, CornerRadii = new CornerRadius(0) },
                new SkUiCoreEntry().SetFillColor(Colors.Red).SetBorderWidth(0).SetCornerRadius(0));
            Assert.Equal(SKColors.Red, square.GetPixel(0, 0));

            using var pill = RenderBoth(new SkUiEntry { FillColor = Colors.Red, BorderWidth = 0, CornerRadius = 100 },
                new SkUiCoreEntry().SetFillColor(Colors.Red).SetBorderWidth(0).SetCornerRadius(100));
            Assert.Equal(0, pill.GetPixel(2, 2).Alpha);
            Assert.Equal(SKColors.Red, pill.GetPixel(Width / 2, 0));

            // Per corner: only the top left is rounded.
            using var corner = RenderBoth(new SkUiPicker { FillColor = Colors.Red, BorderWidth = 0, CornerRadii = new CornerRadius(20, 0, 0, 0) },
                new SkUiCorePicker().SetFillColor(Colors.Red).SetBorderWidth(0).SetCornerRadii(new CornerRadius(20, 0, 0, 0)));
            Assert.Equal(0, corner.GetPixel(1, 1).Alpha);
            Assert.Equal(SKColors.Red, corner.GetPixel(Width - 1, 0));
            Assert.Equal(SKColors.Red, corner.GetPixel(0, Height - 1));
        });
    }

    [Fact]
    public void ASearchBarWithAStyleOfItsOwnDrawsThatFieldAroundItsIcon()
    {
        WithLook(_ => { }, () =>
        {
            using var own = RenderBoth(new SkUiSearchBar { FieldStyle = SkUiTextFieldStyle.Outline, BorderColor = Colors.Blue, BorderWidth = 2 },
                new SkUiCoreSearchBar().SetFieldStyle(SkUiTextFieldStyle.Outline).SetBorderColor(Colors.Blue).SetBorderWidth(2));
            Assert.Equal(SKColors.Blue, own.GetPixel(Width - 1, MidY));
            using var entry = Render(new SkUiEntry { FieldStyle = SkUiTextFieldStyle.Outline, BorderColor = Colors.Blue, BorderWidth = 2 });
            // The search icon at the start is the only difference.
            Assert.NotEqual(entry.Pixels, own.Pixels);
            Assert.Equal(entry.GetPixel(Width - 1, MidY), own.GetPixel(Width - 1, MidY));

            // The look's search field takes the fill, the radii and an idle outline.
            using var pill = RenderBoth(new SkUiSearchBar { FillColor = Colors.Red, CornerRadius = 0, BorderWidth = 2, BorderColor = Colors.Blue },
                new SkUiCoreSearchBar().SetFillColor(Colors.Red).SetCornerRadius(0).SetBorderWidth(2).SetBorderColor(Colors.Blue));
            Assert.Equal(SKColors.Red, pill.GetPixel(Width - 10, MidY));
            Assert.Equal(SKColors.Blue, pill.GetPixel(0, 0));
        });
    }

    [Fact]
    public void TextPaddingReplacesTheLooks()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var look = SkUiLook.Current.TextFieldPadding;
        var padding = new Thickness(look.Left + 10, look.Top + 20, look.Right + 30, look.Bottom + 40);
        var plain = new SkUiEntry { FontFamily = SkUiTestHelpers.BundledFontFamily, Text = "Text" };
        var padded = new SkUiEntry { FontFamily = SkUiTestHelpers.BundledFontFamily, Text = "Text", TextPadding = padding };
        var core = new SkUiCoreEntry().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetText("Text").SetTextPadding(padding);
        var before = ((IView)plain).Measure(double.PositiveInfinity, double.PositiveInfinity);
        var after = ((IView)padded).Measure(double.PositiveInfinity, double.PositiveInfinity);
        Assert.Equal(before.Width + 40, after.Width, 3);
        // Taller by the extra 60 (the plain field may be held at the look's minimum height).
        Assert.True(after.Height > before.Height);
        Assert.Equal(after, core.Measure(double.PositiveInfinity, double.PositiveInfinity));

        padded.TextPadding = null;
        Assert.Equal(before, ((IView)padded).Measure(double.PositiveInfinity, double.PositiveInfinity));
    }

    [Fact]
    public void CornerRadiusIsAUniformViewOfCornerRadii()
    {
        var entry = new SkUiEntry { CornerRadius = 8 };
        Assert.Equal(new CornerRadius(8), entry.CornerRadii);
        entry.CornerRadii = new CornerRadius(1.6, 2, 3, 4);
        Assert.Equal(2, entry.CornerRadius);
        Assert.Equal(new CornerRadius(1.6, 2, 3, 4), entry.CornerRadii);
        entry.CornerRadius = null;
        Assert.Null(entry.CornerRadii);

        var picker = new SkUiDatePicker { CornerRadii = new CornerRadius(5) };
        Assert.Equal(5, picker.CornerRadius);
        picker.CornerRadii = null;
        Assert.Null(picker.CornerRadius);
    }

    [Fact]
    public void InvalidValuesAreIgnoredAndSettersThrow()
    {
        var entry = new SkUiEntry { BorderWidth = -1, CornerRadius = -2, TextPadding = new Thickness(-1), FieldStyle = (SkUiTextFieldStyle)99 };
        Assert.Null(entry.BorderWidth);
        Assert.Null(entry.CornerRadius);
        Assert.Null(entry.TextPadding);
        Assert.Equal(SkUiTextFieldStyle.Default, entry.FieldStyle);
        Assert.Throws<ArgumentOutOfRangeException>(() => entry.SetBorderWidth(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => entry.SetCornerRadii(new CornerRadius(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => entry.SetTextPadding(new Thickness(double.PositiveInfinity)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiPicker().SetFieldStyle((SkUiPickerFieldStyle)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiCoreEntry().SetBorderWidth(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiCorePicker().SetCornerRadius(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiCoreEditor().SetTextPadding(new Thickness(-3)));
    }

    [Fact]
    public void CoreSettersRaisePropertyChanged()
    {
        var entry = new SkUiCoreEntry();
        var changed = new List<string?>();
        entry.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        entry.SetFieldStyle(SkUiTextFieldStyle.Filled).SetTextPadding(new Thickness(2)).SetFillColor(Colors.Red).SetBorderColor(Colors.Blue)
            .SetBorderWidth(2).SetCornerRadius(4);
        Assert.Equal(["FieldStyle", "TextPadding", "FillColor", "BorderColor", "BorderWidth", "CornerRadii"], changed);
        Assert.Equal((SkUiTextFieldStyle.Filled, new Thickness(2), 2d, new CornerRadius(4)), (entry.FieldStyle, entry.TextPadding, entry.BorderWidth, entry.CornerRadii));
    }

    [Fact]
    public void SetCornerRadiusNullGoesBackToTheStylesOnBothLayers()
    {
        var entry = new SkUiEntry().SetCornerRadius(4).SetCornerRadius(null);
        var coreEntry = new SkUiCoreEntry().SetCornerRadius(4).SetCornerRadius(null);
        var picker = new SkUiPicker().SetCornerRadius(4).SetCornerRadius(null);
        var corePicker = new SkUiCorePicker().SetCornerRadius(4.5).SetCornerRadius(null);
        Assert.Equal(((CornerRadius?)null, (CornerRadius?)null, (CornerRadius?)null, (CornerRadius?)null),
            (entry.CornerRadii, coreEntry.CornerRadii, picker.CornerRadii, corePicker.CornerRadii));
    }
}

/// <summary>The per-field chrome from XAML: attributes and a style's setters (nullable thickness and radii use MAUI's converters).</summary>
[Collection(RuntimeXamlCollection.Name)]
public class FieldChromeXamlTests
{
    [Fact]
    public void XamlSetsTheFieldsChrome()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        const string xaml = """
            <VerticalStackLayout xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                                 xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                                 xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <VerticalStackLayout.Resources>
                <Style x:Key="Pill" TargetType="sk:SkUiEntry">
                  <Setter Property="FieldStyle" Value="Outline" />
                  <Setter Property="TextPadding" Value="16,6" />
                  <Setter Property="CornerRadii" Value="22" />
                  <Setter Property="BorderWidth" Value="0" />
                  <Setter Property="FillColor" Value="#EEEEEE" />
                </Style>
              </VerticalStackLayout.Resources>
              <sk:SkUiEntry FieldStyle="None" TextPadding="4,2" CornerRadii="1,2,3,4" BorderWidth="2" BorderColor="Red" FillColor="Blue" />
              <sk:SkUiEntry Style="{StaticResource Pill}" />
              <sk:SkUiPicker FieldStyle="Underline" CornerRadius="6" BorderWidth="1.5" />
            </VerticalStackLayout>
            """;
        var root = new VerticalStackLayout();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var plain = Assert.IsType<SkUiEntry>(root.Children[0]);
        var pill = Assert.IsType<SkUiEntry>(root.Children[1]);
        var picker = Assert.IsType<SkUiPicker>(root.Children[2]);

        Assert.Equal((SkUiTextFieldStyle.None, new Thickness(4, 2), new CornerRadius(1, 2, 3, 4), 2d), (plain.FieldStyle, plain.TextPadding, plain.CornerRadii, plain.BorderWidth));
        Assert.Equal((Colors.Red, Colors.Blue), (plain.BorderColor, plain.FillColor));
        Assert.Equal((SkUiTextFieldStyle.Outline, new Thickness(16, 6), new CornerRadius(22), 0d), (pill.FieldStyle, pill.TextPadding, pill.CornerRadii, pill.BorderWidth));
        Assert.Equal(Color.FromArgb("#EEEEEE"), pill.FillColor);
        Assert.Equal((SkUiPickerFieldStyle.Underline, new CornerRadius(6), 1.5), (picker.FieldStyle, picker.CornerRadii, picker.BorderWidth));
    }

    [Fact]
    public void CompiledXamlSetsTheFieldsChrome()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        // The sample's XAML is compiled by the XAML source generator, as in an app.
        var page = new FieldStylesSample();
        var fields = Descendants(Assert.IsAssignableFrom<SkUiView>(page.SampleContent)).ToList();
        var search = Assert.Single(fields.OfType<SkUiSearchBar>());
        Assert.Equal((Color.FromArgb("#EEF1F3"), 22), (search.FillColor, search.CornerRadius));
        var entries = fields.OfType<SkUiEntry>().ToList();
        Assert.Equal((SkUiTextFieldStyle.Outline, new Thickness(18, 10), new CornerRadius(22), 0d), (entries[0].FieldStyle, entries[0].TextPadding, entries[0].CornerRadii, entries[0].BorderWidth));
        Assert.Equal((SkUiTextFieldStyle.Underline, new Thickness(0, 10)), (entries[1].FieldStyle, entries[1].TextPadding));
        var picker = Assert.Single(fields.OfType<SkUiPicker>());
        Assert.Equal((SkUiPickerFieldStyle.Outline, new CornerRadius(12)), (picker.FieldStyle, picker.CornerRadii));
        Assert.Equal(SkUiTextFieldStyle.None, Assert.Single(fields.OfType<SkUiEditor>()).FieldStyle);
    }

    private static IEnumerable<ISkUiView> Descendants(ISkUiView view)
    {
        yield return view;
        if (view is SkUiView node)
            foreach (var child in node.SkiaChildren)
                foreach (var descendant in Descendants(child))
                    yield return descendant;
    }
}
