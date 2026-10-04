using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn node as assistive technologies see it (<see cref="SkUiView"/> or a Core node): what it reports, how it is
/// focused and what it does when activated.
/// </summary>
internal interface ISkUiAccessibleNode : ISkUiRenderable
{
    /// <summary>Fills <paramref name="info"/> (already reset): role, text, state and actions, then description, hint and heading.</summary>
    void GetSemantics(SkUiSemanticsInfo info);

    /// <summary>Performs one action (<see cref="SkUiSemanticsActions"/> flag); returns whether it was done.</summary>
    bool PerformSemanticsAction(SkUiSemanticsActions action);

    /// <summary>Sets a range value (a slider moved by TalkBack or Narrator); returns whether it was done.</summary>
    bool SetSemanticsValue(double value);

    /// <summary>Whether the node takes keyboard focus now (visible, enabled, a tab stop, interactive).</summary>
    bool IsKeyboardFocusable { get; }

    /// <summary>Tab order key: lower first, ties in tree order (MAUI's <c>TabIndex</c>).</summary>
    int TabIndex { get; }

    /// <summary>Whether the node has keyboard focus.</summary>
    bool IsFocused { get; }

    /// <summary>Applies a focus change from the surface's focus manager: the focused state and whether the ring shows.</summary>
    void SetKeyboardFocus(bool focused, bool ringVisible);
}

/// <summary>One element of a surface's semantics tree; positions are in surface (root) DIPs.</summary>
internal sealed class SkUiSemanticsNode
{
    /// <summary>Id of the synthetic node for the surface itself.</summary>
    public const int RootId = 0;

    public int Id;
    /// <summary>The drawn node it describes; <c>null</c> for the root.</summary>
    public ISkUiAccessibleNode? Source;
    public SkUiSemanticsNode? Parent;
    public readonly List<SkUiSemanticsNode> Children = [];
    /// <summary>The visible part of the element (clipped by ancestors and scroll viewports), surface DIPs.</summary>
    public Rect Bounds;
    public SkUiSemanticsRole Role;
    /// <summary>What is read as the name: the description, else the element's own text and merged child text.</summary>
    public string? Label;
    public string? Hint;
    public string? Value;
    public SkUiCheckState? CheckState;
    public SkUiSemanticsRange? Range;
    public bool IsEnabled = true;
    public SkUiSemanticsActions Actions;
    public bool IsHorizontal;
    public SemanticHeadingLevel HeadingLevel;
    public string? AutomationId;
    public bool IsFocusable;
    public bool IsFocused;
    /// <summary>A hosted native view: not read through this tree (the platform reads the native view).</summary>
    public bool IsNative;
    /// <summary><see cref="Bounds"/> and every descendant's (set for hit-testing: subtrees away from the point are skipped).</summary>
    internal Rect Extent;

    /// <summary>Actions that do something now (none while disabled).</summary>
    public SkUiSemanticsActions EnabledActions => IsEnabled ? Actions : SkUiSemanticsActions.None;

    /// <summary>Whether everything but the children equals <paramref name="other"/>'s.</summary>
    public bool ContentEquals(SkUiSemanticsNode other) =>
        Id == other.Id && ReferenceEquals(Source, other.Source) && Bounds == other.Bounds && Role == other.Role && Label == other.Label
        && Hint == other.Hint && Value == other.Value && CheckState == other.CheckState && Range == other.Range && IsEnabled == other.IsEnabled
        && Actions == other.Actions && IsHorizontal == other.IsHorizontal && HeadingLevel == other.HeadingLevel
        && AutomationId == other.AutomationId && IsFocusable == other.IsFocusable && IsFocused == other.IsFocused && IsNative == other.IsNative;

    /// <summary>The value read for a range element without an explicit one: a percentage (as native sliders).</summary>
    public string? DisplayValue => Value ?? (Range is { } range ? range.Fraction.ToString("P0", System.Globalization.CultureInfo.CurrentCulture) : null);

    public override string ToString() => $"#{Id} {Role} \"{Label}\" {Bounds}";
}

/// <summary>
/// The semantics tree of one surface, built from the drawn tree (both layers): which drawn nodes are elements, what they
/// read and do, and where they are. Rules, after native platforms:
/// <list type="bullet">
/// <item>Controls are elements; text is an element when it has text; images and plain containers only with a description,
/// a heading level or an action (<c>AutomationProperties.IsInAccessibleTree</c> forces either way;
/// <c>ExcludedWithChildren</c> drops the subtree).</item>
/// <item>An actionable element (a tappable card, a button) reads the text of its non-actionable descendants as its name,
/// and they are not elements of their own (like Android's merged clickable groups); actionable descendants stay separate
/// elements. A description replaces the text, and then descendant text is not read at all.</item>
/// <item>Order is the drawn tree's (children in paint order). Bounds are the visible part: elements scrolled or clipped
/// out of view are left out (scroll actions bring them in).</item>
/// </list>
/// </summary>
internal sealed class SkUiSemanticsTree
{
    private static int _nextId;
    private readonly Dictionary<int, SkUiSemanticsNode> _byId = [];

    private SkUiSemanticsTree(SkUiSemanticsNode root) => Root = root;

    /// <summary>A tree with no elements (a surface whose bridge went away).</summary>
    public static SkUiSemanticsTree Empty { get; } = CreateEmpty();

    private static SkUiSemanticsTree CreateEmpty()
    {
        var root = new SkUiSemanticsNode { Id = SkUiSemanticsNode.RootId, Label = string.Empty };
        var tree = new SkUiSemanticsTree(root);
        tree._byId[root.Id] = root;
        return tree;
    }

    /// <summary>The surface (synthetic, <see cref="SkUiSemanticsNode.RootId"/>); its children are the top-level elements.</summary>
    public SkUiSemanticsNode Root { get; }

    /// <summary>Every element, by id (the root included).</summary>
    public IReadOnlyDictionary<int, SkUiSemanticsNode> Nodes => _byId;

    /// <summary>The element with <paramref name="id"/>, or <c>null</c>.</summary>
    public SkUiSemanticsNode? Find(int id) => _byId.GetValueOrDefault(id);

    /// <summary>The element of <paramref name="source"/>, or <c>null</c> when it is not one (merged, hidden, not drawn).</summary>
    public SkUiSemanticsNode? Find(ISkUiAccessibleNode source) =>
        source.RenderState.SemanticsId is var id and > 0 && Find(id) is { } node && ReferenceEquals(node.Source, source) ? node : null;

    /// <summary>The front-most, innermost element at <paramref name="point"/> (surface DIPs), or <c>null</c> (the surface).</summary>
    public SkUiSemanticsNode? HitTest(Point point)
    {
        // The tree does not change once built: extents are computed on the first hit-test (hover asks many times).
        if (!_hasExtents)
        {
            ComputeExtent(Root);
            _hasExtents = true;
        }
        return HitTest(Root, point);
    }

    private bool _hasExtents;

    private static Rect ComputeExtent(SkUiSemanticsNode node)
    {
        var extent = node.Bounds;
        foreach (var child in node.Children)
            if (ComputeExtent(child) is { IsEmpty: false } inner)
                extent = extent.IsEmpty ? inner : extent.Union(inner);
        return node.Extent = extent;
    }

    private static SkUiSemanticsNode? HitTest(SkUiSemanticsNode node, Point point)
    {
        for (var index = node.Children.Count - 1; index >= 0; index--)
        {
            var child = node.Children[index];
            if (!child.Extent.Contains(point))
                continue;
            if (child.Bounds.Contains(point))
                return HitTest(child, point) ?? child;
            // A child that does not clip may hold elements outside its own bounds.
            if (HitTest(child, point) is { } inner)
                return inner;
        }
        return null;
    }

    /// <summary>Every element in reading order (depth first), the root excluded.</summary>
    public IEnumerable<SkUiSemanticsNode> Flatten()
    {
        var stack = new Stack<SkUiSemanticsNode>();
        for (var index = Root.Children.Count - 1; index >= 0; index--)
            stack.Push(Root.Children[index]);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            for (var index = node.Children.Count - 1; index >= 0; index--)
                stack.Push(node.Children[index]);
        }
    }

    /// <summary>
    /// Compares with an older tree of the same surface: whether elements were added, removed or reordered, and the ids
    /// whose content changed (in both trees).
    /// </summary>
    public (bool StructureChanged, List<int> Changed) Diff(SkUiSemanticsTree? previous)
    {
        var changed = new List<int>();
        if (previous is null)
            return (true, changed);
        var structure = previous._byId.Count != _byId.Count;
        foreach (var (id, node) in _byId)
        {
            if (!previous._byId.TryGetValue(id, out var old))
            {
                structure = true;
                continue;
            }
            if (!structure && !SameChildren(node, old))
                structure = true;
            if (!node.ContentEquals(old))
                changed.Add(id);
        }
        return (structure, changed);
    }

    private static bool SameChildren(SkUiSemanticsNode node, SkUiSemanticsNode old)
    {
        if (node.Children.Count != old.Children.Count || node.Parent?.Id != old.Parent?.Id)
            return false;
        for (var index = 0; index < node.Children.Count; index++)
            if (node.Children[index].Id != old.Children[index].Id)
                return false;
        return true;
    }

    /// <summary>Builds the tree of the surface rooted at <paramref name="root"/> (UI thread).</summary>
    public static SkUiSemanticsTree Build(SkUiView root, SkUiFocusManager? focus = null)
    {
        var props = GetProps(root);
        var rootNode = new SkUiSemanticsNode
        {
            Id = SkUiSemanticsNode.RootId,
            Bounds = new Rect(0, 0, props.Width, props.Height),
            Label = string.Empty
        };
        var tree = new SkUiSemanticsTree(rootNode);
        tree._byId[rootNode.Id] = rootNode;
        // The surface shows the root's local space: its own frame offset is outside the surface.
        props.X = props.Y = 0;
        var builder = new Builder(tree, focus);
        builder.Visit(root, props, SKMatrix.Identity, new SKRect(0, 0, props.Width, props.Height), rootNode, absorber: null, enabled: true);
        return tree;
    }

    private static SkUiRenderProps GetProps(ISkUiRenderable node)
    {
        var props = SkUiRenderProps.Default;
        node.GetRenderProps(ref props);
        return props;
    }

    /// <summary>Gives <paramref name="node"/> its stable id (kept while the node lives, also across surfaces).</summary>
    internal static int IdOf(ISkUiRenderable node)
    {
        var state = node.RenderState;
        if (state.SemanticsId == 0)
            state.SemanticsId = Interlocked.Increment(ref _nextId);
        return state.SemanticsId;
    }

    private sealed class Builder(SkUiSemanticsTree tree, SkUiFocusManager? focus)
    {
        private readonly SkUiSemanticsInfo _info = new();
        private readonly List<ISkUiRenderable> _children = [];

        /// <summary>
        /// An element that reads its descendants' text as its name: <see cref="Merged"/> collects the text, or (with a
        /// description) nothing is collected and the text is dropped.
        /// </summary>
        internal sealed class Absorber(SkUiSemanticsNode element, bool collects)
        {
            public SkUiSemanticsNode Element { get; } = element;
            public bool Collects { get; } = collects;
            public List<string>? Merged;
        }

        public void Visit(ISkUiRenderable node, SkUiRenderProps props, SKMatrix parentToRoot, SKRect clip, SkUiSemanticsNode parent, Absorber? absorber,
            bool enabled)
        {
            if (props.IsSkipped)
                return;
            var toRoot = parentToRoot.PreConcat(props.Matrix);
            var ownRect = toRoot.MapRect(props.Bounds);
            var visible = Intersect(ownRect, clip);
            // Outside the visible area (scrolled away in a long list) and clipping its children to itself: nothing of the
            // subtree shows, so it is not walked (the tree is rebuilt when it scrolls in). Inside an absorbing element the
            // text is still collected, as the group reads all its text.
            if (visible.IsEmpty && props.ClipToBounds && absorber is null)
                return;
            var element = parent;
            var childAbsorber = absorber;
            if (node is ISkUiAccessibleNode accessible)
            {
                _info.Reset();
                accessible.GetSemantics(_info);
                _info.IsEnabled &= enabled;
                if (_info.ExcludedWithChildren)
                    return;
                if (_info.IsNative)
                {
                    if (!visible.IsEmpty)
                        Add(parent, NewNode(accessible, visible, label: null, native: true));
                    return;
                }
                var text = Clean(_info.Description) ?? Clean(_info.Text);
                if (IsElement(_info, text))
                {
                    var actionable = (_info.Actions & (SkUiSemanticsActions.Activate | SkUiSemanticsActions.LongPress)) != 0
                        || _info.Role is SkUiSemanticsRole.Button or SkUiSemanticsRole.CheckBox or SkUiSemanticsRole.Switch
                            or SkUiSemanticsRole.RadioButton or SkUiSemanticsRole.Slider;
                    if (absorber is not null && !actionable && _info.Role != SkUiSemanticsRole.ScrollView)
                    {
                        // Read as part of the actionable (or described) ancestor.
                        if (absorber.Collects && text is not null)
                            (absorber.Merged ??= []).Add(text);
                    }
                    else if (!visible.IsEmpty)
                    {
                        element = NewNode(accessible, visible, text, native: false);
                        Add(parent, element);
                        var described = Clean(_info.Description) is not null;
                        childAbsorber = actionable || described ? new Absorber(element, collects: !described) : null;
                    }
                }
            }

            // Children: in the children space (scroll offset, overscroll scale), clipped as drawn; pinned children (scroll
            // bars, pinned headers) in local coordinates, outside the children clip.
            var ownClip = props.ClipToBounds ? Intersect(clip, ownRect) : clip;
            if (props.ClipPath is { } clipPath)
                ownClip = Intersect(ownClip, toRoot.MapRect(clipPath.Bounds));
            var childrenClip = props.ChildrenClipRect.IsEmpty ? ownClip : Intersect(ownClip, toRoot.MapRect(props.ChildrenClipRect));
            var childrenToRoot = toRoot.PreConcat(props.ChildrenMatrix);
            // A disabled node (pointers and keys blocked) disables its subtree for screen readers too.
            var childrenEnabled = enabled && node is not ISkUiInputNode { IsInputEnabled: false };
            var start = _children.Count;
            node.GetRenderChildren(_children);
            var end = _children.Count;
            try
            {
                for (var index = start; index < end; index++)
                {
                    var child = _children[index];
                    var childProps = GetProps(child);
                    if (childProps.Pinned)
                        Visit(child, childProps, toRoot, ownClip, element, childAbsorber, childrenEnabled);
                    else
                        Visit(child, childProps, childrenToRoot, childrenClip, element, childAbsorber, childrenEnabled);
                }
            }
            finally
            {
                _children.RemoveRange(start, _children.Count - start);
            }

            if (!ReferenceEquals(element, parent) && childAbsorber is { Merged: { Count: > 0 } merged } && ReferenceEquals(childAbsorber.Element, element))
                element.Label = element.Label is { } own ? own + ", " + string.Join(", ", merged) : string.Join(", ", merged);
        }

        private static bool IsElement(SkUiSemanticsInfo info, string? text)
        {
            if (info.IsInAccessibleTree is { } forced)
                return forced;
            if (Clean(info.Description) is not null || info.HeadingLevel != SemanticHeadingLevel.None)
                return true;
            return info.Role switch
            {
                SkUiSemanticsRole.None => info.Actions != SkUiSemanticsActions.None,
                SkUiSemanticsRole.Text => text is not null || info.Actions != SkUiSemanticsActions.None,
                SkUiSemanticsRole.Image => info.Actions != SkUiSemanticsActions.None,
                _ => true
            };
        }

        private SkUiSemanticsNode NewNode(ISkUiAccessibleNode source, SKRect visible, string? label, bool native)
        {
            var info = _info;
            return new SkUiSemanticsNode
            {
                Id = IdOf(source),
                Source = source,
                Bounds = new Rect(visible.Left, visible.Top, visible.Width, visible.Height),
                Role = info.Role,
                Label = label,
                Hint = Clean(info.Hint),
                Value = Clean(info.Value),
                CheckState = info.CheckState,
                Range = info.Range,
                IsEnabled = info.IsEnabled,
                Actions = info.Actions,
                IsHorizontal = info.IsHorizontal,
                HeadingLevel = info.HeadingLevel,
                AutomationId = info.AutomationId,
                IsFocusable = !native && source.IsKeyboardFocusable,
                IsFocused = !native && (focus?.Focused is { } focused ? ReferenceEquals(focused, source) : source.IsFocused),
                IsNative = native
            };
        }

        private void Add(SkUiSemanticsNode parent, SkUiSemanticsNode node)
        {
            node.Parent = parent;
            parent.Children.Add(node);
            tree._byId[node.Id] = node;
        }

        private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        private static SKRect Intersect(SKRect a, SKRect b)
        {
            var result = SKRect.Intersect(a, b);
            return result.Width <= 0 || result.Height <= 0 ? SKRect.Empty : result;
        }
    }
}
