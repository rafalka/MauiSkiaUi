using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

public partial class SkUiView
{
    private SkUiScrollEffectSet? _scrollEffects;

    /// <summary>
    /// Ties this view's translation, scale, opacity or a clip to a scroller, on the render thread (<see cref="SkUiScrollEffect"/>):
    /// collapsing headers, parallax, fades, views that hide by scroll direction. Created on first read; a view without
    /// effects pays one field.
    /// </summary>
    public IList<SkUiScrollEffect> ScrollEffects => ScrollEffectSet.Effects;

    private SkUiScrollEffectSet ScrollEffectSet =>
        _scrollEffects ??= new SkUiScrollEffectSet(this, () => InvalidateRender(SkUiRenderDirty.Props), () => IsRightToLeft);

    /// <summary>Adds <paramref name="effect"/> to <see cref="ScrollEffects"/>.</summary>
    public SkUiView AddScrollEffect(SkUiScrollEffect effect)
    {
        ScrollEffects.Add(effect);
        return this;
    }

    /// <summary>Adds several effects (<see cref="AddScrollEffect"/>).</summary>
    public SkUiView AddScrollEffects(params ReadOnlySpan<SkUiScrollEffect> effects)
    {
        foreach (var effect in effects)
            ScrollEffects.Add(effect);
        return this;
    }

    /// <summary>Removes <paramref name="effect"/> from <see cref="ScrollEffects"/>; <c>false</c> when it was not there.</summary>
    public bool RemoveScrollEffect(SkUiScrollEffect effect) => _scrollEffects?.Effects.Remove(effect) ?? false;
}
