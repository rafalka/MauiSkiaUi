using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>How a node draws in a skeleton made from a shimmer's content (<see cref="SkUiSkeletonScope.Resolve"/>).</summary>
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
/// A skeleton made from a shimmer's content (FR-31, C9b): a shimmer without <c>SkeletonContent</c> shows its content while
/// loading, and every node under it draws its bones instead of itself. A node finds out by walking up to its nearest shimmer
/// (<see cref="Resolve"/>) when it records or snapshots its render properties; while no shimmer shows its content as a
/// skeleton (<see cref="IsActive"/>) nothing walks, so other trees pay one static read.
/// </summary>
internal static class SkUiSkeletonScope
{
    [ThreadStatic] private static List<SkUiSkeletonBone>? _bones;
    [ThreadStatic] private static List<SKRect>? _lines;
    [ThreadStatic] private static List<ISkUiRenderable>? _children;

    /// <summary>Shimmers showing their content as a skeleton (every UI thread: interlocked).</summary>
    private static int _active;

    /// <summary>Whether a shimmer shows its content as a skeleton: only then do nodes look for one.</summary>
    public static bool IsActive => Volatile.Read(ref _active) > 0;

    /// <summary>
    /// Registers (or unregisters) a shimmer showing its content as a skeleton and has every node under it draw again
    /// (<paramref name="invalidate"/>; not on a reset, which re-records them anyway). Returns the new registration.
    /// </summary>
    public static bool SetActive(ISkUiShimmer shimmer, bool registered, bool active, bool invalidate = true)
    {
        if (registered == active)
            return registered;
        if (active)
            Interlocked.Increment(ref _active);
        else
            Interlocked.Decrement(ref _active);
        if (invalidate)
            InvalidateContent(shimmer);
        return active;
    }

    /// <summary>
    /// The nodes under <paramref name="node"/> (down to nested shimmers, which draw their own content) entered or left a
    /// skeleton, or how they draw in it changed: each records and snapshots again (once: the walk costs O(nodes)).
    /// </summary>
    public static void InvalidateContent(ISkUiRenderable node)
    {
        var children = _children ??= [];
        var start = children.Count;
        node.GetRenderChildren(children);
        var end = children.Count;
        for (var index = start; index < end; index++)
        {
            var child = children[index];
            switch (child)
            {
                case SkUiView view: view.OnSkeletonChanged(); break;
                case Core.SkUiCoreNode core: core.OnSkeletonChanged(); break;
                default: SkUiRenderInvalidation.Mark(child, SkUiRenderDirty.Content | SkUiRenderDirty.Props); break;
            }
            if (child is not ISkUiShimmer)
                InvalidateContent(child);
        }
        children.RemoveRange(start, end - start);
    }

    /// <summary>
    /// How <paramref name="node"/> draws: as it is unless its nearest shimmer shows its content as a skeleton; then by its
    /// own <see cref="SkUiSkeleton"/> value and its ancestors' (a hidden view or a bone hides what is in it; the nearest
    /// <see cref="SkUiSkeleton.Keep"/> keeps what has no value of its own), else its bones, or its chrome if it has children.
    /// </summary>
    public static SkUiSkeletonContext Resolve(ISkUiRenderable node)
    {
        if (!IsActive)
            return default;
        var inherited = SkUiSkeleton.Default;
        for (var parent = node.RenderParent; parent is not null; parent = parent.RenderParent)
        {
            if (parent is ISkUiShimmer shimmer)
                return shimmer.ShowsContentAsSkeleton ? new SkUiSkeletonContext(Decide(node, inherited), shimmer) : default;
            switch (ValueOf(parent))
            {
                case SkUiSkeleton.Bone or SkUiSkeleton.Hidden:
                    inherited = SkUiSkeleton.Hidden;
                    break;
                case SkUiSkeleton.Keep when inherited == SkUiSkeleton.Default:
                    inherited = SkUiSkeleton.Keep;
                    break;
            }
        }
        return default;
    }

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
        var container = node switch
        {
            SkUiView view => view.IsSkeletonContainer,
            Core.SkUiCoreNode core => core.IsSkeletonContainer,
            _ => true
        };
        return container ? SkUiSkeletonDraw.Container : SkUiSkeletonDraw.Bones;
    }

    /// <summary>A node's value: set on it, else its type's (scroll bars are hidden).</summary>
    private static SkUiSkeleton ValueOf(ISkUiRenderable node) => node switch
    {
        SkUiView view => view.SkeletonState?.Value ?? view.DefaultSkeleton,
        Core.SkUiCoreNode core => core.SkeletonState?.Value ?? core.DefaultSkeleton,
        _ => SkUiSkeleton.Default
    };

    /// <summary>Whether a node has render children (a view with children is a container by default).</summary>
    public static bool HasRenderChildren(ISkUiRenderable node)
    {
        var children = _children ??= [];
        var start = children.Count;
        node.GetRenderChildren(children);
        var any = children.Count > start;
        children.RemoveRange(start, children.Count - start);
        return any;
    }

    /// <summary>
    /// Applies the skeleton to a node's render properties (after its own): a hidden node is not drawn, a bone node draws its
    /// bones through a drawable (no shadow, nothing spinning or sliding), a container keeps its shadow only while its chrome
    /// draws. Forgets the node's bones once it is no longer in a skeleton.
    /// </summary>
    public static void ApplyProps(ISkUiRenderable node, ref SkUiRenderProps props, ref SkUiSkeletonState? state)
    {
        var context = Resolve(node);
        switch (context.Draw)
        {
            case SkUiSkeletonDraw.None or SkUiSkeletonDraw.Keep:
                state?.Bones.Forget();
                return;
            case SkUiSkeletonDraw.Hidden:
                state?.Bones.Forget();
                props.IsVisible = false;
                return;
            case SkUiSkeletonDraw.Container or SkUiSkeletonDraw.Children:
                state?.Bones.Forget();
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
        var block = context.Draw == SkUiSkeletonDraw.Block;
        switch (node)
        {
            case SkUiView view when block: builder.AddShape(builder.Bounds, view.PressEffectCornerRadii); break;
            case SkUiView view: view.AddSkeletonBones(builder); break;
            case Core.SkUiCoreNode core when block: builder.AddShape(builder.Bounds, core.PressEffectCornerRadii); break;
            case Core.SkUiCoreNode core: core.AddSkeletonBones(builder); break;
        }
        props.Drawable = (state ??= new SkUiSkeletonState()).Bones.Get(node, null, bones);
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

/// <summary>A node's skeleton state, made the first time it matters: its <see cref="SkUiSkeleton"/> value and its bones' drawable.</summary>
internal sealed class SkUiSkeletonState
{
    /// <summary>The value set on the node (attached property), <c>null</c> when only its bones are kept here.</summary>
    public SkUiSkeleton? Value;

    /// <summary>The bones' drawable.</summary>
    public SkUiBoneSlot Bones;
}

/// <summary>Collects a control's bones (<see cref="SkUiLook.AddSkeletonBones"/>) in its coordinates.</summary>
internal readonly struct SkUiSkeletonBuilder(List<SkUiSkeletonBone> bones, float width, float height)
{
    /// <summary>The control's rectangle.</summary>
    public SKRect Bounds => new(0, 0, width, height);

    public float Width => width;

    public float Height => height;

    /// <summary>Adds the bones of a part (the look decides).</summary>
    public void Add(SkUiSkeletonPartKind kind, SKRect bounds, CornerRadius radii = default) =>
        SkUiLook.Current.AddSkeletonBones(new SkUiSkeletonPart(kind, bounds, radii), bones);

    /// <summary>Adds the bones of a part in <paramref name="bounds"/>.</summary>
    public void Add(SkUiSkeletonPartKind kind, Rect bounds) =>
        Add(kind, new SKRect((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom));

    /// <summary>
    /// Adds the bones of a part the look sees horizontally in <paramref name="bounds"/> (x along it), turned -90° about the
    /// control's bottom-left corner as a vertical slider draws.
    /// </summary>
    public void AddTurned(SkUiSkeletonPartKind kind, SKRect bounds)
    {
        var start = bones.Count;
        Add(kind, bounds);
        for (var index = start; index < bones.Count; index++)
        {
            var bone = bones[index];
            var (left, top, right, bottom) = (bone.Bounds.Left, bone.Bounds.Top, bone.Bounds.Right, bone.Bounds.Bottom);
            var radii = bone.CornerRadius;
            bones[index] = new SkUiSkeletonBone(new SKRect(top, height - right, bottom, height - left),
                new CornerRadius(radii.TopRight, radii.BottomRight, radii.BottomLeft, radii.TopLeft));
        }
    }

    /// <summary>Adds a shape.</summary>
    public void AddShape(SKRect bounds, CornerRadius radii) => Add(SkUiSkeletonPartKind.Shape, bounds, radii);

    /// <summary>Adds a text line per box.</summary>
    public void AddLines(List<SKRect> boxes)
    {
        foreach (var box in boxes)
            Add(SkUiSkeletonPartKind.TextLine, box);
    }

    /// <summary>
    /// Adds <paramref name="count"/> placeholder lines <paramref name="spacing"/> apart (an empty label's) across the
    /// <paramref name="padding"/>-inset slot, aligned as text: full width, the last of several 60 % wide.
    /// </summary>
    public void AddPlaceholderLines(int count, float spacing, Thickness padding, TextAlignment horizontal, TextAlignment vertical, bool rightToLeft)
    {
        var available = Math.Max(0, width - padding.HorizontalThickness);
        if (count <= 0 || spacing <= 0 || available <= 0)
            return;
        var (top, gap) = SkUiTextLayout.PlaceVertically(count * spacing, count, padding, height, vertical);
        for (var line = 0; line < count; line++)
        {
            var lineWidth = (float)(count > 1 && line == count - 1 ? available * 0.6 : available);
            var left = SkUiTextLayout.LineLeft(rightToLeft ? (byte)1 : (byte)0, lineWidth, horizontal, padding, available);
            Add(SkUiSkeletonPartKind.TextLine, new SKRect(left, top, left + lineWidth, top + spacing));
            top += spacing + gap;
        }
    }
}
