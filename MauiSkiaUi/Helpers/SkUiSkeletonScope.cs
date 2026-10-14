using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>How a node draws in a skeleton made from a shimmer's content (<see cref="SkUiSkeletonScope.Resolve(ISkUiRenderable)"/>).</summary>
internal enum SkUiSkeletonDraw : byte
{
    /// <summary>Not in such a skeleton: as it is.</summary>
    None,

    /// <summary><see cref="SkUiSkeleton.Keep"/>: as it is.</summary>
    Keep,

    /// <summary>Its own bones (a control), its recording hidden.</summary>
    Bones,

    /// <summary><see cref="SkUiSkeleton.Bone"/>: one bone, its rectangle; its descendants are hidden.</summary>
    Block,

    /// <summary>A view with children: its own drawing as it is in <see cref="SkUiShimmerMode.Bones"/>, none in a silhouette.</summary>
    Container,

    /// <summary><see cref="SkUiSkeleton.Children"/>: nothing of its own.</summary>
    Children,

    /// <summary><see cref="SkUiSkeleton.Hidden"/>, or under a hidden view or a block: nothing, nor its descendants.</summary>
    Hidden
}

/// <summary>A node's place in a skeleton made from content: how it draws and the shimmer whose content it is.</summary>
internal readonly record struct SkUiSkeletonContext(SkUiSkeletonDraw Draw, ISkUiShimmer? Shimmer)
{
    /// <summary>Whether the node records its content and overlay as usual.</summary>
    public bool DrawsAsIs => Draw switch
    {
        SkUiSkeletonDraw.None or SkUiSkeletonDraw.Keep => true,
        SkUiSkeletonDraw.Container => Shimmer!.Mode == SkUiShimmerMode.Bones,
        _ => false
    };
}

/// <summary>
/// Where a node sits in a skeleton made from content: the shimmer showing that content (<c>null</c>: in none) and the
/// <see cref="SkUiSkeleton"/> its ancestors below the shimmer pass on (<see cref="SkUiSkeleton.Hidden"/> under a hidden view
/// or a bone, else <see cref="SkUiSkeleton.Keep"/> under a kept view, else <see cref="SkUiSkeleton.Default"/>).
/// </summary>
internal readonly record struct SkUiSkeletonInherited(ISkUiShimmer? Shimmer, SkUiSkeleton FromAncestors)
{
    /// <summary>The same place (by reference; no equality comparer on the per-attach path).</summary>
    public bool Is(in SkUiSkeletonInherited other) => ReferenceEquals(Shimmer, other.Shimmer) && FromAncestors == other.FromAncestors;
}

/// <summary>
/// A skeleton made from a shimmer's content (FR-31, C9b): a shimmer without <c>SkeletonContent</c> shows its content while
/// loading, and every node under it draws its bones instead of itself. Each node keeps its place in such a skeleton
/// (<see cref="SkUiSkeletonInherited"/>, in its <see cref="SkUiSkeletonState"/>), set from the top: by the shimmer when it
/// starts or stops showing its content as a skeleton (<see cref="Update"/>), when the node gets a parent
/// (<see cref="OnParentChanged"/>) and, as a safety net, when it becomes a drawn child (<see cref="SyncChildren"/>). Nodes in
/// no skeleton have no state: they pay one null check, wherever skeletons are shown.
/// </summary>
internal static class SkUiSkeletonScope
{
    [ThreadStatic] private static List<SkUiSkeletonBone>? _bones;
    [ThreadStatic] private static List<SKRect>? _lines;
    [ThreadStatic] private static List<ISkUiRenderable>? _children;

    /// <summary>
    /// The shimmer may have started or stopped showing its content as a skeleton (<see cref="ISkUiShimmer.ShowsContentAsSkeleton"/>):
    /// its content takes its place and, where that changed, draws again. Only nodes whose place changes are visited, so
    /// calling it when nothing changed costs a look at the shimmer's shown child.
    /// </summary>
    public static void Update(ISkUiShimmer shimmer) => EnterChildren(shimmer);

    /// <summary>
    /// The node got another parent (or none): it and its subtree take their place under it. Only nodes whose place
    /// changes are visited, so attaching a node outside any skeleton costs O(1).
    /// </summary>
    public static void OnParentChanged(ISkUiRenderable node) =>
        Enter(node, node.RenderParent is { } parent ? ChildPlace(parent) : default);

    /// <summary>
    /// The recorder's children pass of <paramref name="parent"/>: any child whose place is stale takes it, before it records.
    /// Parent changes already set it; this covers children drawn without one. Runs when the drawn children changed
    /// (<paramref name="listChanged"/>), and on every children pass of a parent that passes a skeleton on (a check per child),
    /// so a child is never recorded once as itself in a skeleton; other trees skip it unless their list changed.
    /// </summary>
    public static void SyncChildren(ISkUiRenderable parent, ISkUiRenderable[] children, bool listChanged)
    {
        if (children.Length == 0)
            return;
        var place = ChildPlace(parent);
        if (!listChanged && place.Shimmer is null)
            return;
        foreach (var child in children)
            if (!PlaceOf(child).Is(place))
                Enter(child, place);
    }

    /// <summary>
    /// How the nodes under <paramref name="node"/> draw changed (its <see cref="SkUiSkeleton"/> value, a shimmer's mode): the
    /// children take their places again, and those in the skeleton record and snapshot again, down to nested shimmers.
    /// </summary>
    public static void InvalidateContent(ISkUiRenderable node)
    {
        var place = ChildPlace(node);
        var children = _children ??= [];
        var start = children.Count;
        try
        {
            node.GetRenderChildren(children);
            var end = children.Count;
            for (var index = start; index < end; index++)
            {
                var child = children[index];
                if (!PlaceOf(child).Is(place))
                {
                    Enter(child, place);
                    continue;
                }
                if (place.Shimmer is null)
                    continue;
                child.OnSkeletonChanged();
                if (child is not ISkUiShimmer)
                    InvalidateContent(child);
            }
        }
        finally
        {
            children.RemoveRange(start, children.Count - start); // also when a child's handler throws: no stale scratch
        }
    }

    /// <summary>The place <paramref name="parent"/> passes to its children.</summary>
    private static SkUiSkeletonInherited ChildPlace(ISkUiRenderable parent)
    {
        if (parent is ISkUiShimmer shimmer)
            return shimmer.ShowsContentAsSkeleton ? new SkUiSkeletonInherited(shimmer, SkUiSkeleton.Default) : default;
        var place = PlaceOf(parent);
        if (place.Shimmer is null)
            return default;
        var inherited = place.FromAncestors;
        var value = ValueOf(parent);
        if (inherited == SkUiSkeleton.Hidden || value is SkUiSkeleton.Bone or SkUiSkeleton.Hidden)
            inherited = SkUiSkeleton.Hidden;
        else if (value == SkUiSkeleton.Keep)
            inherited = SkUiSkeleton.Keep;
        return place with { FromAncestors = inherited };
    }

    /// <summary>Gives <paramref name="node"/> its place and, when it changed, passes the node's place on to its subtree.</summary>
    private static void Enter(ISkUiRenderable node, SkUiSkeletonInherited place)
    {
        ref var state = ref node.SkeletonStateRef;
        if ((state?.Place ?? default).Is(place))
            return;
        (state ??= new SkUiSkeletonState()).Place = place;
        node.OnSkeletonChanged();
        EnterChildren(node);
    }

    private static void EnterChildren(ISkUiRenderable node)
    {
        var place = ChildPlace(node);
        var children = _children ??= [];
        var start = children.Count;
        try
        {
            node.GetRenderChildren(children);
            var end = children.Count;
            for (var index = start; index < end; index++)
                Enter(children[index], place);
        }
        finally
        {
            children.RemoveRange(start, children.Count - start);
        }
    }

    private static SkUiSkeletonInherited PlaceOf(ISkUiRenderable node) => node.SkeletonStateRef?.Place ?? default;

    /// <summary>
    /// How <paramref name="node"/> draws: as it is unless it is in a skeleton made from content; then by its own
    /// <see cref="SkUiSkeleton"/> value and what its ancestors pass on (a hidden view or a bone hides what is in it; the
    /// nearest <see cref="SkUiSkeleton.Keep"/> keeps what has no value of its own), else its bones, or its chrome if it has
    /// children. O(1) for nodes in no skeleton.
    /// </summary>
    public static SkUiSkeletonContext Resolve(ISkUiRenderable node) => Resolve(node, node.SkeletonStateRef);

    /// <inheritdoc cref="Resolve(ISkUiRenderable)" />
    public static SkUiSkeletonContext Resolve(ISkUiRenderable node, SkUiSkeletonState? state) =>
        state?.Place is { Shimmer: { } shimmer } place ? new SkUiSkeletonContext(Decide(node, place.FromAncestors), shimmer) : default;

    private static SkUiSkeletonDraw Decide(ISkUiRenderable node, SkUiSkeleton inherited)
    {
        if (inherited == SkUiSkeleton.Hidden)
            return SkUiSkeletonDraw.Hidden;
        switch (ValueOf(node))
        {
            case SkUiSkeleton.Hidden: return SkUiSkeletonDraw.Hidden;
            case SkUiSkeleton.Bone: return SkUiSkeletonDraw.Block;
            case SkUiSkeleton.Keep: return SkUiSkeletonDraw.Keep;
            case SkUiSkeleton.Children: return SkUiSkeletonDraw.Children;
        }
        if (inherited == SkUiSkeleton.Keep)
            return SkUiSkeletonDraw.Keep;
        return node.IsSkeletonContainer ? SkUiSkeletonDraw.Container : SkUiSkeletonDraw.Bones;
    }

    /// <summary>A node's value: set on it, else its type's (scroll bars are hidden).</summary>
    private static SkUiSkeleton ValueOf(ISkUiRenderable node) => node.SkeletonStateRef?.Value ?? node.DefaultSkeleton;

    /// <summary>Whether a node has render children (a view with children is a container by default).</summary>
    public static bool HasRenderChildren(ISkUiRenderable node)
    {
        var children = _children ??= [];
        var start = children.Count;
        try
        {
            node.GetRenderChildren(children);
            return children.Count > start;
        }
        finally
        {
            children.RemoveRange(start, children.Count - start);
        }
    }

    /// <summary>
    /// Applies the skeleton to a node's render properties (after its own): a hidden node is not drawn, a bone node draws its
    /// bones through a drawable (no shadow, nothing spinning or sliding), a container keeps its shadow only while its chrome
    /// draws. Forgets the node's bones once it is no longer in a skeleton. Callers skip nodes without a state.
    /// </summary>
    public static void ApplyProps(ISkUiRenderable node, ref SkUiRenderProps props, SkUiSkeletonState state)
    {
        var context = Resolve(node, state);
        switch (context.Draw)
        {
            case SkUiSkeletonDraw.None or SkUiSkeletonDraw.Keep:
                state.Bones.Forget();
                return;
            case SkUiSkeletonDraw.Hidden:
                state.Bones.Forget();
                props.IsVisible = false;
                return;
            case SkUiSkeletonDraw.Container or SkUiSkeletonDraw.Children:
                state.Bones.Forget();
                if (!context.DrawsAsIs)
                    props.Shadow = null;
                return;
        }
        props.Shadow = null;
        props.ContentSpinPeriod = 0;
        props.ContentSlidePeriod = 0;
        var bones = _bones ??= [];
        bones.Clear();
        var builder = new SkUiSkeletonBuilder(bones, props.Width, props.Height);
        node.AddSkeletonBones(builder, block: context.Draw == SkUiSkeletonDraw.Block);
        props.Drawable = state.Bones.Get(node, null, bones);
        bones.Clear();
    }

    /// <summary>How many font sizes wide placeholder lines measure without a width constraint.</summary>
    private const double PlaceholderEms = 10;

    /// <summary>
    /// The size of an empty label's placeholder lines: <paramref name="lines"/> lines <paramref name="spacing"/> apart, as wide
    /// as the constraint (without one, ten font sizes), with its padding.
    /// </summary>
    public static Size PlaceholderSize(int lines, float spacing, double fontSize, Thickness padding, double widthConstraint)
    {
        var width = double.IsFinite(widthConstraint) ? widthConstraint : PlaceholderEms * fontSize + padding.HorizontalThickness;
        return new Size(Math.Max(0, width), lines * spacing + padding.VerticalThickness);
    }

    /// <summary>Scratch for text line boxes (UI thread).</summary>
    public static List<SKRect> LineScratch()
    {
        var lines = _lines ??= [];
        lines.Clear();
        return lines;
    }
}

/// <summary>A node's skeleton state, made the first time it matters: its <see cref="SkUiSkeleton"/> value, its place in a skeleton and its bones' drawable.</summary>
internal sealed class SkUiSkeletonState
{
    /// <summary>The value set on the node (attached property), <c>null</c> when only its bones are kept here.</summary>
    public SkUiSkeleton? Value;

    /// <summary>Its place in a skeleton made from content (<c>default</c>: in none).</summary>
    public SkUiSkeletonInherited Place;

    /// <summary>Whether the node is in a skeleton made from content.</summary>
    public bool InSkeleton => Place.Shimmer is not null;

    /// <summary>The bones' drawable.</summary>
    public SkUiBoneSlot Bones;
}

/// <summary>
/// Collects the bones a control stands for in a skeleton made from a shimmer's content, in its coordinates: what an
/// override of <see cref="SkUiView.AddSkeletonBones"/> or <see cref="Core.SkUiCoreNode.AddSkeletonBones"/> adds (a custom
/// control's text lines, shapes, tracks). Parts go through the look (<see cref="SkUiLook.AddSkeletonBones"/>), which turns
/// them into bones; the shimmer draws them with its effect.
/// </summary>
public readonly struct SkUiSkeletonBuilder
{
    private readonly List<SkUiSkeletonBone> _bones;
    private readonly float _width;
    private readonly float _height;

    internal SkUiSkeletonBuilder(List<SkUiSkeletonBone> bones, float width, float height)
    {
        _bones = bones;
        _width = width;
        _height = height;
    }

    /// <summary>The control's rectangle, at its origin.</summary>
    public SKRect Bounds => new(0, 0, _width, _height);

    /// <summary>The control's width.</summary>
    public float Width => _width;

    /// <summary>The control's height.</summary>
    public float Height => _height;

    /// <summary>Adds the bones of a part in <paramref name="bounds"/> (the look decides: a text line's bar, a switch's pill, a shape as it is).</summary>
    public void Add(SkUiSkeletonPartKind kind, SKRect bounds, CornerRadius radii = default) =>
        SkUiLook.Current.AddSkeletonBones(new SkUiSkeletonPart(kind, bounds, radii), _bones);

    /// <summary>Adds the bones of a part in <paramref name="bounds"/>.</summary>
    public void Add(SkUiSkeletonPartKind kind, Rect bounds) =>
        Add(kind, new SKRect((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom));

    /// <summary>
    /// Adds the bones of a part the look sees horizontally in <paramref name="bounds"/> (x along it), turned -90° about the
    /// control's bottom-left corner as a vertical slider draws.
    /// </summary>
    internal void AddTurned(SkUiSkeletonPartKind kind, SKRect bounds)
    {
        var start = _bones.Count;
        Add(kind, bounds);
        for (var index = start; index < _bones.Count; index++)
        {
            var bone = _bones[index];
            var (left, top, right, bottom) = (bone.Bounds.Left, bone.Bounds.Top, bone.Bounds.Right, bone.Bounds.Bottom);
            var radii = bone.CornerRadius;
            _bones[index] = new SkUiSkeletonBone(new SKRect(top, _height - right, bottom, _height - left),
                new CornerRadius(radii.TopRight, radii.BottomRight, radii.BottomLeft, radii.TopLeft));
        }
    }

    /// <summary>Adds a shape: a rounded rectangle with <paramref name="radii"/>.</summary>
    public void AddShape(SKRect bounds, CornerRadius radii) => Add(SkUiSkeletonPartKind.Shape, bounds, radii);

    /// <summary>Adds a text line per box.</summary>
    internal void AddLines(List<SKRect> boxes)
    {
        foreach (var box in boxes)
            Add(SkUiSkeletonPartKind.TextLine, box);
    }

    /// <summary>
    /// Adds <paramref name="count"/> placeholder lines <paramref name="spacing"/> apart (an empty label's) across the
    /// <paramref name="padding"/>-inset slot, aligned as text: full width, the last of several 60 % wide.
    /// </summary>
    internal void AddPlaceholderLines(int count, float spacing, Thickness padding, TextAlignment horizontal, TextAlignment vertical, bool rightToLeft)
    {
        var available = Math.Max(0, _width - padding.HorizontalThickness);
        if (count <= 0 || spacing <= 0 || available <= 0)
            return;
        var (top, gap) = SkUiTextLayout.PlaceVertically(count * spacing, count, padding, _height, vertical);
        for (var line = 0; line < count; line++)
        {
            var lineWidth = (float)(count > 1 && line == count - 1 ? available * 0.6 : available);
            var left = SkUiTextLayout.LineLeft(rightToLeft ? (byte)1 : (byte)0, lineWidth, horizontal, padding, available);
            Add(SkUiSkeletonPartKind.TextLine, new SKRect(left, top, left + lineWidth, top + spacing));
            top += spacing + gap;
        }
    }
}
