using System.Collections.Specialized;
using System.ComponentModel;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>
/// A drawn sheet inside the surface: a panel that slides in from an edge (<see cref="Edge"/>) over the page and rests on
/// <see cref="Detents"/>, or a centered card (<see cref="Presentation"/>), with an optional title, description and action row around its
/// <see cref="Content"/>. The user drags it between detents and away (inner scrollers hand the drag over at their edge), flings it,
/// taps the scrim, presses Back or Escape; the app opens and closes it with <see cref="IsOpen"/>, <see cref="OpenAsync"/>,
/// <see cref="CloseAsync"/> and <see cref="Close"/>. Every motion runs on the render thread and the scrim follows the sheet's position
/// there. A modal sheet (<see cref="IsModal"/>) dims the page, traps keyboard focus and takes the page out of what screen readers read.
/// </summary>
/// <remarks>
/// <para>
/// Placed in XAML as the last child of a layout that fills the page (a grid's cell spanning it): it fills that space and draws nothing
/// while closed, and pointers pass through it. <see cref="ShowAsync{TResult}"/> opens one over the whole surface without placing it.
/// Several sheets can be open; the last opened is on top and the only modal pane.
/// </para>
/// <para>
/// A sheet is drawn inside its surface: it cannot cover hosted native views (<see cref="SkUiMauiContentView"/>) or native chrome outside
/// the surface (a Shell tab bar). Lower detents show part of the sheet, which keeps its full length: its bottom is off the surface.
/// </para>
/// </remarks>
[ContentProperty(nameof(Content))]
public partial class SkUiSheet : SkUiView, ISkUiKeyHandler
{
    /// <summary>Appended to the sheet's <c>AutomationId</c> for its scrim's.</summary>
    public const string ScrimAutomationIdSuffix = "_Scrim";

    /// <summary>Appended to the sheet's <c>AutomationId</c> for its grabber's.</summary>
    public const string GrabberAutomationIdSuffix = "_Grabber";

    private enum SheetState
    {
        Closed,
        Opening,
        Open,
        Closing
    }

    private readonly SkUiSheetEngine _engine = new();
    private SheetState _state;
    private Track? _track;
    private Strip? _strip;
    private Panel? _panel;
    private Scrim? _scrim;
    private bool _openPending;
    private bool _requesting;
    private bool _moving; // a programmatic motion of the sheet's own runs (tween to a detent, a dialog's fade)
    private bool _laidOut;
    private bool _isDialog;
    private SkUiSheetEdgePhysical _physical;
    private double _length; // the sheet's own length along its axis (its height, or its width for a side sheet)
    private Size _size;
    private object? _result;
    private bool _hasResult;
    private TaskCompletionSource? _opened;
    private TaskCompletionSource? _closed;
    private SkUiView? _modalRoot;
    private WeakReference<ISkUiAccessibleNode>? _focusBefore;
    private SkUiWeakListener<SkUiSheet>? _detentsListener;
    private int _writingDetent;
    private IDisposable? _backHandler;

    /// <summary>Creates a closed sheet.</summary>
    public SkUiSheet()
    {
        AttachChild(ScrimView);
        AttachChild(TrackView);
    }

    #region Bindable properties

    /// <summary>Bindable property for <see cref="Content"/>.</summary>
    public static readonly BindableProperty ContentProperty = BindableProperty.Create(nameof(Content), typeof(View), typeof(SkUiSheet), null,
        validateValue: static (_, value) => value is null or ISkUiView,
        propertyChanged: (view, _, value) => ((SkUiSheet)view).PanelView.SetContent((View?)value));

    /// <summary>Bindable property for <see cref="Edge"/>.</summary>
    public static readonly BindableProperty EdgeProperty = BindableProperty.Create(nameof(Edge), typeof(SkUiSheetEdge), typeof(SkUiSheet), SkUiSheetEdge.Bottom,
        validateValue: static (_, value) => value is SkUiSheetEdge edge && Enum.IsDefined(edge),
        propertyChanged: (view, _, _) => ((SkUiSheet)view).OnGeometryChanged());

    /// <summary>Bindable property for <see cref="Detents"/>.</summary>
    public static readonly BindableProperty DetentsProperty = BindableProperty.Create(nameof(Detents), typeof(IList<SkUiSheetDetent>), typeof(SkUiSheet), null,
        validateValue: SkUiValidate.NotNull,
        defaultValueCreator: _ => new List<SkUiSheetDetent> { SkUiSheetDetent.Fraction(0.5), SkUiSheetDetent.Fraction(1) },
        propertyChanged: (view, _, value) => ((SkUiSheet)view).OnDetentsChanged((IList<SkUiSheetDetent>)value));

    /// <summary>Bindable property for <see cref="SelectedDetentIndex"/> (two-way by default: drags and flings set it).</summary>
    public static readonly BindableProperty SelectedDetentIndexProperty = BindableProperty.Create(nameof(SelectedDetentIndex), typeof(int), typeof(SkUiSheet), 0,
        BindingMode.TwoWay, validateValue: static (_, value) => value is >= 0,
        propertyChanged: (view, oldValue, value) => ((SkUiSheet)view).OnSelectedDetentIndexChanged((int)oldValue, (int)value));

    /// <summary>Bindable property for <see cref="IsOpen"/> (two-way by default: the user closes it).</summary>
    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(nameof(IsOpen), typeof(bool), typeof(SkUiSheet), false,
        BindingMode.TwoWay, propertyChanged: (view, _, value) => ((SkUiSheet)view).OnIsOpenChanged((bool)value));

    /// <summary>Bindable property for <see cref="IsModal"/>.</summary>
    public static readonly BindableProperty IsModalProperty = BindableProperty.Create(nameof(IsModal), typeof(bool), typeof(SkUiSheet), true,
        propertyChanged: (view, _, _) => ((SkUiSheet)view).OnIsModalChanged());

    /// <summary>Bindable property for <see cref="ScrimColor"/>.</summary>
    public static readonly BindableProperty ScrimColorProperty = BindableProperty.Create(nameof(ScrimColor), typeof(Color), typeof(SkUiSheet), null,
        propertyChanged: (view, _, _) => ((SkUiSheet)view).ScrimView.InvalidatePaint());

    /// <summary>Bindable property for <see cref="DismissOnScrimTap"/>.</summary>
    public static readonly BindableProperty DismissOnScrimTapProperty = BindableProperty.Create(nameof(DismissOnScrimTap), typeof(bool), typeof(SkUiSheet), true);

    /// <summary>Bindable property for <see cref="IsDragEnabled"/>.</summary>
    public static readonly BindableProperty IsDragEnabledProperty = BindableProperty.Create(nameof(IsDragEnabled), typeof(bool), typeof(SkUiSheet), true,
        propertyChanged: (view, _, _) => ((SkUiSheet)view).OnIsDragEnabledChanged());

    /// <summary>Bindable property for <see cref="HasGrabber"/>.</summary>
    public static readonly BindableProperty HasGrabberProperty = BindableProperty.Create(nameof(HasGrabber), typeof(bool), typeof(SkUiSheet), true,
        propertyChanged: (view, _, _) => ((SkUiSheet)view).OnGeometryChanged());

    /// <summary>Bindable property for <see cref="CornerRadius"/>.</summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(SkUiSheet), -1d,
        validateValue: SkUiValidate.NonNegativeOrLook,
        propertyChanged: (view, _, _) => ((SkUiSheet)view).PanelView.InvalidatePaint());

    /// <summary>Bindable property for <see cref="Presentation"/>.</summary>
    public static readonly BindableProperty PresentationProperty = BindableProperty.Create(nameof(Presentation), typeof(SkUiSheetPresentation), typeof(SkUiSheet),
        SkUiSheetPresentation.Sheet, validateValue: static (_, value) => value is SkUiSheetPresentation presentation && Enum.IsDefined(presentation),
        propertyChanged: (view, _, _) => ((SkUiSheet)view).OnGeometryChanged());

    /// <summary>Bindable property for <see cref="Title"/>.</summary>
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(SkUiSheet), null,
        propertyChanged: (view, _, value) => ((SkUiSheet)view).PanelView.SetTitle((string?)value));

    /// <summary>Bindable property for <see cref="Description"/>.</summary>
    public static readonly BindableProperty DescriptionProperty = BindableProperty.Create(nameof(Description), typeof(string), typeof(SkUiSheet), null,
        propertyChanged: (view, _, value) => ((SkUiSheet)view).PanelView.SetDescription((string?)value));

    /// <summary>Bindable property for <see cref="Actions"/>.</summary>
    public static readonly BindableProperty ActionsProperty = BindableProperty.Create(nameof(Actions), typeof(View), typeof(SkUiSheet), null,
        validateValue: static (_, value) => value is null or ISkUiView,
        propertyChanged: (view, _, value) => ((SkUiSheet)view).PanelView.SetActions((View?)value));

    /// <summary>What the sheet shows below its header (a drawn view; Core content through an <see cref="Core.SkUiCoreHost"/>).</summary>
    public View? Content { get => (View?)GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>The edge the sheet comes from (default <see cref="SkUiSheetEdge.Bottom"/>; <c>Start</c> and <c>End</c> follow the flow direction).</summary>
    public SkUiSheetEdge Edge { get => (SkUiSheetEdge)GetValue(EdgeProperty); set => SetValue(EdgeProperty, value); }

    /// <summary>
    /// The heights the sheet rests at (default half and full; a side sheet that sets none is full), each measured along the sheet's axis
    /// (a side sheet's width). In XAML: <c>"Fit, 50%, 100%"</c>. A list that raises collection changes is followed.
    /// </summary>
    [TypeConverter(typeof(SkUiSheetDetentsTypeConverter))]
    public IList<SkUiSheetDetent> Detents { get => (IList<SkUiSheetDetent>)GetValue(DetentsProperty); set => SetValue(DetentsProperty, value); }

    /// <summary>The detent the sheet rests at, an index into <see cref="Detents"/> (two-way: drags and flings set it); it opens there.</summary>
    public int SelectedDetentIndex { get => (int)GetValue(SelectedDetentIndexProperty); set => SetValue(SelectedDetentIndexProperty, value); }

    /// <summary>Whether the sheet is open or opening (two-way: the user closes it).</summary>
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>Whether the sheet dims the page with a scrim that takes its pointers, traps keyboard focus and hides the page from screen readers (default <c>true</c>).</summary>
    public bool IsModal { get => (bool)GetValue(IsModalProperty); set => SetValue(IsModalProperty, value); }

    /// <summary>The scrim's color while the sheet is open (default <c>null</c>: the look's <see cref="SkUiLook.SheetScrimColor"/>).</summary>
    public Color? ScrimColor { get => (Color?)GetValue(ScrimColorProperty); set => SetValue(ScrimColorProperty, value); }

    /// <summary>Whether a tap on the scrim closes the sheet (default <c>true</c>); the scrim takes the tap either way.</summary>
    public bool DismissOnScrimTap { get => (bool)GetValue(DismissOnScrimTapProperty); set => SetValue(DismissOnScrimTapProperty, value); }

    /// <summary>Whether the user can drag the sheet between its detents and away (default <c>true</c>).</summary>
    public bool IsDragEnabled { get => (bool)GetValue(IsDragEnabledProperty); set => SetValue(IsDragEnabledProperty, value); }

    /// <summary>Whether the sheet shows a grabber at its inner edge (default <c>true</c>; never on a dialog).</summary>
    public bool HasGrabber { get => (bool)GetValue(HasGrabberProperty); set => SetValue(HasGrabberProperty, value); }

    /// <summary>The corner radius on the edge the sheet opens from, every corner of a dialog, in DIPs (default -1: the look's <see cref="SkUiLook.DefaultSheetCornerRadius"/>).</summary>
    public double CornerRadius { get => (double)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>How the sheet shows: from its edge, as a centered dialog, or one by the surface's width (default <see cref="SkUiSheetPresentation.Sheet"/>).</summary>
    public SkUiSheetPresentation Presentation { get => (SkUiSheetPresentation)GetValue(PresentationProperty); set => SetValue(PresentationProperty, value); }

    /// <summary>The title above the content, drawn as a picker popup's (a heading for screen readers); <c>null</c>: none.</summary>
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>A line of text under the title, in the muted color; <c>null</c>: none.</summary>
    public string? Description { get => (string?)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>A row of the app's (buttons) below the content, padded as a picker popup's buttons; <c>null</c>: none.</summary>
    public View? Actions { get => (View?)GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    /// <summary>Sets <see cref="Content"/> (same as the property setter).</summary>
    public SkUiSheet SetContent(View? value) { ThrowIfNotDrawn(value, nameof(value)); Content = value; return this; }

    /// <summary>Sets <see cref="Edge"/>.</summary>
    public SkUiSheet SetEdge(SkUiSheetEdge value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value), value, null);
        Edge = value;
        return this;
    }

    /// <summary>Sets <see cref="Detents"/> (a copy of <paramref name="value"/>).</summary>
    public SkUiSheet SetDetents(params IEnumerable<SkUiSheetDetent> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Detents = [.. value];
        return this;
    }

    /// <summary>Sets <see cref="SelectedDetentIndex"/>.</summary>
    public SkUiSheet SetSelectedDetentIndex(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        SelectedDetentIndex = value;
        return this;
    }

    /// <summary>Sets <see cref="IsOpen"/> (same as the property setter).</summary>
    public SkUiSheet SetIsOpen(bool value) { IsOpen = value; return this; }

    /// <summary>Sets <see cref="IsModal"/> (same as the property setter).</summary>
    public SkUiSheet SetIsModal(bool value) { IsModal = value; return this; }

    /// <summary>Sets <see cref="ScrimColor"/> (same as the property setter).</summary>
    public SkUiSheet SetScrimColor(Color? value) { ScrimColor = value; return this; }

    /// <summary>Sets <see cref="DismissOnScrimTap"/> (same as the property setter).</summary>
    public SkUiSheet SetDismissOnScrimTap(bool value) { DismissOnScrimTap = value; return this; }

    /// <summary>Sets <see cref="IsDragEnabled"/> (same as the property setter).</summary>
    public SkUiSheet SetIsDragEnabled(bool value) { IsDragEnabled = value; return this; }

    /// <summary>Sets <see cref="HasGrabber"/> (same as the property setter).</summary>
    public SkUiSheet SetHasGrabber(bool value) { HasGrabber = value; return this; }

    /// <summary>Sets <see cref="CornerRadius"/>: DIPs, or -1 for the look's.</summary>
    public SkUiSheet SetCornerRadius(double value)
    {
        SkUiValidate.ThrowIfNotNonNegativeOrLook(value, nameof(value));
        CornerRadius = value;
        return this;
    }

    /// <summary>Sets <see cref="Presentation"/>.</summary>
    public SkUiSheet SetPresentation(SkUiSheetPresentation value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value), value, null);
        Presentation = value;
        return this;
    }

    /// <summary>Sets <see cref="Title"/> (same as the property setter).</summary>
    public SkUiSheet SetTitle(string? value) { Title = value; return this; }

    /// <summary>Sets <see cref="Description"/> (same as the property setter).</summary>
    public SkUiSheet SetDescription(string? value) { Description = value; return this; }

    /// <summary>Sets <see cref="Actions"/> (same as the property setter).</summary>
    public SkUiSheet SetActions(View? value) { ThrowIfNotDrawn(value, nameof(value)); Actions = value; return this; }

    private static void ThrowIfNotDrawn(View? value, string name)
    {
        if (value is not null and not ISkUiView)
            throw new ArgumentException("A sheet shows drawn views; host a native view in an SkUiMauiContentView.", name);
    }

    #endregion

    #region Events

    /// <summary>The sheet finished opening (its motion ended on a detent, or it showed at once).</summary>
    public event EventHandler? Opened;

    /// <summary>The sheet closed: with what <see cref="Close"/> was given, or dismissed.</summary>
    public event EventHandler<SkUiSheetClosedEventArgs>? Closed;

    /// <summary>The detent the sheet rests at changed (<see cref="SelectedDetentIndex"/>), by the user or the app.</summary>
    public event EventHandler<SkUiSelectedIndexChangedEventArgs>? DetentChanged;

    #endregion

    #region Opening and closing

    /// <summary>Opens the sheet at <see cref="SelectedDetentIndex"/>; completes when it is open (at once when it already is).</summary>
    public Task OpenAsync()
    {
        if (_state == SheetState.Open)
            return Task.CompletedTask;
        var opened = _opened ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IsOpen = true;
        if (_state == SheetState.Closing)
            BeginOpen(); // IsOpen stayed true while a drag or a scrim tap closed it
        return opened.Task;
    }

    /// <summary>Closes the sheet without a result (dismissed); completes when it is closed (at once when it already is).</summary>
    public Task CloseAsync() => CloseCore(null, hasResult: false);

    /// <summary>Closes the sheet with <paramref name="result"/>, which <see cref="Closed"/> and <see cref="ShowAsync{TResult}"/> report.</summary>
    public void Close(object? result) => _ = CloseCore(result, hasResult: true);

    private Task CloseCore(object? result, bool hasResult)
    {
        if (_state == SheetState.Closed)
            return Task.CompletedTask;
        _result = result;
        _hasResult = hasResult;
        var closed = _closed ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (IsOpen)
            this.WriteBack(IsOpenProperty, false); // runs BeginClose
        else
            BeginClose();
        return closed.Task;
    }

    /// <summary>The user closed the sheet (the scrim, Back, Escape): dismissed, unless it is already closing.</summary>
    internal void Dismiss()
    {
        if (_state is SheetState.Opening or SheetState.Open)
            _ = CloseCore(null, hasResult: false);
    }

    private void OnIsOpenChanged(bool value)
    {
        if (value)
            BeginOpen();
        else
            BeginClose();
    }

    private void BeginOpen()
    {
        if (_state is SheetState.Opening or SheetState.Open)
            return;
        var wasClosed = _state == SheetState.Closed;
        _state = SheetState.Opening;
        _result = null;
        _hasResult = false;
        CompleteClosed(); // opened again while closing: a CloseAsync waiting for it is done
        if (wasClosed)
        {
            ScrimView.IsVisible = IsModal;
            TrackView.IsVisible = true;
            IsShownChanged += OnShownChanged;
        }
        // Over every sheet opened before it, wherever it is placed, also as the modal pane (opened again while closing: back on top).
        if (!Hoist() && SurfaceOverlayShowing is { } overlay)
            overlay.BringToFront(this);
        if (_modalRoot is { } modalRoot)
            modalRoot.AddModalPane(this);
        else if (_laidOut)
            EnsureModal();
        _openPending = true;
        if (_laidOut && wasClosed)
            InvalidateMeasureOverride(); // the parts were hidden: laid out before they move
        else if (_laidOut)
            StartPendingOpen();
        else
            InvalidateMeasureOverride();
    }

    private void BeginClose()
    {
        if (_state is SheetState.Closed or SheetState.Closing)
            return;
        _openPending = false;
        _state = SheetState.Closing;
        _opened?.TrySetResult(); // an open that never finished: done waiting
        _opened = null;
        if (!_laidOut || !TrackView.IsVisible)
        {
            FinishClose();
            return;
        }
        var transition = Transition;
        if (_isDialog)
        {
            AnimateDialog(show: false, transition);
            return;
        }
        MoveTo(0, transition);
    }

    /// <summary>The look's transition for the sheet's motions (none under reduced motion).</summary>
    private SkUiTransition Transition
    {
        get
        {
            using var lookScope = SkUiLookScope.Enter(this);
            return SkUiLook.Current.GetTransition(_isDialog ? SkUiTransitionKind.SheetDialog : SkUiTransitionKind.Sheet);
        }
    }

    /// <summary>Starts the opening motion once the sheet has been laid out with its parts shown.</summary>
    private void StartPendingOpen()
    {
        if (!_openPending || _state != SheetState.Opening)
            return;
        _openPending = false;
        var transition = Transition;
        if (_isDialog)
        {
            AnimateDialog(show: true, transition);
            return;
        }
        // From where it is: closed, or caught while closing and opened again.
        MoveTo(_engine.HeightOf(SelectedDetentIndex), transition);
    }

    /// <summary>Moves the sheet to show <paramref name="position"/> DIPs, by <paramref name="transition"/> (at once without one).</summary>
    private void MoveTo(double position, SkUiTransition transition)
    {
        var controller = TrackView.Controller;
        var offset = OffsetFor(position);
        _requesting = true;
        try
        {
            if (transition.IsNone || !SurfaceIsLive)
            {
                controller.StopMotion();
                controller.SetOffset(Horizontal ? offset : 0, Horizontal ? 0 : offset);
                _moving = false;
            }
            else
            {
                _moving = true;
                _motionTarget = position;
                _ = controller.ScrollToAsync(Horizontal ? offset : 0, Horizontal ? 0 : offset, transition);
                if (!controller.IsMotionRunning)
                    _moving = false;
            }
        }
        finally
        {
            _requesting = false;
        }
        if (!_moving)
            OnRest();
    }

    /// <summary>A dialog fades in from slightly below, or out; the scrim fades with it.</summary>
    private void AnimateDialog(bool show, SkUiTransition transition)
    {
        var panel = PanelView;
        var scrim = ScrimView;
        var from = panel.Opacity;
        var fromY = panel.TranslationY;
        var scrimFrom = scrim.Opacity;
        panel.Opacity = show ? 1 : 0;
        panel.TranslationY = show ? 0 : DialogSlide;
        scrim.Opacity = show ? 1 : 0;
        if (transition.IsNone)
        {
            _moving = false;
            OnRest();
            return;
        }
        _moving = true;
        var length = (uint)Math.Max(1, transition.Duration.TotalMilliseconds);
        // From closed: opening from its place shown at once before the fade (from 1, in place), or from transparent.
        var fromClosed = show && (_state == SheetState.Opening && from >= 1 && Math.Abs(fromY) < SkUiSheetEngine.Tolerance || from < OpacityTolerance);
        var startFrom = fromClosed ? 0 : from;
        var startY = fromClosed ? DialogSlide : fromY;
        var version = ++_dialogVersion;
        _ = panel.AnimateFromAsync(SkUiAnimatableProperty.Opacity, startFrom, length, transition.Easing, completed =>
        {
            if (version != _dialogVersion)
                return;
            _moving = false;
            OnRest();
        });
        _ = panel.AnimateFromAsync(SkUiAnimatableProperty.TranslationY, startY, length, transition.Easing);
        if (IsModal)
            _ = scrim.AnimateFromAsync(SkUiAnimatableProperty.Opacity, fromClosed ? 0 : scrimFrom, length, transition.Easing);
    }

    private int _dialogVersion;

    /// <summary>How far below its place a dialog starts fading in, in DIPs.</summary>
    private const double DialogSlide = 24;

    /// <summary>An opacity this close to 0 counts as transparent.</summary>
    private const double OpacityTolerance = 0.001;

    /// <summary>The sheet came to rest (a motion ended, a drag was released and settled, or it was placed at once).</summary>
    private void OnRest()
    {
        if (_state == SheetState.Closed || _moving)
            return;
        if (_isDialog)
        {
            if (_state == SheetState.Closing)
                FinishClose();
            else if (_state == SheetState.Opening)
                FinishOpen();
            return;
        }
        var position = Position;
        if (position < SkUiSheetEngine.Tolerance)
        {
            if (_state != SheetState.Opening || !_openPending)
                FinishClose();
            return;
        }
        if (_state == SheetState.Closing)
        {
            // A drag caught the closing sheet and put it back: it stays open.
            _state = SheetState.Open;
            _hasResult = false;
            _result = null;
            if (!IsOpen)
                this.WriteBack(IsOpenProperty, true);
            CompleteClosed();
        }
        var index = _engine.NearestIndex(position, SelectedDetentIndex);
        WriteDetent(index);
        if (_state == SheetState.Opening)
            FinishOpen();
        // Released between detents (the keyboard gave back its room, the layout changed under a drag): on to the nearest.
        if (Math.Abs(position - _engine.HeightOf(index)) >= SkUiSheetEngine.Tolerance && TrackView.KeyboardInset <= 0 && !TrackView.Controller.Dragging)
            MoveTo(_engine.HeightOf(index), Transition);
    }

    private void FinishOpen()
    {
        _state = SheetState.Open;
        MoveFocusIn();
        _opened?.TrySetResult();
        _opened = null;
        Opened?.Invoke(this, EventArgs.Empty);
    }

    private void FinishClose()
    {
        if (_state == SheetState.Closed)
            return;
        _state = SheetState.Closed;
        _openPending = false;
        _moving = false;
        ScrimView.IsVisible = false;
        TrackView.IsVisible = false;
        IsShownChanged -= OnShownChanged;
        Unhoist();
        var result = _result;
        var dismissed = !_hasResult;
        _result = null;
        _hasResult = false;
        LeaveModal();
        if (IsOpen)
            this.WriteBack(IsOpenProperty, false);
        _opened?.TrySetResult();
        _opened = null;
        CompleteClosed();
        Closed?.Invoke(this, new SkUiSheetClosedEventArgs(result, dismissed));
    }

    private void CompleteClosed()
    {
        var closed = _closed;
        _closed = null;
        closed?.TrySetResult();
    }

    /// <summary>Where the sheet's own running motion takes it (<see cref="_moving"/>), in DIPs shown.</summary>
    private double _motionTarget;

    /// <summary>
    /// The track stopped moving: where the user left it, the sheet comes to rest; its own motion ended, or was stopped by something
    /// other than the user (the sheet or a layout around it disabled, its edge changed), and it goes where that motion was going.
    /// </summary>
    private void OnTrackMovingChanged(bool moving)
    {
        if (moving || _requesting)
            return;
        var controller = TrackView.Controller;
        if (controller.Dragging || controller.IsCatching)
        {
            // A press caught the motion (it stops before the drag starts): the sheet rests where the user leaves it, not at the target.
            _moving = false;
            return;
        }
        if (_moving && Math.Abs(Position - _motionTarget) >= SkUiSheetEngine.Tolerance)
        {
            _moving = false;
            MoveTo(_motionTarget, SkUiTransition.None); // at once, then rests there (closed, opened, at its detent)
            return;
        }
        _moving = false;
        OnRest();
    }

    #endregion

    #region Detents

    private void OnDetentsChanged(IList<SkUiSheetDetent> value)
    {
        // Weakly: a view model's list outlives the page.
        (_detentsListener ??= new SkUiWeakListener<SkUiSheet>(this, static (sheet, change) =>
        {
            if (change.Kind == SkUiChangeKind.Collection)
                sheet.OnGeometryChanged();
        })).Listen(value as INotifyCollectionChanged);
        OnGeometryChanged();
    }

    /// <summary>The detents in effect: <see cref="Detents"/>, or one full detent for a side sheet that sets none.</summary>
    private IReadOnlyList<SkUiSheetDetent> EffectiveDetents =>
        _physical != SkUiSheetEdgePhysical.Bottom && !IsSet(DetentsProperty) ? FullDetent
        : Detents as IReadOnlyList<SkUiSheetDetent> ?? [.. Detents];

    private static readonly SkUiSheetDetent[] FullDetent = [SkUiSheetDetent.Fraction(1)];

    private void OnSelectedDetentIndexChanged(int oldValue, int value)
    {
        if (_writingDetent == 0 && _state == SheetState.Open && _laidOut && !_isDialog)
            MoveTo(_engine.HeightOf(value), Transition);
        DetentChanged?.Invoke(this, new SkUiSelectedIndexChangedEventArgs(oldValue, value));
    }

    /// <summary>The user's drag or fling put the sheet on detent <paramref name="index"/>.</summary>
    private void WriteDetent(int index)
    {
        if (index == SelectedDetentIndex)
            return;
        _writingDetent++;
        try
        {
            this.WriteBack(SelectedDetentIndexProperty, index);
        }
        finally
        {
            _writingDetent--;
        }
    }

    /// <summary>Moves to the next detent up (or down), wrapping round from the tallest to the lowest when <paramref name="wraps"/>; returns whether it moved.</summary>
    internal bool StepDetent(bool up, bool wraps)
    {
        if (_state != SheetState.Open || _isDialog || !_engine.HasGeometry)
            return false;
        var target = _engine.Step(Position, up) ?? (wraps ? (up ? _engine.LowestHeight : _engine.MaximumHeight) : null);
        if (target is not { } height || Math.Abs(height - Position) < SkUiSheetEngine.Tolerance)
            return false;
        WriteDetent(_engine.NearestIndex(height, SelectedDetentIndex));
        MoveTo(height, Transition);
        return true;
    }

    #endregion

    #region Geometry

    /// <summary>How much of the sheet shows along its axis now (0: closed).</summary>
    internal double Position => PositionOf(Horizontal ? TrackView.Controller.X : TrackView.Controller.Y);

    /// <summary>The resolved detents and geometry (tests).</summary>
    internal SkUiSheetEngine Engine => _engine;

    /// <summary>Whether the sheet shows as a dialog now (tests).</summary>
    internal bool IsDialog => _isDialog;

    private bool Horizontal => !_isDialog && _physical != SkUiSheetEdgePhysical.Bottom;

    /// <summary>The track's offset that shows <paramref name="position"/> DIPs of the sheet.</summary>
    private double OffsetFor(double position) => _isDialog ? 0 : _physical == SkUiSheetEdgePhysical.Left ? _engine.MaximumHeight - position : position;

    private double PositionOf(double offset) => _isDialog ? (_state == SheetState.Closed ? 0 : 1) : _physical == SkUiSheetEdgePhysical.Left ? _engine.MaximumHeight - offset : offset;

    private void OnGeometryChanged()
    {
        if (_physical != PhysicalEdge && _state != SheetState.Closed)
            TrackView.Controller.StopMotion();
        InvalidateMeasureOverride();
        PanelView.InvalidatePaint();
    }

    private SkUiSheetEdgePhysical PhysicalEdge => Edge switch
    {
        SkUiSheetEdge.Start => IsRightToLeft ? SkUiSheetEdgePhysical.Right : SkUiSheetEdgePhysical.Left,
        SkUiSheetEdge.End => IsRightToLeft ? SkUiSheetEdgePhysical.Left : SkUiSheetEdgePhysical.Right,
        _ => SkUiSheetEdgePhysical.Bottom
    };

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged()
    {
        base.OnEffectiveFlowDirectionChanged();
        OnGeometryChanged();
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        // Fills what it is given and asks for nothing: it never sizes the layout it covers.
        new(double.IsFinite(widthConstraint) ? widthConstraint : 0, double.IsFinite(heightConstraint) ? heightConstraint : 0);

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        _size = size;
        using (SkUiLookScope.Enter(this))
            UpdateGeometry(size);
        var full = new Rect(0, 0, size.Width, size.Height);
        ((IView)ScrimView).Measure(size.Width, size.Height);
        ((IView)ScrimView).Arrange(full);
        ((IView)TrackView).Measure(size.Width, size.Height);
        ((IView)TrackView).Arrange(full);
        _laidOut = size.Width > 0 && size.Height > 0;
        OnTrackArranged(); // also when the track kept its layout: the detents may have moved
    }

    /// <summary>Resolves the presentation, the window's insets, the panel's padding and size, and the detents for <paramref name="size"/>.</summary>
    private void UpdateGeometry(Size size)
    {
        var look = SkUiLook.Current;
        var wasDialog = _isDialog;
        _isDialog = Presentation == SkUiSheetPresentation.Dialog
                    || (Presentation == SkUiSheetPresentation.Auto && size.Width >= look.SheetDialogBreakpoint);
        if (wasDialog != _isDialog && _state != SheetState.Closed)
            TrackView.Controller.StopMotion();
        _physical = PhysicalEdge;
        var insets = WindowInsets();
        var panel = PanelView;
        var grabber = HasGrabber && !_isDialog ? look.SheetGrabberBandHeight : 0;
        Thickness padding;
        Rect frame;
        if (_isDialog)
        {
            padding = new Thickness(0, 0, 0, 8);
            var margin = look.SheetDialogMargin;
            var room = new Rect(insets.Left + margin.Left, insets.Top + margin.Top,
                Math.Max(0, size.Width - insets.HorizontalThickness - margin.HorizontalThickness),
                Math.Max(0, size.Height - insets.VerticalThickness - margin.VerticalThickness));
            var width = Math.Min(look.SheetDialogMaximumWidth, room.Width);
            var fit = panel.Fit(width, padding, horizontal: false);
            var height = Math.Min(fit, room.Height);
            frame = new Rect(room.X + (room.Width - width) / 2, room.Y + (room.Height - height) / 2, width, height);
            _engine.Resolve(FullDetent, height, height, 0);
            _length = size.Height;
            TrackView.Orientation = ScrollOrientation.Neither;
            StripView.Extent = size;
        }
        else if (_physical == SkUiSheetEdgePhysical.Bottom)
        {
            padding = new Thickness(insets.Left, grabber, insets.Right, insets.Bottom);
            var width = Math.Min(size.Width, look.SheetMaximumWidth);
            // Centered when narrower than the surface: only the insets it reaches keep it clear.
            var left = (size.Width - width) / 2;
            padding = new Thickness(Math.Max(0, insets.Left - left), grabber, Math.Max(0, insets.Right - left), insets.Bottom);
            var available = Math.Max(0, size.Height - insets.Top - look.SheetTopMargin);
            var fit = panel.Fit(width, padding, horizontal: false);
            _engine.Resolve(EffectiveDetents, available, fit, insets.Bottom);
            _length = size.Height;
            frame = new Rect(left, size.Height, width, _engine.MaximumHeight);
            TrackView.Orientation = ScrollOrientation.Vertical;
            StripView.Extent = new Size(size.Width, size.Height + _engine.MaximumHeight);
        }
        else
        {
            var left = _physical == SkUiSheetEdgePhysical.Left;
            var edgeInset = left ? insets.Left : insets.Right;
            padding = left
                ? new Thickness(insets.Left, insets.Top, grabber, insets.Bottom)
                : new Thickness(grabber, insets.Top, insets.Right, insets.Bottom);
            var available = Math.Max(0, Math.Min(look.SideSheetMaximumWidth + edgeInset, size.Width - SidePageMargin));
            var fit = panel.Fit(size.Height, padding, horizontal: true);
            _engine.Resolve(EffectiveDetents, available, fit, edgeInset);
            _length = size.Width;
            var max = _engine.MaximumHeight;
            frame = left ? new Rect(0, 0, max, size.Height) : new Rect(size.Width, 0, max, size.Height);
            TrackView.Orientation = ScrollOrientation.Horizontal;
            StripView.Extent = new Size(size.Width + max, size.Height);
        }
        panel.Layout(frame, padding, grabber);
        ScrimView.ForgetLinks();
    }

    /// <summary>The room a side sheet leaves showing of the page, in DIPs (Material's navigation drawer).</summary>
    private const double SidePageMargin = 56;

    /// <summary>How far the window's insets reach into the sheet's rectangle, from its surface root's.</summary>
    private Thickness WindowInsets()
    {
        if (SkUiDiagnostics.GetSurfaceRoot(this) is not { } root || SkUiDiagnostics.GetRootBounds(this) is not { } bounds)
            return default;
        var insets = root.SurfaceWindowInsets;
        var rootSize = root.Frame.Size;
        return new Thickness(
            Math.Max(0, insets.Left - bounds.Left),
            Math.Max(0, insets.Top - bounds.Top),
            Math.Max(0, insets.Right - (rootSize.Width - bounds.Right)),
            Math.Max(0, insets.Bottom - (rootSize.Height - bounds.Bottom)));
    }

    /// <summary>The track was arranged: the sheet goes where its state says, and a pending open starts.</summary>
    private void OnTrackArranged()
    {
        if (!_laidOut && (_size.Width <= 0 || _size.Height <= 0))
            return;
        _laidOut = true;
        EnsureModal();
        var controller = TrackView.Controller;
        if (_openPending && _state == SheetState.Opening)
        {
            // Starts from closed: the dialog transparent below its place, the sheet just off its edge.
            if (_isDialog)
            {
                PlaceAt(0);
                PanelView.Opacity = 0;
                PanelView.TranslationY = DialogSlide;
                ScrimView.Opacity = 0;
            }
            else if (!controller.IsMotionRunning && !controller.Dragging && Position < SkUiSheetEngine.Tolerance)
            {
                PlaceAt(0);
                PanelView.Opacity = 1;
                PanelView.TranslationY = 0;
                ScrimView.Opacity = 1;
            }
            StartPendingOpen();
            return;
        }
        if (controller.IsMotionRunning || controller.Dragging || controller.IsCatching || _moving)
            return;
        switch (_state)
        {
            case SheetState.Closed:
                PlaceAt(0);
                break;
            case SheetState.Open when !_isDialog:
                // Laid out again (rotated, the content's height changed): back on its detent.
                PlaceAt(_engine.HeightOf(SelectedDetentIndex));
                break;
            case SheetState.Open:
                PlaceAt(0);
                PanelView.Opacity = 1;
                PanelView.TranslationY = 0;
                ScrimView.Opacity = 1;
                break;
        }
    }

    /// <summary>Puts the sheet at <paramref name="position"/> at once, without coming to rest.</summary>
    private void PlaceAt(double position)
    {
        var offset = OffsetFor(position);
        var controller = TrackView.Controller;
        _requesting = true;
        try
        {
            controller.SetOffset(Horizontal ? offset : 0, Horizontal ? 0 : offset);
        }
        finally
        {
            _requesting = false;
        }
    }

    /// <summary>Where a drag or fling released on the track comes to rest, in track offsets.</summary>
    private double SnapTarget(bool horizontal, double offset, double velocity, double origin)
    {
        // Offsets grow as the sheet opens, except for a sheet from the left, where they shrink.
        var sign = _physical == SkUiSheetEdgePhysical.Left ? -1 : 1;
        var resting = _engine.RestingPosition(PositionOf(offset), sign * velocity, dismissible: true);
        return OffsetFor(resting);
    }

    #endregion

    #region Drawn over the surface

    private SkUiSurfaceOverlay? _hoistedOn;

    /// <inheritdoc />
    private protected override SkUiView? RenderHost => _hoistedOn;

    /// <summary>The overlay layer the sheet shows on: its own (<see cref="ShowAsync{TResult}"/>) or the one it is drawn on while open.</summary>
    private SkUiSurfaceOverlay? SurfaceOverlayShowing => _hoistedOn ?? Parent as SkUiSurfaceOverlay;

    /// <summary>
    /// An open sheet placed in a layout is drawn, hit and read on its surface root's overlay layer, on top of every sheet opened before
    /// it (one placed earlier in the page, one around its own place, one inside another sheet), while its layout keeps it as a child
    /// (bindings, look, measure, where it is arranged). Returns whether it was moved there now.
    /// </summary>
    private bool Hoist()
    {
        if (_hoistedOn is not null || _state == SheetState.Closed || Parent is SkUiSurfaceOverlay || SkiaParent is null
            || SkUiDiagnostics.GetSurfaceRoot(this) is not { } root || ReferenceEquals(root, this) || HiddenByAncestor)
            return false;
        var overlay = root.SurfaceOverlay;
        _hoistedOn = overlay; // first: what it marks from now on reaches the layer
        overlay.Hoist(this); // its look stays its layout's: drawn nodes resolve looks through logical parents
        return true;
    }

    /// <summary>A layout around the sheet is hidden (<see cref="VisualElement.IsVisible"/>): it shows nothing and traps nothing.</summary>
    private bool HiddenByAncestor
    {
        get
        {
            for (var node = SkiaParent; node is not null; node = node.SkiaParent)
                if (!node.IsVisible)
                    return true;
            return false;
        }
    }

    /// <summary>
    /// An open sheet's <see cref="SkUiView.IsShown"/> changed: hidden (a layout around it hidden, its surface released), it is no longer
    /// drawn over the surface nor its modal pane, and stays open; shown again, it is both again.
    /// </summary>
    private void OnShownChanged(object? sender, EventArgs e)
    {
        if (_state == SheetState.Closed)
            return;
        if (IsShown)
        {
            Hoist();
            EnsureModal();
        }
        else
        {
            Unhoist();
            LeaveModal();
        }
    }

    /// <summary>Draws the sheet in its layout again.</summary>
    private void Unhoist()
    {
        if (_hoistedOn is not { } overlay)
            return;
        _hoistedOn = null;
        overlay.Unhoist(this);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Drawn on the overlay layer, the sheet is placed where its layout arranged it, in the surface's coordinates: the layout's bounds
    /// in the surface (through its ancestors' offsets, transforms and scrolling) plus its own frame.
    /// </remarks>
    internal override void OnGetRenderProps(ref SkUiRenderProps props)
    {
        base.OnGetRenderProps(ref props);
        if (_hoistedOn is null || SkiaParent is not { } parent || SkUiDiagnostics.GetRootBounds(parent) is not { } bounds)
            return;
        props.X = (float)(bounds.X + Frame.X);
        props.Y = (float)(bounds.Y + Frame.Y);
    }

    #endregion

    #region Modal behaviour

    /// <summary>
    /// An open modal sheet is the modal pane of the surface it is in now: registered once it is laid out open (its tree is in place by
    /// then; XAML and bindings can open it before it is in a surface), and moved when it lands in another surface.
    /// </summary>
    private void EnsureModal()
    {
        if (!IsModal || _state == SheetState.Closed || SkUiDiagnostics.GetSurfaceRoot(this) is not { } root || ReferenceEquals(root, _modalRoot)
            || HiddenByAncestor)
            return;
        if (ReferenceEquals(root, this))
            return; // not placed yet
        if (_modalRoot is { } previous)
        {
            previous.RemoveModalPane(this);
            _modalRoot = null;
        }
        _modalRoot = root;
        if (root.FocusManagerIfCreated?.Focused is { } focused)
            _focusBefore = new WeakReference<ISkUiAccessibleNode>(focused);
        root.AddModalPane(this);
        _backHandler ??= SkUiSheetBackHandler.Register(this);
    }

    private void LeaveModal()
    {
        _backHandler?.Dispose();
        _backHandler = null;
        if (_modalRoot is not { } root)
            return;
        _modalRoot = null;
        root.RemoveModalPane(this);
        // Focus goes back where it was before the sheet took it, when that is still there.
        if (_focusBefore is { } before && before.TryGetTarget(out var node) && root.FocusManagerIfCreated is { } manager && manager.Focused is null)
            manager.Focus(node);
        _focusBefore = null;
    }

    /// <summary>A modal sheet that opened takes keyboard focus (its first focusable node) and the screen reader's (its title, else that node).</summary>
    private void MoveFocusIn()
    {
        if (_modalRoot is not { } root)
            return;
        var manager = root.FocusManagerIfCreated;
        // Focus moves in when something had it before the sheet opened (the page's focus left when the sheet became the modal pane) or
        // a screen reader runs; a touch user's sheet takes none, so no keyboard shows for a field in it.
        if (manager is not null && (manager.Focused is not null || _focusBefore is not null || SkUiScreenReader.IsRunning))
            manager.FocusFirst();
        if (PanelView.TitleLabel is { } title)
            SkUiSemantics.SetSemanticFocus(title);
    }

    private void OnIsModalChanged()
    {
        if (_state == SheetState.Closed)
            return;
        ScrimView.IsVisible = IsModal;
        if (IsModal)
            EnsureModal();
        else
            LeaveModal();
    }

    private void OnIsDragEnabledChanged()
    {
        TrackView.Controller.IsUserScrollEnabled = IsDragEnabled;
        if (!IsDragEnabled)
            TrackView.CancelGestures();
    }

    /// <inheritdoc />
    bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key)
    {
        if (key.Key != SkUiKey.Escape || key.HasCommandModifier || _state is SheetState.Closed or SheetState.Closing)
            return false;
        Dismiss();
        return true;
    }

    /// <summary>Back was pressed while this sheet is the topmost modal pane: it closes; returns whether it took the press.</summary>
    internal bool OnBackPressed()
    {
        if (_state is SheetState.Closed or SheetState.Closing || _modalRoot is not { } root || !ReferenceEquals(root.ModalPane, this))
            return false;
        Dismiss();
        return true;
    }

    /// <inheritdoc />
    /// <remarks>Runs when the sheet or an ancestor joins or leaves a tree.</remarks>
    protected override void OnAnimationRootChanged(bool subtreeDetached = false)
    {
        base.OnAnimationRootChanged(subtreeDetached);
        if (_state == SheetState.Closed)
            return;
        // Out of its surface while open (it or a layout around it was removed, or is being moved: MAUI moves a view by removing and
        // adding it): it lets go of the surface (the modal pane, Back) and stays open; back in a surface, it is that one's modal pane.
        if (subtreeDetached)
        {
            Unhoist();
            LeaveModal();
        }
        else
        {
            Hoist();
            EnsureModal();
        }
    }

    #endregion

    #region Tree

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren
    {
        get
        {
            yield return ScrimView;
            yield return TrackView;
        }
    }

    /// <inheritdoc />
    /// <remarks>The sheet itself takes no pointers: its scrim and its panel do; elsewhere they reach the page.</remarks>
    internal override bool HitsSelfAt(Point local) => false;

    /// <inheritdoc />
    /// <remarks>The sheet's background is its panel's: the full-size sheet draws nothing itself.</remarks>
    protected override void OnPaintBackground(SkiaSharp.SKCanvas canvas) { }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        switch (propertyName)
        {
            case nameof(Background):
            case nameof(BackgroundColor):
            case nameof(IsEnabled):
                PanelView.InvalidatePaint();
                break;
            case nameof(AutomationId):
                ScrimView.TakeAutomationId(AutomationId, ScrimAutomationIdSuffix);
                PanelView.Grabber.TakeAutomationId(AutomationId, GrabberAutomationIdSuffix);
                break;
        }
    }

    #endregion

    #region Showing a sheet over a surface

    /// <summary>
    /// Opens a sheet showing <paramref name="content"/> over the whole surface of <paramref name="anchor"/> (on its root's overlay layer,
    /// above everything else in it, other sheets opened before included) and completes with the value given to <see cref="Close"/>
    /// when that is a <typeparamref name="TResult"/>, or <c>default</c> when the sheet is dismissed or <paramref name="cancellationToken"/>
    /// closes it. <paramref name="configure"/> sets the sheet up before it opens (title, actions, detents, presentation); content that
    /// closes the sheet with a result keeps the sheet it is given there.
    /// </summary>
    /// <exception cref="InvalidOperationException"><paramref name="anchor"/> is not in a surface.</exception>
    public static async Task<TResult?> ShowAsync<TResult>(SkUiView anchor, View content, Action<SkUiSheet>? configure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(content);
        ThrowIfNotDrawn(content, nameof(content));
        var root = SkUiDiagnostics.GetSurfaceRoot(anchor) ?? throw new InvalidOperationException("The anchor is not in a drawn surface.");
        if (cancellationToken.IsCancellationRequested)
            return default;
        var sheet = new SkUiSheet { Content = content };
        configure?.Invoke(sheet);
        TResult? result = default;
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sheet.Closed += (_, args) =>
        {
            if (!args.IsDismissed && args.Result is TResult typed)
                result = typed;
            root.SurfaceOverlay.Remove(sheet);
            done.TrySetResult();
        };
        root.SurfaceOverlay.Add(sheet);
        var dispatcher = Microsoft.Maui.Dispatching.Dispatcher.GetForCurrentThread();
        await using var registration = cancellationToken.Register(() =>
        {
            if (dispatcher?.IsDispatchRequired == true)
                dispatcher.Dispatch(sheet.Dismiss);
            else
                sheet.Dismiss();
        });
        sheet.IsOpen = true;
        await done.Task.ConfigureAwait(true);
        return result;
    }

    #endregion
}

/// <summary>The physical edge a sheet comes from (the start and end edges resolved by the flow direction).</summary>
internal enum SkUiSheetEdgePhysical
{
    Bottom,
    Left,
    Right
}
