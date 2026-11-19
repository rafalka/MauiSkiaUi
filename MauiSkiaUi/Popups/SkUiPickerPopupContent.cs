namespace MauiSkiaUi;

/// <summary>
/// What the popup of a picker of your own is built for (<see cref="SkUiPickerBase.CreatePopupContent"/>): where it goes, its title,
/// the picker's text style and buttons. Read-only; one per opening.
/// </summary>
public sealed class SkUiPickerPopupContext
{
    internal SkUiPickerPopupContext(in SkUiPickerPopupSetup setup)
    {
        Placement = setup.Placement;
        Title = setup.Title;
        IsRightToLeft = setup.RightToLeft;
        FontFamily = setup.RowStyle.FontFamily;
        FontSize = setup.RowStyle.FontSize;
        FontAttributes = setup.RowStyle.FontAttributes;
        FontAutoScalingEnabled = setup.RowStyle.FontAutoScalingEnabled;
        CharacterSpacing = setup.RowStyle.CharacterSpacing;
        TextColor = setup.RowStyle.TextColor;
        SelectionBackground = setup.RowStyle.SelectionBackground;
        RequiresAccept = setup.Chrome.RequiresAccept;
        AcceptText = setup.Chrome.AcceptText;
        CancelText = setup.Chrome.CancelText;
        IsClearButtonVisible = setup.Chrome.ShowsClear;
    }

    /// <summary>Where the popup goes (never <see cref="SkUiPopupPlacement.Default"/>: the look's choice is made).</summary>
    public SkUiPopupPlacement Placement { get; }

    /// <summary>The title SkiaUi shows above the content (window popups) or in the bottom pane's toolbar; <c>null</c> when none shows.</summary>
    public string? Title { get; }

    /// <summary>Whether the picker lays out right to left.</summary>
    public bool IsRightToLeft { get; }

    /// <summary>The picker's font family (<c>null</c>: the default font), for text in the content that should match the field.</summary>
    public string? FontFamily { get; }

    /// <summary>The picker's font size (the look's when not set).</summary>
    public double FontSize { get; }

    /// <summary>The picker's font attributes.</summary>
    public FontAttributes FontAttributes { get; }

    /// <summary>Whether the picker's text follows the system text size.</summary>
    public bool FontAutoScalingEnabled { get; }

    /// <summary>The picker's character spacing.</summary>
    public double CharacterSpacing { get; }

    /// <summary>The picker's text color; <c>null</c>: the color scheme's foreground.</summary>
    public Color? TextColor { get; }

    /// <summary>The picker's <c>SelectionBackground</c> (behind a chosen item); <c>null</c>: the look's highlight.</summary>
    public Brush? SelectionBackground { get; }

    /// <summary>
    /// Whether a choice in the content waits for OK (<c>UpdateMode</c> <see cref="SkUiPickerUpdateMode.WhenFinished"/>): SkiaUi shows OK
    /// and Cancel, and the content keeps a draft that <see cref="SkUiPickerPopupContent.OnAccepted"/> applies. <c>false</c>: the content
    /// applies each choice at once.
    /// </summary>
    public bool RequiresAccept { get; }

    /// <summary>The OK button's text.</summary>
    public string AcceptText { get; }

    /// <summary>The Cancel button's text.</summary>
    public string CancelText { get; }

    /// <summary>Whether SkiaUi shows a Clear button (the picker's <c>IsPopupClearButtonVisible</c>): it clears the choice and closes.</summary>
    public bool IsClearButtonVisible { get; }
}

/// <summary>
/// The content of a picker of your own's popup (<see cref="SkUiPickerBase.CreatePopupContent"/>): a drawn body of yours that SkiaUi
/// shows in its popup chrome (the title of window popups, the app's header and footer templates, OK / Cancel, Clear, the bottom
/// pane's toolbar) on the picker's popup presenter. One instance per opening.
/// </summary>
/// <remarks>
/// <para>
/// <b>Choosing.</b> Without <see cref="SkUiPickerPopupContext.RequiresAccept"/>, apply each choice to the picker as the user makes it, and
/// call <see cref="Close"/> when a choice ends the popup (a tap on a swatch). With it, keep a draft and apply it in
/// <see cref="OnAccepted"/> (OK, Enter); Cancel, Escape and a dismissal close without calling it. The Clear button clears the picker
/// (<see cref="SkUiPickerBase.ClearSelection"/>), calls <see cref="OnCleared"/> and closes.
/// </para>
/// <para>
/// <b>Lifetime.</b> <see cref="CreateBody"/> runs once, when the popup is built; <see cref="OnClosed"/> once it has gone, after which the
/// popup's request lets go of the content. Keep a reference to your picker in the content if it applies choices (the content is
/// released with the popup, so it keeps the picker alive no longer than the popup).
/// </para>
/// </remarks>
public abstract class SkUiPickerPopupContent
{
    private SkUiPickerPopupContext? _context;
    private SkUiView? _body;

    /// <summary>What the popup is built for; set before <see cref="CreateBody"/> runs.</summary>
    public SkUiPickerPopupContext Context => _context ?? throw new InvalidOperationException("The popup content has not been shown yet.");

    /// <summary>The body <see cref="CreateBody"/> made, once it ran; <c>null</c> before.</summary>
    public SkUiView? Body => _body;

    /// <summary>Whether the popup is open: from when its request exists until it has gone.</summary>
    public bool IsOpen => Request is not null;

    /// <summary>The popup's request while it is open (its <c>Bounds</c>, <c>Placement</c>, …); <c>null</c> before and after.</summary>
    public SkUiPopupRequest? Request { get; private set; }

    /// <summary>Builds the drawn body of the popup (a layout of SkUi* views): called once, when the popup is built.</summary>
    protected abstract SkUiView CreateBody();

    /// <summary>
    /// How short, in DIPs, an anchored popup (<see cref="SkUiPopupPlacement.Anchored"/>) may make the body to fit beside the picker
    /// before it covers the picker instead (SkiaUi adds its chrome's height). <see cref="double.NaN"/> (default): a few rows, for a
    /// body that scrolls; <see cref="double.PositiveInfinity"/>: its whole preferred height, for a form that cannot usefully shrink
    /// (a calendar with buttons): placed whole on the side with more room when it fits there, otherwise moved over the picker,
    /// inside the window's safe area less the look's <c>PopupMargin</c>, and limited to that area when taller. Set it in the
    /// constructor or <see cref="CreateBody"/>; read when the popup is placed. Other placements ignore it.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public double MinimumHeight
    {
        get;
        set => field = value < 0 ? throw new ArgumentOutOfRangeException(nameof(value), value, "The minimum height cannot be negative.") : value;
    } = double.NaN;

    /// <summary>
    /// The size the body wants, at least <paramref name="minimumWidth"/> and at most <paramref name="maximumWidth"/> wide and
    /// <paramref name="maximumHeight"/> tall (SkiaUi adds its chrome's height). By default: the body measured within them.
    /// </summary>
    public virtual Size PreferredSize(double minimumWidth, double maximumWidth, double maximumHeight)
    {
        if (_body is not { } body)
            return Size.Zero;
        var measured = body.Measure(maximumWidth, maximumHeight);
        return new Size(Math.Clamp(Math.Ceiling(measured.Width), Math.Min(minimumWidth, maximumWidth), maximumWidth),
            Math.Min(Math.Ceiling(measured.Height), maximumHeight));
    }

    /// <summary>OK (or Enter): apply the draft to the picker. Called before the popup closes; also without <see cref="SkUiPickerPopupContext.RequiresAccept"/> (Enter).</summary>
    protected virtual void OnAccepted()
    {
    }

    /// <summary>The Clear button cleared the picker (before the popup closes): reset what the body shows if it stays visible.</summary>
    protected virtual void OnCleared()
    {
    }

    /// <summary>The picker's choice changed while the popup is open (the app set it, or your content applied a choice): the body may follow.</summary>
    protected virtual void OnPickerChanged()
    {
    }

    /// <summary>The popup has gone; the content is not used again.</summary>
    protected virtual void OnClosed()
    {
    }

    /// <summary>Closes the popup (a choice that ends it). With <see cref="SkUiPickerPopupContext.RequiresAccept"/>, a draft not accepted is discarded.</summary>
    protected void Close() => Request?.Cancel();

    /// <summary>As the OK button: <see cref="OnAccepted"/>, then the popup closes (a double tap that confirms).</summary>
    protected void Accept() => Request?.Accept();

    internal SkUiView BuildBody(SkUiPickerPopupContext context)
    {
        _context = context;
        return _body = CreateBody() ?? throw new InvalidOperationException($"{GetType().Name}.CreateBody returned null.");
    }

    internal void Attach(SkUiPopupRequest? request) => Request = request;

    /// <summary>The request that carries this content (<paramref name="surface"/>: SkiaUi's chrome around it); typed content creates its own.</summary>
    internal virtual SkUiPopupRequest CreateRequest(View anchor, View? platformAnchor, View surface, in SkUiPopupLayout layout, in SkUiPopupRequestOptions options,
        Action accept, Action clear) =>
        new SkUiCustomPopupRequest(anchor, platformAnchor, surface, layout, accept, clear)
        {
            Title = options.Title,
            OverlayColor = options.OverlayColor,
            IsAnimated = options.IsAnimated,
            RequiresAccept = options.Chrome.RequiresAccept,
            AcceptText = options.Chrome.AcceptText,
            CancelText = options.Chrome.CancelText
        };

    internal void RaiseAccepted() => OnAccepted();

    internal void RaiseCleared()
    {
        ClearedCore();
        OnCleared();
    }

    internal void RaisePickerChanged()
    {
        PickerChangedCore();
        OnPickerChanged();
    }

    internal void RaiseClosed()
    {
        ClosedCore();
        OnClosed();
    }

    /// <summary>What a typed content keeps track of when the picker was cleared, before <see cref="OnCleared"/> (so overrides need not call the base).</summary>
    internal virtual void ClearedCore()
    {
    }

    /// <summary>What a typed content keeps track of when the picker changed, before <see cref="OnPickerChanged"/>.</summary>
    internal virtual void PickerChangedCore()
    {
    }

    /// <summary>What a typed content lets go of when the popup has gone, before <see cref="OnClosed"/>.</summary>
    internal virtual void ClosedCore()
    {
    }

    /// <summary>A key reached the popup (Escape aside): a built-in content takes it before the popup's own Enter (accept); returns whether it did.</summary>
    internal virtual bool OnKeyDownCore(in SkUiKeyEvent key) => false;
}

/// <summary>
/// The request of a picker of your own's popup (<see cref="SkUiPickerBase.CreatePopupContent"/>): <see cref="SkUiPopupRequest.Content"/>
/// is the content in SkiaUi's chrome. A presenter shows it as any other content; <see cref="SkUiPopupRequest.Accept"/> runs the
/// content's <see cref="SkUiPickerPopupContent.OnAccepted"/> and closes, <see cref="SkUiPopupRequest.Clear"/> clears the picker and closes.
/// </summary>
public sealed class SkUiCustomPopupRequest : SkUiPopupRequest
{
    // Dropped once the request completed: a presenter that keeps it (a popup stack) keeps neither the popup nor the picker.
    private Action? _accept;
    private Action? _clear;

    internal SkUiCustomPopupRequest(View anchor, View? platformAnchor, View content, in SkUiPopupLayout layout, Action accept, Action clear)
        : base(anchor, platformAnchor, content, layout)
    {
        _accept = accept;
        _clear = clear;
    }

    private protected override bool AcceptCore()
    {
        _accept?.Invoke();
        Close();
        return true;
    }

    private protected override bool ClearCore()
    {
        _clear?.Invoke();
        Close();
        return true;
    }

    private protected override void OnCompleted()
    {
        _accept = null;
        _clear = null;
    }
}

/// <summary>
/// The popup surface of a picker of your own: the app's <see cref="SkUiPickerPopupContent"/> body in SkiaUi's chrome (title, the app's
/// header and footer, buttons or the bottom pane's toolbar), with Enter (accept), Escape (cancel) and keyboard focus. Holds the picker
/// weakly; lets go of the content once the popup has gone.
/// </summary>
internal sealed class SkUiCustomPopup : SkUiContentView, ISkUiKeyHandler, ISkUiPickerPopupContent
{
    private readonly WeakReference<ISkUiPickerOwner> _owner;
    private readonly SkUiLabel? _title;
    private readonly SkUiView? _footer;
    private readonly SkUiView? _appHeader;
    private readonly SkUiView? _appFooter;
    private SkUiPickerPopupContent? _content;
    private SkUiPopupRequest? _request;
    private bool _focusedOnce;
    private double _chromeHeight; // at the width PreferredSize gave

    public SkUiCustomPopup(SkUiPickerPopupContent content, SkUiPickerPopupContext context, in SkUiPickerPopupSetup setup, ISkUiPickerOwner owner)
    {
        _content = content;
        _owner = new WeakReference<ISkUiPickerOwner>(owner);
        FlowDirection = setup.RightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        BackgroundColor = SkUiColors.DefaultBackground;
        var body = content.BuildBody(context);
        var chrome = setup.Chrome;
        // A bottom pane's title is in its toolbar.
        _title = setup.Title is null || chrome.IsToolbar ? null : SkUiPopupParts.Title(setup.Title, setup.RowStyle);
        _footer = SkUiPopupParts.Buttons(chrome, setup.RowStyle, setup.Title, () => _request, () => _request?.Clear());
        _appHeader = setup.Extras.CreateHeader();
        _appFooter = setup.Extras.CreateFooter();
        Content = SkUiPopupParts.Compose(_title, _appHeader, body, _appFooter, _footer, chrome.IsToolbar);
    }

    /// <summary>The app's content (tests); <c>null</c> once the popup has gone.</summary>
    internal SkUiPickerPopupContent? AppContent => _content;

    /// <inheritdoc />
    public SkUiContentView Surface => this;

    /// <inheritdoc />
    public SkUiPopupRequest CreateRequest(View anchor, View? platformAnchor, in SkUiPopupLayout layout, in SkUiPopupRequestOptions options)
    {
        var request = _content!.CreateRequest(anchor, platformAnchor, this, layout, options, Accepted, Cleared);
        _request = request;
        _content?.Attach(request);
        return request;
    }

    private void Accepted() => _content?.RaiseAccepted();

    private void Cleared()
    {
        if (_owner.TryGetTarget(out var owner))
            owner.ClearChoice();
        _content?.RaiseCleared();
    }

    /// <inheritdoc />
    public Size PreferredSize(double minimumWidth, double maximumWidth, double maximumHeight)
    {
        var chromeHeight = SkUiPopupParts.ExtraHeight(maximumWidth, _title, _appHeader, _appFooter, _footer);
        var body = _content?.PreferredSize(minimumWidth, maximumWidth, Math.Max(0, maximumHeight - chromeHeight)) ?? Size.Zero;
        var width = Math.Clamp(body.Width, Math.Min(minimumWidth, maximumWidth), maximumWidth);
        _chromeHeight = SkUiPopupParts.ExtraHeight(width, _title, _appHeader, _appFooter, _footer);
        return new Size(width, body.Height + _chromeHeight);
    }

    /// <inheritdoc />
    /// <remarks>The app's <see cref="SkUiPickerPopupContent.MinimumHeight"/> for the body, with the chrome's height.</remarks>
    public double MinimumHeight => _content?.MinimumHeight is { } body && !double.IsNaN(body) ? body + _chromeHeight : double.NaN;

    /// <inheritdoc />
    public void SelectionChanged(int index) => _content?.RaisePickerChanged();

    /// <inheritdoc />
    public void ItemStatesChanged(int column, IReadOnlyList<bool>? states)
    {
    }

    /// <inheritdoc />
    public bool OnKeyDown(in SkUiKeyEvent key)
    {
        if (_request is not { } request || request.CloseRequested.IsCancellationRequested)
            return false;
        switch (key.Key)
        {
            case SkUiKey.Escape:
                request.Cancel();
                return true;
        }
        if (_content?.OnKeyDownCore(key) == true)
            return true;
        if (key.Key != SkUiKey.Enter)
            return false;
        request.Accept();
        return true;
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
        // Shown: Enter and Escape reach the popup (the field's surface loses the keys); a view of the body takes focus when tapped.
        Dispatcher.Dispatch(() =>
        {
            if (_request is not null)
                Focus();
        });
    }

    /// <inheritdoc />
    public void Release()
    {
        var content = _content;
        _content = null;
        _request = null;
        content?.Attach(null);
        content?.RaiseClosed();
    }
}
