using System.Text;
using System.Xml;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>An element of a vector icon. Its attributes hold the winning value of each property once the document is read.</summary>
internal sealed class SkUiVectorNode(string name, SkUiVectorNode? parent)
{
    private StringBuilder? _text;

    public string Name { get; } = name;

    /// <summary>The element around this one; <c>null</c> for the root.</summary>
    public SkUiVectorNode? Parent { get; } = parent;

    public Dictionary<string, string> Attributes { get; } = new(StringComparer.Ordinal);
    public List<SkUiVectorNode> Children { get; } = [];

    /// <summary>The characters directly inside a <c>style</c> or <c>text</c> element.</summary>
    public string? Text => _text?.ToString();

    /// <summary>How many characters of attributes, their names too, and text this element has: what drawing it reads each time.</summary>
    public long Size { get; private set; }

    public void AppendText(string text) => (_text ??= new StringBuilder()).Append(text);

    /// <summary>Works out <see cref="Size"/>, once the cascade has set the attributes.</summary>
    public void MeasureSize()
    {
        Size = _text?.Length ?? 0;
        foreach (var (attribute, value) in Attributes)
            Size += attribute.Length + 1 + value.Length;
    }

    public string? this[string attribute] => Attributes.GetValueOrDefault(attribute);
}

/// <summary>
/// The part of SVG that icons use, read into a small tree (<see cref="SkUiVectorPainter"/> draws it). No dependency on an SVG
/// library: paths, shapes, fill and stroke, dashes, transforms, clips, masks, class rules and one line of text.
/// </summary>
/// <remarks>
/// A file cannot reach outside itself: no element here refers to anything but the document, a DTD is skipped and its entities
/// are not expanded, and there is no XML resolver. What it knows it cannot draw fails with <see cref="InvalidDataException"/>
/// naming it, instead of showing an icon with a part missing. Elements it does not know (<c>title</c>, <c>metadata</c>,
/// <c>script</c>, the <c>animate</c> elements, a design tool's own) are skipped with their content, and so are properties that
/// do not change how an icon looks. Reading and drawing are bounded in work as well as in size, and can be cancelled.
/// </remarks>
internal sealed class SkUiVectorDocument
{
    /// <summary>The largest file read. Icons take a few kilobytes.</summary>
    internal const int MaxBytes = 1024 * 1024;

    /// <summary>The most elements kept from a file.</summary>
    internal const int MaxElements = 20_000;

    /// <summary>How deep elements nest; drawing recurses over them.</summary>
    internal const int MaxDepth = 64;

    /// <summary>The largest intrinsic size in DIPs: what the file says goes to layout, which has no use for more.</summary>
    internal const float MaxSize = 1_000_000;

    /// <summary>The most selectors in a file's style sheets. Icons have a handful.</summary>
    internal const int MaxStyleRules = 5_000;

    /// <summary>How many class names and declarations the cascade may go through: a small file can otherwise multiply them.</summary>
    internal const int MaxStyleWork = 1_000_000;

    internal const string SvgElement = "svg";
    internal const string GroupElement = "g";
    internal const string AnchorElement = "a";
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
    private const string UserSpaceOnUse = "userSpaceOnUse";
    private const string ImportantSuffix = "!important";
    private const double DefaultRegionStart = -0.1;
    private const double DefaultRegionLength = 1.2;

    private static readonly HashSet<string> Drawn = new(StringComparer.Ordinal)
    {
        SvgElement, GroupElement, AnchorElement, PathElement, RectElement, CircleElement, EllipseElement, LineElement, PolylineElement, PolygonElement,
        TextElement, DefsElement, ClipPathElement, MaskElement, StyleElement
    };

    // SVG the reader cannot draw: an icon without them would be wrong, so these fail. Other unknown elements are skipped.
    private static readonly HashSet<string> NotDrawn = new(StringComparer.Ordinal)
    {
        "image", "use", "symbol", "pattern", "linearGradient", "radialGradient", "filter", "foreignObject", "tspan", "textPath", "marker", "switch"
    };

    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Ignore,
        XmlResolver = null,
        MaxCharactersFromEntities = 0,
        MaxCharactersInDocument = MaxBytes * 2L,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true
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
    public static SkUiVectorDocument Parse(byte[] bytes, CancellationToken token = default)
    {
        if (bytes.Length > MaxBytes)
            throw new InvalidDataException($"SVG: the file is larger than {MaxBytes} bytes.");
        // A UTF-16 BOM stays for the reader to see. White space before an XML declaration is tolerated: some exports start with it.
        var start = bytes is [0xFF, 0xFE, ..] or [0xFE, 0xFF, ..] ? 0 : TextStart(bytes);
        using var stream = new MemoryStream(bytes, start, bytes.Length - start, writable: false);
        using var reader = XmlReader.Create(stream, ReaderSettings);
        return Read(reader, token);
    }

    /// <summary>
    /// Reads a document from its text, as <see cref="Parse(byte[], CancellationToken)"/>. An encoding the text declares means
    /// nothing here: it is already characters.
    /// </summary>
    public static SkUiVectorDocument ParseText(string text, CancellationToken token = default)
    {
        if (text.Length > MaxBytes)
            throw new InvalidDataException($"SVG: the file is larger than {MaxBytes} bytes.");
        using var characters = new StringReader(text.TrimStart(' ', '\t', '\r', '\n', '\uFEFF'));
        using var reader = XmlReader.Create(characters, ReaderSettings);
        return Read(reader, token);
    }

    private static SkUiVectorDocument Read(XmlReader reader, CancellationToken token)
    {
        try
        {
            return ReadElements(reader, token);
        }
        catch (XmlException error)
        {
            throw new InvalidDataException("SVG: " + error.Message, error);
        }
    }

    private static SkUiVectorDocument ReadElements(XmlReader reader, CancellationToken token)
    {
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
                    token.ThrowIfCancellationRequested();
                    var empty = reader.IsEmptyElement;
                    open.TryPeek(out var parent);
                    var node = new SkUiVectorNode(name, parent);
                    ReadAttributes(reader, node);
                    if (name == StyleElement)
                        styleSheets.Add(node);
                    if (parent is not null)
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
                        holder.AppendText(reader.Value);
                    break;
            }
            more = reader.Read();
        }
        if (root is null)
            throw new InvalidDataException("SVG: the file has no root element.");

        var rules = new SkUiVectorStyleRules();
        foreach (var sheet in styleSheets)
            ReadStyleSheet(sheet.Text ?? string.Empty, rules);
        var document = new SkUiVectorDocument(root);
        document.Finish(root, rules, token);
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
    private void Finish(SkUiVectorNode node, SkUiVectorStyleRules rules, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ApplyCascade(node, rules);
        node.MeasureSize();

        foreach (var property in (ReadOnlySpan<string>)[FillProperty, StrokeProperty])
        {
            if (node[property] is { } paint && paint.TrimStart().StartsWith("url(", StringComparison.OrdinalIgnoreCase))
                throw SkUiVectorValues.Unsupported($"a {property} that refers to a gradient or pattern");
        }
        CheckNotApplied(node);
        if (node.Name is ClipPathElement or MaskElement)
        {
            CheckReference(node);
            if (node[IdAttribute] is { } id)
                _references.TryAdd(id, node);
        }
        foreach (var child in node.Children)
            Finish(child, rules, token);
    }

    // Properties that change how an element looks and are not drawn: only values that change nothing are read.
    private static void CheckNotApplied(SkUiVectorNode node)
    {
        CheckValue(node, "filter", static value => value.Equals("none", StringComparison.OrdinalIgnoreCase));
        CheckValue(node, "mix-blend-mode", static value => value.Equals("normal", StringComparison.OrdinalIgnoreCase));
        CheckValue(node, "vector-effect", static value => value.Equals("none", StringComparison.OrdinalIgnoreCase));
        CheckValue(node, "transform-box", static value => value.Equals("view-box", StringComparison.OrdinalIgnoreCase));
        CheckValue(node, "transform-origin", IsZeroOrigin);
        CheckValue(node, "paint-order", PaintsFillFirst);
    }

    private static void CheckValue(SkUiVectorNode node, string property, Func<string, bool> changesNothing)
    {
        if (node[property] is { } value && !changesNothing(value.Trim()))
            throw SkUiVectorValues.Unsupported($"a {property}");
    }

    // Both coordinates: a single value leaves the other at its center.
    private static bool IsZeroOrigin(string value)
    {
        var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return false;
        foreach (var part in parts)
        {
            if (!(SkUiVectorValues.TryLength(part.TrimEnd('%'), out var length) && length == 0)) // exact: zero is the point
                return false;
        }
        return true;
    }

    // The order is the one SVG draws in anyway; a keyword left out comes after the ones named.
    private static bool PaintsFillFirst(string value)
    {
        var fill = -1;
        var stroke = -1;
        var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < parts.Length; index++)
        {
            if (parts[index].Equals("normal", StringComparison.OrdinalIgnoreCase))
                return true;
            if (parts[index].Equals("fill", StringComparison.OrdinalIgnoreCase))
                fill = index;
            else if (parts[index].Equals("stroke", StringComparison.OrdinalIgnoreCase))
                stroke = index;
        }
        return stroke < 0 || (fill >= 0 && fill < stroke);
    }

    // A fraction of the element's bounds, written as one or as a percentage; null: not a number.
    private static double? BoundsFraction(string value)
    {
        var text = value.Trim();
        var percent = text.EndsWith('%');
        if (!SkUiVectorValues.TryLength(percent ? text[..^1] : text, out var number))
            return null;
        return percent ? number / 100d : number;
    }

    // A region covering at least the element's geometry, as SVG's own default (-10%, 120%), is not applied. What is left out takes
    // that default, whatever else is set. An exact region does cut a stroke past the geometry, which this reader does not do.
    private static bool RegionCoversBounds(SkUiVectorNode node)
    {
        var x = node["x"] is { } xText ? BoundsFraction(xText) : DefaultRegionStart;
        var y = node["y"] is { } yText ? BoundsFraction(yText) : DefaultRegionStart;
        var width = node["width"] is { } widthText ? BoundsFraction(widthText) : DefaultRegionLength;
        var height = node["height"] is { } heightText ? BoundsFraction(heightText) : DefaultRegionLength;
        return x <= 0 && y <= 0 && x + width >= 1 && y + height >= 1;
    }

    private static void CheckReference(SkUiVectorNode node)
    {
        if (node[node.Name == MaskElement ? "maskContentUnits" : "clipPathUnits"] == ObjectBoundingBox)
            throw SkUiVectorValues.Unsupported($"<{node.Name}> in {ObjectBoundingBox} units");
        foreach (var property in (ReadOnlySpan<string>)[ClipPathProperty, MaskProperty])
        {
            if (node[property] is { } value && !IsNone(value))
                throw SkUiVectorValues.Unsupported($"a {property} on a <{node.Name}>");
        }
        if (node.Name == MaskElement && !IsUserSpaceMask(node) && !RegionCoversBounds(node))
            throw SkUiVectorValues.Unsupported($"a <mask> region smaller than the element in {ObjectBoundingBox} units");
    }

    private static void ApplyCascade(SkUiVectorNode node, SkUiVectorStyleRules rules)
    {
        HashSet<string>? important = null;
        if (node[ClassAttribute] is { } classes && rules.Count > 0)
        {
            foreach (var declarations in rules.Matching(classes))
                Apply(node, declarations, ref important);
        }
        if (node[StyleAttribute] is { } style)
            Apply(node, Declarations(style).ToList(), ref important);
    }

    // Later declarations win, except over one marked !important, which only another !important replaces.
    private static void Apply(SkUiVectorNode node, List<SkUiVectorDeclaration> declarations, ref HashSet<string>? important)
    {
        foreach (var (property, value, isImportant) in declarations)
        {
            if (isImportant)
                (important ??= new HashSet<string>(StringComparer.Ordinal)).Add(property);
            else if (important?.Contains(property) == true)
                continue;
            node.Attributes[property] = value;
        }
    }

    /// <summary>Whether a mask's region is in user units; otherwise it is at least the element's bounds and not applied.</summary>
    internal static bool IsUserSpaceMask(SkUiVectorNode mask) => mask["maskUnits"]?.Trim() == UserSpaceOnUse;

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

    private static void ReadStyleSheet(string css, SkUiVectorStyleRules rules)
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
                if (selector[0] != '.' || selector.AsSpan(1).IndexOfAny(" \t\r\n.#:[>+~*") >= 0)
                    throw SkUiVectorValues.Unsupported($"the style selector \"{selector}\"");
                rules.Add(selector[1..], declarations);
            }
            rest = rest[(close + 1)..].TrimStart();
        }
    }

    private static string RemoveComments(string css)
    {
        var start = css.IndexOf("/*", StringComparison.Ordinal);
        if (start < 0)
            return css;
        var kept = new StringBuilder(css.Length);
        var from = 0;
        while (start >= 0)
        {
            kept.Append(css, from, start - from);
            var end = css.IndexOf("*/", start + 2, StringComparison.Ordinal);
            from = end < 0 ? css.Length : end + 2;
            start = css.IndexOf("/*", from, StringComparison.Ordinal);
        }
        return kept.Append(css, from, css.Length - from).ToString();
    }

    private static IEnumerable<SkUiVectorDeclaration> Declarations(string text)
    {
        foreach (var declaration in text.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = declaration.IndexOf(':');
            if (colon <= 0)
                continue;
            var value = declaration[(colon + 1)..].Trim();
            var important = value.EndsWith(ImportantSuffix, StringComparison.OrdinalIgnoreCase);
            if (important)
                value = value[..^ImportantSuffix.Length].TrimEnd();
            yield return new SkUiVectorDeclaration(declaration[..colon].Trim(), value, important);
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
            var rect = SKRect.Create(box[0], box[1], box[2], box[3]);
            if (!float.IsFinite(rect.Right) || !float.IsFinite(rect.Bottom))
                throw SkUiVectorValues.Invalid(ViewBoxAttribute, text);
            viewBox = rect;
        }
        // Percentages and units relative to a font or a window say nothing without one: the viewBox gives the size instead.
        var width = RootLength(root, WidthAttribute);
        var height = RootLength(root, HeightAttribute);
        if (viewBox is { } view)
        {
            width ??= height * view.Width / view.Height ?? view.Width;
            height ??= width * view.Height / view.Width;
        }
        if (width is not > 0 || height is not > 0)
            throw new InvalidDataException("SVG: the file has no size: it needs a viewBox, or a width and a height.");
        if (width > MaxSize || height > MaxSize || !float.IsFinite(width.Value) || !float.IsFinite(height.Value))
            throw new InvalidDataException($"SVG: the file's size is more than {MaxSize} on a side.");
        return (new SKSize(width.Value, height.Value), viewBox ?? SKRect.Create(width.Value, height.Value));
    }

    private static float? RootLength(SkUiVectorNode root, string attribute)
    {
        if (root[attribute] is not { } text)
            return null;
        var value = text.AsSpan().Trim();
        foreach (var unit in (ReadOnlySpan<string>)["%", "em", "ex", "rem", "ch", "vw", "vh", "vmin", "vmax", "auto"])
        {
            if (value.EndsWith(unit, StringComparison.OrdinalIgnoreCase))
                return null;
        }
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

/// <summary>A property set in a style sheet or an inline style.</summary>
internal readonly record struct SkUiVectorDeclaration(string Property, string Value, bool IsImportant);

/// <summary>A file's class rules, by class: a look-up per class name of an element instead of a walk over every rule.</summary>
internal sealed class SkUiVectorStyleRules
{
    private readonly Dictionary<string, List<(int Order, List<SkUiVectorDeclaration> Declarations)>> _byClass = new(StringComparer.Ordinal);
    private readonly List<(int Order, List<SkUiVectorDeclaration> Declarations)> _matching = [];
    private int _work;

    /// <summary>How many selectors were added.</summary>
    public int Count { get; private set; }

    public void Add(string name, List<SkUiVectorDeclaration> declarations)
    {
        if (++Count > SkUiVectorDocument.MaxStyleRules)
            throw new InvalidDataException($"SVG: the style sheets have more than {SkUiVectorDocument.MaxStyleRules} selectors.");
        if (!_byClass.TryGetValue(name, out var rules))
            _byClass[name] = rules = [];
        rules.Add((Count, declarations));
    }

    /// <summary>The declarations for an element's <c>class</c> attribute, in the order of their rules (later rules win).</summary>
    public IEnumerable<List<SkUiVectorDeclaration>> Matching(string classes)
    {
        _matching.Clear();
        var lookup = _byClass.GetAlternateLookup<ReadOnlySpan<char>>();
        foreach (var range in classes.AsSpan().SplitAny(" \t\r\n"))
        {
            Charge(1);
            if (lookup.TryGetValue(classes.AsSpan()[range], out var rules))
            {
                foreach (var rule in rules)
                {
                    Charge(1 + rule.Declarations.Count);
                    _matching.Add(rule);
                }
            }
        }
        _matching.Sort(static (a, b) => a.Order.CompareTo(b.Order));
        return _matching.Select(static rule => rule.Declarations);
    }

    private void Charge(int work)
    {
        _work += work;
        if (_work > SkUiVectorDocument.MaxStyleWork)
            throw new InvalidDataException("SVG: the style rules take too much work to apply.");
    }
}
