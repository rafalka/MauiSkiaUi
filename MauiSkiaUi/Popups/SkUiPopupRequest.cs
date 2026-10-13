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
    /// Whether a choice takes effect only when accepted (the control's <c>UpdateMode</c> is
    /// <see cref="SkUiPickerUpdateMode.WhenFinished"/>): the popup needs an OK and a Cancel button, and a dismissal discards what
    /// was chosen in it. The drawn <see cref="Content"/> shows the buttons (drawn on every platform, so they look the same everywhere);
    /// a presenter that shows its own UI for a typed request instead calls <see cref="Accept"/> and <see cref="Cancel"/>.
    /// </summary>
    public bool RequiresAccept { get; init; }

    /// <summary>The text of the accept button: the control's <c>AcceptText</c>, else <see cref="SkUiStrings.AcceptText"/> ("OK").</summary>
    public string AcceptText { get; init; } = SkUiStrings.Current.AcceptText;

    /// <summary>The text of the cancel button: the control's <c>CancelText</c>, else <see cref="SkUiStrings.CancelText"/> ("Cancel").</summary>
    public string CancelText { get; init; } = SkUiStrings.Current.CancelText;

    /// <summary>
    /// Takes what was chosen in the popup (the highlighted item of a list, the checked items, the wheels' items) as the result and
    /// asks the popup to close: the OK button. Returns <c>false</c> when nothing can be taken (a list with nothing highlighted
    /// only closes) or the popup is already closing.
    /// </summary>
    public bool Accept() => !IsCompleted && !CloseRequested.IsCancellationRequested && AcceptCore();

    /// <summary>
    /// Asks the popup to close without a result: the Cancel button (the same as a dismissal). With
    /// <see cref="RequiresAccept"/>, what was chosen in the popup is discarded; otherwise it has already been applied.
    /// </summary>
    public void Cancel() => Close();

    /// <summary>
    /// Clears the choice: the popup's Clear button. Without <see cref="RequiresAccept"/> it applies at once (a list of one choice
    /// then closes); with it, the draft is cleared and <see cref="Accept"/> applies that. Returns <c>false</c> when the request has
    /// no choice to clear or the popup is closing.
    /// </summary>
    public bool Clear() => !IsCompleted && !CloseRequested.IsCancellationRequested && ClearCore();

    /// <summary>What <see cref="Clear"/> does for this kind of request: by default, nothing.</summary>
    private protected virtual bool ClearCore() => false;

    /// <summary>What <see cref="Accept"/> does for this kind of request: by default, closes.</summary>
    private protected virtual bool AcceptCore()
    {
        Close();
        return false;
    }

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
        OnCompleting(result);
        Result = result;
        HasResult = true;
        Close();
        return true;
    }

    /// <summary>Runs before <paramref name="result"/> is taken as the result (a live selection applies it).</summary>
    private protected virtual void OnCompleting(TResult result) { }

    /// <summary>Whether <paramref name="result"/> is a choice this request offers (a list: an index of its items).</summary>
    private protected virtual bool IsValidResult(TResult result) => true;
}

/// <summary>
/// A request for a list to choose one item from (a picker's): the item texts, the selected one and a title, for presenters
/// that show their own list instead of <see cref="SkUiPopupRequest.Content"/>; the result is the chosen index, or -1 to clear
/// the choice (<see cref="SkUiPopupRequest.Clear"/>).
/// </summary>
public sealed class SkUiListPopupRequest : SkUiPopupRequest<int>
{
    private IReadOnlyList<string> _items;

    internal SkUiListPopupRequest(View anchor, View? platformAnchor, View? content, in SkUiPopupLayout layout, IReadOnlyList<string> items, int selectedIndex,
        IReadOnlyList<bool>? itemsEnabled = null)
        : base(anchor, platformAnchor, content, layout)
    {
        _items = items;
        ItemsEnabled = itemsEnabled;
        SelectedIndex = selectedIndex;
        CurrentIndex = selectedIndex;
    }

    /// <summary>
    /// The item the popup has highlighted now (-1: none): the selection when it opened, then the item the user moved to (a tap
    /// when the choice waits for <see cref="SkUiPopupRequest.Accept"/>, the keyboard). What <see cref="SkUiPopupRequest.Accept"/> chooses.
    /// </summary>
    public int CurrentIndex { get; private set; }

    /// <summary>
    /// Which items can be chosen (<c>null</c>: all; else one flag per item): disabled items show (dimmed) but cannot be chosen
    /// (<see cref="SkUiPopupRequest{TResult}.Complete"/> and <see cref="SetCurrentIndex"/> refuse them). Follows the picker while
    /// the popup is open.
    /// </summary>
    public IReadOnlyList<bool>? ItemsEnabled { get; private set; }

    /// <summary>The picker's item states changed while open.</summary>
    internal void SetItemStates(IReadOnlyList<bool>? states) => ItemsEnabled = states;

    /// <summary>Highlights the item at <paramref name="index"/> (a presenter's own list, before <see cref="SkUiPopupRequest.Accept"/>); <c>false</c> for an index that is not an item, or a disabled one.</summary>
    public bool SetCurrentIndex(int index)
    {
        if (index < -1 || index >= _items.Count || !SkUiItemStates.IsEnabled(ItemsEnabled, index))
            return false;
        CurrentIndex = index;
        if (index >= 0)
            _cleared = false;
        return true;
    }

    private bool _cleared;

    /// <inheritdoc />
    private protected override bool ClearCore()
    {
        CurrentIndex = -1;
        _cleared = true;
        return RequiresAccept || Complete(-1);
    }

    /// <inheritdoc />
    private protected override bool AcceptCore() => CurrentIndex >= 0 ? Complete(CurrentIndex) : _cleared ? Complete(-1) : base.AcceptCore();

    /// <summary>The items' display texts, in order (empty once the request completed).</summary>
    public IReadOnlyList<string> Items => _items;

    /// <summary>The index of the selected item when the popup opened, or -1 for none.</summary>
    public int SelectedIndex { get; }

    /// <inheritdoc />
    private protected override bool IsValidResult(int result) => result >= -1 && result < _items.Count && SkUiItemStates.IsEnabled(ItemsEnabled, result);

    /// <inheritdoc />
    private protected override void OnCompleted() => _items = [];
}

/// <summary>
/// A request whose popup changes a selection as the user goes (a multi-select list, the wheels of a multi-column picker): the
/// drawn content reports each change with <see cref="SetSelection"/>. Without <see cref="SkUiPopupRequest.RequiresAccept"/> the
/// control applies each change at once, so dismissing the popup keeps what was chosen; with it, the changes are a draft that
/// <see cref="SkUiPopupRequest.Accept"/> (or <c>Complete</c> with a selection) applies and a dismissal discards. Presenters that
/// show their own UI do the same.
/// </summary>
public abstract class SkUiSelectionPopupRequest : SkUiPopupRequest<IReadOnlyList<int>>
{
    private int[] _selected;
    private Action<IReadOnlyList<int>>? _apply;

    internal SkUiSelectionPopupRequest(View anchor, View? platformAnchor, View? content, in SkUiPopupLayout layout, IReadOnlyList<int> selected,
        Action<IReadOnlyList<int>> apply)
        : base(anchor, platformAnchor, content, layout)
    {
        _selected = [.. selected];
        _apply = apply;
    }

    /// <summary>
    /// The selection in the popup now: the indices of the chosen items (a list: in ascending order), or one index per column
    /// (-1: none). Follows the changes made here and by the app while the popup is open; with
    /// <see cref="SkUiPopupRequest.RequiresAccept"/>, the draft not yet applied.
    /// </summary>
    public IReadOnlyList<int> SelectedIndices => _selected;

    /// <summary>
    /// Changes the selection: applied to the control at once, or (with <see cref="SkUiPopupRequest.RequiresAccept"/>) kept as the
    /// draft <see cref="SkUiPopupRequest.Accept"/> applies. Returns <c>false</c> (and changes nothing) when
    /// <paramref name="indices"/> is not a selection this request offers, or the popup is closing.
    /// </summary>
    public bool SetSelection(IReadOnlyList<int> indices)
    {
        ArgumentNullException.ThrowIfNull(indices);
        if (IsCompleted || CloseRequested.IsCancellationRequested || _apply is not { } apply || !IsValidSelection(indices))
            return false;
        _selected = [.. indices];
        if (!RequiresAccept)
            apply(_selected);
        return true;
    }

    /// <inheritdoc />
    private protected override bool AcceptCore() => Complete(_selected);

    /// <inheritdoc />
    private protected override bool ClearCore() => SetSelection(EmptySelection);

    /// <summary>The selection with nothing chosen.</summary>
    private protected abstract IReadOnlyList<int> EmptySelection { get; }

    /// <summary>The app changed the selection while the popup is open: <see cref="SelectedIndices"/> follows (the control already has it).</summary>
    internal void Refresh(IReadOnlyList<int> indices) => _selected = [.. indices];

    /// <summary>Whether <paramref name="indices"/> is a selection of this request's items.</summary>
    private protected abstract bool IsValidSelection(IReadOnlyList<int> indices);

    /// <inheritdoc />
    private protected sealed override bool IsValidResult(IReadOnlyList<int> result) => result is not null && IsValidSelection(result);

    /// <inheritdoc />
    private protected sealed override void OnCompleting(IReadOnlyList<int> result)
    {
        // The final selection: applied now (again, unchanged, when each change already was).
        _selected = [.. result];
        _apply?.Invoke(_selected);
    }

    /// <inheritdoc />
    private protected override void OnCompleted()
    {
        _apply = null;
        base.OnCompleted();
    }
}

/// <summary>
/// A request for a list to choose several items from (a picker with <c>SelectionMode</c> <c>Multiple</c>): the item texts and the
/// chosen indices, for presenters that show their own list instead of <see cref="SkUiPopupRequest.Content"/>.
/// </summary>
public sealed class SkUiMultiListPopupRequest : SkUiSelectionPopupRequest
{
    private IReadOnlyList<string> _items;

    internal SkUiMultiListPopupRequest(View anchor, View? platformAnchor, View? content, in SkUiPopupLayout layout, IReadOnlyList<string> items,
        IReadOnlyList<int> selected, Action<IReadOnlyList<int>> apply, IReadOnlyList<bool>? itemsEnabled = null)
        : base(anchor, platformAnchor, content, layout, selected, apply)
    {
        _items = items;
        ItemsEnabled = itemsEnabled;
    }

    /// <summary>
    /// Which items can be chosen (<c>null</c>: all; else one flag per item): a selection may not add or remove a disabled item
    /// (one the app chose stays). Follows the picker while the popup is open.
    /// </summary>
    public IReadOnlyList<bool>? ItemsEnabled { get; private set; }

    /// <summary>The picker's item states changed while open.</summary>
    internal void SetItemStates(IReadOnlyList<bool>? states) => ItemsEnabled = states;

    /// <summary>The items' display texts, in order (empty once the request completed).</summary>
    public IReadOnlyList<string> Items => _items;

    /// <inheritdoc />
    /// <remarks>Disabled items the app chose stay chosen.</remarks>
    private protected override IReadOnlyList<int> EmptySelection => [.. SelectedIndices.Where(index => !SkUiItemStates.IsEnabled(ItemsEnabled, index))];

    /// <summary>Chooses or un-chooses the item at <paramref name="index"/> (see <see cref="SkUiSelectionPopupRequest.SetSelection"/>).</summary>
    public bool SetSelected(int index, bool selected)
    {
        if (index < 0 || index >= _items.Count)
            return false;
        var chosen = SelectedIndices.ToList();
        if (selected == chosen.Contains(index))
            return true;
        if (selected)
        {
            chosen.Add(index);
            chosen.Sort();
        }
        else
        {
            chosen.Remove(index);
        }
        return SetSelection(chosen);
    }

    /// <inheritdoc />
    private protected override bool IsValidSelection(IReadOnlyList<int> indices)
    {
        var previous = -1;
        foreach (var index in indices)
        {
            // Ascending and distinct.
            if (index <= previous || index >= _items.Count)
                return false;
            previous = index;
        }
        if (ItemsEnabled is null)
            return true;
        // Disabled items keep their state: chosen ones stay chosen, others cannot be chosen.
        var current = SelectedIndices;
        for (var index = 0; index < _items.Count; index++)
            if (!SkUiItemStates.IsEnabled(ItemsEnabled, index) && indices.Contains(index) != current.Contains(index))
                return false;
        return true;
    }

    /// <inheritdoc />
    private protected override void OnCompleted()
    {
        _items = [];
        base.OnCompleted();
    }
}

/// <summary>
/// A request for wheels, one per column, to choose one item from each (a multi-column picker): the columns' item texts, whether
/// they loop and the chosen index of each, for presenters that show their own UI instead of <see cref="SkUiPopupRequest.Content"/>.
/// </summary>
public sealed class SkUiColumnsPopupRequest : SkUiSelectionPopupRequest
{
    private IReadOnlyList<IReadOnlyList<string>> _columns;
    private IReadOnlyList<bool> _loops;

    internal SkUiColumnsPopupRequest(View anchor, View? platformAnchor, View? content, in SkUiPopupLayout layout, IReadOnlyList<IReadOnlyList<string>> columns,
        IReadOnlyList<bool> loops, IReadOnlyList<int> selected, Action<IReadOnlyList<int>> apply, IReadOnlyList<string?>? headers = null)
        : base(anchor, platformAnchor, content, layout, selected, apply)
    {
        _columns = columns;
        _loops = loops;
        Headers = headers ?? new string?[columns.Count];
    }

    /// <summary>The columns' headers ("Hour", "Minute"; <c>null</c>: none).</summary>
    public IReadOnlyList<string?> Headers { get; }

    /// <summary>The display texts of each column's items (empty once the request completed).</summary>
    public IReadOnlyList<IReadOnlyList<string>> Columns => _columns;

    /// <summary>Whether each column's items repeat without an end.</summary>
    public IReadOnlyList<bool> Loops => _loops;

    /// <inheritdoc />
    private protected override IReadOnlyList<int> EmptySelection => Enumerable.Repeat(-1, _columns.Count).ToArray();

    /// <inheritdoc />
    /// <remarks>
    /// Wheels always show an item in their middle rows, so they cannot show that nothing is chosen: Clear chooses nothing and
    /// closes, also when the popup waits for OK.
    /// </remarks>
    private protected override bool ClearCore() => Complete(EmptySelection);

    /// <summary>
    /// Which items of each column can be chosen (<c>null</c> for a column: all): disabled items show (dimmed), but a wheel does not
    /// stop on them and a selection cannot choose them. Follows the columns while the popup is open.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<bool>?> ItemsEnabled => _itemsEnabled;

    private IReadOnlyList<bool>?[] _itemsEnabled = [];

    /// <summary>The states the popup opened with, and their changes while open.</summary>
    internal void SetItemStates(int column, IReadOnlyList<bool>? states)
    {
        if (_itemsEnabled.Length != _columns.Count)
            _itemsEnabled = new IReadOnlyList<bool>?[_columns.Count];
        if (column >= 0 && column < _itemsEnabled.Length)
            _itemsEnabled[column] = states;
    }

    /// <summary>Chooses the item at <paramref name="index"/> in the column <paramref name="column"/> (see <see cref="SkUiSelectionPopupRequest.SetSelection"/>).</summary>
    public bool SetSelectedIndex(int column, int index)
    {
        if (column < 0 || column >= _columns.Count)
            return false;
        var chosen = SelectedIndices.ToArray();
        chosen[column] = index;
        return SetSelection(chosen);
    }

    /// <inheritdoc />
    private protected override bool IsValidSelection(IReadOnlyList<int> indices)
    {
        if (indices.Count != _columns.Count)
            return false;
        for (var column = 0; column < indices.Count; column++)
        {
            if (indices[column] < -1 || indices[column] >= _columns[column].Count)
                return false;
            // A disabled item cannot be chosen (one the app chose may stay).
            var states = column < _itemsEnabled.Length ? _itemsEnabled[column] : null;
            if (indices[column] >= 0 && !SkUiItemStates.IsEnabled(states, indices[column]) && indices[column] != SelectedIndices[column])
                return false;
        }
        return true;
    }

    /// <inheritdoc />
    private protected override void OnCompleted()
    {
        _columns = [];
        _loops = [];
        base.OnCompleted();
    }
}
