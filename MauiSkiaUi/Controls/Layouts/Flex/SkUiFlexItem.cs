// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Ported from dotnet/maui 10.0.101, src/Core/src/Layouts/Flex.cs (Microsoft.Maui.Layouts.Flex.Item), which is
// internal to Microsoft.Maui and so cannot be reused by SkUiFlexLayout.
// Author(s) of the original:
//  - Laurent Sansonetti (native Microsoft.Maui.Controls flex https://github.com/xamarin/flex)
//  - Stephane Delcroix (.NET port)
//
// Changes from the original:
//  - Uses MAUI's public Flex* enums (same values as the internal ones) instead of private copies.
//  - No per-pass allocations: the self-sizing size pair is two locals, and the ordered indices and line
//    buffers are kept on the item and reused.
//  - The wrap tolerance (0.1) applies on every platform: drawn layouts are platform-independent.

using Microsoft.Maui.Layouts;

namespace MauiSkiaUi.Flex;

/// <summary>A flex basis: auto, an absolute length, or a length relative to the container (0–1).</summary>
internal readonly struct SkUiFlexBasis
{
    private readonly bool _isRelative;
    private readonly bool _isLength;

    public static readonly SkUiFlexBasis Auto;

    public SkUiFlexBasis(float length, bool isRelative = false)
    {
        Length = length;
        _isLength = !isRelative;
        _isRelative = isRelative;
    }

    public bool IsRelative => _isRelative;
    public bool IsAuto => !_isLength && !_isRelative;
    public float Length { get; }

    /// <summary>Converts MAUI's public <see cref="FlexBasis"/>, whose <c>IsAuto</c> / <c>IsRelative</c> are internal.</summary>
    public static SkUiFlexBasis From(FlexBasis basis)
    {
        if (basis == FlexBasis.Auto)
            return Auto;
        // A relative basis is at most 1 (MAUI's constructor enforces it), so only then can it be relative.
        return basis.Length <= 1 && basis == new FlexBasis(basis.Length, isRelative: true)
            ? new SkUiFlexBasis(basis.Length, isRelative: true)
            : new SkUiFlexBasis(basis.Length);
    }
}

/// <summary>An item of the flex engine: a container (the root) or one child. <see cref="Frame"/> holds x, y, width, height.</summary>
internal sealed class SkUiFlexItem : List<SkUiFlexItem>
{
    private int _order;
    private int[]? _orderedIndices;
    private Line[]? _lines;

    public float[] Frame { get; } = new float[4];

    public SkUiFlexItem? Parent { get; private set; }
    private bool ShouldOrderChildren { get; set; }

    public FlexAlignContent AlignContent { get; set; } = FlexAlignContent.Stretch;
    public FlexAlignItems AlignItems { get; set; } = FlexAlignItems.Stretch;
    public FlexAlignSelf AlignSelf { get; set; } = FlexAlignSelf.Auto;
    public SkUiFlexBasis Basis { get; set; } = SkUiFlexBasis.Auto;
    public float Bottom { get; set; } = float.NaN;
    public FlexDirection Direction { get; set; } = FlexDirection.Column;
    public float Grow { get; set; }
    public float Height { get; set; } = float.NaN;
    public bool IsVisible { get; set; } = true;
    public FlexJustify JustifyContent { get; set; } = FlexJustify.Start;
    public float Left { get; set; } = float.NaN;
    public float MarginBottom { get; set; }
    public float MarginLeft { get; set; }
    public float MarginRight { get; set; }
    public float MarginTop { get; set; }

    public int Order
    {
        get => _order;
        set
        {
            if ((_order = value) != 0 && Parent != null)
                Parent.ShouldOrderChildren = true;
        }
    }

    public float PaddingBottom { get; set; }
    public float PaddingLeft { get; set; }
    public float PaddingRight { get; set; }
    public float PaddingTop { get; set; }
    public FlexPosition Position { get; set; } = FlexPosition.Relative;
    public float Right { get; set; } = float.NaN;
    public float Shrink { get; set; } = 1f;
    public float Top { get; set; } = float.NaN;
    public float Width { get; set; } = float.NaN;
    public FlexWrap Wrap { get; set; } = FlexWrap.NoWrap;

    public Rect GetFrame() => new(Frame[0], Frame[1], Frame[2], Frame[3]);

    /// <summary>The nearest known container size on each axis (-1 when none), as MAUI's <c>FlexExtensions.GetConstraints</c>.</summary>
    public Size GetConstraints()
    {
        var widthConstraint = -1d;
        var heightConstraint = -1d;
        var parent = Parent;
        do
        {
            if (parent == null)
                break;
            if (widthConstraint < 0 && !float.IsNaN(parent.Width))
                widthConstraint = parent.Width;
            if (heightConstraint < 0 && !float.IsNaN(parent.Height))
                heightConstraint = parent.Height;
            parent = parent.Parent;
        } while (widthConstraint < 0 || heightConstraint < 0);
        return new Size(widthConstraint, heightConstraint);
    }

    public new void Add(SkUiFlexItem child)
    {
        ValidateChild(child);
        base.Add(child);
        child.Parent = this;
        ShouldOrderChildren |= child.Order != 0;
    }

    /// <summary>Removes every child and clears their parent links.</summary>
    public new void Clear()
    {
        foreach (var child in this)
            child.Parent = null;
        base.Clear();
        ShouldOrderChildren = false;
    }

    public void Layout(bool inMeasureMode)
    {
        if (Parent != null)
            throw new InvalidOperationException("Layout() must be called on a root item (that hasn't been added to another item)");
        if (float.IsNaN(Width) || float.IsNaN(Height))
            throw new InvalidOperationException("Layout() must be called on an item that has proper values for the Width and Height properties");
        if (SelfSizing != null)
            throw new InvalidOperationException("Layout() cannot be called on an item that has the SelfSizing property set");
        LayoutItem(this, Width, Height, inMeasureMode);
    }

    public delegate void SelfSizingDelegate(SkUiFlexItem item, ref float width, ref float height, bool inMeasureMode);

    public SelfSizingDelegate? SelfSizing { get; set; }

    private void ValidateChild(SkUiFlexItem child)
    {
        if (this == child)
            throw new ArgumentException("cannot add item into self");
        if (child.Parent != null)
            throw new ArgumentException("child already has a parent");
    }

    private static void LayoutItem(SkUiFlexItem item, float width, float height, bool inMeasureMode)
    {
        if (item.Count == 0)
            return;

        var layout = new FlexLayoutState();
        layout.Init(item, width, height);
        layout.Reset();

        int lastLayoutChild = 0;
        int relativeChildrenCount = 0;
        for (int i = 0; i < item.Count; i++)
        {
            var child = layout.ChildAt(item, i);
            if (!child.IsVisible)
                continue;

            // Items with an absolute position have their frames determined
            // directly and are skipped during layout.
            if (child.Position == FlexPosition.Absolute)
            {
                child.Frame[2] = AbsoluteSize(child.Width, child.Left, child.Right, width);
                child.Frame[3] = AbsoluteSize(child.Height, child.Top, child.Bottom, height);
                child.Frame[0] = AbsolutePos(child.Left, child.Right, child.Frame[2], width);
                child.Frame[1] = AbsolutePos(child.Top, child.Bottom, child.Frame[3], height);

                // Now that the item has a frame, we can layout its children.
                LayoutItem(child, child.Frame[2], child.Frame[3], inMeasureMode);
                continue;
            }

            // Initialize frame.
            child.Frame[0] = 0;
            child.Frame[1] = 0;
            child.Frame[2] = child.Width;
            child.Frame[3] = child.Height;

            // Main axis size defaults to 0.
            if (float.IsNaN(child.Frame[layout.FrameSizeI]))
                child.Frame[layout.FrameSizeI] = 0;

            // Cross axis size defaults to the parent's size (or line size in wrap
            // mode, which is calculated later on).
            if (float.IsNaN(child.Frame[layout.FrameSize2I]))
            {
                if (layout.Wrap)
                    layout.NeedLines = true;
                else
                    child.Frame[layout.FrameSize2I] = (layout.Vertical ? width : height) - child.MarginThickness(!layout.Vertical);
            }

            // Call the self_sizing callback if provided. Only non-NAN values
            // are taken into account. If the item's cross-axis align property
            // is set to stretch, ignore the value returned by the callback.
            if (child.SelfSizing != null)
            {
                var sizeWidth = child.Frame[2];
                var sizeHeight = child.Frame[3];

                child.SelfSizing(child, ref sizeWidth, ref sizeHeight, inMeasureMode);

                var stretchCross = ChildAlign(child, item) == FlexAlignItems.Stretch && layout.AlignDim > 0;
                if (!(layout.FrameSize2I == 2 && stretchCross) && !float.IsNaN(sizeWidth))
                    child.Frame[2] = sizeWidth;
                if (!(layout.FrameSize2I == 3 && stretchCross) && !float.IsNaN(sizeHeight))
                    child.Frame[3] = sizeHeight;
            }

            // Honor the `basis' property which overrides the main-axis size.
            if (!child.Basis.IsAuto)
            {
                if (child.Basis.Length < 0)
                    throw new InvalidOperationException("basis should >=0");
                if (child.Basis.IsRelative && child.Basis.Length > 1)
                    throw new InvalidOperationException("relative basis should be <=1");
                float basis = child.Basis.Length;
                if (child.Basis.IsRelative)
                    basis *= layout.Vertical ? height : width;
                child.Frame[layout.FrameSizeI] = basis - child.MarginThickness(layout.Vertical);
            }

            // A small tolerance for floating-point precision errors when deciding whether a child fits on the
            // current line while wrapping (sub-pixel rounding from DPI scaling). MAUI applies it on Android and
            // Windows only; drawn layouts use it everywhere. Values below 1 have no effect on intentional gaps.
            const float flexWrapTolerance = 0.1f;
            float flexTolerance = layout.FlexDim + flexWrapTolerance;
            float childSize = child.Frame[layout.FrameSizeI];
            if (layout.Wrap)
            {
                if (flexTolerance < childSize)
                {
                    // Not enough space for this child on this line, layout the
                    // remaining items and move it to a new line.
                    LayoutItems(item, lastLayoutChild, i, relativeChildrenCount, ref layout, inMeasureMode);

                    layout.Reset();
                    lastLayoutChild = i;
                    relativeChildrenCount = 0;
                }

                float childSize2 = child.Frame[layout.FrameSize2I];
                if (!float.IsNaN(childSize2) && childSize2 + child.MarginThickness(!layout.Vertical) > layout.LineDim)
                    layout.LineDim = childSize2 + child.MarginThickness(!layout.Vertical);
            }

            if (child.Grow < 0 || child.Shrink < 0)
                throw new InvalidOperationException("shrink and grow should be >= 0");

            layout.FlexGrows += child.Grow;
            layout.FlexShrinks += child.Shrink;

            if (layout.FlexDim > 0)
            {
                // If flex_dim is zero, it's because we're measuring unconstrained in that direction
                // So we don't need to keep a running tally of available space
                layout.FlexDim -= childSize + child.MarginThickness(layout.Vertical);
            }

            relativeChildrenCount++;

            if (childSize > 0 && child.Grow > 0)
                layout.ExtraFlexDim += childSize;
        }

        // Layout remaining items in wrap mode, or everything otherwise.
        LayoutItems(item, lastLayoutChild, item.Count, relativeChildrenCount, ref layout, inMeasureMode);

        // In wrap mode we may need to tweak the position of each line according to
        // the align_content property as well as the cross-axis size of items that
        // haven't been set yet.
        if (layout.NeedLines && layout.LineCount > 0)
        {
            float pos = 0;
            float spacing = 0;
            float flexDim = layout.AlignDim - layout.LinesSizes;
            if (flexDim > 0)
                LayoutAlign(item.AlignContent, flexDim, (uint)layout.LineCount, ref pos, ref spacing);

            float oldPos = 0;
            if (layout.Reverse2)
            {
                pos = layout.AlignDim - pos;
                oldPos = layout.AlignDim;
            }

            var lines = item._lines!;
            for (int i = 0; i < layout.LineCount; i++)
            {
                var line = lines[i];

                // Re-position the children of this line, honoring any child
                // alignment previously set within the line.
                for (int j = line.ChildBegin; j < line.ChildEnd; j++)
                {
                    var child = layout.ChildAt(item, j);
                    if (child.Position == FlexPosition.Absolute)
                    {
                        // Should not be re-positioned.
                        continue;
                    }
                    if (float.IsNaN(child.Frame[layout.FrameSize2I]))
                    {
                        // If the child's cross axis size hasn't been set it, it
                        // defaults to the line size.
                        child.Frame[layout.FrameSize2I] = line.Size
                            + (item.AlignContent == FlexAlignContent.Stretch ? spacing : 0);
                    }
                    child.Frame[layout.FramePos2I] = pos + (child.Frame[layout.FramePos2I] - oldPos);
                }

                if (layout.Reverse2)
                {
                    pos -= line.Size;
                    pos -= spacing;
                    oldPos -= line.Size;
                }
                else
                {
                    pos += line.Size;
                    pos += spacing;
                    oldPos += line.Size;
                }
            }
        }
    }

    private float MarginThickness(bool vertical) =>
        vertical ? MarginTop + MarginBottom : MarginLeft + MarginRight;

    private static void LayoutAlign(FlexJustify align, float flexDim, int childrenCount, ref float pos, ref float spacing)
    {
        if (flexDim < 0)
            throw new ArgumentException($"{nameof(flexDim)} must not be negative", nameof(flexDim));
        pos = 0;
        spacing = 0;

        switch (align)
        {
            case FlexJustify.Start:
                return;
            case FlexJustify.End:
                pos = flexDim;
                return;
            case FlexJustify.Center:
                pos = flexDim / 2;
                return;
            case FlexJustify.SpaceBetween:
                if (childrenCount > 0)
                    spacing = flexDim / (childrenCount - 1);
                return;
            case FlexJustify.SpaceAround:
                if (childrenCount > 0)
                {
                    spacing = flexDim / childrenCount;
                    pos = spacing / 2;
                }
                return;
            case FlexJustify.SpaceEvenly:
                if (childrenCount > 0)
                {
                    spacing = flexDim / (childrenCount + 1);
                    pos = spacing;
                }
                return;
            default:
                throw new ArgumentException($"{nameof(FlexJustify)} option not handled", nameof(align));
        }
    }

    private static void LayoutAlign(FlexAlignContent align, float flexDim, uint childrenCount, ref float pos, ref float spacing)
    {
        if (flexDim < 0)
            throw new ArgumentException($"{nameof(flexDim)} must not be negative", nameof(flexDim));
        pos = 0;
        spacing = 0;

        switch (align)
        {
            case FlexAlignContent.Start:
                return;
            case FlexAlignContent.End:
                pos = flexDim;
                return;
            case FlexAlignContent.Center:
                pos = flexDim / 2;
                return;
            case FlexAlignContent.SpaceBetween:
                if (childrenCount > 0)
                    spacing = flexDim / (childrenCount - 1);
                return;
            case FlexAlignContent.SpaceAround:
                if (childrenCount > 0)
                {
                    spacing = flexDim / childrenCount;
                    pos = spacing / 2;
                }
                return;
            case FlexAlignContent.SpaceEvenly:
                if (childrenCount > 0)
                {
                    spacing = flexDim / (childrenCount + 1);
                    pos = spacing;
                }
                return;
            case FlexAlignContent.Stretch:
                spacing = flexDim / childrenCount;
                return;
            default:
                throw new ArgumentException($"{nameof(FlexAlignContent)} option not handled", nameof(align));
        }
    }

    private static void LayoutItems(SkUiFlexItem item, int childBegin, int childEnd, int childrenCount, ref FlexLayoutState layout, bool inMeasureMode)
    {
        if (childrenCount > childEnd - childBegin)
            throw new ArgumentException($"The {childrenCount} must not be smaller than the requested range between {childBegin} and {childEnd}", nameof(childrenCount));
        if (childrenCount <= 0)
            return;
        if (layout.FlexDim > 0 && layout.ExtraFlexDim > 0)
        {
            // If the container has a positive flexible space, let's add to it
            // the sizes of all flexible children.
            layout.FlexDim += layout.ExtraFlexDim;
        }

        // Determine the main axis initial position and optional spacing.
        float pos = 0;
        float spacing = 0;
        if (layout.FlexGrows == 0 && layout.FlexDim > 0)
            LayoutAlign(item.JustifyContent, layout.FlexDim, childrenCount, ref pos, ref spacing);

        if (layout.Reverse)
            pos = layout.SizeDim - pos;

        if (layout.Reverse)
            pos -= layout.Vertical ? item.PaddingBottom : item.PaddingRight;
        else
            pos += layout.Vertical ? item.PaddingTop : item.PaddingLeft;
        if (layout.Wrap && layout.Reverse2)
            layout.Pos2 -= layout.LineDim;

        for (int i = childBegin; i < childEnd; i++)
        {
            var child = layout.ChildAt(item, i);
            if (!child.IsVisible)
                continue;
            if (child.Position == FlexPosition.Absolute)
            {
                // Already positioned.
                continue;
            }

            // Grow or shrink the main axis item size if needed.
            float flexSize = 0;
            if (layout.FlexDim > 0)
            {
                // Only the free space is distributed proportionally, not the total container space.
                // FlexDim was inflated by ExtraFlexDim (the sum of measured sizes of growing items), so the
                // actual free space is recovered by subtracting it back. The item's measured size is preserved
                // and the proportional share of free space is added on top.
                float freeSpace = Math.Max(0, layout.FlexDim - layout.ExtraFlexDim);
                if (child.Grow != 0)
                    flexSize = freeSpace / layout.FlexGrows * child.Grow;
            }
            else if (layout.FlexDim < 0)
            {
                if (child.Shrink != 0)
                    flexSize = layout.FlexDim / layout.FlexShrinks * child.Shrink;
            }
            child.Frame[layout.FrameSizeI] += flexSize;

            // Set the cross axis position (and stretch the cross axis size if
            // needed).
            float alignSize = child.Frame[layout.FrameSize2I];
            float alignPos = layout.Pos2 + 0;
            switch (ChildAlign(child, item))
            {
                case FlexAlignItems.End:
                    alignPos += layout.LineDim - alignSize - (layout.Vertical ? child.MarginRight : child.MarginBottom);
                    break;

                case FlexAlignItems.Center:
                    alignPos += layout.LineDim / 2 - alignSize / 2
                        + ((layout.Vertical ? child.MarginLeft : child.MarginTop)
                           - (layout.Vertical ? child.MarginRight : child.MarginBottom));
                    break;

                case FlexAlignItems.Stretch:
                    if (alignSize == 0)
                    {
                        child.Frame[layout.FrameSize2I] = layout.LineDim
                            - ((layout.Vertical ? child.MarginLeft : child.MarginTop)
                               + (layout.Vertical ? child.MarginRight : child.MarginBottom));
                    }
                    alignPos += layout.Vertical ? child.MarginLeft : child.MarginTop;
                    break;
                case FlexAlignItems.Start:
                    alignPos += layout.Vertical ? child.MarginLeft : child.MarginTop;
                    break;

                default:
                    throw new InvalidOperationException($"{nameof(FlexAlignItems)} option not handled");
            }
            child.Frame[layout.FramePos2I] = alignPos;

            // Set the main axis position.
            if (layout.Reverse)
            {
                pos -= layout.Vertical ? child.MarginBottom : child.MarginRight;
                pos -= child.Frame[layout.FrameSizeI];
                child.Frame[layout.FramePosI] = pos;
                pos -= spacing;
                pos -= layout.Vertical ? child.MarginTop : child.MarginLeft;
            }
            else
            {
                pos += layout.Vertical ? child.MarginTop : child.MarginLeft;
                child.Frame[layout.FramePosI] = pos;
                pos += child.Frame[layout.FrameSizeI];
                pos += spacing;
                pos += layout.Vertical ? child.MarginBottom : child.MarginRight;
            }

            // Now that the item has a frame, we can layout its children.
            LayoutItem(child, child.Frame[2], child.Frame[3], inMeasureMode);
        }

        if (layout.Wrap && !layout.Reverse2)
            layout.Pos2 += layout.LineDim;

        if (layout.NeedLines)
        {
            var lines = item._lines;
            if (lines == null || lines.Length == layout.LineCount)
                Array.Resize(ref item._lines, Math.Max(4, layout.LineCount * 2));
            item._lines![layout.LineCount++] = new Line(childBegin, childEnd, layout.LineDim);
            layout.LinesSizes += layout.LineDim;
        }

        if (layout.Reverse && layout.SizeDim == 0)
        {
            // Handle reversed layouts when there was no fixed size in the first place. All of the positions will be flipped
            // across the axis. Luckily the pos variable is already tracking how far negative the values were in this situation,
            // so we can just offset the distance by that amount and get the desired value
            for (int i = childBegin; i < childEnd; i++)
            {
                var child = layout.ChildAt(item, i);
                if (!child.IsVisible)
                    continue;

                if (child.Position == FlexPosition.Absolute)
                {
                    // Not helpful for this
                    continue;
                }

                child.Frame[layout.FramePosI] = child.Frame[layout.FramePosI] - pos;
            }
        }
    }

    private static float AbsoluteSize(float val, float pos1, float pos2, float dim) =>
        !float.IsNaN(val) ? val : (!float.IsNaN(pos1) && !float.IsNaN(pos2) ? dim - pos2 - pos1 : 0);

    private static float AbsolutePos(float pos1, float pos2, float size, float dim) =>
        !float.IsNaN(pos1) ? pos1 : (!float.IsNaN(pos2) ? dim - size - pos2 : 0);

    private static FlexAlignItems ChildAlign(SkUiFlexItem child, SkUiFlexItem parent) =>
        child.AlignSelf == FlexAlignSelf.Auto ? parent.AlignItems : (FlexAlignItems)child.AlignSelf;

    /// <summary>
    /// Fills <see cref="_orderedIndices"/> with the child indices stably sorted by <see cref="Order"/>
    /// (insertion sort: equal orders keep insertion order, as MAUI's <c>OrderBy</c>).
    /// </summary>
    private int[] OrderedIndices()
    {
        var count = Count;
        if (_orderedIndices == null || _orderedIndices.Length < count)
            _orderedIndices = new int[Math.Max(count, 4)];
        var indices = _orderedIndices;
        for (var i = 0; i < count; i++)
        {
            var order = this[i].Order;
            var j = i - 1;
            while (j >= 0 && this[indices[j]].Order > order)
            {
                indices[j + 1] = indices[j];
                j--;
            }
            indices[j + 1] = i;
        }
        return indices;
    }

    private readonly record struct Line(int ChildBegin, int ChildEnd, float Size);

    private struct FlexLayoutState
    {
        // Set during init.
        public bool Wrap;
        public bool Reverse;                // whether main axis is reversed
        public bool Reverse2;               // whether cross axis is reversed (wrap only)
        public bool Vertical;
        public float SizeDim;               // main axis parent size
        public float AlignDim;              // cross axis parent size
        public int FramePosI;               // main axis position
        public int FramePos2I;              // cross axis position
        public int FrameSizeI;              // main axis size
        public int FrameSize2I;             // cross axis size
        private int[]? _orderedIndices;

        // Set for each line layout.
        public float LineDim;               // the cross axis size
        public float FlexDim;               // the flexible part of the main axis size
        public float ExtraFlexDim;          // sizes of flexible items
        public float FlexGrows;
        public float FlexShrinks;
        public float Pos2;                  // cross axis position

        // Calculated layout lines - only tracked when needed:
        //   - if the root's align_content property isn't set to Start
        //   - or if any child item doesn't have a cross-axis size set
        public bool NeedLines;
        public int LineCount;
        public float LinesSizes;

        public void Reset()
        {
            LineDim = Wrap ? 0 : AlignDim;
            FlexDim = SizeDim;
            ExtraFlexDim = 0;
            FlexGrows = 0;
            FlexShrinks = 0;
        }

        public void Init(SkUiFlexItem item, float width, float height)
        {
            if (item.PaddingLeft < 0 || item.PaddingRight < 0 || item.PaddingTop < 0 || item.PaddingBottom < 0)
                throw new ArgumentException($"The padding on {nameof(item)} must not be negative", nameof(item));

            width = Math.Max(0, width - item.PaddingLeft + item.PaddingRight);
            height = Math.Max(0, height - item.PaddingTop + item.PaddingBottom);

            Reverse = item.Direction == FlexDirection.RowReverse || item.Direction == FlexDirection.ColumnReverse;
            Vertical = true;
            switch (item.Direction)
            {
                case FlexDirection.Row:
                case FlexDirection.RowReverse:
                    Vertical = false;
                    SizeDim = width;
                    AlignDim = height;
                    FramePosI = 0;
                    FramePos2I = 1;
                    FrameSizeI = 2;
                    FrameSize2I = 3;
                    break;
                case FlexDirection.Column:
                case FlexDirection.ColumnReverse:
                    SizeDim = height;
                    AlignDim = width;
                    FramePosI = 1;
                    FramePos2I = 0;
                    FrameSizeI = 3;
                    FrameSize2I = 2;
                    break;
            }

            _orderedIndices = item.ShouldOrderChildren && item.Count > 0 ? item.OrderedIndices() : null;

            FlexDim = 0;
            FlexGrows = 0;
            FlexShrinks = 0;

            Reverse2 = false;
            Wrap = item.Wrap != FlexWrap.NoWrap;
            if (Wrap)
            {
                if (item.Wrap == FlexWrap.Reverse)
                {
                    Reverse2 = true;
                    Pos2 = AlignDim;
                }
            }
            else
            {
                Pos2 = Vertical ? item.PaddingLeft : item.PaddingTop;
            }

            NeedLines = Wrap && item.AlignContent != FlexAlignContent.Start;
            LineCount = 0;
            LinesSizes = 0;
        }

        public readonly SkUiFlexItem ChildAt(SkUiFlexItem item, int i) =>
            item[_orderedIndices?[i] ?? i];
    }
}
