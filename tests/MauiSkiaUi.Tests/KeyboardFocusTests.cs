using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Keyboard focus of drawn surfaces (P10 in ImplementationPlan.md): MAUI's <c>Focus()</c> / <c>Unfocus()</c> /
/// <c>IsFocused</c> on drawn views, Core focus, tab order, activation and arrow keys, the focus ring. In the global
/// collection: whether the keyboard or a pointer was used last is process-wide.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class KeyboardFocusTests
{
    private static SkUiVerticalStackLayout Stack(params ISkUiView[] children)
    {
        var stack = new SkUiVerticalStackLayout();
        foreach (var child in children)
            stack.Children.Add(child);
        SkUiTestHelpers.Arrange(stack, 300, 600);
        return stack;
    }

    private static List<string> Order(SkUiView root) =>
        root.FocusManager.GetTabOrder().Select(node => node switch
        {
            SkUiButton button => button.Text,
            SkUiCoreLabel label => label.Text,
            _ => node.GetType().Name
        }).ToList();

    [Fact]
    public void TabOrderFollowsTabIndexThenTreeOrder()
    {
        var core = new SkUiCoreButton { Text = "Core" };
        var root = Stack(
            new SkUiButton { Text = "A" },
            new SkUiLabel { Text = "Not focusable" },
            new SkUiButton { Text = "B", TabIndex = 2 },
            new SkUiButton { Text = "C", IsTabStop = false },
            new SkUiButton { Text = "D", IsEnabled = false },
            new SkUiButton { Text = "E", IsVisible = false },
            new SkUiButton { Text = "F", TabIndex = 1 },
            new SkUiCoreHost().SetContent(core),
            new SkUiCheckBox(),
            new SkUiSlider());

        Assert.Equal(["A", "Core", "SkUiCheckBox", "SkUiSlider", "F", "B"], Order(root));
        core.SetTabIndex(-1);
        Assert.Equal("Core", Order(root)[0]);
    }

    [Fact]
    public void MauiFocusAndUnfocusWorkOnDrawnViews()
    {
        var first = new SkUiButton { Text = "First" };
        var second = new SkUiButton { Text = "Second" };
        var label = new SkUiLabel { Text = "Label" };
        var root = Stack(first, second, label);
        var events = new List<string>();
        first.Focused += (_, args) => events.Add($"first focused {args.IsFocused}");
        first.Unfocused += (_, args) => events.Add($"first unfocused {args.IsFocused}");
        VisualStateManager.SetVisualStateGroups(first, [new VisualStateGroup { Name = "FocusStates", States = { new VisualState { Name = "Focused" }, new VisualState { Name = "Unfocused" } } }]);

        Assert.True(first.Focus());
        Assert.True(first.IsFocused);
        Assert.Equal("Focused", VisualStateManager.GetVisualStateGroups(first)[0].CurrentState?.Name);
        Assert.Same(first, root.FocusManager.Focused);

        Assert.True(second.Focus());
        Assert.False(first.IsFocused);
        Assert.True(second.IsFocused);
        Assert.Equal("Unfocused", VisualStateManager.GetVisualStateGroups(first)[0].CurrentState?.Name);

        Assert.False(label.Focus());
        second.Unfocus();
        Assert.False(second.IsFocused);
        Assert.Null(root.FocusManager.Focused);
        Assert.Equal(["first focused True", "first unfocused False"], events);
    }

    [Fact]
    public void CoreNodesTakeFocus()
    {
        var button = new SkUiCoreButton { Text = "Core" };
        var label = new SkUiCoreLabel { Text = "Text" };
        var stack = new SkUiCoreVerticalStackLayout();
        stack.Add(button);
        stack.Add(label);
        var root = Stack(new SkUiCoreHost().SetContent(stack));
        var events = new List<string>();
        button.Focused += (_, _) => events.Add("focused");
        button.Unfocused += (_, _) => events.Add("unfocused");

        Assert.True(button.Focus());
        Assert.True(button.IsFocused);
        Assert.False(label.Focus());
        button.Unfocus();
        Assert.False(button.IsFocused);
        Assert.Equal(["focused", "unfocused"], events);
        Assert.Null(root.FocusManager.Focused);
    }

    [Fact]
    public void TabMovesThroughTheSurfaceAndLeavesAtTheEnds()
    {
        var a = new SkUiButton { Text = "A" };
        var b = new SkUiButton { Text = "B" };
        var root = Stack(a, b);
        var focus = root.FocusManager;

        Assert.True(focus.KeyDown(SkUiKey.Tab));
        Assert.True(a.IsFocused);
        Assert.True(focus.KeyDown(SkUiKey.Tab));
        Assert.True(b.IsFocused);
        // Past the last node the platform moves focus on (to the next native control).
        Assert.False(focus.KeyDown(SkUiKey.Tab));
        Assert.True(focus.KeyDown(SkUiKey.Tab, SkUiKeyModifiers.Shift));
        Assert.True(a.IsFocused);
        Assert.False(focus.KeyDown(SkUiKey.Tab, SkUiKeyModifiers.Shift));
    }

    [Fact]
    public void ActivationKeysRunTheControlAction()
    {
        var clicks = 0;
        var button = new SkUiButton { Text = "Go" };
        button.Clicked += (_, _) => clicks++;
        var check = new SkUiCheckBox();
        var tapped = 0;
        var card = new SkUiBorder { Content = new SkUiLabel { Text = "Card" } };
        card.Tapped += (_, _) => tapped++;
        var root = Stack(button, check, card);
        var focus = root.FocusManager;

        button.Focus();
        Assert.True(focus.KeyDown(SkUiKey.Enter));
        check.Focus();
        Assert.True(focus.KeyDown(SkUiKey.Space));
        card.Focus();
        Assert.True(focus.KeyDown(SkUiKey.Space));

        Assert.Equal(1, clicks);
        Assert.True(check.IsChecked);
        Assert.Equal(1, tapped);
    }

    [Fact]
    public void CanMoveFocusTellsWhetherTabStaysInTheSurface()
    {
        var a = new SkUiButton { Text = "A" };
        var hidden = new SkUiButton { Text = "Hidden", IsVisible = false };
        var b = new SkUiButton { Text = "B" };
        var root = Stack(a, hidden, b);
        var focus = root.FocusManager;

        Assert.True(focus.CanMoveFocus(forward: true)); // nothing focused: Tab enters at the first node
        a.Focus();
        Assert.True(focus.CanMoveFocus(forward: true));
        Assert.False(focus.CanMoveFocus(forward: false)); // Shift+Tab leaves before the first
        b.Focus();
        Assert.False(focus.CanMoveFocus(forward: true)); // the hidden node is skipped: B is the last
        Assert.True(focus.CanMoveFocus(forward: false));
        Assert.True(b.IsFocused); // asking moves nothing
        Assert.False(Stack(new SkUiLabel { Text = "Text only" }).FocusManager.CanMoveFocus(forward: true));
    }

    [Fact]
    public void HeldActivationKeysDoNotRepeat()
    {
        var clicks = 0;
        var button = new SkUiButton { Text = "Go" };
        button.Clicked += (_, _) => clicks++;
        var root = Stack(button);
        button.Focus();

        Assert.True(root.FocusManager.KeyDown(SkUiKey.Enter));
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Enter, isRepeat: true));
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Enter, isRepeat: true));

        Assert.Equal(1, clicks);
    }

    [Fact]
    public void FocusOnASurfaceRootFocusesItsFirstNode()
    {
        var label = new SkUiLabel { Text = "Title" };
        var first = new SkUiButton { Text = "First" };
        var root = Stack(label, first, new SkUiButton { Text = "Second" });

        Assert.True(root.Focus());
        Assert.True(first.IsFocused);
        Assert.False(root.IsFocused);
        Assert.False(new SkUiVerticalStackLayout().Focus()); // nothing to focus
    }

    [Fact]
    public void ArrowKeysAdjustSlidersAndHomeEndJump()
    {
        var slider = new SkUiSlider { Maximum = 100, Value = 50 };
        var rtl = new SkUiSlider { Maximum = 100, Value = 50, FlowDirection = FlowDirection.RightToLeft };
        var root = Stack(slider, rtl);
        var focus = root.FocusManager;

        slider.Focus();
        Assert.True(focus.KeyDown(SkUiKey.Right));
        Assert.Equal(55, slider.Value);
        Assert.True(focus.KeyDown(SkUiKey.Down));
        Assert.True(focus.KeyDown(SkUiKey.Left));
        Assert.Equal(45, slider.Value);
        Assert.True(focus.KeyDown(SkUiKey.End));
        Assert.Equal(100, slider.Value);
        Assert.True(focus.KeyDown(SkUiKey.Home));
        Assert.Equal(0, slider.Value);

        rtl.Focus();
        Assert.True(focus.KeyDown(SkUiKey.Left));
        Assert.Equal(55, rtl.Value);
    }

    [Fact]
    public void ArrowAndPageKeysScrollTheFocusedNodesScroller()
    {
        var content = new SkUiVerticalStackLayout();
        var first = new SkUiButton { Text = "First", HeightRequest = 50 };
        content.Children.Add(first);
        for (var index = 0; index < 20; index++)
            content.Children.Add(new SkUiLabel { Text = $"Row {index}", HeightRequest = 50 });
        var scroll = new SkUiScrollView { Content = content, HeightRequest = 200 };
        var root = Stack(scroll);
        var focus = root.FocusManager;

        // Nothing focused: the surface's scroller.
        Assert.True(focus.KeyDown(SkUiKey.Down));
        Assert.Equal(SkUiFocusManager.LineStep, scroll.ScrollY);
        first.Focus();
        Assert.True(focus.KeyDown(SkUiKey.PageDown));
        Assert.Equal(SkUiFocusManager.LineStep + 175, scroll.ScrollY);
        Assert.True(focus.KeyDown(SkUiKey.Up));
        Assert.Equal(175, scroll.ScrollY);
    }

    [Fact]
    public void FocusBringsTheNodeIntoView()
    {
        var content = new SkUiVerticalStackLayout();
        for (var index = 0; index < 10; index++)
            content.Children.Add(new SkUiLabel { Text = $"Row {index}", HeightRequest = 50 });
        var last = new SkUiButton { Text = "Last", HeightRequest = 50 };
        content.Children.Add(last);
        var scroll = new SkUiScrollView { Content = content, HeightRequest = 200 };
        var root = Stack(scroll);
        using var surface = new SkUiTestSurface(root, 300, 600);
        surface.Frame();

        Assert.True(last.Focus());
        surface.Frame(0);
        surface.Frame(1000);

        Assert.Equal(350, scroll.ScrollY, 0.5); // the button's bottom (550) at the viewport's bottom
    }

    [Fact]
    public void HiddenDisabledOrDetachedFocusIsCleared()
    {
        var a = new SkUiButton { Text = "A" };
        var b = new SkUiButton { Text = "B" };
        var c = new SkUiButton { Text = "C" };
        var root = Stack(a, b, c);

        a.Focus();
        a.IsVisible = false;
        Assert.False(a.IsFocused);
        b.Focus();
        b.IsEnabled = false;
        Assert.False(b.IsFocused);
        c.Focus();
        root.Children.Remove(c);
        Assert.False(c.IsFocused);
        Assert.Null(root.FocusManager.Focused);
    }

    [Fact]
    public void RingShowsForKeyboardFocusAndHidesAfterAPointerPress()
    {
        var a = new SkUiButton { Text = "A" };
        var b = new SkUiButton { Text = "B" };
        var root = Stack(a, b);
        var focus = root.FocusManager;
        SkUiFocusManager.NotePointerInput();

        // Programmatic focus after pointer input: focused, no ring.
        a.Focus();
        Assert.True(a.IsFocused);
        Assert.False(a.IsFocusRingVisible);
        // A key press shows it; Tab moves it.
        focus.KeyDown(SkUiKey.Tab);
        Assert.True(b.IsFocusRingVisible);
        Assert.False(a.IsFocusRingVisible);
        // A pointer press hides it and keeps focus.
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, new Point(-50, -50)));
        Assert.True(b.IsFocused);
        Assert.False(b.IsFocusRingVisible);
    }

    [Fact]
    public void RingIsDrawnOverTheFocusedControl()
    {
        var button = new SkUiButton { Text = "", BackgroundColor = Colors.White, WidthRequest = 100, HeightRequest = 40, HorizontalOptions = LayoutOptions.Start };
        var root = Stack(button);
        root.BackgroundColor = Colors.White;
        using var surface = new SkUiTestSurface(root, 300, 600);
        var before = surface.Frame().GetPixel(1, 20);

        root.FocusManager.KeyDown(SkUiKey.Tab);
        var after = surface.Frame().GetPixel(1, 20);

        Assert.True(button.IsFocusRingVisible);
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void PointerPressFocusesOnlyWhenEnabled()
    {
        var button = new SkUiButton { Text = "Button", HeightRequest = 40 };
        var card = new SkUiBorder { Content = new SkUiLabel { Text = "Card", HeightRequest = 40 } };
        card.Tapped += (_, _) => { };
        var plain = new SkUiBox { HeightRequest = 40 };
        var root = Stack(button, card, plain);
        void Press(double y)
        {
            root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, new Point(10, y)));
            root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Cancelled, new Point(10, y)));
        }

        // Off by default (Apple, Android): a press does not move keyboard focus.
        Press(20);
        Assert.False(button.IsFocused);

        // On (Windows): the press focuses the control it hits, or its focusable ancestor, without the ring.
        root.FocusManager.PointerPressFocuses = true;
        Press(20);
        Assert.True(button.IsFocused);
        Assert.False(button.IsFocusRingVisible);
        Press(60); // the label inside the tappable card
        Assert.True(card.IsFocused);
        Press(100); // nothing interactive: focus stays
        Assert.True(card.IsFocused);
    }

    [Fact]
    public void NativeFocusIsTakenAndFollowed()
    {
        var a = new SkUiButton { Text = "A" };
        var b = new SkUiButton { Text = "B" };
        var root = Stack(a, b);
        var focus = root.FocusManager;
        var requests = 0;
        var releases = 0;
        var granted = false;
        focus.RequestNativeFocus = () => { requests++; return granted; };
        focus.ReleaseNativeFocus = () => releases++;
        focus.Connect();

        Assert.False(a.Focus()); // the platform refused
        granted = true;
        Assert.True(a.Focus());
        Assert.Equal(2, requests);

        // Another native control took focus: the drawn node loses it.
        focus.OnNativeFocusChanged(false);
        Assert.False(a.IsFocused);
        // Shift+Tab into the surface focuses its last node.
        focus.OnNativeFocusChanged(true, forward: false);
        Assert.True(b.IsFocused);
        b.Unfocus();
        Assert.Equal(1, releases);
    }
}
