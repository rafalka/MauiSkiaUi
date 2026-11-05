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
        "\\frac{-b \\pm \\sqrt{b^2 - 4ac}}{2a}",
        "\\sum_{i=0}^{n} i = \\frac{n(n+1)}{2}",
        "\\int_0^\\infty e^{-x^2} dx = \\frac{\\sqrt{\\pi}}{2}",
        "\\begin{pmatrix} a & b \\\\ c & d \\end{pmatrix}",
        "x_1 + x_2 + x_3 + x_4 + x_5 + x_6 + x_7 + x_8 + x_9 + x_{10} + x_{11} + x_{12} + x_{13} + x_{14}",
        "\\alpha\\beta\\gamma\\delta\\epsilon\\zeta\\eta\\theta\\iota\\kappa\\lambda\\mu\\nu\\xi\\pi\\rho\\sigma\\tau\\upsilon\\phi\\chi\\psi\\omega",
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
        var depth = SkUiMathGuard.MaximumNestingCommands;
        string[] formulas =
        [
            R("{", SkUiMathGuard.MaximumBraceDepth) + "x" + R("}", SkUiMathGuard.MaximumBraceDepth),
            R("x^{", SkUiMathGuard.MaximumBraceDepth) + "x" + R("}", SkUiMathGuard.MaximumBraceDepth),
            R("\\frac{a}{", SkUiMathGuard.MaximumBraceDepth) + "x" + R("}", SkUiMathGuard.MaximumBraceDepth),
            R("\\frac1", depth) + "2",
            R("\\sqrt", depth) + "x",
            R("\\overline", depth) + "x",
            R("\\hat", depth) + "x",
            R("\\left(", depth) + "x" + R("\\right)", depth),
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
