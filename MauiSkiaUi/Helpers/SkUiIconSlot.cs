using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>
/// An icon a control draws from a MAUI image source (a segment's, a tab header's): loaded through the shared cache, again when
/// the source's own properties change (a glyph, a color bound to the theme). The source is listened to weakly: it can be a
/// resource of the app.
/// </summary>
/// <param name="host">The node that draws it.</param>
/// <param name="changed">The icon was loaded, or another one is shown: draw again.</param>
internal sealed class SkUiIconSlot(ISkUiTransitionHost host, Action changed) : IDisposable
{
    private SkUiWeakListener<SkUiIconSlot>? _listener;
    private SkUiImageSlot? _slot;

    /// <summary>The source shown (<c>null</c>: none).</summary>
    public ImageSource? Source { get; private set; }

    /// <summary>The loaded icon's slot, while there is a source.</summary>
    public SkUiImageSlot? Slot => Source is null ? null : _slot;

    /// <summary>Shows <paramref name="source"/>; returns whether it is another one.</summary>
    public bool Show(ImageSource? source)
    {
        if (ReferenceEquals(Source, source))
            return false;
        Source = source;
        (_listener ??= new SkUiWeakListener<SkUiIconSlot>(this, static (icon, change) =>
        {
            if (SkUiMauiImageSources.AffectsImage(change.PropertyName))
                icon.Load();
        })).Listen(source);
        Load();
        return true;
    }

    private void Load()
    {
        if (Source is null && _slot is null)
            return;
        (_slot ??= new SkUiImageSlot(host, changed)).Load(SkUiImageSource.FromMaui(Source), default);
        changed();
    }

    public void Dispose()
    {
        _listener?.Listen(null);
        _slot?.Dispose();
    }
}
