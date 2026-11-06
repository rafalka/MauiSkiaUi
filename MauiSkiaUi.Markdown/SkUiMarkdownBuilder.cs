using System.Globalization;
using System.Text;
using Markdig.Extensions.Alerts;
using Markdig.Extensions.CustomContainers;
using Markdig.Extensions.DefinitionLists;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MauiSkiaUi.Core;

namespace MauiSkiaUi.Markdown;

/// <summary>
/// Walks a syntax tree and creates Core nodes, on any thread: they are in no tree yet, and what needs the UI thread waits in <see cref="UiActions"/>. Recursion is capped (<see cref="SkUiMarkdownStyle.MaximumBlockDepth"/>,
/// <see cref="SkUiMarkdownStyle.MaximumInlineDepth"/>): Markdig lets inlines nest 10,240 deep, far beyond what a stack should hold here.
/// </summary>
internal sealed class SkUiMarkdownBuilder
{
    private const string AlertNote = "NOTE";
    private const string AlertTip = "TIP";
    private const string AlertImportant = "IMPORTANT";
    private const string AlertWarning = "WARNING";
    private const string AlertCaution = "CAUTION";
    private const string ListBullet = "•";
    private const string ListBulletNested = "◦";
    private const string ListBulletDeep = "▪";
    private const double CodeFontScale = 0.9;
    private const double SubSuperscriptScale = 0.7;
    private const double FootnoteMarkScale = 0.75;
    private const double MaximumImageSide = 10_000;
    private const double DefinitionIndent = 24;
    private const double QuoteContentSpacing = 12;
    private const double ContainerPadding = 12;
    private const double CopyButtonClearance = 64;
    private const double ScrollBarClearance = 6;

    private readonly SkUiCoreMarkdown _owner;
    private readonly SkUiMarkdownBuildSettings _settings;
    private readonly SkUiMarkdownStyle _style;
    private string _source;
    private readonly double _bodySize;
    private readonly Color _textColor;
    private readonly Color _mutedColor;
    private readonly Color _linkColor;
    private readonly Color _codeBackground;
    private readonly Color _tint;

    private const double SizeScaleTolerance = 0.001;

    /// <summary>Footnotes, as a multiple of the body size.</summary>
    internal const double FootnoteScale = 0.9;

    internal SkUiMarkdownBuilder(SkUiCoreMarkdown owner, SkUiMarkdownBuildSettings settings, string source, int blockCount, int imageCount)
    {
        _owner = owner;
        _settings = settings;
        var style = _style = settings.Style;
        _source = source;
        BlockCount = blockCount;
        ImageCount = imageCount;
        var scheme = SkUiColorScheme.Current;
        _bodySize = style.FontSize ?? SkUiLook.Current.DefaultFontSize;
        _textColor = style.TextColor ?? scheme.DefaultForeground;
        _mutedColor = scheme.Muted;
        _linkColor = style.LinkColor ?? scheme.Accent;
        _tint = _textColor.WithAlpha(0.07f);
        _codeBackground = style.CodeBackgroundColor ?? _tint;
    }

    /// <summary>A build is done: what stays on screen can outlive it (an image block keeps a way to make its alt text), and must not keep the document's text.</summary>
    internal void ReleaseSource() => _source = string.Empty;

    /// <summary>What must run on the UI thread before the nodes go on screen.</summary>
    internal List<Action> UiActions { get; } = [];

    /// <summary>What went wrong along the way, for the UI thread to report.</summary>
    internal List<string> Warnings { get; } = [];

    /// <summary>Blocks built so far, with those of reused blocks.</summary>
    internal int BlockCount { get; private set; }

    /// <summary>Images seen so far, with those of reused blocks.</summary>
    internal int ImageCount { get; private set; }

    /// <summary>What blocks inherit from those they are in.</summary>
    private readonly record struct BlockContext(int Depth, Color TextColor, FontAttributes Attributes = FontAttributes.None, TextAlignment Alignment = TextAlignment.Start, double SizeScale = 1,
        int ListDepth = 0, bool InTable = false)
    {
        internal BlockContext Deeper() => this with { Depth = Depth + 1 };
    }

    /// <summary>What a run of text looks like; equal neighbors share a span.</summary>
    private readonly record struct SpanLook(FontAttributes Attributes, TextDecorations Decorations = TextDecorations.None, Color? TextColor = null, Color? Background = null,
        double? FontSize = null, string? FontFamily = null, double BaselineShift = 0, string? Link = null);

    /// <summary>Nodes for a block of the document's top level.</summary>
    internal List<SkUiCoreNode> Build(Block block, bool isFirst)
    {
        var output = new List<SkUiCoreNode>();
        AddBlock(block, new BlockContext(0, _textColor), output, isFirst);
        return output;
    }

    /// <summary>A plain label for a document the builder does not take: all its text, none of its marks.</summary>
    internal SkUiCoreLabel PlainLabel(string text)
    {
        var label = NewLabel(new BlockContext(0, _textColor));
        label.SetText(text);
        return label;
    }

    private void CountBlock()
    {
        if (++BlockCount > _style.MaximumBlocks)
            throw new SkUiMarkdownLimitException("The document has more blocks than MaximumBlocks.");
    }

    private void AddBlock(Block block, in BlockContext context, List<SkUiCoreNode> output, bool isFirst)
    {
        CountBlock();
        if (context.Depth > _style.MaximumBlockDepth)
            throw new SkUiMarkdownLimitException("Blocks nest deeper than MaximumBlockDepth.");
        switch (block)
        {
            case LinkReferenceDefinitionGroup or LinkReferenceDefinition or BlankLineBlock:
                break;
            case MathBlock math:
                output.Add(BuildBlockMath(math, context));
                break;
            case CodeBlock code:
                output.Add(BuildCode(code, context));
                break;
            case HeadingBlock heading:
                AddHeading(heading, context, output, isFirst);
                break;
            case ParagraphBlock paragraph:
                AddParagraph(paragraph, context, output);
                break;
            case ThematicBreakBlock:
                output.Add(BuildRule());
                break;
            case AlertBlock alert:
                output.Add(BuildAlert(alert, context));
                break;
            case QuoteBlock quote:
                output.Add(BuildQuote(quote, context));
                break;
            case ListBlock list:
                output.Add(BuildList(list, context));
                break;
            case Table table:
                output.Add(BuildTable(table, context));
                break;
            case FootnoteGroup footnotes:
                AddFootnotes(footnotes, context, output);
                break;
            case DefinitionList definitions:
                output.Add(BuildDefinitions(definitions, context));
                break;
            case CustomContainer container:
                output.Add(BuildContainer(container, context));
                break;
            case ContainerBlock children:
                AddChildren(children, context.Deeper(), output);
                break;
            default:
                output.Add(BuildSource(block, context));
                break;
        }
    }

    private void AddChildren(ContainerBlock container, in BlockContext context, List<SkUiCoreNode> output)
    {
        for (var index = 0; index < container.Count; index++)
            AddBlock(container[index], context, output, index == 0);
    }

    /// <summary>A container's blocks as one node.</summary>
    private SkUiCoreNode BuildBlocks(ContainerBlock container, in BlockContext context, double spacing)
    {
        var nodes = new List<SkUiCoreNode>(container.Count);
        AddChildren(container, context.Deeper(), nodes);
        return Stack(nodes, spacing);
    }

    private static SkUiCoreNode Stack(List<SkUiCoreNode> nodes, double spacing)
    {
        if (nodes.Count == 1)
            return nodes[0];
        var stack = new SkUiCoreVerticalStackLayout().SetSpacing(spacing);
        foreach (var node in nodes)
            stack.Add(node);
        return stack;
    }

    private SkUiCoreLabel NewLabel(in BlockContext context)
    {
        var label = new SkUiCoreLabel();
        label.SetTextColor(context.TextColor);
        label.SetLineBreakMode(LineBreakMode.WordWrap);
        if (_style.FontFamily is { } family)
            label.SetFontFamily(family);
        if (_style.FontSize is not null || Math.Abs(context.SizeScale - 1) > SizeScaleTolerance)
            label.SetFontSize(_bodySize * context.SizeScale);
        if (_style.LineHeight is { } lineHeight)
            label.SetLineHeight(lineHeight);
        if (context.Attributes != FontAttributes.None)
            label.SetFontAttributes(context.Attributes);
        if (context.Alignment != TextAlignment.Start)
            label.SetHorizontalTextAlignment(context.Alignment);
        return label;
    }

    private void AddHeading(HeadingBlock heading, in BlockContext context, List<SkUiCoreNode> output, bool isFirst)
    {
        var scale = heading.Level switch
        {
            1 => _style.Heading1Scale,
            2 => _style.Heading2Scale,
            3 => _style.Heading3Scale,
            4 => _style.Heading4Scale,
            5 => _style.Heading5Scale,
            _ => _style.Heading6Scale
        };
        var level = heading.Level switch
        {
            1 => SemanticHeadingLevel.Level1,
            2 => SemanticHeadingLevel.Level2,
            3 => SemanticHeadingLevel.Level3,
            4 => SemanticHeadingLevel.Level4,
            5 => SemanticHeadingLevel.Level5,
            _ => SemanticHeadingLevel.Level6
        };
        var inner = context with { TextColor = _style.HeadingColor ?? context.TextColor, Attributes = _style.HeadingFontAttributes, SizeScale = scale };
        var start = output.Count;
        AddInlines(heading.Inline, inner, output);
        for (var index = start; index < output.Count; index++)
        {
            output[index].SetSemanticHeadingLevel(level);
            if (index == start && !isFirst && _style.HeadingTopSpacing > 0)
                output[index].SetMargin(new Thickness(0, _style.HeadingTopSpacing, 0, 0));
        }
    }

    private void AddParagraph(ParagraphBlock paragraph, in BlockContext context, List<SkUiCoreNode> output)
    {
        if (paragraph.Inline is { } inline && TryAddImageRow(inline, context, output))
            return;
        AddInlines(paragraph.Inline, context, output);
    }

    /// <summary>A paragraph of nothing but images, a row of badges: they flow and wrap.</summary>
    private bool TryAddImageRow(ContainerInline inline, in BlockContext context, List<SkUiCoreNode> output)
    {
        var images = new List<(LinkInline Image, string? Link)>();
        for (var child = inline.FirstChild; child is not null; child = child.NextSibling)
        {
            switch (child)
            {
                case LinkInline { IsImage: true } image:
                    images.Add((image, null));
                    break;
                case LinkInline { FirstChild: LinkInline { IsImage: true } image, FirstChild.NextSibling: null } link:
                    images.Add((image, link.Url));
                    break;
                case LineBreakInline:
                    break;
                case LiteralInline literal when literal.Content.IsEmptyOrWhitespace():
                    break;
                default:
                    return false;
            }
        }
        if (images.Count < 2)
            return false;
        var row = new SkUiCoreWrapLayout().SetSpacing(4).SetRowSpacing(4);
        foreach (var (image, link) in images)
        {
            row.Add(BuildImage(image, link, context));
        }
        output.Add(row);
        return true;
    }

    private void AddInlines(ContainerInline? inline, in BlockContext context, List<SkUiCoreNode> output)
    {
        if (inline is null)
            return;
        var run = new InlineRun(this, context, output);
        Walk(inline, new SpanLook(context.Attributes), 0, run);
        run.Finish();
    }

    private void Walk(ContainerInline container, SpanLook look, int depth, InlineRun run)
    {
        if (depth > _style.MaximumInlineDepth)
            throw new SkUiMarkdownLimitException("Inlines nest deeper than MaximumInlineDepth.");
        for (var inline = container.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    run.Append(literal.Content.ToString(), look);
                    break;
                case CodeInline code:
                    run.Append(code.Content, CodeLook(look, run.Context));
                    break;
                case EmphasisInline emphasis:
                    Walk(emphasis, Emphasized(emphasis, look, run.Context), depth + 1, run);
                    break;
                case LinkInline { IsImage: true } image:
                    run.AddBlock(BuildImage(image, look.Link, run.Context));
                    break;
                case LinkInline link:
                    Walk(link, LinkLook(look, link.GetDynamicUrl?.Invoke() ?? link.Url ?? string.Empty), depth + 1, run);
                    break;
                case AutolinkInline autolink:
                    run.Append(autolink.Url, LinkLook(look, autolink.IsEmail ? "mailto:" + autolink.Url : autolink.Url));
                    break;
                case LineBreakInline lineBreak:
                    run.Append(lineBreak.IsHard ? "\n" : " ", look);
                    break;
                case HtmlEntityInline entity:
                    run.Append(entity.Transcoded.ToString(), look);
                    break;
                case HtmlInline html:
                    run.Append(html.Tag, look);
                    break;
                case MathInline math:
                    AddInlineMath(math, look, run);
                    break;
                case TaskList:
                    run.TrimNextText();
                    break;
                case FootnoteLink footnote:
                    if (!footnote.IsBackLink)
                        run.Append("[" + footnote.Footnote.Order.ToString(CultureInfo.InvariantCulture) + "]", look with
                        {
                            FontSize = LookSize(look, run.Context) * FootnoteMarkScale, BaselineShift = SkUiSpan.SuperscriptShift
                        });
                    break;
                case DelimiterInline delimiter:
                    // A delimiter that closed nothing (`[`) is text, followed by what it holds.
                    run.Append(delimiter.ToLiteral(), look);
                    Walk(delimiter, look, depth + 1, run);
                    break;
                case ContainerInline children:
                    Walk(children, look, depth + 1, run);
                    break;
                default:
                    run.Append(Source(inline.Span), look);
                    break;
            }
        }
    }

    private double LookSize(in SpanLook look, in BlockContext context) => look.FontSize ?? _bodySize * context.SizeScale;

    private SpanLook Emphasized(EmphasisInline emphasis, SpanLook look, in BlockContext context)
    {
        switch (emphasis.DelimiterChar)
        {
            case '*' or '_':
                return look with { Attributes = look.Attributes | (emphasis.DelimiterCount >= 2 ? FontAttributes.Bold : FontAttributes.Italic) };
            case '~' when emphasis.DelimiterCount >= 2:
                return look with { Decorations = look.Decorations | TextDecorations.Strikethrough };
            case '~':
                return look with { FontSize = LookSize(look, context) * SubSuperscriptScale, BaselineShift = SkUiSpan.SubscriptShift };
            case '^':
                return look with { FontSize = LookSize(look, context) * SubSuperscriptScale, BaselineShift = SkUiSpan.SuperscriptShift };
            case '+':
                return look with { Decorations = look.Decorations | TextDecorations.Underline };
            case '-':
                return look with { Decorations = look.Decorations | TextDecorations.Strikethrough };
            case '=':
                return look with { Background = _style.HighlightColor ?? Color.FromRgba(255, 214, 0, 96) };
            default:
                return look;
        }
    }

    private SpanLook CodeLook(in SpanLook look, in BlockContext context) => look with
    {
        FontFamily = CodeFamily,
        FontSize = _style.CodeFontSize ?? LookSize(look, context) * CodeFontScale,
        TextColor = _style.CodeTextColor ?? look.TextColor,
        Background = _codeBackground
    };

    private SpanLook LinkLook(in SpanLook look, string url) => look with
    {
        TextColor = _linkColor,
        Decorations = look.Decorations | _style.LinkDecorations,
        Link = url
    };

    private string CodeFamily => _style.CodeFontFamily ?? DefaultCodeFamily;

    private static string DefaultCodeFamily =>
        OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst() || OperatingSystem.IsMacOS() ? "Menlo"
        : OperatingSystem.IsWindows() ? "Consolas"
        : "monospace";

    private SkUiCoreSpan CreateSpan(string text, in SpanLook look, in BlockContext context)
    {
        var span = new SkUiCoreSpan(text);
        if (look.Attributes != context.Attributes)
            span.SetFontAttributes(look.Attributes);
        if (look.Decorations != TextDecorations.None)
            span.SetTextDecorations(look.Decorations);
        if (look.TextColor is { } color)
            span.SetTextColor(color);
        if (look.Background is { } background)
            span.SetBackgroundColor(background);
        if (look.FontSize is { } size)
            span.SetFontSize(size);
        if (look.FontFamily is { } family)
            span.SetFontFamily(family);
        if (look.BaselineShift != 0)
            span.SetBaselineShift(look.BaselineShift);
        if (look.Link is { } link)
        {
            var owner = _owner;
            span.Tapped += (_, args) => owner.OnLinkTapped(link, args.Position);
        }
        return span;
    }

    /// <summary>Turns a block's inlines into labels, cut where an image stands on its own.</summary>
    private sealed class InlineRun(SkUiMarkdownBuilder builder, BlockContext context, List<SkUiCoreNode> output)
    {
        private readonly List<SkUiCoreSpan> _spans = [];
        private readonly StringBuilder _text = new();
        private SpanLook _look;
        private bool _hasText;
        private bool _trimNext;

        internal BlockContext Context => context;

        internal void TrimNextText() => _trimNext = true;

        internal void Append(string? text, in SpanLook look)
        {
            if (string.IsNullOrEmpty(text))
                return;
            if (_trimNext)
            {
                text = text.TrimStart();
                _trimNext = false;
                if (text.Length == 0)
                    return;
            }
            if (!_hasText || look != _look)
            {
                Flush();
                _look = look;
                _hasText = true;
            }
            _text.Append(text);
        }

        internal void AppendSpan(SkUiCoreSpan span)
        {
            Flush();
            _spans.Add(span);
        }

        private void Flush()
        {
            if (_hasText && _text.Length > 0)
                _spans.Add(builder.CreateSpan(_text.ToString(), _look, context));
            _text.Clear();
            _hasText = false;
        }

        internal void AddBlock(SkUiCoreNode node)
        {
            Finish();
            output.Add(node);
        }

        internal void Finish()
        {
            Flush();
            if (_spans.Count == 0)
                return;
            var label = builder.NewLabel(context);
            var hasContent = false;
            foreach (var span in _spans)
                hasContent |= span.InlineObject is not null || !string.IsNullOrWhiteSpace(span.Text);
            if (hasContent)
            {
                label.SetSpans(_spans);
                output.Add(label);
            }
            _spans.Clear();
        }
    }

    private SkUiCoreNode BuildRule()
    {
        var rule = new SkUiMarkdownBar();
        rule.SetColor(_style.RuleColor ?? _mutedColor);
        rule.SetHeight(_style.RuleThickness);
        rule.SetIsInAccessibleTree(false);
        return rule;
    }

    private SkUiCoreNode BuildQuote(QuoteBlock quote, in BlockContext context)
    {
        var inner = context with { TextColor = _style.QuoteTextColor ?? _mutedColor };
        return BuildBarBlock(_style.QuoteBarColor ?? _mutedColor, _style.QuoteBackgroundColor, BuildBlocks(quote, inner, _style.BlockSpacing), context);
    }

    private SkUiCoreNode BuildAlert(AlertBlock alert, in BlockContext context)
    {
        var kind = alert.Kind.ToString().ToUpperInvariant();
        var (title, color) = kind switch
        {
            AlertNote => (_style.AlertNoteTitle, _style.AlertNoteColor),
            AlertTip => (_style.AlertTipTitle, _style.AlertTipColor),
            AlertImportant => (_style.AlertImportantTitle, _style.AlertImportantColor),
            AlertWarning => (_style.AlertWarningTitle, _style.AlertWarningColor),
            AlertCaution => (_style.AlertCautionTitle, _style.AlertCautionColor),
            _ => (alert.Kind.ToString(), _style.AlertNoteColor)
        };
        var heading = NewLabel(context with { TextColor = color, Attributes = FontAttributes.Bold });
        heading.SetText(title);
        var nodes = new List<SkUiCoreNode> { heading };
        AddChildren(alert, context.Deeper(), nodes);
        var content = Stack(nodes, _style.ListItemSpacing);
        return BuildBarBlock(color, color.WithAlpha(0.08f), content, context);
    }

    /// <summary>A bar on the text's start side and content beside it: the grid mirrors with the flow direction.</summary>
    private SkUiCoreNode BuildBarBlock(Color barColor, Color? background, SkUiCoreNode content, in BlockContext context)
    {
        if (context.InTable)
            content.SetMaximumWidth(_style.TableMaximumColumnWidth);
        var bar = new SkUiMarkdownBar();
        bar.SetColor(barColor);
        bar.SetIsInAccessibleTree(false);
        var hasBackground = background is not null;
        content.SetMargin(new Thickness(QuoteContentSpacing, hasBackground ? 8 : 0, hasBackground ? 8 : 0, hasBackground ? 8 : 0));
        var grid = new SkUiCoreGrid();
        grid.SetRowDefinitions([new SkUiCoreRowDefinition(SkUiCoreGridLength.Auto)]);
        grid.SetColumnDefinitions([new SkUiCoreColumnDefinition(new SkUiCoreGridLength(_style.QuoteBarWidth)), new SkUiCoreColumnDefinition(ContentColumn(context))]);
        grid.Add(bar, 0, 0);
        grid.Add(content, 0, 1);
        if (background is not { } fill)
            return grid;
        var border = new SkUiCoreBorder().SetBackgroundColor(fill).SetStrokeThickness(0).SetCornerRadius(_style.CornerRadius);
        border.SetContent(grid);
        return border;
    }

    private SkUiCoreNode BuildContainer(CustomContainer container, in BlockContext context)
    {
        var nodes = new List<SkUiCoreNode>();
        if (!string.IsNullOrWhiteSpace(container.Info))
        {
            var title = NewLabel(context with { Attributes = FontAttributes.Bold });
            title.SetText(string.IsNullOrWhiteSpace(container.Arguments) ? container.Info : container.Info + " " + container.Arguments);
            nodes.Add(title);
        }
        AddChildren(container, context.Deeper(), nodes);
        var border = new SkUiCoreBorder()
            .SetBackgroundColor(_style.ContainerBackgroundColor ?? _tint)
            .SetStrokeThickness(0)
            .SetCornerRadius(_style.CornerRadius)
            .SetPadding(new Thickness(ContainerPadding));
        border.SetContent(Stack(nodes, _style.BlockSpacing));
        return border;
    }

    private SkUiCoreNode BuildList(ListBlock list, in BlockContext context)
    {
        var grid = new SkUiCoreGrid();
        grid.SetColumnDefinitions([new SkUiCoreColumnDefinition(SkUiCoreGridLength.Auto), new SkUiCoreColumnDefinition(ContentColumn(context))]);
        grid.SetColumnSpacing(_style.ListMarkerSpacing);
        grid.SetRowSpacing(list.IsLoose ? _style.BlockSpacing : _style.ListItemSpacing);
        var number = list.IsOrdered && long.TryParse(list.OrderedStart, NumberStyles.None, CultureInfo.InvariantCulture, out var start) ? start : 1;
        var bullet = context.ListDepth switch
        {
            0 => ListBullet,
            1 => ListBulletNested,
            _ => ListBulletDeep
        };
        var row = 0;
        foreach (var block in list)
        {
            if (block is not ListItemBlock item)
                continue;
            CountBlock();
            grid.AddRowDefinition(new SkUiCoreRowDefinition(SkUiCoreGridLength.Auto));
            var marker = TaskStateOf(item) is { } isChecked
                ? BuildTaskMarker(isChecked, item)
                : BuildMarker(list.IsOrdered ? number.ToString(CultureInfo.InvariantCulture) + (list.OrderedDelimiter == ')' ? ")" : ".") : bullet, context, list.IsOrdered);
            marker.SetVerticalAlignment(LayoutAlignment.Start);
            grid.Add(marker, row, 0);
            var content = BuildBlocks(item, context with { ListDepth = context.ListDepth + 1 }, list.IsLoose ? _style.BlockSpacing : _style.ListItemSpacing);
            if (context.InTable)
                content.SetMaximumWidth(_style.TableMaximumColumnWidth);
            grid.Add(content, row, 1);
            row++;
            number++;
        }
        return grid;
    }

    /// <summary>What fills beside a marker or bar: all the width, except in a table, whose columns are as wide as their text.</summary>
    private static SkUiCoreGridLength ContentColumn(in BlockContext context) => context.InTable ? SkUiCoreGridLength.Auto : SkUiCoreGridLength.Star;

    private static bool? TaskStateOf(ListItemBlock item) =>
        item.Count > 0 && item[0] is ParagraphBlock { Inline.FirstChild: TaskList task } ? task.Checked : null;

    private SkUiCoreNode BuildMarker(string text, in BlockContext context, bool isOrdered)
    {
        var label = NewLabel(context with { TextColor = _style.ListMarkerColor ?? context.TextColor, Alignment = isOrdered ? TextAlignment.End : TextAlignment.Start });
        label.SetLineBreakMode(LineBreakMode.NoWrap);
        label.SetText(text);
        label.SetIsInAccessibleTree(false);
        return label;
    }

    /// <summary>A task's state, shown and read by screen readers; a document cannot be edited, so it takes no input.</summary>
    private SkUiCoreNode BuildTaskMarker(bool isChecked, ListItemBlock item)
    {
        var box = new SkUiCoreCheckBox();
        if (isChecked)
            UiActions.Add(() => box.SetIsChecked(true)); // a state change starts the box's transition
        box.SetColor(_linkColor);
        box.SetMinimumWidth(0);
        box.SetWidth(_bodySize * 1.2);
        box.SetHeight(_bodySize * 1.2);
        box.SetInputTransparent(true);
        if (item[0] is ParagraphBlock { Inline: { } inline })
            box.SetSemanticDescription(PlainText(inline, 0));
        return box;
    }

    private SkUiCoreNode BuildTable(Table table, in BlockContext context)
    {
        var cellCount = 0;
        foreach (var block in table)
            if (block is TableRow counted)
                cellCount += counted.Count;
        if (cellCount > _style.MaximumTableCells)
            return BuildSource(table, context);

        var grid = new SkUiCoreTable();
        var separator = _style.TableBorderColor ?? _mutedColor;
        grid.SetRowSeparatorColor(separator).SetColumnSeparatorColor(separator);
        var rowCount = 0;
        foreach (var block in table)
            if (block is TableRow)
                rowCount++;
        var taken = new HashSet<(int Row, int Column)>();
        var columnCount = 0;
        var rowIndex = 0;
        var rowDefinitions = new List<SkUiCoreRowDefinition>(rowCount);
        foreach (var block in table)
        {
            if (block is not TableRow row)
                continue;
            rowDefinitions.Add(new SkUiCoreRowDefinition(SkUiCoreGridLength.Auto));
            if (row.IsHeader)
                grid.SetRowBackground(rowIndex, _style.TableHeaderBackgroundColor ?? _tint);
            var next = 0;
            foreach (var cellBlock in row)
            {
                if (cellBlock is not TableCell cell)
                    continue;
                var column = Math.Max(cell.ColumnIndex, next);
                while (taken.Contains((rowIndex, column)))
                    column++;
                var columnSpan = Math.Clamp(cell.ColumnSpan, 1, _style.MaximumTableCells);
                var rowSpan = Math.Clamp(cell.RowSpan, 1, Math.Max(1, rowCount - rowIndex));
                for (var r = 0; r < rowSpan; r++)
                    for (var c = 0; c < columnSpan; c++)
                        taken.Add((rowIndex + r, column + c));
                next = column + columnSpan;
                columnCount = Math.Max(columnCount, next);
                var alignment = column < table.ColumnDefinitions.Count ? AlignmentOf(table.ColumnDefinitions[column].Alignment) : TextAlignment.Start;
                var inner = context with
                {
                    InTable = true,
                    Alignment = alignment,
                    Attributes = row.IsHeader ? context.Attributes | FontAttributes.Bold : context.Attributes
                };
                var content = BuildBlocks(cell, inner, _style.ListItemSpacing);
                content.SetMargin(_style.TableCellPadding);
                content.SetMaximumWidth(_style.TableMaximumColumnWidth * columnSpan);
                grid.Add(content, rowIndex, column, rowSpan, columnSpan);
            }
            rowIndex++;
        }
        grid.SetRowDefinitions(rowDefinitions);
        var columns = new List<SkUiCoreColumnDefinition>(columnCount);
        for (var column = 0; column < columnCount; column++)
            columns.Add(new SkUiCoreColumnDefinition(SkUiCoreGridLength.Auto));
        grid.SetColumnDefinitions(columns);

        var frame = new SkUiCoreBorder().SetStroke(separator).SetStrokeThickness(1).SetCornerRadius(_style.CornerRadius);
        frame.SetContent(grid);
        frame.SetHorizontalAlignment(LayoutAlignment.Start);
        frame.SetMargin(new Thickness(0, 0, 0, ScrollBarClearance));
        return HorizontalScroll(frame);
    }

    private static TextAlignment AlignmentOf(TableColumnAlign? alignment) => alignment switch
    {
        TableColumnAlign.Center => TextAlignment.Center,
        TableColumnAlign.Right => TextAlignment.End,
        _ => TextAlignment.Start
    };

    private static SkUiCoreScrollView HorizontalScroll(SkUiCoreNode content)
    {
        var scroll = new SkUiCoreScrollView().SetOrientation(ScrollOrientation.Horizontal);
        scroll.SetContent(content);
        return scroll;
    }

    private SkUiCoreNode BuildCode(CodeBlock block, in BlockContext context)
    {
        var code = block.Lines.ToString();
        var language = (block as FencedCodeBlock)?.Info;
        var label = BuildCodeLabel(code, language, context);
        // Room inside the scrolled area: for the scroll bar under the text, and so the end of a long line can come out from under the copy button.
        label.SetMargin(new Thickness(0, 0, _style.ShowCodeCopyButton ? CopyButtonClearance : 0, ScrollBarClearance));
        var scroll = HorizontalScroll(label);
        scroll.SetFlowDirection(FlowDirection.LeftToRight); // code reads left to right in any document
        var border = new SkUiCoreBorder()
            .SetBackgroundColor(_codeBackground)
            .SetStrokeThickness(0)
            .SetCornerRadius(_style.CornerRadius)
            .SetPadding(_style.CodePadding);
        border.SetContent(scroll);
        if (!_style.ShowCodeCopyButton)
            return border;

        var button = new SkUiCoreButton();
        button.SetText(_style.CodeCopyButtonText);
        button.SetFontSize(_bodySize * 0.75);
        button.SetMinimumHeight(0);
        button.SetMinimumWidth(0);
        button.SetPadding(new Thickness(8, 2));
        button.SetCornerRadius(4);
        button.SetFillColor(SkUiColorScheme.Current.DefaultBackground); // opaque: long lines scroll under it
        button.SetBorderWidth(0);
        button.SetTextColor(context.TextColor);
        button.SetHorizontalAlignment(LayoutAlignment.End);
        button.SetVerticalAlignment(LayoutAlignment.Start);
        button.SetMargin(new Thickness(4));
        var owner = _owner;
        button.SetClicked(() => owner.CopyCode(code));

        var grid = new SkUiCoreGrid();
        grid.SetFlowDirection(FlowDirection.LeftToRight); // the button stays at the end of the code's first line in any document
        grid.SetRowDefinitions([new SkUiCoreRowDefinition(SkUiCoreGridLength.Auto)]);
        grid.SetColumnDefinitions([new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star)]);
        grid.Add(border, 0, 0);
        grid.Add(button, 0, 0);
        return grid;
    }

    private SkUiCoreLabel BuildCodeLabel(string code, string? language, in BlockContext context)
    {
        var label = NewLabel(context with { TextColor = _style.CodeTextColor ?? context.TextColor, Attributes = FontAttributes.None, Alignment = TextAlignment.Start });
        label.SetFontFamily(CodeFamily);
        label.SetFontSize(_style.CodeFontSize ?? _bodySize * context.SizeScale * CodeFontScale);
        label.SetLineBreakMode(LineBreakMode.NoWrap);
        label.SetTextDirection(SkUiTextDirection.LeftToRight);
        var spans = Highlight(code, language);
        if (spans is { Count: > 0 })
            label.SetSpans(spans);
        else
            label.SetText(code);
        return label;
    }

    /// <summary>A block the builder does not know, or will not build: its source, in the code style. Content is never dropped.</summary>
    private SkUiCoreNode BuildSource(Block block, in BlockContext context)
    {
        var label = BuildCodeLabel(Source(block.Span), null, context);
        return HorizontalScroll(label);
    }

    private string Source(SourceSpan span)
    {
        if (span.Start < 0 || span.Start >= _source.Length || span.End < span.Start)
            return string.Empty;
        return _source.Substring(span.Start, Math.Min(span.Length, _source.Length - span.Start));
    }

    private void AddFootnotes(FootnoteGroup group, in BlockContext context, List<SkUiCoreNode> output)
    {
        if (group.Count == 0)
            return;
        output.Add(BuildRule());
        var grid = new SkUiCoreGrid();
        grid.SetColumnDefinitions([new SkUiCoreColumnDefinition(SkUiCoreGridLength.Auto), new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star)]);
        grid.SetColumnSpacing(_style.ListMarkerSpacing);
        grid.SetRowSpacing(_style.ListItemSpacing);
        var row = 0;
        var inner = context with { SizeScale = FootnoteScale };
        foreach (var block in group)
        {
            if (block is not Footnote footnote)
                continue;
            grid.AddRowDefinition(new SkUiCoreRowDefinition(SkUiCoreGridLength.Auto));
            var marker = BuildMarker(footnote.Order.ToString(CultureInfo.InvariantCulture) + ".", inner, true);
            marker.SetVerticalAlignment(LayoutAlignment.Start);
            grid.Add(marker, row, 0);
            grid.Add(BuildBlocks(footnote, inner, _style.ListItemSpacing), row, 1);
            row++;
        }
        output.Add(grid);
    }

    private SkUiCoreNode BuildDefinitions(DefinitionList list, in BlockContext context)
    {
        var nodes = new List<SkUiCoreNode>();
        foreach (var block in list)
        {
            if (block is not DefinitionItem item)
                continue;
            CountBlock();
            foreach (var part in item)
            {
                if (part is DefinitionTerm term)
                {
                    AddInlines(term.Inline, context with { Attributes = context.Attributes | FontAttributes.Bold }, nodes);
                    continue;
                }
                var start = nodes.Count;
                AddBlock(part, context.Deeper(), nodes, false);
                for (var index = start; index < nodes.Count; index++)
                    nodes[index].SetMargin(new Thickness(DefinitionIndent, 0, 0, 0));
            }
        }
        return Stack(nodes, _style.ListItemSpacing);
    }

    private SkUiCoreNode BuildImage(LinkInline image, string? link, in BlockContext context)
    {
        var url = image.Url ?? string.Empty;
        var alt = PlainText(image, 0);
        var source = ++ImageCount > _style.MaximumImages ? null : ResolveImage(url, context.TextColor);
        var inner = context;
        var attributes = image.TryGetAttributes();
        var owner = _owner;
        SkUiMarkdownImageBlock? block = null;
        block = new SkUiMarkdownImageBlock(source, alt, () => BuildAltLabel(alt, inner), picture => ApplyAttributes(picture, attributes), AlignmentAttribute(attributes, "horizontal"),
            (_, args) =>
            {
                if (link is not null)
                    owner.OnLinkTapped(link, args.Position);
                else
                    owner.OnImageTapped(url, alt, block?.ShowsAltText == false ? source : null);
            });
        block.SetVerticalAlignment(AlignmentAttribute(attributes, "vertical"));
        UiActions.Add(block.Start);
        return block;
    }

    private SkUiCoreLabel BuildAltLabel(string alt, in BlockContext context)
    {
        var label = NewLabel(context with { TextColor = _mutedColor, Attributes = context.Attributes | FontAttributes.Italic });
        label.SetText(string.IsNullOrWhiteSpace(alt) ? _style.ImagePlaceholderText : alt);
        return label;
    }

    /// <summary><c>{width=200 height=100 aspect=AspectFit}</c>: a size given reserves the space, so a load does not move the page.</summary>
    private static void ApplyAttributes(SkUiMarkdownPicture picture, HtmlAttributes? attributes)
    {
        if (attributes?.Properties is not { } properties)
            return;
        foreach (var (key, value) in properties)
        {
            if (value is null)
                continue;
            if (key.Equals("width", StringComparison.OrdinalIgnoreCase) && TryParseSide(value, out var width))
                picture.SetWidth(width);
            else if (key.Equals("height", StringComparison.OrdinalIgnoreCase) && TryParseSide(value, out var height))
                picture.SetHeight(height);
            else if (key.Equals("aspect", StringComparison.OrdinalIgnoreCase) && Enum.TryParse<Aspect>(value, ignoreCase: true, out var aspect) && Enum.IsDefined(aspect))
                picture.SetAspect(aspect);
        }
    }

    private static LayoutAlignment AlignmentAttribute(HtmlAttributes? attributes, string name)
    {
        if (attributes?.Properties is { } properties)
            foreach (var (key, value) in properties)
                if (value is not null && key.Equals(name, StringComparison.OrdinalIgnoreCase) && Enum.TryParse<LayoutAlignment>(value, ignoreCase: true, out var alignment) && Enum.IsDefined(alignment))
                    return alignment;
        return LayoutAlignment.Start;
    }

    private static bool TryParseSide(string value, out double side) =>
        double.TryParse(value.AsSpan().TrimEnd("px"), NumberStyles.Float, CultureInfo.InvariantCulture, out side) && double.IsFinite(side) && side is > 0 and <= MaximumImageSide;

    /// <summary>An inline's text without its marks: an image's alt text, a task's description.</summary>
    private string PlainText(ContainerInline container, int depth)
    {
        if (depth > _style.MaximumInlineDepth)
            throw new SkUiMarkdownLimitException("Inlines nest deeper than MaximumInlineDepth.");
        var text = new StringBuilder();
        for (var inline = container.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    text.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    text.Append(code.Content);
                    break;
                case LineBreakInline:
                    text.Append(' ');
                    break;
                case HtmlEntityInline entity:
                    text.Append(entity.Transcoded.ToString());
                    break;
                case DelimiterInline delimiter:
                    text.Append(delimiter.ToLiteral());
                    text.Append(PlainText(delimiter, depth + 1));
                    break;
                case ContainerInline children:
                    text.Append(PlainText(children, depth + 1));
                    break;
            }
        }
        return text.ToString().Trim();
    }

    private SkUiImageSource? ResolveImage(string url, Color vectorColor)
    {
        try
        {
            return _settings.ImageResolver is { } resolver ? resolver(url) : SkUiMarkdownImages.Resolve(url, _settings.AllowRemoteImages, vectorColor);
        }
        catch (Exception error)
        {
            Warnings.Add("The image resolver failed: " + error.Message);
            return null;
        }
    }

    private IReadOnlyList<SkUiCoreSpan>? Highlight(string code, string? language)
    {
        if (_settings.CodeHighlighter is not { } highlighter)
            return null;
        try
        {
            return highlighter(code, language)?.ToArray();
        }
        catch (Exception error)
        {
            Warnings.Add("The code highlighter failed: " + error.Message);
            return null;
        }
    }

    private SkUiCoreNode? CreateMathBlock(string latex, SkUiMarkdownMathContext context)
    {
        try
        {
            return _settings.MathRenderer?.CreateBlock(latex, context);
        }
        catch (Exception error)
        {
            Warnings.Add("The math renderer failed: " + error.Message);
            return null;
        }
    }

    private SkUiInlineObject? CreateMathInline(string latex, SkUiMarkdownMathContext context)
    {
        try
        {
            return _settings.MathRenderer?.CreateInline(latex, context);
        }
        catch (Exception error)
        {
            Warnings.Add("The math renderer failed: " + error.Message);
            return null;
        }
    }

    private SkUiCoreNode BuildBlockMath(MathBlock math, in BlockContext context)
    {
        var latex = math.Lines.ToString().Trim();
        var size = _bodySize * context.SizeScale;
        var node = CreateMathBlock(latex, new SkUiMarkdownMathContext(size, context.TextColor, _settings.ScaledSize(size)));
        if (node is null)
        {
            var source = HorizontalScroll(BuildCodeLabel(latex, null, context));
            source.SetSemanticDescription(latex);
            return source;
        }
        node.SetHorizontalAlignment(LayoutAlignment.Center);
        var scroll = HorizontalScroll(node);
        scroll.SetFlowDirection(FlowDirection.LeftToRight); // formulas read left to right in any document
        scroll.SetSemanticDescription(latex);
        return scroll;
    }

    private void AddInlineMath(MathInline math, in SpanLook look, InlineRun run)
    {
        var latex = math.Content.ToString();
        var size = LookSize(look, run.Context);
        var inlineObject = CreateMathInline(latex, new SkUiMarkdownMathContext(size, look.TextColor ?? run.Context.TextColor, _settings.ScaledSize(size)));
        if (inlineObject is null)
        {
            run.Append(latex, CodeLook(look, run.Context)); // D7 in the plan: the source, until the renderer can place it
            return;
        }
        var span = CreateSpan(latex, look, run.Context);
        span.SetInlineObject(inlineObject);
        run.AppendSpan(span);
    }
}

/// <summary>What a build reads: a copy of the view's settings, so a build on another thread sees nothing change.</summary>
/// <param name="Style">How the document draws.</param>
/// <param name="AllowRemoteImages">Whether remote images load.</param>
/// <param name="ImageResolver">The rule of what a URL may load.</param>
/// <param name="CodeHighlighter">Colors code blocks.</param>
/// <param name="MathRenderer">Draws formulas.</param>
/// <param name="ScaledSizes">What the system text size and the look's scale make of the document's sizes, read on the UI thread: on Apple platforms a build's thread cannot, and the scale is not the same for every size.</param>
/// <param name="TextScale">The same at the body size, for a size not among them.</param>
internal sealed record SkUiMarkdownBuildSettings(SkUiMarkdownStyle Style, bool AllowRemoteImages, Func<string, SkUiImageSource?>? ImageResolver, SkUiMarkdownCodeHighlighter? CodeHighlighter,
    ISkUiMarkdownMathRenderer? MathRenderer, IReadOnlyDictionary<double, double> ScaledSizes, double TextScale)
{
    /// <summary>The size <paramref name="size"/> draws at.</summary>
    internal double ScaledSize(double size) => ScaledSizes.TryGetValue(size, out var scaled) ? scaled : size * TextScale;
}
