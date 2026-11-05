namespace MauiSkiaUiDemo;

/// <summary>The Markdown demo's document: every feature of the view, in one text.</summary>
internal static class MarkdownDemoDocument
{
    /// <summary>A small PNG, so the page shows a picture without the network.</summary>
    private const string Dot = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAAFklEQVR42mO4o6ZGEmIY1TCqYfhqAAATqigQ9kvG0QAAAABJRU5ErkJggg==";

    private const string Icon = "data:image/svg+xml,%3Csvg%20xmlns='http://www.w3.org/2000/svg'%20width='48'%20height='48'%20viewBox='0%200%2024%2024'%3E%3Ccircle%20cx='12'%20cy='12'%20r='10'%20fill='currentColor'/%3E%3C/svg%3E";

    internal const string Text = "# Heading 1\n\n## Heading 2\n\n### Heading 3\n\n#### Heading 4\n\n##### Heading 5\n\n###### Heading 6\n\n"
        + "Plain text with **bold**, *italic*, ***both***, ~~strikethrough~~, ==highlight==, ++inserted++, H~2~O, x^2^ and `inline code`. A soft\nbreak and a hard  \nbreak. Emoji: :smile: :rocket:. Raw <b>HTML</b> shows as text.\n\n"
        + "Links: [a link](https://example.com), <https://example.com/autolink>, https://example.com/bare and <someone@example.com>.\n\n"
        + "---\n\n"
        + "> A quote with a [link](https://example.com).\n>\n> > And one inside it.\n\n"
        + "> [!NOTE]\n> A note.\n\n> [!TIP]\n> A tip.\n\n> [!IMPORTANT]\n> Something important.\n\n> [!WARNING]\n> A warning.\n\n> [!CAUTION]\n> A caution.\n\n"
        + ":::info\nA container with **blocks** in it.\n:::\n\n"
        + "1. First\n2. Second\n   - Nested\n     - Deeper\n3. Third, long enough to wrap so the lines stay beside the marker and not under it, whatever the width of the window is.\n\n"
        + "- [x] A finished task\n- [ ] An open task\n\n"
        + "```csharp\nvar total = items.Sum(item => item.Price * item.Quantity); // a long line scrolls sideways, and the page still scrolls up and down\nConsole.WriteLine(total);\n```\n\n"
        + "    An indented code block\n\n"
        + "| Left | Center | Right |\n|:-----|:------:|------:|\n| a | b | c |\n| **bold** | `code` | [link](https://example.com) |\n\n"
        + "+------+------+\n| Grid | Table|\n+======+======+\n| spans two   |\n+------+------+\n| - a  | text |\n| - b  |      |\n+------+------+\n\n"
        + "Text with a footnote[^1] and a formula $E = mc^2$ in the line.\n\n"
        + "$$\n\\frac{-b \\pm \\sqrt{b^2 - 4ac}}{2a}\n$$\n\n"
        + "Term\n:   Its definition, indented.\n\n"
        + "A picture with a set size (tap it):\n\n![A red square](" + Dot + "){width=120 height=120}\n\n"
        + "Badges in a row:\n\n![one](" + Dot + "){width=48 height=48} ![two](" + Dot + "){width=48 height=48} ![three](" + Icon + ")\n\n"
        + "Remote images load only when allowed, and files never:\n\n![A remote image](https://example.com/picture.png) ![A local file](/etc/passwd)\n\n"
        + "[^1]: The footnote, at the end of the document.\n";
}
