using SkiaSharp;

namespace MauiSkiaUi.Rendering;

/// <summary>
/// GPU pipeline warm-up: draws the kinds of operations SkiaUi's controls use (rectangles, rounded rectangles, gradients,
/// paths, dashes, arcs, text, images, clips, layers, blurs; plain, rotated and scaled) into a small offscreen GPU surface
/// and flushes it, so the GPU compiles their shaders while the surface is idle instead of in the first frame that needs
/// them. A first use of a pipeline otherwise stalls that frame for tens of milliseconds (measured on Metal) until the OS
/// shader cache holds it. Runs one step at a time on the render thread, between frames.
/// </summary>
internal sealed class SkUiGpuWarmUp
{
    private const int Size = 128;

    /// <summary>The offscreen surface: room for the large scale, where GPUs switch some shapes (blurs, paths) to other pipelines.</summary>
    private const int SurfaceSize = 3 * Size;

    /// <summary>Turn off to skip the warm-up (diagnostics: measuring cold pipelines).</summary>
    internal static bool IsEnabled { get; set; } = true;

    private static readonly Action<SKCanvas, Resources>[] Steps =
    [
        DrawShapes, DrawGradients, DrawPaths, DrawStrokes, DrawText, DrawImages, DrawClips, DrawLayers, DrawShadows
    ];

    /// <summary>The matrices each step is drawn under: GPUs pick different pipelines for them.</summary>
    private static readonly SKMatrix[] Transforms =
    [
        SKMatrix.Identity,
        SKMatrix.CreateRotationDegrees(15, Size / 2f, Size / 2f),
        SKMatrix.CreateScale(1.5f, 1.5f),
        SKMatrix.CreateScale(3, 3)
    ];

    private int _step;

    /// <summary>Number of steps (for tests).</summary>
    internal static int StepCount => Steps.Length;

    /// <summary>Whether every step ran.</summary>
    public bool IsDone => _step >= Steps.Length || !IsEnabled;

    /// <summary>
    /// Render thread: runs the next step into an offscreen surface of <paramref name="colorType"/> on
    /// <paramref name="context"/> and flushes it. Returns <c>true</c> while steps remain.
    /// </summary>
    public bool RunNext(GRRecordingContext context, SKColorType colorType)
    {
        if (IsDone)
            return false;
        using var surface = SKSurface.Create(context, false, new SKImageInfo(SurfaceSize, SurfaceSize, colorType, SKAlphaType.Premul));
        if (surface is null)
        {
            _step = Steps.Length; // this context cannot make offscreen surfaces: give up quietly
            return false;
        }
        Draw(surface.Canvas, _step++);
        surface.Flush();
        return !IsDone;
    }

    /// <summary>Draws step <paramref name="step"/> onto any canvas (tests run every step on a raster canvas).</summary>
    internal static void Draw(SKCanvas canvas, int step)
    {
        using var resources = new Resources();
        canvas.Clear(SKColors.Transparent);
        foreach (var transform in Transforms)
        {
            var save = canvas.Save();
            canvas.SetMatrix(in transform);
            Steps[step](canvas, resources);
            canvas.RestoreToCount(save);
        }
    }

    private static void DrawShapes(SKCanvas canvas, Resources r)
    {
        var paint = r.Paint(SKColors.Teal);
        canvas.DrawRect(4, 4, 40, 24, paint);
        paint.IsAntialias = false;
        canvas.DrawRect(50, 4, 40, 24, paint);
        paint.IsAntialias = true;
        canvas.DrawRoundRect(new SKRect(4, 32, 60, 60), 8, 8, paint);
        canvas.DrawCircle(90, 46, 14, paint);
        canvas.DrawOval(new SKRect(4, 66, 60, 90), paint);
        using var rounded = new SKRoundRect();
        rounded.SetRectRadii(new SKRect(64, 66, 120, 100), [new(2, 2), new(10, 10), new(4, 4), new(12, 12)]);
        canvas.DrawRoundRect(rounded, paint);
        var stroke = r.Stroke(SKColors.Black, 2);
        canvas.DrawRoundRect(new SKRect(4, 100, 60, 124), 6, 6, stroke);
        canvas.DrawRect(64, 104, 56, 18, stroke);
        canvas.DrawCircle(100, 20, 10, stroke);
    }

    private static void DrawGradients(SKCanvas canvas, Resources r)
    {
        using var linear = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(Size, 0),
            [SKColors.Red, SKColors.Blue], [0, 1], SKShaderTileMode.Clamp);
        using var radial = SKShader.CreateRadialGradient(new SKPoint(Size / 2f, Size / 2f), Size / 2f,
            [SKColors.Yellow, SKColors.Green, SKColors.Transparent], [0, 0.5f, 1], SKShaderTileMode.Clamp);
        var paint = r.Paint(SKColors.White);
        paint.Shader = linear;
        canvas.DrawRect(4, 4, 120, 30, paint);
        canvas.DrawRoundRect(new SKRect(4, 40, 120, 70), 10, 10, paint);
        paint.Shader = radial;
        canvas.DrawRect(4, 76, 56, 48, paint);
        canvas.DrawPath(r.Ticket, paint);
        paint.Color = SKColors.White.WithAlpha(128);
        canvas.DrawRoundRect(new SKRect(64, 76, 124, 124), 12, 12, paint);
        paint.Shader = null;
    }

    private static void DrawPaths(SKCanvas canvas, Resources r)
    {
        var fill = r.Paint(SKColors.Purple);
        canvas.DrawPath(r.Ticket, fill);
        canvas.DrawPath(r.Star, fill);
        var stroke = r.Stroke(SKColors.Black, 2);
        canvas.DrawPath(r.Ticket, stroke);
        stroke.StrokeCap = SKStrokeCap.Round;
        stroke.StrokeJoin = SKStrokeJoin.Round;
        canvas.DrawPath(r.Star, stroke);
        using var dash = SKPathEffect.CreateDash([8, 4], 0);
        stroke.PathEffect = dash;
        canvas.DrawPath(r.Ticket, stroke);
        canvas.DrawRoundRect(new SKRect(10, 10, 110, 60), 10, 10, stroke);
        stroke.PathEffect = null;
        stroke.StrokeWidth = 4;
        canvas.DrawArc(new SKRect(70, 70, 120, 120), 30, 270, false, stroke); // activity indicator
        canvas.DrawLine(4, 120, 60, 100, stroke);
    }

    /// <summary>Strokes with gradients, dashes, caps and joins (shape and border outlines).</summary>
    private static void DrawStrokes(SKCanvas canvas, Resources r)
    {
        using var gradient = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(Size, 0),
            [SKColors.Teal, SKColors.SlateBlue], [0, 1], SKShaderTileMode.Clamp);
        using var dash = SKPathEffect.CreateDash([8, 4], 2);
        using var dots = SKPathEffect.CreateDash([0, 6], 0);
        foreach (var (cap, join) in new[] { (SKStrokeCap.Butt, SKStrokeJoin.Miter), (SKStrokeCap.Round, SKStrokeJoin.Round), (SKStrokeCap.Square, SKStrokeJoin.Bevel) })
        {
            var stroke = r.Stroke(SKColors.White, 2);
            stroke.StrokeCap = cap;
            stroke.StrokeJoin = join;
            stroke.Shader = gradient;
            canvas.DrawPath(r.Ticket, stroke);
            canvas.DrawRoundRect(new SKRect(8, 8, 120, 60), 12, 12, stroke);
            stroke.PathEffect = dash;
            canvas.DrawPath(r.Ticket, stroke);
            canvas.DrawRoundRect(new SKRect(8, 64, 120, 120), 12, 12, stroke);
            stroke.Shader = null;
            canvas.DrawPath(r.Star, stroke);
            stroke.PathEffect = dots;
            canvas.DrawOval(new SKRect(20, 20, 108, 108), stroke);
            stroke.StrokeWidth = 1;
            canvas.DrawRect(4, 4, 120, 120, stroke);
        }
    }

    /// <summary>Shadows: blurred outlines of several sizes and blurs, rasters of blurred content, under opacity layers.</summary>
    private static void DrawShadows(SKCanvas canvas, Resources r)
    {
        foreach (var sigma in (ReadOnlySpan<float>)[2.2f, 4, 7.4f, 12])
        {
            using var blur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, sigma);
            var shadow = r.Paint(SKColors.Black.WithAlpha(46));
            shadow.MaskFilter = blur;
            canvas.DrawRoundRect(new SKRect(4, 4, 124, 40), 12, 12, shadow);
            canvas.DrawRect(4, 48, 120, 30, shadow);
            canvas.DrawOval(new SKRect(4, 84, 40, 120), shadow);
            canvas.DrawPath(r.Ticket, shadow);
            using var gradient = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(Size, 0),
                [SKColors.Indigo, SKColors.Teal], [0, 1], SKShaderTileMode.Clamp);
            shadow.Shader = gradient;
            canvas.DrawRoundRect(new SKRect(44, 84, 124, 124), 10, 10, shadow);
            shadow.Shader = null;
        }
        // A content shadow drawn live (blur layer, then SrcIn fill), and its cached raster drawn as an image, under opacity.
        var layer = r.Paint(SKColors.White.WithAlpha(230));
        canvas.SaveLayer(new SKRect(0, 0, Size, Size), layer);
        canvas.SaveLayer(new SKRect(0, 0, Size, Size), null);
        using var layerBlur = SKImageFilter.CreateBlur(3, 3);
        using var blurPaint = new SKPaint { ImageFilter = layerBlur };
        canvas.SaveLayer(new SKRect(0, 0, Size, Size), blurPaint);
        canvas.DrawText("Shadow", 8, 40, SKTextAlign.Left, r.Font, r.Paint(SKColors.Black));
        canvas.Restore();
        var srcIn = r.Paint(SKColors.Black.WithAlpha(90));
        srcIn.BlendMode = SKBlendMode.SrcIn;
        canvas.DrawRect(0, 0, Size, Size, srcIn);
        canvas.Restore();
        canvas.DrawImage(r.Image, new SKRect(10.5f, 60.25f, 118.5f, 120.75f), new SKSamplingOptions(SKFilterMode.Linear));
        canvas.Restore();
    }

    private static void DrawText(SKCanvas canvas, Resources r)
    {
        var paint = r.Paint(SKColors.Black);
        foreach (var size in (ReadOnlySpan<float>)[11, 16, 24])
        {
            r.Font.Size = size;
            canvas.DrawText("SkiaUi 0123 Ag", 4, 4 + size * 2, SKTextAlign.Left, r.Font, paint);
        }
        paint.Color = SKColors.Red.WithAlpha(160);
        r.Font.Size = 40;
        canvas.DrawText("Wg", 4, 120, SKTextAlign.Left, r.Font, paint);
    }

    private static void DrawImages(SKCanvas canvas, Resources r)
    {
        var sampling = new SKSamplingOptions(SKFilterMode.Linear);
        canvas.DrawImage(r.Image, new SKRect(4, 4, 60, 60), sampling);
        canvas.DrawImage(r.Image, new SKRect(64, 4, 124, 40), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        canvas.DrawImage(r.Image, 64, 64, sampling, null);
        var paint = r.Paint(SKColors.White);
        paint.Color = SKColors.White.WithAlpha(128);
        canvas.DrawImage(r.Image, new SKRect(4, 64, 60, 124), sampling, paint);
        canvas.DrawPicture(r.Picture);
    }

    private static void DrawClips(SKCanvas canvas, Resources r)
    {
        var paint = r.Paint(SKColors.Orange);
        var save = canvas.Save();
        canvas.ClipRect(new SKRect(8, 8, 100, 100));
        canvas.DrawRect(0, 0, Size, Size, paint);
        canvas.ClipRoundRect(new SKRoundRect(new SKRect(10, 10, 90, 90), 12), antialias: true);
        canvas.DrawImage(r.Image, new SKRect(0, 0, Size, Size), new SKSamplingOptions(SKFilterMode.Linear));
        canvas.RestoreToCount(save);
        save = canvas.Save();
        canvas.ClipPath(r.Ticket, antialias: true);
        paint.Color = SKColors.Navy;
        canvas.DrawRect(0, 0, Size, Size, paint);
        canvas.DrawText("Clip", 10, 60, SKTextAlign.Left, r.Font, r.Paint(SKColors.White));
        canvas.RestoreToCount(save);
        save = canvas.Save();
        canvas.ClipPath(r.Circle, antialias: true);
        canvas.DrawPicture(r.Picture);
        canvas.RestoreToCount(save);
    }

    private static void DrawLayers(SKCanvas canvas, Resources r)
    {
        var paint = r.Paint(SKColors.White.WithAlpha(180));
        canvas.SaveLayer(new SKRect(0, 0, Size, Size), paint);
        canvas.DrawPicture(r.Picture);
        canvas.Restore();
        using var blur = SKImageFilter.CreateBlur(4, 4);
        using var layer = new SKPaint { ImageFilter = blur };
        canvas.SaveLayer(new SKRect(0, 0, Size, Size), layer);
        canvas.DrawPath(r.Star, r.Paint(SKColors.Black));
        canvas.Restore();
        var srcIn = r.Paint(SKColors.Black.WithAlpha(90));
        srcIn.BlendMode = SKBlendMode.SrcIn;
        canvas.DrawRect(0, 0, Size, Size, srcIn);
        using var maskBlur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6);
        var shadow = r.Paint(SKColors.Black.WithAlpha(60));
        shadow.MaskFilter = maskBlur;
        canvas.DrawRoundRect(new SKRect(10, 10, 80, 60), 10, 10, shadow);
        canvas.DrawRect(70, 70, 40, 40, shadow);
        canvas.DrawPath(r.Ticket, shadow);
        shadow.MaskFilter = null;
    }

    /// <summary>Shared geometry, image, picture and paints of one step.</summary>
    private sealed class Resources : IDisposable
    {
        private readonly List<SKPaint> _paints = [];

        public Resources()
        {
            using (var builder = new SKPathBuilder())
            {
                builder.MoveTo(4, 30);
                builder.ArcTo(new SKRect(-4, 22, 12, 38), -90, 180, false);
                builder.LineTo(124, 38);
                builder.LineTo(124, 90);
                builder.LineTo(4, 90);
                builder.Close();
                Ticket = builder.Detach();
            }
            using (var builder = new SKPathBuilder())
            {
                builder.MoveTo(64, 4);
                builder.LineTo(78, 46);
                builder.LineTo(124, 46);
                builder.LineTo(86, 72);
                builder.LineTo(100, 120);
                builder.LineTo(64, 90);
                builder.LineTo(28, 120);
                builder.LineTo(42, 72);
                builder.LineTo(4, 46);
                builder.LineTo(50, 46);
                builder.Close();
                Star = builder.Detach();
            }
            using (var builder = new SKPathBuilder())
            {
                builder.AddOval(new SKRect(20, 20, 108, 108));
                Circle = builder.Detach();
            }
            using (var bitmap = new SKBitmap(32, 32))
            {
                bitmap.Erase(SKColors.CornflowerBlue);
                for (var x = 0; x < 32; x += 2)
                    bitmap.SetPixel(x, x, SKColors.White);
                Image = SKImage.FromBitmap(bitmap);
            }
            using (var recorder = new SKPictureRecorder())
            {
                var canvas = recorder.BeginRecording(new SKRect(0, 0, Size, Size));
                using var fill = new SKPaint { IsAntialias = true, Color = SKColors.SeaGreen };
                canvas.DrawRoundRect(new SKRect(16, 16, 112, 112), 16, 16, fill);
                fill.Color = SKColors.White;
                canvas.DrawText("Pic", 30, 70, SKTextAlign.Left, Font, fill);
                Picture = recorder.EndRecording();
            }
        }

        public SKPath Ticket { get; }
        public SKPath Star { get; }
        public SKPath Circle { get; }
        public SKImage Image { get; }
        public SKPicture Picture { get; }
        public SKFont Font { get; } = new() { Size = 16, Subpixel = true, Edging = SKFontEdging.SubpixelAntialias };

        public SKPaint Paint(SKColor color)
        {
            var paint = new SKPaint { Color = color, IsAntialias = true };
            _paints.Add(paint);
            return paint;
        }

        public SKPaint Stroke(SKColor color, float width)
        {
            var paint = Paint(color);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = width;
            return paint;
        }

        public void Dispose()
        {
            foreach (var paint in _paints)
                paint.Dispose();
            Ticket.Dispose();
            Star.Dispose();
            Circle.Dispose();
            Image.Dispose();
            Picture.Dispose();
            Font.Dispose();
        }
    }
}
