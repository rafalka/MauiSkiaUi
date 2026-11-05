using MauiSkiaUi.Core;

namespace MauiSkiaUi.Markdown;

/// <summary>A bar or rule: as large as it is placed, 40 × 40 of a box would hold short text apart.</summary>
internal sealed class SkUiMarkdownBar : SkUiCoreBox
{
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => Size.Zero;
}

/// <summary>A picture that fits the width it gets: scaled down to it keeping its shape, and never up unless its size is set.</summary>
internal sealed class SkUiMarkdownPicture : SkUiCoreImage
{
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var natural = base.MeasureContent(widthConstraint, heightConstraint);
        if (natural.Width <= 0 || natural.Height <= 0)
            return natural;
        var hasWidth = !double.IsNaN(Width);
        var hasHeight = !double.IsNaN(Height);
        // A set side decides the scale; otherwise the picture only shrinks into what it gets.
        double scale;
        if (hasWidth)
            scale = Width / natural.Width;
        else if (hasHeight)
            scale = Height / natural.Height;
        else
        {
            scale = 1;
            if (double.IsFinite(widthConstraint) && natural.Width > widthConstraint)
                scale = widthConstraint / natural.Width;
        }
        return new Size(natural.Width * scale, natural.Height * scale);
    }
}

/// <summary>An image in a document: the picture, or its alt text when it may not load or its load fails. A tap on either reaches the view.</summary>
internal sealed class SkUiMarkdownImageBlock : SkUiCoreContentView
{
    private readonly SkUiImageSource? _source;
    private readonly Func<SkUiCoreLabel> _createAltLabel;
    private readonly Action<SkUiMarkdownPicture> _configure;
    private readonly LayoutAlignment _alignment;

    /// <summary>What a tap does: the block is as wide as the document, the picture may not be.</summary>
    private readonly EventHandler<SkUiTappedEventArgs> _tapped;

    /// <param name="source">The picture; <c>null</c>: the alt text.</param>
    /// <param name="altText">Read by screen readers.</param>
    /// <param name="createAltLabel">Creates what shows instead of the picture.</param>
    /// <param name="configure">Applies the picture's attributes.</param>
    /// <param name="alignment">Where the picture sits in the block once its size is known.</param>
    /// <param name="tapped">What a tap does.</param>
    internal SkUiMarkdownImageBlock(SkUiImageSource? source, string altText, Func<SkUiCoreLabel> createAltLabel, Action<SkUiMarkdownPicture> configure, LayoutAlignment alignment,
        EventHandler<SkUiTappedEventArgs> tapped)
    {
        _source = source;
        _createAltLabel = createAltLabel;
        _configure = configure;
        _alignment = alignment;
        _tapped = tapped;
        SetSemanticDescription(altText);
    }

    /// <summary>On the UI thread, before the block goes on screen: an image load finishes on the dispatcher it starts from.</summary>
    internal void Start()
    {
        if (_source is null)
        {
            ShowAltText();
            return;
        }
        var picture = new SkUiMarkdownPicture();
        picture.SetDownsampleToViewSize(true);
        _configure(picture);
        // As wide as the block until it loads, so it decodes no larger than the document needs; a set size is placed at once.
        picture.SetHorizontalAlignment(double.IsNaN(picture.Width) ? LayoutAlignment.Fill : _alignment);
        picture.SetVerticalAlignment(LayoutAlignment.Start);
        picture.LoadingFinished += OnLoadingFinished;
        picture.Tapped += _tapped;
        SetContent(picture);
        picture.SetSource(_source);
    }

    /// <summary>Whether the alt text shows instead of a picture.</summary>
    internal bool ShowsAltText => Content is SkUiCoreLabel;

    private void OnLoadingFinished(object? sender, SkUiImageLoadFinishedEventArgs e)
    {
        if (e.Status == SkUiImageLoadStatus.Failed)
            ShowAltText();
        else if (e.Status == SkUiImageLoadStatus.Succeeded)
            Content?.SetHorizontalAlignment(_alignment);
    }

    private void ShowAltText()
    {
        if (Content is SkUiMarkdownPicture picture)
        {
            picture.LoadingFinished -= OnLoadingFinished;
            picture.Tapped -= _tapped;
        }
        var label = _createAltLabel();
        label.Tapped += _tapped;
        SetContent(label);
    }
}
