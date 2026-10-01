using System.ComponentModel;
using System.Globalization;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// MAUI Label's text properties (Phase P3 in ImplementationPlan.md) on both layers: <c>MaxLines</c>, <c>LineHeight</c>,
/// <c>CharacterSpacing</c>, <c>TextDecorations</c>, <c>TextTransform</c>, Core <c>FontAttributes</c>; and custom line
/// breakers (<see cref="SkUiTextLineBreaker"/>): a custom ellipsis, fewer decimals instead of an ellipsis.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class LabelTextPropertiesTests
{
    // The MAUI docs' Label samples text (Display multiple lines / line height).
    private const string Lorem = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. In facilisis nulla eu felis fringilla vulputate. Nullam porta eleifend lacinia. Donec at iaculis tellus.";

    private static SKTypeface Primary() =>
        SKTypeface.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "RobotoMono-Regular.ttf"));

    private static SKFont Font() => new(Primary(), 16) { LinearMetrics = true };

    /// <summary>Roboto Mono advance at 16 DIPs (every glyph).</summary>
    private static float Advance()
    {
        using var font = Font();
        return font.MeasureText("0");
    }

    private static float LineSpacing()
    {
        using var font = Font();
        return font.Spacing;
    }

    private static SkUiLabel Label(string text) => new() { Text = text, FontFamily = SkUiTestHelpers.BundledFontFamily };

    private static SkUiCoreLabel Core(string text) => new SkUiCoreLabel().SetText(text).SetFontFamily(SkUiTestHelpers.BundledFontFamily);

    private static Size Measure(SkUiLabel label, double width) => ((IView)label).Measure(width, double.PositiveInfinity);

    /// <summary>Wraps <paramref name="breaker"/> to record the lines it returns.</summary>
    private static SkUiTextLineBreaker Recording(SkUiTextLineBreaker breaker, List<IReadOnlyList<string>> calls) => context =>
    {
        var lines = breaker(context);
        calls.Add(lines);
        return lines;
    };

    /// <param name="scale">Pixels per DIP (2: a Retina screen; phones are 2–3).</param>
    private static SKBitmap Render(SkUiLabel label, int width, int height, float scale = 1)
    {
        SkUiTestHelpers.Arrange(label, width, height);
        var bitmap = new SKBitmap((int)(width * scale), (int)(height * scale));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        canvas.Scale(scale);
        label.Paint(canvas);
        return bitmap;
    }

    /// <summary>Inked pixels per row (also the faint antialiased edge of a thin underline).</summary>
    private static int[] InkRows(SKBitmap bitmap)
    {
        var rows = new int[bitmap.Height];
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).Red < 200) rows[y]++;
        return rows;
    }

    private static (int Left, int Right) InkSpan(SKBitmap bitmap, int y)
    {
        var (left, right) = (int.MaxValue, -1);
        for (var x = 0; x < bitmap.Width; x++)
            if (bitmap.GetPixel(x, y).Red < 200) (left, right) = (Math.Min(left, x), x);
        return (left, right);
    }

    [Fact]
    public void MaxLinesKeepsThatManyLinesOnBothLayers()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label(Lorem);
        var unlimited = Measure(label, 200).Height;
        label.MaxLines = 2;
        var limited = Measure(label, 200);
        Assert.Equal(LineSpacing() * 2, limited.Height, 0.5);
        Assert.True(unlimited > limited.Height * 2);
        Assert.Equal(limited, Core(Lorem).SetMaxLines(2).Measure(200, double.PositiveInfinity));
        label.MaxLines = 0; // as -1: no limit
        Assert.Equal(unlimited, Measure(label, 200).Height, 0.5);
    }

    [Fact]
    public void TailTruncationWithMaxLinesWrapsAndEndsTheLastLineWithTheEllipsis()
    {
        var lines = SkUiTestHelpers.BreakLines(Lorem, 200, LineBreakMode.TailTruncation, Primary(), maxLines: 2);
        Assert.Equal(2, lines.Count);
        Assert.Equal(SkUiTestHelpers.BreakLines(Lorem, 200, LineBreakMode.WordWrap, Primary())[0], lines[0]);
        Assert.EndsWith("...", lines[1]);
        Assert.DoesNotContain(" ...", lines[1]); // trailing spaces go before the ellipsis
        using var font = Font();
        Assert.All(lines, line => Assert.True(font.MeasureText(line) <= 200.5, $"'{line}' overflows"));
        // Without MaxLines, as before (and as MAUI): one truncated line per paragraph.
        Assert.EndsWith("...", Assert.Single(SkUiTestHelpers.BreakLines(Lorem, 200, LineBreakMode.TailTruncation, Primary())));
    }

    [Fact]
    public void MaxLinesCountsParagraphsAndOnlyTailTruncationAddsAnEllipsis()
    {
        const string text = "one\ntwo\nthree";
        Assert.Equal(["one", "two"], SkUiTestHelpers.BreakLines(text, 500, LineBreakMode.WordWrap, Primary(), maxLines: 2));
        Assert.Equal(["one", "two"], SkUiTestHelpers.BreakLines(text, 500, LineBreakMode.NoWrap, Primary(), maxLines: 2));
        Assert.Equal(["one", "two..."], SkUiTestHelpers.BreakLines(text, 500, LineBreakMode.TailTruncation, Primary(), maxLines: 2));
        Assert.Equal(["one", "two", "three"], SkUiTestHelpers.BreakLines(text, 500, LineBreakMode.TailTruncation, Primary(), maxLines: 3));
        // The ellipsis still fits: the kept line gives way to it.
        var narrow = Advance() * 5.5;
        Assert.Equal(["one", "tw..."], SkUiTestHelpers.BreakLines(text, narrow, LineBreakMode.TailTruncation, Primary(), maxLines: 2));
    }

    [Fact]
    public void LineHeightScalesEveryLineAndCentersTheGlyphsInIt()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label("one\ntwo");
        var normal = Measure(label, 500).Height;
        label.LineHeight = 1.8;
        Assert.Equal(normal * 1.8, Measure(label, 500).Height, 0.5);
        Assert.Equal(Measure(label, 500), Core("one\ntwo").SetLineHeight(1.8).Measure(500, double.PositiveInfinity));
        label.LineHeight = -1;
        Assert.Equal(normal, Measure(label, 500).Height, 0.5);

        // Half-leading: a line twice as high draws its glyphs half a line lower.
        static int InkTop(double lineHeight)
        {
            var single = Label("Hg");
            single.TextColor = Colors.Black;
            single.LineHeight = lineHeight;
            using var bitmap = Render(single, 100, 80);
            return Array.FindIndex(InkRows(bitmap), count => count > 0);
        }
        Assert.Equal(LineSpacing() / 2, InkTop(2) - InkTop(1), 1.5);
    }

    [Fact]
    public void CharacterSpacingWidensEveryCharacterAndKeepsTheFastPath()
    {
        const string text = "Item 0001";
        var plain = new SkUiTextLayout().Measure(text, new SkUiTextStyle(Primary(), 16), default, double.PositiveInfinity).Width;
        var spacedLayout = new SkUiTextLayout();
        var spaced = spacedLayout.Measure(text, new SkUiTextStyle(Primary(), 16, CharacterSpacing: 2), default, double.PositiveInfinity).Width;
        Assert.Equal(plain + 2 * text.Length, spaced, 0.5);
        Assert.True(spacedLayout.LastLayoutSimple); // plain Latin still skips HarfBuzz
        var shaped = new SkUiTextLayout().Measure(text, new SkUiTextStyle(Primary(), 16, CharacterSpacing: 2, Rendering: SkUiTextRendering.Shaped), default, double.PositiveInfinity).Width;
        Assert.Equal(spaced, shaped, 0.5);

        // Wrapping and truncation count the spacing.
        using var font = Font();
        foreach (var line in SkUiTestHelpers.BreakLines(Lorem, 200, LineBreakMode.WordWrap, Primary(), characterSpacing: 3))
            Assert.True(font.MeasureText(line) + 3 * line.Length <= 200.5, $"'{line}' overflows");
        var truncated = Assert.Single(SkUiTestHelpers.BreakLines(Lorem, 200, LineBreakMode.TailTruncation, Primary(), characterSpacing: 3));
        Assert.True(font.MeasureText(truncated) + 3 * truncated.Length <= 200.5);
    }

    [Theory]
    [InlineData(SkUiTextRendering.Auto)]
    [InlineData(SkUiTextRendering.Shaped)]
    public void CharacterSpacingIsDrawn(SkUiTextRendering rendering)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        int InkWidth(double spacing)
        {
            var label = Label("iiii");
            label.TextColor = Colors.Black;
            label.TextRendering = rendering;
            label.CharacterSpacing = spacing;
            using var bitmap = Render(label, 200, 30);
            var rows = InkRows(bitmap);
            var y = Array.IndexOf(rows, rows.Max());
            var (left, right) = InkSpan(bitmap, y);
            return right - left;
        }
        Assert.InRange(InkWidth(10) - InkWidth(0), 3 * 10 - 2, 3 * 10 + 2);
    }

    [Fact]
    public void DecorationsDrawUnderAndThroughEachLineWithoutChangingItsSize()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        SKBitmap Draw(TextDecorations decorations, FlowDirection flow = FlowDirection.MatchParent)
        {
            var label = Label("xxxx");
            label.TextColor = Colors.Black;
            label.TextDecorations = decorations;
            label.FlowDirection = flow;
            return Render(label, 200, 30);
        }
        using var plain = Draw(TextDecorations.None);
        using var underline = Draw(TextDecorations.Underline);
        using var strikethrough = Draw(TextDecorations.Strikethrough);
        var (plainRows, underRows, strikeRows) = (InkRows(plain), InkRows(underline), InkRows(strikethrough));
        var glyphBottom = Array.FindLastIndex(plainRows, count => count > 0);
        var glyphTop = Array.FindIndex(plainRows, count => count > 0);
        var underlineRow = Array.FindLastIndex(underRows, count => count > 0);
        Assert.True(underlineRow > glyphBottom, "the underline is below the glyphs");
        // It spans the line's glyphs.
        var spans = Enumerable.Range(glyphTop, glyphBottom - glyphTop + 1).Select(y => InkSpan(plain, y)).ToArray();
        var glyphs = (Left: spans.Min(span => span.Left), Right: spans.Max(span => span.Right));
        var line = InkSpan(underline, underlineRow);
        Assert.InRange(line.Left, glyphs.Left - 3, glyphs.Left + 1);
        Assert.InRange(line.Right, glyphs.Right - 1, glyphs.Right + 3);
        // The strikethrough fills a row inside the glyphs, across the gaps between them.
        Assert.Contains(Enumerable.Range(glyphTop, glyphBottom - glyphTop + 1), y => strikeRows[y] >= line.Right - line.Left - 2 && strikeRows[y] > plainRows[y]);
        Assert.Equal(Array.FindLastIndex(strikeRows, count => count > 0), glyphBottom);

        // A right-to-left line is drawn at the right edge; its underline goes with it.
        using var rtl = Draw(TextDecorations.Underline, FlowDirection.RightToLeft);
        var rtlRow = Array.FindLastIndex(InkRows(rtl), count => count > 0);
        Assert.True(InkSpan(rtl, rtlRow).Left > 100);

        var measured = Label("xxxx");
        var size = Measure(measured, 200);
        measured.TextDecorations = TextDecorations.Underline | TextDecorations.Strikethrough;
        Assert.Equal(size, Measure(measured, 200));
    }

    [Fact]
    public void TextTransformChangesTheDisplayedTextOnly()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var seen = new List<string>();
        SkUiTextLineBreaker capture = context => { seen.Add(context.Text); return context.Break(); };
        var label = Label("Hello World");
        label.LineBreaker = capture;
        label.TextTransform = TextTransform.Uppercase;
        Measure(label, 500);
        Assert.Equal("HELLO WORLD", seen[^1]);
        Assert.Equal("Hello World", label.Text);
        label.TextTransform = TextTransform.Lowercase;
        Measure(label, 500);
        Assert.Equal("hello world", seen[^1]);
        label.TextTransform = TextTransform.None;
        Measure(label, 500);
        Assert.Equal("Hello World", seen[^1]);

        var core = Core("Hello").SetLineBreaker(capture).SetTextTransform(TextTransform.Uppercase);
        core.Measure(500, double.PositiveInfinity);
        Assert.Equal("HELLO", seen[^1]);
        Assert.Equal("Hello", core.Text);
    }

    [Fact]
    public void CoreLabelHasFontAttributesAndTheTextProperties()
    {
        var core = new SkUiCoreLabel();
        var changed = new List<string?>();
        core.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        core.SetFontAttributes(FontAttributes.Bold).SetMaxLines(3).SetLineHeight(1.2).SetCharacterSpacing(1)
            .SetTextDecorations(TextDecorations.Underline).SetTextTransform(TextTransform.Uppercase).SetLineBreakMode(LineBreakMode.TailTruncation);
        Assert.Equal([nameof(SkUiCoreLabel.FontAttributes), nameof(SkUiCoreLabel.MaxLines), nameof(SkUiCoreLabel.LineHeight), nameof(SkUiCoreLabel.CharacterSpacing),
            nameof(SkUiCoreLabel.TextDecorations), nameof(SkUiCoreLabel.TextTransform), nameof(SkUiCoreLabel.LineBreakMode)], changed);
        Assert.Equal((FontAttributes.Bold, 3, 1.2, 1d, TextDecorations.Underline, TextTransform.Uppercase, LineBreakMode.TailTruncation),
            (core.FontAttributes, core.MaxLines, core.LineHeight, core.CharacterSpacing, core.TextDecorations, core.TextTransform, core.LineBreakMode));
        Assert.Throws<ArgumentOutOfRangeException>(() => core.SetLineHeight(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => core.SetCharacterSpacing(double.PositiveInfinity));

        // The same font as the SkUi* label, with bold resolved from the system families.
        var label = new SkUiLabel { Text = "Bold text", FontAttributes = FontAttributes.Bold };
        Assert.Equal(Measure(label, 500), new SkUiCoreLabel().SetText("Bold text").SetFontAttributes(FontAttributes.Bold).Measure(500, double.PositiveInfinity));
    }

    [Fact]
    public void SystemFamiliesResolveTheirBoldAndItalicFaces()
    {
        if (string.IsNullOrEmpty(SKTypeface.Default.FamilyName))
            return; // a host without system fonts (bare Linux CI)
        // The default family (no FontFamily): an empty family name alone would give the regular face for any style.
        Assert.True(SkUiTypefaces.HasBold(SkUiTypefaces.Resolve(null, FontAttributes.Bold)));
        Assert.True(SkUiTypefaces.HasItalic(SkUiTypefaces.Resolve(null, FontAttributes.Italic)));
        Assert.False(SkUiTypefaces.HasBold(SkUiTypefaces.Resolve(null)));
        // An unknown family falls back to the default family, keeping the style.
        Assert.True(SkUiTypefaces.HasBold(SkUiTypefaces.Resolve("No Such Family 42", FontAttributes.Bold)));
    }

    [Fact]
    public void ARegisteredFontWithoutStyledFilesGetsSyntheticBoldAndItalic()
    {
        using var _ = SkUiTestHelpers.UseBundledFont(); // Roboto Mono Regular only
        // At 2 pixels per DIP: Skia's synthetic bold widens outlines by about size / 24, under a pixel at 1x.
        const float scale = 2;
        SKBitmap Draw(string text, FontAttributes attributes, bool core = false)
        {
            if (!core)
            {
                var label = Label(text);
                label.TextColor = Colors.Black;
                label.FontAttributes = attributes;
                return Render(label, 120, 40, scale);
            }
            var node = Core(text).SetTextColor(Colors.Black).SetFontAttributes(attributes);
            node.Measure(120, 40);
            node.Arrange(new Rect(0, 0, 120, 40));
            var bitmap = new SKBitmap((int)(120 * scale), (int)(40 * scale));
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);
            canvas.Scale(scale);
            node.Paint(canvas);
            return bitmap;
        }
        static int Ink(SKBitmap bitmap) => InkRows(bitmap).Sum();

        using var regular = Draw("Hello", FontAttributes.None);
        using var bold = Draw("Hello", FontAttributes.Bold);
        using var coreBold = Draw("Hello", FontAttributes.Bold, core: true);
        Assert.True(Ink(bold) > Ink(regular) * 1.12, $"bold {Ink(bold)} vs regular {Ink(regular)}");
        Assert.Equal(Ink(bold), Ink(coreBold));

        // Italic slants a vertical bar: its top is further right than its bottom.
        static double Slant(SKBitmap bitmap)
        {
            var rows = InkRows(bitmap);
            var (top, bottom) = (Array.FindIndex(rows, count => count > 0), Array.FindLastIndex(rows, count => count > 0));
            static double Center((int Left, int Right) span) => (span.Left + span.Right) / 2.0;
            return Center(InkSpan(bitmap, top + 1)) - Center(InkSpan(bitmap, bottom - 1));
        }
        using var upright = Draw("|", FontAttributes.None);
        using var italic = Draw("|", FontAttributes.Italic);
        using var coreItalic = Draw("|", FontAttributes.Italic, core: true);
        Assert.InRange(Slant(upright), -0.5, 0.5);
        Assert.True(Slant(italic) > 2 * scale, $"slant {Slant(italic)}");
        Assert.Equal(Slant(italic), Slant(coreItalic));
    }

    [Theory]
    [InlineData(SkUiTextRendering.Auto)]
    [InlineData(SkUiTextRendering.Shaped)]
    public void JustifyStretchesWrappedLinesButTheLastOfAParagraph(SkUiTextRendering rendering)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        const string text = Lorem + "\nShort line.";
        var lineHeight = LineSpacing();
        SKBitmap Draw(TextAlignment alignment)
        {
            var label = Label(text);
            label.TextColor = Colors.Black;
            label.TextRendering = rendering;
            label.HorizontalTextAlignment = alignment;
            return Render(label, 200, (int)(lineHeight * 12));
        }
        using var start = Draw(TextAlignment.Start);
        using var justified = Draw(TextAlignment.Justify);
        static int RightEdge(SKBitmap bitmap, float lineHeight, int line)
        {
            var right = -1;
            for (var y = (int)(line * lineHeight) + 1; y < (int)((line + 1) * lineHeight) - 1; y++)
                right = Math.Max(right, InkSpan(bitmap, y).Right);
            return right;
        }
        // Each wrapped line's last glyph moves right by what the line was short of the width; a paragraph's last line
        // stays, and so does a line of one word (no spaces to widen).
        using var font = Font();
        var lines = SkUiTestHelpers.BreakLines(text, 200, LineBreakMode.WordWrap, Primary());
        for (var line = 0; line < lines.Count; line++)
        {
            var paragraphEnd = line >= lines.Count - 2;
            var expected = paragraphEnd || !lines[line].Contains(' ') ? 0 : 200 - font.MeasureText(lines[line]);
            var moved = RightEdge(justified, lineHeight, line) - RightEdge(start, lineHeight, line);
            Assert.True(Math.Abs(moved - expected) <= 1.5, $"line {line} '{lines[line]}' moved {moved}, expected {expected:0.#}");
        }

        var measured = Label(text);
        measured.HorizontalTextAlignment = TextAlignment.Justify;
        Assert.Equal(200, Measure(measured, 200).Width, 0.5); // justified lines are as wide as the label
        measured.HorizontalTextAlignment = TextAlignment.Start;
        Assert.True(Measure(measured, 200).Width < 200);
    }

    [Fact]
    public void VerticalJustifySpreadsTheLinesOverTheHeight()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        int InkBottom(TextAlignment vertical, string text)
        {
            var label = Label(text);
            label.TextColor = Colors.Black;
            label.VerticalTextAlignment = vertical;
            using var bitmap = Render(label, 200, 200);
            return Array.FindLastIndex(InkRows(bitmap), count => count > 0);
        }
        Assert.True(InkBottom(TextAlignment.Justify, "one\ntwo") > 180);
        Assert.Equal(InkBottom(TextAlignment.Start, "one\ntwo"), InkBottom(TextAlignment.Start, "one\ntwo"));
        Assert.Equal(InkBottom(TextAlignment.Start, "one"), InkBottom(TextAlignment.Justify, "one")); // a single line stays at the top
    }

    [Fact]
    public void LabelSettersMatchTheProperties()
    {
        var label = new SkUiLabel().SetMaxLines(2).SetLineHeight(1.5).SetCharacterSpacing(2).SetTextDecorations(TextDecorations.Strikethrough)
            .SetTextTransform(TextTransform.Lowercase).SetLineBreaker(SkUiTextLineBreakers.NoWrap);
        Assert.Equal((2, 1.5, 2d, TextDecorations.Strikethrough, TextTransform.Lowercase), (label.MaxLines, label.LineHeight, label.CharacterSpacing, label.TextDecorations, label.TextTransform));
        Assert.Same(SkUiTextLineBreakers.NoWrap, label.LineBreaker);
        Assert.Throws<ArgumentOutOfRangeException>(() => label.SetLineHeight(double.NaN));
        label.CharacterSpacing = double.NaN; // ignored, as MAUI ignores a value its validation rejects
        Assert.Equal(2d, label.CharacterSpacing);
        Assert.Equal(-1, new SkUiLabel().MaxLines);
        Assert.Equal(TextTransform.Default, new SkUiLabel().TextTransform);
    }

    /// <summary>The MAUI docs' Label samples, with only the namespace prefix changed.</summary>
    [Fact]
    public void MauiDocSamplesLoadBindAndMeasure()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        using var font = SkUiTestHelpers.UseBundledFont();
        var xaml = $$"""
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiVerticalStackLayout>
                <sk:SkUiLabel Text="Character spaced text" CharacterSpacing="10" FontFamily="{{SkUiTestHelpers.BundledFontFamily}}" />
                <sk:SkUiLabel Text="This is underlined text." TextDecorations="Underline" />
                <sk:SkUiLabel Text="This is text with strikethrough." TextDecorations="Strikethrough" />
                <sk:SkUiLabel Text="This is underlined text with strikethrough." TextDecorations="Underline, Strikethrough" />
                <sk:SkUiLabel Text="This text will be displayed in uppercase." TextTransform="Uppercase" />
                <sk:SkUiLabel Text="{{Lorem}}" LineBreakMode="WordWrap" MaxLines="{Binding Lines}" FontFamily="{{SkUiTestHelpers.BundledFontFamily}}" />
                <sk:SkUiLabel Text="{{Lorem}}" LineBreakMode="WordWrap" LineHeight="1.8" FontFamily="{{SkUiTestHelpers.BundledFontFamily}}" />
              </sk:SkUiVerticalStackLayout>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var labels = ((SkUiVerticalStackLayout)root.Content).Children.Cast<SkUiLabel>().ToArray();
        var model = new LinesModel { Lines = 2 };
        root.BindingContext = model;

        Assert.Equal(10d, labels[0].CharacterSpacing);
        Assert.Equal(Measure(Label("Character spaced text"), double.PositiveInfinity).Width + 10 * "Character spaced text".Length, Measure(labels[0], double.PositiveInfinity).Width, 0.5);
        Assert.Equal([TextDecorations.Underline, TextDecorations.Strikethrough, TextDecorations.Underline | TextDecorations.Strikethrough],
            labels[1..4].Select(label => label.TextDecorations));
        Assert.Equal(TextTransform.Uppercase, labels[4].TextTransform);

        Assert.Equal(2, labels[5].MaxLines);
        Assert.Equal(LineSpacing() * 2, Measure(labels[5], 300).Height, 0.5);
        model.Lines = 3;
        Assert.Equal(LineSpacing() * 3, Measure(labels[5], 300).Height, 0.5);

        var lineHeight = Label(Lorem);
        Assert.Equal(Measure(lineHeight, 300).Height * 1.8, Measure(labels[6], 300).Height, 0.5);
    }

    private sealed class LinesModel : INotifyPropertyChanged
    {
        private int _lines;
        public event PropertyChangedEventHandler? PropertyChanged;
        public int Lines { get => _lines; set { _lines = value; PropertyChanged?.Invoke(this, new(nameof(Lines))); } }
    }

    [Fact]
    public void ContextDescribesTheLabelAndMeasuresAsTheEngineDraws()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        SkUiTextLineBreakContext? seen = null;
        double measured = 0;
        var label = Label("abc");
        label.Padding = new Thickness(10, 0);
        label.MaxLines = 3;
        label.LineBreakMode = LineBreakMode.MiddleTruncation;
        label.CharacterSpacing = 2;
        label.LineBreaker = context => { seen = context; measured = context.Measure("abcd"); return context.Break(); };
        Measure(label, 120);
        Assert.NotNull(seen);
        Assert.Same(label, seen.Owner);
        Assert.Equal((100d, 3, LineBreakMode.MiddleTruncation, "abc"), (seen.AvailableWidth, seen.MaxLines, seen.LineBreakMode, seen.Text));
        Assert.Equal(4 * (Advance() + 2), measured, 0.5);

        var core = Core("abc").SetLineBreaker(context => { seen = context; return context.Break(); });
        core.Measure(double.PositiveInfinity, double.PositiveInfinity);
        Assert.Same(core, seen!.Owner);
        Assert.True(double.IsPositiveInfinity(seen.AvailableWidth));
        Assert.True(seen.Fits("anything"));
    }

    [Fact]
    public void CustomBreakerLinesAreShapedAndCappedByMaxLines()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label("a b c d");
        label.LineBreaker = context => context.Text.Split(' ');
        Assert.Equal(LineSpacing() * 4, Measure(label, 500).Height, 0.5);
        label.MaxLines = 2;
        Assert.Equal(LineSpacing() * 2, Measure(label, 500).Height, 0.5);
        Assert.Equal(Measure(label, 500), Core("a b c d").SetMaxLines(2).SetLineBreaker(label.LineBreaker).Measure(500, double.PositiveInfinity));
    }

    [Fact]
    public void AStockBreakerIsItsLineBreakMode()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        foreach (var mode in Enum.GetValues<LineBreakMode>())
        {
            var byMode = Label(Lorem);
            byMode.LineBreakMode = mode;
            var byBreaker = Label(Lorem);
            byBreaker.LineBreaker = SkUiTextLineBreakers.For(mode);
            Assert.Equal(Measure(byMode, 200), Measure(byBreaker, 200));
        }
    }

    [Fact]
    public void WithEllipsisUsesTheLabelsModeAndItsOwnEllipsis()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var calls = new List<IReadOnlyList<string>>();
        var label = Label(Lorem);
        label.LineBreakMode = LineBreakMode.TailTruncation;
        label.MaxLines = 2;
        label.LineBreaker = Recording(SkUiTextLineBreakers.WithEllipsis("… more"), calls);
        Measure(label, 200);
        var lines = calls[^1];
        Assert.Equal(2, lines.Count);
        Assert.EndsWith("… more", lines[1]);
        using var font = Font();
        Assert.True(font.MeasureText(lines[1]) <= 200.5);

        label.LineBreakMode = LineBreakMode.HeadTruncation;
        label.MaxLines = -1;
        Measure(label, 200);
        Assert.StartsWith("… more", Assert.Single(calls[^1]));
    }

    [Fact]
    public void FirstFitDropsDecimalsBeforeTruncating()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        const double value = 3.14159265;
        var calls = new List<IReadOnlyList<string>>();
        var label = Label(value.ToString("F6", CultureInfo.InvariantCulture));
        label.LineBreakMode = LineBreakMode.TailTruncation;
        label.LineBreaker = Recording(SkUiTextLineBreakers.FirstFit(_ =>
            Enumerable.Range(0, 6).Reverse().Select(decimals => value.ToString($"F{decimals}", CultureInfo.InvariantCulture))), calls);
        IReadOnlyList<string> At(double characters)
        {
            Measure(label, Advance() * characters);
            return calls[^1];
        }
        Assert.Equal(["3.141593"], At(20)); // fits: unchanged
        Assert.Equal(["3.14"], At(4.5));
        Assert.Equal(["3"], At(2.5));

        // Nothing fits: the shortest form with the label's ellipsis.
        var big = Label("123456.78");
        big.LineBreakMode = LineBreakMode.TailTruncation;
        big.LineBreaker = Recording(SkUiTextLineBreakers.FirstFit(_ => ["123456.8", "123457"]), calls);
        Measure(big, Advance() * 4.5);
        Assert.Equal(["1..."], calls[^1]);
    }

    [Fact]
    public void AContextAwareEllipsisCountsWhatItLeftOut()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        // "Alice, Bob +2": as many names as fit, then how many more.
        SkUiTextLineBreaker names = context =>
        {
            var all = context.Text.Split(", ");
            for (var shown = all.Length; shown > 0; shown--)
            {
                var candidate = string.Join(", ", all[..shown]) + (shown < all.Length ? $" +{all.Length - shown}" : "");
                if (context.Fits(candidate))
                    return [candidate];
            }
            return context.Break();
        };
        var calls = new List<IReadOnlyList<string>>();
        var label = Label("Alice, Bob, Carol, Dave");
        label.LineBreaker = Recording(names, calls);
        Measure(label, Advance() * 14);
        Assert.Equal(["Alice, Bob +2"], calls[^1]);
        Measure(label, 1000);
        Assert.Equal(["Alice, Bob, Carol, Dave"], calls[^1]);
    }

    [Fact]
    public void InvalidateTextLayoutRunsTheBreakerAgain()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var suffix = "!";
        var label = Label("hi");
        label.LineBreaker = context => [context.Text + suffix];
        var before = Measure(label, 500).Width;
        suffix = "!!!";
        Assert.Equal(before, Measure(label, 500).Width); // cached
        label.InvalidateTextLayout();
        Assert.Equal(before + 2 * Advance(), Measure(label, 500).Width, 0.5);

        var core = Core("hi").SetLineBreaker(context => [context.Text + suffix]);
        var width = core.Measure(500, double.PositiveInfinity).Width;
        suffix = "!";
        core.InvalidateTextLayout();
        Assert.Equal(width - 2 * Advance(), core.Measure(500, double.PositiveInfinity).Width, 0.5);
    }
}
