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

    [Fact]
    public void TwoUiThreadsFocusAndChangeTheirTreesAtOnce()
    {
        using var start = new Barrier(2);
        var failures = new Exception?[2];
        var threads = Enumerable.Range(0, 2).Select(index => new Thread(() =>
        {
            try
            {
                FocusAndDetach(start);
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
}
