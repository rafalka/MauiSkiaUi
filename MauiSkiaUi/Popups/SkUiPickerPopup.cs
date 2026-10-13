using System.Collections;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// How a picker's popup rows draw their text without an item template (the picker's text properties), and what is behind the
/// selected rows (<c>null</c>: the look's <see cref="SkUiLook.PopupHighlightColor"/>).
/// </summary>
internal readonly record struct SkUiPickerRowStyle(string? FontFamily, double FontSize, FontAttributes FontAttributes, bool FontAutoScalingEnabled,
    double CharacterSpacing, Color? TextColor, Brush? SelectionBackground = null);

/// <summary>
/// A picker's popup content: a drawn surface of its own with the items as a virtualized <see cref="SkUiCollectionView"/>
/// (plain rows, or the picker's <c>ItemTemplate</c>), the selection highlighted and scrolled into view, an optional title
/// above (window popups). A tap on a row chooses it. With keyboard focus (it takes it when shown): Up / Down, Page Up /
/// Page Down, Home / End move the highlight, typing jumps to the item starting with what was typed, Enter or Space chooses,
/// Escape closes without a choice. Holds the items it was opened with, never the picker.
/// </summary>
/// <remarks>
/// <para>
/// <b>Several choices</b> (a picker with <c>SelectionMode</c> <c>Multiple</c>): the list selects several rows (a check box in each),
/// every change goes to the request (<see cref="SkUiSelectionPopupRequest.SetSelection"/>), and the popup stays open. The
/// keyboard moves a cursor: Space toggles the row at it, Enter accepts, Escape cancels.
/// </para>
/// <para>
/// <b>Accepted choices</b> (<c>UpdateMode</c> <see cref="SkUiPickerUpdateMode.WhenFinished"/>): a tap only highlights (one choice)
/// or toggles a draft (several), and Cancel / OK buttons below the list cancel or accept.
/// </para>
/// </remarks>
internal sealed class SkUiPickerPopup : SkUiContentView, ISkUiKeyHandler, ISkUiPickerPopupContent
{
    /// <summary>Items whose texts are measured for the popup's width (more would cost too much on a long list).</summary>
    private const int MeasuredItems = 200;

    private readonly SkUiCollectionView _list;
    private readonly SkUiLabel? _title;
    private readonly SkUiView? _footer;
    private readonly SkUiPickerPopupChrome _chrome;
    private readonly IReadOnlyList<string> _texts;
    private readonly SkUiPickerRowStyle _style;
    private readonly DataTemplate? _template;
    private Row[] _rows;
    private readonly bool _multiple;
    private readonly bool _closesOnToggle;
    private readonly Action<IReadOnlyList<int>>? _apply;
    private readonly Func<IReadOnlyList<int>>? _currentSelection;
    private readonly int _initialSelected;
    private readonly int[] _initialChecked = [];
    private int _highlighted;
    private SkUiTypeAhead _typeAhead;
    private bool _focusedOnce;
    private bool _applying;

    /// <summary>Check box (24) and its gap (12) in rows of a list with several choices.</summary>
    private const double CheckColumnWidth = 36;

    /// <summary>
    /// Creates the content. <paramref name="apply"/> (with <paramref name="currentSelection"/>, the picker's chosen indices now)
    /// makes it a list of several choices starting with <paramref name="checkedIndices"/>; <paramref name="selected"/> is for single lists.
    /// <paramref name="chrome"/>: whether the choice waits to be accepted, and the buttons.
    /// </summary>
    public SkUiPickerPopup(IReadOnlyList<string> texts, int selected, DataTemplate? template, IList? itemsSource, SkUiPickerRowStyle style, string? title,
        bool rightToLeft, SkUiPickerPopupChrome chrome, Action<IReadOnlyList<int>>? apply = null, Func<IReadOnlyList<int>>? currentSelection = null,
        IReadOnlyList<int>? checkedIndices = null, bool closesOnToggle = false)
    {
        _closesOnToggle = closesOnToggle;
        _texts = texts;
        _chrome = chrome;
        _style = style;
        _template = template;
        _multiple = apply is not null;
        _apply = apply;
        _currentSelection = currentSelection;
        _initialSelected = selected;
        if (_multiple)
            _initialChecked = [.. checkedIndices ?? []];
        _highlighted = !_multiple && selected >= 0 && selected < texts.Count ? selected : -1;
        var look = SkUiLook.Current;
        FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        BackgroundColor = SkUiColors.DefaultBackground;
        AutomationProperties.SetIsInAccessibleTree(this, false);
        _rows = new Row[texts.Count];
        for (var index = 0; index < _rows.Length; index++)
            _rows[index] = new Row(index, texts[index], itemsSource is not null && index < itemsSource.Count ? itemsSource[index] : texts[index]);
        _list = new SkUiCollectionView
        {
            SelectionBackground = style.SelectionBackground ?? new SolidColorBrush(look.PopupHighlightColor),
            ItemTemplate = new DataTemplate(() => new RowView(template, style, _multiple)),
            ItemsSource = _rows
        };
        if (_multiple)
        {
            _list.SelectionMode = SkUiSelectionMode.Multiple;
            foreach (var index in _initialChecked)
                if (index >= 0 && index < _rows.Length)
                {
                    _rows[index].IsChecked = true;
                    _list.SelectedItems.Add(_rows[index]);
                }
            _list.SelectionChanged += OnListSelectionChanged;
        }
        else
        {
            _list.SelectionMode = SkUiSelectionMode.Single;
        }
        if (template is null)
            _list.ItemExtent = look.PopupItemHeight;
        if (_highlighted >= 0)
            _list.SelectedItem = _rows[_highlighted];
        _list.ItemTapped += OnItemTapped;
        _title = title is null ? null : SkUiPopupParts.Title(title, style);
        _footer = chrome.HasButtons ? SkUiPopupParts.Buttons(chrome, style, () => Request, ClearChoice) : null;
        Content = SkUiPopupParts.Compose(_title, _list, _footer);
        var first = _multiple ? (_initialChecked.Length > 0 ? _initialChecked[0] : -1) : _highlighted;
        if (first >= 0 && first < _rows.Length)
            _ = _list.ScrollToIndex(first, ScrollToPosition.Center, animated: false);
    }

    /// <summary>The request this content completes (set before it is shown; cleared once it has gone).</summary>
    public SkUiPopupRequest? Request { get; private set; }

    /// <inheritdoc />
    public SkUiContentView Surface => this;

    /// <inheritdoc />
    public SkUiPopupRequest CreateRequest(View anchor, View? platformAnchor, in SkUiPopupLayout layout, in SkUiPopupRequestOptions options)
    {
        var chrome = options.Chrome;
        SkUiPopupRequest request = _multiple
            ? new SkUiMultiListPopupRequest(anchor, platformAnchor, this, layout, _texts, _initialChecked, indices => _apply!(indices))
            {
                Title = options.Title, OverlayColor = options.OverlayColor, IsAnimated = options.IsAnimated,
                RequiresAccept = chrome.RequiresAccept, AcceptText = chrome.AcceptText, CancelText = chrome.CancelText
            }
            : new SkUiListPopupRequest(anchor, platformAnchor, this, layout, _texts, _initialSelected)
            {
                Title = options.Title, OverlayColor = options.OverlayColor, IsAnimated = options.IsAnimated,
                RequiresAccept = chrome.RequiresAccept, AcceptText = chrome.AcceptText, CancelText = chrome.CancelText
            };
        Request = request;
        return request;
    }

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
        var width = Math.Ceiling(widest + padding.HorizontalThickness + (_multiple ? CheckColumnWidth : 0));
        double rowHeight = look.PopupItemHeight;
        RowView? probe = null;
        if (_template is not null && _rows.Length > 0)
        {
            // Templated rows: at least as wide as the first one wants (its own padding included) and as tall as it at the
            // popup's width, for every row (the list measures each row once it shows).
            probe = new RowView(_template, _style, _multiple) { BindingContext = _rows[0] };
            width = Math.Max(width, Math.Ceiling(probe.Measure(maximumWidth, double.PositiveInfinity).Width));
        }
        width = Math.Clamp(width, Math.Min(minimumWidth, maximumWidth), maximumWidth);
        if (probe is not null)
        {
            rowHeight = probe.Measure(width, double.PositiveInfinity).Height;
            probe.BindingContext = null;
        }
        var height = _rows.Length * rowHeight + SkUiPopupParts.ExtraHeight(_title, _footer, width);
        return new Size(width, height);
    }

    private void OnItemTapped(object? sender, SkUiItemTappedEventArgs args)
    {
        if (_multiple)
            _highlighted = args.Index; // the list toggled the row; the keyboard goes on from it
        else if (_chrome.RequiresAccept)
            Highlight(args.Index); // the list selected it; OK (or Enter) chooses it
        else
            Choose(args.Index);
    }

    /// <summary>The list changed its chosen rows (a tap, or <see cref="ToggleRow"/>): the picker takes them at once.</summary>
    private void OnListSelectionChanged(object? sender, SkUiSelectionChangedEventArgs args)
    {
        if (_applying || Request is not SkUiSelectionPopupRequest request)
            return;
        var chosen = new List<int>();
        foreach (var item in _list.SelectedItems)
            if (item is Row row)
                chosen.Add(row.Index);
        chosen.Sort();
        ShowChecks(chosen);
        _applying = true;
        try
        {
            request.SetSelection(chosen);
        }
        finally
        {
            _applying = false;
        }
        // A combo box that adds one item at a time (each change already applied).
        if (_closesOnToggle && !request.RequiresAccept)
            request.Close();
    }

    /// <summary>Shows exactly <paramref name="chosen"/> (ascending) as checked.</summary>
    private void ShowChecks(IReadOnlyList<int> chosen)
    {
        var next = 0;
        for (var index = 0; index < _rows.Length; index++)
        {
            while (next < chosen.Count && chosen[next] < index)
                next++;
            _rows[index].IsChecked = next < chosen.Count && chosen[next] == index;
        }
    }

    private void ToggleRow(int index)
    {
        if (index < 0 || index >= _rows.Length)
            return;
        var row = _rows[index];
        if (!_list.SelectedItems.Remove(row))
            _list.SelectedItems.Add(row);
    }

    /// <summary>Completes the request with <paramref name="index"/> (the presenter then closes the popup).</summary>
    private void Choose(int index)
    {
        if (index >= 0 && index < _rows.Length)
            (Request as SkUiListPopupRequest)?.Complete(index);
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
                request.Cancel();
                return true;
            case SkUiKey.Enter when _multiple:
                request.Accept();
                return true;
            case SkUiKey.Space when _multiple && !key.IsRepeat:
                ToggleRow(_highlighted);
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

    /// <summary>The Clear button: no row chosen (several choices: through the list, so the request follows), no highlight.</summary>
    internal void ClearChoice()
    {
        if (Request is not { } request || request.CloseRequested.IsCancellationRequested)
            return;
        if (_multiple)
        {
            _list.SelectedItems.Clear();
            return;
        }
        _highlighted = -1;
        _list.SelectedItem = null;
        request.Clear();
    }

    /// <summary>The row at the keyboard's cursor (several choices) shows it.</summary>
    private void ShowCursor(int index)
    {
        for (var row = 0; row < _rows.Length; row++)
            _rows[row].IsCursor = row == index;
    }

    /// <summary>Moves the highlight to <paramref name="index"/> and scrolls it into view (no choice yet).</summary>
    internal void Highlight(int index)
    {
        if (index < 0 || index >= _rows.Length)
            return;
        (Request as SkUiListPopupRequest)?.SetCurrentIndex(index);
        if (index == _highlighted)
            return;
        _highlighted = index;
        if (_multiple)
            ShowCursor(index);
        else
            _list.SelectedItem = _rows[index];
        _ = _list.ScrollToIndex(index, ScrollToPosition.MakeVisible, animated: false);
    }

    /// <summary>
    /// The picker's selection changed while the popup is open (the app set it): the highlight follows, so Enter chooses what
    /// the field shows; -1 clears it.
    /// </summary>
    public void SelectionChanged(int index)
    {
        if (_multiple)
        {
            SyncChecks();
            return;
        }
        if (index >= 0 && index < _rows.Length)
        {
            Highlight(index);
            return;
        }
        _highlighted = -1;
        _list.SelectedItem = null;
        (Request as SkUiListPopupRequest)?.SetCurrentIndex(-1);
    }

    /// <summary>The picker's chosen rows changed while open (the app set them): the checks follow.</summary>
    private void SyncChecks()
    {
        if (_applying || _currentSelection is null)
            return;
        var chosen = _currentSelection();
        _applying = true;
        try
        {
            ShowChecks(chosen);
            _list.SelectedItems.Clear();
            foreach (var index in chosen)
                if (index >= 0 && index < _rows.Length)
                    _list.SelectedItems.Add(_rows[index]);
            (Request as SkUiSelectionPopupRequest)?.Refresh(chosen);
        }
        finally
        {
            _applying = false;
        }
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
            var focusRow = _multiple ? (_initialChecked.Length > 0 ? _initialChecked[0] : -1) : _highlighted;
            if (focusRow >= 0 && _list.GetRealizedView(focusRow) is SkUiView row)
                row.SetSemanticFocus();
        });
    }

    /// <summary>The popup has gone: lets go of the items and the request.</summary>
    public void Release()
    {
        Request = null;
        _list.ItemTapped -= OnItemTapped;
        _list.SelectionChanged -= OnListSelectionChanged;
        _list.ItemsSource = null;
        _rows = [];
    }

    /// <summary>One item of the list: its index, text and the object its template binds to; whether it is checked or at the keyboard's cursor.</summary>
    internal sealed class Row(int index, string text, object? item)
    {
        private bool _isChecked;
        private bool _isCursor;

        public int Index { get; } = index;
        public string Text { get; } = text ?? string.Empty;
        public object? Item { get; } = item;

        /// <summary>Raised when <see cref="IsChecked"/> or <see cref="IsCursor"/> changed (the row's view shows it).</summary>
        public event EventHandler? StateChanged;

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value)
                    return;
                _isChecked = value;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool IsCursor
        {
            get => _isCursor;
            set
            {
                if (_isCursor == value)
                    return;
                _isCursor = value;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public override string ToString() => Text;
    }

    /// <summary>
    /// A row: the item's text in the picker's font, or the picker's item template bound to the item (not to the row, so the
    /// app's bindings read its own objects); with several choices a check box before it. The list puts the row in the
    /// <c>Selected</c> visual state while it is selected (the highlight, or a chosen row); the row passes it on to the template's
    /// root, so the app's <c>VisualStateGroups</c> style selected rows as in a collection view.
    /// </summary>
    private sealed class RowView : SkUiContentView
    {
        private readonly DataTemplate? _template;
        private readonly SkUiLabel? _label;
        private readonly SkUiContentView? _slot;
        private readonly SkUiCheckBox? _check;
        private Row? _row;
        private DataTemplate? _shownTemplate;

        public RowView(DataTemplate? template, SkUiPickerRowStyle style, bool multiple)
        {
            _template = template;
            var look = SkUiLook.Current;
            if (template is null)
            {
                _label = new SkUiLabel
                {
                    FontFamily = style.FontFamily,
                    FontSize = style.FontSize,
                    FontAttributes = style.FontAttributes,
                    FontAutoScalingEnabled = style.FontAutoScalingEnabled,
                    CharacterSpacing = style.CharacterSpacing,
                    TextColor = style.TextColor ?? SkUiColors.DefaultForeground,
                    Padding = multiple ? new Thickness(0, look.PopupItemPadding.Top, look.PopupItemPadding.Right, look.PopupItemPadding.Bottom) : look.PopupItemPadding,
                    VerticalTextAlignment = TextAlignment.Center,
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 1
                };
            }
            if (!multiple)
            {
                if (_label is not null)
                    Content = _label;
                return;
            }
            // Several choices: a check box (it takes no taps: the row does), then the row's content.
            _check = new SkUiCheckBox { InputTransparent = true, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(look.PopupItemPadding.Left, 0, 12, 0) };
            AutomationProperties.SetIsInAccessibleTree(_check, false);
            _slot = new SkUiContentView { Content = _label };
            var grid = new SkUiGrid { ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)] };
            grid.Children.Add(_check);
            grid.Children.Add(_slot);
            Grid.SetColumn(_slot, 1);
            Content = grid;
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (_row is not null)
                _row.StateChanged -= OnRowStateChanged;
            _row = BindingContext as Row;
            if (_row is not null && _check is not null)
                _row.StateChanged += OnRowStateChanged;
            ShowState();
            var row = _row;
            if (_label is not null)
            {
                _label.Text = row?.Text ?? string.Empty;
                return;
            }
            // A template selector picks per item: a new view when it picks another template.
            var template = _template is DataTemplateSelector selector && row is not null ? selector.SelectTemplate(row.Item, this) : _template;
            var host = _slot ?? this;
            if (!ReferenceEquals(template, _shownTemplate) || host.Content is null)
            {
                _shownTemplate = template;
                host.Content = template?.CreateContent() as ISkUiView;
            }
            if (host.Content is BindableObject content)
                content.BindingContext = row?.Item;
            ForwardSelected();
        }

        /// <inheritdoc />
        protected override void ChangeVisualState()
        {
            base.ChangeVisualState();
            ForwardSelected();
        }

        /// <summary>The template's root follows this row's <c>Selected</c> state.</summary>
        private void ForwardSelected()
        {
            if (_template is not null && (_slot ?? this).Content is SkUiView root)
                root.IsSelectedItem = IsSelectedItem;
        }

        private void OnRowStateChanged(object? sender, EventArgs args) => ShowState();

        private void ShowState()
        {
            if (_check is null)
                return;
            _check.IsChecked = _row?.IsChecked == true;
            BackgroundColor = _row?.IsCursor == true ? SkUiColors.DefaultForeground.WithAlpha(0.08f) : Colors.Transparent;
        }
    }
}
