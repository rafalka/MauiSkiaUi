using Markdig;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Tables;
using Markdig.Parsers.Inlines;
using Markdig.Renderers;
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

    // Definitions and setext underlines in a document.
    private const int MaximumSlowLines = 4_000;

    // Longer than any alt text or data URL header a real image has, and short enough that a scan cannot add up.
    private const int MaximumSkippedHeaderLength = 1_024;

    /// <summary>
    /// Only what the view draws, not <c>UseAdvancedExtensions</c> (diagrams, media links, figures, citations and more the builder would
    /// have to handle). HTML shows as text; emoji without smileys, so <c>:)</c> stays text; generic attributes last, as Markdig requires.
    /// </summary>
    internal static MarkdownPipeline Pipeline { get; } = CreateBuilder().Build();

    /// <summary>The same blocks as <see cref="Pipeline"/>, with their inlines left as text.</summary>
    private static MarkdownPipeline BlocksOnly { get; } = CreateBlocksOnly();

    private static MarkdownPipelineBuilder CreateBuilder() => new MarkdownPipelineBuilder()
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
        .UseGenericAttributes();

    private static MarkdownPipeline CreateBlocksOnly()
    {
        var builder = CreateBuilder();
        builder.Extensions.Add(new BlocksOnlyExtension());
        return builder.Build();
    }

    /// <summary>Runs after the others, which look for the parsers they extend.</summary>
    private sealed class BlocksOnlyExtension : IMarkdownExtension
    {
        internal static readonly object FootnotesKey = new();

        public void Setup(MarkdownPipelineBuilder pipeline)
        {
            pipeline.InlineParsers.RemoveAll(static parser => parser is not LiteralInlineParser);
            // Without inline parsers no footnote has a link, and Markdig drops those from the document after this pass, though a full parse reads them: keep them here.
            if (pipeline.BlockParsers.Find<FootnoteParser>() is { } footnotes)
                footnotes.Closed += static (processor, block) =>
                {
                    if (processor.Document.GetData(FootnotesKey) is not List<Block> kept)
                        processor.Document.SetData(FootnotesKey, kept = []);
                    kept.Add(block);
                };
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
        }
    }

    /// <summary>Parses <paramref name="text"/>; Markdig throws on nesting past its limits, which the caller catches.</summary>
    internal static MarkdownDocument Parse(string text) => Markdig.Markdown.Parse(text, Pipeline);

    /// <summary>
    /// Whether a block the inline parser will read holds too many delimiters to parse in reasonable time. The blocks come from Markdig itself, with no inline parsers: only it can
    /// tell a paragraph from code, as what ends a fence or a list depends on every extension in the pipeline. So what is never parsed for delimiters (code, formulas) does not count,
    /// and neither does the data of a <c>data:</c> image. Throws as <see cref="Parse"/> does.
    /// </summary>
    internal static bool HasPathologicalBlock(string text)
    {
        if (HasManySlowLines(text))
            return true;
        // Fewer in all than a block may hold: most documents need no pass.
        if (text.Length == 0 || !HoldsTooManyDelimiters(text, new SourceSpan(0, text.Length - 1)))
            return false;
        var document = Markdig.Markdown.Parse(text, BlocksOnly);
        var containers = new Stack<ContainerBlock>();
        containers.Push(document);
        if (document.GetData(BlocksOnlyExtension.FootnotesKey) is List<Block> footnotes)
            foreach (var footnote in footnotes)
                containers.Push((ContainerBlock)footnote);
        while (containers.TryPop(out var container))
        {
            foreach (var block in container)
            {
                switch (block)
                {
                    case Table: // a grid table's cells are parsed apart from the document: all of it counts, the safe side
                    case LeafBlock { ProcessInlines: true }:
                        if (HoldsTooManyDelimiters(text, block.Span))
                            return true;
                        break;
                    case ContainerBlock inner:
                        containers.Push(inner);
                        break;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// The block pass itself takes time faster than the text's size for reference and footnote definitions and setext underlines: 100,000 definitions, 700 KB, take 12 s on a
    /// desktop. Documents do not hold thousands of these, so counting lines keeps the pass from starting on such a text. Blocks are not known yet, so lines in code count too.
    /// </summary>
    private static bool HasManySlowLines(string text)
    {
        var count = 0;
        var remaining = text.AsSpan();
        while (!remaining.IsEmpty)
        {
            var end = remaining.IndexOfAny('\n', '\r');
            var line = (end < 0 ? remaining : remaining[..end]).TrimStart(" \t>");
            remaining = end < 0 ? default : remaining[(end + 1)..];
            var isDefinition = line.IndexOf("]:") >= 0; // anywhere: a label can start on an earlier line, and a definition can be in a list item
            var isUnderline = !line.IsEmpty && line[0] is '=' or '-' && line.TrimEnd(" \t").TrimStart(line[0]).IsEmpty;
            if ((isDefinition || isUnderline) && ++count > MaximumSlowLines)
                return true;
        }
        return false;
    }

    private static bool HoldsTooManyDelimiters(string text, SourceSpan span)
    {
        var end = Math.Min(text.Length, span.End + 1);
        var delimiters = 0;
        var (dataStart, dataEnd) = (-1, -1);
        for (var index = Math.Max(0, span.Start); index < end; index++)
        {
            if (index == dataStart)
            {
                index = dataEnd;
                continue;
            }
            if (text[index] == '!')
                (dataStart, dataEnd) = FindImageData(text, index, end);
            if (text[index] is '*' or '_' or '[' or ']' or '`' or '~' or '^' or '=' or '+' or '$' or '<' or '!' or ':' && ++delimiters > MaximumDelimitersPerBlock)
                return true;
        }
        return false;
    }

    /// <summary>
    /// The start of the destination and its <c>)</c>, when an image written <c>![alt](data:type;base64,data)</c> starts at <paramref name="index"/>; otherwise -1. Only a whole,
    /// plain image counts, with nothing in its header or data that could keep it from being one: then its data is a destination, not text. Its alt text is parsed, and counts.
    /// </summary>
    private static (int Start, int End) FindImageData(string text, int index, int end)
    {
        const string Destination = "](data:";
        if (index + 1 >= end || text[index + 1] != '[')
            return (-1, -1);
        var position = index + 2;
        var altEnd = Math.Min(end, position + MaximumSkippedHeaderLength);
        while (position < altEnd && text[position] is not ('[' or ']' or '`' or '\\'))
            position++;
        if (!text.AsSpan(position, end - position).StartsWith(Destination, StringComparison.Ordinal))
            return (-1, -1);
        var start = position + 1;
        position += Destination.Length;
        var headerEnd = Math.Min(end, position + MaximumSkippedHeaderLength);
        while (position < headerEnd && (char.IsAsciiLetterOrDigit(text[position]) || text[position] is '/' or '+' or '-' or '.' or ';' or '='))
            position++;
        if (position >= headerEnd || text[position] != ',')
            return (-1, -1);
        position++;
        while (position < end && (char.IsAsciiLetterOrDigit(text[position]) || text[position] is '+' or '/' or '='))
            position++;
        return position < end && text[position] == ')' ? (start, position) : (-1, -1);
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
