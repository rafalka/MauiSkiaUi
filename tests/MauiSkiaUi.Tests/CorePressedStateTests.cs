using System.ComponentModel;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// M6: a Core node's press, as <see cref="SkUiView.IsPressed"/> and <c>OnPressedChanged</c>: set while a pointer is pressed inside a
/// node with a tap action, cleared on release and when its gestures are cancelled, raised as <c>PropertyChanged</c>; the Core
/// controls keep their pressed state and events on it.
/// </summary>
public class CorePressedStateTests
{
    private static int _pointer = 7000;

    /// <summary>An app-made Core control that draws its own pressed look.</summary>
    private sealed class PressableRow : SkUiCoreBox
    {
        public readonly List<bool> Changes = [];

        protected override void OnPressedChanged() => Changes.Add(IsPressed);
    }

    private static (SkUiVerticalStackLayout Root, Point At) Host(SkUiCoreNode node)
    {
        var root = new SkUiVerticalStackLayout();
        root.Children.Add(new SkUiCoreHost { WidthRequest = 100, HeightRequest = 40, HorizontalOptions = LayoutOptions.Start }.SetContent(node));
        SkUiTestHelpers.Arrange(root, 300, 200);
        return (root, new Point(50, 20));
    }

    private static int Press(SkUiView root, Point at)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        return id;
    }

    private static void Release(SkUiView root, int id, Point at) => root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.04)));

    private static List<string?> Notifications(INotifyPropertyChanged node)
    {
        var names = new List<string?>();
        node.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        return names;
    }

    [Fact]
    public void ANodeWithATapActionIsPressedWhileAPointerIsDown()
    {
        var row = new PressableRow();
        var taps = 0;
        row.Tapped += (_, _) => taps++;
        var (root, at) = Host(row);
        var names = Notifications(row);

        var id = Press(root, at);
        Assert.True(row.IsPressed);
        Release(root, id, at);
        Assert.False(row.IsPressed);
        Assert.Equal([true, false], row.Changes);
        Assert.Equal(2, names.Count(name => name == nameof(SkUiCoreNode.IsPressed)));
        Assert.Equal(1, taps);
    }

    [Fact]
    public void CancellingTheGesturesClearsThePress()
    {
        var row = new PressableRow();
        row.SetTappedCommand(new Command(() => { }));
        var (root, at) = Host(row);
        var id = Press(root, at);
        Assert.True(row.IsPressed);
        root.Touch(new(id, SkUiTouchAction.Cancelled, at, TimeSpan.FromSeconds(1.02)));
        Assert.False(row.IsPressed);
        Assert.Equal([true, false], row.Changes);
    }

    [Fact]
    public void NodesWithoutATapActionOrDisabledAreNotPressed()
    {
        var plain = new PressableRow();
        var (root, at) = Host(plain);
        Release(root, Press(root, at), at);
        Assert.Empty(plain.Changes);

        var disabled = new PressableRow();
        disabled.Tapped += (_, _) => { };
        disabled.SetIsEnabled(false);
        (root, at) = Host(disabled);
        Release(root, Press(root, at), at);
        Assert.Empty(disabled.Changes);
        Assert.False(disabled.IsPressed);
    }

    [Fact]
    public void CoreControlsKeepTheirPressedStateAndEvents()
    {
        var button = new SkUiCoreButton { Text = "Save" };
        var events = new List<string>();
        button.Pressed += (_, _) => events.Add(nameof(SkUiCoreButton.Pressed) + button.IsPressed);
        button.Released += (_, _) => events.Add(nameof(SkUiCoreButton.Released) + button.IsPressed);
        var (root, at) = Host(button);
        var id = Press(root, at);
        Assert.True(button.IsPressed);
        Release(root, id, at);
        Assert.Equal(["PressedTrue", "ReleasedFalse"], events);

        var check = new SkUiCoreCheckBox();
        (root, at) = Host(check);
        id = Press(root, at);
        Assert.True(check.IsPressed);
        Release(root, id, at);
        Assert.False(check.IsPressed);
        Assert.True(check.IsChecked);
    }
}
