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

    /// <summary>Whether a block of <paramref name="text"/> (lines up to a blank one) holds too many delimiters to parse in reasonable time.</summary>
    internal static bool HasPathologicalBlock(string text)
    {
        var delimiters = 0;
        var lineHasText = false;
        foreach (var character in text)
        {
            switch (character)
            {
                case '\n':
                    if (!lineHasText) delimiters = 0;
                    lineHasText = false;
                    continue;
                case '*' or '_' or '[' or ']' or '`' or '~' or '^' or '=' or '+' or '$' or '<' or '!' or ':':
                    if (++delimiters > MaximumDelimitersPerBlock)
                        return true;
                    break;
            }
            if (character is not (' ' or '\t' or '\r'))
                lineHasText = true;
        }
        return false;
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
