using MauiSkiaUi.Rendering;

namespace MauiSkiaUi.Core;

public partial class SkUiCoreNode
{
    private SkUiScrollEffectSet? _scrollEffects;

    /// <summary>This node's scroll effects (<see cref="AddScrollEffect"/>), in the order added.</summary>
    public IReadOnlyList<SkUiScrollEffect> ScrollEffects => (IReadOnlyList<SkUiScrollEffect>?)_scrollEffects?.Effects ?? [];

    /// <summary>
    /// Ties this node's translation, scale, opacity or a clip to a scroller, on the render thread (<see cref="SkUiScrollEffect"/>):
    /// collapsing headers, parallax, fades, nodes that hide by scroll direction. The scroller can be a Core or a SkUi* one
    /// (through the <see cref="SkUiCoreHost"/>).
    /// </summary>
    public SkUiCoreNode AddScrollEffect(SkUiScrollEffect effect)
    {
        (_scrollEffects ??= new SkUiScrollEffectSet(this, () => InvalidateRender(SkUiRenderDirty.Props), () => IsRightToLeft)).Effects.Add(effect);
        return this;
    }

    /// <summary>Adds several effects (<see cref="AddScrollEffect"/>).</summary>
    public SkUiCoreNode AddScrollEffects(params ReadOnlySpan<SkUiScrollEffect> effects)
    {
        foreach (var effect in effects)
            AddScrollEffect(effect);
        return this;
    }

    /// <summary>Removes <paramref name="effect"/>; <c>false</c> when it was not on this node.</summary>
    public bool RemoveScrollEffect(SkUiScrollEffect effect) => _scrollEffects?.Effects.Remove(effect) ?? false;
}
