using System.Windows.Input;
using MauiSkiaUi.Core;

namespace MauiSkiaUi.Markdown;

/// <summary>
/// Draws a Markdown document: a <see cref="SkUiCoreHost"/> around a <see cref="SkUiCoreMarkdown"/>, the same engine with bindable properties. Put it in a
/// scroll view: it is as tall as its document. Nothing a document says happens by itself: links and images raise events, and remote images load only with
/// <see cref="AllowRemoteImages"/>.
/// </summary>
public class SkUiMarkdownView : SkUiCoreHost
{
    // A field initializer: an implicit style applies inside the base constructor, before the constructor body.
    private readonly SkUiCoreMarkdown _markdown = new();

    /// <summary>Bindable <see cref="Text"/>.</summary>
    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(SkUiMarkdownView), string.Empty,
        propertyChanged: static (view, _, value) => ((SkUiMarkdownView)view)._markdown.SetText((string?)value));

    /// <summary>Bindable <see cref="MarkdownStyle"/>.</summary>
    public static readonly BindableProperty MarkdownStyleProperty = BindableProperty.Create(nameof(MarkdownStyle), typeof(SkUiMarkdownStyle), typeof(SkUiMarkdownView), SkUiMarkdownStyle.Default,
        validateValue: static (_, value) => value is SkUiMarkdownStyle style && style.FindInvalidValue() is null,
        propertyChanged: static (view, _, value) => ((SkUiMarkdownView)view)._markdown.SetMarkdownStyle((SkUiMarkdownStyle)value));

    /// <summary>Bindable <see cref="AllowRemoteImages"/>.</summary>
    public static readonly BindableProperty AllowRemoteImagesProperty = BindableProperty.Create(nameof(AllowRemoteImages), typeof(bool), typeof(SkUiMarkdownView), false,
        propertyChanged: static (view, _, value) => ((SkUiMarkdownView)view)._markdown.SetAllowRemoteImages((bool)value));

    /// <summary>Bindable <see cref="ImageResolver"/>.</summary>
    public static readonly BindableProperty ImageResolverProperty = BindableProperty.Create(nameof(ImageResolver), typeof(Func<string, SkUiImageSource?>), typeof(SkUiMarkdownView), null,
        propertyChanged: static (view, _, value) => ((SkUiMarkdownView)view)._markdown.SetImageResolver((Func<string, SkUiImageSource?>?)value));

    /// <summary>Bindable <see cref="CodeHighlighter"/>.</summary>
    public static readonly BindableProperty CodeHighlighterProperty = BindableProperty.Create(nameof(CodeHighlighter), typeof(SkUiMarkdownCodeHighlighter), typeof(SkUiMarkdownView), null,
        propertyChanged: static (view, _, value) => ((SkUiMarkdownView)view)._markdown.SetCodeHighlighter((SkUiMarkdownCodeHighlighter?)value));

    /// <summary>Bindable <see cref="MathRenderer"/>.</summary>
    public static readonly BindableProperty MathRendererProperty = BindableProperty.Create(nameof(MathRenderer), typeof(ISkUiMarkdownMathRenderer), typeof(SkUiMarkdownView), null,
        propertyChanged: static (view, _, value) => ((SkUiMarkdownView)view)._markdown.SetMathRenderer((ISkUiMarkdownMathRenderer?)value));

    /// <summary>Bindable <see cref="AllowImageZoom"/>.</summary>
    public static readonly BindableProperty AllowImageZoomProperty = BindableProperty.Create(nameof(AllowImageZoom), typeof(bool), typeof(SkUiMarkdownView), false,
        propertyChanged: static (view, _, value) => ((SkUiMarkdownView)view)._markdown.SetAllowImageZoom((bool)value));

    /// <summary>Bindable <see cref="LinkTappedCommand"/>.</summary>
    public static readonly BindableProperty LinkTappedCommandProperty = BindableProperty.Create(nameof(LinkTappedCommand), typeof(ICommand), typeof(SkUiMarkdownView), null,
        propertyChanged: static (view, _, value) => ((SkUiMarkdownView)view)._markdown.SetLinkTappedCommand((ICommand?)value));

    /// <summary>Bindable <see cref="ImageTappedCommand"/>.</summary>
    public static readonly BindableProperty ImageTappedCommandProperty = BindableProperty.Create(nameof(ImageTappedCommand), typeof(ICommand), typeof(SkUiMarkdownView), null,
        propertyChanged: static (view, _, value) => ((SkUiMarkdownView)view)._markdown.SetImageTappedCommand((ICommand?)value));

    private static readonly BindablePropertyKey IsPlainTextPropertyKey = BindableProperty.CreateReadOnly(nameof(IsPlainText), typeof(bool), typeof(SkUiMarkdownView), false);

    /// <summary>Bindable <see cref="IsPlainText"/>.</summary>
    public static readonly BindableProperty IsPlainTextProperty = IsPlainTextPropertyKey.BindableProperty;

    /// <summary>Creates a view with no document.</summary>
    public SkUiMarkdownView()
    {
        _markdown.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SkUiCoreMarkdown.IsPlainText))
                SetValue(IsPlainTextPropertyKey, _markdown.IsPlainText);
        };
        // An implicit style can have set a text before the line above.
        if (_markdown.IsPlainText)
            SetValue(IsPlainTextPropertyKey, true);
        _markdown.LinkTapped += (_, args) => LinkTapped?.Invoke(this, args);
        _markdown.ImageTapped += (_, args) => ImageTapped?.Invoke(this, args);
        _markdown.Warning += (_, message) => Warning?.Invoke(this, message);
        _markdown.ContentUpdated += (_, args) => ContentUpdated?.Invoke(this, args);
        SetContent(_markdown);
    }

    /// <inheritdoc cref="SkUiCoreMarkdown.LinkTapped" />
    public event EventHandler<SkUiLinkTappedEventArgs>? LinkTapped;

    /// <inheritdoc cref="SkUiCoreMarkdown.ImageTapped" />
    public event EventHandler<SkUiMarkdownImageTappedEventArgs>? ImageTapped;

    /// <inheritdoc cref="SkUiCoreMarkdown.Warning" />
    public event EventHandler<string>? Warning;

    /// <inheritdoc cref="SkUiCoreMarkdown.ContentUpdated" />
    public event EventHandler? ContentUpdated;

    /// <summary>The engine behind this view.</summary>
    public SkUiCoreMarkdown Markdown => _markdown;

    /// <inheritdoc cref="SkUiCoreMarkdown.Text" />
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="SkUiCoreMarkdown.MarkdownStyle" />
    public SkUiMarkdownStyle MarkdownStyle { get => (SkUiMarkdownStyle)GetValue(MarkdownStyleProperty); set => SetValue(MarkdownStyleProperty, value); }

    /// <inheritdoc cref="SkUiCoreMarkdown.AllowRemoteImages" />
    public bool AllowRemoteImages { get => (bool)GetValue(AllowRemoteImagesProperty); set => SetValue(AllowRemoteImagesProperty, value); }

    /// <inheritdoc cref="SkUiCoreMarkdown.ImageResolver" />
    public Func<string, SkUiImageSource?>? ImageResolver { get => (Func<string, SkUiImageSource?>?)GetValue(ImageResolverProperty); set => SetValue(ImageResolverProperty, value); }

    /// <inheritdoc cref="SkUiCoreMarkdown.CodeHighlighter" />
    public SkUiMarkdownCodeHighlighter? CodeHighlighter { get => (SkUiMarkdownCodeHighlighter?)GetValue(CodeHighlighterProperty); set => SetValue(CodeHighlighterProperty, value); }

    /// <inheritdoc cref="SkUiCoreMarkdown.MathRenderer" />
    public ISkUiMarkdownMathRenderer? MathRenderer { get => (ISkUiMarkdownMathRenderer?)GetValue(MathRendererProperty); set => SetValue(MathRendererProperty, value); }

    /// <inheritdoc cref="SkUiCoreMarkdown.AllowImageZoom" />
    public bool AllowImageZoom { get => (bool)GetValue(AllowImageZoomProperty); set => SetValue(AllowImageZoomProperty, value); }

    /// <inheritdoc cref="SkUiCoreMarkdown.LinkTappedCommand" />
    public ICommand? LinkTappedCommand { get => (ICommand?)GetValue(LinkTappedCommandProperty); set => SetValue(LinkTappedCommandProperty, value); }

    /// <inheritdoc cref="SkUiCoreMarkdown.ImageTappedCommand" />
    public ICommand? ImageTappedCommand { get => (ICommand?)GetValue(ImageTappedCommandProperty); set => SetValue(ImageTappedCommandProperty, value); }

    /// <inheritdoc cref="SkUiCoreMarkdown.IsPlainText" />
    public bool IsPlainText => (bool)GetValue(IsPlainTextProperty);

    /// <inheritdoc cref="SkUiCoreMarkdown.WhenUpdated" />
    public Task WhenUpdated => _markdown.WhenUpdated;

    /// <summary>Sets <see cref="Text"/>.</summary>
    public SkUiMarkdownView SetText(string? value) { Text = value ?? string.Empty; return this; }

    /// <summary>Sets <see cref="MarkdownStyle"/>.</summary>
    public SkUiMarkdownView SetMarkdownStyle(SkUiMarkdownStyle value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.FindInvalidValue() is { } invalid)
            throw new ArgumentException($"{invalid} is out of range.", nameof(value));
        MarkdownStyle = value;
        return this;
    }

    /// <summary>Sets <see cref="AllowRemoteImages"/>.</summary>
    public SkUiMarkdownView SetAllowRemoteImages(bool value) { AllowRemoteImages = value; return this; }

    /// <summary>Sets <see cref="ImageResolver"/>.</summary>
    public SkUiMarkdownView SetImageResolver(Func<string, SkUiImageSource?>? value) { ImageResolver = value; return this; }

    /// <summary>Sets <see cref="CodeHighlighter"/>.</summary>
    public SkUiMarkdownView SetCodeHighlighter(SkUiMarkdownCodeHighlighter? value) { CodeHighlighter = value; return this; }

    /// <summary>Sets <see cref="MathRenderer"/>.</summary>
    public SkUiMarkdownView SetMathRenderer(ISkUiMarkdownMathRenderer? value) { MathRenderer = value; return this; }

    /// <summary>Sets <see cref="AllowImageZoom"/>.</summary>
    public SkUiMarkdownView SetAllowImageZoom(bool value) { AllowImageZoom = value; return this; }

    /// <summary>Sets <see cref="LinkTappedCommand"/>.</summary>
    public SkUiMarkdownView SetLinkTappedCommand(ICommand? value) { LinkTappedCommand = value; return this; }

    /// <summary>Sets <see cref="ImageTappedCommand"/>.</summary>
    public SkUiMarkdownView SetImageTappedCommand(ICommand? value) { ImageTappedCommand = value; return this; }
}
