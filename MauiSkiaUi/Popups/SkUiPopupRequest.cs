namespace MauiSkiaUi;

/// <summary>Where a drawn control's popup goes (<see cref="SkUiPopupRequest.Placement"/>, a picker's <c>PopupPlacement</c>).</summary>
public enum SkUiPopupPlacement
{
    /// <summary>The look's choice for the platform and idiom (<see cref="SkUiLook.DefaultPopupPlacement"/>); never in a request.</summary>
    Default,
    /// <summary>
    /// At the anchor, as a combo box: below it, or above when it fits better; start-aligned (mirrored in right-to-left layouts),
    /// at least as wide as the anchor.
    /// </summary>
    Anchored,
    /// <summary>In the window, aligned by <see cref="SkUiPopupRequest.HorizontalOptions"/> / <see cref="SkUiPopupRequest.VerticalOptions"/>: a centered dialog, a bottom sheet.</summary>
    Window,
    /// <summary>At a rectangle of the window the app gives (a picker's <c>PopupBounds</c>).</summary>
    Absolute
}

/// <summary>
/// A drawn control's request to show a popup, handed to the app's <see cref="ISkUiPopupPresenter"/>: what to show
/// (<see cref="Content"/>, a ready MAUI view), where (<see cref="Bounds"/>, worked out by SkiaUi for the
/// <see cref="Placement"/>, and the anchor's geometry for presenters that place popups themselves) and how it ends
/// (<see cref="CloseRequested"/>). Created by the control for one opening; typed requests (<see cref="SkUiListPopupRequest"/>)
/// describe the content's data for presenters that show their own UI.
/// </summary>
/// <remarks>
/// All rectangles are in window coordinates (device-independent units, the window's top-left at 0, 0) unless named otherwise.
/// The request holds the anchor, its surface, page and window weakly, and once it completed (the presenter's task finished)
/// it lets go of them and of the content (<see cref="Anchor"/> and the others read <c>null</c>): a request kept by a presenter
/// keeps no control alive.
/// </remarks>
public class SkUiPopupRequest
{
    private readonly CancellationTokenSource _close = new();
    // Weak: a request kept by a presenter (a popup stack, a popup still shown after its page went) keeps no control alive.
    private WeakReference<View>? _anchor;
    private WeakReference<View>? _platformAnchor;
    private WeakReference<Window>? _window;
    private WeakReference<Page>? _page;
    private View? _content;

    internal SkUiPopupRequest(View anchor, View? platformAnchor, View? content, in SkUiPopupLayout layout)
    {
        _anchor = new WeakReference<View>(anchor);
        _platformAnchor = platformAnchor is null ? null : new WeakReference<View>(platformAnchor);
        _content = content;
        _window = anchor.Window is { } window ? new WeakReference<Window>(window) : null;
        _page = PageOf(anchor) is { } page ? new WeakReference<Page>(page) : null;
        AnchorBounds = layout.AnchorBounds;
        AnchorBoundsInPlatformAnchor = layout.AnchorBoundsInPlatformAnchor;
        WindowSize = layout.WindowSize;
        SafeAreaInsets = layout.SafeAreaInsets;
        Margin = layout.Margin;
        Placement = layout.Placement;
        HorizontalOptions = layout.HorizontalOptions;
        VerticalOptions = layout.VerticalOptions;
        PreferredSize = layout.PreferredSize;
        MaximumSize = layout.MaximumSize;
        Bounds = layout.Bounds;
        OpensAbove = layout.OpensAbove;
        SpaceAbove = layout.SpaceAbove;
        SpaceBelow = layout.SpaceBelow;
        IsRightToLeft = layout.IsRightToLeft;
    }

    /// <summary>
    /// The MAUI view that opens the popup: the drawn control itself (it has no handler), or for a Core node the
    /// <c>SkUiCoreHost</c> it is drawn in. <c>null</c> once the request completed.
    /// </summary>
    public View? Anchor => Target(_anchor);

    /// <summary>
    /// The nearest ancestor of <see cref="Anchor"/> with a platform view (the surface it is drawn on), for popup systems that
    /// place a popup relative to a view (<c>SfPopup.ShowRelativeToView</c>) with an offset from
    /// <see cref="AnchorBoundsInPlatformAnchor"/>. <c>null</c> once the request completed.
    /// </summary>
    public View? PlatformAnchor => Target(_platformAnchor);

    /// <summary>The anchor's window; <c>null</c> when it is in none, and once the request completed.</summary>
    public Window? Window => Target(_window);

    /// <summary>
    /// The page to show the popup on: the nearest page around the anchor (its <c>Navigation</c> shows modal popups on the
    /// anchor's window). <c>null</c> outside a page, and once the request completed.
    /// </summary>
    public Page? Page => Target(_page);

    /// <summary>
    /// What to show: a ready MAUI view (a drawn surface with the control's popup content, already measured for
    /// <see cref="PreferredSize"/>), hosted like any other view; it fills the size the popup gives it. Not shown by presenters
    /// that show their own UI for a typed request. <c>null</c> once the request completed.
    /// </summary>
    public View? Content => _content;

    /// <summary>A title for popups that show one (a dialog's header, an action sheet's title); <c>null</c> for none.</summary>
    public string? Title { get; init; }

    /// <summary>The anchor's rectangle in the window, through drawn offsets, transforms and scrolling.</summary>
    public Rect AnchorBounds { get; }

    /// <summary>The anchor's rectangle relative to <see cref="PlatformAnchor"/> (the offset for view-relative popups).</summary>
    public Rect AnchorBoundsInPlatformAnchor { get; }

    /// <summary>The window's size (zero when unknown).</summary>
    public Size WindowSize { get; }

    /// <summary>The window's safe area insets (status bar, notch, home indicator), which popups stay clear of.</summary>
    public Thickness SafeAreaInsets { get; }

    /// <summary>The space kept between popups and the safe area's edges (the look's <see cref="SkUiLook.PopupMargin"/>).</summary>
    public Thickness Margin { get; }

    /// <summary>Where the popup goes: <see cref="SkUiPopupPlacement.Anchored"/>, <see cref="SkUiPopupPlacement.Window"/> or <see cref="SkUiPopupPlacement.Absolute"/>.</summary>
    public SkUiPopupPlacement Placement { get; }

    /// <summary>How a <see cref="SkUiPopupPlacement.Window"/> popup is aligned horizontally in the window (<c>Fill</c>: as wide as it allows).</summary>
    public LayoutOptions HorizontalOptions { get; }

    /// <summary>How a <see cref="SkUiPopupPlacement.Window"/> popup is aligned vertically (<c>End</c>: a bottom sheet).</summary>
    public LayoutOptions VerticalOptions { get; }

    /// <summary>The content's own size (measured), before it is limited to <see cref="MaximumSize"/> and the room there is.</summary>
    public Size PreferredSize { get; }

    /// <summary>The largest the popup may be: the window's safe area less <see cref="Margin"/>, and the control's maximum (<c>MaxPopupHeight</c>).</summary>
    public Size MaximumSize { get; }

    /// <summary>
    /// Where SkiaUi puts the popup, in the window: for <see cref="SkUiPopupPlacement.Anchored"/> below the anchor (or above,
    /// <see cref="OpensAbove"/>), start-aligned and at least its width, kept inside the safe area; for
    /// <see cref="SkUiPopupPlacement.Window"/> aligned in the window; for <see cref="SkUiPopupPlacement.Absolute"/> the app's
    /// rectangle. Presenters that position by coordinates or margins use it as it is.
    /// </summary>
    public Rect Bounds { get; }

    /// <summary>Whether an anchored popup opens above its anchor (there was not room enough below).</summary>
    public bool OpensAbove { get; }

    /// <summary>The room above the anchor, to the safe area's top less <see cref="Margin"/> (for presenters that flip a popup themselves).</summary>
    public double SpaceAbove { get; }

    /// <summary>The room below the anchor, to the safe area's bottom less <see cref="Margin"/>.</summary>
    public double SpaceBelow { get; }

    /// <summary>Whether the anchor lays out right to left (anchored popups align to its right edge then).</summary>
    public bool IsRightToLeft { get; }

    /// <summary>Whether the user may close the popup without a choice: a tap outside it, Back, Escape (default <c>true</c>).</summary>
    public bool IsDismissible { get; init; } = true;

    /// <summary>
    /// What a modal popup system dims the page with, or <c>null</c> for nothing (anchored popups, which look like a part of
    /// the control); from the look (<see cref="SkUiLook.GetPopupOverlayColor"/>).
    /// </summary>
    public Color? OverlayColor { get; init; }

    /// <summary>Whether the popup may animate in and out (off when the system asks for reduced motion).</summary>
    public bool IsAnimated { get; init; } = true;

    /// <summary>
    /// Cancelled when the popup must close: after the content completed the request (a choice), or when the control closed it.
    /// The same token is passed to <see cref="ISkUiPopupPresenter.ShowAsync"/>.
    /// </summary>
    public CancellationToken CloseRequested => _close.Token;

    /// <summary>Whether the request has completed: the presenter's task finished (the popup is gone).</summary>
    public bool IsCompleted { get; private set; }

    /// <summary>Whether a result was chosen (<see cref="SkUiPopupRequest{TResult}.Result"/>); <c>false</c> for a dismissal or a close.</summary>
    public bool HasResult { get; private protected set; }

    /// <summary>The control closes the popup (or a result was chosen): the presenter's token is cancelled.</summary>
    internal void Close()
    {
        if (!_close.IsCancellationRequested)
            _close.Cancel();
    }

    /// <summary>The presenter's task finished: nothing more happens, and the request lets go of the views it held.</summary>
    internal void MarkCompleted()
    {
        IsCompleted = true;
        Close();
        _anchor = null;
        _platformAnchor = null;
        _window = null;
        _page = null;
        _content = null;
        OnCompleted();
    }

    /// <summary>Lets go of what a typed request holds (its items) once it completed.</summary>
    private protected virtual void OnCompleted() { }

    private static T? Target<T>(WeakReference<T>? reference) where T : class =>
        reference is not null && reference.TryGetTarget(out var target) ? target : null;

    private static Page? PageOf(Element element)
    {
        for (var current = element.Parent; current is not null; current = current.Parent)
            if (current is Page page)
                return page;
        return null;
    }
}

/// <summary>A popup request whose content (or a presenter's own UI) chooses a <typeparamref name="TResult"/>.</summary>
/// <typeparam name="TResult">What is chosen (an item index for a list).</typeparam>
public class SkUiPopupRequest<TResult> : SkUiPopupRequest
{
    internal SkUiPopupRequest(View anchor, View? platformAnchor, View? content, in SkUiPopupLayout layout)
        : base(anchor, platformAnchor, content, layout)
    {
    }

    /// <summary>The choice, once <see cref="SkUiPopupRequest.HasResult"/>.</summary>
    public TResult? Result { get; private set; }

    /// <summary>
    /// Chooses <paramref name="result"/> and asks the popup to close (<see cref="SkUiPopupRequest.CloseRequested"/>): the drawn
    /// content calls it, and a presenter that shows its own UI calls it with the choice made there. Returns <c>false</c> when
    /// the request already has a result, was closed or has completed (nothing changes then).
    /// </summary>
    public bool Complete(TResult result)
    {
        if (IsCompleted || HasResult || CloseRequested.IsCancellationRequested || !IsValidResult(result))
            return false;
        Result = result;
        HasResult = true;
        Close();
        return true;
    }

    /// <summary>Whether <paramref name="result"/> is a choice this request offers (a list: an index of its items).</summary>
    private protected virtual bool IsValidResult(TResult result) => true;
}

/// <summary>
/// A request for a list to choose one item from (a picker's): the item texts, the selected one and a title, for presenters
/// that show their own list instead of <see cref="SkUiPopupRequest.Content"/>; the result is the chosen index.
/// </summary>
public sealed class SkUiListPopupRequest : SkUiPopupRequest<int>
{
    private IReadOnlyList<string> _items;

    internal SkUiListPopupRequest(View anchor, View? platformAnchor, View? content, in SkUiPopupLayout layout, IReadOnlyList<string> items, int selectedIndex)
        : base(anchor, platformAnchor, content, layout)
    {
        _items = items;
        SelectedIndex = selectedIndex;
    }

    /// <summary>The items' display texts, in order (empty once the request completed).</summary>
    public IReadOnlyList<string> Items => _items;

    /// <summary>The index of the selected item when the popup opened, or -1 for none.</summary>
    public int SelectedIndex { get; }

    /// <inheritdoc />
    private protected override bool IsValidResult(int result) => result >= 0 && result < _items.Count;

    /// <inheritdoc />
    private protected override void OnCompleted() => _items = [];
}
