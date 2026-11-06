using MauiSkiaUi.Core;
using SkiaSharp;

namespace MauiSkiaUi.Markdown.Math;

/// <summary>Formulas for the Markdown views, drawn with CSharpMath.</summary>
public static class SkUiMarkdownMath
{
    /// <summary>The renderer. A formula past the limits, or one the engine cannot read, shows its source in the code style.</summary>
    public static ISkUiMarkdownMathRenderer Renderer { get; } = new SkUiMathRenderer();

    /// <summary>Draws formulas in this view.</summary>
    public static SkUiCoreMarkdown UseMath(this SkUiCoreMarkdown view) => view.SetMathRenderer(Renderer);

    /// <summary>Draws formulas in this view.</summary>
    public static SkUiMarkdownView UseMath(this SkUiMarkdownView view) => view.SetMathRenderer(Renderer);

    /// <summary>Draws formulas in every Markdown view of the app.</summary>
    public static MauiAppBuilder UseSkiaUiMarkdownMath(this MauiAppBuilder builder)
    {
        SkUiCoreMarkdown.DefaultMathRenderer = Renderer;
        return builder;
    }
}

internal sealed class SkUiMathRenderer : ISkUiMarkdownMathRenderer
{
    /// <summary>The engine shares font data and caches between painters and is not documented as thread safe; documents build on background threads and draw on the UI thread.</summary>
    internal static readonly Lock Engine = new();

    // Wider or taller than this does not fit a line of text: the source wraps there. A formula within the guard's limits can still be tens of thousands of DIPs tall.
    private const float MaximumInlineWidth = 1_200;
    private const float MaximumInlineHeightInLines = 20;

    // A block scrolls sideways, not down, and one hostile word should not make a document tens of thousands of DIPs long.
    private const float MaximumBlockWidth = 20_000;
    private const float MaximumBlockHeight = 3_000;

    // The engine measures the columns of an environment narrower than it draws them (by up to a third of the font size at 16 DIPs).
    private const float EnvironmentWidthPadding = 0.5f;

    public SkUiCoreNode? CreateBlock(string latex, SkUiMarkdownMathContext context)
    {
        // The whole typesetting happens here, on the build's thread; the node only does it again if the text size changed since.
        var painter = Typeset(latex, context.ScaledFontSize, context.TextColor, CSharpMath.Atom.LineStyle.Display, out var bounds);
        return painter is not null ? new SkUiMathNode(latex, context, painter, bounds) : null;
    }

    public SkUiInlineObject? CreateInline(string latex, SkUiMarkdownMathContext context)
    {
        var painter = Typeset(latex, context.ScaledFontSize, context.TextColor, CSharpMath.Atom.LineStyle.Text, out var bounds);
        if (painter is null)
            return null;
        var ascent = -bounds.Top;
        return new SkUiInlineObject(bounds.Width, bounds.Height, ascent, canvas => Draw(painter, canvas, ascent));
    }

    /// <summary>Draws a typeset formula. What the engine throws here, from a formula out of a document, stays here: the frame goes on without it.</summary>
    internal static void Draw(SkUiMathPainter painter, SKCanvas canvas, float baseline)
    {
        var saved = canvas.SaveCount;
        try
        {
            lock (Engine)
                painter.Draw(canvas, 0, baseline);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            canvas.RestoreToCount(saved);
            System.Diagnostics.Debug.WriteLine("SkiaUi Markdown math: " + error.Message);
        }
    }

    /// <summary>A painter holding the typeset formula and its bounds (the baseline at 0), or <c>null</c> when the guard or the engine refuses the formula, or it comes out too large.</summary>
    internal static SkUiMathPainter? Typeset(string latex, double fontSize, Color color, CSharpMath.Atom.LineStyle style, out System.Drawing.RectangleF bounds)
    {
        bounds = default;
        if (!SkUiMathGuard.IsSafe(latex))
            return null;
        try
        {
            lock (Engine)
            {
                var painter = new SkUiMathPainter
                {
                    FontSize = (float)fontSize,
                    LineStyle = style,
                    DisplayErrorInline = false,
                    TextColor = new SKColor((byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255), (byte)(color.Alpha * 255)),
                    LaTeX = latex
                };
                if (painter.ErrorMessage is not null)
                    return null;
                bounds = painter.Measure();
                if (latex.Contains("\\begin", StringComparison.Ordinal))
                    bounds.Width += (float)fontSize * EnvironmentWidthPadding;
                var (maximumWidth, maximumHeight) = style == CSharpMath.Atom.LineStyle.Display
                    ? (MaximumBlockWidth, MaximumBlockHeight)
                    : (MaximumInlineWidth, (float)fontSize * MaximumInlineHeightInLines);
                return bounds.Width > 0 && bounds.Width <= maximumWidth && bounds.Height <= maximumHeight ? painter : null;
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            System.Diagnostics.Debug.WriteLine("SkiaUi Markdown math: " + error.Message);
            return null;
        }
    }
}

/// <summary>A formula on its own, typeset as the document was built. Follows the system text size, as labels do.</summary>
internal sealed class SkUiMathNode : SkUiCoreNode
{
    private const double SizeTolerance = 0.01;

    private readonly string _latex;
    private readonly SkUiMarkdownMathContext _context;
    private SkUiMathPainter? _painter;
    private System.Drawing.RectangleF _bounds;
    private double _typesetSize;

    internal SkUiMathNode(string latex, SkUiMarkdownMathContext context, SkUiMathPainter painter, System.Drawing.RectangleF bounds)
    {
        _latex = latex;
        _context = context;
        _painter = painter;
        _bounds = bounds;
        _typesetSize = context.ScaledFontSize;
        SetSemanticDescription(latex);
    }

    /// <summary>Typesets again only when the size really changed: on the UI thread, until the rebuild a text size change starts. A size that failed is not tried twice.</summary>
    private void Typeset()
    {
        var size = SkUiFontScaling.ScaleFontSize(_context.FontSize);
        if (System.Math.Abs(size - _typesetSize) < SizeTolerance)
            return;
        _typesetSize = size;
        _painter = SkUiMathRenderer.Typeset(_latex, size, _context.TextColor, CSharpMath.Atom.LineStyle.Display, out _bounds);
    }

    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        Typeset();
        return _painter is null ? Size.Zero : new Size(_bounds.Width, _bounds.Height);
    }

    protected override void OnPaintContent(SKCanvas canvas)
    {
        Typeset();
        if (_painter is not null)
            SkUiMathRenderer.Draw(_painter, canvas, -_bounds.Top);
    }
}
