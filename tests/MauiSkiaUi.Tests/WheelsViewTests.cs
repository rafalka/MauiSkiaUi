using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// <see cref="SkUiWheelsView"/> on its own, without a popup: it chooses only through its host's callbacks (what is chosen, select,
/// item states), and chooses nothing while the host says nothing can be chosen.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class WheelsViewTests : IDisposable
{
    private static readonly string[] Hours = ["09", "10", "11"];
    private static readonly string[] Minutes = ["00", "15", "30", "45"];
    private static readonly SkUiPickerRowStyle Style = new(null, 16, FontAttributes.None, false, 0, null);

    private readonly TestUiContext _ui = TestUiContext.Install();

    public void Dispose() => _ui.Dispose();

    /// <summary>A host that keeps the choice and records each selection.</summary>
    private sealed class Host(params int[] chosen)
    {
        public int[] Chosen = chosen;
        public bool IsActive = true;
        public readonly List<int[]> Selections = [];

        public IReadOnlyList<int>? GetChosen() => IsActive ? Chosen : null;

        public void Select(int[] indices)
        {
            Selections.Add(indices);
            Chosen = indices;
        }
    }

    private static SkUiWheelsView Create(Host host, IReadOnlyList<bool>?[]? states = null) =>
        new([Hours, Minutes], [false, true], [-1, -1], host.Chosen, 5, Style, false, host.GetChosen, host.Select, states);

    private static SkUiKeyEvent Key(SkUiKey key) => new(key, SkUiKeyModifiers.None, false);

    private static List<SkUiSemanticsNode> Wheels(SkUiView view) =>
        [.. SkUiSemanticsTree.Build(view).Flatten().Where(node => node.Source is SkUiCarouselView)];

    [Fact]
    public void KeysChooseThroughTheHost()
    {
        var host = new Host(1, 2);
        var wheels = Create(host);
        SkUiTestHelpers.Arrange(wheels, 300, 300);

        Assert.True(wheels.OnKeyDown(Key(SkUiKey.Down)));
        Assert.Equal([2, 2], host.Selections[^1]);
        Assert.True(wheels.OnKeyDown(Key(SkUiKey.Right)));
        Assert.Equal(1, wheels.ActiveColumn);
        Assert.True(wheels.OnKeyDown(Key(SkUiKey.End)));
        Assert.Equal([2, 3], host.Selections[^1]);
        Assert.Equal(3, wheels.Wheel(1).Position);
    }

    [Fact]
    public void AColumnWithNothingChosenTakesWhatItsWheelShows()
    {
        var host = new Host(-1, -1);
        var wheels = Create(host);
        SkUiTestHelpers.Arrange(wheels, 300, 300);

        // Down on a column with nothing chosen chooses the item in its middle row; the other column takes its wheel's item too.
        Assert.True(wheels.OnKeyDown(Key(SkUiKey.Down)));
        Assert.Equal([0, 0], host.Selections[^1]);
    }

    [Fact]
    public void DisabledItemsArePassed()
    {
        var host = new Host(0, 0);
        var wheels = Create(host, [[true, false, true], null]);
        SkUiTestHelpers.Arrange(wheels, 300, 300);

        Assert.True(wheels.OnKeyDown(Key(SkUiKey.Down)));
        Assert.Equal([2, 0], host.Selections[^1]);

        // The app disables the chosen item: the wheel moves to the nearest earlier one that can be chosen.
        wheels.ItemStatesChanged(0, [true, true, false]);
        Assert.Equal([1, 0], host.Selections[^1]);
    }

    [Fact]
    public void NothingIsChosenWhileTheHostIsInactive()
    {
        var host = new Host(1, 1) { IsActive = false };
        var wheels = Create(host);
        using var surface = new SkUiTestSurface(wheels, 300, 300);
        surface.Frame();

        var hour = Wheels(wheels)[0];
        Assert.Equal(SkUiSemanticsRole.Slider, hour.Role);
        Assert.False(new SkUiSemanticsOwner(wheels).Perform(hour.Id, SkUiSemanticsActions.Increment));
        Assert.Empty(host.Selections);

        host.IsActive = true;
        Assert.True(new SkUiSemanticsOwner(wheels).Perform(hour.Id, SkUiSemanticsActions.Increment));
        Assert.Equal([2, 1], host.Selections[^1]);
    }

    [Fact]
    public void ShowChoiceMovesTheWheelsWithoutChoosing()
    {
        var host = new Host(0, 0);
        var wheels = Create(host);
        SkUiTestHelpers.Arrange(wheels, 300, 300);

        wheels.ShowChoice([2, 3]);

        Assert.Equal(2, wheels.Wheel(0).Position);
        Assert.Equal(3, wheels.Wheel(1).Position);
        Assert.Empty(host.Selections);
    }

    [Fact]
    public void MeasuresItsRowsAndColumns()
    {
        var wheels = Create(new Host(0, 0));

        Assert.Equal(5 * SkUiLook.Current.PickerWheelRowHeight, wheels.PreferredHeight(300), 3);
        Assert.True(wheels.PreferredWidth() > 2 * 2 * SkUiWheelsView.TextPadding);
    }
}
