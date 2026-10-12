using System.Collections;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>How a picker's popup rows draw their text without an item template (the picker's text properties).</summary>
internal readonly record struct SkUiPickerRowStyle(string? FontFamily, double FontSize, FontAttributes FontAttributes, bool FontAutoScalingEnabled,
    double CharacterSpacing, Color? TextColor);

/// <summary>
/// A picker's popup content: a drawn surface of its own with the items as a virtualized <see cref="SkUiCollectionView"/>
/// (plain rows, or the picker's <c>ItemTemplate</c>), the selection highlighted and scrolled into view, an optional title
/// above (window popups). A tap on a row chooses it. With keyboard focus (it takes it when shown): Up / Down, Page Up /
/// Page Down, Home / End move the highlight, typing jumps to the item starting with what was typed, Enter or Space chooses,
/// Escape closes without a choice. Holds the items it was opened with, never the picker.
/// </summary>
internal sealed class SkUiPickerPopup : SkUiContentView, ISkUiKeyHandler
{
    /// <summary>Items whose texts are measured for the popup's width (more would cost too much on a long list).</summary>
    private const int MeasuredItems = 200;

    private readonly SkUiCollectionView _list;
    private readonly SkUiLabel? _title;
    private readonly IReadOnlyList<string> _texts;
    private readonly SkUiPickerRowStyle _style;
    private readonly DataTemplate? _template;
    private Row[] _rows;
    private int _highlighted;
    private SkUiTypeAhead _typeAhead;
    private bool _focusedOnce;

    public SkUiPickerPopup(IReadOnlyList<string> texts, int selected, DataTemplate? template, IList? itemsSource, SkUiPickerRowStyle style, string? title,
        bool rightToLeft)
    {
        _texts = texts;
        _style = style;
        _template = template;
        _highlighted = selected >= 0 && selected < texts.Count ? selected : -1;
        var look = SkUiLook.Current;
        FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        BackgroundColor = SkUiColors.DefaultBackground;
        AutomationProperties.SetIsInAccessibleTree(this, false);
        _rows = new Row[texts.Count];
        for (var index = 0; index < _rows.Length; index++)
            _rows[index] = new Row(index, texts[index], itemsSource is not null && index < itemsSource.Count ? itemsSource[index] : texts[index]);
        _list = new SkUiCollectionView
        {
            SelectionMode = SkUiSelectionMode.Single,
            SelectionBackground = new SolidColorBrush(look.PopupHighlightColor),
            ItemTemplate = new DataTemplate(() => new RowView(template, style)),
            ItemsSource = _rows
        };
        if (template is null)
            _list.ItemExtent = look.PopupItemHeight;
        if (_highlighted >= 0)
            _list.SelectedItem = _rows[_highlighted];
        _list.ItemTapped += OnItemTapped;
        if (title is null)
        {
            Content = _list;
        }
        else
        {
            _title = new SkUiLabel
            {
                Text = title,
                FontAttributes = FontAttributes.Bold,
                FontFamily = style.FontFamily,
                FontSize = style.FontSize,
                FontAutoScalingEnabled = style.FontAutoScalingEnabled,
                TextColor = style.TextColor ?? SkUiColors.DefaultForeground,
                Padding = new Thickness(look.PopupItemPadding.Left, 16, look.PopupItemPadding.Right, 8),
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 2
            };
            SemanticProperties.SetHeadingLevel(_title, SemanticHeadingLevel.Level1);
            var grid = new SkUiGrid { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)] };
            grid.Children.Add(_title);
            grid.Children.Add(_list);
            Grid.SetRow(_list, 1);
            Content = grid;
        }
        if (_highlighted >= 0)
            _ = _list.ScrollToIndex(_highlighted, ScrollToPosition.Center, animated: false);
    }

    /// <summary>The request this content completes (set before it is shown; cleared once it has gone).</summary>
    public SkUiListPopupRequest? Request { get; set; }

    /// <summary>The list (tests).</summary>
    internal SkUiCollectionView List => _list;

    /// <summary>The highlighted item: the selection, then the keyboard's position (tests).</summary>
    internal int HighlightedIndex => _highlighted;

    /// <summary>
    /// The size the content wants: as wide as its widest text (the first items') or templated row (the first one's), at least
    /// <paramref name="minimumWidth"/>, at most <paramref name="maximumWidth"/>, and as tall as all its rows and its title.
    /// </summary>
    public Size PreferredSize(double minimumWidth, double maximumWidth)
    {
        var look = SkUiLook.Current;
        var padding = look.PopupItemPadding;
        var widest = 0d;
        var measured = Math.Min(_texts.Count, MeasuredItems);
        if (measured > 0)
        {
            var layout = new SkUiTextLayout();
            var textStyle = new SkUiTextStyle(SkUiTypefaces.Resolve(_style.FontFamily, _style.FontAttributes),
                SkUiFontScaling.ScaleFontSize(_style.FontSize, _style.FontAutoScalingEnabled), LineBreakMode.NoWrap,
                CharacterSpacing: _style.CharacterSpacing, FontAttributes: _style.FontAttributes);
            for (var index = 0; index < measured; index++)
            {
                widest = Math.Max(widest, layout.Measure(_texts[index] ?? string.Empty, textStyle, default, double.PositiveInfinity).Width);
                layout.Invalidate();
            }
            layout.Release();
        }
        var width = Math.Ceiling(widest + padding.HorizontalThickness);
        double rowHeight = look.PopupItemHeight;
        RowView? probe = null;
        if (_template is not null && _rows.Length > 0)
        {
            // Templated rows: at least as wide as the first one wants (its own padding included) and as tall as it at the
            // popup's width, for every row (the list measures each row once it shows).
            probe = new RowView(_template, _style) { BindingContext = _rows[0] };
            width = Math.Max(width, Math.Ceiling(probe.Measure(maximumWidth, double.PositiveInfinity).Width));
        }
        width = Math.Clamp(width, Math.Min(minimumWidth, maximumWidth), maximumWidth);
        if (probe is not null)
        {
            rowHeight = probe.Measure(width, double.PositiveInfinity).Height;
            probe.BindingContext = null;
        }
        var height = _rows.Length * rowHeight;
        if (_title is not null)
            height += _title.Measure(width, double.PositiveInfinity).Height;
        return new Size(width, height);
    }

    private void OnItemTapped(object? sender, SkUiItemTappedEventArgs args) => Choose(args.Index);

    /// <summary>Completes the request with <paramref name="index"/> (the presenter then closes the popup).</summary>
    private void Choose(int index)
    {
        if (index >= 0 && index < _rows.Length)
            Request?.Complete(index);
    }

    /// <inheritdoc />
    public bool OnKeyDown(in SkUiKeyEvent key)
    {
        if (Request is not { } request || request.CloseRequested.IsCancellationRequested)
            return false;
        var count = _rows.Length;
        switch (key.Key)
        {
            case SkUiKey.Escape:
                request.Close();
                return true;
            case SkUiKey.Enter or SkUiKey.Space when !key.IsRepeat:
                if (_highlighted >= 0)
                    Choose(_highlighted);
                else
                    request.Close();
                return true;
            case SkUiKey.Tab:
                return true; // focus stays in the popup
        }
        if (count == 0 || key.HasCommandModifier)
            return false;
        var page = Math.Max(1, (int)(_list.Height / SkUiLook.Current.PopupItemHeight) - 1);
        var target = key.Key switch
        {
            SkUiKey.Down => _highlighted + 1,
            SkUiKey.Up => _highlighted < 0 ? 0 : _highlighted - 1,
            SkUiKey.PageDown => _highlighted + page,
            SkUiKey.PageUp => _highlighted - page,
            SkUiKey.Home => 0,
            SkUiKey.End => count - 1,
            SkUiKey.Character => SkUiTypeAhead.Next(_texts, _highlighted, key.Character, ref _typeAhead),
            _ => int.MinValue
        };
        if (target == int.MinValue)
            return false;
        Highlight(Math.Clamp(target, 0, count - 1));
        return true;
    }

    /// <summary>Moves the highlight to <paramref name="index"/> and scrolls it into view (no choice yet).</summary>
    internal void Highlight(int index)
    {
        if (index == _highlighted || index < 0 || index >= _rows.Length)
            return;
        _highlighted = index;
        _list.SelectedItem = _rows[index];
        _ = _list.ScrollToIndex(index, ScrollToPosition.MakeVisible, animated: false);
    }

    /// <summary>
    /// The picker's selection changed while the popup is open (the app set it): the highlight follows, so Enter chooses what
    /// the field shows; -1 clears it.
    /// </summary>
    internal void SyncSelection(int index)
    {
        if (index >= 0 && index < _rows.Length)
        {
            Highlight(index);
            return;
        }
        _highlighted = -1;
        _list.SelectedItem = null;
    }

    /// <inheritdoc />
    internal override bool TakesKeyboardFocus => true;

    /// <inheritdoc />
    internal override bool ShowsFocusRing => false;

    /// <inheritdoc />
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler is null || _focusedOnce)
            return;
        _focusedOnce = true;
        // Shown: keys go to the list (the field's surface loses them), and screen readers start at the selection.
        Dispatcher.Dispatch(() =>
        {
            if (Request is null)
                return;
            Focus();
            if (_highlighted >= 0 && _list.GetRealizedView(_highlighted) is SkUiView row)
                row.SetSemanticFocus();
        });
    }

    /// <summary>The popup has gone: lets go of the items and the request.</summary>
    public void Release()
    {
        Request = null;
        _list.ItemTapped -= OnItemTapped;
        _list.ItemsSource = null;
        _rows = [];
    }

    /// <summary>One item of the list: its index, text and the object its template binds to.</summary>
    internal sealed class Row(int index, string text, object? item)
    {
        public int Index { get; } = index;
        public string Text { get; } = text ?? string.Empty;
        public object? Item { get; } = item;
        public override string ToString() => Text;
    }

    /// <summary>
    /// A row: the item's text in the picker's font, or the picker's item template bound to the item (not to the row, so the
    /// app's bindings read its own objects).
    /// </summary>
    private sealed class RowView : SkUiContentView
    {
        private readonly DataTemplate? _template;
        private readonly SkUiLabel? _label;
        private DataTemplate? _shownTemplate;

        public RowView(DataTemplate? template, SkUiPickerRowStyle style)
        {
            _template = template;
            if (template is not null)
                return;
            var look = SkUiLook.Current;
            _label = new SkUiLabel
            {
                FontFamily = style.FontFamily,
                FontSize = style.FontSize,
                FontAttributes = style.FontAttributes,
                FontAutoScalingEnabled = style.FontAutoScalingEnabled,
                CharacterSpacing = style.CharacterSpacing,
                TextColor = style.TextColor ?? SkUiColors.DefaultForeground,
                Padding = look.PopupItemPadding,
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1
            };
            Content = _label;
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            var row = BindingContext as Row;
            if (_label is not null)
            {
                _label.Text = row?.Text ?? string.Empty;
                return;
            }
            // A template selector picks per item: a new view when it picks another template.
            var template = _template is DataTemplateSelector selector && row is not null ? selector.SelectTemplate(row.Item, this) : _template;
            if (!ReferenceEquals(template, _shownTemplate) || Content is null)
            {
                _shownTemplate = template;
                Content = template?.CreateContent() as ISkUiView;
            }
            if (Content is BindableObject content)
                content.BindingContext = row?.Item;
        }
    }
}
