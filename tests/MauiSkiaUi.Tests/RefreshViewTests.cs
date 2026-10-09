using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// C2 <see cref="SkUiRefreshView"/>: MAUI's <c>RefreshView</c> API around drawn content. Pulls through the content's
/// scrollers (short content, overscroll off, a drag that first scrolls to the top, nested and horizontal scrollers, a
/// collection view, Core scrollers), pulls of the view itself (content that does not scroll, a header over a list), the
/// indicator, MAUI's rules for <c>IsRefreshEnabled</c>, <c>IsEnabled</c> and commands that cannot execute, taps in the
/// content, replaced content and nested refresh views.
/// </summary>
public class RefreshViewTests
{
    private static long _pointer = 160_000;

    private sealed class Command(Action<object?> execute, Func<bool>? canExecute = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => execute(parameter);
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private static SkUiBox Box(double height) => new() { HeightRequest = height };

    private static SkUiScrollView Scroller(double contentHeight) => new()
    {
        Content = Box(contentHeight),
        Overscroll = SkUiOverscrollMode.None,
        VerticalScrollBarVisibility = ScrollBarVisibility.Never
    };

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    /// <summary>A slow drag (100 ms per step: no fling) from <paramref name="from"/> to <paramref name="to"/>.</summary>
    private static void Drag(SkUiView root, Point from, Point to, bool release = true, int steps = 6)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, from, TimeSpan.FromMilliseconds(0)));
        for (var step = 1; step <= steps; step++)
        {
            var point = new Point(from.X + (to.X - from.X) * step / steps, from.Y + (to.Y - from.Y) * step / steps);
            root.Touch(new(id, SkUiTouchAction.Moved, point, TimeSpan.FromMilliseconds(step * 100)));
        }
        if (release)
            Release(root, to, steps * 100 + 300);
    }

    private static void Release(SkUiView root, Point at, double ms = 1000) =>
        root.Touch(new(_pointer, SkUiTouchAction.Released, at, TimeSpan.FromMilliseconds(ms)));

    private static SkUiRefreshView Refresh(ISkUiView content, List<string>? log = null)
    {
        var refresh = new SkUiRefreshView { Content = content };
        if (log is not null)
        {
            refresh.Refreshing += (_, _) => log.Add("refreshing");
            refresh.Command = new Command(parameter => log.Add($"command {parameter}"));
        }
        return refresh;
    }

    private static SkUiScrollController ControllerOf(ISkUiScrollHost host) => host.Scroller;

    #region API

    [Fact]
    public void DefaultsMatchMaui()
    {
        var refresh = new SkUiRefreshView();
        var maui = new RefreshView();
        Assert.Equal(maui.IsRefreshing, refresh.IsRefreshing);
        Assert.Equal(maui.IsRefreshEnabled, refresh.IsRefreshEnabled);
        Assert.Equal(maui.RefreshColor, refresh.RefreshColor);
        Assert.Null(refresh.Command);
        Assert.Null(refresh.CommandParameter);
        Assert.True(refresh.ClipToBounds);
        Assert.Equal(BindingMode.TwoWay, SkUiRefreshView.IsRefreshingProperty.DefaultBindingMode);
        Assert.Equal(0, refresh.RefreshIndicator.ShownOpacity);
    }

    [Fact]
    public void SettingIsRefreshingRaisesRefreshingThenRunsTheCommandAsMauis()
    {
        var log = new List<string>();
        var refresh = Refresh(Box(100)).SetCommand(new Command(parameter => log.Add($"command {parameter}")), "p");
        refresh.Refreshing += (_, _) => log.Add("refreshing");
        SkUiTestHelpers.Arrange(refresh, 300, 400);

        refresh.IsRefreshing = true;
        Assert.Equal(["refreshing", "command p"], log);
        Assert.Equal(1, refresh.RefreshIndicator.ShownOpacity);
        Assert.Equal(SkUiLook.Current.RefreshRestDistance - SkUiLook.Current.RefreshIndicatorSize, refresh.RefreshIndicator.TranslationY);
        Assert.True(refresh.RefreshIndicator.IsRefreshing);

        refresh.IsRefreshing = false;
        Assert.Equal(0, refresh.RefreshIndicator.ShownOpacity);
        Assert.False(refresh.RefreshIndicator.IsRefreshing);
        Assert.Equal(2, log.Count);
    }

    [Fact]
    public void IsRefreshEnabledTurnsThePullOffAndEndsARefresh()
    {
        var log = new List<string>();
        var scroller = Scroller(1000);
        var refresh = Refresh(scroller, log);
        refresh.IsRefreshEnabled = false;
        SkUiTestHelpers.Arrange(refresh, 300, 400);

        // Off: no pull, and the app cannot start a refresh; the content still scrolls.
        Drag(refresh, new Point(150, 50), new Point(150, 350));
        Assert.False(refresh.IsRefreshing);
        Assert.Equal(0, refresh.RefreshIndicator.ShownOpacity);
        refresh.IsRefreshing = true;
        Assert.False(refresh.IsRefreshing);
        Drag(refresh, new Point(150, 350), new Point(150, 50));
        Assert.True(scroller.ScrollY > 0);
        Assert.Empty(log);

        // On again: a refresh runs until the pull is turned off.
        refresh.IsRefreshEnabled = true;
        refresh.IsRefreshing = true;
        Assert.True(refresh.IsRefreshing);
        refresh.IsRefreshEnabled = false;
        Assert.False(refresh.IsRefreshing);
        Assert.Equal(0, refresh.RefreshIndicator.ShownOpacity);
    }

    [Fact]
    public void DisablingTheViewEndsARefreshAndStopsThePull()
    {
        var refresh = Refresh(Scroller(100));
        SkUiTestHelpers.Arrange(refresh, 300, 400);
        refresh.IsRefreshing = true;
        refresh.IsEnabled = false;
        Assert.False(refresh.IsRefreshing);
        refresh.IsRefreshing = true;
        Assert.False(refresh.IsRefreshing);
        Drag(refresh, new Point(150, 50), new Point(150, 350));
        Assert.False(refresh.IsRefreshing);

        refresh.IsEnabled = true;
        Drag(refresh, new Point(150, 50), new Point(150, 350));
        Assert.True(refresh.IsRefreshing);
    }

    [Fact]
    public void ACommandThatCannotExecuteTurnsThePullOffButNotARunningRefresh()
    {
        var canExecute = false;
        var ran = 0;
        var command = new Command(_ => ran++, () => canExecute);
        var refresh = Refresh(Scroller(100)).SetCommand(command);
        SkUiTestHelpers.Arrange(refresh, 300, 400);

        Drag(refresh, new Point(150, 50), new Point(150, 350));
        Assert.False(refresh.IsRefreshing);
        refresh.IsRefreshing = true;
        Assert.False(refresh.IsRefreshing);
        Assert.True(refresh.IsRefreshEnabled); // MAUI reports false here; SkiaUi keeps the value set

        canExecute = true;
        command.RaiseCanExecuteChanged();
        Drag(refresh, new Point(150, 50), new Point(150, 350));
        Assert.True(refresh.IsRefreshing);
        Assert.Equal(1, ran);

        // The command cannot execute while it runs (a typical async command): the refresh goes on.
        canExecute = false;
        command.RaiseCanExecuteChanged();
        Assert.True(refresh.IsRefreshing);
        Assert.Equal(1, refresh.RefreshIndicator.ShownOpacity);
    }

    #endregion

    #region Pulling through scrollers

    [Fact]
    public void PullingTheTopOfShortContentPastTheTriggerRefreshes()
    {
        var log = new List<string>();
        var scroller = Scroller(100); // does not overflow, and overscroll is off
        var refresh = Refresh(scroller, log);
        refresh.CommandParameter = "p";
        using var surface = new SkUiTestSurface(refresh, 300, 400);
        surface.Frame(0);

        // A short pull shows the indicator coming down; released before the trigger, it does nothing.
        Drag(refresh, new Point(150, 50), new Point(150, 110), release: false);
        Assert.Same(scroller, refresh.PulledScroller);
        Assert.InRange(refresh.RefreshIndicator.ShownOpacity, 0.05, 0.99);
        Assert.True(refresh.RefreshIndicator.TranslationY < 0);
        Release(refresh, new Point(150, 110));
        Assert.False(refresh.IsRefreshing);
        Assert.Empty(log);

        Drag(refresh, new Point(150, 50), new Point(150, 350));
        Assert.True(refresh.IsRefreshing);
        Assert.Equal(["refreshing", "command p"], log);
        Assert.Equal(1, refresh.RefreshIndicator.ShownOpacity);

        // A pull while refreshing does not start another refresh.
        Drag(refresh, new Point(150, 50), new Point(150, 350));
        Assert.Equal(2, log.Count);
        for (var frame = 1; frame <= 60; frame++)
            surface.Frame(frame * 16);
        Assert.Equal(1, refresh.RefreshIndicator.ShownOpacity);
        refresh.IsRefreshing = false;
        Assert.Equal(0, refresh.RefreshIndicator.ShownOpacity);
        Assert.Equal(0, scroller.ScrollY);
    }

    [Fact]
    public void ADragThatScrollsToTheTopPullsOnInTheSameDrag()
    {
        var scroller = Scroller(1000);
        var refresh = Refresh(scroller);
        SkUiTestHelpers.Arrange(refresh, 300, 400);
        _ = scroller.ScrollToAsync(0, 100, animated: false);

        Drag(refresh, new Point(150, 20), new Point(150, 380), release: false);
        Assert.Equal(0, scroller.ScrollY);
        Assert.True(-ControllerOf(scroller).OverscrollY >= SkUiLook.Current.RefreshTriggerDistance);
        Release(refresh, new Point(150, 380));
        Assert.True(refresh.IsRefreshing);
    }

    [Fact]
    public void ADownwardDragOfAScrolledDownScrollerOnlyScrolls()
    {
        var scroller = Scroller(1000);
        var refresh = Refresh(scroller);
        SkUiTestHelpers.Arrange(refresh, 300, 400);
        _ = scroller.ScrollToAsync(0, 300, animated: false);

        Drag(refresh, new Point(150, 50), new Point(150, 250));
        Assert.Equal(100, scroller.ScrollY);
        Assert.False(refresh.IsRefreshing);
        Assert.Equal(0, refresh.RefreshIndicator.ShownOpacity);
    }

    [Fact]
    public void BouncingScrollersPullTooAndUpwardOrSidewaysDragsNever()
    {
        var scroller = Scroller(1000);
        scroller.Overscroll = SkUiOverscrollMode.Bounce;
        var refresh = Refresh(scroller);
        SkUiTestHelpers.Arrange(refresh, 300, 400);

        Drag(refresh, new Point(150, 350), new Point(150, 50));
        Drag(refresh, new Point(20, 200), new Point(280, 210));
        Assert.False(refresh.IsRefreshing);
        _ = scroller.ScrollToAsync(0, 0, animated: false);
        Drag(refresh, new Point(150, 50), new Point(150, 380));
        Assert.True(refresh.IsRefreshing);
    }

    [Fact]
    public void TheOutermostVerticalScrollerUnderTheFingerIsPulledAndHorizontalOnesKeepTheirDrags()
    {
        var inner = Scroller(800);
        inner.HeightRequest = 200;
        var carousel = new SkUiScrollView { Orientation = ScrollOrientation.Horizontal, HeightRequest = 100, Content = new SkUiBox { WidthRequest = 1000 } };
        var column = new SkUiVerticalStackLayout();
        column.Children.Add(carousel);
        column.Children.Add(inner);
        column.Children.Add(Box(100));
        var outer = Scroller(0);
        outer.Content = column;
        var refresh = Refresh(outer);
        SkUiTestHelpers.Arrange(refresh, 300, 400);

        // The carousel takes sideways drags.
        Drag(refresh, new Point(280, 50), new Point(20, 55));
        Assert.Equal(260, carousel.ScrollX);
        Assert.False(refresh.IsRefreshing);

        // A drag in the inner list pulls through the outer scroller (at its top), which the inner one chains to.
        Drag(refresh, new Point(150, 150), new Point(150, 390));
        Assert.Same(outer, refresh.PulledScroller);
        Assert.Equal(SkUiPullInput.None, ControllerOf(inner).PullsForRefreshView);
        Assert.True(refresh.IsRefreshing);
    }

    [Fact]
    public void ACollectionViewIsPulledForTheRefreshViewNotForItself()
    {
        var log = new List<string>();
        var list = new SkUiCollectionView
        {
            ItemsSource = new ObservableCollection<int>([1, 2, 3]),
            ItemTemplate = new DataTemplate(() => Box(50)),
            Overscroll = SkUiOverscrollMode.None
        };
        var refresh = Refresh(list, log);
        SkUiTestHelpers.Arrange(refresh, 300, 400);

        Drag(refresh, new Point(150, 20), new Point(150, 380));
        Assert.True(refresh.IsRefreshing);
        Assert.False(list.IsRefreshing);
        Assert.Equal(0, list.RefreshIndicator.ShownOpacity);
        Assert.Equal(["refreshing", "command "], log);

        // The list's own setting is kept apart: turning it on and off leaves the refresh view's pull in place.
        refresh.IsRefreshing = false;
        list.IsPullToRefreshEnabled = true;
        list.IsPullToRefreshEnabled = false;
        Assert.Equal(SkUiPullInput.Touch, ControllerOf(list.ScrollView).PullsForRefreshView);
        Drag(refresh, new Point(150, 20), new Point(150, 380));
        Assert.True(refresh.IsRefreshing);
    }

    [Fact]
    public void CoreScrollersArePulledToo()
    {
        var stack = new SkUiCoreVerticalStackLayout();
        for (var i = 0; i < 3; i++)
            stack.Add(new SkUiCoreBox().SetHeight(50));
        var core = new SkUiCoreScrollView();
        core.SetContent(stack);
        core.SetOverscroll(SkUiOverscrollMode.None);
        var refresh = Refresh(new SkUiCoreHost().SetContent(core));
        SkUiTestHelpers.Arrange(refresh, 300, 400);

        Drag(refresh, new Point(150, 20), new Point(150, 380));
        Assert.Same(core, refresh.PulledScroller);
        Assert.True(refresh.IsRefreshing);
    }

    [Fact]
    public void ReplacedContentNoLongerPullsAndAMovedScrollerIsLetGo()
    {
        var first = Scroller(100);
        var refresh = Refresh(first);
        SkUiTestHelpers.Arrange(refresh, 300, 400);
        Drag(refresh, new Point(150, 20), new Point(150, 100));
        Assert.Equal(SkUiPullInput.Touch, ControllerOf(first).PullsForRefreshView);

        var second = Scroller(100);
        refresh.Content = second;
        Assert.Equal(SkUiPullInput.None, ControllerOf(first).PullsForRefreshView);
        Assert.Null(refresh.PulledScroller);
        SkUiTestHelpers.Arrange(refresh, 300, 400);
        Drag(refresh, new Point(150, 20), new Point(150, 380));
        Assert.Same(second, refresh.PulledScroller);
        Assert.True(refresh.IsRefreshing);

        // Pulled elsewhere, a scroller that left the content starts no refresh here and is let go.
        refresh.IsRefreshing = false;
        var holder = new SkUiContentView();
        var inner = Scroller(100);
        holder.Content = inner;
        refresh.Content = holder;
        SkUiTestHelpers.Arrange(refresh, 300, 400);
        Drag(refresh, new Point(150, 20), new Point(150, 100));
        Assert.Same(inner, refresh.PulledScroller);
        holder.Content = null;
        SkUiTestHelpers.Arrange(inner, 300, 400);
        Drag(inner, new Point(150, 20), new Point(150, 380));
        Assert.False(refresh.IsRefreshing);
        Assert.Null(refresh.PulledScroller);
        Assert.Equal(SkUiPullInput.None, ControllerOf(inner).PullsForRefreshView);
    }

    #endregion

    #region Pulling the view itself

    [Fact]
    public void ContentThatDoesNotScrollIsPulledByTheView()
    {
        var log = new List<string>();
        var refresh = Refresh(Box(100), log);
        using var surface = new SkUiTestSurface(refresh, 300, 400);
        surface.Frame(0);

        Drag(refresh, new Point(150, 200), new Point(150, 260), release: false);
        Assert.Null(refresh.PulledScroller);
        var shown = refresh.RefreshIndicator.ShownOpacity;
        Assert.InRange(shown, 0.05, 0.99);

        // Released before the trigger: the indicator goes back up on the UI clock.
        Release(refresh, new Point(150, 260));
        Assert.False(refresh.IsRefreshing);
        surface.Root.AnimationClock.Tick(TimeSpan.FromMilliseconds(50));
        Assert.InRange(refresh.RefreshIndicator.ShownOpacity, 0.01, shown - 0.01);
        surface.Root.AnimationClock.Tick(TimeSpan.FromMilliseconds(SkUiRefreshView.PullBackLength + 100));
        Assert.Equal(0, refresh.RefreshIndicator.ShownOpacity);

        // Upward and sideways drags do nothing; a long pull refreshes.
        Drag(refresh, new Point(150, 300), new Point(150, 20));
        Drag(refresh, new Point(20, 200), new Point(280, 220));
        Assert.Equal(0, refresh.RefreshIndicator.ShownOpacity);
        Assert.Empty(log);
        Drag(refresh, new Point(150, 20), new Point(150, 380));
        Assert.True(refresh.IsRefreshing);
        Assert.Equal(["refreshing", "command "], log);
        Assert.Equal(SkUiLook.Current.RefreshRestDistance - SkUiLook.Current.RefreshIndicatorSize, refresh.RefreshIndicator.TranslationY);
    }

    [Fact]
    public void AHeaderOverAListPullsOnlyWhileTheListIsAtItsTop()
    {
        var list = Scroller(1000);
        var grid = new SkUiGrid { RowDefinitions = [new RowDefinition(new GridLength(50)), new RowDefinition(GridLength.Star)] };
        Grid.SetRow(list, 1);
        grid.Children.Add(Box(50));
        grid.Children.Add(list);
        var refresh = Refresh(grid);
        SkUiTestHelpers.Arrange(refresh, 300, 400);

        _ = list.ScrollToAsync(0, 200, animated: false);
        Drag(refresh, new Point(150, 10), new Point(150, 390));
        Assert.False(refresh.IsRefreshing);
        Assert.Equal(200, list.ScrollY);

        _ = list.ScrollToAsync(0, 0, animated: false);
        Drag(refresh, new Point(150, 10), new Point(150, 390));
        Assert.True(refresh.IsRefreshing);
    }

    [Fact]
    public void TapsInTheContentStillReachIt()
    {
        var clicks = 0;
        var button = new SkUiButton { Text = "Go", HeightRequest = 40 };
        button.Clicked += (_, _) => clicks++;
        var stack = new SkUiVerticalStackLayout();
        stack.Children.Add(button);
        var refresh = Refresh(stack);
        SkUiTestHelpers.Arrange(refresh, 300, 400);

        Tap(refresh, new Point(150, 20));
        Assert.Equal(1, clicks);

        // A pull that starts on the button is the view's, not a tap.
        Drag(refresh, new Point(150, 20), new Point(150, 380));
        Assert.Equal(1, clicks);
        Assert.True(refresh.IsRefreshing);
    }

    [Fact]
    public void ANestedRefreshViewPullsForItself()
    {
        var innerLog = new List<string>();
        var outerLog = new List<string>();
        var inner = Refresh(Scroller(100), innerLog);
        inner.HeightRequest = 200;
        var stack = new SkUiVerticalStackLayout();
        stack.Children.Add(inner);
        stack.Children.Add(Box(100));
        var outer = Refresh(stack, outerLog);
        SkUiTestHelpers.Arrange(outer, 300, 400);

        Drag(outer, new Point(150, 20), new Point(150, 390));
        Assert.True(inner.IsRefreshing);
        Assert.False(outer.IsRefreshing);
        Assert.Null(outer.PulledScroller);

        Drag(outer, new Point(150, 210), new Point(150, 399), steps: 3);
        Assert.True(outer.IsRefreshing);
        Assert.Single(innerLog, entry => entry == "refreshing");
    }

    [Fact]
    public void PullingMovesTheIndicatorWithoutRecordingAgain()
    {
        var refresh = Refresh(Scroller(100));
        using var surface = new SkUiTestSurface(refresh, 300, 400);
        surface.Frame(0);
        // The indicator is recorded when first drawn, once while pulling and once while refreshing (the look draws each).
        Drag(refresh, new Point(150, 20), new Point(150, 100), release: false);
        surface.Frame(1);
        Release(refresh, new Point(150, 100));
        surface.Frame(2);
        var recorded = surface.RecordedPictures;

        Drag(refresh, new Point(150, 20), new Point(150, 200), release: false);
        surface.Frame(100);
        Drag(refresh, new Point(150, 20), new Point(150, 300), release: false);
        surface.Frame(200);
        Assert.Equal(recorded, surface.RecordedPictures);
    }

    #endregion
}

/// <summary><see cref="SkUiRefreshView"/> XAML: MAUI's docs sample with the prefix changed.</summary>
[Collection(RuntimeXamlCollection.Name)]
public class RefreshViewXamlTests
{
    private sealed class Model : INotifyPropertyChanged
    {
        private bool _isRefreshing;

        public Model() => RefreshCommand = new Microsoft.Maui.Controls.Command(() => Refreshes++);

        public ObservableCollection<string> Items { get; } = ["Red", "Green", "Blue"];
        public ICommand RefreshCommand { get; }
        public int Refreshes { get; private set; }

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

    [Fact]
    public void MauiDocsSampleLoadsWithThePrefixChanged()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiRefreshView IsRefreshing="{Binding IsRefreshing}"
                                  RefreshColor="Teal"
                                  Command="{Binding RefreshCommand}">
                <sk:SkUiScrollView>
                  <sk:SkUiFlexLayout Direction="Row" Wrap="Wrap" AlignItems="Center" AlignContent="Center"
                                     BindableLayout.ItemsSource="{Binding Items}">
                    <BindableLayout.ItemTemplate>
                      <DataTemplate>
                        <sk:SkUiLabel Text="{Binding}" WidthRequest="100" HeightRequest="100" />
                      </DataTemplate>
                    </BindableLayout.ItemTemplate>
                  </sk:SkUiFlexLayout>
                </sk:SkUiScrollView>
              </sk:SkUiRefreshView>
            </ContentView>
            """;
        var model = new Model();
        var root = new ContentView { BindingContext = model };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var refresh = (SkUiRefreshView)root.Content;
        SkUiTestHelpers.Arrange(refresh, 300, 400);

        Assert.Equal(Colors.Teal, refresh.RefreshColor);
        Assert.IsType<SkUiScrollView>(refresh.Content);

        // A pull sets the bound property (two-way) and runs the command; the model ends the refresh.
        refresh.Touch(new(1, SkUiTouchAction.Pressed, new Point(150, 20), TimeSpan.Zero));
        for (var step = 1; step <= 6; step++)
            refresh.Touch(new(1, SkUiTouchAction.Moved, new Point(150, 20 + step * 60), TimeSpan.FromMilliseconds(step * 100)));
        refresh.Touch(new(1, SkUiTouchAction.Released, new Point(150, 380), TimeSpan.FromMilliseconds(900)));
        Assert.True(model.IsRefreshing);
        Assert.Equal(1, model.Refreshes);
        model.IsRefreshing = false;
        Assert.False(refresh.IsRefreshing);
        Assert.False(refresh.RefreshIndicator.IsRefreshing);
    }
}
