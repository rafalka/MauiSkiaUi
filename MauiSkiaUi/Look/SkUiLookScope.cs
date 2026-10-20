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

    private SkUiLookScope(SkUiLook? look)
    {
        _previous = SkUiLook.Scoped;
        SkUiLook.Scoped = look;
        _entered = true;
    }

    /// <summary>Enters the scope of the look <paramref name="node"/> draws with, when it or an ancestor sets one.</summary>
    /// <remarks>
    /// A node without a tree look under another scope (a surface hosted in native content of a subtree with a look,
    /// measured inside that subtree's measure) leaves it: it uses the app's look.
    /// </remarks>
    internal static SkUiLookScope Enter(ISkUiRenderable node)
    {
        var look = SkUiLook.AnyTreeLook ? node.TreeLook : null;
        if (look is null && SkUiLook.Scoped is null)
            return default;
        return ReferenceEquals(look, SkUiLook.Scoped) ? default : new SkUiLookScope(look);
    }

    /// <summary>The look <paramref name="node"/> draws with: its own or the nearest ancestor's, else the app's.</summary>
    internal static SkUiLook Resolve(ISkUiRenderable node) =>
        (SkUiLook.AnyTreeLook ? node.TreeLook : null) ?? SkUiLook.App;

    /// <summary>Makes <paramref name="look"/> current on this thread (controls created in the scope read their defaults from it).</summary>
    internal static SkUiLookScope Enter(SkUiLook look) => new(look);

    /// <summary>Restores the look the thread used before.</summary>
    public void Dispose()
    {
        if (_entered)
            SkUiLook.Scoped = _previous;
    }
}
