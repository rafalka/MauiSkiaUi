using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MauiSkiaUi;

/// <summary>The property a <see cref="SkUiScrollEffect"/> drives.</summary>
public enum SkUiScrollEffectProperty
{
    /// <summary>Moves the view across, in DIPs, added to its own <c>TranslationX</c>.</summary>
    TranslationX,
    /// <summary>Moves the view down, in DIPs, added to its own <c>TranslationY</c>.</summary>
    TranslationY,
    /// <summary>Fades the view: a factor on its own opacity (0: gone, not hit, hidden from screen readers).</summary>
    Opacity,

    /// <summary>Both scales, about the view's anchor.</summary>
    Scale,
    /// <summary>Scales the view across, about its anchor: a factor on its own scale.</summary>
    ScaleX,
    /// <summary>Scales the view down, about its anchor: a factor on its own scale.</summary>
    ScaleY,

    /// <summary>Clips the view to this many DIPs down (<see cref="SkUiScrollEffect.ClipEdge"/> says which part stays).</summary>
    ClipHeight,

    /// <summary>Clips the view to this many DIPs across.</summary>
    ClipWidth
}

/// <summary>Which part of a view a <see cref="SkUiScrollEffectProperty.ClipHeight"/> / <see cref="SkUiScrollEffectProperty.ClipWidth"/> keeps.</summary>
public enum SkUiClipEdge
{
    /// <summary>The top, or the leading side (left in left to right, right in right to left).</summary>
    Start,

    /// <summary>The bottom, or the trailing side.</summary>
    End,
    /// <summary>Centered: both sides go by half.</summary>
    Center
}

/// <summary>The corners of a scroll-linked clip.</summary>
public enum SkUiClipShape
{
    /// <summary>Square corners.</summary>
    Rectangle,

    /// <summary>Corners of <see cref="SkUiScrollEffect.ClipCornerRadius"/>, at most half the clip's shorter side.</summary>
    RoundRectangle,

    /// <summary>Corners of half the clip's shorter side at every size: a pill stays a pill while it shrinks.</summary>
    Capsule
}

/// <summary>What a <see cref="SkUiScrollEffect"/> does while the system asks for reduced motion (<see cref="SkUiMotion.IsMotionReduced"/>).</summary>
public enum SkUiScrollEffectReducedMotion
{
    /// <summary>
    /// Fades and clips run; scales, and translations that do not move exactly with the content (parallax), hold
    /// <see cref="SkUiScrollEffect.From"/>; a triggered reveal's show and hide jump for translation and scale.
    /// </summary>
    Default,

    /// <summary>Runs as with motion.</summary>
    Run,

    /// <summary>Holds <see cref="SkUiScrollEffect.From"/>.</summary>
    HoldFrom
}

/// <summary>
/// A scroller a <see cref="SkUiScrollEffect"/> or <see cref="SkUiScrollReveal"/> can follow: the scroll views of both layers,
/// <see cref="SkUiCollectionView"/> (its list's scroller) and <see cref="SkUiRefreshView"/> (the scroller it pulls).
/// Implemented by SkiaUi's scrolling views only.
/// </summary>
public interface ISkUiScrollEffectSource
{
    /// <summary>The scroller that drives effects; <c>null</c> while there is none (a refresh view without a scrolling child).</summary>
    internal ISkUiScrollHost? ScrollEffectHost { get; }
}

/// <summary>
/// Ties one property of a view to a scroller, evaluated on the render thread every frame: a header that collapses as a
/// list scrolls, a parallax picture, a toolbar that fades in, a header that grows when the list is pulled past its top, and,
/// with a <see cref="Reveal"/>, views that hide while the list scrolls one way and come back when it scrolls the other.
/// Nothing is recorded again while the scroller moves, and the view follows render-thread flings.
/// </summary>
/// <remarks>
/// <para>Add it to <see cref="SkUiView.ScrollEffects"/> or with <see cref="Core.SkUiCoreNode.AddScrollEffect"/>; it belongs to one
/// view at a time. The output is <see cref="From"/> at <see cref="InputStart"/> and before, <see cref="To"/> at
/// <see cref="InputEnd"/> and after, through <see cref="Easing"/> in between. Offsets include the overscroll shown (negative
/// past the start), so a range below 0 follows a pull. Several effects combine: translations add, scales and opacities
/// multiply, clips take the smaller; with the view's own values they combine the same way.</para>
/// <para>Clips do not change layout: the view keeps its size and its neighbors stay where they are. A collapsing header sits
/// over the scroller, whose <c>ViewportInsets</c> (or its content's padding) leave the header's expanded size.</para>
/// <para>Values are read when the view next commits a frame; a change never records the view again. Not a bindable object:
/// set it from XAML or code, and replace it to change it from a binding.</para>
/// </remarks>
public sealed class SkUiScrollEffect : INotifyPropertyChanged
{
    /// <summary>The number of points an easing is sampled at for the render thread.</summary>
    internal const int CurveSamples = 33;

    private static readonly PropertyChangedEventArgs SourceChangedArgs = new(nameof(Source));
    private static readonly PropertyChangedEventArgs OrientationChangedArgs = new(nameof(Orientation));
    private static readonly PropertyChangedEventArgs InputStartChangedArgs = new(nameof(InputStart));
    private static readonly PropertyChangedEventArgs InputEndChangedArgs = new(nameof(InputEnd));
    private static readonly PropertyChangedEventArgs RevealChangedArgs = new(nameof(Reveal));
    private static readonly PropertyChangedEventArgs PropertyChangedArgs = new(nameof(Property));
    private static readonly PropertyChangedEventArgs FromChangedArgs = new(nameof(From));
    private static readonly PropertyChangedEventArgs ToChangedArgs = new(nameof(To));
    private static readonly PropertyChangedEventArgs EasingChangedArgs = new(nameof(Easing));
    private static readonly PropertyChangedEventArgs ClipEdgeChangedArgs = new(nameof(ClipEdge));
    private static readonly PropertyChangedEventArgs ClipShapeChangedArgs = new(nameof(ClipShape));
    private static readonly PropertyChangedEventArgs ClipCornerRadiusChangedArgs = new(nameof(ClipCornerRadius));
    private static readonly PropertyChangedEventArgs ReducedMotionChangedArgs = new(nameof(ReducedMotion));
    private static readonly PropertyChangedEventArgs IsEnabledChangedArgs = new(nameof(IsEnabled));
    private static readonly PropertyChangedEventArgs SnapsToEndsChangedArgs = new(nameof(SnapsToEnds));

    /// <summary>A <see cref="SkUiScrollEffectProperty.TranslationY"/> effect with no range yet: set its properties.</summary>
    public SkUiScrollEffect() { }

    /// <summary>An effect on <paramref name="property"/> from <paramref name="from"/> to <paramref name="to"/> over the offsets <paramref name="inputStart"/>…<paramref name="inputEnd"/>.</summary>
    public SkUiScrollEffect(SkUiScrollEffectProperty property, double inputStart, double inputEnd, double from, double to)
    {
        Property = property;
        InputStart = inputStart;
        InputEnd = inputEnd;
        From = from;
        To = to;
    }

    /// <summary>Raised when a property changes (the view builds its links again on its next commit).</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The scroller that drives it; <c>null</c> (default): the view's nearest scrolling ancestor. Ignored with a <see cref="Reveal"/>, which has its own.</summary>
    public ISkUiScrollEffectSource? Source
    {
        get;
        set => Set(ref field, value, SourceChangedArgs);
    }

    /// <summary>Which offset drives it: <see cref="ScrollOrientation.Vertical"/> (default) or <see cref="ScrollOrientation.Horizontal"/>.</summary>
    public ScrollOrientation Orientation
    {
        get;
        set
        {
            if (value is not (ScrollOrientation.Vertical or ScrollOrientation.Horizontal))
                throw new ArgumentOutOfRangeException(nameof(value), value, "A scroll effect follows one axis: Vertical or Horizontal.");
            Set(ref field, value, OrientationChangedArgs);
        }
    } = ScrollOrientation.Vertical;

    /// <summary>The offset (DIPs) at which the output is <see cref="From"/>; may be negative (a pull past the start).</summary>
    public double InputStart
    {
        get;
        set => Set(ref field, Finite(value), InputStartChangedArgs);
    }

    /// <summary>The offset (DIPs) at which the output is <see cref="To"/>; equal to <see cref="InputStart"/>: a step there.</summary>
    public double InputEnd
    {
        get;
        set => Set(ref field, Finite(value), InputEndChangedArgs);
    }

    /// <summary>
    /// A scroll-direction input instead of the offset: its progress, 0 (shown) to 1 (hidden), maps <see cref="From"/> (shown)
    /// to <see cref="To"/> (hidden). Several effects can share one, so they move as one.
    /// </summary>
    public SkUiScrollReveal? Reveal
    {
        get;
        set => Set(ref field, value, RevealChangedArgs);
    }

    /// <summary>The property it drives (default <see cref="SkUiScrollEffectProperty.TranslationY"/>).</summary>
    public SkUiScrollEffectProperty Property
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
            Set(ref field, value, PropertyChangedArgs);
        }
    } = SkUiScrollEffectProperty.TranslationY;

    /// <summary>The output at <see cref="InputStart"/> and before (DIPs for translations and clips, a factor for scales and opacity).</summary>
    public double From
    {
        get;
        set => Set(ref field, Finite(value), FromChangedArgs);
    }

    /// <summary>The output at <see cref="InputEnd"/> and after.</summary>
    public double To
    {
        get;
        set => Set(ref field, Finite(value), ToChangedArgs);
    }

    /// <summary>The curve between the two (linear when <c>null</c>), sampled at 33 points for the render thread.</summary>
    public Easing? Easing
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
                return;
            field = value;
            Curve = Sample(value);
            PropertyChanged?.Invoke(this, EasingChangedArgs);
        }
    }

    /// <summary>For a clip: which part of the view stays (default <see cref="SkUiClipEdge.Start"/>: the top or leading part).</summary>
    public SkUiClipEdge ClipEdge
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
            Set(ref field, value, ClipEdgeChangedArgs);
        }
    }

    /// <summary>For a clip: its corners (default <see cref="SkUiClipShape.Rectangle"/>).</summary>
    public SkUiClipShape ClipShape
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
            Set(ref field, value, ClipShapeChangedArgs);
        }
    }

    /// <summary>For a <see cref="SkUiClipShape.RoundRectangle"/> clip: the corner radius in DIPs (at least 0).</summary>
    public double ClipCornerRadius
    {
        get;
        set
        {
            if (!double.IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "A corner radius is a finite number of DIPs, at least 0.");
            Set(ref field, value, ClipCornerRadiusChangedArgs);
        }
    }

    /// <summary>What it does under reduced motion (default <see cref="SkUiScrollEffectReducedMotion.Default"/>).</summary>
    public SkUiScrollEffectReducedMotion ReducedMotion
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
            Set(ref field, value, ReducedMotionChangedArgs);
        }
    }

    /// <summary>Off: the property shows the view's own value.</summary>
    public bool IsEnabled
    {
        get;
        set => Set(ref field, value, IsEnabledChangedArgs);
    } = true;

    /// <summary>
    /// A drag, fling or wheel scroll never comes to rest strictly between <see cref="InputStart"/> and <see cref="InputEnd"/>:
    /// it settles on the nearer end, so a collapsing header ends fully open or fully collapsed (default off). Works with or
    /// without the scroller's snap points; a programmatic scroll is left where it goes. Offset effects only (a reveal settles
    /// by itself: <see cref="SkUiScrollReveal.SettlesWhenIdle"/>).
    /// </summary>
    public bool SnapsToEnds
    {
        get;
        set => Set(ref field, value, SnapsToEndsChangedArgs);
    }

    /// <summary>The easing sampled for the render thread; <c>null</c> = linear.</summary>
    internal float[]? Curve { get; private set; }

    /// <summary>The view (SkUi* or Core) it belongs to; <c>null</c> = none.</summary>
    internal object? Owner { get; set; }

    /// <summary>Sets <see cref="Source"/>.</summary>
    public SkUiScrollEffect SetSource(ISkUiScrollEffectSource? value) { Source = value; return this; }

    /// <summary>Sets <see cref="Orientation"/>.</summary>
    public SkUiScrollEffect SetOrientation(ScrollOrientation value) { Orientation = value; return this; }

    /// <summary>Sets <see cref="InputStart"/> and <see cref="InputEnd"/>.</summary>
    public SkUiScrollEffect SetInput(double start, double end) { InputStart = start; InputEnd = end; return this; }

    /// <summary>Sets <see cref="Reveal"/>.</summary>
    public SkUiScrollEffect SetReveal(SkUiScrollReveal? value) { Reveal = value; return this; }

    /// <summary>Sets <see cref="Property"/>.</summary>
    public SkUiScrollEffect SetProperty(SkUiScrollEffectProperty value) { Property = value; return this; }

    /// <summary>Sets <see cref="From"/> and <see cref="To"/>.</summary>
    public SkUiScrollEffect SetOutput(double from, double to) { From = from; To = to; return this; }

    /// <summary>Sets <see cref="Easing"/>.</summary>
    public SkUiScrollEffect SetEasing(Easing? value) { Easing = value; return this; }

    /// <summary>Sets <see cref="ClipEdge"/>.</summary>
    public SkUiScrollEffect SetClipEdge(SkUiClipEdge value) { ClipEdge = value; return this; }

    /// <summary>Sets <see cref="ClipShape"/> and <see cref="ClipCornerRadius"/>.</summary>
    public SkUiScrollEffect SetClipShape(SkUiClipShape value, double cornerRadius = 0) { ClipShape = value; ClipCornerRadius = cornerRadius; return this; }

    /// <summary>Sets <see cref="ReducedMotion"/>.</summary>
    public SkUiScrollEffect SetReducedMotion(SkUiScrollEffectReducedMotion value) { ReducedMotion = value; return this; }

    /// <summary>Sets <see cref="SnapsToEnds"/>.</summary>
    public SkUiScrollEffect SetSnapsToEnds(bool value) { SnapsToEnds = value; return this; }

    /// <summary>Sets <see cref="IsEnabled"/>.</summary>
    public SkUiScrollEffect SetIsEnabled(bool value) { IsEnabled = value; return this; }

    /// <summary>
    /// A view outside the scroller (a header picture) moving up at <paramref name="rate"/> times the scroll speed: 0.5 is half
    /// the speed of the content. Vertical; <paramref name="range"/> is how far it follows.
    /// </summary>
    public static SkUiScrollEffect Parallax(double rate, double range = 100_000) =>
        new(SkUiScrollEffectProperty.TranslationY, 0, range, 0, -rate * range);

    /// <summary>A view fading in between the offsets <paramref name="start"/> and <paramref name="end"/> (a toolbar's title).</summary>
    public static SkUiScrollEffect FadeIn(double start, double end) => new(SkUiScrollEffectProperty.Opacity, start, end, 0, 1);

    /// <summary>
    /// A header clipped from <paramref name="expanded"/> to <paramref name="collapsed"/> DIPs tall over the first
    /// <c>expanded − collapsed</c> DIPs of scroll, keeping its top: the content under it moves up by as much.
    /// </summary>
    public static SkUiScrollEffect CollapseTo(double expanded, double collapsed) =>
        new(SkUiScrollEffectProperty.ClipHeight, 0, Math.Max(0, expanded - collapsed), expanded, collapsed);

    /// <summary>
    /// A view moved by <paramref name="translationY"/> DIPs (positive: down, out at the bottom; negative: up) as
    /// <paramref name="reveal"/> hides it: a floating button, a bottom bar, a top bar.
    /// </summary>
    public static SkUiScrollEffect HideOnScroll(SkUiScrollReveal reveal, double translationY) =>
        new SkUiScrollEffect { Property = SkUiScrollEffectProperty.TranslationY, From = 0, To = translationY }.SetReveal(reveal);

    /// <summary>Whether it is a clip.</summary>
    internal bool IsClip => Property is SkUiScrollEffectProperty.ClipHeight or SkUiScrollEffectProperty.ClipWidth;

    private static double Finite(double value, [CallerArgumentExpression(nameof(value))] string? name = null) =>
        double.IsFinite(value) ? value : throw new ArgumentOutOfRangeException(name, value, "A scroll effect's values are finite numbers.");

    private void Set<T>(ref T field, T value, PropertyChangedEventArgs args)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, args);
    }

    private static float[]? Sample(Easing? easing)
    {
        if (easing is null || ReferenceEquals(easing, Easing.Linear))
            return null;
        var curve = new float[CurveSamples];
        for (var index = 0; index < CurveSamples; index++)
            curve[index] = (float)easing.Ease((double)index / (CurveSamples - 1));
        return curve;
    }
}
