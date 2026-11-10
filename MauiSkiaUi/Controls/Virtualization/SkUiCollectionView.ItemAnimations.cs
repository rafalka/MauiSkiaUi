using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

// Animated inserts, removes and moves (AnimatesItemChanges): before a change of the rows, where every realized item shows
// in the viewport is noted; after it, the items that moved are drawn from their old places back to their new ones,
// inserted items fade in, and the views of removed items fade out where they were (in a host under the items) before they
// go back to their pool. Everything runs on the render thread through composite-time properties while the views keep
// their final values, so nothing re-records and taps hit the final layout.
public partial class SkUiCollectionView
{
    /// <summary>What a realized view of the rows shows.</summary>
    private enum MotionPart : byte { Item, Header, Footer }

    /// <summary>An item, group header or group footer, by what it shows (views are recycled and grid cells rebound).</summary>
    private readonly record struct MotionKey(MotionPart Part, object? Value);

    /// <summary>A view drawn from an offset back to its place: where it started, when, for how long, how.</summary>
    private readonly record struct Motion(Point From, long Started, double Length, Easing Easing);

    /// <summary>A removed item's view fading out: its recycle key, and the views generation it belongs to.</summary>
    private sealed record Ghost(SkUiView View, object Key, Rect Place, int Generation);

    private bool? _animatesItemChanges;
    // While a change runs: the realized parts' places in the viewport before it, the parts it inserted, the removed views.
    private Dictionary<MotionKey, Rect>? _placesBefore;
    private HashSet<MotionKey>? _insertedParts;
    private List<Ghost>? _removedViews;
    private readonly Dictionary<SkUiView, Motion> _motions = [];
    private readonly List<Ghost> _ghosts = [];
    // Bumped when recycled views are dropped (a template changed): fading views of before then are not recycled.
    private int _viewsGeneration;

    /// <summary>Bindable property for <see cref="AnimatesItemChanges"/>.</summary>
    public static readonly BindableProperty AnimatesItemChangesProperty = BindableProperty.Create(nameof(AnimatesItemChanges), typeof(bool?),
        typeof(SkUiCollectionView), null,
        propertyChanged: (view, _, value) => ((SkUiCollectionView)view)._animatesItemChanges = (bool?)value);

    /// <summary>
    /// Whether inserted, removed and moved items animate, as native lists do: items that move glide to their new places,
    /// inserted ones fade in and removed ones fade out where they were (also the rows of groups expanding and collapsing).
    /// <c>null</c> (default): the look's <see cref="SkUiLook.CollectionViewAnimatesItemChanges"/> (<c>false</c> in 1.x).
    /// Durations and curves are the look's (<see cref="SkUiTransitionKind.ItemInsert"/>, <see cref="SkUiTransitionKind.ItemRemove"/>,
    /// <see cref="SkUiTransitionKind.ItemMove"/>). Only items in view animate; resets, changes during a fling or an animated
    /// scroll, a reorder's drop and Reduce Motion do not. The animation runs on the render thread; taps hit the final places.
    /// </summary>
    public bool? AnimatesItemChanges { get => (bool?)GetValue(AnimatesItemChangesProperty); set => SetValue(AnimatesItemChangesProperty, value); }

    /// <summary>Sets <see cref="AnimatesItemChanges"/> (same as the property setter).</summary>
    public SkUiCollectionView SetAnimatesItemChanges(bool? value)
    {
        AnimatesItemChanges = value;
        return this;
    }

    /// <summary>Whether a change of the rows starting now animates.</summary>
    private bool AnimatesChangeNow =>
        (_animatesItemChanges ?? SkUiLookScope.Resolve(this).CollectionViewAnimatesItemChanges)
        && _drag is null && !_committingReorder && IsShown && ItemsHost.HasWindow && !Scroller.IsMotionRunning;

    /// <summary>Whether the change running now fades removed rows out (their views are taken from the engine).</summary>
    private bool FadesRemovedRows => _placesBefore is not null;

    /// <summary>The fading views of removed items (tests).</summary>
    internal int FadingViewCount => _ghosts.Count;

    /// <summary>The offset a view is drawn from towards its place, as it started, or <c>null</c> (tests).</summary>
    internal Point? MotionFrom(SkUiView view) => _motions.TryGetValue(view, out var motion) && Offset(motion) is not null ? motion.From : null;

    /// <summary>Whether a view fades in (tests).</summary>
    internal bool IsFadingIn(SkUiView view) => _fadingIn.Contains(view);

    private readonly HashSet<SkUiView> _fadingIn = [];

    /// <summary>A change of the rows starts: the first one of a change notes where the realized parts show.</summary>
    private void BeforeRowsChange()
    {
        if (_placesBefore is not null || !AnimatesChangeNow)
            return;
        var places = new Dictionary<MotionKey, Rect>();
        foreach (var (view, key) in RealizedParts())
            if (PlaceOf(view) is { } place)
                places.TryAdd(key, Offset(view) is { } offset ? place.Offset(offset.X, offset.Y) : place);
        _placesBefore = places;
        _insertedParts = [];
    }

    /// <summary>Rows were inserted (or replaced): their parts fade in instead of moving.</summary>
    private void NoteInsertedRows(int row, int count)
    {
        if (_insertedParts is not { } inserted)
            return;
        for (var index = row; index < row + count && index < Model.RowCount; index++)
        {
            var current = Model.RowAt(index);
            switch (current.Kind)
            {
                case RowKind.Items:
                    for (var slot = 0; slot < current.Count; slot++)
                        inserted.Add(new MotionKey(MotionPart.Item, Model.ItemAt(current.Group, current.Start + slot)));
                    break;
                case RowKind.GroupHeader:
                    inserted.Add(new MotionKey(MotionPart.Header, Model.Groups[current.Group].Value));
                    break;
                default:
                    inserted.Add(new MotionKey(MotionPart.Footer, Model.Groups[current.Group].Value));
                    break;
            }
        }
    }

    /// <summary>The engine releases a removed row's view: one that showed (a list row, not a grid row) is kept to fade out.</summary>
    private bool TakeRemovedView(ISkUiView view, object? key)
    {
        if (key is null || view is GridRowPart || view is not SkUiView drawn || _placesBefore is not { } places
            || !places.TryGetValue(KeyOf(drawn), out var place))
            return false;
        (_removedViews ??= []).Add(new Ghost(drawn, key, place, _viewsGeneration));
        return true;
    }

    /// <summary>
    /// A change of the items ended (<see cref="OnItemsChanged"/>). The animation waits for the next layout, so the changes of
    /// one frame (a burst of inserts and removes) animate together from where things showed before the first; a reset drops it.
    /// </summary>
    private void ItemChangeEnded(bool reset)
    {
        if (reset && _placesBefore is not null)
            DropPendingItemAnimation();
    }

    private void DropPendingItemAnimation()
    {
        var removed = _removedViews;
        _placesBefore = null;
        _insertedParts = null;
        _removedViews = null;
        if (removed is not null)
            foreach (var ghost in removed)
                Recycle(ghost);
    }

    /// <summary>
    /// After the layout that followed changes of the items: the parts that moved in the viewport glide from their old places,
    /// inserted ones fade in, parts that came into view move with the ones before them, removed views fade out.
    /// </summary>
    private void AnimateItemChanges()
    {
        var before = _placesBefore;
        var inserted = _insertedParts;
        var removed = _removedViews;
        _placesBefore = null;
        _insertedParts = null;
        _removedViews = null;
        if (before is null)
            return;
        var look = SkUiLookScope.Resolve(this);
        var move = look.GetTransition(SkUiTransitionKind.ItemMove);
        var insert = look.GetTransition(SkUiTransitionKind.ItemInsert);
        Point? lastShift = null;
        List<SkUiView>? waiting = null;
        foreach (var (view, key) in RealizedParts())
        {
            if (PlaceOf(view) is not { } place)
                continue;
            if (before.TryGetValue(key, out var old))
            {
                var shift = new Point(old.X - place.X, old.Y - place.Y);
                Glide(view, shift, move);
                // Parts that came into view before the first one that showed move with it.
                if (waiting is not null && lastShift is null)
                    foreach (var earlier in waiting)
                        Glide(earlier, shift, move);
                waiting = null;
                lastShift = shift;
            }
            else if (inserted?.Contains(key) == true)
            {
                FadeIn(view, insert);
            }
            else if (lastShift is { } shift)
            {
                Glide(view, shift, move); // came into view: moves as the part before it
            }
            else
            {
                (waiting ??= []).Add(view);
            }
        }
        if (removed is not null)
            FadeOut(removed, look.GetTransition(SkUiTransitionKind.ItemRemove));
    }

    /// <summary>Draws <paramref name="view"/> from <paramref name="shift"/> back to its place (both axes: grid cells move across too).</summary>
    private void Glide(SkUiView view, Point shift, SkUiTransition transition)
    {
        const double Still = 0.5; // DIPs: closer than this, the part did not move
        if (transition.IsNone || (Math.Abs(shift.X) < Still && Math.Abs(shift.Y) < Still))
            return;
        var length = (uint)Math.Max(1, transition.Duration.TotalMilliseconds);
        var easing = transition.Easing ?? Easing.Linear;
        var motion = new Motion(shift, System.Diagnostics.Stopwatch.GetTimestamp(), length, easing);
        _motions[view] = motion;
        _ = view.AnimateFromAsync(SkUiAnimatableProperty.TranslationX, view.TranslationX + shift.X, length, easing);
        // The render thread's end (or a newer glide replacing it) ends the bookkeeping too: the clock above only estimates
        // where the view is drawn while it glides.
        _ = view.AnimateFromAsync(SkUiAnimatableProperty.TranslationY, view.TranslationY + shift.Y, length, easing, _ =>
        {
            if (_motions.TryGetValue(view, out var current) && current == motion)
                _motions.Remove(view);
        });
    }

    private void FadeIn(SkUiView view, SkUiTransition transition)
    {
        if (transition.IsNone)
            return;
        _fadingIn.Add(view);
        _ = view.AnimateFromAsync(SkUiAnimatableProperty.Opacity, 0, (uint)Math.Max(1, transition.Duration.TotalMilliseconds), transition.Easing,
            _ => _fadingIn.Remove(view));
    }

    /// <summary>Shows the removed views where they were, in the host under the items, and fades them out; each goes to its pool after.</summary>
    private void FadeOut(List<Ghost> removed, SkUiTransition transition)
    {
        if (transition.IsNone || SkUiScrollController.GetContentBounds(Scroller, ItemsHost) is not { } items)
        {
            foreach (var ghost in removed)
                Recycle(ghost);
            return;
        }
        var host = Body.GhostHost ??= Body.Adopt(new GhostPart());
        var (scrollX, scrollY) = (Scroller.ScrollX, Scroller.ScrollY);
        var length = (uint)Math.Max(1, transition.Duration.TotalMilliseconds);
        foreach (var ghost in removed)
        {
            // Its place in the viewport, back in the items' coordinates as they are scrolled now.
            var place = ghost.Place.Offset(scrollX - items.X, scrollY - items.Y);
            _ghosts.Add(ghost);
            host.Show(ghost.View, place);
            ghost.View.Opacity = 0; // where it ends; drawn from opaque
            _ = ghost.View.AnimateFromAsync(SkUiAnimatableProperty.Opacity, 1, length, transition.Easing, _ => EndFadeOut(ghost));
        }
    }

    private void EndFadeOut(Ghost ghost)
    {
        if (!_ghosts.Remove(ghost))
            return;
        Body.GhostHost?.Hide(ghost.View);
        Recycle(ghost);
    }

    /// <summary>A removed view goes back to its pool (unless its template's views were dropped meanwhile), opaque again.</summary>
    private void Recycle(Ghost ghost)
    {
        ghost.View.Opacity = 1;
        if (ghost.Generation == _viewsGeneration)
            ItemsHost.RecycleView(ghost.View, ghost.Key);
    }

    /// <summary>Views were dropped (a template changed, the source replaced): fading views are dropped with them.</summary>
    private void DropFadingViews()
    {
        _viewsGeneration++;
        // Copied first: hiding a view ends its fade, whose end handler looks for it in the list.
        Ghost[] fading = [.. _ghosts];
        _ghosts.Clear();
        foreach (var ghost in fading)
        {
            Body.GhostHost?.Hide(ghost.View);
            ghost.View.Opacity = 1;
        }
        DropPendingItemAnimation(); // removed views waiting for the layout are not recycled either (older generation)
    }

    /// <summary>The realized parts, in row order: the rows' views, and the cells of grid rows.</summary>
    private IEnumerable<(SkUiView View, MotionKey Key)> RealizedParts()
    {
        foreach (var view in ItemsHost.RealizedViews)
        {
            if (view is GridRowPart rowPart)
            {
                foreach (var cell in rowPart.Cells)
                    yield return (cell, KeyOf(cell));
            }
            else if (view is SkUiView drawn)
            {
                yield return (drawn, KeyOf(drawn));
            }
        }
    }

    private static MotionKey KeyOf(SkUiView view) => new(view switch
    {
        ItemHost => MotionPart.Item,
        GroupHeaderHost => MotionPart.Header,
        _ => MotionPart.Footer
    }, ((BindableObject)view).BindingContext);

    /// <summary>Where <paramref name="view"/> is laid out in the list's viewport (its scroll content less the scroll offset).</summary>
    private Rect? PlaceOf(SkUiView view) =>
        SkUiScrollController.GetContentBounds(Scroller, view) is { } bounds ? bounds.Offset(-Scroller.ScrollX, -Scroller.ScrollY) : null;

    /// <summary>Where a gliding view is drawn now, from its place, or <c>null</c> (not gliding any more).</summary>
    private Point? Offset(SkUiView view)
    {
        if (!_motions.TryGetValue(view, out var motion))
            return null;
        if (Offset(motion) is { } offset)
            return offset;
        _motions.Remove(view);
        return null;
    }

    private static Point? Offset(Motion motion)
    {
        var progress = System.Diagnostics.Stopwatch.GetElapsedTime(motion.Started).TotalMilliseconds / motion.Length;
        if (progress >= 1)
            return null;
        var left = 1 - motion.Easing.Ease(Math.Max(0, progress));
        return new Point(motion.From.X * left, motion.From.Y * left);
    }

    /// <summary>Holds the views of removed items while they fade out: under the items, input-transparent, hidden from screen readers.</summary>
    private sealed class GhostPart : SkUiView
    {
        private readonly List<(SkUiView View, Rect Place)> _views = [];

        public GhostPart()
        {
            InputTransparent = true;
            ClipToBounds = false;
            AutomationProperties.SetExcludedWithChildren(this, true);
        }

        public void Show(SkUiView view, Rect place)
        {
            _views.Add((view, place));
            AttachChild(view);
            InvalidateMeasure();
        }

        public void Hide(SkUiView view)
        {
            var index = _views.FindIndex(entry => ReferenceEquals(entry.View, view));
            if (index < 0)
                return;
            _views.RemoveAt(index);
            DetachChild(view);
            InvalidateMeasure();
        }

        internal override IEnumerable<ISkUiView> SkiaChildren
        {
            get
            {
                foreach (var (view, _) in _views)
                    yield return view;
            }
        }

        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            foreach (var (view, place) in _views)
                ((IView)view).Measure(place.Width, place.Height);
            return Size.Zero;
        }

        protected override void ArrangeContent(Size size)
        {
            foreach (var (view, place) in _views)
                ((IView)view).Arrange(place);
        }
    }
}
