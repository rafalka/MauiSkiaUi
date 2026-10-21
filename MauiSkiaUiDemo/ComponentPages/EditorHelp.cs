using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace MauiSkiaUiDemo;

/// <summary>A description shown by an editor's (?) popup.</summary>
/// <param name="Source">Where it comes from: the documented member (<c>SkUiLabel.ShrinkToFit</c>), or <c>null</c> for the demo's own text.</param>
/// <param name="Html">The description in the HTML subset drawn labels show (<c>TextType = Html</c>).</param>
internal sealed record EditorHelpEntry(string? Source, string Html);

/// <summary>
/// Descriptions of the component pages' editors, for their (?) popups. An editor named after a property gets the property's
/// XML documentation (SkiaUi's, then MAUI's: the control's type and its bases, the page's other documented types); an editor
/// that sets up a scenario gets its section of <c>EditorHelp.md</c>, which also overrides a property's text. Both are embedded
/// at build time (<c>EditorHelp.targets</c>), so a documentation comment changed in the library shows in the demo's next build.
/// </summary>
internal sealed partial class EditorHelp
{
    /// <summary>Logical name of the embedded Markdown file.</summary>
    internal const string MarkdownResource = "EditorHelp.md";

    /// <summary>Prefix of the embedded XML documentation files' logical names.</summary>
    internal const string ApiDocsPrefix = "ApiDocs/";

    private static readonly Lazy<EditorHelp> Loaded = new(Load);

    // Markdown sections ("LabelDemoPage.Script") as HTML; XML documentation members ("P:MauiSkiaUi.SkUiLabel.FontSize";
    // getters of attached properties without their parameters) as their raw XML, converted when an editor asks.
    private readonly Dictionary<string, string> _sections;
    private readonly Dictionary<string, string> _members;

    private EditorHelp(Dictionary<string, string> sections, Dictionary<string, string> members)
    {
        _sections = sections;
        _members = members;
    }

    /// <summary>The descriptions, read on first use (<see cref="Preload"/> reads them in the background at startup).</summary>
    public static EditorHelp Shared => Loaded.Value;

    /// <summary>Starts reading the descriptions on a background thread, so the first component page does not wait.</summary>
    public static void Preload() => Task.Run(() => Loaded.Value);

    /// <summary>The <c>Page.Editor</c> keys of the Markdown sections.</summary>
    internal IEnumerable<string> SectionKeys => _sections.Keys;

    /// <summary>
    /// The description of the editor <paramref name="title"/> on a page of type <paramref name="page"/>: its Markdown section
    /// (the page's, then its base pages'), else, when <paramref name="property"/>, the documentation of the property the title
    /// names on one of <paramref name="types"/>. <c>null</c> when there is none.
    /// </summary>
    public EditorHelpEntry? Find(Type page, IReadOnlyList<Type> types, string title, bool property = true)
    {
        for (var type = page; type is not null && type != typeof(ContentPage); type = type.BaseType)
            if (_sections.TryGetValue(type.Name + "." + title, out var section))
                return new EditorHelpEntry(null, section);
        if (!property)
            return null;
        foreach (var name in MemberNames(title))
            foreach (var type in types)
                if (FindMember(type, name) is { } entry)
                    return entry;
        return null;
    }

    /// <summary>
    /// The property names an editor title can stand for: the title without a parenthesized note ("MaxTokenLines (0: no
    /// limit)"), and for a dotted one the property after the dot ("Child3.Grow", "Content.HeightRequest"), then before it
    /// ("CornerRadii.TopLeft").
    /// </summary>
    private static IEnumerable<string> MemberNames(string title)
    {
        var name = ParenthesizedNote().Replace(title, "").Trim();
        var dot = name.IndexOf('.');
        if (dot < 0)
        {
            if (IsIdentifier(name))
                yield return name;
            yield break;
        }
        if (IsIdentifier(name[(dot + 1)..]))
            yield return name[(dot + 1)..];
        if (IsIdentifier(name[..dot]))
            yield return name[..dot];
    }

    private static bool IsIdentifier(string name) => name.Length > 0 && char.IsLetter(name[0]) && name.All(char.IsLetterOrDigit);

    /// <summary>
    /// The documentation of <paramref name="name"/> on <paramref name="type"/>, its bases or its interfaces: the property, its
    /// bindable field, or an attached property's getter. A member that only inherits its documentation is passed over.
    /// </summary>
    private EditorHelpEntry? FindMember(Type type, string name)
    {
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
            if (FindDeclared(current, name) is { } entry)
                return entry;
        foreach (var contract in type.GetInterfaces())
            if (FindDeclared(contract, name) is { } entry)
                return entry;
        return null;
    }

    private EditorHelpEntry? FindDeclared(Type type, string name)
    {
        var typeName = DocName(type);
        foreach (var key in (ReadOnlySpan<string>)[$"P:{typeName}.{name}", $"F:{typeName}.{name}Property", $"M:{typeName}.Get{name}"])
            if (Resolve(key, depth: 0) is { } html)
            {
                // The documented type's own members read without it ("MinimumFontScale" on SkUiLabel.ShrinkToFit).
                var shortName = ShortName(typeName);
                return new EditorHelpEntry(shortName + "." + name, html.Replace("<code>" + shortName + ".", "<code>", StringComparison.Ordinal));
            }
        return null;
    }

    /// <summary>The HTML of member <paramref name="key"/>, following <c>inheritdoc cref</c>; <c>null</c> for none.</summary>
    private string? Resolve(string key, int depth)
    {
        if (depth > 4 || !_members.TryGetValue(key, out var xml))
            return null;
        var member = XElement.Parse(xml);
        if (member.Element("inheritdoc") is { } inherit)
            return inherit.Attribute("cref")?.Value is { } cref ? Resolve(MemberKey(cref), depth + 1) : null;
        var html = new StringBuilder();
        if (member.Element("summary") is { } summary)
            AppendBlock(html, summary);
        if (member.Element("remarks") is { } remarks)
            AppendBlock(html, remarks);
        return html.Length > 0 ? html.ToString() : null;
    }

    /// <summary>A documentation section as paragraphs: its text up to the first block (<c>para</c>, <c>list</c>) in one.</summary>
    private static void AppendBlock(StringBuilder html, XElement section)
    {
        var paragraph = new StringBuilder();
        foreach (var node in section.Nodes())
        {
            if (node is XElement { Name.LocalName: "para" or "list" or "code" } block)
            {
                Flush();
                AppendNode(html, block);
            }
            else
            {
                AppendNode(paragraph, node);
            }
        }
        Flush();

        void Flush()
        {
            var text = paragraph.ToString().Trim();
            if (text.Length > 0)
                html.Append("<p>").Append(text).Append("</p>");
            paragraph.Clear();
        }
    }

    private static void AppendNode(StringBuilder html, XNode node)
    {
        switch (node)
        {
            case XText text:
                html.Append(WebUtility.HtmlEncode(Whitespace().Replace(text.Value, " ")));
                break;
            case XElement element:
                AppendElement(html, element);
                break;
        }
    }

    private static void AppendElement(StringBuilder html, XElement element)
    {
        switch (element.Name.LocalName)
        {
            case "para":
                html.Append("<p>");
                AppendChildren(html, element);
                html.Append("</p>");
                break;
            case "c" or "code":
                html.Append("<code>");
                AppendChildren(html, element);
                html.Append("</code>");
                break;
            case "b" or "i" or "u":
                html.Append('<').Append(element.Name.LocalName).Append('>');
                AppendChildren(html, element);
                html.Append("</").Append(element.Name.LocalName).Append('>');
                break;
            case "see" or "seealso":
                if (element.Attribute("langword")?.Value is { } word)
                    html.Append("<code>").Append(word).Append("</code>");
                else if (!element.IsEmpty)
                    AppendChildren(html, element);
                else if (element.Attribute("cref")?.Value is { } cref)
                    html.Append("<code>").Append(WebUtility.HtmlEncode(CrefText(cref))).Append("</code>");
                else if (element.Attribute("href")?.Value is { } href)
                    html.Append(WebUtility.HtmlEncode(href));
                break;
            case "paramref" or "typeparamref":
                html.Append("<i>").Append(WebUtility.HtmlEncode(element.Attribute("name")?.Value ?? "")).Append("</i>");
                break;
            case "list":
                var tag = element.Attribute("type")?.Value == "number" ? "ol" : "ul";
                html.Append('<').Append(tag).Append('>');
                foreach (var item in element.Elements("item"))
                {
                    html.Append("<li>");
                    if (item.Element("term") is { } term)
                    {
                        html.Append("<b>");
                        AppendChildren(html, term);
                        html.Append("</b>: ");
                    }
                    foreach (var part in (item.Element("description") ?? item).Nodes())
                        if (part is not XElement { Name.LocalName: "term" })
                            AppendNode(html, part);
                    html.Append("</li>");
                }
                html.Append("</").Append(tag).Append('>');
                break;
            default:
                AppendChildren(html, element);
                break;
        }
    }

    private static void AppendChildren(StringBuilder html, XElement element)
    {
        foreach (var node in element.Nodes())
            AppendNode(html, node);
    }

    /// <summary>How a <c>cref</c> reads in a description: <c>SkUiLabel.MaxLines</c>, <c>LineBreakMode.WordWrap</c>, <c>SkUiLabel</c>.</summary>
    private static string CrefText(string cref)
    {
        var name = cref.Length > 2 && cref[1] == ':' ? cref[2..] : cref;
        var parameters = name.IndexOf('(');
        if (parameters >= 0)
            name = name[..parameters];
        name = GenericArity().Replace(name, "");
        var parts = name.Split('.');
        return cref.StartsWith("T:", StringComparison.Ordinal) || parts.Length < 2 ? parts[^1] : parts[^2] + "." + parts[^1];
    }

    /// <summary>The <see cref="_members"/> key of a <c>cref</c>: methods without their parameters.</summary>
    private static string MemberKey(string cref)
    {
        var parameters = cref.IndexOf('(');
        return parameters >= 0 ? cref[..parameters] : cref;
    }

    /// <summary>A type's name in XML documentation: the generic definition's, with dots for nested types.</summary>
    private static string DocName(Type type)
    {
        if (type.IsGenericType)
            type = type.GetGenericTypeDefinition();
        return (type.FullName ?? type.Name).Replace('+', '.');
    }

    private static string ShortName(string typeName) => GenericArity().Replace(typeName[(typeName.LastIndexOf('.') + 1)..], "");

    private static EditorHelp Load()
    {
        var watch = Stopwatch.StartNew();
        var assembly = typeof(EditorHelp).Assembly;
        var sections = new Dictionary<string, string>(StringComparer.Ordinal);
        if (assembly.GetManifestResourceStream(MarkdownResource) is { } markdown)
            using (var reader = new StreamReader(markdown))
                ReadSections(reader.ReadToEnd(), sections);
        var members = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(ApiDocsPrefix, StringComparison.Ordinal))
                continue;
            using var stream = assembly.GetManifestResourceStream(resource)!;
            ReadMembers(stream, members);
        }
        Trace.WriteLine($"Demo: editor help read in {watch.ElapsedMilliseconds} ms ({sections.Count} sections, {members.Count} documented members).");
        return new EditorHelp(sections, members);
    }

    /// <summary>Keeps the members editors can be named after: properties, bindable fields and attached properties' getters.</summary>
    private static void ReadMembers(Stream stream, Dictionary<string, string> members)
    {
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { IgnoreComments = true, IgnoreProcessingInstructions = true });
        while (reader.ReadToFollowing("member"))
        {
            var name = reader.GetAttribute("name");
            if (name is null)
                continue;
            if (name.StartsWith("P:", StringComparison.Ordinal) || (name.StartsWith("F:", StringComparison.Ordinal) && name.EndsWith("Property", StringComparison.Ordinal)))
                members[name] = reader.ReadOuterXml();
            else if (name.StartsWith("M:", StringComparison.Ordinal) && name.Contains(".Get", StringComparison.Ordinal))
                members[MemberKey(name)] = reader.ReadOuterXml();
        }
    }

    /// <summary>
    /// The <c>## Page.Editor</c> sections of <paramref name="markdown"/> as HTML; text before the first section is the
    /// file's own introduction.
    /// </summary>
    internal static void ReadSections(string markdown, Dictionary<string, string> sections)
    {
        string? key = null;
        var body = new List<string>();
        foreach (var line in markdown.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                Add();
                key = trimmed[3..].Trim();
                body.Clear();
            }
            else if (key is not null)
            {
                body.Add(trimmed);
            }
        }
        Add();

        void Add()
        {
            if (key is not null)
                sections[key] = MarkdownToHtml(body);
        }
    }

    /// <summary>
    /// The Markdown the help file uses as HTML: paragraphs (separated by blank lines), <c>-</c> and <c>1.</c> lists,
    /// <c>**bold**</c>, <c>*italic*</c>, <c>`code`</c> and <c>[links](https://…)</c>.
    /// </summary>
    internal static string MarkdownToHtml(IEnumerable<string> lines)
    {
        var html = new StringBuilder();
        var paragraph = new List<string>();
        string? list = null;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            var bullet = BulletItem().Match(line);
            var numbered = NumberedItem().Match(line);
            if (line.Length == 0)
            {
                EndParagraph();
                EndList();
            }
            else if (bullet.Success || numbered.Success)
            {
                EndParagraph();
                var tag = bullet.Success ? "ul" : "ol";
                if (list != tag)
                {
                    EndList();
                    html.Append('<').Append(tag).Append('>');
                    list = tag;
                }
                html.Append("<li>").Append(Inline((bullet.Success ? bullet : numbered).Groups[1].Value)).Append("</li>");
            }
            else if (list is not null)
            {
                // A wrapped list item: continues the last one.
                html.Length -= "</li>".Length;
                html.Append(' ').Append(Inline(line)).Append("</li>");
            }
            else
            {
                paragraph.Add(line);
            }
        }
        EndParagraph();
        EndList();
        return html.ToString();

        void EndParagraph()
        {
            if (paragraph.Count > 0)
                html.Append("<p>").Append(Inline(string.Join(' ', paragraph))).Append("</p>");
            paragraph.Clear();
        }

        void EndList()
        {
            if (list is not null)
                html.Append("</").Append(list).Append('>');
            list = null;
        }
    }

    private static string Inline(string text)
    {
        text = WebUtility.HtmlEncode(text);
        text = MarkdownCode().Replace(text, "<code>$1</code>");
        text = MarkdownBold().Replace(text, "<b>$1</b>");
        text = MarkdownItalic().Replace(text, "<i>$1</i>");
        return MarkdownLink().Replace(text, "<a href=\"$2\">$1</a>");
    }

    [GeneratedRegex(@"\s*\([^)]*\)\s*$")]
    private static partial Regex ParenthesizedNote();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"`\d+")]
    private static partial Regex GenericArity();

    [GeneratedRegex(@"^[-*]\s+(.*)$")]
    private static partial Regex BulletItem();

    [GeneratedRegex(@"^\d+\.\s+(.*)$")]
    private static partial Regex NumberedItem();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex MarkdownCode();

    [GeneratedRegex(@"\*\*([^*]+)\*\*")]
    private static partial Regex MarkdownBold();

    [GeneratedRegex(@"(?<![*\w])\*([^*]+)\*(?![*\w])")]
    private static partial Regex MarkdownItalic();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)\s]+)\)")]
    private static partial Regex MarkdownLink();
}
