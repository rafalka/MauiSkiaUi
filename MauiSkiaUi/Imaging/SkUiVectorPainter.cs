using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>Draws a <see cref="SkUiVectorDocument"/> into a bitmap of a given size, with a given value of <c>currentColor</c>.</summary>
internal sealed class SkUiVectorPainter
{
    /// <summary>
    /// How many layers (a group's opacity, a mask) may be open at once: each can take as much memory as the whole bitmap,
    /// and real icons use two or three.
    /// </summary>
    internal const int MaxLayers = 12;

    /// <summary>
    /// How much work one drawing may do, in the costs below: masks referring to masks and a file's largest values can otherwise
    /// hold a thread for minutes. Real icons stay far below it, also with a few layers at the largest bitmap.
    /// </summary>
    internal const long MaxWork = 4_000_000;

    private const int ElementCost = 20;
    private const int StrokeCostPerCharacter = 15; // on top of reading the path: its joins and caps cost far more than a fill
    private const int LayerPixelsPerCost = 256; // measured: a layer of the whole bitmap costs far less time per pixel than a stroke per character
    private const int DashCost = 20;
    private const int ClipOperationCostPerPoint = 8;
    private const double MaxDashes = 1_000_000; // Skia draws no more than about this many of one stroke

    /// <summary>
    /// How deep drawing may recurse, mask content included: a stack overflow cannot be caught, and masks can chain nesting
    /// on top of nesting. Above <see cref="SkUiVectorDocument.MaxDepth"/>, with room on the small stacks of worker threads.
    /// </summary>
    internal const int MaxDrawDepth = 128;

    private readonly SkUiVectorDocument _document;
    private readonly SKCanvas _canvas;
    private readonly SKColor _currentColor;
    private readonly long _layerCost;
    private readonly CancellationToken _token;
    private readonly HashSet<SkUiVectorNode> _activeMasks = [];
    private int _depth;
    private int _layers;
    private long _work;

    private SkUiVectorPainter(SkUiVectorDocument document, SKCanvas canvas, SKSizeI pixels, SKColor currentColor, CancellationToken token)
    {
        _document = document;
        _canvas = canvas;
        _currentColor = currentColor;
        _layerCost = (long)pixels.Width * pixels.Height / LayerPixelsPerCost;
        _token = token;
    }

    /// <summary>A bitmap of <paramref name="pixels"/> showing <paramref name="document"/>, placed by its <c>preserveAspectRatio</c>.</summary>
    public static SKImage Render(SkUiVectorDocument document, SKSizeI pixels, SKColor currentColor, CancellationToken token = default)
    {
        var info = new SKImageInfo(pixels.Width, pixels.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("Could not create a vector icon surface.");
        Draw(document, surface.Canvas, pixels, currentColor, token);
        surface.Flush();
        return surface.Snapshot();
    }

    /// <summary>Draws <paramref name="document"/> filling <paramref name="pixels"/> on <paramref name="canvas"/>.</summary>
    internal static void Draw(SkUiVectorDocument document, SKCanvas canvas, SKSizeI pixels, SKColor currentColor, CancellationToken token = default)
    {
        var box = document.ViewBox;
        var (scaleX, scaleY, x, y) = ViewBoxMapping(document.Root["preserveAspectRatio"], box, pixels);
        canvas.Save();
        canvas.Translate(x, y);
        canvas.Scale(scaleX, scaleY);
        canvas.Translate(-box.Left, -box.Top);
        new SkUiVectorPainter(document, canvas, pixels, currentColor, token).Draw(document.Root, SkUiVectorState.Initial(currentColor));
        canvas.Restore();
    }

    private static (float ScaleX, float ScaleY, float X, float Y) ViewBoxMapping(string? preserve, SKRect box, SKSizeI pixels)
    {
        var scaleX = pixels.Width / box.Width;
        var scaleY = pixels.Height / box.Height;
        var parts = (preserve ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts is ["none", ..])
            return (scaleX, scaleY, 0, 0);
        var scale = parts is [_, "slice", ..] ? Math.Max(scaleX, scaleY) : Math.Min(scaleX, scaleY);
        var align = parts.Length > 0 ? parts[0] : "xMidYMid";
        var x = Align(align.Contains("xMin") ? 0 : align.Contains("xMax") ? 1 : 0.5f, pixels.Width, box.Width * scale);
        var y = Align(align.Contains("YMin") ? 0 : align.Contains("YMax") ? 1 : 0.5f, pixels.Height, box.Height * scale);
        return (scale, scale, x, y);
    }

    private static float Align(float at, float space, float size) => (space - size) * at;

    private void Draw(SkUiVectorNode node, SkUiVectorState parent)
    {
        if (node.Name is SkUiVectorDocument.DefsElement or SkUiVectorDocument.ClipPathElement or SkUiVectorDocument.MaskElement
            || !IsDrawnElement(node))
            return;
        Charge(ElementCost + node.Size);
        if (IsHidden(node))
            return;

        if (++_depth > MaxDrawDepth)
            throw new InvalidDataException($"SVG: drawing nests deeper than {MaxDrawDepth} levels.");
        var state = parent.Inherit(node);
        var restore = _canvas.Save();
        var layers = _layers;
        if (node["transform"] is { } transform)
        {
            var matrix = SkUiVectorValues.Transform(transform);
            _canvas.Concat(in matrix);
        }
        if (node[SkUiVectorDocument.ClipPathProperty] is { } clipPath
            && SkUiVectorDocument.ReferenceId(clipPath, SkUiVectorDocument.ClipPathProperty) is { } clipId
            && _document.Reference(clipId) is { Name: SkUiVectorDocument.ClipPathElement } clip)
        {
            using var path = ClipGeometry(clip);
            _canvas.ClipPath(path, SKClipOperation.Intersect, antialias: true);
        }
        if (node["opacity"] is { } opacityText && SkUiVectorValues.Opacity(opacityText, "opacity") is < 1 and var opacity)
        {
            using var paint = new SKPaint { Color = SKColors.Black.WithAlpha((byte)Math.Round(opacity * 255)) };
            OpenLayer(paint);
        }

        var mask = node[SkUiVectorDocument.MaskProperty] is { } maskText
            && SkUiVectorDocument.ReferenceId(maskText, SkUiVectorDocument.MaskProperty) is { } maskId
            && _document.Reference(maskId) is { Name: SkUiVectorDocument.MaskElement } found ? found : null;
        if (mask is not null)
            OpenLayer(null);
        DrawContent(node, state);
        if (mask is not null)
            ApplyMask(mask);

        _layers = layers;
        _canvas.RestoreToCount(restore);
        _depth--;
    }

    private void DrawContent(SkUiVectorNode node, in SkUiVectorState state)
    {
        switch (node.Name)
        {
            case SkUiVectorDocument.SvgElement:
            case SkUiVectorDocument.GroupElement:
            case SkUiVectorDocument.AnchorElement:
                foreach (var child in node.Children)
                    Draw(child, state);
                break;
            case SkUiVectorDocument.TextElement:
                if (state.Visible)
                    DrawText(node, state);
                break;
            default:
                if (state.Visible)
                    DrawShape(node, state);
                break;
        }
    }

    private void DrawShape(SkUiVectorNode node, in SkUiVectorState state)
    {
        using var path = Geometry(node, state.EvenOdd);
        if (path is null)
            return;
        if (state.Color(state.Fill, state.FillOpacity) is { } fill)
        {
            using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = fill };
            _canvas.DrawPath(path, paint);
        }
        if (state.StrokeWidth > 0 && state.Color(state.Stroke, state.StrokeOpacity) is { } stroke)
        {
            Charge(node.Size * StrokeCostPerCharacter);
            if (state.Dash is { } dash)
                Charge(DashCost * Dashes(path, dash));
            using var paint = state.StrokePaint(stroke, out var dashEffect);
            using (dashEffect)
                _canvas.DrawPath(path, paint);
        }
    }

    // How many dashes the stroke has at most: its length over the pattern's, in the units of both.
    private static long Dashes(SKPath path, float[] dash)
    {
        var pattern = 0d;
        foreach (var length in dash)
            pattern += length;
        var total = 0d;
        using var measure = new SKPathMeasure(path);
        do
        {
            total += measure.Length;
        }
        while (measure.NextContour());
        return (long)Math.Min(MaxDashes, total / pattern * dash.Length);
    }

    private void DrawText(SkUiVectorNode node, in SkUiVectorState state)
    {
        var text = string.Join(' ', (node.Text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (text.Length == 0)
            return;
        foreach (var attribute in (ReadOnlySpan<string>)["dx", "dy", "rotate", "textLength"])
        {
            if (node[attribute] is not null)
                throw SkUiVectorValues.Unsupported($"a <text> with {attribute}");
        }
        var attributes = state.FontAttributes;
        using var font = SkUiTextLayout.CreateFont(SkUiTypefaces.Resolve(state.FontFamily, attributes), state.FontSize, attributes);
        var x = TextPosition(node, "x");
        var y = TextPosition(node, "y");
        if (state.Color(state.Fill, state.FillOpacity) is { } fill)
        {
            using var paint = new SKPaint { IsAntialias = true, Color = fill };
            _canvas.DrawText(text, x, y, state.TextAlign, font, paint);
        }
        if (state.StrokeWidth > 0 && state.Color(state.Stroke, state.StrokeOpacity) is { } stroke)
        {
            Charge(node.Size * StrokeCostPerCharacter);
            using var paint = state.StrokePaint(stroke, out var dashEffect);
            using (dashEffect)
                _canvas.DrawText(text, x, y, state.TextAlign, font, paint);
        }
    }

    private static float TextPosition(SkUiVectorNode node, string attribute)
    {
        if (node[attribute] is not { } text)
            return 0;
        return SkUiVectorValues.Numbers(text, attribute) switch
        {
            [] => 0,
            [var position] => position,
            _ => throw SkUiVectorValues.Unsupported($"a <text> with a list of {attribute} positions")
        };
    }

    private void ApplyMask(SkUiVectorNode mask)
    {
        if (!_activeMasks.Add(mask))
            throw new InvalidDataException("SVG: a mask refers to itself.");
        using var paint = new SKPaint { BlendMode = SKBlendMode.DstIn };
        using var luma = mask["mask-type"] is not "alpha" ? SKColorFilter.CreateLumaColor() : null;
        paint.ColorFilter = luma;
        OpenLayer(paint);
        // A region in user units; without a width and a height it is larger than anything an icon shows.
        if (SkUiVectorDocument.IsUserSpaceMask(mask) && mask["width"] is { } widthText && mask["height"] is { } heightText)
        {
            var region = SKRect.Create(
                mask["x"] is { } xText ? SkUiVectorValues.Length(xText, "x") : 0,
                mask["y"] is { } yText ? SkUiVectorValues.Length(yText, "y") : 0,
                SkUiVectorValues.Length(widthText, "width"),
                SkUiVectorValues.Length(heightText, "height"));
            _canvas.ClipRect(region, SKClipOperation.Intersect, antialias: true);
        }
        // A mask's content inherits from the mask and what is around it, not from what uses it.
        var content = StateAt(mask);
        foreach (var child in mask.Children)
            Draw(child, content);
        _canvas.Restore();
        _layers--;
        _canvas.Restore();
        _layers--;
        _activeMasks.Remove(mask);
    }

    // The state inside a clipPath or mask: what it and the elements around it set, whatever uses it.
    private SkUiVectorState StateAt(SkUiVectorNode node)
    {
        var path = new Stack<SkUiVectorNode>();
        for (var current = node; current is not null; current = current.Parent)
        {
            Charge(ElementCost + current.Size);
            path.Push(current);
        }
        var state = SkUiVectorState.Initial(_currentColor);
        foreach (var element in path)
            state = state.Inherit(element);
        return state;
    }

    private void Charge(long work)
    {
        _token.ThrowIfCancellationRequested();
        _work += work;
        if (_work > MaxWork)
            throw new InvalidDataException("SVG: drawing the file takes too much work.");
    }

    private void OpenLayer(SKPaint? paint)
    {
        if (++_layers > MaxLayers)
            throw new InvalidDataException($"SVG: more than {MaxLayers} layers of opacity and masks are open at once.");
        Charge(_layerCost);
        if (paint is null)
            _canvas.SaveLayer();
        else
            _canvas.SaveLayer(paint);
    }

    private SKPath ClipGeometry(SkUiVectorNode clip)
    {
        var inherited = StateAt(clip);
        SKPath? union = null;
        try
        {
            foreach (var child in clip.Children)
            {
                Charge(ElementCost + child.Size);
                if (child.Name == SkUiVectorDocument.TextElement || child[SkUiVectorDocument.ClipPathProperty] is not null)
                    throw SkUiVectorValues.Unsupported($"<{child.Name}> with a clip of its own or text in a clipPath");
                if (IsHidden(child) || !IsShape(child))
                    continue;
                var state = inherited.Inherit(child);
                if (!state.Visible)
                    continue;
                using var geometry = Geometry(child, state.ClipEvenOdd);
                if (geometry is null)
                    continue;
                using var moved = child["transform"] is { } transform ? Moved(geometry, SkUiVectorValues.Transform(transform)) : null;
                var next = moved ?? geometry;
                if (union is null)
                {
                    union = new SKPath(next);
                    continue;
                }
                Charge(ClipOperationCostPerPoint * (union.PointCount + next.PointCount));
                var combined = union.Op(next, SKPathOp.Union) ?? throw new InvalidDataException("SVG: a clipPath could not be combined.");
                union.Dispose();
                union = combined;
            }
        }
        catch
        {
            union?.Dispose();
            throw;
        }
        union ??= new SKPath();
        if (clip["transform"] is not { } own)
            return union;
        using (union)
            return Moved(union, SkUiVectorValues.Transform(own));
    }

    private static SKPath Moved(SKPath path, SKMatrix matrix)
    {
        var moved = new SKPath(path);
        moved.Transform(matrix);
        return moved;
    }

    private static bool IsShape(SkUiVectorNode node) => node.Name is SkUiVectorDocument.PathElement or SkUiVectorDocument.RectElement
        or SkUiVectorDocument.CircleElement or SkUiVectorDocument.EllipseElement or SkUiVectorDocument.LineElement
        or SkUiVectorDocument.PolylineElement or SkUiVectorDocument.PolygonElement;

    private static bool IsDrawnElement(SkUiVectorNode node) => IsShape(node)
        || node.Name is SkUiVectorDocument.SvgElement or SkUiVectorDocument.GroupElement or SkUiVectorDocument.AnchorElement or SkUiVectorDocument.TextElement;

    private static bool IsHidden(SkUiVectorNode node) => node["display"]?.Trim() == "none";

    /// <summary>The element's outline; <c>null</c> when it has none to draw (SVG: a rect of no width, a circle of no radius).</summary>
    private static SKPath? Geometry(SkUiVectorNode node, bool evenOdd)
    {
        var path = node.Name switch
        {
            SkUiVectorDocument.PathElement => PathData(node),
            SkUiVectorDocument.RectElement => Rect(node),
            SkUiVectorDocument.CircleElement => Circle(node),
            SkUiVectorDocument.EllipseElement => Ellipse(node),
            SkUiVectorDocument.LineElement => Line(node),
            SkUiVectorDocument.PolylineElement => Points(node, close: false),
            SkUiVectorDocument.PolygonElement => Points(node, close: true),
            _ => null
        };
        if (path is not null)
            path.FillType = evenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        return path;
    }

    private static SKPath? PathData(SkUiVectorNode node)
    {
        if (string.IsNullOrWhiteSpace(node["d"]))
            return null;
        return SKPath.ParseSvgPathData(node["d"]) ?? throw SkUiVectorValues.Invalid("path data", node["d"]!);
    }

    private static SKPath? Rect(SkUiVectorNode node)
    {
        var rect = SKRect.Create(Number(node, "x"), Number(node, "y"), Number(node, "width"), Number(node, "height"));
        if (rect.Width <= 0 || rect.Height <= 0)
            return null;
        var rx = OptionalNumber(node, "rx");
        var ry = OptionalNumber(node, "ry");
        var radiusX = Math.Clamp(rx ?? ry ?? 0, 0, rect.Width / 2);
        var radiusY = Math.Clamp(ry ?? rx ?? 0, 0, rect.Height / 2);
        using var builder = new SKPathBuilder();
        if (radiusX > 0 && radiusY > 0)
            builder.AddRoundRect(rect, radiusX, radiusY);
        else
            builder.AddRect(rect);
        return builder.Detach();
    }

    private static SKPath? Circle(SkUiVectorNode node)
    {
        var radius = Number(node, "r");
        if (radius <= 0)
            return null;
        using var builder = new SKPathBuilder();
        builder.AddCircle(Number(node, "cx"), Number(node, "cy"), radius);
        return builder.Detach();
    }

    private static SKPath? Ellipse(SkUiVectorNode node)
    {
        var rx = OptionalNumber(node, "rx");
        var ry = OptionalNumber(node, "ry");
        var radiusX = rx ?? ry ?? 0;
        var radiusY = ry ?? rx ?? 0;
        if (radiusX <= 0 || radiusY <= 0)
            return null;
        var cx = Number(node, "cx");
        var cy = Number(node, "cy");
        using var builder = new SKPathBuilder();
        builder.AddOval(new SKRect(cx - radiusX, cy - radiusY, cx + radiusX, cy + radiusY));
        return builder.Detach();
    }

    private static SKPath Line(SkUiVectorNode node)
    {
        using var builder = new SKPathBuilder();
        builder.MoveTo(Number(node, "x1"), Number(node, "y1"));
        builder.LineTo(Number(node, "x2"), Number(node, "y2"));
        return builder.Detach();
    }

    private static SKPath? Points(SkUiVectorNode node, bool close)
    {
        var points = SkUiVectorValues.Numbers(node["points"] ?? string.Empty, "points");
        if (points.Length < 2)
            return null;
        using var builder = new SKPathBuilder();
        builder.MoveTo(points[0], points[1]);
        for (var index = 2; index + 1 < points.Length; index += 2)
            builder.LineTo(points[index], points[index + 1]);
        if (close)
            builder.Close();
        return builder.Detach();
    }

    private static float Number(SkUiVectorNode node, string attribute) => OptionalNumber(node, attribute) ?? 0;

    private static float? OptionalNumber(SkUiVectorNode node, string attribute)
    {
        if (node[attribute] is not { } text || text.Trim() == "auto")
            return null;
        if (text.TrimEnd().EndsWith('%'))
            throw SkUiVectorValues.Unsupported($"a {attribute} in percent");
        return SkUiVectorValues.Length(text, attribute);
    }
}

/// <summary>The properties that elements inherit from their parents.</summary>
internal readonly record struct SkUiVectorState
{
    private const string InheritKeyword = "inherit";

    public SKColor CurrentColor { get; init; }
    public SkUiVectorPaint Fill { get; init; }
    public SkUiVectorPaint Stroke { get; init; }
    public float FillOpacity { get; init; }
    public float StrokeOpacity { get; init; }
    public float StrokeWidth { get; init; }
    public SKStrokeCap Cap { get; init; }
    public SKStrokeJoin Join { get; init; }
    public float MiterLimit { get; init; }
    public float[]? Dash { get; init; }
    public float DashOffset { get; init; }
    public bool EvenOdd { get; init; }
    public bool ClipEvenOdd { get; init; }
    public bool Visible { get; init; }
    public string? FontFamily { get; init; }
    public float FontSize { get; init; }
    public FontAttributes FontAttributes { get; init; }
    public SKTextAlign TextAlign { get; init; }

    /// <summary>SVG's initial values, with <paramref name="currentColor"/> as <c>currentColor</c>.</summary>
    public static SkUiVectorState Initial(SKColor currentColor) => new()
    {
        CurrentColor = currentColor,
        Fill = new SkUiVectorPaint(SkUiVectorPaintKind.Color, SKColors.Black),
        Stroke = SkUiVectorPaint.None,
        FillOpacity = 1,
        StrokeOpacity = 1,
        StrokeWidth = 1,
        MiterLimit = 4,
        Visible = true,
        FontSize = 16
    };

    /// <summary>This state with what <paramref name="node"/> sets.</summary>
    public SkUiVectorState Inherit(SkUiVectorNode node)
    {
        var state = this;
        foreach (var (name, raw) in node.Attributes)
        {
            var value = raw.Trim();
            if (value == InheritKeyword)
                continue;
            state = name switch
            {
                "color" => state with { CurrentColor = value.Equals("currentColor", StringComparison.OrdinalIgnoreCase) ? state.CurrentColor : SkUiVectorValues.Color(value, name) },
                "fill" => state with { Fill = SkUiVectorValues.Paint(value, name) },
                "stroke" => state with { Stroke = SkUiVectorValues.Paint(value, name) },
                "fill-opacity" => state with { FillOpacity = SkUiVectorValues.Opacity(value, name) },
                "stroke-opacity" => state with { StrokeOpacity = SkUiVectorValues.Opacity(value, name) },
                "stroke-width" => state with { StrokeWidth = StrokeLength(value, name) },
                "stroke-linecap" => state with { Cap = value switch { "round" => SKStrokeCap.Round, "square" => SKStrokeCap.Square, _ => SKStrokeCap.Butt } },
                "stroke-linejoin" => state with { Join = value switch { "round" => SKStrokeJoin.Round, "bevel" => SKStrokeJoin.Bevel, _ => SKStrokeJoin.Miter } },
                "stroke-miterlimit" => state with { MiterLimit = SkUiVectorValues.Length(value, name) },
                "stroke-dasharray" => state with { Dash = DashArray(value, name) },
                "stroke-dashoffset" => state with { DashOffset = StrokeLength(value, name) },
                "fill-rule" => state with { EvenOdd = value == "evenodd" },
                "clip-rule" => state with { ClipEvenOdd = value == "evenodd" },
                "visibility" => state with { Visible = value == "visible" },
                "font-family" => state with { FontFamily = Family(value) },
                "font-size" => state with { FontSize = SkUiVectorValues.Length(value, name) },
                "font-weight" => state with { FontAttributes = Weight(value) ? state.FontAttributes | FontAttributes.Bold : state.FontAttributes & ~FontAttributes.Bold },
                "font-style" => state with { FontAttributes = value is "italic" or "oblique" ? state.FontAttributes | FontAttributes.Italic : state.FontAttributes & ~FontAttributes.Italic },
                "text-anchor" => state with { TextAlign = value switch { "middle" => SKTextAlign.Center, "end" => SKTextAlign.Right, _ => SKTextAlign.Left } },
                _ => state
            };
        }
        return state;
    }

    /// <summary>The color <paramref name="paint"/> draws with at <paramref name="opacity"/>; <c>null</c>: nothing to draw.</summary>
    public SKColor? Color(SkUiVectorPaint paint, float opacity)
    {
        if (paint.Kind == SkUiVectorPaintKind.None)
            return null;
        var color = paint.Kind == SkUiVectorPaintKind.Current ? CurrentColor : paint.Color;
        return color.WithAlpha((byte)Math.Round(color.Alpha * opacity));
    }

    /// <summary>
    /// A stroke paint with this state's settings. A paint does not take over a path effect it is given: the caller disposes
    /// <paramref name="dash"/> as well.
    /// </summary>
    public SKPaint StrokePaint(SKColor color, out SKPathEffect? dash)
    {
        dash = Dash is { } pattern ? SKPathEffect.CreateDash(pattern, DashOffset) : null;
        return new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = StrokeWidth,
            StrokeCap = Cap,
            StrokeJoin = Join,
            StrokeMiter = MiterLimit,
            Color = color,
            PathEffect = dash
        };
    }

    private static float StrokeLength(string value, string name)
    {
        if (value.EndsWith('%'))
            throw SkUiVectorValues.Unsupported($"a {name} in percent");
        return SkUiVectorValues.Length(value, name);
    }

    private static float[]? DashArray(string value, string name)
    {
        if (value == "none")
            return null;
        var parts = value.Split([' ', ',', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var dash = new float[parts.Length % 2 == 0 ? parts.Length : parts.Length * 2];
        var sum = 0f;
        for (var index = 0; index < dash.Length; index++)
        {
            var part = StrokeLength(parts[index % parts.Length], name);
            if (part < 0)
                return null; // SVG: an error in the list draws a solid line
            dash[index] = part;
            sum += part;
        }
        return sum > 0 ? dash : null;
    }

    private static string? Family(string value)
    {
        var first = value.Split(',')[0].Trim().Trim('"', '\'');
        return first is "serif" or "sans-serif" or "monospace" or "cursive" or "fantasy" or "system-ui" ? null : first;
    }

    private static bool Weight(string value) =>
        value is "bold" or "bolder" || (int.TryParse(value, out var weight) && weight >= (int)SKFontStyleWeight.SemiBold);
}
