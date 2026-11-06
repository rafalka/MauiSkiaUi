using MauiSkiaUi.Core;
using MauiSkiaUi.Markdown;
using MauiSkiaUi.Markdown.Math;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>The math package: formulas drawn, and hostile LaTeX refused before the engine sees it.</summary>
public class MarkdownMathTests
{
    private static SkUiMarkdownStyle Style { get; } = SkUiMarkdownStyle.Default with
    {
        FontFamily = SkUiTestHelpers.BundledFontFamily,
        CodeFontFamily = SkUiTestHelpers.BundledFontFamily,
        MaximumInPlaceParseLength = int.MaxValue
    };

    private static IEnumerable<T> Nodes<T>(ISkUiCoreNode root) where T : class
    {
        if (root is T match)
            yield return match;
        foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<ISkUiCoreNode>())
            foreach (var found in Nodes<T>(child))
                yield return found;
    }

    private static SKBitmap Draw(SkUiCoreNode node, int width, int height)
    {
        node.Measure(width, height);
        node.Arrange(new Rect(0, 0, width, height));
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        node.Paint(canvas);
        return bitmap;
    }

    private static int InkPixels(SKBitmap bitmap)
    {
        var ink = 0;
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).Red < 128)
                    ink++;
        return ink;
    }

    [Fact]
    public void ABlockFormulaDrawsCenteredAndDescribesItself()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Style with { TextColor = Colors.Black }).UseMath().SetText("$$\n\\frac{a}{b} + \\sqrt{x^2}\n$$");
        var node = Assert.Single(Nodes<SkUiMathNode>(view));
        using var bitmap = Draw(view, 300, 100);
        Assert.True(InkPixels(bitmap) > 20);
        Assert.Contains("\\frac", node.SemanticDescription);
        Assert.True(node.Frame.Width > 10);
        Assert.Empty(Nodes<SkUiCoreLabel>(view));
        var (center, half) = (node.Frame.X + node.Frame.Width / 2, 150.0);
        Assert.Equal(half, center, 0);
    }

    [Fact]
    public void AnInlineFormulaIsPartOfItsLine()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Style with { TextColor = Colors.Black }).UseMath().SetText("area is $\\pi r^2$ here");
        var label = Assert.Single(Nodes<SkUiCoreLabel>(view));
        var formula = Assert.Single(label.Spans, span => span.InlineObject is not null);
        Assert.Equal("\\pi r^2", formula.Text);
        Assert.True(formula.InlineObject!.Width > 5);
        using var bitmap = Draw(view, 300, 60);
        Assert.True(InkPixels(bitmap) > 20);
    }

    [Theory]
    [InlineData("$$\n\\unknowncommand{\n$$")]
    [InlineData("$\\frac{a$ x")]
    public void WhatTheEngineCannotReadShowsItsSource(string document)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Style).UseMath().SetText(document);
        Assert.Empty(Nodes<SkUiMathNode>(view));
        Assert.Contains(Nodes<SkUiCoreLabel>(view), label => label.Text.Contains('\\') || label.Spans.Any(span => span.Text.Contains('\\')));
        using var bitmap = Draw(view, 300, 100);
    }

    [Fact]
    public void WithoutARendererAFormulaShowsItsSource()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Style).SetText("$$\n\\pi\n$$\n\nsee $x$ there");
        Assert.Empty(Nodes<SkUiMathNode>(view));
        var labels = Nodes<SkUiCoreLabel>(view).ToArray();
        Assert.Equal("\\pi", labels[0].Text);
        Assert.Equal(SkUiTestHelpers.BundledFontFamily, labels[1].Spans.Single(span => span.Text == "x").FontFamily);
    }

    public static TheoryData<string> Hostile()
    {
        string R(string text, int count) => string.Concat(Enumerable.Repeat(text, count));
        return new TheoryData<string>
        {
            R("{", 1000),
            R("x^{", 1000),
            R("\\frac{a}{", 500),
            R("\\frac1", 2000),
            R("\\sqrt", 500),
            R("\\sqrt{", 500),
            R("\\left(", 500),
            R("\\overline", 500),
            R("\\hat", 500),
            R("\\mathbf{", 500),
            R("\\text{", 500),
            R("\\begin{matrix}", 500),
            R("\\unknown", 500),
            R("^", 1000),
            R("_^", 1000),
            R("\\sqrt[", 500),
            R("a", 40000),
            // An escaped brace is a symbol to the engine, so it must not close what the count has open.
            R("{\\}", 1365),
            R("{x\\}", 1000),
            // A comment hides its closing brace from the engine, up to a line end of any kind.
            R("{%}\n", 800) + "x" + R("}", 800),
            R("{%}\r", 800) + "x" + R("}", 800),
            R("{%}\u2028", 800) + "x" + R("}", 800),
            R("\\\\{%}\n", 500) + "x" + R("}", 500),
            // Limits that add up: scripts, braces and commands nest in one another.
            R("^^^^^^^^\\frac1", 24) + "2",
            R("^^^^^^^^{", 32) + R("^^^^^^^^\\sqrt", 48) + "x" + R("}", 32),
            R("\\sqrt{", 30) + R("\\frac1", 30) + "2" + R("}", 30),
            // The engine skips control characters, which would hide scripts from a count of runs.
            R("^\u0001", 300),
            // Short, and exponential in time: each \left typesets what it holds again.
            R("\\left(\\frac{a}{", 24) + "x" + R("}\\right)", 24),
            R("\\left(x^{", 24) + "x" + R("}\\right)", 24),
        };
    }

    [Theory]
    [MemberData(nameof(Hostile))]
    public void HostileFormulasNeverReachTheEngine(string latex)
    {
        Assert.False(SkUiMathGuard.IsSafe(latex));
        Assert.Null(SkUiMathRenderer.Typeset(latex, 16, Colors.Black, CSharpMath.Atom.LineStyle.Display, out _));
    }

    public static TheoryData<string> Ordinary() => new()
    {
        "E = mc^2",
        "50\\% + 25\\%",
        "\\frac{-b \\pm \\sqrt{b^2 - 4ac}}{2a}",
        "\\sum_{i=0}^{n} i = \\frac{n(n+1)}{2}",
        "\\int_0^\\infty e^{-x^2} dx = \\frac{\\sqrt{\\pi}}{2}",
        "\\begin{pmatrix} a & b \\\\ c & d \\end{pmatrix}",
        "x_1 + x_2 + x_3 + x_4 + x_5 + x_6 + x_7 + x_8 + x_9 + x_{10} + x_{11} + x_{12} + x_{13} + x_{14}",
        "\\alpha\\beta\\gamma\\delta\\epsilon\\zeta\\eta\\theta\\iota\\kappa\\lambda\\mu\\nu\\xi\\pi\\rho\\sigma\\tau\\upsilon\\phi\\chi\\psi\\omega",
        "\\left(\\frac{a}{b}\\right)^2 + e^{-x} + x^* + y^\\prime + \\{a, b\\}",
        string.Concat(Enumerable.Repeat("x_{i}^{2} + ", 100)) + "y",
    };

    [Theory]
    [MemberData(nameof(Ordinary))]
    public void OrdinaryFormulasPass(string latex) => Assert.True(SkUiMathGuard.IsSafe(latex));

    /// <summary>
    /// What gets past the guard must also draw on a small stack: parsing, typesetting and drawing all recurse. A stack overflow here ends the test process, which is
    /// the point of the test.
    /// </summary>
    [Fact]
    public void TheDeepestFormulasThatPassDrawOnASmallStack()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        string R(string text, int count) => string.Concat(Enumerable.Repeat(text, count));
        var depth = SkUiMathGuard.MaximumNesting;
        var lefts = SkUiMathGuard.MaximumLeftDepth;
        var pairs = depth / 2;
        string[] formulas =
        [
            R("{", depth) + "x" + R("}", depth),
            R("x^{", depth) + "x" + R("}", depth),
            R("\\frac{a}{", pairs) + "x" + R("}", pairs),
            R("\\frac1", depth) + "2",
            R("\\sqrt ", depth) + "x",
            R("\\sqrt{", pairs) + "x" + R("}", pairs),
            R("\\sqrt[", pairs) + "3" + R("]{x}", pairs),
            R("\\overline ", depth) + "x",
            R("\\hat ", depth) + "x",
            R("\\begin{matrix}", depth - 1) + "x" + R("\\end{matrix}", depth - 1),
            R("\\left(", lefts) + "x" + R("\\right)", lefts),
            R("^", depth) + "x",
            R("^\\sqrt ", pairs) + "x",
            R("\\sqrt{\\frac{a}{", depth / 4) + "x" + R("}}", depth / 4),
            R("\\left(\\frac{a}{", lefts) + "x" + R("}\\right)", lefts),
        ];
        Exception? failure = null;
        var passed = 0;
        var thread = new Thread(() =>
        {
            try
            {
                foreach (var latex in formulas)
                {
                    Assert.True(SkUiMathGuard.IsSafe(latex), latex);
                    var view = new SkUiCoreMarkdown().SetMarkdownStyle(Style).UseMath().SetText("$$\n" + latex + "\n$$");
                    Assert.Single(Nodes<SkUiMathNode>(view)); // what the engine refuses would pass without testing anything
                    using var bitmap = Draw(view, 300, 100);
                    passed++;
                }
            }
            catch (Exception error)
            {
                failure = error;
            }
        }, 512 * 1024);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
        Assert.Equal(formulas.Length, passed);
    }

    /// <summary>The engine holds its lock as it typesets and a typeset cannot be stopped, so what the guard lets through has to end soon.</summary>
    [Fact]
    public void TheSlowestFormulasThatPassTypesetQuickly()
    {
        string R(string text, int count) => string.Concat(Enumerable.Repeat(text, count));
        var lefts = SkUiMathGuard.MaximumLeftDepth;
        var rest = (SkUiMathGuard.MaximumNesting - 3 * lefts) / 2;
        string[] formulas =
        [
            R("\\left(\\frac{a}{", lefts) + "x" + R("}\\right)", lefts),
            R("\\left(x^{", lefts) + "x" + R("}\\right)", lefts),
            R("\\left(\\frac{a}{", lefts) + R("\\frac{a}{", rest) + "x" + R("}", rest) + R("}\\right)", lefts),
        ];
        foreach (var latex in formulas)
        {
            Assert.True(SkUiMathGuard.IsSafe(latex), latex);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            _ = SkUiMathRenderer.Typeset(latex, 16, Colors.Black, CSharpMath.Atom.LineStyle.Display, out _);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), latex);
        }
    }

    [Fact]
    public void AFormulaTooLargeToPlaceShowsItsSource()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        string R(string text, int count) => string.Concat(Enumerable.Repeat(text, count));
        var lefts = SkUiMathGuard.MaximumLeftDepth;
        // Inside every limit of the guard, and hundreds of lines high.
        var tall = R("\\left(\\frac{a}{", lefts) + "x" + R("}\\right)", lefts);
        var inline = new SkUiCoreMarkdown().SetMarkdownStyle(Style).UseMath().SetText("a $" + tall + "$ b");
        Assert.DoesNotContain(Nodes<SkUiCoreLabel>(inline).SelectMany(label => label.Spans), span => span.InlineObject is not null);
        var wide = R("x", SkUiMathGuard.MaximumLength);
        Assert.Null(SkUiMathRenderer.Typeset(wide, 16, Colors.Black, CSharpMath.Atom.LineStyle.Display, out _));
        var rest = (SkUiMathGuard.MaximumNesting - 3 * lefts) / 2;
        var taller = R("\\left(\\frac{a}{", lefts) + R("\\frac{a}{", rest) + "x" + R("}", rest) + R("}\\right)", lefts);
        Assert.Null(SkUiMathRenderer.Typeset(taller, 16, Colors.Black, CSharpMath.Atom.LineStyle.Display, out _));
    }

    /// <summary>The engine measures an environment's columns narrower than it draws them.</summary>
    [Theory]
    [InlineData("\\begin{aligned} a &= b \\\\ c &= d \\end{aligned}", 47)]
    [InlineData("\\begin{cases} a & b \\\\ c & d \\end{cases}", 53)]
    public void AnEnvironmentIsAsWideAsItsInk(string latex, double ink)
    {
        Assert.NotNull(SkUiMathRenderer.Typeset(latex, 16, Colors.Black, CSharpMath.Atom.LineStyle.Display, out var bounds));
        Assert.True(bounds.Width >= ink, bounds.Width.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task AFormulaOnABackgroundBuildDraws()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Style with { TextColor = Colors.Black, MaximumInPlaceParseLength = 0 }).UseMath().SetText("$$\n\\frac{a}{b}\n$$\n\nsee $x^2$ there");
        await view.WhenUpdated;
        Assert.Single(Nodes<SkUiMathNode>(view));
        Assert.Contains(Nodes<SkUiCoreLabel>(view).SelectMany(label => label.Spans), span => span.InlineObject is not null);
        using var bitmap = Draw(view, 300, 120);
        Assert.True(InkPixels(bitmap) > 20);
    }

    [Fact]
    public void TheMauiViewDrawsFormulasToo()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = new SkUiMarkdownView().SetMarkdownStyle(Style).UseMath().SetText("$$\nx^2\n$$");
        Assert.Same(SkUiMarkdownMath.Renderer, view.MathRenderer);
        Assert.Single(Nodes<SkUiMathNode>(view.Markdown));
    }

    [Fact]
    public void ARendererThatThrowsShowsTheSource()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        string? warning = null;
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Style);
        view.Warning += (_, message) => warning = message;
        view.SetMathRenderer(new Throwing()).SetText("$$\nx\n$$\n\na $y$ b");
        Assert.Contains("boom", warning);
        Assert.Contains(Nodes<SkUiCoreLabel>(view), label => label.Text == "x");
    }

    private sealed class Throwing : ISkUiMarkdownMathRenderer
    {
        public SkUiCoreNode CreateBlock(string latex, SkUiMarkdownMathContext context) => throw new InvalidOperationException("boom");

        public SkUiInlineObject CreateInline(string latex, SkUiMarkdownMathContext context) => throw new InvalidOperationException("boom");
    }
}
