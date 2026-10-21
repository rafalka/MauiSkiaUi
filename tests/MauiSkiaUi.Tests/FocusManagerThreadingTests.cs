using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Surfaces on different UI threads (windows with a thread each, test collections running in parallel) keep focus apart: one
/// thread's tree changes check only its own surfaces' focus, so focusing and reparenting on two threads at once never fails.
/// </summary>
public class FocusManagerThreadingTests
{
    private const int Rounds = 2000;

    /// <summary>Focuses a button on a surface of its own, then takes it out of the tree, <see cref="Rounds"/> times.</summary>
    private static void FocusAndDetach(Barrier start)
    {
        using var ui = TestUiContext.Install();
        start.SignalAndWait();
        for (var round = 0; round < Rounds; round++)
        {
            var button = new SkUiButton { Text = "Focus me" };
            var stack = new SkUiVerticalStackLayout { Children = { button } };
            SkUiTestHelpers.Arrange(stack, 200, 100);
            Assert.True(button.Focus());
            // Detaching validates every surface's focus on this thread: the button's surface loses it.
            stack.Children.Remove(button);
            Assert.False(button.IsFocused);
        }
    }

    /// <summary>Runs <paramref name="work"/> on <paramref name="count"/> new threads at once and fails with the first failure.</summary>
    private static void OnThreads(int count, Action work)
    {
        var failures = new Exception?[count];
        var threads = Enumerable.Range(0, count).Select(index => new Thread(() =>
        {
            try
            {
                work();
            }
            catch (Exception exception)
            {
                failures[index] = exception;
            }
        })).ToArray();
        foreach (var thread in threads)
            thread.Start();
        foreach (var thread in threads)
            thread.Join();
        Assert.All(failures, Assert.Null);
    }

    [Fact]
    public void TwoUiThreadsFocusAndChangeTheirTreesAtOnce()
    {
        using var start = new Barrier(2);
        OnThreads(2, () => FocusAndDetach(start));
    }

    [Fact]
    public void AKeyPressShowsFocusRingsOnItsOwnUiThreadOnly()
    {
        OnThreads(1, () =>
        {
            using var ui = TestUiContext.Install();
            var a = new SkUiButton { Text = "A" };
            var b = new SkUiButton { Text = "B" };
            var stack = new SkUiVerticalStackLayout { Children = { a, b } };
            SkUiTestHelpers.Arrange(stack, 200, 100);
            a.Focus();
            stack.FocusManager.KeyDown(SkUiKey.Tab);
            Assert.True(b.IsFocusRingVisible);
        });
        // Another UI thread had no key press: programmatic focus there shows no ring.
        OnThreads(1, () =>
        {
            using var ui = TestUiContext.Install();
            var button = new SkUiButton { Text = "Focus me" };
            SkUiTestHelpers.Arrange(new SkUiVerticalStackLayout { Children = { button } }, 200, 100);
            Assert.True(button.Focus());
            Assert.False(button.IsFocusRingVisible);
        });
    }
}
