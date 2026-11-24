using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>What a <see cref="SkUiCarouselEngine"/> reads from its user, live: settings that can change between two layouts.</summary>
internal interface ISkUiCarouselEngineHost
{
    /// <summary>Whether the items should repeat without an end (the setting; <see cref="SkUiCarouselEngine.Loops"/> also needs two items).</summary>
    bool Loop { get; }

    /// <summary>Whether the strip runs horizontally.</summary>
    bool Horizontal { get; }

    /// <summary>Whether the strip runs from the right (horizontal and right to left).</summary>
    bool Mirrored { get; }

    /// <summary>The length along the axis of items that fill an unbounded carousel: the first item measured across <paramref name="cross"/>.</summary>
    double ProbeExtent(double cross);
}

/// <summary>The settings of a carousel's item layout, read when it lays the items out (<see cref="SkUiCarouselEngine.UpdateGeometry"/>).</summary>
/// <param name="PeekAreaInsets">Room at the ends of the axis where neighbors peek (the start side mirrors in right to left).</param>
/// <param name="ItemSpacing">The gap between items.</param>
/// <param name="ItemExtent">Every item's length along the axis when positive; 0 fills the viewport minus the insets.</param>
/// <param name="Alignment">Where the current item lines up.</param>
/// <param name="Effect">The item effect, for the items it shows beyond the layout's.</param>
internal readonly record struct SkUiCarouselLayout(Thickness PeekAreaInsets, double ItemSpacing, double ItemExtent, SnapPointsAlignment Alignment,
    SkUiCarouselEffect? Effect);

/// <summary>
/// The layout of a carousel's strip of items, with no view types: item geometry, the strip of copies a looping carousel repeats
/// its items along (<c>slot mod count</c>) and moving back to its middle copy by whole cycles, snap offsets, the item at the
/// snap point, the window of slots to realize, and the place kept across layout changes. Its users create and recycle the
/// views (or keep them) and drive the scroller; it works out where everything goes.
/// </summary>
/// <remarks>
/// Offsets are logical (from the start of the items; mirrored from the physical scroll offset in right-to-left layouts) unless a
/// member says physical. Callers pass the current logical offset in (<see cref="LogicalOffset"/> of the scroller's): the offset
/// a pending placement will take wins over the scroller's until <see cref="TakePendingOffset"/> hands it over.
/// </remarks>
internal sealed class SkUiCarouselEngine(ISkUiCarouselEngineHost host)
{
    /// <summary>How far from the middle copy (in cycles of all items) a looping carousel goes before it moves back.</summary>
    public const double RecenterDistance = 0.75;

    /// <summary>How many points one snap query lists at most.</summary>
    public const int MaxSnapPoints = 4096;

    /// <summary>How much of an item must be inside the viewport to count as in view, in DIPs (touching it is not showing).</summary>
    public const double VisibilityTolerance = 0.5;

    private double _viewport = double.NaN; // offered along the axis (infinite: unbounded)
    private double _align;
    private double _padEnd;
    private bool _geometryValid;
    private bool _placeRequested = true; // the first layout places the current item
    private double? _keep;               // the item position to show once the layout changed
    private double? _pendingLogical;     // the offset the scroller takes after its next arrange

    /// <summary>The number of items.</summary>
    public int Count { get; private set; }

    /// <summary>Whether the items repeat without an end.</summary>
    public bool Loops => host.Loop && Count > 1;

    /// <summary>Whether the items are laid out (positions and offsets are known).</summary>
    public bool HasGeometry => _geometryValid && Stride > 0 && Count > 0 && Slots > 0;

    /// <summary>The cross-axis constraint of the last layout (infinite before the first).</summary>
    public double CrossConstraint { get; private set; } = double.PositiveInfinity;

    /// <summary>The viewport along the axis (the offered one, or an item and the peek insets when unbounded).</summary>
    public double ViewportLength { get; private set; }

    /// <summary>Every item's length along the axis.</summary>
    public double Extent { get; private set; }

    /// <summary>From one item's start to the next one's: <see cref="Extent"/> plus <see cref="Spacing"/>.</summary>
    public double Stride { get; private set; }

    /// <summary>The gap between items.</summary>
    public double Spacing { get; private set; }

    /// <summary>The peek inset before the first item.</summary>
    public double PadStart { get; private set; }

    /// <summary>The strip's length along the axis: insets, slots and the spacing between them.</summary>
    public double Length { get; private set; }

    /// <summary>The number of places in the strip (the items times <see cref="Copies"/>).</summary>
    public int Slots { get; private set; }

    /// <summary>How many copies of the items the strip holds (1 unless looping).</summary>
    public int Copies { get; private set; } = 1;

    /// <summary>Whether a placement waits for the scroller (<see cref="TakePendingOffset"/>).</summary>
    public bool HasPendingOffset => _pendingLogical is not null;

    /// <summary>The largest logical offset.</summary>
    public double MaxLogical => Math.Max(0, Length - ViewportLength);

    /// <summary>One cycle of all items along the strip.</summary>
    public double CycleLength => Count * Stride;

    /// <summary>Where the middle copy of the items starts (looping).</summary>
    private int HomeSlot => Copies / 2 * Count;

    /// <summary>The current logical offset for the scroller's <paramref name="physical"/> one: the one the next layout takes, or the scroller's.</summary>
    public double LogicalOffset(double physical) => _pendingLogical ?? Logical(physical);

    /// <summary>The logical offset of a physical one.</summary>
    public double Logical(double physical) => host.Mirrored ? MaxLogical - physical : physical;

    /// <summary>The physical offset of a logical one.</summary>
    public double Physical(double logical) => host.Mirrored ? MaxLogical - logical : logical;

    /// <summary>The logical offset that lines slot <paramref name="slot"/> up at the snap point (not clamped).</summary>
    public double SnapOffset(int slot) => slot * Stride - _align;

    /// <summary><see cref="SnapOffset"/> within the scroll range (a strip that does not loop cannot pass its ends).</summary>
    public double ClampedSnapOffset(int slot) => Math.Clamp(SnapOffset(slot), 0, MaxLogical);

    /// <summary>Where slot <paramref name="slot"/> starts along the strip (logical: the layout mirrors frames in right to left).</summary>
    public double SlotStart(int slot) => PadStart + slot * Stride;

    /// <summary>The slot at the snap point, fractional while scrolling.</summary>
    public double FractionalSlot(double logical) => (logical + _align) / Stride;

    /// <summary>
    /// The slot nearest to the snap point. Halves round up (to the next slot), the same way in both directions: banker's
    /// rounding would round half-item offsets alternately down and up.
    /// </summary>
    public int NearestSlot(double logical) => (int)Math.Floor(FractionalSlot(logical) + 0.5);

    /// <summary>The item at the snap point, fractional (see <see cref="SkUiCarouselView.ScrollPosition"/>).</summary>
    public double ScrollPosition(double logical) =>
        Loops ? Mod(FractionalSlot(logical), Count) : Math.Clamp(FractionalSlot(logical), 0, Count - 1);

    /// <summary>How far the strip is past the nearest snap point, in items (-0.5 to 0.5).</summary>
    public double WithinItem(double logical) => HasGeometry ? FractionalSlot(logical) - NearestSlot(logical) : 0;

    /// <summary>The item shown in slot <paramref name="slot"/>.</summary>
    public int IndexOfSlot(int slot) => Loops ? (int)Mod(slot, Count) : slot;

    /// <summary><paramref name="value"/> modulo <paramref name="count"/>, never negative.</summary>
    public static double Mod(double value, double count) => (value % count + count) % count;

    /// <summary>The metrics an item effect gets, with the arranged thickness across the axis.</summary>
    public SkUiCarouselItemMetrics Metrics(double arrangedCross) =>
        new(Extent, double.IsFinite(arrangedCross) ? arrangedCross : 0, Spacing, ViewportLength, host.Horizontal);

    /// <summary>An item's position relative to the snap point (0 there, 1 one item after it; physical) at a physical offset.</summary>
    public double ItemPosition(int slot, double physical) => (Physical(SnapOffset(slot)) - physical) / Stride;

    /// <summary>The terms of an effect link for slot <paramref name="slot"/>: its position is <c>Factor · offset + Base</c> (physical).</summary>
    public (float Factor, float Base) EffectLinkTerms(int slot) => ((float)(-1 / Stride), (float)(Physical(SnapOffset(slot)) / Stride));

    #region Layout

    /// <summary>
    /// The viewport along the axis offered by the carousel's layout. Returns whether it changed (the items are laid out again
    /// from the current place, which <paramref name="logical"/> gives).
    /// </summary>
    public bool SetViewport(double viewport, double logical, bool arranged)
    {
        // Exact: a layout offering the same length needs nothing (NaN before the first).
        if (viewport == _viewport || (double.IsInfinity(viewport) && double.IsInfinity(_viewport)))
            return false;
        KeepCurrentPlace(logical, arranged);
        _viewport = viewport;
        return true;
    }

    /// <summary>The number of items changed (or the items did): the item position <paramref name="keep"/> shows once laid out.</summary>
    public void SetCount(int count, double keep)
    {
        Count = count;
        _keep = count > 0 ? keep : null;
        _placeRequested = true;
        _geometryValid = false;
    }

    /// <summary>The items' layout changes next: the current place (the fractional item at the snap point) is kept.</summary>
    public void KeepCurrentPlace(double logical, bool arranged)
    {
        if (HasGeometry && arranged)
            _keep ??= Loops ? Mod(FractionalSlot(logical), Count) : FractionalSlot(logical);
        _placeRequested = true;
        _geometryValid = false;
    }

    /// <summary>The items are laid out again at the next layout, from where they are.</summary>
    public void Invalidate() => _geometryValid = false;

    /// <summary>Whether a layout across <paramref name="cross"/> has to lay the items out (<see cref="UpdateGeometry"/>).</summary>
    public bool NeedsLayout(double cross) => !_geometryValid || cross != CrossConstraint; // exact: a constraint, compared as given

    /// <summary>
    /// Lays the items out across <paramref name="cross"/>. Returns whether they were placed anew (the slots show other items now:
    /// views are bound again), with the offset they are placed at pending (<see cref="TakePendingOffset"/>): the kept place, or
    /// <paramref name="position"/>.
    /// </summary>
    public bool UpdateGeometry(in SkUiCarouselLayout layout, double cross, int position)
    {
        CrossConstraint = cross;
        var horizontal = host.Horizontal;
        var insets = layout.PeekAreaInsets;
        var (start, end) = horizontal ? (host.Mirrored ? (insets.Right, insets.Left) : (insets.Left, insets.Right)) : (insets.Top, insets.Bottom);
        var spacing = layout.ItemSpacing;
        var extent = layout.ItemExtent > 0 ? layout.ItemExtent
            : double.IsFinite(_viewport) ? Math.Max(0, _viewport - start - end)
            : Count > 0 ? host.ProbeExtent(cross) : 0;
        var viewport = double.IsFinite(_viewport) ? _viewport : extent + start + end;
        var stride = extent + spacing;
        var inner = viewport - start - end;
        var align = layout.Alignment switch
        {
            SnapPointsAlignment.Center => (inner - extent) / 2,
            SnapPointsAlignment.End => inner - extent,
            _ => 0
        };
        var copies = 1;
        if (Loops && stride > 0)
        {
            // Room on either side of the middle copy for the drift before moving back, the longest fling, the viewport
            // and the items an effect shows.
            var cycle = Count * stride;
            var range = layout.Effect?.GetVisibleRange(new SkUiCarouselItemMetrics(extent, 0, spacing, viewport, horizontal)) ?? 0;
            var reach = RecenterDistance * cycle + SkUiGestureSettings.FlingMaximumVelocity * SkUiRenderFling.TotalTravelPerVelocity
                + viewport + (2 + range) * stride;
            copies = 2 * (int)Math.Ceiling(reach / cycle) + 1;
        }
        var slots = stride > 0 ? Count * copies : 0;
        var length = slots > 0 ? start + slots * stride - spacing + end : start + end;
        // Exact: any change of the layout's numbers lays the items out again from the kept place.
        var changed = !_geometryValid && (extent != Extent || stride != Stride || align != _align || copies != Copies || slots != Slots
            || start != PadStart || end != _padEnd || viewport != ViewportLength || length != Length);
        (Extent, Stride, Spacing, _align, Copies, Slots, PadStart, _padEnd, ViewportLength, Length) =
            (extent, stride, spacing, align, copies, slots, start, end, viewport, length);
        _geometryValid = true;
        var placed = false;
        if ((changed || _placeRequested || _keep is not null) && HasGeometry)
        {
            var place = _keep ?? Math.Min(position, Count - 1);
            _pendingLogical = Loops ? SnapOffset(HomeSlot) + place * Stride : Math.Clamp(place * Stride - _align, 0, MaxLogical);
            placed = true;
        }
        _keep = null;
        _placeRequested = false;
        return placed;
    }

    /// <summary>
    /// The slots that show at <paramref name="logical"/>, with one item of prefetch on either side, widened to
    /// <paramref name="effectRange"/> items on either side of the snap point (the items an effect shows).
    /// </summary>
    public (int First, int Last) SlotWindow(double logical, double effectRange)
    {
        // Slots that overlap the viewport widened by one stride on either side (touching it is not overlapping).
        var first = (int)Math.Floor((logical - Stride - PadStart - Extent) / Stride) + 1;
        var last = (int)Math.Ceiling((logical + ViewportLength + Stride - PadStart) / Stride) - 1;
        if (effectRange > 0)
        {
            var center = FractionalSlot(logical);
            first = Math.Min(first, (int)Math.Floor(center - effectRange));
            last = Math.Max(last, (int)Math.Ceiling(center + effectRange));
        }
        return (Math.Max(0, first), Math.Min(Slots - 1, last));
    }

    /// <summary>
    /// Whether slot <paramref name="slot"/> shows in the viewport at <paramref name="logical"/> (more than
    /// <see cref="VisibilityTolerance"/> of it), moved along the axis by an item effect's physical <paramref name="translation"/>
    /// and scaled about its center by <paramref name="scale"/>.
    /// </summary>
    public bool IsSlotInView(int slot, double logical, double translation = 0, double scale = 1)
    {
        var center = SlotStart(slot) + Extent / 2 + (host.Mirrored ? -translation : translation);
        var half = Extent * Math.Abs(scale) / 2;
        return center + half > logical + VisibilityTolerance && center - half < logical + ViewportLength - VisibilityTolerance;
    }

    /// <summary>The first and last slots in view at <paramref name="logical"/> (within the strip).</summary>
    public (int First, int Last) VisibleSlots(double logical) =>
        (Math.Clamp((int)Math.Floor((logical - PadStart) / Stride), 0, Slots - 1),
            Math.Clamp((int)Math.Floor((logical + ViewportLength - PadStart - 0.5) / Stride), 0, Slots - 1));

    #endregion

    #region Scrolling

    /// <summary>The item at the snap point at <paramref name="logical"/>: <paramref name="keep"/> while its (clamped) snap point is where the strip is.</summary>
    public int CurrentIndex(double logical, int keep)
    {
        if (!HasGeometry)
            return keep;
        if (!Loops)
        {
            // Items whose snap points the ends clamp share them: the current one stays current there.
            if (keep >= 0 && keep < Count && Math.Abs(ClampedSnapOffset(keep) - logical) < 0.5)
                return keep;
            return Math.Clamp(NearestSlot(logical), 0, Count - 1);
        }
        return IndexOfSlot(NearestSlot(logical));
    }

    /// <summary>The last item in view at <paramref name="logical"/> (not looping), or -1 before the layout.</summary>
    public int LastVisibleIndex(double logical) => HasGeometry
        ? Math.Clamp((int)Math.Floor((logical + ViewportLength - PadStart - 0.5) / Stride), 0, Count - 1)
        : -1;

    /// <summary>The logical offset of the item at <paramref name="index"/>: looping, the nearest copy of it (the shorter way round).</summary>
    public double TargetOffset(int index, double logical) => Loops ? SnapOffset(NearestSlotOf(index, logical)) : ClampedSnapOffset(index);

    /// <summary>The slot of the item at <paramref name="index"/> nearest to the snap point (the shorter way round when looping).</summary>
    public int NearestSlotOf(int index, double logical)
    {
        if (!Loops)
            return index;
        var current = NearestSlot(logical);
        var delta = (int)Mod(index - Mod(current, Count), Count);
        if (delta > Count / 2)
            delta -= Count;
        return current + delta;
    }

    /// <summary>
    /// The item at <paramref name="index"/> goes to the snap point. Returns <c>true</c> when the next layout places it (before
    /// the first arrange, or while a placement is pending): lay out again. <c>false</c>: scroll to
    /// <see cref="TargetOffset"/> now.
    /// </summary>
    public bool DeferPlacement(int index, double logical, bool arranged)
    {
        if (HasGeometry && arranged && _pendingLogical is null)
            return false;
        _keep = index;
        _placeRequested = true;
        if (HasGeometry)
            _pendingLogical = TargetOffset(index, logical);
        _geometryValid = false;
        return true;
    }

    /// <summary>After the scroller's arrange: the physical offset it takes (the placed or kept place), once; <c>null</c> when none waits.</summary>
    public double? TakePendingOffset()
    {
        if (_pendingLogical is not { } logical)
            return null;
        _pendingLogical = null;
        return HasGeometry ? Physical(Math.Clamp(logical, 0, MaxLogical)) : null;
    }

    /// <summary>
    /// Looping: once the strip is far from its middle copy at <paramref name="logical"/>, the whole cycles to move it back by
    /// (what shows stays the same: slots shift by cycles times the count), with the physical scroll correction; 0 otherwise.
    /// </summary>
    public int RecenterCycles(double logical, out double physicalCorrection)
    {
        physicalCorrection = 0;
        if (!Loops || !HasGeometry || _pendingLogical is not null)
            return 0;
        var cycle = CycleLength;
        var drift = logical - SnapOffset(HomeSlot);
        if (Math.Abs(drift) < RecenterDistance * cycle)
            return 0;
        var cycles = (int)Math.Round(drift / cycle);
        if (cycles == 0)
            return 0;
        var correction = -cycles * cycle;
        physicalCorrection = host.Mirrored ? -correction : correction;
        return cycles;
    }

    /// <summary>Adds the physical snap offsets between physical <paramref name="from"/> and <paramref name="to"/> to <paramref name="points"/>.</summary>
    public void GetSnapOffsets(double from, double to, List<double> points)
    {
        if (!HasGeometry)
            return;
        var (low, high) = host.Mirrored ? (Logical(to), Logical(from)) : (from, to);
        var first = Math.Max(0, (int)Math.Ceiling((low + _align) / Stride));
        var last = Math.Min(Slots - 1, (int)Math.Floor((high + _align) / Stride));
        var loops = Loops;
        if (!loops)
        {
            // Clamped snap points of the first and last items lie at the ends.
            first = Math.Min(first, low <= 0 ? 0 : first);
            last = Math.Max(last, high >= MaxLogical ? Slots - 1 : last);
        }
        if (last - first >= MaxSnapPoints)
            last = first + MaxSnapPoints - 1;
        for (var slot = first; slot <= last; slot++)
            points.Add(Physical(loops ? SnapOffset(slot) : ClampedSnapOffset(slot)));
    }

    #endregion
}
