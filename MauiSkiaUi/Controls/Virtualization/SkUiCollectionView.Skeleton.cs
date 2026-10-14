using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>Placeholder items in a skeleton made from content (FR-31, C9b): an empty list in a loading shimmer shows rows of its template.</summary>
public partial class SkUiCollectionView
{
    /// <summary>The most placeholder rows that fill a viewport.</summary>
    private const int MaxPlaceholderRows = 50;

    /// <summary>Placeholder rows when the list is not bounded along its axis (nothing to fill).</summary>
    private const int UnboundedPlaceholderRows = 3;

    private bool _showsPlaceholders;
    private double _viewportLength = double.NaN;

    /// <summary>
    /// Whether the list shows placeholder items: it is empty, in a skeleton made from a shimmer's content
    /// (<see cref="SkUiShimmer.ShowsContentAsSkeleton"/>), and <see cref="SkUiShimmer.SkeletonItemCountProperty"/> is not 0.
    /// The items are its <see cref="ItemTemplate"/>'s views without an item (no binding context): their texts draw placeholder
    /// lines, their shapes bones. They are not items: no selection, no events, no recycling.
    /// </summary>
    public bool ShowsPlaceholders => _showsPlaceholders;

    /// <summary>Whether the empty view shows (tests).</summary>
    internal bool ShowsEmptyView => _body.EmptyHost.IsVisible;

    /// <summary>The placeholder items shown (tests).</summary>
    internal IReadOnlyList<ISkUiView> PlaceholderItems => _body.PlaceholderHost?.Views ?? [];

    /// <summary>Whether an empty list should show placeholder items now.</summary>
    private bool WantsPlaceholders() =>
        SkeletonState is { InSkeleton: true } && SkUiShimmer.GetSkeletonItemCount(this) != 0
        && SkUiSkeletonScope.Resolve(this) is { Shimmer: not null, Draw: SkUiSkeletonDraw.Container or SkUiSkeletonDraw.Children };

    /// <summary>Shows or drops the placeholder items (dropped views go: a later skeleton makes new ones).</summary>
    private void SetPlaceholders(bool show)
    {
        if (show == _showsPlaceholders)
            return;
        _showsPlaceholders = show;
        if (show)
            (_body.PlaceholderHost ??= _body.Adopt(new PlaceholderPart(this))).IsVisible = true;
        else if (_body.PlaceholderHost is { } host)
        {
            host.Clear();
            host.IsVisible = false;
        }
        OnPropertyChanged(nameof(ShowsPlaceholders));
    }

    /// <inheritdoc />
    internal override void OnSkeletonChanged()
    {
        base.OnSkeletonChanged();
        _body.PlaceholderHost?.InvalidateMeasure(); // the count may have changed
        UpdateEmpty();
    }

    /// <summary>A placeholder item: the template's view for no item (a selector is asked with <c>null</c>), else a default label.</summary>
    private ISkUiView CreatePlaceholderItem()
    {
        var view = SelectTemplate(_itemTemplate, null) is { } template ? CreateFromTemplate(template, nameof(ItemTemplate)) : new SkUiLabel();
        // No item: bindings give their defaults (empty texts draw placeholder lines), not the page's context inherited.
        ((BindableObject)view).BindingContext = null;
        return view;
    }

    /// <summary>
    /// The placeholder items, laid out as the list lays out its items (<see cref="Span"/> per row, <see cref="ItemSpacing"/>
    /// between rows, <see cref="SpanSpacing"/> between items of a row): as many rows as <see cref="SkUiShimmer.SkeletonItemCountProperty"/>
    /// asks, or enough to fill the list's viewport.
    /// </summary>
    private sealed class PlaceholderPart(SkUiCollectionView owner) : SkUiView
    {
        private readonly List<ISkUiView> _views = [];
        private readonly List<double> _rows = []; // each row's length along the axis

        internal override IEnumerable<ISkUiView> SkiaChildren => _views;

        internal override bool IsSkeletonContainer => true;

        /// <summary>The placeholder items (tests).</summary>
        public IReadOnlyList<ISkUiView> Views => _views;

        /// <summary>Drops the items (the template changed, or the list is no longer a skeleton).</summary>
        public void Clear()
        {
            SetCount(0);
            InvalidateMeasure();
        }

        // Added during measure as the virtual list adds items: without invalidating the measure being taken.
        private void SetCount(int count)
        {
            var changed = false;
            while (_views.Count < count)
            {
                var view = owner.CreatePlaceholderItem();
                ValidateChild(view);
                _views.Add(view);
                AddLogicalChild((Element)view);
                changed = true;
            }
            while (_views.Count > count)
            {
                var view = _views[^1];
                _views.RemoveAt(_views.Count - 1);
                RemoveLogicalChild((Element)view);
                changed = true;
            }
            if (changed)
                InvalidateRender(SkUiRenderDirty.Children);
        }

        private (int Span, double Cell) Cells(double across)
        {
            var span = Math.Max(1, owner.Span);
            var cell = double.IsFinite(across) ? Math.Max(0, (across - (span - 1) * owner.SpanSpacing) / span) : double.PositiveInfinity;
            return (span, cell);
        }

        /// <summary>Measures the items of the row starting at <paramref name="start"/>; returns its length along and the items' extent across.</summary>
        private (double Along, double Across) MeasureRow(int start, int span, double cell)
        {
            var horizontal = owner._horizontal;
            var (along, across) = (0d, 0d);
            for (var index = start; index < Math.Min(_views.Count, start + span); index++)
            {
                var size = horizontal ? _views[index].Measure(double.PositiveInfinity, cell) : _views[index].Measure(cell, double.PositiveInfinity);
                along = Math.Max(along, owner.Along(size));
                across += owner.Across(size) + (index > start ? owner.SpanSpacing : 0);
            }
            return (along, across);
        }

        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            var horizontal = owner._horizontal;
            var available = horizontal ? heightConstraint : widthConstraint;
            var (span, cell) = Cells(available);
            var spacing = owner.ItemSpacing;
            var requested = SkUiShimmer.GetSkeletonItemCount(owner);
            int count;
            if (requested > 0)
            {
                count = requested;
            }
            else
            {
                // Enough rows to fill the viewport, by the first row's length.
                SetCount(span);
                var first = MeasureRow(0, span, cell).Along;
                var viewport = owner._viewportLength;
                var rows = double.IsFinite(viewport) && viewport > 0
                    ? (int)Math.Ceiling((viewport + spacing) / Math.Max(1, first + spacing))
                    : UnboundedPlaceholderRows;
                count = Math.Clamp(rows, 1, MaxPlaceholderRows) * span;
            }
            SetCount(count);
            _rows.Clear();
            var (length, extent) = (0d, 0d);
            for (var start = 0; start < count; start += span)
            {
                var (along, across) = MeasureRow(start, span, cell);
                _rows.Add(along);
                length += along + (start > 0 ? spacing : 0);
                extent = Math.Max(extent, across);
            }
            if (span > 1 && double.IsFinite(available))
                extent = available;
            return horizontal ? new Size(length, extent) : new Size(extent, length);
        }

        protected override void ArrangeContent(Size size)
        {
            var horizontal = owner._horizontal;
            var (span, cell) = Cells(owner.Across(size));
            var position = 0d;
            for (var row = 0; row < _rows.Count; row++)
            {
                var length = _rows[row];
                for (var column = 0; column < span && row * span + column < _views.Count; column++)
                {
                    var offset = column * (cell + owner.SpanSpacing);
                    var width = span == 1 ? owner.Across(size) : cell;
                    _views[row * span + column].Arrange(horizontal ? new Rect(position, offset, length, width) : new Rect(offset, position, width, length));
                }
                position += length + owner.ItemSpacing;
            }
        }
    }
}
