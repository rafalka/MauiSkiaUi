namespace MauiSkiaUi.Markdown;

/// <summary>
/// How a Markdown view draws a document: an immutable record, set once and shared between views. A <c>null</c> value comes from
/// <see cref="SkUiColorScheme"/> and the look. Copy <see cref="Default"/> with <c>with</c> to change a part:
/// <c>SkUiMarkdownStyle.Default with { CodeFontSize = 13 }</c>.
/// </summary>
public sealed record SkUiMarkdownStyle
{
    /// <summary>The default style: colors from the scheme, sizes from the look.</summary>
    public static SkUiMarkdownStyle Default { get; } = new();

    /// <summary>Body font family; <c>null</c>: the look's.</summary>
    public string? FontFamily { get; init; }

    /// <summary>Body font size in DIPs; <c>null</c>: the look's. Heading sizes are multiples of it.</summary>
    public double? FontSize { get; init; }

    /// <summary>Body text color; <c>null</c>: the scheme's foreground.</summary>
    public Color? TextColor { get; init; }

    /// <summary>Multiplier of the body's line spacing; <c>null</c>: the font's.</summary>
    public double? LineHeight { get; init; }

    /// <summary>Size of a level 1 heading, as a multiple of the body size.</summary>
    public double Heading1Scale { get; init; } = 1.9;

    /// <summary>Size of a level 2 heading, as a multiple of the body size.</summary>
    public double Heading2Scale { get; init; } = 1.55;

    /// <summary>Size of a level 3 heading, as a multiple of the body size.</summary>
    public double Heading3Scale { get; init; } = 1.3;

    /// <summary>Size of a level 4 heading, as a multiple of the body size.</summary>
    public double Heading4Scale { get; init; } = 1.15;

    /// <summary>Size of a level 5 heading, as a multiple of the body size.</summary>
    public double Heading5Scale { get; init; } = 1;

    /// <summary>Size of a level 6 heading, as a multiple of the body size.</summary>
    public double Heading6Scale { get; init; } = 0.9;

    /// <summary>Heading font attributes.</summary>
    public FontAttributes HeadingFontAttributes { get; init; } = FontAttributes.Bold;

    /// <summary>Heading color; <c>null</c>: the text color.</summary>
    public Color? HeadingColor { get; init; }

    /// <summary>Extra space above a heading that follows another block, in DIPs.</summary>
    public double HeadingTopSpacing { get; init; } = 8;

    /// <summary>Space between blocks, in DIPs.</summary>
    public double BlockSpacing { get; init; } = 12;

    /// <summary>Space between list items, in DIPs.</summary>
    public double ListItemSpacing { get; init; } = 4;

    /// <summary>Space between a list marker and its text, in DIPs.</summary>
    public double ListMarkerSpacing { get; init; } = 8;

    /// <summary>List marker color; <c>null</c>: the text color.</summary>
    public Color? ListMarkerColor { get; init; }

    /// <summary>Link color; <c>null</c>: the scheme's accent.</summary>
    public Color? LinkColor { get; init; }

    /// <summary>Link decorations.</summary>
    public TextDecorations LinkDecorations { get; init; } = TextDecorations.Underline;

    /// <summary>Background of highlighted text (<c>==text==</c>); <c>null</c>: a translucent yellow.</summary>
    public Color? HighlightColor { get; init; }

    /// <summary>Code font family; <c>null</c>: the platform's monospace family (Menlo, <c>monospace</c>, Consolas).</summary>
    public string? CodeFontFamily { get; init; }

    /// <summary>Code font size in DIPs; <c>null</c>: 90 % of the text around it.</summary>
    public double? CodeFontSize { get; init; }

    /// <summary>Code text color; <c>null</c>: the text color.</summary>
    public Color? CodeTextColor { get; init; }

    /// <summary>Background of inline code and code blocks; <c>null</c>: a translucent foreground.</summary>
    public Color? CodeBackgroundColor { get; init; }

    /// <summary>Padding inside a code block, in DIPs.</summary>
    public Thickness CodePadding { get; init; } = new(12);

    /// <summary>Corner radius of code blocks, quotes, alerts and containers, in DIPs.</summary>
    public double CornerRadius { get; init; } = 6;

    /// <summary>Whether code blocks show a button that copies the code.</summary>
    public bool ShowCodeCopyButton { get; init; } = true;

    /// <summary>The copy button's text.</summary>
    public string CodeCopyButtonText { get; init; } = "Copy";

    /// <summary>Quote bar color; <c>null</c>: the scheme's muted color.</summary>
    public Color? QuoteBarColor { get; init; }

    /// <summary>Width of the bar of quotes and alerts, on the text's start side, in DIPs.</summary>
    public double QuoteBarWidth { get; init; } = 3;

    /// <summary>Quote text color; <c>null</c>: the scheme's muted color.</summary>
    public Color? QuoteTextColor { get; init; }

    /// <summary>Quote background; <c>null</c>: none.</summary>
    public Color? QuoteBackgroundColor { get; init; }

    /// <summary>Title of a <c>[!NOTE]</c> alert.</summary>
    public string AlertNoteTitle { get; init; } = "Note";

    /// <summary>Title of a <c>[!TIP]</c> alert.</summary>
    public string AlertTipTitle { get; init; } = "Tip";

    /// <summary>Title of a <c>[!IMPORTANT]</c> alert.</summary>
    public string AlertImportantTitle { get; init; } = "Important";

    /// <summary>Title of a <c>[!WARNING]</c> alert.</summary>
    public string AlertWarningTitle { get; init; } = "Warning";

    /// <summary>Title of a <c>[!CAUTION]</c> alert.</summary>
    public string AlertCautionTitle { get; init; } = "Caution";

    /// <summary>Color of a <c>[!NOTE]</c> alert.</summary>
    public Color AlertNoteColor { get; init; } = Color.FromRgb(0x09, 0x69, 0xDA);

    /// <summary>Color of a <c>[!TIP]</c> alert.</summary>
    public Color AlertTipColor { get; init; } = Color.FromRgb(0x1A, 0x7F, 0x37);

    /// <summary>Color of a <c>[!IMPORTANT]</c> alert.</summary>
    public Color AlertImportantColor { get; init; } = Color.FromRgb(0x82, 0x50, 0xDF);

    /// <summary>Color of a <c>[!WARNING]</c> alert.</summary>
    public Color AlertWarningColor { get; init; } = Color.FromRgb(0x9A, 0x67, 0x00);

    /// <summary>Color of a <c>[!CAUTION]</c> alert.</summary>
    public Color AlertCautionColor { get; init; } = Color.FromRgb(0xCF, 0x22, 0x2E);

    /// <summary>Background of containers (<c>:::</c>); <c>null</c>: a translucent foreground.</summary>
    public Color? ContainerBackgroundColor { get; init; }

    /// <summary>Color of table separators and frames; <c>null</c>: the scheme's muted color.</summary>
    public Color? TableBorderColor { get; init; }

    /// <summary>Background of table headers; <c>null</c>: a translucent foreground.</summary>
    public Color? TableHeaderBackgroundColor { get; init; }

    /// <summary>Padding inside table cells, in DIPs.</summary>
    public Thickness TableCellPadding { get; init; } = new(8, 4);

    /// <summary>Text in a table wraps at this width, in DIPs; a table wider than the view scrolls.</summary>
    public double TableMaximumColumnWidth { get; init; } = 320;

    /// <summary>Color of rules (<c>---</c>); <c>null</c>: the scheme's muted color.</summary>
    public Color? RuleColor { get; init; }

    /// <summary>Thickness of rules, in DIPs.</summary>
    public double RuleThickness { get; init; } = 1;

    /// <summary>What a picture shows when it cannot and has no alt text.</summary>
    public string ImagePlaceholderText { get; init; } = "Image";

    /// <summary>Above this many characters the document is cut there and shows as plain text.</summary>
    public int MaximumInputLength { get; init; } = 1_000_000;

    /// <summary>Above this many blocks in the document (in all, nested ones included), it shows as plain text.</summary>
    public int MaximumBlocks { get; init; } = 20_000;

    /// <summary>Blocks nested deeper than this make the document show as plain text.</summary>
    public int MaximumBlockDepth { get; init; } = 32;

    /// <summary>Inline text nested deeper than this (emphasis in emphasis) makes the document show as plain text.</summary>
    public int MaximumInlineDepth { get; init; } = 64;

    /// <summary>A table with more cells than this shows as its source.</summary>
    public int MaximumTableCells { get; init; } = 2_000;

    /// <summary>Images after this many in a document show their alt text.</summary>
    public int MaximumImages { get; init; } = 100;

    /// <summary>
    /// Up to this many characters a document is parsed and built on the thread that sets the text, so the first frame has its content. Default 0: never, because parsing
    /// must not hold up the UI thread; the previous content stays until the build is done. <see cref="int.MaxValue"/>: always, for text the app trusts and wants at once.
    /// </summary>
    public int MaximumInPlaceParseLength { get; init; }

    /// <summary>The name of a value no document can draw with, or <c>null</c> when all of them are fine: sizes finite and not negative, multiples and font sizes above 0, limits not negative.</summary>
    internal string? FindInvalidValue()
    {
        static bool IsLength(double value) => double.IsFinite(value) && value >= 0;
        static bool IsSize(double value) => double.IsFinite(value) && value > 0;
        static bool IsThickness(Thickness value) => IsLength(value.Left) && IsLength(value.Top) && IsLength(value.Right) && IsLength(value.Bottom);

        if (FontSize is { } fontSize && !IsSize(fontSize)) return nameof(FontSize);
        if (CodeFontSize is { } codeFontSize && !IsSize(codeFontSize)) return nameof(CodeFontSize);
        if (LineHeight is { } lineHeight && !IsSize(lineHeight)) return nameof(LineHeight);
        if (!IsSize(Heading1Scale)) return nameof(Heading1Scale);
        if (!IsSize(Heading2Scale)) return nameof(Heading2Scale);
        if (!IsSize(Heading3Scale)) return nameof(Heading3Scale);
        if (!IsSize(Heading4Scale)) return nameof(Heading4Scale);
        if (!IsSize(Heading5Scale)) return nameof(Heading5Scale);
        if (!IsSize(Heading6Scale)) return nameof(Heading6Scale);
        if (!IsLength(HeadingTopSpacing)) return nameof(HeadingTopSpacing);
        if (!IsLength(BlockSpacing)) return nameof(BlockSpacing);
        if (!IsLength(ListItemSpacing)) return nameof(ListItemSpacing);
        if (!IsLength(ListMarkerSpacing)) return nameof(ListMarkerSpacing);
        if (!IsLength(CornerRadius)) return nameof(CornerRadius);
        if (!IsLength(QuoteBarWidth)) return nameof(QuoteBarWidth);
        if (!IsSize(TableMaximumColumnWidth)) return nameof(TableMaximumColumnWidth);
        if (!IsLength(RuleThickness)) return nameof(RuleThickness);
        if (!IsThickness(CodePadding)) return nameof(CodePadding);
        if (!IsThickness(TableCellPadding)) return nameof(TableCellPadding);
        if (MaximumInputLength < 0) return nameof(MaximumInputLength);
        if (MaximumBlocks < 0) return nameof(MaximumBlocks);
        if (MaximumBlockDepth < 0) return nameof(MaximumBlockDepth);
        if (MaximumInlineDepth < 0) return nameof(MaximumInlineDepth);
        if (MaximumTableCells < 0) return nameof(MaximumTableCells);
        if (MaximumImages < 0) return nameof(MaximumImages);
        return MaximumInPlaceParseLength < 0 ? nameof(MaximumInPlaceParseLength) : null;
    }
}
