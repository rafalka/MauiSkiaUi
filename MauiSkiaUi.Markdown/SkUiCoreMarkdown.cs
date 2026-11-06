using System.Windows.Input;
using Markdig.Extensions.Footnotes;
using Markdig.Syntax;
using MauiSkiaUi.Core;

namespace MauiSkiaUi.Markdown;

/// <summary>
/// Draws a Markdown document with Core nodes (Core analogue of <see cref="SkUiMarkdownView"/>). Put it in a scroll view: it is as tall as its
/// document. Nothing a document says happens by itself: links and images raise events, and remote images load only with
/// <see cref="AllowRemoteImages"/>. Bad input never throws: a document that cannot be built shows as plain text, with a warning
/// (<see cref="Warning"/>). Parsing and creating nodes run on a background thread; only putting them in the tree, measuring and recording run on the UI thread.
/// </summary>
public class SkUiCoreMarkdown : SkUiCoreContentView
{
    private readonly SkUiCoreVerticalStackLayout _stack = new();
    private readonly List<Entry> _entries = [];
    private string _text = string.Empty;
    // The text the entries were built from, what decides whether they still hold.
    private string _builtText = string.Empty;
    private int _builtReferences;
    private bool _builtFootnotes;
    private bool _isStale = true;
    private SkUiMarkdownStyle _style = SkUiMarkdownStyle.Default;
    private SkUiColorScheme? _scheme;
    private bool _allowRemoteImages;
    private bool _allowImageZoom;
    private Func<string, SkUiImageSource?>? _imageResolver;
    private SkUiMarkdownCodeHighlighter? _codeHighlighter;
    private ISkUiMarkdownMathRenderer? _mathRenderer;
    private ICommand? _linkTappedCommand;
    private ICommand? _imageTappedCommand;
    private bool _isPlainText;
    private int _generation;
    private bool _isBuilding;
    private TaskCompletionSource? _updated;
    private bool _isZoomOpen;

    /// <summary>One top-level block: where its source was, and the nodes made from it.</summary>
    private sealed record Entry(int Start, int Length, List<SkUiCoreNode> Nodes, int BlockCount, int ImageCount);

    /// <summary>What a build needs, read on the UI thread: a build on another thread sees nothing that can change.</summary>
    private sealed record BuildRequest(SkUiCoreMarkdown Owner, string Text, SkUiMarkdownBuildSettings Settings, Entry[] Entries, string BuiltText, int BuiltReferences, bool BuiltFootnotes, bool CanReuse);

    /// <summary>What a build made, for the UI thread to put on screen; nothing of it is in the tree yet.</summary>
    private sealed record BuildResult(string Text, List<Entry> Built, int[] PrefixStarts, int[] SuffixStarts, int References, bool HasFootnotes, SkUiCoreLabel? PlainLabel,
        List<Action> UiActions, List<string> Warnings);

    /// <summary>Creates a view with no document.</summary>
    public SkUiCoreMarkdown()
    {
        _stack.SetSpacing(_style.BlockSpacing);
        SetContent(_stack);
        SkUiColorScheme.CurrentChanged += OnSchemeChanged; // weak: the scheme does not keep a view alive
        ListenToScheme();
        // Weak too. Sizes are worked out as a document builds, so what changes them builds it again.
        SkUiLook.CurrentChanged += OnSizesChanged;
        SkUiFontScaling.Changed += OnSizesChanged;
        // On Windows, the first read of the text size decides the thread its changes are raised on: this one, not a build's.
        _ = SkUiFontScaling.ScaleFontSize(1);
    }

    /// <summary>The renderer of views that set none: the math package's <c>UseSkiaUiMarkdownMath()</c> sets it app-wide.</summary>
    public static ISkUiMarkdownMathRenderer? DefaultMathRenderer { get; set; }

    /// <summary>A link was tapped (<c>mailto:</c> included): nothing opens by itself, and a document can link to anything, so check the target's scheme.</summary>
    public event EventHandler<SkUiLinkTappedEventArgs>? LinkTapped;

    /// <summary>An image, or the alt text shown instead of it, was tapped.</summary>
    public event EventHandler<SkUiMarkdownImageTappedEventArgs>? ImageTapped;

    /// <summary>A document could not be built or passed a limit, and shows as plain text. The argument says why.</summary>
    public event EventHandler<string>? Warning;

    /// <summary>The document's text changed on screen.</summary>
    public event EventHandler? ContentUpdated;

    /// <summary>The Markdown. It is parsed and built on a background thread, and goes on screen when that is done (see <see cref="SkUiMarkdownStyle.MaximumInPlaceParseLength"/>).</summary>
    public string Text { get => _text; set => SetText(value); }

    /// <summary>How the document draws. Default <see cref="SkUiMarkdownStyle.Default"/>.</summary>
    public SkUiMarkdownStyle MarkdownStyle { get => _style; set => SetMarkdownStyle(value); }

    /// <summary>
    /// Whether <c>http</c> and <c>https</c> images load. Default <c>false</c>: an image URL reaches its server as soon as it draws, which can track a
    /// reader or carry data out of a conversation. See <see cref="SkUiMarkdownImages.Resolve"/>; <see cref="ImageResolver"/> replaces the rule.
    /// </summary>
    public bool AllowRemoteImages { get => _allowRemoteImages; set => SetAllowRemoteImages(value); }

    /// <summary>Replaces the rule of what a URL may load: <c>null</c> from it shows the alt text. <c>null</c>: <see cref="SkUiMarkdownImages.Resolve"/>. Runs on a background thread.</summary>
    public Func<string, SkUiImageSource?>? ImageResolver { get => _imageResolver; set => SetImageResolver(value); }

    /// <summary>Colors code blocks, on a background thread. No tokenizer ships.</summary>
    public SkUiMarkdownCodeHighlighter? CodeHighlighter { get => _codeHighlighter; set => SetCodeHighlighter(value); }

    /// <summary>Draws formulas; <c>null</c>: <see cref="DefaultMathRenderer"/>, else they show their source in the code style.</summary>
    public ISkUiMarkdownMathRenderer? MathRenderer { get => _mathRenderer; set => SetMathRenderer(value); }

    /// <summary>Whether tapping a picture opens it, zoomable, through the app's popup presenter (<see cref="SkUiPopups.Presenter"/>); without one, <see cref="ImageTapped"/> remains.</summary>
    public bool AllowImageZoom { get => _allowImageZoom; set => SetAllowImageZoom(value); }

    /// <summary>Runs with the target of a tapped link.</summary>
    public ICommand? LinkTappedCommand { get => _linkTappedCommand; set => SetLinkTappedCommand(value); }

    /// <summary>Runs with the <see cref="SkUiMarkdownImageTappedEventArgs"/> of a tapped image.</summary>
    public ICommand? ImageTappedCommand { get => _imageTappedCommand; set => SetImageTappedCommand(value); }

    /// <summary>Whether the document shows as plain text: it could not be built, or passed a limit of <see cref="MarkdownStyle"/>.</summary>
    public bool IsPlainText => _isPlainText;

    /// <summary>Completes when the current text is on screen.</summary>
    public Task WhenUpdated => _updated?.Task ?? Task.CompletedTask;

    /// <summary>Sets <see cref="Text"/>.</summary>
    public SkUiCoreMarkdown SetText(string? value)
    {
        value ??= string.Empty;
        if (!SetProperty(ref _text, value, nameof(Text))) return this;
        Update();
        return this;
    }

    /// <summary>Sets <see cref="MarkdownStyle"/>; the document is built again.</summary>
    public SkUiCoreMarkdown SetMarkdownStyle(SkUiMarkdownStyle value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.FindInvalidValue() is { } invalid)
            throw new ArgumentException($"{invalid} is out of range.", nameof(value));
        if (!SetProperty(ref _style, value, nameof(MarkdownStyle))) return this;
        _stack.SetSpacing(value.BlockSpacing);
        Rebuild();
        return this;
    }

    /// <summary>Sets <see cref="AllowRemoteImages"/>; the document is built again.</summary>
    public SkUiCoreMarkdown SetAllowRemoteImages(bool value)
    {
        if (!SetProperty(ref _allowRemoteImages, value, nameof(AllowRemoteImages))) return this;
        Rebuild();
        return this;
    }

    /// <summary>Sets <see cref="ImageResolver"/>; the document is built again.</summary>
    public SkUiCoreMarkdown SetImageResolver(Func<string, SkUiImageSource?>? value)
    {
        if (!SetProperty(ref _imageResolver, value, nameof(ImageResolver))) return this;
        Rebuild();
        return this;
    }

    /// <summary>Sets <see cref="CodeHighlighter"/>; the document is built again.</summary>
    public SkUiCoreMarkdown SetCodeHighlighter(SkUiMarkdownCodeHighlighter? value)
    {
        if (!SetProperty(ref _codeHighlighter, value, nameof(CodeHighlighter))) return this;
        Rebuild();
        return this;
    }

    /// <summary>Sets <see cref="MathRenderer"/>; the document is built again.</summary>
    public SkUiCoreMarkdown SetMathRenderer(ISkUiMarkdownMathRenderer? value)
    {
        if (!SetProperty(ref _mathRenderer, value, nameof(MathRenderer))) return this;
        Rebuild();
        return this;
    }

    /// <summary>Sets <see cref="AllowImageZoom"/>.</summary>
    public SkUiCoreMarkdown SetAllowImageZoom(bool value)
    {
        SetProperty(ref _allowImageZoom, value, nameof(AllowImageZoom));
        return this;
    }

    /// <summary>Sets <see cref="LinkTappedCommand"/>.</summary>
    public SkUiCoreMarkdown SetLinkTappedCommand(ICommand? value)
    {
        SetProperty(ref _linkTappedCommand, value, nameof(LinkTappedCommand));
        return this;
    }

    /// <summary>Sets <see cref="ImageTappedCommand"/>.</summary>
    public SkUiCoreMarkdown SetImageTappedCommand(ICommand? value)
    {
        SetProperty(ref _imageTappedCommand, value, nameof(ImageTappedCommand));
        return this;
    }

    internal void OnLinkTapped(string target, Point position)
    {
        LinkTapped?.Invoke(this, new SkUiLinkTappedEventArgs(target, position));
        if (_linkTappedCommand?.CanExecute(target) == true)
            _linkTappedCommand.Execute(target);
    }

    internal void OnImageTapped(string url, string altText, SkUiImageSource? picture)
    {
        var args = new SkUiMarkdownImageTappedEventArgs(url, altText);
        ImageTapped?.Invoke(this, args);
        if (_imageTappedCommand?.CanExecute(args) == true)
            _imageTappedCommand.Execute(args);
        if (_allowImageZoom && !_isZoomOpen && picture is not null && SkUiPopups.Presenter is not null && SkUiDiagnostics.GetSurfaceRoot(this) is { Window: { } window } anchor)
            _ = ShowOneZoomAsync(anchor, picture, altText, new Size(window.Width, window.Height));
    }

    // One popup at a time: a quick second tap can come before the first has opened.
    private async Task ShowOneZoomAsync(SkUiView anchor, SkUiImageSource picture, string altText, Size size)
    {
        _isZoomOpen = true;
        try
        {
            await ShowZoomAsync(anchor, picture, altText, size);
        }
        finally
        {
            _isZoomOpen = false;
        }
    }

    /// <summary>The picture in a popup of the app's presenter: fitted, zoomable, and a tap closes it.</summary>
    internal static async Task ShowZoomAsync(SkUiView anchor, SkUiImageSource picture, string altText, Size size, ISkUiPopupPresenter? presenter = null)
    {
        try
        {
            var image = new SkUiCoreImage();
            image.SetSemanticDescription(altText);
            image.SetSource(picture);
            var zoom = new SkUiCoreZoomView();
            zoom.SetContent(image);
            var host = new SkUiCoreHost();
            host.BackgroundColor = SkUiColorScheme.Current.DefaultBackground;
            host.SetContent(zoom);
            await SkUiPopups.ShowContentAsync(anchor, host, size, presenter, request => zoom.Tapped += (_, _) => request.Cancel());
        }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine("SkiaUi Markdown: the image popup failed: " + error.Message);
        }
    }

    /// <summary>What writes to the clipboard; tests replace it.</summary>
    internal Func<string, Task> ClipboardWriter { get; set; } = static code => Clipboard.Default.SetTextAsync(code);

    internal async void CopyCode(string code)
    {
        try
        {
            await ClipboardWriter(code);
        }
        catch (Exception error)
        {
            RaiseWarning("The code could not be copied: " + error.Message);
        }
    }

    internal void RaiseWarning(string message)
    {
        System.Diagnostics.Debug.WriteLine("SkiaUi Markdown: " + message);
        Warning?.Invoke(this, message);
    }

    private void OnSchemeChanged(object? sender, EventArgs e)
    {
        ListenToScheme();
        Rebuild();
    }

    private void OnSchemeTokensChanged(object? sender, EventArgs e) => Rebuild();

    private void OnSizesChanged(object? sender, EventArgs e) => Rebuild();

    private void ListenToScheme()
    {
        if (_scheme is not null)
            _scheme.Changed -= OnSchemeTokensChanged;
        _scheme = SkUiColorScheme.Current;
        _scheme.Changed += OnSchemeTokensChanged; // weak, as CurrentChanged
    }

    /// <summary>The look changed: nothing built so far holds.</summary>
    private void Rebuild()
    {
        _isStale = true;
        Update();
    }

    private void Update()
    {
        var generation = ++_generation;
        var text = _text;
        if (text.Length == 0)
        {
            Apply(new BuildResult(text, [], [], [], 0, false, null, [], []));
            return;
        }
        if (SkUiMarkdownParser.ParsesInPlace(text, _style.MaximumInPlaceParseLength))
        {
            Apply(Build(CreateRequest()));
            return;
        }
        // Latest text wins, one build at a time: a stream of appends cannot pile up parses, which cannot be stopped.
        _updated ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (_isBuilding)
            return;
        _isBuilding = true;
        _ = BuildInBackgroundAsync(CreateRequest(), generation, Dispatcher.GetForCurrentThread());
    }

    private BuildRequest CreateRequest()
    {
        var bodySize = _style.FontSize ?? SkUiLook.Current.DefaultFontSize;
        var scaled = new Dictionary<double, double>(8);
        foreach (var scale in (ReadOnlySpan<double>)[1, _style.Heading1Scale, _style.Heading2Scale, _style.Heading3Scale, _style.Heading4Scale, _style.Heading5Scale, _style.Heading6Scale, SkUiMarkdownBuilder.FootnoteScale])
            scaled[bodySize * scale] = SkUiFontScaling.ScaleFontSize(bodySize * scale);
        return new BuildRequest(this, _text,
            new SkUiMarkdownBuildSettings(_style, _allowRemoteImages, _imageResolver, _codeHighlighter, _mathRenderer ?? DefaultMathRenderer, scaled, scaled[bodySize] / bodySize),
            [.. _entries], _builtText, _builtReferences, _builtFootnotes, !_isStale && !_isPlainText);
    }

    private async Task BuildInBackgroundAsync(BuildRequest request, int generation, IDispatcher? dispatcher)
    {
        while (true)
        {
            var current = request;
            var result = await Task.Run(() => Build(current)).ConfigureAwait(false);
            BuildRequest? next = null;
            void Finish()
            {
                if (generation == _generation)
                {
                    _isBuilding = false;
                    Apply(result);
                }
                else if (_updated is null)
                    _isBuilding = false; // a newer text went on screen without a build here
                else
                {
                    // Dropped: nothing of it was shown, so it holds nothing native. Build what is current.
                    generation = _generation;
                    next = CreateRequest();
                }
            }
            if (dispatcher is null)
                Finish();
            else
            {
                var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                if (!dispatcher.Dispatch(() =>
                    {
                        try
                        {
                            Finish();
                        }
                        finally
                        {
                            finished.SetResult();
                        }
                    }))
                {
                    // The dispatcher is closed, as when the app is closing: the result cannot go on screen, and the tree is not this thread's. Whoever waits stops
                    // waiting, and the next text builds again.
                    _isBuilding = false;
                    Interlocked.Exchange(ref _updated, null)?.TrySetResult();
                    finished.SetResult();
                }
                await finished.Task.ConfigureAwait(false);
            }
            if (next is null)
                return;
            request = next;
        }
    }

    /// <summary>
    /// Parses and creates nodes, on any thread: Core nodes outside a tree belong to no thread. What does need the UI thread (starting an image load) goes in
    /// <see cref="BuildResult.UiActions"/>. Keeps the nodes of blocks whose source and look did not change.
    /// </summary>
    private static BuildResult Build(BuildRequest request)
    {
        var text = request.Text;
        try
        {
            var style = request.Settings.Style;
            if (text.Length > style.MaximumInputLength)
                return BuildPlain(request, text[..style.MaximumInputLength], "The document is longer than MaximumInputLength.");
            if (SkUiMarkdownParser.HasPathologicalBlock(text))
                return BuildPlain(request, text, "A block holds too many delimiters.");
            return BuildDocument(request, SkUiMarkdownParser.Parse(text));
        }
        catch (Exception error)
        {
            return BuildPlain(request, text, "The document could not be built: " + error.Message);
        }
    }

    private static BuildResult BuildPlain(BuildRequest request, string text, string problem)
    {
        try
        {
            var label = new SkUiMarkdownBuilder(request.Owner, request.Settings, text, 0, 0).PlainLabel(text);
            return new BuildResult(text, [], [], [], 0, false, label, [], [problem]);
        }
        catch (Exception error)
        {
            return new BuildResult(string.Empty, [], [], [], 0, false, null, [], [problem, error.Message]);
        }
    }

    private static BuildResult BuildDocument(BuildRequest request, MarkdownDocument document)
    {
        var text = request.Text;
        var blocks = new List<Block>(document.Count);
        var references = 0;
        var hasFootnotes = false;
        foreach (var block in document)
        {
            if (block is LinkReferenceDefinitionGroup group)
            {
                // A reference defined later changes blocks before it, whose source did not change.
                foreach (var (key, definition) in group.Links)
                    references ^= HashCode.Combine(key, definition.Url, definition.Title);
                continue;
            }
            hasFootnotes |= block is FootnoteGroup;
            if (block is not BlankLineBlock)
                blocks.Add(block);
        }

        var reusable = request.CanReuse && references == request.BuiltReferences;
        var old = request.Entries;
        var prefix = 0;
        if (reusable)
            while (prefix < old.Length && prefix < blocks.Count && SameSource(old[prefix], blocks[prefix], text, request.BuiltText))
                prefix++;
        // Footnote numbers depend on what is before a block, so only what comes first is sure to hold when there are footnotes. Being first changes a block too (a heading's top spacing).
        var suffix = 0;
        if (reusable && !hasFootnotes && !request.BuiltFootnotes)
            while (suffix < old.Length - prefix && suffix < blocks.Count - prefix && (old.Length - suffix == 1) == (blocks.Count - suffix == 1)
                   && SameSource(old[^(suffix + 1)], blocks[^(suffix + 1)], text, request.BuiltText))
                suffix++;

        var blockCount = 0;
        var imageCount = 0;
        for (var index = 0; index < prefix; index++)
            (blockCount, imageCount) = (blockCount + old[index].BlockCount, imageCount + old[index].ImageCount);
        for (var index = 0; index < suffix; index++)
            (blockCount, imageCount) = (blockCount + old[^(index + 1)].BlockCount, imageCount + old[^(index + 1)].ImageCount);

        var builder = new SkUiMarkdownBuilder(request.Owner, request.Settings, text, blockCount, imageCount);
        var built = new List<Entry>(blocks.Count - prefix - suffix);
        for (var index = prefix; index < blocks.Count - suffix; index++)
        {
            var (blocksBefore, imagesBefore) = (builder.BlockCount, builder.ImageCount);
            var nodes = builder.Build(blocks[index], index == 0);
            built.Add(new Entry(blocks[index].Span.Start, blocks[index].Span.Length, nodes, builder.BlockCount - blocksBefore, builder.ImageCount - imagesBefore));
        }

        var prefixStarts = new int[prefix];
        for (var index = 0; index < prefix; index++)
            prefixStarts[index] = blocks[index].Span.Start;
        var suffixStarts = new int[suffix];
        for (var index = 0; index < suffix; index++)
            suffixStarts[index] = blocks[blocks.Count - suffix + index].Span.Start;
        builder.ReleaseSource();
        return new BuildResult(text, built, prefixStarts, suffixStarts, references, hasFootnotes, null, builder.UiActions, builder.Warnings);
    }

    private static bool SameSource(Entry entry, Block block, string text, string builtText)
    {
        var span = block.Span;
        if (span.Length != entry.Length || block is FootnoteGroup || span.Start < 0 || span.Start + span.Length > text.Length)
            return false;
        return entry.Start >= 0 && entry.Start + entry.Length <= builtText.Length
            && text.AsSpan(span.Start, span.Length).SequenceEqual(builtText.AsSpan(entry.Start, entry.Length));
    }

    /// <summary>The UI thread's part: what a build made goes into the tree. Everything of it is measured and recorded from here on.</summary>
    private void Apply(BuildResult result)
    {
        StartUpdating();
        try
        {
            foreach (var action in result.UiActions)
                action();
            result.UiActions.Clear(); // the builder lives on in what an image block keeps, and these hold every node of this build
            if (_isPlainText)
            {
                _stack.Clear();
                _entries.Clear();
            }
            if (result.PlainLabel is { } label)
            {
                _stack.Clear();
                _entries.Clear();
                _stack.Add(label);
                _builtText = string.Empty;
                _isStale = true; // nothing to reuse next time
                SetPlainText(true);
            }
            else
            {
                var (prefix, suffix) = (result.PrefixStarts.Length, result.SuffixStarts.Length);
                var first = 0;
                for (var index = 0; index < prefix; index++)
                    first += _entries[index].Nodes.Count;
                var removed = 0;
                for (var index = prefix; index < _entries.Count - suffix; index++)
                    removed += _entries[index].Nodes.Count;
                for (var index = first + removed - 1; index >= first; index--)
                    _stack.RemoveAt(index);
                var insertAt = first;
                foreach (var entry in result.Built)
                    foreach (var node in entry.Nodes)
                        _stack.Insert(insertAt++, node);

                var next = new List<Entry>(prefix + result.Built.Count + suffix);
                for (var index = 0; index < prefix; index++)
                    next.Add(_entries[index] with { Start = result.PrefixStarts[index] });
                next.AddRange(result.Built);
                for (var index = 0; index < suffix; index++)
                    next.Add(_entries[_entries.Count - suffix + index] with { Start = result.SuffixStarts[index] });
                _entries.Clear();
                _entries.AddRange(next);
                _builtText = result.Text;
                _builtReferences = result.References;
                _builtFootnotes = result.HasFootnotes;
                _isStale = false;
                SetPlainText(false);
            }
        }
        catch (Exception error)
        {
            // Nothing in a build should fail here; the tree stays as it was left, and the next text builds everything.
            _isStale = true;
            result.Warnings.Add("The document could not be shown: " + error.Message);
        }
        finally
        {
            EndUpdating();
        }
        foreach (var warning in result.Warnings)
            RaiseWarning(warning);
        ContentUpdated?.Invoke(this, EventArgs.Empty);
        var updated = _updated;
        _updated = null;
        updated?.TrySetResult();
    }

    private void SetPlainText(bool value)
    {
        if (_isPlainText == value) return;
        _isPlainText = value;
        OnPropertyChanged(nameof(IsPlainText));
    }
}
