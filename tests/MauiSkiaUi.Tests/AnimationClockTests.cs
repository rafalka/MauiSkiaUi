using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary><see cref="SkUiAnimationClock"/>: callbacks that start or stop animations while the clock ticks.</summary>
public class AnimationClockTests
{
    [Fact]
    public void CallbacksMayDisposeOtherAnimationsDuringATick()
    {
        var clock = new SkUiAnimationClock();
        var calls = new List<string>();
        IDisposable? second = null;
        var first = clock.Start(_ => { calls.Add("first"); second?.Dispose(); }, TimeSpan.FromSeconds(1));
        second = clock.Start(_ => calls.Add("second"), TimeSpan.FromSeconds(1));
        var third = clock.Start(_ => calls.Add("third"), TimeSpan.FromSeconds(1));
        calls.Clear();

        clock.Tick(TimeSpan.FromMilliseconds(100)); // "first" disposes "second": "third" must not be skipped
        Assert.Equal(["first", "third"], calls);
        calls.Clear();
        clock.Tick(TimeSpan.FromMilliseconds(200));
        Assert.Equal(["first", "third"], calls);
        first.Dispose();
        third.Dispose();
        Assert.False(clock.IsRunning);
    }

    [Fact]
    public void CallbacksMayDisposeThemselvesOrStopEverything()
    {
        var clock = new SkUiAnimationClock();
        var stops = 0;
        clock.RunningChanged += (_, _) => { if (!clock.IsRunning) stops++; };
        IDisposable? self = null;
        var seenAfter = 0;
        self = clock.Start(_ => self?.Dispose(), TimeSpan.FromSeconds(1));
        clock.Start(_ => seenAfter++, TimeSpan.FromSeconds(1));
        seenAfter = 0;
        clock.Tick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(1, seenAfter); // the animation after a self-disposing one still runs
        Assert.True(clock.IsRunning);

        var stopped = 0;
        var later = 0;
        clock.Start(t => { if (t > 0) clock.StopAll(); }, TimeSpan.FromSeconds(1)); // not at Start's apply(0)
        clock.Start(_ => later++, TimeSpan.FromSeconds(1), easing: null, repeat: false, stopped: () => stopped++);
        later = 0;
        clock.Tick(TimeSpan.FromMilliseconds(100)); // StopAll mid-tick: no exception, the rest is not run, nothing left
        Assert.Equal(0, later);
        Assert.Equal(1, stopped);
        Assert.False(clock.IsRunning);
        Assert.Equal(1, stops);
        clock.Tick(TimeSpan.FromMilliseconds(150));
    }

    [Fact]
    public void StoppedRunsOnlyWhenAnAnimationDoesNotFinish()
    {
        var clock = new SkUiAnimationClock();
        var stopped = new List<string>();
        clock.Start(_ => { }, TimeSpan.FromMilliseconds(100), easing: null, repeat: false, stopped: () => stopped.Add("finished"));
        var cancelled = clock.Start(_ => { }, TimeSpan.FromSeconds(1), easing: null, repeat: false, stopped: () => stopped.Add("cancelled"));
        clock.Tick(TimeSpan.FromMilliseconds(150));
        cancelled.Dispose();
        cancelled.Dispose();
        Assert.Equal(["cancelled"], stopped);
    }
}
