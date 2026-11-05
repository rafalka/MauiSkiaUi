using System.Xml;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>An element of a vector icon. Its attributes hold the winning value of each property once the document is read.</summary>
internal sealed class SkUiVectorNode(string name)
{
    public string Name { get; } = name;
    public Dictionary<string, string> Attributes { get; } = new(StringComparer.Ordinal);
    public List<SkUiVectorNode> Children { get; } = [];

    /// <summary>The characters directly inside a <c>style</c> or <c>text</c> element.</summary>
    public string? Text { get; set; }

    public string? this[string attribute] => Attributes.GetValueOrDefault(attribute);
}

/// <summary>
/// The part of SVG that icons use, read into a small tree (<see cref="SkUiVectorPainter"/> draws it). No dependency on an SVG
/// library: paths, shapes, fill and stroke, dashes, transforms, clips, masks, class rules and one line of text.
/// </summary>
/// <remarks>
/// A file cannot reach outside itself: no element here refers to anything but the document, a DTD is skipped and its entities
/// are not expanded, and there is no XML resolver. What cannot be drawn fails with <see cref="InvalidDataException"/> naming
/// it, so an icon is never shown with a part missing. Elements that are not SVG are skipped with their content.
/// </remarks>
internal sealed class SkUiVectorDocument
{
    /// <summary>The largest file read. Icons take a few kilobytes.</summary>
    internal const int MaxBytes = 1024 * 1024;

    /// <summary>The most elements kept from a file.</summary>
    internal const int MaxElements = 20_000;

    /// <summary>How deep elements nest; drawing recurses over them.</summary>
    internal const int MaxDepth = 64;

    internal const string SvgElement = "svg";
    internal const string GroupElement = "g";
    internal const string PathElement = "path";
    internal const string RectElement = "rect";
    internal const string CircleElement = "circle";
    internal const string EllipseElement = "ellipse";
    internal const string LineElement = "line";
    internal const string PolylineElement = "polyline";
    internal const string PolygonElement = "polygon";
    internal const string TextElement = "text";
    internal const string DefsElement = "defs";
    internal const string ClipPathElement = "clipPath";
    internal const string MaskElement = "mask";
    private const string StyleElement = "style";

    internal const string ClipPathProperty = "clip-path";
    internal const string MaskProperty = "mask";
    private const string FillProperty = "fill";
    private const string StrokeProperty = "stroke";
    private const string StyleAttribute = "style";
    private const string ClassAttribute = "class";
    private const string IdAttribute = "id";
    private const string WidthAttribute = "width";
    private const string HeightAttribute = "height";
    private const string ViewBoxAttribute = "viewBox";
    private const string SvgNamespace = "http://www.w3.org/2000/svg";
    private const string ObjectBoundingBox = "objectBoundingBox";

    private static readonly HashSet<string> Drawn = new(StringComparer.Ordinal)
    {
        SvgElement, GroupElement, PathElement, RectElement, CircleElement, EllipseElement, LineElement, PolylineElement, PolygonElement,
        TextElement, DefsElement, ClipPathElement, MaskElement, StyleElement
    };

    // SVG the reader cannot draw: an icon without them would be wrong, so these fail. Other unknown elements are skipped.
    private static readonly HashSet<string> NotDrawn = new(StringComparer.Ordinal)
    {
        "image", "use", "symbol", "pattern", "linearGradient", "radialGradient", "filter", "foreignObject", "tspan", "textPath", "marker", "switch"
    };

    private readonly Dictionary<string, SkUiVectorNode> _references = new(StringComparer.Ordinal);

    private SkUiVectorDocument(SkUiVectorNode root)
    {
        Root = root;
        (Size, ViewBox) = ReadSize(root);
    }

    /// <summary>The root <c>svg</c> element.</summary>
    public SkUiVectorNode Root { get; }

    /// <summary>The intrinsic size in DIPs.</summary>
    public SKSize Size { get; }

    /// <summary>The part of the user space that fills <see cref="Size"/>.</summary>
    public SKRect ViewBox { get; }

    /// <summary>The <c>clipPath</c> or <c>mask</c> element called <paramref name="id"/>.</summary>
    public SkUiVectorNode? Reference(string id) => _references.GetValueOrDefault(id);

    /// <summary>Whether <paramref name="bytes"/> can be an SVG: <c>&lt;</c> first, after a byte order mark and white space.</summary>
    public static bool LooksLikeSvg(ReadOnlySpan<byte> bytes)
    {
        if (bytes is [0xFF, 0xFE, ..] or [0xFE, 0xFF, ..])
            return true;
        var start = TextStart(bytes);
        return start < bytes.Length && bytes[start] == '<';
    }

    /// <summary>Reads a document; <see cref="InvalidDataException"/> when it is not one this class can draw.</summary>
    public static SkUiVectorDocument Parse(byte[] bytes)
    {
        if (bytes.Length > MaxBytes)
            throw new InvalidDataException($"SVG: the file is larger than {MaxBytes} bytes.");
        try
        {
            return Read(bytes);
        }
        catch (XmlException error)
        {
            throw new InvalidDataException("SVG: " + error.Message, error);
        }
    }

    private static SkUiVectorDocument Read(byte[] bytes)
    {
        // A UTF-16 BOM stays for the reader to see. White space before an XML declaration is tolerated: some exports start with it.
        var start = bytes is [0xFF, 0xFE, ..] or [0xFE, 0xFF, ..] ? 0 : TextStart(bytes);
        using var stream = new MemoryStream(bytes, start, bytes.Length - start, writable: false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = MaxBytes * 2L,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true
        });

        SkUiVectorNode? root = null;
        var open = new Stack<SkUiVectorNode>();
        var styleSheets = new List<SkUiVectorNode>();
        var count = 0;
        var more = reader.Read();
        while (more)
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    if (root is not null && open.Count == 0)
                        throw new InvalidDataException("SVG: more than one root element.");
                    var svgNamespace = reader.NamespaceURI.Length == 0 || reader.NamespaceURI == SvgNamespace;
                    var name = reader.LocalName;
                    if (root is null && (!svgNamespace || name != SvgElement))
                        throw new InvalidDataException($"SVG: the root element is <{name}>, not <svg>.");
                    if (svgNamespace && NotDrawn.Contains(name))
                        throw SkUiVectorValues.Unsupported($"<{name}>");
                    if (root is not null && svgNamespace && name == SvgElement)
                        throw SkUiVectorValues.Unsupported("a nested <svg>");
                    if (!svgNamespace || !Drawn.Contains(name))
                    {
                        reader.Skip();
                        more = !reader.EOF;
                        continue;
                    }
                    if (open.Count >= MaxDepth)
                        throw new InvalidDataException($"SVG: elements nest deeper than {MaxDepth} levels.");
                    if (++count > MaxElements)
                        throw new InvalidDataException($"SVG: the file has more than {MaxElements} elements.");
                    var empty = reader.IsEmptyElement;
                    var node = new SkUiVectorNode(name);
                    ReadAttributes(reader, node);
                    if (name == StyleElement)
                        styleSheets.Add(node);
                    if (open.TryPeek(out var parent))
                        parent.Children.Add(node);
                    else
                        root = node;
                    if (!empty)
                        open.Push(node);
                    break;
                case XmlNodeType.EndElement:
                    open.Pop();
                    break;
                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                case XmlNodeType.SignificantWhitespace:
                case XmlNodeType.Whitespace:
                    if (open.TryPeek(out var holder) && holder.Name is TextElement or StyleElement)
                        holder.Text += reader.Value;
                    break;
            }
            more = reader.Read();
        }
        if (root is null)
            throw new InvalidDataException("SVG: the file has no root element.");

        var rules = new List<(string Class, List<KeyValuePair<string, string>> Declarations)>();
        foreach (var sheet in styleSheets)
            ReadStyleSheet(sheet.Text ?? string.Empty, rules);
        var document = new SkUiVectorDocument(root);
        document.Finish(root, rules);
        return document;
    }

    private static void ReadAttributes(XmlReader reader, SkUiVectorNode node)
    {
        if (!reader.MoveToFirstAttribute())
            return;
        do
        {
            if (reader.Prefix.Length == 0 && reader.LocalName != "xmlns")
                node.Attributes[reader.LocalName] = reader.Value;
        }
        while (reader.MoveToNextAttribute());
        reader.MoveToElement();
    }

    /// <summary>Applies the cascade (attribute, then class rules, then the inline style) and checks what the document refers to.</summary>
    private void Finish(SkUiVectorNode node, List<(string Class, List<KeyValuePair<string, string>> Declarations)> rules)
    {
        if (node[ClassAttribute] is { } classes && rules.Count > 0)
        {
            foreach (var (name, declarations) in rules)
            {
                if (!HasClass(classes, name))
                    continue;
                foreach (var (property, value) in declarations)
                    node.Attributes[property] = value;
            }
        }
        if (node[StyleAttribute] is { } style)
        {
            foreach (var (property, value) in Declarations(style))
                node.Attributes[property] = value;
        }

        foreach (var property in (ReadOnlySpan<string>)[FillProperty, StrokeProperty])
        {
            if (node[property] is { } paint && paint.TrimStart().StartsWith("url(", StringComparison.OrdinalIgnoreCase))
                throw SkUiVectorValues.Unsupported($"a {property} that refers to a gradient or pattern");
        }
        if (node["filter"] is { } filter && !IsNone(filter))
            throw SkUiVectorValues.Unsupported("a filter");
        if (node.Name is ClipPathElement or MaskElement)
        {
            if (node[node.Name == MaskElement ? "maskContentUnits" : "clipPathUnits"] == ObjectBoundingBox)
                throw SkUiVectorValues.Unsupported($"<{node.Name}> in {ObjectBoundingBox} units");
            if (node[IdAttribute] is { } id)
                _references.TryAdd(id, node);
        }
        foreach (var child in node.Children)
            Finish(child, rules);
    }

    /// <summary>The id in <c>url(#id)</c>; <c>null</c> for <c>none</c>.</summary>
    internal static string? ReferenceId(string value, string name)
    {
        if (IsNone(value))
            return null;
        var text = value.AsSpan().Trim();
        if (!text.StartsWith("url(", StringComparison.OrdinalIgnoreCase) || text[^1] != ')')
            throw SkUiVectorValues.Unsupported($"the {name} \"{value}\"");
        var id = text[4..^1].Trim().Trim("\"'");
        if (id.IsEmpty || id[0] != '#')
            throw SkUiVectorValues.Unsupported($"the {name} \"{value}\"");
        return id[1..].ToString();
    }

    private static bool IsNone(string value) => value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase);

    private static bool HasClass(string classes, string name)
    {
        foreach (var range in classes.AsSpan().SplitAny(" \t\r\n"))
        {
            if (classes.AsSpan()[range].SequenceEqual(name))
                return true;
        }
        return false;
    }

    private static void ReadStyleSheet(string css, List<(string, List<KeyValuePair<string, string>>)> rules)
    {
        var rest = RemoveComments(css).AsSpan().Trim();
        while (!rest.IsEmpty)
        {
            var open = rest.IndexOf('{');
            var close = rest.IndexOf('}');
            if (open < 0 || close < open)
                throw SkUiVectorValues.Unsupported($"the style sheet \"{rest.ToString()}\"");
            var selectors = rest[..open].ToString();
            var declarations = Declarations(rest[(open + 1)..close].ToString()).ToList();
            foreach (var selector in selectors.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (selector[0] != '.' || selector.AsSpan(1).IndexOfAny(" .#:[>+~*") >= 0)
                    throw SkUiVectorValues.Unsupported($"the style selector \"{selector}\"");
                rules.Add((selector[1..], declarations));
            }
            rest = rest[(close + 1)..].TrimStart();
        }
    }

    private static string RemoveComments(string css)
    {
        var start = css.IndexOf("/*", StringComparison.Ordinal);
        while (start >= 0)
        {
            var end = css.IndexOf("*/", start + 2, StringComparison.Ordinal);
            css = end < 0 ? css[..start] : css.Remove(start, end + 2 - start);
            start = css.IndexOf("/*", StringComparison.Ordinal);
        }
        return css;
    }

    private static IEnumerable<KeyValuePair<string, string>> Declarations(string text)
    {
        foreach (var declaration in text.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = declaration.IndexOf(':');
            if (colon <= 0)
                continue;
            var value = declaration[(colon + 1)..].Trim();
            if (value.EndsWith("!important", StringComparison.OrdinalIgnoreCase))
                value = value[..^"!important".Length].TrimEnd();
            yield return new KeyValuePair<string, string>(declaration[..colon].Trim(), value);
        }
    }

    private static (SKSize Size, SKRect ViewBox) ReadSize(SkUiVectorNode root)
    {
        SKRect? viewBox = null;
        if (root[ViewBoxAttribute] is { } text)
        {
            var box = SkUiVectorValues.Numbers(text, ViewBoxAttribute);
            if (box.Length != 4 || box[2] <= 0 || box[3] <= 0)
                throw SkUiVectorValues.Invalid(ViewBoxAttribute, text);
            viewBox = SKRect.Create(box[0], box[1], box[2], box[3]);
        }
        // Percentages say nothing without a container: the viewBox gives the size instead.
        var width = RootLength(root, WidthAttribute);
        var height = RootLength(root, HeightAttribute);
        if (viewBox is { } view)
        {
            width ??= height * view.Width / view.Height ?? view.Width;
            height ??= width * view.Height / view.Width;
        }
        if (width is not > 0 || height is not > 0)
            throw new InvalidDataException("SVG: the file has no size: it needs a viewBox, or a width and a height.");
        return (new SKSize(width.Value, height.Value), viewBox ?? SKRect.Create(width.Value, height.Value));
    }

    private static float? RootLength(SkUiVectorNode root, string attribute)
    {
        if (root[attribute] is not { } text || text.TrimEnd().EndsWith('%'))
            return null;
        return SkUiVectorValues.Length(text, attribute);
    }

    private static int TextStart(ReadOnlySpan<byte> bytes)
    {
        var start = bytes is [0xEF, 0xBB, 0xBF, ..] ? 3 : 0;
        while (start < bytes.Length && bytes[start] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            start++;
        return start;
    }
}
