namespace MauiSkiaUi.Markdown;

/// <summary>An image in a document was tapped.</summary>
/// <param name="source">The image's URL as written in the document.</param>
/// <param name="altText">The image's alt text.</param>
public sealed class SkUiMarkdownImageTappedEventArgs(string source, string altText) : EventArgs
{
    /// <summary>The image's URL as written in the document: check it before using it, a document can hold anything.</summary>
    public string Source { get; } = source;

    /// <summary>The image's alt text.</summary>
    public string AltText { get; } = altText;
}

/// <summary>Turns code into colored spans: the <c>CodeHighlighter</c> of a Markdown view. Returning <c>null</c> or throwing shows the code plain. Runs on a background thread.</summary>
/// <param name="code">The code.</param>
/// <param name="language">The language written after the fence, or <c>null</c>.</param>
public delegate IEnumerable<MauiSkiaUi.Core.SkUiCoreSpan>? SkUiMarkdownCodeHighlighter(string code, string? language);

/// <summary>What a math renderer draws with.</summary>
/// <param name="FontSize">Font size of the text around the formula, in DIPs (before the system text size).</param>
/// <param name="TextColor">Color of the text around the formula.</param>
/// <param name="ScaledFontSize">The size that text draws at, with the system text size: worked out on the UI thread, because on Apple platforms a build's thread cannot. A change starts a new build.</param>
public readonly record struct SkUiMarkdownMathContext(double FontSize, Color TextColor, double ScaledFontSize);

/// <summary>
/// Draws formulas for a Markdown view (<c>MathRenderer</c>): the math package's <c>UseMath()</c> sets one. Without one, or when it
/// returns <c>null</c>, a formula shows its source in the code style. The formula comes from the document, so it can be hostile. Runs on a background thread, as the document
/// is built, and what it returns goes on screen on the UI thread.
/// </summary>
public interface ISkUiMarkdownMathRenderer
{
    /// <summary>A node for a formula on its own (<c>$$ … $$</c>), or <c>null</c> to show its source.</summary>
    /// <param name="latex">The formula's source.</param>
    /// <param name="context">Size and color of the text around it.</param>
    Core.SkUiCoreNode? CreateBlock(string latex, SkUiMarkdownMathContext context);

    /// <summary>An object for a formula in a line of text (<c>$ … $</c>), or <c>null</c> to show its source.</summary>
    /// <param name="latex">The formula's source.</param>
    /// <param name="context">Size and color of the text around it.</param>
    SkUiInlineObject? CreateInline(string latex, SkUiMarkdownMathContext context);
}
