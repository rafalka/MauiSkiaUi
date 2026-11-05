using CSharpMath.Rendering.FrontEnd;
using SkiaSharp;
using DrawingColor = System.Drawing.Color;
using ICanvas = CSharpMath.Rendering.FrontEnd.ICanvas;
using Path = CSharpMath.Rendering.FrontEnd.Path;

namespace MauiSkiaUi.Markdown.Math;

/// <summary>CSharpMath's painter on SkiaSharp. Everything it creates is disposed when drawn, inside the recording of the node that draws it, so nothing native reaches the render thread.</summary>
internal sealed class SkUiMathPainter : MathPainter<SKCanvas, SKColor>
{
    public override DrawingColor WrapColor(SKColor color) => DrawingColor.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);

    public override SKColor UnwrapColor(DrawingColor color) => new(color.R, color.G, color.B, color.A);

    public override ICanvas WrapCanvas(SKCanvas canvas) => new SkUiMathCanvas(canvas);
}

/// <summary>CSharpMath's canvas on an <see cref="SKCanvas"/>.</summary>
internal sealed class SkUiMathCanvas(SKCanvas canvas) : ICanvas
{
    // The UI thread's, for what it records: one paint, not one for each of a formula's glyphs.
    [ThreadStatic] private static SKPaint? _paint;

    public float Width => canvas.LocalClipBounds.Width;

    public float Height => canvas.LocalClipBounds.Height;

    public DrawingColor DefaultColor { get; set; }

    public DrawingColor? CurrentColor { get; set; }

    public PaintStyle CurrentStyle { get; set; }

    internal SKPaint Paint(DrawingColor color, SKPaintStyle style, float strokeWidth = 0)
    {
        var paint = _paint ??= new SKPaint { IsAntialias = true };
        paint.Color = new SKColor(color.R, color.G, color.B, color.A);
        paint.Style = style;
        paint.StrokeWidth = strokeWidth;
        return paint;
    }

    private DrawingColor Color => CurrentColor ?? DefaultColor;

    internal SKCanvas Target => canvas;

    internal SKPaintStyle PathStyle => CurrentStyle == PaintStyle.Fill ? SKPaintStyle.Fill : SKPaintStyle.Stroke;

    public Path StartNewPath() => new SkUiMathPath(this);

    public void DrawLine(float x1, float y1, float x2, float y2, float lineThickness)
    {
        if (lineThickness <= 0)
            return;
        canvas.DrawLine(x1, y1, x2, y2, Paint(Color, SKPaintStyle.Stroke, lineThickness));
    }

    public void StrokeRect(float left, float top, float width, float height) =>
        canvas.DrawRect(SKRect.Create(left, top, width, height), Paint(Color, SKPaintStyle.Stroke));

    public void FillRect(float left, float top, float width, float height)
    {
        if (Color.A > 0)
            canvas.DrawRect(SKRect.Create(left, top, width, height), Paint(Color, SKPaintStyle.Fill));
    }

    public void Save() => canvas.Save();

    public void Translate(float dx, float dy) => canvas.Translate(dx, dy);

    public void Scale(float sx, float sy) => canvas.Scale(sx, sy);

    public void Restore() => canvas.Restore();
}

/// <summary>One glyph or shape: drawn and disposed when CSharpMath disposes it.</summary>
internal sealed class SkUiMathPath(SkUiMathCanvas canvas) : Path
{
    private readonly SKPathBuilder _path = new() { FillType = SKPathFillType.EvenOdd };
    private bool _disposed;

    public override DrawingColor? Foreground { get; set; }

    public override void MoveTo(float x0, float y0) => _path.MoveTo(x0, y0);

    public override void LineTo(float x1, float y1) => _path.LineTo(x1, y1);

    public override void Curve3(float x1, float y1, float x2, float y2) => _path.QuadTo(x1, y1, x2, y2);

    public override void Curve4(float x1, float y1, float x2, float y2, float x3, float y3) => _path.CubicTo(x1, y1, x2, y2, x3, y3);

    public override void CloseContour() => _path.Close();

    public override void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        using var path = _path.Detach();
        canvas.Target.DrawPath(path, canvas.Paint(Foreground ?? canvas.CurrentColor ?? canvas.DefaultColor, canvas.PathStyle));
        _path.Dispose();
    }
}
