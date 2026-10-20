using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>
/// Makes a node's own look (or the nearest ancestor's) the one <see cref="SkUiLook.Current"/> returns on this thread while
/// the node measures and records. Does nothing until a node sets a look, so apps with one app-wide look pay a flag check.
/// </summary>
internal readonly struct SkUiLookScope : IDisposable
{
    private readonly SkUiLook? _previous;
    private readonly bool _entered;

    private SkUiLookScope(SkUiLook look)
    {
        _previous = SkUiLook.Scoped;
        SkUiLook.Scoped = look;
        _entered = true;
    }

    /// <summary>Enters the scope of the look <paramref name="node"/> draws with, when it or an ancestor sets one.</summary>
    internal static SkUiLookScope Enter(ISkUiRenderable node)
    {
        if (!SkUiLook.AnyTreeLook)
            return default;
        for (var current = node; current is not null; current = current.RenderParent)
            if (current.LookOverride is { } look)
                return new SkUiLookScope(look);
        return default;
    }

    /// <summary>The look <paramref name="node"/> draws with: its own or the nearest ancestor's, else <see cref="SkUiLook.Current"/>.</summary>
    internal static SkUiLook Resolve(ISkUiRenderable node)
    {
        if (SkUiLook.AnyTreeLook)
            for (var current = node; current is not null; current = current.RenderParent)
                if (current.LookOverride is { } look)
                    return look;
        return SkUiLook.Current;
    }

    /// <summary>Makes <paramref name="look"/> current on this thread (controls created in the scope read their defaults from it).</summary>
    internal static SkUiLookScope Enter(SkUiLook look) => new(look);

    /// <summary>Restores the look the thread used before.</summary>
    public void Dispose()
    {
        if (_entered)
            SkUiLook.Scoped = _previous;
    }
}
