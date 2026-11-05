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

    // Wider than this does not fit a line of text: the source wraps there.
    private const float MaximumInlineWidth = 1_200;

    public SkUiCoreNode? CreateBlock(string latex, SkUiMarkdownMathContext context)
    {
        // The node typesets as it measures; whether it can is found out here, once, off the drawing path.
        return Typeset(latex, context.FontSize, context.TextColor, CSharpMath.Atom.LineStyle.Display, out _) is not null
            ? new SkUiMathNode(latex, context)
            : null;
    }

    public SkUiInlineObject? CreateInline(string latex, SkUiMarkdownMathContext context)
    {
        var painter = Typeset(latex, SkUiFontScaling.ScaleFontSize(context.FontSize), context.TextColor, CSharpMath.Atom.LineStyle.Text, out var bounds);
        if (painter is null || bounds.Width > MaximumInlineWidth)
            return null;
        var ascent = -bounds.Top;
        return new SkUiInlineObject(bounds.Width, bounds.Height, ascent, canvas =>
        {
            lock (Engine)
                painter.Draw(canvas, 0, ascent);
        });
    }

    /// <summary>A painter holding the typeset formula and its bounds (the baseline at 0), or <c>null</c> when the guard or the engine refuses the formula.</summary>
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
                return bounds.Width > 0 && float.IsFinite(bounds.Width) && float.IsFinite(bounds.Height) ? painter : null;
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            System.Diagnostics.Debug.WriteLine("SkiaUi Markdown math: " + error.Message);
            return null;
        }
    }
}

/// <summary>A formula on its own. Follows the system text size, as labels do.</summary>
internal sealed class SkUiMathNode : SkUiCoreNode
{
    private readonly string _latex;
    private readonly SkUiMarkdownMathContext _context;
    private SkUiMathPainter? _painter;
    private System.Drawing.RectangleF _bounds;
    private double _typesetSize = double.NaN;

    internal SkUiMathNode(string latex, SkUiMarkdownMathContext context)
    {
        _latex = latex;
        _context = context;
        SetSemanticDescription(latex);
    }

    private void Typeset()
    {
        var size = SkUiFontScaling.ScaleFontSize(_context.FontSize);
        if (_painter is not null && System.Math.Abs(size - _typesetSize) < 0.01)
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
        if (_painter is null)
            return;
        lock (SkUiMathRenderer.Engine)
            _painter.Draw(canvas, 0, -_bounds.Top);
    }
}
