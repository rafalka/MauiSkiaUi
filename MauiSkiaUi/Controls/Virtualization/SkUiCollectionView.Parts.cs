using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

public partial class SkUiCollectionView
{
    /// <summary>The recycle key of items without a template (a default label).</summary>
    private static readonly object DefaultItemKey = new();

    /// <summary>The recycle key of grid rows (their cells are recycled per template on their own).</summary>
    private static readonly object GridRowKey = new();

    /// <summary>The recycle key of a group header or footer made by a template.</summary>
    private sealed record KindKey(RowKind Kind, DataTemplate Template);

    private readonly Dictionary<(RowKind, DataTemplate), KindKey> _kindKeys = [];

    /// <summary>Grid cells waiting for reuse, per item template (a grid row whose items need other templates swaps cells).</summary>
    private readonly Dictionary<object, Stack<ItemHost>> _cellPool = [];

    #region Rows

    private DataTemplate? SelectTemplate(DataTemplate? template, object? item) =>
        template is DataTemplateSelector selector ? selector.SelectTemplate(item, this) : template;

    private static ISkUiView CreateFromTemplate(DataTemplate template, string property) =>
        template.CreateContent() as ISkUiView
        ?? throw new InvalidOperationException($"{property} of {nameof(SkUiCollectionView)} must create drawn (SkUi*) views; put native views inside an SkUiMauiContentView.");

    /// <summary>The recycle key of an item: its (selected) template, or one key for default labels.</summary>
    private object ItemKey(object? item) => (object?)SelectTemplate(_itemTemplate, item) ?? DefaultItemKey;

    private KindKey KindKeyOf(RowKind kind, DataTemplate template)
    {
        if (!_kindKeys.TryGetValue((kind, template), out var key))
            _kindKeys[(kind, template)] = key = new KindKey(kind, template);
        return key;
    }

    private object? RecycleKeyOf(int rowIndex)
    {
        var row = _model.RowAt(rowIndex);
        return row.Kind switch
        {
            RowKind.Items => _model.Span > 1 ? GridRowKey : ItemKey(_model.ItemAt(row.Group, row.Start)),
            RowKind.GroupHeader => SelectTemplate(GroupHeaderTemplate, _model.Groups[row.Group].Value) is { } header ? KindKeyOf(RowKind.GroupHeader, header) : null,
            _ => SelectTemplate(GroupFooterTemplate, _model.Groups[row.Group].Value) is { } footer ? KindKeyOf(RowKind.GroupFooter, footer) : null
        };
    }

    private ISkUiView CreateRowView(int rowIndex, object? key) => key switch
    {
        _ when ReferenceEquals(key, GridRowKey) => new GridRowPart(this),
        KindKey { Kind: RowKind.GroupHeader } header => new GroupHeaderHost(this) { Content = CreateFromTemplate(header.Template, nameof(GroupHeaderTemplate)) },
        KindKey footer => CreateFromTemplate(footer.Template, nameof(GroupFooterTemplate)),
        null => CreateGroupRowWithoutTemplate(rowIndex),
        _ => CreateCell(key)
    };

    /// <summary>A header or footer whose selector chose no template: an empty view (not recycled).</summary>
    private ISkUiView CreateGroupRowWithoutTemplate(int rowIndex) =>
        _model.RowAt(rowIndex).Kind == RowKind.GroupHeader ? new GroupHeaderHost(this) : new SkUiContentView();

    /// <summary>An item container with the view of <paramref name="key"/>'s template (or a default label).</summary>
    private ItemHost CreateCell(object key)
    {
        var content = key is DataTemplate template ? CreateFromTemplate(template, nameof(ItemTemplate)) : new SkUiLabel();
        return new ItemHost(this, key) { Content = content, ShowsPressEffect = ShowsItemPressEffect };
    }

    /// <summary>A grid cell for <paramref name="key"/>: a pooled one, or a new one.</summary>
    private ItemHost TakeCell(object key) =>
        _cellPool.TryGetValue(key, out var pooled) && pooled.TryPop(out var cell) ? cell : CreateCell(key);

    private void ReturnCell(ItemHost cell)
    {
        if (!_cellPool.TryGetValue(cell.TemplateKey, out var pooled))
            _cellPool[cell.TemplateKey] = pooled = new Stack<ItemHost>();
        if (pooled.Count < 64)
            pooled.Push(cell);
    }

    private void BindRow(int rowIndex, ISkUiView view, object? key)
    {
        var row = _model.RowAt(rowIndex);
        switch (view)
        {
            case GridRowPart grid:
                grid.Bind(row);
                break;
            case ItemHost host:
                BindItemHost(host, _model.ItemAt(row.Group, row.Start));
                break;
            case GroupHeaderHost header:
                header.StickyGroup = -1;
                header.BindingContext = _model.Groups[row.Group].Value;
                header.SetExpanded(_model.Groups[row.Group].Expanded);
                break;
            default:
                ((BindableObject)view).BindingContext = _model.Groups[row.Group].Value;
                break;
        }
    }

    /// <summary>Shows an item in its container: binding context, default label text, selected state.</summary>
    private void BindItemHost(ItemHost host, object? item)
    {
        ((BindableObject)host).BindingContext = item;
        if (ReferenceEquals(host.TemplateKey, DefaultItemKey) && host.Content is SkUiLabel label)
            label.Text = item?.ToString() ?? string.Empty;
        host.IsSelected = ShowsSelection(item);
    }

    private object? RowItem(int rowIndex)
    {
        var row = _model.RowAt(rowIndex);
        return row.Kind == RowKind.Items ? _model.ItemAt(row.Group, row.Start) : _model.Groups[row.Group].Value;
    }

    #endregion

    /// <summary>The rows: an engine whose items are the model's rows.</summary>
    private sealed class ItemsPart(SkUiCollectionView owner) : SkUiVirtualVerticalStackLayoutBase
    {
        public void Reset(int count) => ResetItems(count);

        public void InsertRows(int row, int count) => InsertItems(row, count);

        public void RemoveRows(int row, int count) => RemoveItems(row, count);

        public void ReplaceRows(int row, int count) => ReplaceItems(row, count);

        public void MoveRows(int row, int count, int newRow) => MoveItems(row, count, newRow);

        public void DropRecycled() => ClearRecycledViews();

        protected override object? GetRecycleKey(int index) => owner.RecycleKeyOf(index);

        protected override ISkUiView? CreateItemView(int index, object? recycleKey) => owner.CreateRowView(index, recycleKey);

        protected override void BindItemView(int index, ISkUiView view, object? recycleKey) => owner.BindRow(index, view, recycleKey);

        protected override object? GetItem(int index) => owner.RowItem(index);
    }

    /// <summary>
    /// Hosts a sticky part over the list (header, footer, the current group's header). A drag on it scrolls the list (it has
    /// its own drag recognizer for the list's scroller), a press stops a fling, the wheel scrolls the list, and a tap does not
    /// reach the items behind it; views inside it keep their taps.
    /// </summary>
    private sealed class StickyHost(SkUiScrollController scroller) : SkUiContentView, ISkUiWheelProxy
    {
        private SkUiScrollGestureRecognizer? _drag;

        SkUiScrollController ISkUiWheelProxy.WheelScroller => scroller;

        internal override void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
        {
            base.CollectGestureRecognizers(recognizers);
            if (Content is not null && scroller.Orientation != ScrollOrientation.Neither)
                recognizers.Add(_drag ??= new SkUiScrollGestureRecognizer(scroller));
        }

        internal override void CancelGestures()
        {
            base.CancelGestures();
            _drag?.Cancel();
        }
    }

    /// <summary>Hosts an item's view: takes its taps, draws the selection background, and puts its root in the <c>Selected</c> state.</summary>
    private sealed class ItemHost(SkUiCollectionView owner, object templateKey) : SkUiContentView
    {
        private bool _selected;

        /// <summary>The template (or default-label key) its view comes from.</summary>
        public object TemplateKey { get; } = templateKey;

        public bool IsSelected
        {
            get => _selected;
            set
            {
                if (_selected == value)
                    return;
                _selected = value;
                if (Content is SkUiView root)
                    root.IsSelectedItem = value;
                InvalidatePaint();
                InvalidateSemantics();
            }
        }

        protected override bool HandlesTap => owner.WantsItemTaps;

        protected override void OnTapped(SkUiTappedEventArgs args)
        {
            RaiseTapped(args);
            owner.OnItemHostTapped(this);
        }

        protected override void OnPaintBackground(SKCanvas canvas)
        {
            base.OnPaintBackground(canvas);
            if (!_selected)
                return;
            var rect = new SKRect(0, 0, (float)Width, (float)Height);
            if (owner._selectionBackground is { } brush)
                SkUiShapePainter.FillRect(canvas, rect, (Paint?)brush);
            else
                SkUiShapePainter.FillRect(canvas, rect, SkUiFill.From(SkUiColors.Accent.WithAlpha(0.12f)));
        }

        protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
        {
            base.OnPopulateSemantics(info);
            if (_selected)
                info.Value = SelectedStateText();
        }
    }

    /// <summary>
    /// A grid row: up to <see cref="Span"/> item containers side by side (stacked in a horizontal list), as long as its longest
    /// item. Recycled as a whole; its cells are kept while their template fits the item they show next, else swapped with
    /// the list's cell pool.
    /// </summary>
    private sealed class GridRowPart : SkUiView
    {
        private readonly SkUiCollectionView _owner;
        private readonly List<ItemHost> _cells = [];

        public GridRowPart(SkUiCollectionView owner)
        {
            _owner = owner;
            ClipToBounds = false;
        }

        public IReadOnlyList<ItemHost> Cells => _cells;

        public int IndexOf(ItemHost cell) => _cells.IndexOf(cell);

        public ItemHost? CellAt(int slot) => (uint)slot < (uint)_cells.Count ? _cells[slot] : null;

        /// <summary>Shows the items of <paramref name="row"/>.</summary>
        public void Bind(Row row)
        {
            for (var slot = 0; slot < row.Count; slot++)
            {
                var item = _owner._model.ItemAt(row.Group, row.Start + slot);
                var key = _owner.ItemKey(item);
                if (slot == _cells.Count)
                {
                    var cell = _owner.TakeCell(key);
                    _cells.Add(cell);
                    AttachChild(cell);
                }
                else if (!Equals(_cells[slot].TemplateKey, key))
                {
                    // Another template: the cell goes back to the pool, one of the right template takes its place.
                    var old = _cells[slot];
                    DetachChild(old);
                    _owner.ReturnCell(old);
                    var cell = _owner.TakeCell(key);
                    _cells[slot] = cell;
                    AttachChild(cell);
                }
                _owner.BindItemHost(_cells[slot], item);
            }
            while (_cells.Count > row.Count)
                Release(_cells.Count - 1);
        }

        private void Release(int slot)
        {
            var cell = _cells[slot];
            _cells.RemoveAt(slot);
            DetachChild(cell);
            _owner.ReturnCell(cell);
        }

        internal override IEnumerable<ISkUiView> SkiaChildren => _cells;

        /// <summary>The size of a cell across the row, and the gap between cells.</summary>
        private (double Cell, double Gap) CellSize(double across)
        {
            var span = _owner._model.Span;
            var gap = _owner._spanSpacing;
            return (Math.Max(0, (across - (span - 1) * gap) / span), gap);
        }

        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            var horizontal = _owner._horizontal;
            var (cell, _) = CellSize(horizontal ? heightConstraint : widthConstraint);
            var length = 0d;
            foreach (var host in _cells)
            {
                var size = horizontal ? ((IView)host).Measure(widthConstraint, cell) : ((IView)host).Measure(cell, heightConstraint);
                length = Math.Max(length, horizontal ? size.Width : size.Height);
            }
            return horizontal ? new Size(length, heightConstraint) : new Size(widthConstraint, length);
        }

        protected override void ArrangeContent(Size size)
        {
            var horizontal = _owner._horizontal;
            var (cell, gap) = CellSize(horizontal ? size.Height : size.Width);
            for (var slot = 0; slot < _cells.Count; slot++)
            {
                var at = slot * (cell + gap);
                ((IView)_cells[slot]).Arrange(horizontal ? new Rect(0, at, size.Width, cell) : new Rect(at, 0, cell, size.Height));
            }
        }
    }

    /// <summary>
    /// Hosts a group's header (in the list, or over it for <see cref="IsStickyGroupHeader"/>): a tap expands or collapses the
    /// group (<see cref="AllowGroupExpandCollapse"/>); its root goes to the <c>Expanded</c> / <c>Collapsed</c> visual state;
    /// screen readers read it as a button with that state.
    /// </summary>
    private sealed class GroupHeaderHost(SkUiCollectionView owner) : SkUiContentView
    {
        private bool _expanded = true;

        /// <summary>The group the sticky header shows, or -1 (a header in the list, found by its row).</summary>
        public int StickyGroup { get; set; } = -1;

        public void SetExpanded(bool expanded)
        {
            _expanded = expanded;
            if (Content is VisualElement root)
                VisualStateManager.GoToState(root, expanded ? "Expanded" : "Collapsed");
            InvalidateSemantics();
        }

        protected override void OnContentChanged()
        {
            base.OnContentChanged();
            SetExpanded(_expanded);
        }

        protected override bool HandlesTap => owner.AllowGroupExpandCollapse && Content is not null;

        protected override void OnTapped(SkUiTappedEventArgs args)
        {
            RaiseTapped(args);
            owner.OnGroupHeaderTapped(this);
        }

        protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
        {
            base.OnPopulateSemantics(info);
            if (!owner.AllowGroupExpandCollapse || Content is null)
                return;
            info.Role = SkUiSemanticsRole.Button;
            info.Value = SkUiExpander.ExpandedStateText(_expanded);
        }
    }

    /// <summary>The load-more row's host: in manual mode a tap on it loads (views inside keep their taps).</summary>
    private sealed class LoadMoreHost(SkUiCollectionView owner) : SkUiContentView
    {
        protected override bool HandlesTap => owner.LoadMoreTappable && Content is not LoadMoreDefaultView;

        protected override void OnTapped(SkUiTappedEventArgs args)
        {
            RaiseTapped(args);
            owner.RunLoadMore();
        }
    }

    /// <summary>The default load-more row: a "Load more" button (manual mode), a spinner while loading.</summary>
    private sealed class LoadMoreDefaultView : SkUiContentView
    {
        private readonly SkUiButton _button;
        private readonly SkUiActivityIndicator _spinner = new() { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };

        public LoadMoreDefaultView(SkUiCollectionView owner)
        {
            Padding = new Thickness(8);
            _button = new SkUiButton { Text = LoadMoreText(), HorizontalOptions = LayoutOptions.Center };
            _button.Clicked += (_, _) => owner.RunLoadMore();
        }

        public void Show(bool manual, bool active)
        {
            _spinner.IsRunning = active;
            ISkUiView? content = active ? _spinner : manual ? _button : null;
            if (!ReferenceEquals(Content, content))
                Content = content;
        }
    }

    /// <summary>The content of the scroller along the list's axis: the header, the load-more row at the start, the items (or the empty view), the load-more row at the end, the footer.</summary>
    private sealed class BodyPart : SkUiView
    {
        private readonly SkUiCollectionView _owner;
        private bool _loadMoreAtStart;

        public BodyPart(SkUiCollectionView owner)
        {
            _owner = owner;
            ClipToBounds = false; // items may draw past their slots (shadows, press scale)
            EmptyHost.IsVisible = false;
            AttachChild(owner._items);
            AttachChild(EmptyHost);
        }

        /// <summary>The scrolled header's host, created the first time it shows a header (<see cref="Adopt"/>).</summary>
        public SkUiContentView? HeaderHost;

        /// <summary>The scrolled footer's host, created on demand as <see cref="HeaderHost"/>.</summary>
        public SkUiContentView? FooterHost;

        /// <summary>The load-more row's host, created the first time it shows.</summary>
        public SkUiContentView? LoadMoreHost;

        public SkUiContentView EmptyHost { get; } = new();

        public bool LoadMoreAtStart
        {
            get => _loadMoreAtStart;
            set
            {
                if (_loadMoreAtStart == value)
                    return;
                _loadMoreAtStart = value;
                InvalidateBody();
            }
        }

        /// <summary>Attaches a host created on demand.</summary>
        public T Adopt<T>(T host) where T : SkUiContentView
        {
            AttachChild(host);
            return host;
        }

        public void InvalidateBody() => InvalidateMeasure();

        internal override IEnumerable<ISkUiView> SkiaChildren
        {
            get
            {
                if (HeaderHost is not null)
                    yield return HeaderHost;
                if (LoadMoreHost is not null && _loadMoreAtStart)
                    yield return LoadMoreHost;
                yield return _owner._items;
                yield return EmptyHost;
                if (LoadMoreHost is not null && !_loadMoreAtStart)
                    yield return LoadMoreHost;
                if (FooterHost is not null)
                    yield return FooterHost;
            }
        }

        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            var (along, across) = (0d, 0d);
            foreach (var part in SkiaChildren)
            {
                if (part is not SkUiView { IsVisible: true } view)
                    continue;
                var size = _owner.MeasurePart(view, widthConstraint, heightConstraint);
                along += _owner.Along(size);
                across = Math.Max(across, _owner.Across(size));
            }
            return _owner._horizontal ? new Size(along, across) : new Size(across, along);
        }

        protected override void ArrangeContent(Size size)
        {
            // The empty view fills what the viewport has left besides the other parts.
            var others = 0d;
            foreach (var part in SkiaChildren)
                if (!ReferenceEquals(part, EmptyHost))
                    others += _owner.AlongOf((SkUiView)part);
            var position = 0d;
            foreach (var part in SkiaChildren)
            {
                var view = (SkUiView)part;
                var length = ReferenceEquals(view, EmptyHost) && view.IsVisible
                    ? Math.Max(_owner.AlongOf(view), _owner.Along(size) - others)
                    : _owner.AlongOf(view);
                ((IView)view).Arrange(_owner.Slot(position, length, size));
                position += length;
            }
        }
    }
}
