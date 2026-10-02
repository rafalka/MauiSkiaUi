using System.Globalization;
using System.Net;
using System.Text;
using MauiSkiaUi.Core;

namespace MauiSkiaUi;

/// <summary>
/// The style of a run of HTML text, relative to the label that shows it: what the markup sets; the rest is the label's.
/// </summary>
/// <param name="FontAttributes">Bold / italic added to the label's.</param>
/// <param name="Decorations">Underline / strikethrough added to the label's.</param>
/// <param name="TextColor">Color, or <c>null</c> for the label's.</param>
/// <param name="Background">Fill behind the run, or <c>null</c>.</param>
/// <param name="SizeScale">Multiplier of the label's font size (headings, <c>big</c>, <c>em</c> sizes).</param>
/// <param name="FontSize">An absolute size in DIPs (<c>font-size: 12px</c>), or <c>null</c> for the scaled label size.</param>
/// <param name="FontFamily">Family, or <c>null</c> for the label's.</param>
/// <param name="Href">The link target of an <c>a href</c>, or <c>null</c>.</param>
internal readonly record struct SkUiHtmlStyle(
    FontAttributes FontAttributes = FontAttributes.None,
    TextDecorations Decorations = TextDecorations.None,
    Color? TextColor = null,
    Color? Background = null,
    double SizeScale = 1,
    double? FontSize = null,
    string? FontFamily = null,
    string? Href = null)
{
    /// <summary>Text the markup does not style (<c>default</c> would scale sizes by 0).</summary>
    internal static SkUiHtmlStyle Plain { get; } = new(SizeScale: 1);

    /// <summary>The font size against <paramref name="labelSize"/>.</summary>
    internal double Size(double labelSize) => FontSize ?? labelSize * SizeScale;
}

/// <summary>A run of HTML text in one style (newlines separate paragraphs).</summary>
internal readonly record struct SkUiHtmlRun(string Text, SkUiHtmlStyle Style);

/// <summary>
/// HTML for drawn labels (MAUI's <c>Label.TextType="Html"</c>): a small, tolerant parser of the tag subset Android's
/// <c>Html.fromHtml</c> supports, turned into styled runs drawn by the formatted-text engine. Identical on every platform,
/// no dependencies, never throws (unparsable markup shows as its raw text).
/// <para>
/// Supported: <c>b</c> / <c>strong</c>, <c>i</c> / <c>em</c> / <c>cite</c> / <c>dfn</c>, <c>u</c> / <c>ins</c>,
/// <c>s</c> / <c>strike</c> / <c>del</c>, <c>big</c> / <c>small</c>, <c>sub</c> / <c>sup</c> (smaller, no baseline shift),
/// <c>h1</c>–<c>h6</c>, <c>tt</c> / <c>code</c> / <c>pre</c> (monospace; <c>pre</c> keeps whitespace), <c>font</c>
/// (<c>color</c>, <c>face</c>, <c>size</c>), <c>a href</c> (links), <c>p</c>, <c>div</c>, <c>blockquote</c>, <c>br</c>,
/// <c>hr</c>, <c>ul</c> / <c>ol</c> / <c>li</c> (bullets and numbers), and on any element a <c>style</c> with
/// <c>color</c>, <c>background-color</c>, <c>font-size</c>, <c>font-weight</c>, <c>font-style</c>, <c>font-family</c> and
/// <c>text-decoration</c>. Whitespace collapses as in HTML; entities are decoded. Other tags show their content;
/// <c>img</c>, <c>script</c> and <c>style</c> show nothing.
/// </para>
/// </summary>
public static class SkUiHtml
{
    /// <summary>
    /// MAUI spans for <paramref name="html"/>, for a label of <paramref name="fontSize"/> DIPs (headings and relative sizes
    /// scale it). With <paramref name="linkTapped"/>, links get a <see cref="TapGestureRecognizer"/> that calls it with their
    /// <c>href</c>. Unset values are left to the label (its font, color, attributes).
    /// </summary>
    public static FormattedString ToFormattedString(string? html, double fontSize = 16, Action<string>? linkTapped = null)
    {
        var formatted = new FormattedString();
        foreach (var run in Parse(html))
        {
            var style = run.Style;
            var span = new Span { Text = run.Text };
            if (style.FontAttributes != FontAttributes.None) span.FontAttributes = style.FontAttributes;
            if (style.Decorations != TextDecorations.None) span.TextDecorations = style.Decorations;
            if (style.TextColor is { } color) span.TextColor = color;
            if (style.Background is { } background) span.BackgroundColor = background;
            if (style.FontSize is not null || style.SizeScale != 1) span.FontSize = style.Size(fontSize);
            if (style.FontFamily is { } family) span.FontFamily = family;
            if (style.Href is { } href && linkTapped is not null)
                span.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => linkTapped(href)) });
            formatted.Spans.Add(span);
        }
        return formatted;
    }

    /// <summary>
    /// Core spans for <paramref name="html"/> (see <see cref="ToFormattedString"/>); with <paramref name="linkTapped"/>,
    /// links are tappable (<see cref="SkUiCoreSpan.Tapped"/>).
    /// </summary>
    public static IReadOnlyList<SkUiCoreSpan> ToCoreSpans(string? html, double fontSize = 16, Action<string>? linkTapped = null)
    {
        var spans = new List<SkUiCoreSpan>();
        foreach (var run in Parse(html))
        {
            var style = run.Style;
            var span = new SkUiCoreSpan(run.Text)
                .SetTextColor(style.TextColor)
                .SetBackgroundColor(style.Background)
                .SetFontFamily(style.FontFamily)
                .SetFontAttributes(style.FontAttributes == FontAttributes.None ? null : style.FontAttributes)
                .SetTextDecorations(style.Decorations == TextDecorations.None ? null : style.Decorations)
                .SetFontSize(style.FontSize is not null || style.SizeScale != 1 ? style.Size(fontSize) : null);
            if (style.Href is { } href && linkTapped is not null)
                span.Tapped += (_, _) => linkTapped(href);
            spans.Add(span);
        }
        return spans;
    }

    /// <summary>Whether any of <paramref name="runs"/> is a link.</summary>
    internal static bool HasLinks(IReadOnlyList<SkUiHtmlRun> runs)
    {
        foreach (var run in runs)
            if (run.Style.Href is not null)
                return true;
        return false;
    }

    /// <summary>The runs of <paramref name="html"/>; the raw text as one run when the markup cannot be read.</summary>
    internal static IReadOnlyList<SkUiHtmlRun> Parse(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return [];
        try
        {
            return new Parser(html).Run();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return [new SkUiHtmlRun(html, SkUiHtmlStyle.Plain)];
        }
    }

    /// <summary>
    /// <paramref name="runs"/> resolved against a label's values (what the markup leaves unset, and the base of its
    /// relative sizes) for the formatted-text engine; one engine span per run.
    /// </summary>
    internal static SkUiRichText ToRichText(IReadOnlyList<SkUiHtmlRun> runs, string? fontFamily, double fontSize, FontAttributes fontAttributes,
        double characterSpacing, double lineHeight, Color textColor, TextDecorations decorations)
    {
        if (runs.Count == 0) return SkUiRichText.Empty;
        var builder = new SkUiRichText.Builder();
        foreach (var (text, style) in runs)
        {
            var attributes = fontAttributes | style.FontAttributes;
            builder.Add(text,
                new SkUiTextSpanStyle(SkUiTypefaces.Resolve(style.FontFamily ?? fontFamily, attributes), style.Size(fontSize), attributes, characterSpacing, lineHeight),
                new SkUiTextSpanPaint(SkUiToggleDrawing.ToSkColor(style.TextColor ?? textColor),
                    style.Background is { } background ? SkUiToggleDrawing.ToSkColor(background) : default, decorations | style.Decorations));
        }
        return builder.Build();
    }

    /// <summary>The platform's monospace family (<c>tt</c>, <c>code</c>, <c>pre</c>, <c>font-family: monospace</c>).</summary>
    internal static string MonospaceFamily =>
        OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst() || OperatingSystem.IsMacOS() ? "Menlo"
        : OperatingSystem.IsWindows() ? "Consolas"
        : "monospace";

    private static readonly double[] HeadingScales = [1.5, 1.4, 1.3, 1.2, 1.1, 1.0];
    // HTML <font size="1".."7">, as browsers scale them.
    private static readonly double[] FontSizeScales = [0.63, 0.82, 1, 1.13, 1.5, 2, 3];

    private sealed class Parser(string html)
    {
        private readonly List<(StringBuilder Text, SkUiHtmlStyle Style)> _runs = [];
        private readonly List<Element> _open = [];
        private SkUiHtmlStyle _style = SkUiHtmlStyle.Plain;
        private int _pre;
        // Whitespace collapsing: at the start of a line nothing is emitted for spaces; after a space, no second one.
        private bool _lineStart = true;
        private bool _lastSpace;
        private bool _pendingBreak;

        private sealed class Element(string name, SkUiHtmlStyle saved)
        {
            public string Name { get; } = name;
            public SkUiHtmlStyle Saved { get; } = saved;
            public bool Ordered { get; init; }
            public int Items { get; set; }
        }

        public List<SkUiHtmlRun> Run()
        {
            var index = 0;
            while (index < html.Length)
            {
                var open = html.IndexOf('<', index);
                if (open < 0)
                {
                    Text(html.AsSpan(index));
                    break;
                }
                Text(html.AsSpan(index, open - index));
                index = Markup(open);
            }
            TrimEnd(trimNewlines: true);
            var runs = new List<SkUiHtmlRun>(_runs.Count);
            foreach (var (text, style) in _runs)
                if (text.Length > 0)
                    runs.Add(new SkUiHtmlRun(text.ToString(), style));
            return runs;
        }

        /// <summary>Reads the markup at <paramref name="start"/> (a <c>&lt;</c>); returns where the text continues.</summary>
        private int Markup(int start)
        {
            if (string.CompareOrdinal(html, start, "<!--", 0, 4) == 0)
            {
                var end = html.IndexOf("-->", start + 4, StringComparison.Ordinal);
                return end < 0 ? html.Length : end + 3;
            }
            var position = start + 1;
            if (position < html.Length && html[position] is '!' or '?')
            {
                var end = html.IndexOf('>', position);
                return end < 0 ? html.Length : end + 1;
            }
            var closing = position < html.Length && html[position] == '/';
            if (closing) position++;
            var nameStart = position;
            while (position < html.Length && (char.IsAsciiLetterOrDigit(html[position]) || html[position] is '-' or ':'))
                position++;
            if (position == nameStart || !char.IsAsciiLetter(html[nameStart]))
            {
                Text("<"); // a lone '<' is text
                return start + 1;
            }
            var name = html[nameStart..position].ToLowerInvariant();
            var attributes = closing ? null : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var selfClosing = false;
            while (true)
            {
                while (position < html.Length && char.IsWhiteSpace(html[position])) position++;
                if (position >= html.Length)
                {
                    Text(html.AsSpan(start)); // an unterminated tag is text
                    return html.Length;
                }
                var c = html[position];
                if (c == '>') { position++; break; }
                if (c == '/') { selfClosing = true; position++; continue; }
                var attributeStart = position;
                while (position < html.Length && !char.IsWhiteSpace(html[position]) && html[position] is not ('=' or '>' or '/'))
                    position++;
                var attribute = html[attributeStart..position];
                if (attribute.Length == 0) { position++; continue; }
                while (position < html.Length && char.IsWhiteSpace(html[position])) position++;
                var value = string.Empty;
                if (position < html.Length && html[position] == '=')
                {
                    position++;
                    while (position < html.Length && char.IsWhiteSpace(html[position])) position++;
                    if (position < html.Length && html[position] is '"' or '\'')
                    {
                        var quote = html[position];
                        var end = html.IndexOf(quote, position + 1);
                        if (end < 0) end = html.Length;
                        value = html[(position + 1)..end];
                        position = Math.Min(html.Length, end + 1);
                    }
                    else
                    {
                        var valueStart = position;
                        while (position < html.Length && !char.IsWhiteSpace(html[position]) && html[position] != '>') position++;
                        value = html[valueStart..position];
                    }
                }
                attributes![attribute] = WebUtility.HtmlDecode(value);
            }
            if (closing)
            {
                End(name);
                return position;
            }
            if (name is "script" or "style")
            {
                // Raw content up to the matching end tag is not shown.
                var end = html.IndexOf("</" + name, position, StringComparison.OrdinalIgnoreCase);
                if (end < 0) return html.Length;
                var close = html.IndexOf('>', end);
                return close < 0 ? html.Length : close + 1;
            }
            Start(name, attributes!, selfClosing);
            return position;
        }

        private static bool IsBlock(string name) => name is "p" or "div" or "blockquote" or "pre" or "ul" or "ol" or "li"
            or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "hr" or "header" or "footer" or "section" or "article" or "table" or "tr";

        private void Start(string name, Dictionary<string, string> attributes, bool selfClosing)
        {
            switch (name)
            {
                case "br":
                    TrimEnd(trimNewlines: false);
                    Emit("\n");
                    _lineStart = true;
                    _pendingBreak = false;
                    return;
                case "hr":
                    Break();
                    return;
                case "img" or "wbr" or "meta" or "link" or "input" or "col" or "area" or "base" or "source":
                    return;
            }
            // An open paragraph or list item ends where the next one starts (HTML's implied end tags).
            if (name is "p" or "li" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "div" or "ul" or "ol" or "blockquote" or "pre")
                CloseImplied(name is "li" ? ["li"] : ["p"]);
            if (IsBlock(name))
                Break();
            var element = new Element(name, _style) { Ordered = name == "ol" };
            _style = Styled(name, attributes, _style);
            if (name == "pre") _pre++;
            if (selfClosing)
            {
                _style = element.Saved;
                if (name == "pre") _pre--;
                return;
            }
            _open.Add(element);
            if (name == "li")
                ListItem();
        }

        private void CloseImplied(string[] names)
        {
            for (var index = _open.Count - 1; index >= 0; index--)
            {
                var open = _open[index].Name;
                if (Array.IndexOf(names, open) >= 0)
                {
                    End(open);
                    return;
                }
                if (open is "ul" or "ol" or "div" or "blockquote" or "table" or "td" or "th")
                    return;
            }
        }

        private void ListItem()
        {
            Element? list = null;
            var depth = 0;
            foreach (var open in _open)
                if (open.Name is "ul" or "ol")
                {
                    list = open;
                    depth++;
                }
            var indent = new string(' ', Math.Max(0, depth - 1));
            var marker = list is { Ordered: true } ? $"{++list.Items}. " : "• ";
            if (list is { Ordered: false }) list.Items++;
            EmitPending();
            Emit(indent + marker);
            _lineStart = false;
            _lastSpace = true; // the marker ends with a space
        }

        private void End(string name)
        {
            var index = _open.FindLastIndex(open => open.Name == name);
            if (index < 0)
                return; // a stray end tag
            var element = _open[index];
            for (var inner = _open.Count - 1; inner >= index; inner--)
                if (_open[inner].Name == "pre") _pre--;
            _open.RemoveRange(index, _open.Count - index);
            _style = element.Saved;
            if (IsBlock(name))
                Break();
        }

        /// <summary>A paragraph break before the next text (one between blocks, none at the start).</summary>
        private void Break()
        {
            if (_runs.Exists(run => run.Text.Length > 0))
                _pendingBreak = true;
            _lineStart = true;
        }

        private void EmitPending()
        {
            if (!_pendingBreak) return;
            _pendingBreak = false;
            TrimEnd(trimNewlines: false);
            if (_runs.Count > 0 && _runs[^1].Text is { Length: > 0 } last && last[^1] == '\n')
                return; // a <br> already ended the line
            Emit("\n");
        }

        private void Text(ReadOnlySpan<char> raw)
        {
            if (raw.IsEmpty) return;
            var text = raw.IndexOf('&') < 0 ? raw.ToString() : WebUtility.HtmlDecode(raw.ToString());
            var output = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                if (_pre > 0)
                {
                    if (c == '\r') continue;
                    Flush(output);
                    output.Append(c);
                    _lineStart = c == '\n';
                    continue;
                }
                if (c is ' ' or '\t' or '\n' or '\r' or '\f')
                {
                    if (_lineStart || _pendingBreak || _lastSpace) continue;
                    output.Append(' ');
                    _lastSpace = true;
                    continue;
                }
                if (_pendingBreak)
                {
                    Emit(output.ToString());
                    output.Clear();
                    EmitPending();
                }
                output.Append(c);
                _lineStart = false;
                _lastSpace = false;
            }
            Emit(output.ToString());

            void Flush(StringBuilder pending)
            {
                if (!_pendingBreak) return;
                Emit(pending.ToString());
                pending.Clear();
                EmitPending();
            }
        }

        private void Emit(string text)
        {
            if (text.Length == 0) return;
            if (_runs.Count > 0 && _runs[^1].Style == _style)
                _runs[^1].Text.Append(text);
            else
                _runs.Add((new StringBuilder(text), _style));
        }

        /// <summary>Drops trailing spaces (and newlines) of what was emitted, across runs.</summary>
        private void TrimEnd(bool trimNewlines)
        {
            for (var index = _runs.Count - 1; index >= 0; index--)
            {
                var text = _runs[index].Text;
                while (text.Length > 0 && (text[^1] == ' ' || (trimNewlines && text[^1] == '\n')))
                    text.Length--;
                if (text.Length > 0)
                    break;
            }
            _lastSpace = false;
        }

        private static SkUiHtmlStyle Styled(string name, Dictionary<string, string> attributes, SkUiHtmlStyle style)
        {
            switch (name)
            {
                case "b" or "strong":
                    style = style with { FontAttributes = style.FontAttributes | FontAttributes.Bold };
                    break;
                case "i" or "em" or "cite" or "dfn" or "var":
                    style = style with { FontAttributes = style.FontAttributes | FontAttributes.Italic };
                    break;
                case "u" or "ins":
                    style = style with { Decorations = style.Decorations | TextDecorations.Underline };
                    break;
                case "s" or "strike" or "del":
                    style = style with { Decorations = style.Decorations | TextDecorations.Strikethrough };
                    break;
                case "big":
                    style = Scaled(style, 1.25);
                    break;
                case "small" or "sub" or "sup":
                    style = Scaled(style, name == "small" ? 0.8 : 0.7);
                    break;
                case "tt" or "code" or "kbd" or "samp" or "pre":
                    style = style with { FontFamily = MonospaceFamily };
                    break;
                case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                    style = Scaled(style, HeadingScales[name[1] - '1']) with { FontAttributes = style.FontAttributes | FontAttributes.Bold };
                    break;
                case "a" when attributes.TryGetValue("href", out var href):
                    style = style with
                    {
                        Href = href,
                        TextColor = SkUiColors.Accent,
                        Decorations = style.Decorations | TextDecorations.Underline
                    };
                    break;
                case "font":
                    if (attributes.TryGetValue("color", out var color) && ParseColor(color) is { } fontColor)
                        style = style with { TextColor = fontColor };
                    if (attributes.TryGetValue("face", out var face) && Family(face) is { } fontFace)
                        style = style with { FontFamily = fontFace };
                    if (attributes.TryGetValue("size", out var size) && FontSizeScale(size) is { } scale)
                        style = Scaled(style, scale);
                    break;
            }
            return attributes.TryGetValue("style", out var css) ? Css(css, style) : style;
        }

        private static SkUiHtmlStyle Scaled(SkUiHtmlStyle style, double scale) => style.FontSize is { } size
            ? style with { FontSize = size * scale }
            : style with { SizeScale = style.SizeScale * scale };

        /// <summary>The inline <c>style</c> declarations SkiaUi draws.</summary>
        private static SkUiHtmlStyle Css(string css, SkUiHtmlStyle style)
        {
            foreach (var declaration in css.Split(';'))
            {
                var colon = declaration.IndexOf(':');
                if (colon <= 0) continue;
                var property = declaration[..colon].Trim().ToLowerInvariant();
                var value = declaration[(colon + 1)..].Replace("!important", "", StringComparison.OrdinalIgnoreCase).Trim();
                var lower = value.ToLowerInvariant();
                switch (property)
                {
                    case "color" when ParseColor(value) is { } color:
                        style = style with { TextColor = color };
                        break;
                    case "background-color" or "background" when ParseColor(value) is { } background:
                        style = style with { Background = background };
                        break;
                    case "font-weight":
                        var bold = lower is "bold" or "bolder" || (int.TryParse(lower, NumberStyles.Integer, CultureInfo.InvariantCulture, out var weight) && weight >= 600);
                        style = style with { FontAttributes = bold ? style.FontAttributes | FontAttributes.Bold : style.FontAttributes & ~FontAttributes.Bold };
                        break;
                    case "font-style":
                        var italic = lower is "italic" or "oblique";
                        style = style with { FontAttributes = italic ? style.FontAttributes | FontAttributes.Italic : style.FontAttributes & ~FontAttributes.Italic };
                        break;
                    case "text-decoration" or "text-decoration-line":
                        var decorations = TextDecorations.None;
                        if (lower.Contains("underline")) decorations |= TextDecorations.Underline;
                        if (lower.Contains("line-through")) decorations |= TextDecorations.Strikethrough;
                        style = style with { Decorations = decorations };
                        break;
                    case "font-family" when Family(value) is { } family:
                        style = style with { FontFamily = family };
                        break;
                    case "font-size":
                        style = CssFontSize(lower, style);
                        break;
                }
            }
            return style;
        }

        private static SkUiHtmlStyle CssFontSize(string value, SkUiHtmlStyle style)
        {
            double? Number(string suffix) =>
                value.EndsWith(suffix, StringComparison.Ordinal)
                && double.TryParse(value.AsSpan(0, value.Length - suffix.Length).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                && number > 0 && double.IsFinite(number) ? number : null;

            if (Number("px") is { } px) return style with { FontSize = px, SizeScale = 1 };
            if (Number("pt") is { } pt) return style with { FontSize = pt * 4 / 3, SizeScale = 1 };
            if (Number("rem") is { } rem) return style with { FontSize = null, SizeScale = rem };
            if (Number("em") is { } em) return Scaled(style, em);
            if (Number("%") is { } percent) return Scaled(style, percent / 100);
            if (Number(string.Empty) is { } plain) return style with { FontSize = plain, SizeScale = 1 };
            return value switch
            {
                "xx-small" => Scaled(style, 0.6),
                "x-small" => Scaled(style, 0.75),
                "small" or "smaller" => Scaled(style, 0.89),
                "large" or "larger" => Scaled(style, 1.2),
                "x-large" => Scaled(style, 1.5),
                "xx-large" => Scaled(style, 2),
                _ => style
            };
        }

        /// <summary>HTML <c>font size</c>: 1–7, or relative to 3 (<c>+1</c>, <c>-2</c>).</summary>
        private static double? FontSizeScale(string value)
        {
            value = value.Trim();
            if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var size))
                return null;
            if (value[0] is '+' or '-')
                size += 3;
            return FontSizeScales[Math.Clamp(size, 1, 7) - 1];
        }

        /// <summary>The first family of a CSS / <c>face</c> list; generic <c>monospace</c> is the platform's.</summary>
        private static string? Family(string value)
        {
            var first = value.Split(',')[0].Trim().Trim('"', '\'').Trim();
            if (first.Length == 0) return null;
            return first.Equals("monospace", StringComparison.OrdinalIgnoreCase) ? MonospaceFamily : first;
        }

        private static Color? ParseColor(string value) =>
            Color.TryParse(value.Trim(), out var color) ? color : null;
    }
}
