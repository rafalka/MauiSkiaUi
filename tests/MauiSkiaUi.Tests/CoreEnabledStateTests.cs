using System.Windows.Input;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Core enabled state and tap command (M1 in ImplementationPlan.md, architecture review N2 / N3): <c>IsEnabled</c> cascading
/// through Core trees and from the host, <c>InputTransparent</c>, <c>TappedCommand</c>, and one blocking rule for both layers,
/// checked SkUi vs Core: taps, ancestor scrolling, semantics and the drawn disabled state.
/// </summary>
public class CoreEnabledStateTests
{
    /// <summary>Press, move in steps, release, 10 ms per sample.</summary>
    private static void Drag(SkUiView root, Point from, Point to, long id = 1, int steps = 10)
    {
        root.Touch(new(id, SkUiTouchAction.Pressed, from, TimeSpan.Zero));
        for (var step = 1; step <= steps; step++)
        {
            var t = (double)step / steps;
            root.Touch(new(id, SkUiTouchAction.Moved, new Point(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t),
                TimeSpan.FromMilliseconds(step * 10)));
        }
        root.Touch(new(id, SkUiTouchAction.Released, to, TimeSpan.FromMilliseconds(steps * 10 + 500)));
    }

    private static SKBitmap Render(Action<SKCanvas> paint, int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        paint(canvas);
        return bitmap;
    }

    private static SKBitmap Render(SkUiView view, int width, int height)
    {
        view.WidthRequest = width;
        view.HeightRequest = height;
        SkUiTestHelpers.Arrange(view, width, height);
        return Render(view.Paint, width, height);
    }

    private static SKBitmap Render(SkUiCoreNode node, int width, int height)
    {
        node.SetWidth(width).SetHeight(height);
        node.Measure(width, height);
        node.Arrange(new Rect(0, 0, width, height));
        return Render(node.Paint, width, height);
    }

    /// <summary>A command that counts how often it is asked whether it can execute.</summary>
    private sealed class CountingCommand : ICommand
    {
        public bool Allowed = true;
        public int CanExecuteCalls;
        public readonly List<object?> Executed = [];

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            CanExecuteCalls++;
            return Allowed;
        }

        public void Execute(object? parameter) => Executed.Add(parameter);

        public void Change(bool allowed)
        {
            Allowed = allowed;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [Fact]
    public void IsEnabledCascadesThroughCoreTreesAndFromTheHost()
    {
        var child = new SkUiCoreBox();
        var inner = new SkUiCoreVerticalStackLayout();
        inner.Add(child);
        var outer = new SkUiCoreVerticalStackLayout();
        outer.Add(inner);
        var host = new SkUiCoreHost().SetContent(outer);
        var page = new SkUiVerticalStackLayout();
        page.Children.Add(host);
        var changes = new List<bool>();
        child.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SkUiCoreNode.IsEnabled))
                changes.Add(child.IsEnabled);
        };

        outer.SetIsEnabled(false);
        Assert.False(inner.IsEnabled);
        Assert.False(child.IsEnabled);
        child.SetIsEnabled(true); // its own value; the parent still disables it
        Assert.False(child.IsEnabled);
        outer.SetIsEnabled(true);
        Assert.True(child.IsEnabled);

        // An own false value outlasts an ancestor's change.
        inner.SetIsEnabled(false);
        outer.SetIsEnabled(false);
        outer.SetIsEnabled(true);
        Assert.False(inner.IsEnabled);
        Assert.False(child.IsEnabled);
        inner.SetIsEnabled(true);
        Assert.True(child.IsEnabled);

        // The host's effective IsEnabled caps its Core tree, also when an ancestor of the host changes.
        host.IsEnabled = false;
        Assert.False(child.IsEnabled);
        host.IsEnabled = true;
        page.IsEnabled = false;
        Assert.False(outer.IsEnabled);
        Assert.False(child.IsEnabled);
        page.IsEnabled = true;
        Assert.True(child.IsEnabled);

        // Attached under a disabled parent it is disabled; detached it is enabled again.
        var late = new SkUiCoreBox();
        outer.SetIsEnabled(false);
        outer.Add(late);
        Assert.False(late.IsEnabled);
        outer.Remove(late);
        Assert.True(late.IsEnabled);
        outer.SetIsEnabled(true);

        // A Core tree set on a disabled host starts disabled.
        host.IsEnabled = false;
        var other = new SkUiCoreBox();
        host.SetContent(other);
        Assert.False(other.IsEnabled);
        host.SetContent(null);
        Assert.True(other.IsEnabled);

        Assert.Equal([false, true, false, true, false, true, false, true, false, true, false, true], changes);
    }

    [Fact]
    public void DisabledNodesTakeTapsWithoutActingAndInputTransparentOnesPassThem()
    {
        // SkUi
        var (parentTaps, childTaps) = (0, 0);
        var view = new SkUiBox { WidthRequest = 50, HeightRequest = 50, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        view.Tapped += (_, _) => childTaps++;
        var viewParent = new SkUiContentView { Content = view };
        viewParent.Tapped += (_, _) => parentTaps++;
        SkUiTestHelpers.Arrange(viewParent, 200, 200);
        // Core
        var (coreParentTaps, coreChildTaps) = (0, 0);
        var node = new SkUiCoreBox();
        node.SetWidth(50).SetHeight(50).SetHorizontalAlignment(LayoutAlignment.Start).SetVerticalAlignment(LayoutAlignment.Start);
        node.Tapped += (_, _) => coreChildTaps++;
        var nodeParent = new SkUiCoreContentView().SetContent(node);
        nodeParent.Tapped += (_, _) => coreParentTaps++;
        var host = new SkUiCoreHost().SetContent(nodeParent);
        SkUiTestHelpers.Arrange(host, 200, 200);

        void Tap()
        {
            SkUiDiagnostics.SimulateTap(view);
            SkUiDiagnostics.SimulateTap(node);
        }

        Tap();
        Assert.Equal((0, 1), (parentTaps, childTaps));
        Assert.Equal((0, 1), (coreParentTaps, coreChildTaps));

        view.IsEnabled = false;
        node.SetIsEnabled(false);
        Tap();
        Assert.Equal((0, 1), (parentTaps, childTaps));
        Assert.Equal((0, 1), (coreParentTaps, coreChildTaps));

        view.IsEnabled = true;
        node.SetIsEnabled(true);
        view.InputTransparent = true;
        node.SetInputTransparent(true);
        Tap();
        Assert.Equal((1, 1), (parentTaps, childTaps));
        Assert.Equal((1, 1), (coreParentTaps, coreChildTaps));
    }

    [Fact]
    public void ButtonsWhoseCommandCannotExecuteBlockTapsOnBothLayers()
    {
        // N3: a Core button whose command cannot execute let taps through to what is underneath; SkUiButton blocks them.
        var command = new CountingCommand { Allowed = false };
        var button = new SkUiButton { Command = command, WidthRequest = 80, HeightRequest = 40, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        var parent = new SkUiContentView { Content = button };
        var parentTaps = 0;
        parent.Tapped += (_, _) => parentTaps++;
        SkUiTestHelpers.Arrange(parent, 200, 200);
        var core = new SkUiCoreButton().SetCommand(command);
        core.SetWidth(80).SetHeight(40).SetHorizontalAlignment(LayoutAlignment.Start).SetVerticalAlignment(LayoutAlignment.Start);
        var coreParent = new SkUiCoreContentView().SetContent(core);
        var coreParentTaps = 0;
        coreParent.Tapped += (_, _) => coreParentTaps++;
        var host = new SkUiCoreHost().SetContent(coreParent);
        SkUiTestHelpers.Arrange(host, 200, 200);

        SkUiDiagnostics.SimulateTap(button);
        SkUiDiagnostics.SimulateTap(core);
        Assert.Equal((0, 0, 0), (parentTaps, coreParentTaps, command.Executed.Count));

        command.Change(allowed: true);
        SkUiDiagnostics.SimulateTap(button);
        SkUiDiagnostics.SimulateTap(core);
        Assert.Equal((0, 0, 2), (parentTaps, coreParentTaps, command.Executed.Count));
    }

    [Fact]
    public void DisabledNodesDoNotKeepScrollersAroundThemFromScrolling()
    {
        // N2: a press on a disabled node used to end in an empty arena, so a list of disabled buttons could not be scrolled from them.
        var command = new CountingCommand { Allowed = false };
        var column = new SkUiVerticalStackLayout();
        column.Children.Add(new SkUiButton { IsEnabled = false, HeightRequest = 50 });
        column.Children.Add(new SkUiButton { Command = command, HeightRequest = 50 });
        column.Children.Add(new SkUiBox { HeightRequest = 1000 });
        var scroll = new SkUiScrollView { Content = column };
        SkUiTestHelpers.Arrange(scroll, 300, 400);

        var coreColumn = new SkUiCoreVerticalStackLayout();
        coreColumn.Add(new SkUiCoreButton().SetHeight(50).SetIsEnabled(false));
        coreColumn.Add(new SkUiCoreButton().SetCommand(command).SetHeight(50));
        coreColumn.Add(new SkUiCoreBox().SetHeight(1000));
        var coreScroll = new SkUiCoreScrollView();
        coreScroll.SetContent(coreColumn);
        var host = new SkUiCoreHost().SetContent(coreScroll);
        SkUiTestHelpers.Arrange(host, 300, 400);

        Drag(scroll, new Point(150, 25), new Point(150, -15));
        Drag(host, new Point(150, 25), new Point(150, -15));
        Assert.Equal(40, scroll.ScrollY);
        Assert.Equal(40, coreScroll.ScrollY);
        Drag(scroll, new Point(150, 75), new Point(150, 35), id: 2);
        Drag(host, new Point(150, 75), new Point(150, 35), id: 2);
        Assert.Equal(80, scroll.ScrollY);
        Assert.Equal(80, coreScroll.ScrollY);
        Assert.Empty(command.Executed);
    }

    [Fact]
    public void ATouchOnADisabledScrollersContentLeavesItsAnimatedScrollAlone()
    {
        // A disabled scroller's gestures stay out of a press on its (disabled) content: the hit test stops at the outermost
        // disabled node, whose ancestors are enabled, so the press only blocks and a scroll the app animates meanwhile (as
        // during a refresh) keeps going, on both layers.
        var column = new SkUiVerticalStackLayout();
        var coreColumn = new SkUiCoreVerticalStackLayout();
        for (var index = 0; index < 40; index++)
        {
            column.Children.Add(new SkUiButton { Text = $"Row {index}", HeightRequest = 50 });
            coreColumn.Add(new SkUiCoreButton { Text = $"Row {index}" }.SetHeight(50));
        }
        var scroll = new SkUiScrollView { Content = column, IsEnabled = false };
        var coreScroll = new SkUiCoreScrollView();
        coreScroll.SetContent(coreColumn).SetIsEnabled(false);
        var host = new SkUiCoreHost().SetContent(new SkUiCoreContentView().SetContent(coreScroll));
        var root = new SkUiContentView { Content = scroll };
        using var surface = new SkUiTestSurface(root, 300, 400);
        using var coreSurface = new SkUiTestSurface(host, 300, 400);
        surface.Frame(0);
        coreSurface.Frame(0);

        _ = scroll.ScrollToAsync(0, 1000, animated: true);
        _ = coreScroll.ScrollToAsync(0, 1000, animated: true);
        surface.Frame(16);
        coreSurface.Frame(16);
        Assert.True(scroll.IsMotionRunning);
        Assert.True(coreScroll.IsScrollAnimating);
        root.Touch(new(1, SkUiTouchAction.Pressed, new Point(150, 100), TimeSpan.FromMilliseconds(20)));
        host.Touch(new(1, SkUiTouchAction.Pressed, new Point(150, 100), TimeSpan.FromMilliseconds(20)));
        Assert.True(scroll.IsMotionRunning);
        Assert.True(coreScroll.IsScrollAnimating);
        root.Touch(new(1, SkUiTouchAction.Released, new Point(150, 100), TimeSpan.FromMilliseconds(30)));
        host.Touch(new(1, SkUiTouchAction.Released, new Point(150, 100), TimeSpan.FromMilliseconds(30)));
    }

    [Fact]
    public void TappedCommandRunsWhileItCanExecuteAndIsReadOnChange()
    {
        var command = new CountingCommand();
        var view = new SkUiBox { WidthRequest = 50, HeightRequest = 50, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        var viewParent = new SkUiContentView { Content = view };
        var events = new List<string>();
        var node = new SkUiCoreBox();
        node.SetWidth(50).SetHeight(50).SetHorizontalAlignment(LayoutAlignment.Start).SetVerticalAlignment(LayoutAlignment.Start);
        node.SetTappedCommand(command).SetTappedCommandParameter("core");
        node.Tapped += (_, _) => events.Add($"tapped, executed {command.Executed.Count}");
        var nodeParent = new SkUiCoreContentView().SetContent(node);
        nodeParent.Tapped += (_, _) => events.Add("parent");
        var host = new SkUiCoreHost().SetContent(nodeParent);
        SkUiTestHelpers.Arrange(host, 200, 200);
        view.TappedCommand = command;
        view.TappedCommandParameter = "view";
        SkUiTestHelpers.Arrange(viewParent, 200, 200);

        // The Tapped handler first, then the command, with its parameter.
        var calls = command.CanExecuteCalls;
        SkUiDiagnostics.SimulateTap(node);
        Assert.Equal(["tapped, executed 0"], events);
        Assert.Equal(["core"], command.Executed);
        Assert.Equal(calls + 1, command.CanExecuteCalls); // asked once more, when it runs: not per press
        SkUiDiagnostics.SimulateTap(view);
        Assert.Equal(["core", "view"], command.Executed);

        // While it cannot execute, the Tapped handler still runs; a node with only the command is passive (as SkUiView's):
        // the tap reaches the parent.
        command.Change(allowed: false);
        SkUiDiagnostics.SimulateTap(node);
        Assert.Equal(["tapped, executed 0", "tapped, executed 2"], events);
        Assert.Equal(2, command.Executed.Count);

        var bare = new SkUiCoreBox().SetTappedCommand(command);
        bare.SetWidth(50).SetHeight(50).SetHorizontalAlignment(LayoutAlignment.End).SetVerticalAlignment(LayoutAlignment.End);
        var bareParent = new SkUiCoreContentView().SetContent(bare);
        var bareParentTaps = 0;
        bareParent.Tapped += (_, _) => bareParentTaps++;
        var bareHost = new SkUiCoreHost().SetContent(bareParent);
        SkUiTestHelpers.Arrange(bareHost, 200, 200);
        SkUiDiagnostics.SimulateTap(bare);
        Assert.Equal(1, bareParentTaps);
        command.Change(allowed: true);
        SkUiDiagnostics.SimulateTap(bare);
        Assert.Equal(1, bareParentTaps);
        Assert.Equal(3, command.Executed.Count);

        // Screen readers: an activate action while it can execute, none otherwise.
        var tree = SkUiSemanticsTree.Build(bareHost);
        Assert.Equal(SkUiSemanticsActions.Activate, tree.Find(bare)!.Actions);
        command.Change(allowed: false);
        Assert.Null(SkUiSemanticsTree.Build(bareHost).Find(bare));
    }

    [Fact]
    public void ANewCommandParameterRepaintsOnlyWhenCanExecuteChanges()
    {
        // Recycled rows re-set the parameter on every bind: nothing about tapping changes unless CanExecute does.
        var command = new Command<string>(_ => { }, parameter => parameter != "locked");
        var row = new SkUiCoreBox().SetTappedCommand(command);
        var button = new SkUiCoreButton().SetCommand(command);
        var host = new SkUiCoreHost().SetContent(new SkUiCoreVerticalStackLayout().Add(row).Add(button));
        SkUiTestHelpers.Arrange(host, 300, 400);
        var paints = 0;
        row.PaintInvalidated += (_, _) => paints++;
        button.PaintInvalidated += (_, _) => paints++;

        foreach (var item in new[] { "a", "b", "c" })
        {
            row.SetTappedCommandParameter(item);
            button.SetCommandParameter(item);
        }
        Assert.Equal(0, paints);

        row.SetTappedCommandParameter("locked");
        button.SetCommandParameter("locked");
        Assert.Equal(2, paints);
        Assert.False(button.IsInteractionEnabled);
    }

    [Fact]
    public void DisabledCoreNodesAreDisabledForScreenReaders()
    {
        var check = new SkUiCoreCheckBox();
        var tapped = new SkUiCoreBox().SetTappedCommand(new CountingCommand());
        tapped.SetHeight(20);
        var group = new SkUiCoreVerticalStackLayout();
        group.Add(check);
        group.Add(tapped);
        var root = new SkUiCoreHost().SetContent(group);
        SkUiTestHelpers.Arrange(root, 300, 400);
        var owner = new SkUiSemanticsOwner(root);
        Assert.All(owner.Tree.Flatten(), node => Assert.True(node.IsEnabled));

        group.SetIsEnabled(false);
        Assert.Equal(2, owner.Tree.Flatten().Count());
        Assert.All(owner.Tree.Flatten(), node => Assert.False(node.IsEnabled));
        Assert.False(owner.Perform(owner.Tree.Find(check)!.Id, SkUiSemanticsActions.Activate));
        Assert.False(check.IsChecked);

        group.SetIsEnabled(true);
        Assert.True(owner.Perform(owner.Tree.Find(check)!.Id, SkUiSemanticsActions.Activate));
        Assert.True(check.IsChecked);
    }

    [Fact]
    public void DisabledCoreControlsDrawLikeTheirSkUiTwins()
    {
        // Each pair disabled, the Core control through a disabled parent (the cascade feeds the drawing).
        AssertSamePixels(new SkUiCheckBox { IsChecked = true }, new SkUiCoreCheckBox().SetIsChecked(true), 40, 40);
        AssertSamePixels(new SkUiRadioButton { IsChecked = true }, new SkUiCoreRadioButton().SetIsChecked(true), 40, 40);
        AssertSamePixels(new SkUiSwitch { IsToggled = true }, new SkUiCoreSwitch().SetIsToggled(true), 60, 40);
        AssertSamePixels(new SkUiSlider { Value = 0.5 }, new SkUiCoreSlider().SetValue(0.5), 200, 40);
        AssertSamePixels(new SkUiProgressBar { Progress = 0.5 }, new SkUiCoreProgressBar().SetProgress(0.5), 200, 10);
        AssertSamePixels(new SkUiButton(), new SkUiCoreButton(), 100, 44);

        static void AssertSamePixels(SkUiView view, SkUiCoreNode node, int width, int height)
        {
            using var enabled = Render(node, width, height);
            var parent = new SkUiCoreContentView().SetContent(node);
            var paints = 0;
            node.PaintInvalidated += (_, _) => paints++;
            parent.SetIsEnabled(false);
            Assert.True(paints > 0, node.GetType().Name); // re-recorded
            using var viewEnabled = Render(view, width, height);
            var viewPaints = 0;
            view.PaintInvalidated += (_, _) => viewPaints++;
            view.IsEnabled = false;
            Assert.True(viewPaints > 0, view.GetType().Name);
            using var expected = Render(view, width, height);
            using var actual = Render(node, width, height);
            Assert.NotEqual(enabled.Pixels, actual.Pixels);
            Assert.Equal(expected.Pixels, actual.Pixels);
        }
    }

    [Fact]
    public void DisabledCoreTextFieldsTakeNoTextOrFocus()
    {
        var entry = new SkUiCoreEntry();
        entry.SetWidth(200).SetHeight(40);
        var completed = 0;
        entry.Completed += (_, _) => completed++;
        var root = new SkUiCoreHost().SetContent(entry);
        SkUiTestHelpers.Arrange(root, 300, 100);

        Assert.True(entry.Focus());
        entry.SetIsEnabled(false);
        Assert.False(entry.IsFocused);
        Assert.False(entry.Focus());
        ((ISkUiTextFieldOwner)entry).Completed();
        Assert.Equal(0, completed);

        entry.SetIsEnabled(true);
        Assert.True(entry.Focus());
        ((ISkUiTextFieldOwner)entry).Completed();
        Assert.Equal(1, completed);
    }
}
