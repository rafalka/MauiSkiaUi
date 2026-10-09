using System.Windows.Input;
using MauiSkiaUi.Core;

namespace MauiSkiaUi;

/// <summary>A view with pull-to-refresh: <see cref="SkUiCollectionView"/>, <see cref="SkUiRefreshView"/>.</summary>
internal interface ISkUiRefreshOwner
{
    /// <summary>The owner's bindable <c>IsRefreshing</c>.</summary>
    bool IsRefreshing { get; set; }

    /// <summary>
    /// Whether a pull released past the trigger may start a refresh now: the owner is enabled, its pull is on and its command
    /// (if any) can execute (it then runs without being asked again).
    /// </summary>
    bool CanStartRefresh { get; }

    /// <summary>Runs when a refresh starts.</summary>
    ICommand? RefreshCommand { get; }

    /// <summary>The parameter of <see cref="RefreshCommand"/>.</summary>
    object? RefreshCommandParameter { get; }

    /// <summary>The owner's trigger distance; 0: the look's.</summary>
    double RefreshTriggerDistance { get; }

    /// <summary>The owner's style; <see cref="SkUiRefreshStyle.Default"/>: the look's.</summary>
    SkUiRefreshStyle RefreshStyle { get; }

    /// <summary>Whether mouse drags pull too.</summary>
    bool IsMousePullEnabled { get; }

    /// <summary>Who ends a refresh.</summary>
    SkUiRefreshCompletion RefreshCompletion { get; }

    /// <summary>Raises the owner's <c>Refreshing</c> event.</summary>
    void RaiseRefreshing(SkUiRefreshingEventArgs args);
}

/// <summary>
/// Pull-to-refresh shared by its owners (<see cref="ISkUiRefreshOwner"/>): the indicator (<see cref="Layer"/>, the public
/// <see cref="SkUiCoreRefreshIndicator"/>) placed for the owner's style, a pull released past the trigger distance starting
/// a refresh (mouse pulls only when the owner allows them), and <c>IsRefreshing</c> becoming <c>true</c> raising
/// <c>Refreshing</c> and running the command, as MAUI's <c>RefreshView</c>, then (with automatic completion) setting it back
/// when the command and every deferral are done. A list that refreshes by itself (<see cref="SkUiCollectionView"/>) is
/// pulled at its scroller's vertical start through <see cref="PullThrough"/>, which also holds the content down while an
/// inline refresh runs; the refresh view reports the pulls of the scrollers in its content and of itself
/// (<see cref="ShowPull"/>, <see cref="Release"/>) and holds what it pulled.
/// </summary>
internal sealed class SkUiPullToRefresh(ISkUiRefreshOwner owner)
{
    private SkUiScrollController? _scroller;
    private bool _pullEnabled;
    private RefreshRun? _run;
    private double _gap, _top;
    private SkUiPointerDevice _device;

    /// <summary>The indicator's layer; the owner attaches it over the pulled area.</summary>
    public SkUiRefreshLayer Layer { get; } = new();

    /// <summary>The indicator (the owner's public <c>RefreshIndicator</c>).</summary>
    public SkUiCoreRefreshIndicator Indicator => Layer.Indicator;

    /// <summary>The trigger distance in force: the owner's, or the look's.</summary>
    public double TriggerDistance => owner.RefreshTriggerDistance > 0 ? owner.RefreshTriggerDistance : Math.Max(1, SkUiLook.Current.RefreshTriggerDistance);

    /// <summary>How far below the top the indicator's area ends while refreshing (the look's).</summary>
    public static double RestDistance => Math.Max(0, SkUiLook.Current.RefreshRestDistance);

    /// <summary>The style in force: the owner's, or the look's.</summary>
    public SkUiRefreshStyle Style => SkUiCoreRefreshIndicator.Resolve(owner.RefreshStyle);

    /// <summary>Which drags pull a scroller for the owner.</summary>
    public SkUiPullInput PullInput => owner.IsMousePullEnabled ? SkUiPullInput.TouchAndMouse : SkUiPullInput.Touch;

    /// <summary>Whether a pull by <paramref name="device"/> counts (mouse pulls only when the owner allows them).</summary>
    public bool Counts(SkUiPointerDevice device) => device != SkUiPointerDevice.Mouse || owner.IsMousePullEnabled;

    /// <summary>Pulls the owner's own scroller at its vertical start while <see cref="PullEnabled"/>.</summary>
    public void PullThrough(SkUiScrollController scroller)
    {
        _scroller = scroller;
        scroller.PullReleased += OnScrollerPullReleased;
        scroller.OverscrollChanged += ShowScrollerPull;
    }

    /// <summary>Whether the scroller of <see cref="PullThrough"/> is pulled (the owner's pull-to-refresh is on, and it scrolls vertically).</summary>
    public bool PullEnabled
    {
        get => _pullEnabled;
        set
        {
            _pullEnabled = value;
            OnSettingsChanged();
        }
    }

    /// <summary>The owner's style, trigger distance or mouse setting changed (or the look's): the scroller and the indicator follow.</summary>
    public void OnSettingsChanged()
    {
        if (_scroller is { } scroller)
        {
            scroller.PullsAtVerticalStart = _pullEnabled ? PullInput : SkUiPullInput.None;
            if (owner.IsRefreshing)
                scroller.SetTopRest(Style == SkUiRefreshStyle.Inline ? RestDistance : 0);
            ShowScrollerPull();
        }
        else
        {
            ShowPull(_gap, _top, _device);
        }
    }

    /// <summary>A pull by <paramref name="device"/> released <paramref name="distance"/> DIPs past the top: past the trigger it starts a refresh, when the owner can.</summary>
    public void Release(double distance, SkUiPointerDevice device)
    {
        if (Counts(device) && distance >= TriggerDistance && !owner.IsRefreshing && owner.CanStartRefresh)
            owner.IsRefreshing = true;
    }

    /// <summary>
    /// Shows a pull of <paramref name="gap"/> DIPs past the top by <paramref name="device"/> (nothing for a mouse pull the
    /// owner does not allow), from <paramref name="top"/> in the layer (inline). While refreshing, an overlay badge stays at
    /// its place and an inline indicator follows the content.
    /// </summary>
    public void ShowPull(double gap, double top, SkUiPointerDevice device)
    {
        _gap = gap;
        _top = top;
        _device = device;
        var refreshing = owner.IsRefreshing;
        Layer.Show(refreshing || Counts(device) ? gap : 0, top, Style, TriggerDistance, RestDistance, refreshing);
    }

    /// <summary>
    /// The owner's <c>IsRefreshing</c> changed: <c>true</c> spins the indicator (an inline one holds the own scroller's content
    /// down), raises <c>Refreshing</c> and runs the command, and with automatic completion ends the refresh when its work is
    /// done; <c>false</c> stops it, lets the content back up and the indicator follow the pull again.
    /// </summary>
    public void OnIsRefreshingChanged(bool value)
    {
        if (!value)
        {
            _run?.Cancel();
            _run = null;
            if (_scroller is { } scroller)
            {
                scroller.SetTopRest(0);
                ShowScrollerPull();
            }
            else
            {
                ShowPull(_gap, _top, _device);
            }
            return;
        }
        if (_scroller is { } held && Style == SkUiRefreshStyle.Inline)
            held.SetTopRest(RestDistance);
        ShowPull(_gap, _top, _device);
        var run = owner.RefreshCompletion == SkUiRefreshCompletion.Automatic ? new RefreshRun(this) : null;
        _run = run;
        try
        {
            owner.RaiseRefreshing(new SkUiRefreshingEventArgs(run is null ? null : run.Defer));
            if (!owner.IsRefreshing || !ReferenceEquals(_run, run))
                return; // a handler ended it (the run was cancelled)
            // As MAUI's RefreshView: the command runs once the refresh started, not asked again (a pull starts one only while it
            // can execute, CanStartRefresh; a CanExecute such as "not refreshing" would refuse now).
            var parameter = owner.RefreshCommandParameter;
            if (owner.RefreshCommand is { } command)
            {
                command.Execute(parameter);
                // Busy right after it ran (an async command): done once it can execute again.
                if (run is not null && ReferenceEquals(_run, run) && !command.CanExecute(parameter))
                    run.WaitFor(command, parameter);
            }
        }
        finally
        {
            // Also when a handler or the command throws: an automatic refresh must still end.
            run?.Complete();
        }
    }

    private void OnRunDone(RefreshRun run)
    {
        if (!ReferenceEquals(_run, run))
            return;
        _run = null;
        owner.IsRefreshing = false;
    }



    private void OnScrollerPullReleased(SkUiScrollEdges edge, double distance)
    {
        if (_pullEnabled && edge == SkUiScrollEdges.Top && _scroller is { } scroller)
            Release(distance, scroller.DragDevice);
    }

    private void ShowScrollerPull()
    {
        if (_scroller is not { } scroller)
            return;
        var gap = _pullEnabled || owner.IsRefreshing ? -scroller.OverscrollY : 0;
        ShowPull(gap, 0, scroller.DragDevice);
    }

    /// <summary>
    /// One refresh with automatic completion: done when its start, the busy command (if any) and every deferral are done.
    /// Ends the refresh once, unless it was ended or replaced before.
    /// </summary>
    private sealed class RefreshRun(SkUiPullToRefresh refresh)
    {
        private readonly SkUiPullToRefresh _refresh = refresh;
        // The UI thread's (the run starts there): deferrals and commands may report from any thread.
        private readonly IDispatcher? _dispatcher = Microsoft.Maui.Dispatching.Dispatcher.GetForCurrentThread();
        private int _pending = 1; // the start itself, completed when the command ran
        private bool _done;
        private ICommand? _command;
        private object? _parameter;
        private SkUiWeakListener<RefreshRun>? _commandListener;

        public SkUiRefreshDeferral Defer()
        {
            if (_done)
                return SkUiRefreshDeferral.None;
            _pending++;
            return new SkUiRefreshDeferral(() => OnUiThread(Complete));
        }

        public void WaitFor(ICommand command, object? parameter)
        {
            _pending++;
            _command = command;
            _parameter = parameter;
            (_commandListener = new SkUiWeakListener<RefreshRun>(this, static (run, change) =>
            {
                if (change.Kind == SkUiChangeKind.CanExecute)
                    run.OnUiThread(run.OnCanExecuteChanged);
            })).Listen(command);
            // It may have become executable between the caller's check and the subscription (a fast async command), and
            // that notification is gone: ask again now that a later one would be heard.
            OnCanExecuteChanged();
        }

        /// <summary>Runs <paramref name="action"/> on the UI thread the run started on.</summary>
        private void OnUiThread(Action action)
        {
            if (_dispatcher is { IsDispatchRequired: true } dispatcher)
                dispatcher.Dispatch(action);
            else
                action();
        }

        private void OnCanExecuteChanged()
        {
            if (_command is not { } command || !command.CanExecute(_parameter))
                return;
            StopWaiting();
            Complete();
        }

        public void Complete()
        {
            if (_done || --_pending > 0)
                return;
            _done = true;
            StopWaiting();
            _refresh.OnRunDone(this);
        }

        public void Cancel()
        {
            _done = true;
            StopWaiting();
        }

        private void StopWaiting()
        {
            _commandListener?.Listen(null);
            _command = null;
        }
    }
}
