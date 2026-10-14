namespace MauiSkiaUi.Rendering;

/// <summary>
/// Composite-time property values driven by the render clock (<see cref="SkUiRenderProps.Timeline"/>): each property goes
/// from its <c>from</c> to its <c>to</c> value in one pass of <see cref="PassSeconds"/>, along <see cref="Easing"/>, then
/// starts again (or comes back, <see cref="Reverse"/>), forever. The position follows the shared clock
/// (<see cref="SkUiRenderClock"/>), not the moment the node was committed, so every node with the same timing is in phase
/// on every surface. Evaluated when the node is drawn: nothing is recorded or committed per frame, and a node that is not
/// drawn requests no frames.
/// </summary>
/// <remarks>Immutable; owns no native objects. The easing runs on the render thread (MAUI's easings are pure functions).</remarks>
internal sealed class SkUiRenderTimeline
{
    /// <summary>The properties a timeline may drive: the node's placement and opacities (not the children's offsets or scale).</summary>
    private const int AllowedMask = (1 << (int)SkUiRenderProperty.TranslationX) | (1 << (int)SkUiRenderProperty.TranslationY)
        | (1 << (int)SkUiRenderProperty.Rotation) | (1 << (int)SkUiRenderProperty.ScaleX) | (1 << (int)SkUiRenderProperty.ScaleY)
        | (1 << (int)SkUiRenderProperty.Opacity) | (1 << (int)SkUiRenderProperty.ChildrenOpacity);

    private readonly SkUiRenderProperty[] _properties;
    private readonly float[] _from;
    private readonly float[] _to;

    /// <param name="properties">The driven properties (placement and opacities only).</param>
    /// <param name="from">Each property's value at the start of a pass.</param>
    /// <param name="to">Each property's value at the end of a pass.</param>
    /// <param name="passSeconds">One pass; positive.</param>
    /// <param name="reverse">Every other pass runs backwards (there and back) instead of starting again.</param>
    /// <param name="easing">The curve of a pass; <c>null</c>: linear.</param>
    public SkUiRenderTimeline(SkUiRenderProperty[] properties, float[] from, float[] to, double passSeconds, bool reverse, Easing? easing)
    {
        if (properties.Length == 0 || from.Length != properties.Length || to.Length != properties.Length)
            throw new ArgumentException("Every property needs a start and an end value.", nameof(properties));
        foreach (var property in properties)
            if ((AllowedMask & (1 << (int)property)) == 0)
                throw new ArgumentOutOfRangeException(nameof(properties), property, "A timeline drives the node's placement and opacities only.");
        if (!double.IsFinite(passSeconds) || passSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(passSeconds), passSeconds, "A pass must be finite and positive.");
        _properties = properties;
        _from = from;
        _to = to;
        PassSeconds = passSeconds;
        Reverse = reverse;
        Easing = easing ?? Easing.Linear;
    }

    public double PassSeconds { get; }

    public bool Reverse { get; }

    public Easing Easing { get; }

    /// <summary>The eased position within the current pass at <paramref name="now"/>, 0–1.</summary>
    public float Progress(TimeSpan now)
    {
        var passes = now.TotalSeconds / PassSeconds;
        double position;
        if (Reverse)
        {
            var cycle = passes % 2;
            position = cycle <= 1 ? cycle : 2 - cycle;
        }
        else
        {
            position = passes % 1;
        }
        return (float)Easing.Ease(position);
    }

    /// <summary>Writes every driven property's value at <paramref name="now"/> into <paramref name="props"/>.</summary>
    public void Apply(ref SkUiRenderProps props, TimeSpan now)
    {
        var progress = Progress(now);
        for (var index = 0; index < _properties.Length; index++)
            props.Set(_properties[index], _from[index] + (_to[index] - _from[index]) * progress);
    }
}
