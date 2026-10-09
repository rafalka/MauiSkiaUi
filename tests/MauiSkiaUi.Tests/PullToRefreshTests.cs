using System.Reflection;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// C2 pull-to-refresh shared by <see cref="SkUiRefreshView"/> and <see cref="SkUiCollectionView"/>: the public
/// <see cref="SkUiCoreRefreshIndicator"/>, the inline (iOS) style holding the pulled content down, the trigger distance,
/// mouse pulls, and automatic completion (commands busy while they run, deferrals).
/// </summary>
public class PullToRefreshTests
{
    private static long _pointer = 190_000;

    private static double Rest => SkUiLook.Current.RefreshRestDistance;
    private static double IndicatorSize => SkUiLook.Current.RefreshIndicatorSize;

    private sealed class Command(Action<object?> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute(parameter);
    }

    /// <summary>A command that can execute while <paramref name="canExecute"/> says so.</summary>
    private sealed class GuardedCommand(Func<bool> canExecute, Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => canExecute();
        public void Execute(object? parameter) => execute();
    }

    /// <summary>A command that cannot execute while it runs (as the MVVM Toolkit's <c>AsyncRelayCommand</c>), until <see cref="Finish"/>.</summary>
    private sealed class BusyCommand : ICommand
    {
        private bool _running;

        public int Runs { get; private set; }
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => !_running;

        public void Execute(object? parameter)
        {
            Runs++;
            _running = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Finish()
        {
            _running = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class Model : INotifyPropertyChanged
    {
        private bool _isRefreshing;

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set
            {
                _isRefreshing = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private static SkUiScrollView Scroller(double contentHeight, SkUiOverscrollMode overscroll = SkUiOverscrollMode.None) => new()
    {
        Content = new SkUiBox { HeightRequest = contentHeight },
        Overscroll = overscroll,
        VerticalScrollBarVisibility = ScrollBarVisibility.Never
    };

    private static SkUiCollectionView List(SkUiOverscrollMode overscroll = SkUiOverscrollMode.None) => new()
    {
        ItemsSource = new ObservableCollection<int>([1, 2, 3]),
        ItemTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = 50 }),
        Overscroll = overscroll,
        IsPullToRefreshEnabled = true
    };

    private static void Drag(SkUiView root, Point from, Point to, bool release = true, int steps = 6,
        SkUiPointerDevice device = SkUiPointerDevice.Touch)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, from, TimeSpan.FromMilliseconds(0), Device: device));
        for (var step = 1; step <= steps; step++)
        {
            var point = new Point(from.X + (to.X - from.X) * step / steps, from.Y + (to.Y - from.Y) * step / steps);
            root.Touch(new(id, SkUiTouchAction.Moved, point, TimeSpan.FromMilliseconds(step * 100), Device: device));
        }
        if (release)
            root.Touch(new(id, SkUiTouchAction.Released, to, TimeSpan.FromMilliseconds(steps * 100 + 300), Device: device));
    }

    private static void Frames(SkUiTestSurface surface, int count = 60)
    {
        for (var frame = 1; frame <= count; frame++)
            surface.Frame(frame * 16);
    }

    private static SkUiScrollController ControllerOf(ISkUiScrollHost host) => host.Scroller;

    #region Indicator

    [Fact]
    public void BothControlsShowTheSamePublicIndicator()
    {
        var refresh = new SkUiRefreshView { RefreshColor = Colors.Teal };
        var list = List();
        list.RefreshColor = Colors.Orange;
        Assert.IsType<SkUiCoreRefreshIndicator>(refresh.RefreshIndicator);
        Assert.Equal(Colors.Teal, refresh.RefreshIndicator.Color);
        Assert.Equal(Colors.Orange, list.RefreshIndicator.Color);
        Assert.Equal(SkUiRefreshStyle.Default, refresh.RefreshStyle);
        Assert.Equal(0, refresh.RefreshTriggerDistance);
        Assert.False(refresh.IsMousePullEnabled);
        Assert.Equal(SkUiRefreshCompletion.Manual, refresh.RefreshCompletion);
        Assert.Equal(SkUiRefreshCompletion.Manual, list.RefreshCompletion);
    }

    [Fact]
    public void TheCoreIndicatorWorksOnItsOwnWithoutRecordingWhilePulled()
    {
        var indicator = new SkUiCoreRefreshIndicator();
        var host = new SkUiCoreHost().SetContent(indicator);
        using var surface = new SkUiTestSurface(host, 100, 100);
        Assert.Equal(new Size(IndicatorSize, IndicatorSize), indicator.Measure(100, 100));
        indicator.SetPullProgress(0.5);
        surface.Frame(0);
        Assert.Equal(0.5, indicator.ShownOpacity, 3);
        var recorded = surface.RecordedPictures;

        indicator.SetPullProgress(0.8);
        surface.Frame(1);
        Assert.Equal(0.8, indicator.ShownOpacity, 3);
        Assert.Equal(recorded, surface.RecordedPictures);

        indicator.SetIsRefreshing(true);
        surface.Frame(2);
        Assert.Equal(1, indicator.ShownOpacity);
        Assert.Equal(recorded + 1, surface.RecordedPictures);
        Assert.Throws<ArgumentOutOfRangeException>(() => indicator.SetPullProgress(-1));
    }

    #endregion

    #region Inline style

    [Fact]
    public void InlineHoldsAPulledScrollersContentDownWhileRefreshing()
    {
        var scroller = Scroller(100, SkUiOverscrollMode.Bounce);
        var refresh = new SkUiRefreshView { Content = scroller, RefreshStyle = SkUiRefreshStyle.Inline };
        using var surface = new SkUiTestSurface(refresh, 300, 400);
        surface.Frame(0);
        var top = SkUiDiagnostics.GetRootBounds(scroller.Content!)!.Value.Top;

        Drag(refresh, new Point(150, 20), new Point(150, 380));
        Assert.True(refresh.IsRefreshing);
        Frames(surface);
        Assert.Equal(-Rest, ControllerOf(scroller).OverscrollY, 1);
        Assert.Equal(top + Rest, SkUiDiagnostics.GetRootBounds(scroller.Content!)!.Value.Top, 1);
        // Centered in the room it holds, fully shown.
        Assert.Equal((Rest - IndicatorSize) / 2, refresh.RefreshIndicator.TranslationY, 1);
        Assert.Equal(1, refresh.RefreshIndicator.ShownOpacity);

        refresh.IsRefreshing = false;
        Frames(surface);
        Assert.Equal(0, ControllerOf(scroller).OverscrollY);
        Assert.Equal(0, refresh.RefreshIndicator.ShownOpacity);
    }

    [Fact]
    public void InlineMovesContentThatDoesNotScrollAndHoldsIt()
    {
        var box = new SkUiBox { HeightRequest = 100 };
        var refresh = new SkUiRefreshView { Content = box, RefreshStyle = SkUiRefreshStyle.Inline };
        SkUiTestHelpers.Arrange(refresh, 300, 400);
        var part = (View)box.Parent;

        Drag(refresh, new Point(150, 20), new Point(150, 120), release: false); // short of the trigger
        Assert.True(part.TranslationY > 0); // the content follows the pull
        refresh.Touch(new(_pointer, SkUiTouchAction.Released, new Point(150, 120), TimeSpan.FromSeconds(1)));
        Assert.False(refresh.IsRefreshing);
        Assert.Equal(0, part.TranslationY); // no clock: it is back at once

        Drag(refresh, new Point(150, 20), new Point(150, 380));
        Assert.True(refresh.IsRefreshing);
        Assert.Equal(Rest, part.TranslationY);
        refresh.IsRefreshing = false;
        Assert.Equal(0, part.TranslationY);
    }

    [Fact]
    public void AnInlineCollectionViewHoldsItsItemsDown()
    {
        var list = List(SkUiOverscrollMode.Bounce);
        list.RefreshStyle = SkUiRefreshStyle.Inline;
        using var surface = new SkUiTestSurface(list, 300, 400);
        surface.Frame(0);

        Drag(list, new Point(150, 20), new Point(150, 380));
        Assert.True(list.IsRefreshing);
        Frames(surface);
        Assert.Equal(-Rest, ControllerOf(list.ScrollView).OverscrollY, 1);

        // Started by the app, the items come down too.
        list.IsRefreshing = false;
        Frames(surface);
        Assert.Equal(0, ControllerOf(list.ScrollView).OverscrollY);
        list.IsRefreshing = true;
        Frames(surface);
        Assert.Equal(-Rest, ControllerOf(list.ScrollView).OverscrollY, 1);
    }

    [Fact]
    public void AnOverlayBadgeLeavesTheContentInPlace()
    {
        var scroller = Scroller(100, SkUiOverscrollMode.Bounce);
        var refresh = new SkUiRefreshView { Content = scroller, RefreshStyle = SkUiRefreshStyle.Overlay };
        using var surface = new SkUiTestSurface(refresh, 300, 400);
        surface.Frame(0);
        Drag(refresh, new Point(150, 20), new Point(150, 380));
        Frames(surface);
        Assert.True(refresh.IsRefreshing);
        Assert.Equal(0, ControllerOf(scroller).OverscrollY);
        Assert.Equal(Rest - IndicatorSize, refresh.RefreshIndicator.TranslationY);
        Assert.Equal(SkUiRefreshStyle.Overlay, refresh.RefreshIndicator.EffectiveStyle);
    }

    [Fact]
    public void ReplacingTheContentDuringAnInlineRefreshLetsTheOldScrollerGoAndHoldsTheNewContent()
    {
        var scroller = Scroller(100, SkUiOverscrollMode.Bounce);
        var refresh = new SkUiRefreshView { Content = scroller, RefreshStyle = SkUiRefreshStyle.Inline };
        using var surface = new SkUiTestSurface(refresh, 300, 400);
        surface.Frame(0);
        Drag(refresh, new Point(150, 20), new Point(150, 380));
        Assert.True(refresh.IsRefreshing);
        Assert.Equal(Rest, ControllerOf(scroller).TopRest);

        var box = new SkUiBox { HeightRequest = 100 };
        refresh.Content = box;
        Assert.Equal(0, ControllerOf(scroller).TopRest); // the old scroller rests at its edge again
        void Tick(int from)
        {
            // The view's own hold animates on the UI clock.
            for (var frame = from; frame < from + 60; frame++)
                refresh.AnimationClock.Tick(TimeSpan.FromMilliseconds(frame * 16));
        }
        Tick(1);
        Assert.Equal(Rest, ((View)box.Parent).TranslationY, 1); // the view holds the new content down
        refresh.IsRefreshing = false;
        Tick(61);
        Assert.Equal(0, ((View)box.Parent).TranslationY, 1);
    }

    #endregion

    #region Trigger distance and mouse

    [Fact]
    public void TheTriggerDistanceIsAdjustable()
    {
        var refresh = new SkUiRefreshView { Content = Scroller(100), RefreshTriggerDistance = 150 };
        SkUiTestHelpers.Arrange(refresh, 300, 800);

        // About 97 DIPs shown: past the look's 64, short of 150.
        Drag(refresh, new Point(150, 20), new Point(150, 220), release: false);
        var shown = -ControllerOf((SkUiScrollView)refresh.Content!).OverscrollY;
        Assert.InRange(shown, 70, 140);
        Assert.Equal(shown / 150, refresh.RefreshIndicator.PullProgress, 3);
        refresh.Touch(new(_pointer, SkUiTouchAction.Released, new Point(150, 220), TimeSpan.FromSeconds(1)));
        Assert.False(refresh.IsRefreshing);

        ((SkUiScrollView)refresh.Content!).ScrollTo(0, 0);
        Drag(refresh, new Point(150, 20), new Point(150, 720));
        Assert.True(refresh.IsRefreshing);

        var list = List();
        list.RefreshTriggerDistance = 150;
        SkUiTestHelpers.Arrange(list, 300, 800);
        Drag(list, new Point(150, 20), new Point(150, 220));
        Assert.False(list.IsRefreshing);
    }

    [Fact]
    public void MouseDragsPullOnlyWhenEnabled()
    {
        var scroller = Scroller(100);
        var refresh = new SkUiRefreshView { Content = scroller };
        SkUiTestHelpers.Arrange(refresh, 300, 400);

        Drag(refresh, new Point(150, 20), new Point(150, 380), device: SkUiPointerDevice.Mouse);
        Assert.False(refresh.IsRefreshing);
        Assert.Equal(0, ControllerOf(scroller).OverscrollY); // not even pulled
        Assert.Equal(0, refresh.RefreshIndicator.ShownOpacity);

        Drag(refresh, new Point(150, 20), new Point(150, 380), device: SkUiPointerDevice.Pen);
        Assert.True(refresh.IsRefreshing);
        refresh.IsRefreshing = false;
        scroller.ScrollTo(0, 0);

        refresh.IsMousePullEnabled = true;
        Assert.Equal(SkUiPullInput.TouchAndMouse, ControllerOf(scroller).PullsForRefreshView);
        Drag(refresh, new Point(150, 20), new Point(150, 380), device: SkUiPointerDevice.Mouse);
        Assert.True(refresh.IsRefreshing);
    }

    [Fact]
    public void MouseDragsDoNotPullTheViewItselfOrShowABounceUnlessEnabled()
    {
        var own = new SkUiRefreshView { Content = new SkUiBox { HeightRequest = 100 } };
        SkUiTestHelpers.Arrange(own, 300, 400);
        Drag(own, new Point(150, 20), new Point(150, 380), device: SkUiPointerDevice.Mouse);
        Assert.False(own.IsRefreshing);
        own.IsMousePullEnabled = true;
        Drag(own, new Point(150, 20), new Point(150, 380), device: SkUiPointerDevice.Mouse);
        Assert.True(own.IsRefreshing);

        // A bouncing scroller still bounces for the mouse; the indicator does not follow and nothing refreshes.
        var scroller = Scroller(1000, SkUiOverscrollMode.Bounce);
        var bouncing = new SkUiRefreshView { Content = scroller };
        SkUiTestHelpers.Arrange(bouncing, 300, 400);
        Drag(bouncing, new Point(150, 20), new Point(150, 380), release: false, device: SkUiPointerDevice.Mouse);
        Assert.True(ControllerOf(scroller).OverscrollY < 0);
        Assert.Equal(0, bouncing.RefreshIndicator.ShownOpacity);
        bouncing.Touch(new(_pointer, SkUiTouchAction.Released, new Point(150, 380), TimeSpan.FromSeconds(1), Device: SkUiPointerDevice.Mouse));
        Assert.False(bouncing.IsRefreshing);

        var list = List();
        SkUiTestHelpers.Arrange(list, 300, 400);
        Drag(list, new Point(150, 20), new Point(150, 380), device: SkUiPointerDevice.Mouse);
        Assert.False(list.IsRefreshing);
        list.IsMousePullEnabled = true;
        Drag(list, new Point(150, 20), new Point(150, 380), device: SkUiPointerDevice.Mouse);
        Assert.True(list.IsRefreshing);
    }

    #endregion

    #region Completion

    [Fact]
    public void TheCommandRunsOnceTheRefreshStartedWithoutBeingAskedAgain()
    {
        // As MAUI's RefreshView: a command guarded by "not refreshing" still runs (refreshing has started when it runs).
        var ran = 0;
        var refresh = new SkUiRefreshView { Content = Scroller(100) };
        refresh.Command = new GuardedCommand(() => !refresh.IsRefreshing, () => ran++);
        SkUiTestHelpers.Arrange(refresh, 300, 400);
        Drag(refresh, new Point(150, 20), new Point(150, 380));
        Assert.True(refresh.IsRefreshing);
        Assert.Equal(1, ran);

        var list = List();
        var listRan = 0;
        var canExecute = false;
        list.RefreshCommand = new GuardedCommand(() => canExecute && !list.IsRefreshing, () => listRan++);
        SkUiTestHelpers.Arrange(list, 300, 400);
        Drag(list, new Point(150, 20), new Point(150, 380)); // a pull starts a refresh only while the command can execute
        Assert.False(list.IsRefreshing);
        canExecute = true;
        Drag(list, new Point(150, 20), new Point(150, 380));
        Assert.True(list.IsRefreshing);
        Assert.Equal(1, listRan);
    }

    [Fact]
    public void AnAutomaticRefreshRunEndsWhenAHandlerOrTheCommandThrows()
    {
        // The run (its deferrals, the command it waits for) ends instead of staying alive. IsRefreshing itself stays true: an
        // exception out of a property's change callback leaves MAUI's BindableObject unable to set that property again (as
        // MAUI's RefreshView, which raises Refreshing and runs its command there too).
        static object? Run(object owner) =>
            typeof(SkUiPullToRefresh).GetField("_run", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(
                owner.GetType().GetField("_refresh", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner));
        var refresh = new SkUiRefreshView { Content = Scroller(100), RefreshCompletion = SkUiRefreshCompletion.Automatic };
        refresh.Refreshing += (_, _) => throw new InvalidOperationException("handler");
        Assert.Throws<InvalidOperationException>(() => refresh.IsRefreshing = true);
        Assert.Null(Run(refresh));

        var list = List();
        list.RefreshCompletion = SkUiRefreshCompletion.Automatic;
        list.RefreshCommand = new Command(_ => throw new InvalidOperationException("command"));
        Assert.Throws<InvalidOperationException>(() => list.IsRefreshing = true);
        Assert.Null(Run(list));
    }

    [Fact]
    public void AutomaticCompletionEndsWithACommandThatIsDoneAtOnce()
    {
        var ran = 0;
        var refreshing = 0;
        var refresh = new SkUiRefreshView
        {
            Content = Scroller(100),
            Command = new Command(_ => ran++),
            RefreshCompletion = SkUiRefreshCompletion.Automatic
        };
        refresh.Refreshing += (_, _) => refreshing++;
        SkUiTestHelpers.Arrange(refresh, 300, 400);

        Drag(refresh, new Point(150, 20), new Point(150, 380));
        Assert.Equal((1, 1), (ran, refreshing));
        Assert.False(refresh.IsRefreshing);
    }

    [Fact]
    public void AutomaticCompletionWaitsForABusyCommandAndUpdatesTheBinding()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var command = new BusyCommand();
        var model = new Model();
        var refresh = new SkUiRefreshView { Content = Scroller(100), Command = command, RefreshCompletion = SkUiRefreshCompletion.Automatic, BindingContext = model };
        refresh.SetBinding(SkUiRefreshView.IsRefreshingProperty, nameof(Model.IsRefreshing));
        SkUiTestHelpers.Arrange(refresh, 300, 400);

        Drag(refresh, new Point(150, 20), new Point(150, 380));
        Assert.True(model.IsRefreshing);
        Assert.Equal(1, command.Runs);
        command.Finish();
        Assert.False(refresh.IsRefreshing);
        Assert.False(model.IsRefreshing);

        // Manual (MAUI's rule): the app ends it.
        refresh.RefreshCompletion = SkUiRefreshCompletion.Manual;
        ((SkUiScrollView)refresh.Content!).ScrollTo(0, 0);
        Drag(refresh, new Point(150, 20), new Point(150, 380));
        command.Finish();
        Assert.True(refresh.IsRefreshing);
    }

    [Fact]
    public void DeferralsKeepAnAutomaticRefreshRunning()
    {
        var list = List();
        list.RefreshCompletion = SkUiRefreshCompletion.Automatic;
        var deferrals = new List<SkUiRefreshDeferral>();
        list.Refreshing += (_, e) =>
        {
            deferrals.Add(e.GetDeferral());
            deferrals.Add(e.GetDeferral());
        };

        list.IsRefreshing = true;
        deferrals[0].Complete();
        deferrals[0].Complete(); // once only
        Assert.True(list.IsRefreshing);
        deferrals[1].Dispose();
        Assert.False(list.IsRefreshing);

        // Ended by the app first: a late deferral does not end the next refresh.
        list.IsRefreshing = true;
        var late = deferrals[2];
        list.IsRefreshing = false;
        list.IsRefreshing = true;
        late.Complete();
        Assert.True(list.IsRefreshing);

        // Manual: deferrals change nothing.
        list.IsRefreshing = false;
        list.RefreshCompletion = SkUiRefreshCompletion.Manual;
        list.IsRefreshing = true;
        deferrals[^1].Complete();
        deferrals[^2].Complete();
        Assert.True(list.IsRefreshing);
    }

    [Fact]
    public void MauiStyleRefreshingHandlersStillAttach()
    {
        var calls = 0;
        void OnRefreshing(object? sender, EventArgs e) => calls++;
        var refresh = new SkUiRefreshView();
        refresh.Refreshing += OnRefreshing;
        refresh.IsRefreshing = true;
        Assert.Equal(1, calls);
    }

    #endregion
}

/// <summary>Pull-to-refresh drawn by custom looks (global look state).</summary>
[Collection(GlobalStateCollection.Name)]
public class PullToRefreshLookTests
{
    private sealed class SmallIndicatorLook : DefaultSkUiLook
    {
        public bool DrawsProgress { get; init; }
        public override double RefreshIndicatorSize => 30;
        public override double RefreshRestDistance => 80;
        public override bool RefreshIndicatorDrawsPullProgress => DrawsProgress;
    }

    private static T WithLook<T>(SkUiLook look, Func<T> test)
    {
        var previous = SkUiLook.Current;
        SkUiLook.Current = look;
        try
        {
            return test();
        }
        finally
        {
            SkUiLook.Current = previous;
        }
    }

    [Fact]
    public void TheLookDrawsTheIndicatorWithTheActivityIndicatorByDefault()
    {
        var look = new DefaultSkUiLook();
        var arcs = 0;
        look.ActivityIndicatorPainter = (_, _, _, _, _) => arcs++;
        WithLook(look, () =>
        {
            foreach (var style in new[] { SkUiRefreshStyle.Overlay, SkUiRefreshStyle.Inline })
            {
                var refresh = new SkUiRefreshView { Content = new SkUiBox(), RefreshStyle = style };
                using var surface = new SkUiTestSurface(refresh, 200, 200);
                refresh.IsRefreshing = true;
                surface.Frame(0);
            }
            return 0;
        });
        Assert.Equal(2, arcs);
    }

    [Fact]
    public void ACustomLookPaintsTheIndicatorWithItsSizesAndStyle()
    {
        var look = new SmallIndicatorLook { RefreshStyle = SkUiRefreshStyle.Inline };
        var painted = new List<SkUiRefreshIndicatorPaint>();
        look.RefreshIndicatorPainter = (_, paint) => painted.Add(paint);
        var refresh = WithLook(look, () =>
        {
            var view = new SkUiRefreshView { Content = new SkUiBox(), RefreshColor = Colors.Teal };
            using var surface = new SkUiTestSurface(view, 200, 200);
            view.IsRefreshing = true;
            surface.Frame(0);
            Assert.Equal(SkUiRefreshStyle.Inline, view.RefreshIndicator.EffectiveStyle);
            Assert.Equal((80 - 30) / 2d, view.RefreshIndicator.TranslationY); // centered in the rest the look gives
            return view;
        });
        var paint = Assert.Single(painted);
        Assert.Equal(new SkiaSharp.SKRect(0, 0, 30, 30), paint.Bounds);
        Assert.True(paint.IsRefreshing);
        Assert.Equal(SkUiRefreshStyle.Inline, paint.Style);
        Assert.Equal(SkUiToggleDrawing.ToSkColor(Colors.Teal), paint.Color);
        Assert.NotNull(refresh);
    }

    [Fact]
    public void ALookCanDrawThePullItself()
    {
        var look = new SmallIndicatorLook { DrawsProgress = true, RefreshStyle = SkUiRefreshStyle.Overlay };
        var progress = new List<double>();
        look.RefreshIndicatorPainter = (_, paint) => progress.Add(paint.PullProgress);
        WithLook(look, () =>
        {
            var indicator = new SkUiCoreRefreshIndicator();
            using var surface = new SkUiTestSurface(new SkUiCoreHost().SetContent(indicator), 100, 100);
            indicator.SetPullProgress(0.25);
            surface.Frame(0);
            indicator.SetPullProgress(0.75);
            surface.Frame(1);
            return 0;
        });
        Assert.Equal([0.25, 0.75], progress);
    }

    [Fact]
    public void TheDefaultLookFollowsThePlatformAndCanBeSet()
    {
        Assert.Equal(OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst() ? SkUiRefreshStyle.Inline : SkUiRefreshStyle.Overlay,
            DefaultSkUiLook.PlatformRefreshStyle);
        var look = new DefaultSkUiLook { RefreshStyle = SkUiRefreshStyle.Inline };
        Assert.Equal(SkUiRefreshStyle.Inline, look.DefaultRefreshStyle);
        Assert.NotNull(look.GetRefreshIndicatorShadow(SkUiRefreshStyle.Overlay));
        Assert.Null(look.GetRefreshIndicatorShadow(SkUiRefreshStyle.Inline));
        var style = WithLook(look, () => new SkUiCoreRefreshIndicator().EffectiveStyle);
        Assert.Equal(SkUiRefreshStyle.Inline, style);
    }
}
