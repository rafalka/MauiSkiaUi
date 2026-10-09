using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Text that fits its label (Phase C3 in ImplementationPlan.md): <c>ShrinkToFit</c> / <c>MinimumFontScale</c>,
/// <c>AllowsTightening</c> and <c>GrowToFill</c> / <c>MaximumFontScale</c> on both label layers, the engine's fit
/// (<see cref="SkUiTextFit"/>) for plain text, spans and custom breakers, and its cache.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class LabelShrinkToFitTests
{
    private const string Digits = "0123456789";
    // "aaaa bbbb" is 9 advances: two words a line at 9 advances wide.
    private const string Words = "aaaa bbbb cccc dddd eeee ffff";

    private static readonly SKTypeface Mono = SKTypeface.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "RobotoMono-Regular.ttf"));

    /// <summary>Roboto Mono advance at 16 DIPs (every glyph).</summary>
    private static readonly float Advance = MeasureFont(font => font.MeasureText("0"));

    /// <summary>Roboto Mono line spacing at 16 DIPs.</summary>
    private static readonly float LineSpacing = MeasureFont(font => font.Spacing);

    /// <summary>The widest a fitted size may fall short of the largest that fits: one search step of font size, as a scale.</summary>
    private const double StepScale = SkUiTextFit.Step / 16;

    private static float MeasureFont(Func<SKFont, float> measure)
    {
        using var font = new SKFont(Mono, 16) { LinearMetrics = true };
        return measure(font);
    }

    private static SkUiLabel Label(string text, LineBreakMode mode = LineBreakMode.TailTruncation) =>
        new() { Text = text, FontFamily = SkUiTestHelpers.BundledFontFamily, LineBreakMode = mode, ShrinkToFit = true };

    private static SkUiCoreLabel Core(string text) =>
        new SkUiCoreLabel().SetText(text).SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetLineBreakMode(LineBreakMode.TailTruncation).SetShrinkToFit(true);

    private static Size Measure(IView view, double width, double height = double.PositiveInfinity) => view.Measure(width, height);

    /// <summary>
    /// Fits <paramref name="text"/> with the engine: the stock lines drawn (a breaker that records <c>Break()</c>, so the
    /// fit is the stock mode's) and the measured size.
    /// </summary>
    private static (IReadOnlyList<string> Lines, Size Size) Fit(string text, double width, LineBreakMode mode, double height = double.PositiveInfinity,
        int maxLines = -1, double minimum = SkUiTextFit.DefaultMinimumScale, double maximum = 1, bool tightening = false)
    {
        IReadOnlyList<string> lines = [];
        var style = new SkUiTextStyle(Mono, 16, mode, context => lines = context.Break(), maxLines, MinimumScale: (float)minimum, MaximumScale: (float)maximum,
            AllowsTightening: tightening);
        var size = new SkUiTextLayout().Measure(text, style, default, width, height);
        return (lines, size);
    }

    [Fact]
    public void IsOffByDefaultAndShrinksOneLineJustEnough()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label(Digits);
        label.ShrinkToFit = false;
        Assert.Equal(SkUiTextFit.DefaultMinimumScale, label.MinimumFontScale);
        var width = Advance * 8;
        Assert.Equal(LineSpacing, Measure(label, width).Height, 0.5); // truncated at full size

        label.ShrinkToFit = true;
        var fitted = Measure(label, width);
        // The largest size the width allows: 8 of 10 advances, less than a step below it.
        Assert.InRange(fitted.Width, width * (1 - StepScale * 10 / 8) - 0.01, width + 0.01);
        Assert.Equal(LineSpacing * fitted.Width / (Advance * 10), fitted.Height, 0.5);
        var (lines, _) = Fit(Digits, width, LineBreakMode.TailTruncation);
        Assert.Equal([Digits], lines); // shrunk, not truncated

        // Text that fits keeps its size.
        Assert.Equal(new Size(Advance * 10, LineSpacing), Measure(label, Advance * 20), new SizeComparer(0.5));
    }

    [Fact]
    public void TextStillTooLongIsDrawnAtTheMinimumAndBrokenByTheMode()
    {
        var (lines, size) = Fit(Digits, Advance * 4, LineBreakMode.TailTruncation, minimum: 0.5);
        Assert.EndsWith("...", Assert.Single(lines));
        Assert.Equal(LineSpacing * 0.5, size.Height, 0.5);

        // A minimum of 1 never shrinks.
        (lines, size) = Fit(Digits, Advance * 8, LineBreakMode.TailTruncation, minimum: 1);
        Assert.EndsWith("...", Assert.Single(lines));
        Assert.Equal(LineSpacing, size.Height, 0.5);

        // NoWrap shrinks the line into the width too.
        (lines, size) = Fit(Digits, Advance * 8, LineBreakMode.NoWrap);
        Assert.Equal([Digits], lines);
        Assert.True(size.Width <= Advance * 8 + 0.01, $"{size.Width}");
    }

    [Fact]
    public void WordWrapShrinksAWordInsteadOfBreakingItButWrapsWords()
    {
        const string word = "Supercalifragilistic"; // 20 advances
        var (lines, size) = Fit(word, Advance * 15, LineBreakMode.WordWrap);
        Assert.Equal([word], lines);
        Assert.True(size.Width <= Advance * 15 + 0.01);

        // Words that wrap whole keep the size (no height limit).
        (lines, size) = Fit(Words, Advance * 9, LineBreakMode.WordWrap);
        Assert.Equal(["aaaa bbbb", "cccc dddd", "eeee ffff"], lines);
        Assert.Equal(LineSpacing * 3, size.Height, 0.5);

        // Character wrap breaks words by design: it never shrinks for them.
        (lines, _) = Fit(word, Advance * 15, LineBreakMode.CharacterWrap);
        Assert.Equal(2, lines.Count);
    }

    [Fact]
    public void WrappedTextShrinksToItsHeightAndMaxLines()
    {
        // Three lines in the height of two: smaller, all words kept, the lines no taller than the slot.
        var height = LineSpacing * 2;
        var (lines, size) = Fit(Words, Advance * 9, LineBreakMode.WordWrap, height);
        Assert.Equal(Words, string.Join(' ', lines));
        Assert.True(size.Height <= height + SkUiTextFit.HeightTolerance, $"{size.Height} > {height}");
        Assert.True(size.Height > height * 0.6, $"{size.Height}: shrunk too far");

        // MaxLines 2 with tail truncation: two lines without an ellipsis (14 advances a line at about 0.64).
        (lines, _) = Fit(Words, Advance * 9, LineBreakMode.TailTruncation, maxLines: 2);
        Assert.Equal(["aaaa bbbb cccc", "dddd eeee ffff"], lines);

        // On the label: a height request is the slot.
        using var font = SkUiTestHelpers.UseBundledFont();
        var label = Label(Words, LineBreakMode.WordWrap);
        label.HeightRequest = height;
        Assert.True(Measure(label, Advance * 9).Height <= height + SkUiTextFit.HeightTolerance);
        var core = Core(Words).SetLineBreakMode(LineBreakMode.WordWrap);
        Assert.Equal(size.Height, core.Measure(Advance * 9, height).Height, 0.5);
    }

    [Fact]
    public void SpansAndHtmlShrinkAlike()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = new SkUiLabel
        {
            FontFamily = SkUiTestHelpers.BundledFontFamily,
            LineBreakMode = LineBreakMode.TailTruncation,
            FormattedText = new FormattedString { Spans = { new Span { Text = "01234" }, new Span { Text = "56789", FontSize = 32 } } }
        };
        var natural = Measure(label, double.PositiveInfinity);
        Assert.Equal(Advance * 15, natural.Width, 0.5); // 5 at 16, 5 at 32
        label.ShrinkToFit = true;
        var fitted = Measure(label, natural.Width * 0.75);
        Assert.InRange(fitted.Width, natural.Width * (0.75 - StepScale) - 0.01, natural.Width * 0.75 + 0.01);
        Assert.Equal(natural.Height * fitted.Width / natural.Width, fitted.Height, 0.5);

        var html = Label("<b>0123456789</b>");
        html.TextType = TextType.Html;
        Assert.True(Measure(html, Advance * 8).Height < LineSpacing - 1);

        var core = Core(string.Empty).SetSpans(new SkUiCoreSpan("01234"), new SkUiCoreSpan("56789") { FontSize = 32 });
        Assert.Equal(fitted.Width, core.Measure(natural.Width * 0.75, double.PositiveInfinity).Width, 0.5);
    }

    [Fact]
    public void CoreLabelShrinksAsTheLabel()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var core = Core(Digits);
        Assert.True(core.ShrinkToFit);
        var changes = new List<string?>();
        core.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        Assert.Equal(Measure(Label(Digits), Advance * 8), core.Measure(Advance * 8, double.PositiveInfinity));
        core.SetMinimumFontScale(0.9);
        Assert.Contains(nameof(SkUiCoreLabel.MinimumFontScale), changes);
        Assert.EndsWith("...", Assert.Single(Fit(Digits, Advance * 8, LineBreakMode.TailTruncation, minimum: 0.9).Lines));
        Assert.Equal(LineSpacing * 0.9, core.Measure(Advance * 8, double.PositiveInfinity).Height, 0.5);
        core.SetShrinkToFit(false);
        Assert.Equal(LineSpacing, core.Measure(Advance * 8, double.PositiveInfinity).Height, 0.5);
    }

    [Fact]
    public void MinimumFontScaleIsAFraction()
    {
        var label = new SkUiLabel();
        Assert.Throws<ArgumentOutOfRangeException>(() => label.SetMinimumFontScale(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => label.SetMinimumFontScale(1.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => label.SetMinimumFontScale(double.NaN));
        label.MinimumFontScale = -1; // ignored, as MAUI ignores invalid values
        Assert.Equal(SkUiTextFit.DefaultMinimumScale, label.MinimumFontScale);
        Assert.Equal(1, label.SetMinimumFontScale(1).MinimumFontScale);
        Assert.Equal(0.25, label.SetShrinkToFit(true).SetMinimumFontScale(0.25).MinimumFontScale);

        var core = new SkUiCoreLabel();
        Assert.Throws<ArgumentOutOfRangeException>(() => core.SetMinimumFontScale(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => core.MinimumFontScale = 2);
        Assert.Equal(SkUiTextFit.DefaultMinimumScale, core.MinimumFontScale);
    }

    [Fact]
    public void ACustomBreakerGetsTheTextShrunkByItsMode()
    {
        // FirstFit draws the text as it is when it fits: after shrinking it does, so the shorter forms are not needed.
        IReadOnlyList<string> drawn = [];
        var forms = SkUiTextLineBreakers.FirstFit(_ => ["12345.67", "12346"]);
        SkUiTextLineBreaker recording = context => drawn = forms(context);
        var style = new SkUiTextStyle(Mono, 16, LineBreakMode.TailTruncation, recording, MinimumScale: 0.5f);
        var layout = new SkUiTextLayout();
        layout.Measure("12345.6789", style, default, Advance * 8);
        Assert.Equal(["12345.6789"], drawn);
        // Below the minimum, the breaker shortens the text at the minimum.
        layout.Measure("12345.6789", style, default, Advance * 3);
        Assert.Equal(["12346"], drawn);
    }

    [Fact]
    public void AnArrangedSlotSmallerThanTheMeasuredOneShrinksTheDrawnText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        int InkHeight(bool shrink)
        {
            var label = Label(Digits);
            label.ShrinkToFit = shrink;
            ((IView)label).Measure(double.PositiveInfinity, double.PositiveInfinity); // natural size
            ((IView)label).Arrange(new Rect(0, 0, Advance * 6, LineSpacing));
            using var bitmap = new SKBitmap(200, 40);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);
            label.Paint(canvas);
            var rows = 0;
            for (var y = 0; y < bitmap.Height; y++)
            {
                var inked = false;
                for (var x = 0; x < bitmap.Width && !inked; x++)
                    inked = bitmap.GetPixel(x, y).Red < 200;
                if (inked) rows++;
            }
            return rows;
        }
        var full = InkHeight(shrink: false);
        var shrunk = InkHeight(shrink: true);
        Assert.InRange(shrunk, full * 0.5, full * 0.75); // drawn at about 6/10 of the size
    }

    [Fact]
    public void MeasureAndDrawShareTheFit()
    {
        var layout = new SkUiTextLayout();
        var style = new SkUiTextStyle(Mono, 16, LineBreakMode.TailTruncation, MinimumScale: 0.5f);
        var width = Advance * 8;
        var size = layout.Measure(Digits, style, default, width);
        var afterMeasure = layout.LayoutCount;
        Assert.InRange(afterMeasure, 2, 5); // full size, a guess, its neighbour, the fitted lines
        layout.Measure(Digits, style, default, width);
        using var bitmap = new SKBitmap(200, 40);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint();
        // Arranged at the measured size: the same lines.
        layout.Draw(canvas, Digits, style, default, size.Width, size.Height, TextAlignment.Start, TextAlignment.Start, paint);
        layout.Draw(canvas, Digits, style, default, width, size.Height, TextAlignment.Start, TextAlignment.Start, paint);
        Assert.Equal(afterMeasure, layout.LayoutCount);
        // A new text fits again.
        layout.Invalidate();
        layout.Measure(Digits + Digits, style, default, width);
        Assert.True(layout.LayoutCount > afterMeasure);
    }

    [Fact]
    public void ButtonsInheritIt()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        // The text beside a button's image (buttons have a minimum height of their own).
        var button = new SkUiButton { Text = Digits, FontFamily = SkUiTestHelpers.BundledFontFamily };
        var text = (SkUiButtonImageLayout.IText)button;
        Assert.Equal(LineSpacing, text.MeasureText(Advance * 8).Height, 0.5);
        button.ShrinkToFit = true;
        Assert.True(text.MeasureText(Advance * 8).Height < LineSpacing - 1);
    }

    [Fact]
    public void TighteningFitsTextBeforeItIsTruncatedOrShrunk()
    {
        // At most 0.8 DIPs a character at 16 DIPs: a line 3 DIPs too long fits at full size.
        var slightly = Advance * 10 - 3;
        var (lines, size) = Fit(Digits, slightly, LineBreakMode.TailTruncation, minimum: 1, tightening: true);
        Assert.Equal([Digits], lines);
        Assert.True(size.Width <= slightly + 0.01, $"{size.Width}");
        Assert.True(size.Width > slightly - Advance, $"{size.Width}: tightened too far");
        Assert.Equal(LineSpacing, size.Height, 0.5);
        Assert.EndsWith("...", Assert.Single(Fit(Digits, slightly, LineBreakMode.TailTruncation, minimum: 1).Lines));

        // Too long even fully tightened: truncated, tightened (as many characters as without, or more).
        var narrow = Advance * 8;
        var tight = Assert.Single(Fit(Digits, narrow, LineBreakMode.TailTruncation, minimum: 1, tightening: true).Lines);
        Assert.EndsWith("...", tight);
        Assert.True(tight.Length >= Assert.Single(Fit(Digits, narrow, LineBreakMode.TailTruncation, minimum: 1).Lines).Length);

        // With shrinking: tightened first (full size), then shrunk with the tightening kept, so less shrinking.
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label(Digits);
        label.AllowsTightening = true;
        Assert.Equal(LineSpacing, Measure(label, slightly).Height, 0.5);
        var tightenedThenShrunk = Measure(label, narrow);
        label.AllowsTightening = false;
        var shrunk = Measure(label, narrow);
        Assert.True(tightenedThenShrunk.Height > shrunk.Height + 0.5, $"{tightenedThenShrunk.Height} vs {shrunk.Height}");
        Assert.True(tightenedThenShrunk.Width <= narrow + 0.01);

        var core = Core(Digits).SetShrinkToFit(false).SetAllowsTightening(true);
        Assert.Equal(LineSpacing, core.Measure(slightly, double.PositiveInfinity).Height, 0.5);
        Assert.True(core.Measure(slightly, double.PositiveInfinity).Width <= slightly + 0.01);
    }

    [Fact]
    public void GrowToFillMakesTextAsLargeAsFits()
    {
        // One line: the largest size the width allows, less than a step below it; unlimited, the maximum.
        var (lines, size) = Fit("01234", Advance * 8, LineBreakMode.NoWrap, minimum: 1, maximum: 3);
        Assert.Equal(["01234"], lines);
        Assert.InRange(size.Width, Advance * 8 - SkUiTextFit.Step / 16 * Advance * 5 - 0.01, Advance * 8 + 0.01);
        Assert.Equal(LineSpacing * size.Width / (Advance * 5), size.Height, 0.5);
        Assert.Equal(LineSpacing * 3, Fit("01234", double.PositiveInfinity, LineBreakMode.NoWrap, minimum: 1, maximum: 3).Size.Height, 0.5);

        // Wrapped text grows into its height, all words kept: one word a line past scale 1, six lines in ten line heights.
        var height = LineSpacing * 10;
        (lines, size) = Fit(Words, Advance * 9, LineBreakMode.WordWrap, height, minimum: 1, maximum: 3);
        Assert.Equal(Words, string.Join(' ', lines));
        Assert.True(size.Height <= height + SkUiTextFit.HeightTolerance, $"{size.Height} > {height}");
        Assert.True(size.Height > LineSpacing * 9, $"{size.Height}: grew too little");

        // On both layers, also shrinking: one label grows in a wide slot and shrinks in a narrow one.
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label("01234", LineBreakMode.NoWrap);
        label.GrowToFill = true;
        Assert.Equal(SkUiTextFit.DefaultMaximumScale, label.MaximumFontScale);
        Assert.Equal(LineSpacing * 2, Measure(label, Advance * 20).Height, 0.5); // the maximum: 2
        Assert.True(Measure(label, Advance * 4).Height < LineSpacing);
        var core = Core("01234").SetLineBreakMode(LineBreakMode.NoWrap).SetGrowToFill(true).SetMaximumFontScale(3);
        Assert.Equal(Fit("01234", Advance * 8, LineBreakMode.NoWrap, maximum: 3).Size.Height, core.Measure(Advance * 8, double.PositiveInfinity).Height, 0.5);

        // Spans grow alike.
        var spans = new SkUiLabel
        {
            FontFamily = SkUiTestHelpers.BundledFontFamily,
            LineBreakMode = LineBreakMode.NoWrap,
            FormattedText = new FormattedString { Spans = { new Span { Text = "01234" }, new Span { Text = "56789", FontSize = 32 } } }
        };
        var natural = Measure(spans, double.PositiveInfinity);
        spans.GrowToFill = true;
        var grown = Measure(spans, natural.Width * 1.5);
        Assert.InRange(grown.Width, natural.Width * (1.5 - SkUiTextFit.Step / 16) - 0.01, natural.Width * 1.5 + 0.01);
        Assert.Equal(natural.Height * grown.Width / natural.Width, grown.Height, 0.5);
    }

    [Fact]
    public void GrowingOneLineTakesAFewLayouts()
    {
        var layout = new SkUiTextLayout();
        var style = new SkUiTextStyle(Mono, 16, LineBreakMode.NoWrap, MaximumScale: 4f);
        layout.Measure("01234", style, default, Advance * 13);
        Assert.InRange(layout.LayoutCount, 2, 4); // as set, the guess from the room left, its neighbour
    }

    [Fact]
    public void MaximumFontScaleIsAtLeastOne()
    {
        var label = new SkUiLabel();
        Assert.Throws<ArgumentOutOfRangeException>(() => label.SetMaximumFontScale(0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => label.SetMaximumFontScale(double.PositiveInfinity));
        label.MaximumFontScale = double.NaN; // ignored
        Assert.Equal(SkUiTextFit.DefaultMaximumScale, label.MaximumFontScale);
        Assert.Equal(1, label.SetMaximumFontScale(1).MaximumFontScale);
        Assert.Equal(4, label.SetGrowToFill(true).SetMaximumFontScale(4).MaximumFontScale);
        Assert.True(label.SetAllowsTightening(true).AllowsTightening);

        var core = new SkUiCoreLabel();
        Assert.Throws<ArgumentOutOfRangeException>(() => core.SetMaximumFontScale(0.99));
        Assert.Equal(SkUiTextFit.DefaultMaximumScale, core.MaximumFontScale);
    }

    private sealed class SizeComparer(double tolerance) : IEqualityComparer<Size>
    {
        public bool Equals(Size x, Size y) => Math.Abs(x.Width - y.Width) <= tolerance && Math.Abs(x.Height - y.Height) <= tolerance;
        public int GetHashCode(Size size) => 0;
    }
}
