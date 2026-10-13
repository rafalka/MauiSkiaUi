using System.Collections;
using System.Diagnostics;
using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Whether a popup's choice waits to be accepted (<see cref="SkUiPickerUpdateMode.WhenFinished"/>): then its content shows the OK
/// and Cancel buttons, with these texts.
/// </summary>
internal readonly record struct SkUiPickerPopupChrome(bool RequiresAccept, string AcceptText, string CancelText);

/// <summary>What a popup's content is built for: where it goes, its title, the picker's text style and its buttons.</summary>
internal readonly record struct SkUiPickerPopupContext(SkUiPopupPlacement Placement, string? Title, bool RightToLeft, SkUiPickerRowStyle RowStyle,
    SkUiPickerPopupChrome Chrome);

/// <summary>What every request a picker creates carries besides its content and geometry.</summary>
internal readonly record struct SkUiPopupRequestOptions(string? Title, Color? OverlayColor, bool IsAnimated, SkUiPickerPopupChrome Chrome);

/// <summary>
/// The content of a picker's popup, as the popup session sees it: a drawn surface that measures itself, builds the request that
/// carries it (typed to what it chooses) and follows the picker's selection while it is open.
/// </summary>
internal interface ISkUiPickerPopupContent
{
    /// <summary>The surface handed to the presenter.</summary>
    SkUiContentView Surface { get; }

    /// <summary>Whether the content has keyboard focus.</summary>
    bool IsFocused { get; }

    /// <summary>The size the content wants (at least <paramref name="minimumWidth"/>, at most <paramref name="maximumWidth"/> wide).</summary>
    Size PreferredSize(double minimumWidth, double maximumWidth);

    /// <summary>Creates the request that carries the content (and links it back).</summary>
    SkUiPopupRequest CreateRequest(View anchor, View? platformAnchor, in SkUiPopupLayout layout, in SkUiPopupRequestOptions options);

    /// <summary>The picker's selection changed while the popup is open (<paramref name="index"/>: its selected index, for single lists).</summary>
    void SelectionChanged(int index);

    /// <summary>The popup has gone: lets go of the request and the items.</summary>
    void Release();
}

/// <summary>What the shared picker engine (<see cref="SkUiPickerModel"/>) needs from its control, on either layer.</summary>
internal interface ISkUiPickerOwner : ISkUiTransitionHost
{
    /// <summary>The drawn picker node (its geometry anchors the popup).</summary>
    ISkUiRenderable Node { get; }

    /// <summary>Whether the picker lays out right to left.</summary>
    bool IsRightToLeft { get; }

    /// <summary>The paragraph direction of the picker's text.</summary>
    SkUiTextDirection TextDirection { get; }

    /// <summary>Whether a popup may show now: enabled and visible.</summary>
    bool CanOpen { get; }

    /// <summary>The objects the popup's templated rows bind to (the picker's <c>ItemsSource</c>), or <c>null</c> (the item texts).</summary>
    IList? ItemsSource { get; }

    /// <summary>The user chose <paramref name="index"/> (in the popup, or with an arrow key on the closed field).</summary>
    void Choose(int index);

    /// <summary>Sets the control's <c>IsOpen</c> from the popup's lifetime (raising <c>Opened</c> / <c>Closed</c>).</summary>
    void SetIsOpen(bool open);

    /// <summary>The popup had keyboard focus when it closed: the field takes it back (as a native combo box).</summary>
    void FocusField();

    /// <summary>The field's size depends on something that changed (the text, the font).</summary>
    void InvalidateMeasure();

    /// <summary>The field must be drawn again.</summary>
    void InvalidatePaint();

    /// <summary>What screen readers read changed.</summary>
    void InvalidateSemantics();
}

/// <summary>
/// The engine both pickers share (<see cref="SkUiPicker"/>, <see cref="Core.SkUiCorePicker"/>): the field's text and its
/// measure, the field drawn by the look with its states (pressed, pointer over, focused, open, disabled), keyboard and screen
/// readers, and the popup's lifetime on the app's popup system (<see cref="ISkUiPopupPresenter"/>). The controls hold the
/// properties and the selection rules of their layer and copy the values here.
/// </summary>
internal sealed class SkUiPickerModel
{
    /// <summary>MAUI's picker font size when none is set.</summary>
    internal const double DefaultFontSize = 16;

    private readonly ISkUiPickerOwner _owner;
    private readonly SkUiTextLayout _layout;
    private readonly SkUiTween _focus;
    private readonly SkUiTween _open;
    private SkUiPressAnimator? _press;
    private IReadOnlyList<string> _items = [];
    private PopupSession? _session;
    private bool _reopen;

    public SkUiPickerModel(ISkUiPickerOwner owner)
    {
        _owner = owner;
        _layout = new SkUiTextLayout(owner);
        _focus = new SkUiTween(owner);
        _open = new SkUiTween(owner);
    }

    #region State the controls copy in

    /// <summary>
    /// The item texts, in order (the control keeps the list; set again when it changes). An open popup closes: it shows the
    /// items it opened with, and a choice made there is mapped to the item it showed (<see cref="Ended"/>).
    /// </summary>
    public IReadOnlyList<string> Items
    {
        get => _items;
        set
        {
            _items = value;
            if (_session is { } session)
            {
                session.ItemsChanged = true;
                Close();
            }
            TextChanged();
        }
    }

    /// <summary>The selected index (-1: none).</summary>
    public int SelectedIndex = -1;

    public string? Title;
    public Color? TitleColor;
    public string? Placeholder;
    public Color? PlaceholderColor;
    public double PopupWidth = -1;
    public System.Windows.Input.ICommand? SelectionChangedCommand;
    public object? SelectionChangedCommandParameter;
    public Color? TextColor;
    public string? FontFamily;
    public double FontSize = DefaultFontSize;
    public FontAttributes FontAttributes;
    public bool FontAutoScalingEnabled = true;
    public double CharacterSpacing;
    public TextAlignment HorizontalTextAlignment = TextAlignment.Start;
    public TextAlignment VerticalTextAlignment = TextAlignment.Center;
    public DataTemplate? ItemTemplate;
    public Brush? SelectionBackground;
    public double MaxPopupHeight = -1;
    public SkUiPopupPlacement PopupPlacement = SkUiPopupPlacement.Default;
    public Rect PopupBounds = Rect.Zero;
    public ISkUiPopupPresenter? PopupPresenter;
    public SkUiPickerUpdateMode UpdateMode = SkUiPickerUpdateMode.Immediately;
    public string? AcceptText;
    public string? CancelText;
    public bool IsEnabled = true;
    public bool IsPointerOver;
    public bool HasExplicitMinimumHeight;

    /// <summary>
    /// Whether the field shows <see cref="ValueText"/> instead of the item at <see cref="SelectedIndex"/> (multiple selection,
    /// columns); the control sets the text and calls <see cref="TextChanged"/>.
    /// </summary>
    public bool UsesValueText;

    /// <summary>
    /// The choice changed (any kind: an index, items, a column): runs the control's <c>SelectionChangedCommand</c> with its
    /// parameter when it can execute. The controls call it before their change events.
    /// </summary>
    public void RunSelectionChangedCommand()
    {
        if (SelectionChangedCommand is { } command && command.CanExecute(SelectionChangedCommandParameter))
            command.Execute(SelectionChangedCommandParameter);
    }

    /// <summary>The field's text while <see cref="UsesValueText"/>; <c>null</c>: nothing is chosen (the title shows).</summary>
    public string? ValueText;

    /// <summary>Whether the closed field changes the selection with arrow keys and typing (single lists; not for several choices).</summary>
    public bool ClosedFieldSelects = true;

    /// <summary>
    /// Builds the popup's content; <c>null</c> (default): the list of <see cref="Items"/> to choose one from. Called for each
    /// opening, with the items as they are then.
    /// </summary>
    public Func<SkUiPickerPopupContext, ISkUiPickerPopupContent>? ContentFactory;

    /// <summary>The selected item's text, or <c>null</c> without a selection (the title shows then).</summary>
    public string? SelectedText => UsesValueText ? ValueText
        : SelectedIndex >= 0 && SelectedIndex < _items.Count ? _items[SelectedIndex] ?? string.Empty : null;

    /// <summary>The text the field shows: the selection's, else the title (else nothing).</summary>
    /// <summary>The hint the field shows while nothing is chosen: the placeholder, else the title.</summary>
    private string? PlaceholderText => Placeholder ?? Title;

    private string DisplayText => SelectedText ?? PlaceholderText ?? string.Empty;

    /// <summary>The shown text, the font or the items changed: lay the text out again and measure.</summary>
    public void TextChanged()
    {
        _layout.Invalidate();
        _owner.InvalidateMeasure();
        _owner.InvalidatePaint();
        _owner.InvalidateSemantics();
    }

    /// <summary>A paint-only property changed (a color).</summary>
    public void PaintChanged() => _owner.InvalidatePaint();

    /// <summary>
    /// The selection changed: new text, and an open popup's highlight follows (its request keeps the selection it opened
    /// with).
    /// </summary>
    public void SelectionChanged()
    {
        _session?.SelectionChanged(SelectedIndex);
        TextChanged();
    }

    #endregion

    #region Field

    /// <summary>The text style of the field (and of the popup's rows without a template).</summary>
    internal SkUiTextStyle TextStyle => new(SkUiTypefaces.Resolve(FontFamily, FontAttributes), SkUiFontScaling.ScaleFontSize(FontSize, FontAutoScalingEnabled),
        LineBreakMode.TailTruncation, null, 1, -1, CharacterSpacing, _owner.TextDirection, SkUiTextRendering.Default, FontAttributes);

    /// <summary>Where the text sits in the field: the look's padding, with the chevron's room at the end side.</summary>
    private Thickness TextInset
    {
        get
        {
            var look = SkUiLook.Current;
            var padding = look.PickerFieldPadding;
            var chevron = look.PickerChevronWidth;
            return _owner.IsRightToLeft
                ? new Thickness(padding.Right + chevron, padding.Top, padding.Left, padding.Bottom)
                : new Thickness(padding.Left, padding.Top, padding.Right + chevron, padding.Bottom);
        }
    }

    /// <summary>The chevron's rectangle in a field of <paramref name="width"/> × <paramref name="height"/>.</summary>
    private SKRect ChevronBounds(float width, float height)
    {
        var chevron = (float)SkUiLook.Current.PickerChevronWidth;
        return _owner.IsRightToLeft ? new SKRect(0, 0, chevron, height) : new SKRect(width - chevron, 0, width, height);
    }

    /// <summary>The field's size: the shown text (or the title) with the padding and chevron, at least the look's touch height.</summary>
    public Size Measure(double widthConstraint, double heightConstraint)
    {
        var inset = TextInset;
        var text = DisplayText;
        var size = text.Length == 0
            ? new Size(inset.HorizontalThickness, inset.VerticalThickness + SkUiFontScaling.ScaleFontSize(FontSize, FontAutoScalingEnabled) * 1.2)
            : _layout.Measure(text, TextStyle, inset, widthConstraint, heightConstraint);
        if (!HasExplicitMinimumHeight)
            size = new Size(size.Width, Math.Max(size.Height, SkUiLook.Current.DefaultPickerMinimumHeight));
        return size;
    }

    /// <summary>Draws the field: the look's chrome and chevron, then the text (the selection's, or the title in the title color).</summary>
    public void Draw(SKCanvas canvas, float width, float height)
    {
        var foreground = ToSkColor(TextColor ?? SkUiColors.DefaultForeground);
        SkUiLook.Current.DrawPickerField(canvas, new SkUiPickerFieldPaint(new SKRect(0, 0, width, height), ChevronBounds(width, height),
            ToSkColor(SkUiColors.Accent), ToSkColor(SkUiColors.Muted), foreground, _focus.Value, _open.Value, _press?.Visual ?? SkUiPressVisual.None,
            IsPointerOver, IsEnabled, _owner.IsRightToLeft));
        var selected = SelectedText;
        var text = selected ?? PlaceholderText;
        if (string.IsNullOrEmpty(text))
            return;
        var color = selected is not null ? foreground : ToSkColor(PlaceholderColor ?? TitleColor ?? SkUiColors.Muted);
        if (!IsEnabled)
            color = color.WithAlpha((byte)(color.Alpha * 0.5f));
        var inset = TextInset;
        canvas.Save();
        canvas.ClipRect(new SKRect((float)inset.Left, 0, (float)(width - inset.Right), height));
        var paint = SkUiTextResources.TextPaint;
        paint.Color = color;
        _layout.Draw(canvas, text, TextStyle, inset, width, height, HorizontalTextAlignment, VerticalTextAlignment, paint);
        canvas.Restore();
    }

    /// <summary>The text's native resources go (the picker left its surface).</summary>
    public void ReleaseDrawingResources() => _layout.Release();

    /// <summary>The field is pressed or released (<paramref name="origin"/>: the press point).</summary>
    public void SetPressed(bool pressed, Point origin)
    {
        (_press ??= new SkUiPressAnimator(_owner, ripple: false)).SetPressed(pressed, origin);
        _owner.InvalidatePaint();
    }

    /// <summary>The field gained or lost keyboard focus.</summary>
    public void SetFocused(bool focused) => UpdateHighlight(focused);

    private bool _keyboardFocused;

    private void UpdateHighlight(bool? focused = null)
    {
        if (focused is { } value)
            _keyboardFocused = value;
        var target = _keyboardFocused || IsOpen ? 1f : 0f;
        if (Math.Abs(_focus.Target - target) > 0)
            _focus.AnimateTo(target, SkUiLook.Current.GetTransition(SkUiTransitionKind.FieldFocus));
        var open = IsOpen ? 1f : 0f;
        if (Math.Abs(_open.Target - open) > 0)
            _open.AnimateTo(open, SkUiLook.Current.GetTransition(SkUiTransitionKind.PickerOpen));
    }

    #endregion

    #region Keyboard and screen readers

    /// <summary>
    /// The closed field's keys, as a desktop combo box's: Alt+Down / Alt+Up (and F4-less platforms' Alt+arrows) open; Up / Down
    /// (Left / Right) select the previous / next item, Home / End the first / last, without opening. Space and Enter open
    /// through the activation.
    /// </summary>
    public bool OnKeyDown(in SkUiKeyEvent key)
    {
        if (!IsEnabled || IsOpen)
            return false;
        if ((key.Modifiers & SkUiKeyModifiers.Alt) != 0 && key.Key is SkUiKey.Down or SkUiKey.Up)
        {
            _owner.SetIsOpen(true);
            return true;
        }
        if (key.HasCommandModifier || _items.Count == 0 || !ClosedFieldSelects)
            return false;
        var current = SelectedIndex;
        var rightToLeft = _owner.IsRightToLeft;
        var target = key.Key switch
        {
            SkUiKey.Down => current + 1,
            SkUiKey.Up => current < 0 ? 0 : current - 1,
            SkUiKey.Right => rightToLeft ? Math.Max(0, current - 1) : current + 1,
            SkUiKey.Left => rightToLeft ? current + 1 : Math.Max(0, current - 1),
            SkUiKey.Home => 0,
            SkUiKey.End => _items.Count - 1,
            SkUiKey.Character => SkUiTypeAhead.Next(_items, current, key.Character, ref _typeAhead),
            _ => int.MinValue
        };
        if (target == int.MinValue)
            return false;
        target = Math.Clamp(target, 0, _items.Count - 1);
        if (target != current)
            _owner.Choose(target);
        return true;
    }

    private SkUiTypeAhead _typeAhead;

    /// <summary>A combo box: its title as the name, the selection as the value, whether the list is open, opened by activation.</summary>
    public void PopulateSemantics(SkUiSemanticsInfo info)
    {
        info.Role = SkUiSemanticsRole.ComboBox;
        info.Text = Title ?? Placeholder;
        info.Value = SelectedText;
        info.IsExpanded = IsOpen;
        info.IsEnabled &= IsEnabled;
    }

    #endregion

    #region Popup

    /// <summary>Whether a popup is open (or closing).</summary>
    public bool IsOpen => _session is not null;

    /// <summary>The open popup's request (tests); <c>null</c> while closed.</summary>
    internal SkUiPopupRequest? Request => _session?.Request;

    /// <summary>The control's <c>IsOpen</c> became <c>true</c>: opens the popup (after a closing one has gone).</summary>
    public void Open()
    {
        if (_session is { } running)
        {
            // A popup closing while it opens again: open the next one once this one has gone.
            _reopen = running.IsClosing;
            return;
        }
        if (!_owner.CanOpen || SkUiDiagnostics.GetSurfaceRoot(_owner.Node) is not { } root || AnchorOf(_owner.Node) is not { } anchor)
        {
            Trace.WriteLine("SkiaUi: a picker that is disabled, hidden or not on a drawn surface cannot open.");
            _owner.SetIsOpen(false);
            return;
        }
        var session = new PopupSession(this, anchor, root);
        _session = session;
        UpdateHighlight();
        _owner.InvalidateSemantics();
        try
        {
            session.Start();
        }
        catch (Exception exception)
        {
            // Not shown (a failure building or placing it, e.g. in the item template): closed again, so the picker can open
            // later. Reported as presenter failures are (this runs in the IsOpen change, where a throw would leave it open).
            Trace.WriteLine($"SkiaUi: a picker's popup could not be built: {exception}");
            session.Abort();
            if (ReferenceEquals(_session, session))
            {
                _session = null;
                _reopen = false;
                UpdateHighlight();
                _owner.InvalidateSemantics();
                _owner.SetIsOpen(false);
            }
        }
    }

    /// <summary>The control's <c>IsOpen</c> became <c>false</c>, or the picker can no longer show its popup: the popup closes.</summary>
    public void Close()
    {
        _reopen = false;
        _session?.Close();
    }

    /// <summary>The popup is gone: applies a choice, then <c>IsOpen</c> goes back to <c>false</c> (or the next popup opens).</summary>
    private void Ended(PopupSession session, int choice, bool hadFocus)
    {
        if (!ReferenceEquals(_session, session))
            return;
        _session = null;
        UpdateHighlight();
        _owner.InvalidateSemantics();
        choice = session.ItemsChanged ? session.CurrentIndexOf(choice) : choice;
        if (choice >= 0 && choice < _items.Count)
            _owner.Choose(choice);
        if (hadFocus)
            _owner.FocusField();
        if (_reopen && _owner.CanOpen)
        {
            _reopen = false;
            Open();
            return;
        }
        _reopen = false;
        _owner.SetIsOpen(false);
    }

    /// <summary>The MAUI view a request anchors to: the picker itself, or for a Core node its host (the nearest drawn view).</summary>
    private static View? AnchorOf(ISkUiRenderable node)
    {
        for (ISkUiRenderable? current = node; current is not null; current = current.RenderParent)
            if (current is SkUiView view)
                return view;
        return null;
    }

    /// <summary>
    /// One opening of the popup: builds the drawn list and the request, hands them to the presenter, closes on the control's
    /// triggers, and reports the choice. It keeps the model only until the presenter's task finished.
    /// </summary>
    private sealed class PopupSession(SkUiPickerModel model, View anchor, SkUiView surfaceRoot)
    {
        private readonly List<SkUiScrollController> _scrollers = [];
        private readonly List<ScrollView> _nativeScrollers = [];
        private Window? _window;
        private ISkUiPickerPopupContent? _content;
        private string[] _texts = [];
        private object?[]? _sourceItems;

        public SkUiPopupRequest? Request { get; private set; }

        /// <summary>The picker's items changed while the popup was open (it closes; a choice made in it is mapped).</summary>
        public bool ItemsChanged { get; set; }

        /// <summary>
        /// Where the item the popup showed at <paramref name="choice"/> is now: the same object of the picker's source, else the
        /// same text (at the same index when it is still there); -1 when it has gone.
        /// </summary>
        public int CurrentIndexOf(int choice)
        {
            if (choice < 0 || choice >= _texts.Length)
                return -1;
            if (_sourceItems is { } shown && model._owner.ItemsSource is { } source)
                return source.IndexOf(shown[choice]);
            var text = _texts[choice];
            var items = model._items;
            if (choice < items.Count && items[choice] == text)
                return choice;
            for (var index = 0; index < items.Count; index++)
                if (items[index] == text)
                    return index;
            return -1;
        }

        /// <summary>The picker's selection changed while open: the list's highlight follows.</summary>
        public void SelectionChanged(int index) => _content?.SelectionChanged(index);

        public bool IsClosing => Request?.CloseRequested.IsCancellationRequested ?? false;

        public void Start()
        {
            var look = SkUiLook.Current;
            var placement = model.PopupPlacement == SkUiPopupPlacement.Default ? look.DefaultPopupPlacement : model.PopupPlacement;
            if (placement == SkUiPopupPlacement.Default)
                placement = SkUiPopupPlacement.Anchored;
            var rightToLeft = model._owner.IsRightToLeft;
            var rootBounds = SkUiDiagnostics.GetRootBounds(model._owner.Node) ?? Rect.Zero;
            var origin = SkUiWindowMetrics.SurfaceOrigin(surfaceRoot);
            var anchorBounds = rootBounds.Offset(origin.X, origin.Y);
            var windowSize = SkUiWindowMetrics.WindowSize(anchor.Window);
            var rowStyle = new SkUiPickerRowStyle(model.FontFamily, model.FontSize, model.FontAttributes, model.FontAutoScalingEnabled, model.CharacterSpacing,
                model.TextColor, model.SelectionBackground);
            var presenter = SkUiPopups.Resolve(model.PopupPresenter);
            var requiresAccept = model.UpdateMode == SkUiPickerUpdateMode.WhenFinished;
            // The texts are read at each opening, so a culture switch applies to the next popup.
            var strings = SkUiStrings.Current;
            var chrome = new SkUiPickerPopupChrome(requiresAccept, model.AcceptText ?? strings.AcceptText, model.CancelText ?? strings.CancelText);
            var context = new SkUiPickerPopupContext(placement, placement == SkUiPopupPlacement.Anchored ? null : model.Title, rightToLeft, rowStyle, chrome);
            var content = model.ContentFactory is { } factory ? factory(context) : BuildList(context);
            var maximumHeight = model.MaxPopupHeight >= 0 ? model.MaxPopupHeight : double.PositiveInfinity;
            var windowWidth = windowSize.Width > 0 ? windowSize.Width : double.PositiveInfinity;
            // A width of the app's: the content is measured at it (within the window).
            var preferred = model.PopupWidth >= 0
                ? content.PreferredSize(Math.Min(model.PopupWidth, windowWidth), Math.Min(model.PopupWidth, windowWidth))
                : content.PreferredSize(placement == SkUiPopupPlacement.Anchored ? anchorBounds.Width : look.PopupPreferredWidth, windowWidth);
            var layout = SkUiPopupGeometry.Layout(new SkUiPopupPlacementInput(anchorBounds, rootBounds, windowSize,
                SkUiWindowMetrics.SafeAreaInsets(surfaceRoot), look.PopupMargin, look.PopupAnchorGap, placement, look.PopupHorizontalOptions,
                look.PopupVerticalOptions, preferred, maximumHeight, model.PopupBounds, rightToLeft, model.PopupWidth >= 0));
            _content = content;
            var request = content.CreateRequest(anchor, surfaceRoot, layout,
                new SkUiPopupRequestOptions(model.Title, look.GetPopupOverlayColor(layout.Placement), !SkUiMotion.IsMotionReduced, chrome));
            content.Surface.WidthRequest = layout.Bounds.Width;
            content.Surface.HeightRequest = layout.Bounds.Height;
            Request = request;
            if (layout.Placement == SkUiPopupPlacement.Anchored)
                WatchAnchor();
            _ = RunAsync(request, presenter);
        }

        /// <summary>The default content: the items to choose one from.</summary>
        private ISkUiPickerPopupContent BuildList(in SkUiPickerPopupContext context)
        {
            var items = model._items.ToArray();
            _texts = items;
            if (model._owner.ItemsSource is { } source)
            {
                _sourceItems = new object?[source.Count];
                source.CopyTo(_sourceItems, 0);
            }
            return new SkUiPickerPopup(items, model.SelectedIndex, model.ItemTemplate, model._owner.ItemsSource, context.RowStyle, context.Title, context.RightToLeft,
                context.Chrome);
        }

        private async Task RunAsync(SkUiPopupRequest request, ISkUiPopupPresenter presenter)
        {
            try
            {
                await presenter.ShowAsync(request, request.CloseRequested);
            }
            catch (OperationCanceledException)
            {
                // Closed: the same as returning.
            }
            catch (Exception exception)
            {
                Trace.WriteLine($"SkiaUi: the popup presenter {presenter.GetType().Name} failed: {exception.Message}");
            }
            finally
            {
                StopWatching();
                var choice = request is SkUiListPopupRequest { HasResult: true } list ? list.Result : -1;
                var hadFocus = _content?.IsFocused == true;
                request.MarkCompleted();
                _content?.Release();
                _content = null;
                model.Ended(this, choice, hadFocus);
                _sourceItems = null;
            }
        }

        public void Close() => Request?.Close();

        /// <summary><see cref="Start"/> failed: nothing was handed to a presenter; stops watching and lets go of the content.</summary>
        public void Abort()
        {
            StopWatching();
            Request?.MarkCompleted();
            _content?.Release();
            _content = null;
            _sourceItems = null;
        }

        /// <summary>An anchored popup closes when its anchor scrolls (a drawn or a MAUI scroller around it) or the window resizes.</summary>
        private void WatchAnchor()
        {
            for (ISkUiRenderable? current = model._owner.Node.RenderParent; current is not null; current = current.RenderParent)
                if (current is ISkUiScrollHost host)
                {
                    host.Scroller.Scrolled += Close;
                    _scrollers.Add(host.Scroller);
                }
            for (var element = surfaceRoot.Parent; element is not null; element = element.Parent)
                if (element is ScrollView scrollView)
                {
                    scrollView.Scrolled += OnNativeScrolled;
                    _nativeScrollers.Add(scrollView);
                }
            _window = anchor.Window;
            if (_window is not null)
                _window.SizeChanged += OnWindowSizeChanged;
        }

        private void StopWatching()
        {
            foreach (var scroller in _scrollers)
                scroller.Scrolled -= Close;
            _scrollers.Clear();
            foreach (var scrollView in _nativeScrollers)
                scrollView.Scrolled -= OnNativeScrolled;
            _nativeScrollers.Clear();
            if (_window is not null)
                _window.SizeChanged -= OnWindowSizeChanged;
            _window = null;
        }

        private void OnNativeScrolled(object? sender, ScrolledEventArgs args) => Close();

        private void OnWindowSizeChanged(object? sender, EventArgs args) => Close();
    }

    #endregion

    private static SKColor ToSkColor(Color color) => SkUiToggleDrawing.ToSkColor(color);
}

/// <summary>
/// Type-ahead in a list of texts: typing jumps to the next item starting with what was typed within a second (case- and
/// culture-insensitive); the same letter again cycles through the items starting with it.
/// </summary>
internal struct SkUiTypeAhead
{
    private const long ResetMilliseconds = 1000;
    private string _typed;
    private long _last;

    /// <summary>The item to go to after typing <paramref name="character"/> at <paramref name="current"/>, or <paramref name="current"/> when none matches.</summary>
    public static int Next(IReadOnlyList<string> items, int current, char character, ref SkUiTypeAhead state)
    {
        var now = Environment.TickCount64;
        var typed = now - state._last > ResetMilliseconds || state._typed is null ? string.Empty : state._typed;
        state._last = now;
        typed += character;
        // One letter typed again and again: cycle through the items starting with it.
        var repeated = typed.Length > 1 && typed.All(c => char.ToUpperInvariant(c) == char.ToUpperInvariant(typed[0]));
        var prefix = repeated ? typed[..1] : typed;
        state._typed = typed;
        var start = repeated || typed.Length == 1 ? current + 1 : Math.Max(0, current);
        for (var offset = 0; offset < items.Count; offset++)
        {
            var index = (start + offset) % items.Count;
            if (index >= 0 && (items[index] ?? string.Empty).StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase))
                return index;
        }
        return current;
    }
}
