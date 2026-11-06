using MauiSkiaUi;
using MauiSkiaUi.Markdown;

namespace MauiSkiaUiDemo;

/// <summary>A document with every feature of <see cref="SkUiMarkdownView"/>, with its text editable and what taps do.</summary>
public sealed class MarkdownDemoPage : ComponentDemoPage
{
    private static readonly string[] CodeSizes = ["Default", "11", "16"];

    private readonly SkUiMarkdownView _markdown;
    private bool _isStreaming;

    public MarkdownDemoPage() : base(nameof(SkUiMarkdownView), new SkUiScrollView { HeightRequest = 480 }, widthRange: (240, 600, 360))
    {
        var scroll = (SkUiScrollView)SkiaControl;
        _markdown = new SkUiMarkdownView { Text = MarkdownDemoDocument.Text, Margin = new Thickness(8) };
        scroll.Content = _markdown;
        _markdown.LinkTapped += (_, args) => Feedback("Link: " + args.Href);
        _markdown.ImageTapped += (_, args) => Feedback("Image: " + args.AltText);
        _markdown.Warning += (_, message) => Feedback("Warning: " + message);

        MultilineText(nameof(SkUiMarkdownView.Text), MarkdownDemoDocument.Text, value => _markdown.Text = value, () => _markdown.Text);
        Toggle(nameof(SkUiMarkdownView.AllowRemoteImages), false, value => _markdown.AllowRemoteImages = value, () => _markdown.AllowRemoteImages);
        Toggle(nameof(SkUiMarkdownView.AllowImageZoom), false, value => _markdown.AllowImageZoom = value, () => _markdown.AllowImageZoom);
        Choice(nameof(SkUiMarkdownStyle.CodeFontSize), CodeSizes, CodeSizes[0],
            value => _markdown.MarkdownStyle = _markdown.MarkdownStyle with { CodeFontSize = value == CodeSizes[0] ? null : double.Parse(value, System.Globalization.CultureInfo.InvariantCulture) },
            () => _markdown.MarkdownStyle.CodeFontSize is { } size ? size.ToString(System.Globalization.CultureInfo.InvariantCulture) : CodeSizes[0]);
        ActionButton("Stream the document", StreamDocument);
    }

    /// <summary>The text arrives a piece at a time, as an answer from a model does: only the last block is built again.</summary>
    private async void StreamDocument()
    {
        if (_isStreaming)
            return;
        _isStreaming = true;
        try
        {
            var text = MarkdownDemoDocument.Text;
            // Stops when the page is left.
            for (var length = 0; length == 0 || _markdown.Window is not null; length += 40)
            {
                _markdown.Text = text[..Math.Min(length, text.Length)];
                if (length >= text.Length)
                    break;
                await Task.Delay(16);
            }
        }
        finally
        {
            _isStreaming = false;
        }
    }
}
