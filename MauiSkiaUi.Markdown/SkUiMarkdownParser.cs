using Markdig;
using Markdig.Syntax;

namespace MauiSkiaUi.Markdown;

/// <summary>Markdown to syntax tree: one shared pipeline, and what decides where and whether a text is parsed.</summary>
internal static class SkUiMarkdownParser
{
    // Parse time grows faster than a block's size when a block is full of delimiters that do not close (`[a](`, `*a **a`): 13 KB of
    // them take 165 ms on a desktop and 156 KB take 9 s, and a parse cannot be stopped. So a block with more than this shows as plain text.
    internal const int MaximumDelimitersPerBlock = 4_000;

    // Text with more delimiters than this in all parses off the UI thread, however short it is.
    private const int MaximumInPlaceDelimiters = 1_000;

    // Markdown's own numbers: an indent that makes code, the shortest fence, the longest ordered list number.
    private const int CodeIndent = 4;
    private const int MinimumFenceLength = 3;
    private const int MaximumListNumberLength = 9;
    private const int MaximumHeadingLevel = 6;

    // Longer than any alt text or data URL header a real image has, and short enough that a scan cannot add up.
    private const int MaximumSkippedHeaderLength = 1_024;

    /// <summary>
    /// Only what the view draws, not <c>UseAdvancedExtensions</c> (diagrams, media links, figures, citations and more the builder would
    /// have to handle). HTML shows as text; emoji without smileys, so <c>:)</c> stays text; generic attributes last, as Markdig requires.
    /// </summary>
    internal static MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder()
        .UsePreciseSourceLocation()
        .UseAlertBlocks()
        .UseAutoLinks()
        .UseCustomContainers()
        .UseDefinitionLists()
        .UseEmojiAndSmiley(enableSmileys: false)
        .UseEmphasisExtras()
        .UseFootnotes()
        .UseGridTables()
        .UseMathematics()
        .UsePipeTables()
        .UseTaskLists()
        .DisableHtml()
        .UseGenericAttributes()
        .Build();

    /// <summary>Parses <paramref name="text"/>; Markdig throws on nesting past its limits, which the caller catches.</summary>
    internal static MarkdownDocument Parse(string text) => Markdig.Markdown.Parse(text, Pipeline);

    /// <summary>
    /// Whether a run of text the inline parser takes as one holds too many delimiters to parse in reasonable time. A run ends at a blank line, a list item or a heading.
    /// Code at the start of a line (a fence, indented) and the data of a <c>data:</c> image are never parsed for delimiters, so they do not count. This only reads the lines,
    /// so it can count too much (a document then shows as plain text), which is the safe side: what it skips must really be left alone by the parser, or a hostile document gets past it.
    /// </summary>
    internal static bool HasPathologicalBlock(string text)
    {
        var delimiters = 0;
        var previousBlank = true;
        var previousItem = false;
        var inIndentedCode = false;
        var hasContainer = false; // inside a list or a footnote, an indented line is a paragraph, not code
        var hasCustomContainer = false; // the end of one ends a fence in it too, which this does not follow
        var fenceLength = 0;
        var fenceCharacter = '\0';
        var lineStart = 0;
        while (lineStart < text.Length)
        {
            // As the parser: \r alone ends a line too.
            var lineEnd = text.AsSpan(lineStart).IndexOfAny('\n', '\r');
            lineEnd = lineEnd < 0 ? text.Length : lineStart + lineEnd;
            var next = lineEnd + (lineEnd + 1 < text.Length && text[lineEnd] == '\r' && text[lineEnd + 1] == '\n' ? 2 : 1);
            var line = text.AsSpan(lineStart, lineEnd - lineStart);
            var trimmed = line.TrimStart(' ');
            var indent = line.Length - trimmed.Length;
            var isBlank = line.Trim(" \t").IsEmpty; // only these: a line of other white space is text, and does not end a paragraph
            var isItem = false;

            if (fenceLength > 0)
            {
                if (indent < CodeIndent && ClosesFence(trimmed, fenceCharacter, fenceLength))
                {
                    fenceLength = 0;
                    delimiters = 0;
                }
            }
            // Only at the start of a line and outside a custom container: a fence in a list or a container ends with it, however it was opened.
            else if (indent == 0 && !hasCustomContainer && OpensFence(trimmed, out fenceCharacter, out fenceLength))
                delimiters = 0;
            else if (isBlank)
                delimiters = 0;
            else
            {
                hasContainer |= StartsListMarker(trimmed) || trimmed.StartsWith("[^") || trimmed.StartsWith(":");
                hasCustomContainer |= trimmed.StartsWith(":::");
                inIndentedCode = !hasContainer && indent + (line.Length > indent && line[indent] == '\t' ? CodeIndent : 0) >= CodeIndent && (previousBlank || inIndentedCode);
                if (!inIndentedCode)
                {
                    isItem = indent < CodeIndent && StartsListItem(trimmed, afterParagraph: !previousBlank && !previousItem);
                    if (isItem || (indent < CodeIndent && IsHeading(trimmed)))
                        delimiters = 0;
                    var (dataStart, dataEnd) = (-1, -1);
                    for (var index = lineStart; index < lineEnd; index++)
                    {
                        if (index == dataStart)
                        {
                            index = dataEnd;
                            continue;
                        }
                        if (text[index] == '!')
                            (dataStart, dataEnd) = FindImageData(text, index, lineEnd);
                        if (text[index] is '*' or '_' or '[' or ']' or '`' or '~' or '^' or '=' or '+' or '$' or '<' or '!' or ':' && ++delimiters > MaximumDelimitersPerBlock)
                            return true;
                    }
                }
            }
            previousBlank = isBlank;
            previousItem = isItem;
            lineStart = next;
        }
        return false;
    }

    /// <summary>A fence: three or more of the same mark, and an info string without backticks after backticks.</summary>
    private static bool OpensFence(ReadOnlySpan<char> line, out char character, out int length)
    {
        character = line.Length > 0 ? line[0] : '\0';
        length = 0;
        if (character is not ('`' or '~'))
            return false;
        var run = line.Length - line.TrimStart(character).Length;
        if (run < MinimumFenceLength || (character == '`' && line[run..].Contains('`')))
            return false;
        length = run;
        return true;
    }

    private static bool ClosesFence(ReadOnlySpan<char> line, char character, int length)
    {
        var rest = line.TrimStart(character);
        return line.Length - rest.Length >= length && rest.IsWhiteSpace();
    }

    private static bool IsHeading(ReadOnlySpan<char> line)
    {
        var rest = line.TrimStart('#');
        var level = line.Length - rest.Length;
        return level is > 0 and <= MaximumHeadingLevel && rest.Length > 0 && rest[0] is ' ' or '\t';
    }

    /// <summary>
    /// Whether a line starts a list item, and so a run of its own. It must really do so: a line that only continues a paragraph is still that paragraph's text. An empty item
    /// cannot interrupt a paragraph, and after a paragraph (<paramref name="afterParagraph"/>) an ordered item must be number 1.
    /// </summary>
    private static bool StartsListItem(ReadOnlySpan<char> line, bool afterParagraph)
    {
        var marker = ListMarkerLength(line);
        if (marker == 0 || marker >= line.Length || line[marker] is not (' ' or '\t') || line[marker..].Trim(" \t").IsEmpty)
            return false;
        return !afterParagraph || line[0] is '-' or '*' or '+' || (marker == 2 && line[0] == '1');
    }

    /// <summary>Whether a line can open a list, an empty item too: what follows may be inside it.</summary>
    private static bool StartsListMarker(ReadOnlySpan<char> line)
    {
        var marker = ListMarkerLength(line);
        return marker > 0 && (marker == line.Length || line[marker] is ' ' or '\t');
    }

    private static int ListMarkerLength(ReadOnlySpan<char> line)
    {
        if (line.Length > 0 && line[0] is '-' or '*' or '+')
            return 1;
        var digits = 0;
        while (digits < line.Length && digits < MaximumListNumberLength && char.IsAsciiDigit(line[digits]))
            digits++;
        return digits > 0 && digits < line.Length && line[digits] is '.' or ')' ? digits + 1 : 0;
    }

    /// <summary>
    /// The start of the destination and its <c>)</c>, when an image written <c>![alt](data:type;base64,data)</c> starts at <paramref name="index"/>; otherwise -1. Only a whole,
    /// plain image counts, with nothing in its header or data that could keep it from being one: then its data is a destination, not text. Its alt text is parsed, and counts.
    /// </summary>
    private static (int Start, int End) FindImageData(string text, int index, int lineEnd)
    {
        const string Destination = "](data:";
        if (index + 1 >= lineEnd || text[index + 1] != '[')
            return (-1, -1);
        var position = index + 2;
        var altEnd = Math.Min(lineEnd, position + MaximumSkippedHeaderLength);
        while (position < altEnd && text[position] is not ('[' or ']' or '`' or '\\'))
            position++;
        if (!text.AsSpan(position, lineEnd - position).StartsWith(Destination, StringComparison.Ordinal))
            return (-1, -1);
        var start = position + 1;
        position += Destination.Length;
        var headerEnd = Math.Min(lineEnd, position + MaximumSkippedHeaderLength);
        while (position < headerEnd && (char.IsAsciiLetterOrDigit(text[position]) || text[position] is '/' or '+' or '-' or '.' or ';' or '='))
            position++;
        if (position >= headerEnd || text[position] != ',')
            return (-1, -1);
        position++;
        while (position < lineEnd && (char.IsAsciiLetterOrDigit(text[position]) || text[position] is '+' or '/' or '='))
            position++;
        return position < lineEnd && text[position] == ')' ? (start, position) : (-1, -1);
    }

    /// <summary>Whether <paramref name="text"/> is parsed in place: short, and without enough delimiters to take long. A limit of <see cref="int.MaxValue"/> means always.</summary>
    internal static bool ParsesInPlace(string text, int maximumLength)
    {
        if (maximumLength == int.MaxValue)
            return true;
        if (text.Length > maximumLength)
            return false;
        var delimiters = 0;
        foreach (var character in text)
            if (character is '*' or '_' or '[' or '`' or '~' or '^' or '=' or '+' or '$' or '<' && ++delimiters > MaximumInPlaceDelimiters)
                return false;
        return true;
    }
}

/// <summary>A document passed a limit of the builder: it shows as plain text.</summary>
internal sealed class SkUiMarkdownLimitException(string message) : Exception(message);
