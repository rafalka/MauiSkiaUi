using ISwipeItem = Microsoft.Maui.Controls.ISwipeItem;

namespace MauiSkiaUi;

public partial class SkUiSwipeView
{
    /// <summary>
    /// A side's items, laid out along the content's edge on that side: <see cref="SwipeItem"/>s as drawn buttons, drawn
    /// item views as they are. Created when the side is first revealed; hidden while another side (or none) is shown.
    /// </summary>
    private sealed class ItemsPart : SkUiView
    {
        private readonly SkUiSwipeView _owner;
        private readonly List<(ISwipeItem Item, ISkUiView View)> _entries = [];
        private readonly List<double> _widths = [];

        public ItemsPart(SkUiSwipeView owner, OpenSwipeItem side)
        {
            _owner = owner;
            Side = side;
            IsVisible = false;
        }

        public OpenSwipeItem Side { get; }

        /// <summary>The items shown, or <c>null</c>.</summary>
        public SwipeItems? Items { get; private set; }

        /// <summary>The items' length along the swipe axis (DIPs), after a measure.</summary>
        public double Length { get; private set; }

        /// <summary>How far the content moves when the items are open (DIPs), after a measure.</summary>
        public double OpenDistance { get; private set; }

        /// <summary>The view drawn for an item (tests).</summary>
        public ISkUiView? ViewOf(ISwipeItem item)
        {
            foreach (var entry in _entries)
                if (ReferenceEquals(entry.Item, item))
                    return entry.View;
            return null;
        }

        /// <summary>Shows <paramref name="items"/> (again, after a change): buttons are created anew, item views are hosted here.</summary>
        public void Populate(SwipeItems? items)
        {
            foreach (var (item, view) in _entries)
            {
                DetachChild(view);
                if (view is MenuItemButton button)
                    button.Release();
                else if (Items is { } previous && previous.Contains(item))
                    previous.AddLogicalChild((Element)view); // back to its collection while not shown
            }
            _entries.Clear();
            Items = items;
            if (items is not null)
                foreach (var item in items)
                {
                    ISkUiView view;
                    if (item is SwipeItem menuItem)
                    {
                        view = new MenuItemButton(_owner, Side, menuItem);
                    }
                    else if (item is ISkUiView hosted && hosted.Parent is null or SwipeItems)
                    {
                        // An item view is the collection's logical child until shown: it moves here to be drawn.
                        (hosted.Parent as SwipeItems)?.RemoveLogicalChild((Element)hosted);
                        view = hosted;
                    }
                    else
                    {
                        continue; // shown by another swipe view
                    }
                    _entries.Add((item, view));
                    AttachChild(view);
                }
            InvalidateItems();
        }

        /// <summary>The items changed (size, visibility, mode): measured again with the next layout, buttons aligned for the mode.</summary>
        public void InvalidateItems()
        {
            var execute = Items?.Mode == SwipeMode.Execute;
            foreach (var entry in _entries)
                (entry.View as MenuItemButton)?.AlignForMode(execute);
            InvalidateMeasureOverride();
        }

        internal override IEnumerable<ISkUiView> SkiaChildren
        {
            get
            {
                foreach (var entry in _entries)
                    yield return entry.View;
            }
        }

        /// <summary>
        /// Sizes the items against the content (<paramref name="widthConstraint"/> × <paramref name="heightConstraint"/>) as
        /// MAUI's handlers do. Beside the content, a <see cref="SwipeItem"/> is 100 DIPs wide (in
        /// <see cref="SwipeMode.Execute"/> the items share the content's width) and an item view as wide as it measures; all
        /// are as tall as the content. Above or below it, the items share the content's width and are as tall as the content,
        /// or as the tallest item view. The open distance is the items' length (in <see cref="SwipeMode.Execute"/> with only
        /// <see cref="SwipeItem"/>s, 80 % of the content's), at most the content's length.
        /// </summary>
        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            var horizontal = IsHorizontal(Side);
            var execute = Items?.Mode == SwipeMode.Execute;
            var visible = 0;
            var hasViews = false;
            foreach (var (item, view) in _entries)
                if (IsItemVisible(item))
                {
                    visible++;
                    hasViews |= view is not MenuItemButton;
                }
            _widths.Clear();
            if (visible == 0)
            {
                Length = OpenDistance = 0;
                return Size.Zero;
            }
            if (horizontal)
            {
                var length = 0d;
                foreach (var (item, view) in _entries)
                {
                    if (!IsItemVisible(item))
                        continue;
                    var width = view is MenuItemButton
                        ? execute ? widthConstraint / visible : SwipeItemWidth
                        : ((IView)view).Measure(double.PositiveInfinity, heightConstraint).Width;
                    if (!(width > 0))
                        width = SwipeItemWidth;
                    _widths.Add(width);
                    length += width;
                }
                Length = length;
            }
            else
            {
                var height = 0d;
                if (hasViews)
                    foreach (var (item, view) in _entries)
                        if (IsItemVisible(item) && view is not MenuItemButton)
                            height = Math.Max(height, ((IView)view).Measure(widthConstraint / visible, double.PositiveInfinity).Height);
                Length = height > 0 ? height : heightConstraint;
            }
            var axis = horizontal ? widthConstraint : heightConstraint;
            OpenDistance = Math.Max(0, Math.Min(execute && !hasViews ? ExecuteOpenFraction * axis : Length, axis));
            return horizontal ? new Size(Length, heightConstraint) : new Size(widthConstraint, Length);
        }

        /// <summary>Lays the items out from the outer edge inwards, as MAUI's handlers: right items start at the right edge.</summary>
        protected override void ArrangeContent(Size size)
        {
            var horizontal = IsHorizontal(Side);
            var fromRight = Side == OpenSwipeItem.RightItems;
            var visible = horizontal ? _widths.Count : _entries.Count(entry => IsItemVisible(entry.Item));
            var position = 0d;
            var index = 0;
            foreach (var (item, view) in _entries)
            {
                if (!IsItemVisible(item))
                    continue;
                var width = horizontal ? (index < _widths.Count ? _widths[index] : 0) : size.Width / visible;
                index++;
                ((IView)view).Measure(width, size.Height);
                ((IView)view).Arrange(new Rect(fromRight ? size.Width - position - width : position, 0, width, size.Height));
                position += width;
            }
        }
    }

    /// <summary>
    /// A <see cref="SwipeItem"/> drawn as MAUI's handlers show it: its background color, its icon above its text, the text
    /// white or black by the background's luminosity. It follows the item's changes without the item keeping it alive (an
    /// item may be shared through resources). In <see cref="SwipeMode.Execute"/> the icon and text sit at the item's inner
    /// edge (next to the content) instead of its center: the item is as wide as the content, and is revealed from that edge.
    /// </summary>
    private sealed class MenuItemButton : SkUiButton
    {
        /// <summary>Space between an execute item's icon and text and the content's edge.</summary>
        private const double InnerEdgeInset = 20;

        private readonly SkUiSwipeView _owner;
        private readonly OpenSwipeItem _side;
        private readonly SwipeItem _item;
        private readonly SkUiWeakListener<MenuItemButton> _listener;
        private bool _execute;

        public MenuItemButton(SkUiSwipeView owner, OpenSwipeItem side, SwipeItem item)
        {
            _owner = owner;
            _side = side;
            _item = item;
            CornerRadius = 0;
            AlignForMode(execute: false);
            MinimumHeightRequest = 0;
            LineBreakMode = LineBreakMode.TailTruncation;
            ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Top, 4);
            Clicked += (_, _) => _owner.InvokeItem(_item);
            _listener = new SkUiWeakListener<MenuItemButton>(this, static (button, change) => button.Update(notify: true));
            _listener.Listen(item);
            Update(notify: false);
        }

        /// <summary>Stops following the item (no longer shown).</summary>
        public void Release() => _listener.Listen(null);

        /// <summary>
        /// Centers the icon and text (<see cref="SwipeMode.Reveal"/>: the item shows whole once open), or puts them at the
        /// inner edge (<see cref="SwipeMode.Execute"/>), on the physical side also in right-to-left layouts.
        /// </summary>
        public void AlignForMode(bool execute)
        {
            _execute = execute;
            var horizontal = TextAlignment.Center;
            var vertical = TextAlignment.Center;
            var padding = new Thickness(4);
            if (execute)
                switch (_side)
                {
                    case OpenSwipeItem.LeftItems:
                        horizontal = IsRightToLeft ? TextAlignment.Start : TextAlignment.End; // the right edge
                        padding = new Thickness(4, 4, InnerEdgeInset, 4);
                        break;
                    case OpenSwipeItem.RightItems:
                        horizontal = IsRightToLeft ? TextAlignment.End : TextAlignment.Start; // the left edge
                        padding = new Thickness(InnerEdgeInset, 4, 4, 4);
                        break;
                    case OpenSwipeItem.TopItems:
                        vertical = TextAlignment.End;
                        padding = new Thickness(4, 4, 4, InnerEdgeInset);
                        break;
                    default:
                        vertical = TextAlignment.Start;
                        padding = new Thickness(4, InnerEdgeInset, 4, 4);
                        break;
                }
            HorizontalTextAlignment = horizontal;
            VerticalTextAlignment = vertical;
            Padding = padding;
        }

        internal override void OnEffectiveFlowDirectionChanged()
        {
            base.OnEffectiveFlowDirectionChanged();
            AlignForMode(_execute); // start and end swap sides
        }

        private void Update(bool notify)
        {
            Text = _item.Text;
            ImageSource = _item.IconImageSource;
            var background = _item.BackgroundColor;
            FillColor = background ?? Colors.Transparent;
            TextColor = background is null ? SkUiColors.DefaultForeground : ContrastingTextColor(background);
            IsEnabled = _item.IsEnabled;
            AutomationId = _item.AutomationId;
            if (IsVisible == _item.IsVisible)
                return;
            IsVisible = _item.IsVisible;
            if (notify)
                _owner.OnSideItemsChanged(_side);
        }
    }

    /// <summary>White on dark backgrounds, black on light ones (MAUI's swipe item text color).</summary>
    internal static Color ContrastingTextColor(Color background) =>
        0.2126f * background.Red + 0.7152f * background.Green + 0.0722f * background.Blue < 0.75f ? Colors.White : Colors.Black;
}
