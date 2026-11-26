using System.ComponentModel;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary><see cref="SkUiChip"/> / <see cref="SkUiCoreChip"/> (1.18 K, #130): kinds, selection, the remove button, the badge, semantics, keys, pixels.</summary>
public class ChipTests
{
    private static long _pointer = 180_000;

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    private static SkUiChip Chip(string text, SkUiChipKind kind = SkUiChipKind.Action) =>
        new() { Text = text, Kind = kind, FontFamily = SkUiTestHelpers.BundledFontFamily };

    private static SkUiVerticalStackLayout Root(SkUiView chip)
    {
        chip.HorizontalOptions = LayoutOptions.Start;
        var root = new SkUiVerticalStackLayout { Children = { chip } };
        SkUiTestHelpers.Arrange(root, 300, 100);
        return root;
    }

    private static Point Middle(SkUiView view) => new(view.Frame.X + view.Width / 2 - 4, view.Frame.Y + view.Height / 2);

    private static Point RemoveButton(SkUiView view) => new(view.Frame.Right - 14, view.Frame.Y + view.Height / 2);

    [Fact]
    public void FilterChipsToggleOnTapsBeforeClicked()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var chip = Chip("Approved", SkUiChipKind.Filter);
        var seen = new List<bool>();
        chip.Clicked += (_, _) => seen.Add(chip.IsSelected);
        var root = Root(chip);
        var unselected = chip.Width;

        Tap(root, Middle(chip));
        Assert.True(chip.IsSelected);
        Assert.Equal([true], seen);
        SkUiTestHelpers.Arrange(root, 300, 100);
        Assert.Equal(unselected + SkUiLook.Current.ChipIconSize + SkUiLook.Current.ChipIconSpacing, chip.Width, 1); // the check's room

        Tap(root, Middle(chip));
        Assert.False(chip.IsSelected);

        var action = Chip("Share");
        var actionRoot = Root(action);
        Tap(actionRoot, Middle(action));
        Assert.False(action.IsSelected); // an action chip only clicks
    }

    [Fact]
    public void TheSelectedVisualStateStylesASelectedChip()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var chip = Chip("Approved", SkUiChipKind.Filter);
        VisualStateManager.SetVisualStateGroups(chip, new VisualStateGroupList
        {
            new VisualStateGroup
            {
                Name = "CommonStates",
                States =
                {
                    new VisualState { Name = VisualStateManager.CommonStates.Normal },
                    new VisualState
                    {
                        Name = VisualStateManager.CommonStates.Selected,
                        Setters =
                        {
                            new Setter { Property = SkUiLabel.TextColorProperty, Value = Colors.Red },
                            new Setter { Property = SkUiLabel.FontAttributesProperty, Value = FontAttributes.Bold }
                        }
                    },
                    new VisualState
                    {
                        Name = VisualStateManager.CommonStates.Disabled,
                        Setters = { new Setter { Property = SkUiLabel.TextColorProperty, Value = Colors.Gray } }
                    }
                }
            }
        });
        var normal = chip.TextColor;
        var root = Root(chip);

        Tap(root, Middle(chip));
        Assert.Equal(Colors.Red, chip.TextColor);
        Assert.Equal(FontAttributes.Bold, chip.FontAttributes);

        // No Pressed state in the group: a press keeps the selected look.
        var id = 185_000L;
        root.Touch(new(id, SkUiTouchAction.Pressed, Middle(chip), TimeSpan.FromSeconds(3)));
        Assert.Equal(Colors.Red, chip.TextColor);
        root.Touch(new(id, SkUiTouchAction.Cancelled, Middle(chip), TimeSpan.FromSeconds(3.1)));

        chip.IsEnabled = false;
        Assert.Equal(Colors.Gray, chip.TextColor); // Disabled comes first
        chip.IsEnabled = true;
        Assert.Equal(Colors.Red, chip.TextColor);

        chip.IsSelected = false; // the app
        Assert.Equal(normal, chip.TextColor);
        Assert.Equal(FontAttributes.None, chip.FontAttributes);
    }

    [Fact]
    public void ATapKeepsAOneWayBindingOfIsSelected()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var model = new Model { Selected = false };
        var chip = Chip("Late", SkUiChipKind.Filter);
        chip.SetBinding(SkUiChip.IsSelectedProperty, new Binding(nameof(Model.Selected), BindingMode.OneWay));
        chip.BindingContext = model;
        var root = Root(chip);
        Tap(root, Middle(chip));
        Assert.True(chip.IsSelected);
        model.Selected = true;
        model.Selected = false;
        Assert.False(chip.IsSelected); // the binding still runs
    }

    private sealed class Model : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public bool Selected
        {
            get;
            set
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
            }
        }
    }

    [Fact]
    public void InputChipsRemoveWithTheirCross()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var chip = Chip("Anna", SkUiChipKind.Input);
        var removed = 0;
        object? commanded = null;
        var clicked = 0;
        chip.Removed += (_, _) => removed++;
        chip.Clicked += (_, _) => clicked++;
        chip.CommandParameter = "anna@example.com";
        chip.RemoveCommand = new Command<object>(parameter => commanded = parameter);
        var root = Root(chip);
        Assert.True(chip.EffectiveShowsRemoveButton);
        var withCross = chip.Width;

        Tap(root, RemoveButton(chip));
        Assert.Equal(1, removed);
        Assert.Equal("anna@example.com", commanded);
        Assert.Equal(0, clicked);
        Tap(root, new Point(chip.Frame.X + 14, chip.Frame.Y + chip.Height / 2));
        Assert.Equal(1, clicked);

        Assert.False(chip.ShowsRemoveButton); // the value set: none yet (the kind shows the ×)
        Assert.False(new SkUiCoreChip().SetKind(SkUiChipKind.Input).ShowsRemoveButton); // the Core chip's getter agrees
        chip.ShowsRemoveButton = false;
        SkUiTestHelpers.Arrange(root, 300, 100);
        Assert.True(chip.Width < withCross);
        chip.ClearValue(SkUiChip.ShowsRemoveButtonProperty); // back to the kind's default
        Assert.True(chip.EffectiveShowsRemoveButton);
        Assert.True(Chip("Tag").SetShowsRemoveButton(true).EffectiveShowsRemoveButton);
        Assert.False(Chip("Tag").EffectiveShowsRemoveButton);
    }

    [Fact]
    public void PartsFollowTheSubtreeLooksPadding()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var chip = Chip("Anna", SkUiChipKind.Input);
        var removed = 0;
        chip.Removed += (_, _) => removed++;
        var root = Root(chip);
        root.Look = new WidePaddingLook();
        SkUiTestHelpers.Arrange(root, 300, 100);
        // The × sits inside the subtree look's 40 DIP padding and is its 30 DIPs wide: its middle, not the app look's (12 + 18 / 2).
        Tap(root, new Point(chip.Frame.Right - 40 - 15, chip.Frame.Y + chip.Height / 2));
        Assert.Equal(1, removed);
        // Left of the × by more than the subtree look's 10 DIP spacing: the chip clicks, it is not removed.
        Tap(root, new Point(chip.Frame.Right - 40 - 30 - 12, chip.Frame.Y + chip.Height / 2));
        Assert.Equal(1, removed);
    }

    private sealed class WidePaddingLook : DefaultSkUiLook
    {
        public override Thickness DefaultChipPadding => new(40, 4);
        public override float ChipIconSize => 30;
        public override float ChipIconSpacing => 10;
    }

    [Fact]
    public void TheCrossIsAtTheStartRightToLeft()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var chip = Chip("Anna", SkUiChipKind.Input);
        var removed = 0;
        chip.Removed += (_, _) => removed++;
        var root = Root(chip);
        root.FlowDirection = FlowDirection.RightToLeft;
        SkUiTestHelpers.Arrange(root, 300, 100);
        Tap(root, new Point(chip.Frame.X + 14, chip.Frame.Y + chip.Height / 2));
        Assert.Equal(1, removed);
    }

    [Fact]
    public void ABadgeTakesRoomAndIsReadAsTheValue()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var chip = Chip("Pending");
        var root = Root(chip);
        var plain = chip.Width;
        chip.BadgeText = "12";
        SkUiTestHelpers.Arrange(root, 300, 100);
        Assert.True(chip.Width > plain + SkUiLook.Current.ChipIconSpacing);
        Assert.True(chip.Height >= SkUiLook.Current.DefaultChipHeight);

        var node = SkUiSemanticsTree.Build(root).Flatten().Single();
        Assert.Equal(SkUiSemanticsRole.Button, node.Role);
        Assert.Equal("Pending", node.Label);
        Assert.Equal("12", node.Value);
    }

    [Fact]
    public void ANewBadgeTextIsMeasuredAndDrawnAfresh()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        SkUiChip Badged(string badge) => new SkUiChip
        {
            Text = "Overtime", BadgeText = badge, ShowsRemoveButton = true, FontFamily = SkUiTestHelpers.BundledFontFamily,
            BadgeFontFamily = SkUiTestHelpers.BundledFontFamily, HorizontalOptions = LayoutOptions.Start
        };

        var chip = Badged("w");
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = chip, Background = Colors.White }, 200, 40);
        surface.Frame();
        var narrow = chip.Width;
        chip.BadgeText = "1234"; // the bug: the old text's lines and width stayed
        using var changed = surface.Frame(16).Copy();
        Assert.True(chip.Width > narrow + 10, $"{narrow} → {chip.Width}");
        using var freshSurface = new SkUiTestSurface(new SkUiContentView { Content = Badged("1234"), Background = Colors.White }, 200, 40);
        var fresh = freshSurface.Frame();
        for (var y = 0; y < 40; y += 2)
        for (var x = 0; x < 200; x += 2)
            Assert.Equal(fresh.GetPixel(x, y), changed.GetPixel(x, y));
    }

    [Fact]
    public void TheBadgeTakesItsColorsAndFont()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var chip = Chip("Inbox");
        chip.BadgeText = "8";
        chip.BadgeColor = Colors.Red;
        chip.BadgeFontFamily = SkUiTestHelpers.BundledFontFamily;
        var root = Root(chip);
        var small = chip.Width;
        chip.BadgeFontSize = 22;
        SkUiTestHelpers.Arrange(root, 300, 100);
        Assert.True(chip.Width > small, $"{small} → {chip.Width}");

        root.Children.Clear();
        chip.BadgeFontSize = -1;
        chip.BadgeTextColor = Colors.Red; // text in the pill's color: the pill reads as one red block
        using var surfaceRed = new SkUiTestSurface(new SkUiContentView { Content = chip, Background = Colors.White }, 200, 40);
        var bitmap = surfaceRed.Frame();
        var reds = 0;
        for (var x = 0; x < 200; x++)
            if (bitmap.GetPixel(x, 20) == SKColors.Red)
                reds++;
        Assert.True(reds >= SkUiLook.Current.ChipIconSize - 2, $"{reds} red pixels across the badge");

        Assert.Equal(FontAttributes.Bold, chip.BadgeFontAttributes);
        Assert.Throws<ArgumentOutOfRangeException>(() => chip.SetBadgeFontSize(0));
        chip.BadgeFontSize = double.NaN; // ignored, as MAUI ignores invalid values
        Assert.Equal(-1, chip.BadgeFontSize);
        var core = new SkUiCoreChip().SetBadgeColor(Colors.Red).SetBadgeTextColor(Colors.White).SetBadgeFontSize(13).SetBadgeFontAttributes(FontAttributes.None);
        Assert.Equal(13, core.BadgeFontSize);
        Assert.Throws<ArgumentOutOfRangeException>(() => core.SetBadgeFontSize(-2));
    }

    [Fact]
    public void ScreenReadersReadFiltersAsCheckBoxesAndOfferRemove()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var filter = Chip("Approved", SkUiChipKind.Filter);
        filter.IsSelected = true;
        var input = Chip("Anna", SkUiChipKind.Input);
        var removed = 0;
        input.Removed += (_, _) => removed++;
        var root = new SkUiVerticalStackLayout { Children = { filter, input } };
        SkUiTestHelpers.Arrange(root, 300, 200);
        var owner = new SkUiSemanticsOwner(root);
        var nodes = owner.Tree.Flatten().ToList();
        Assert.Equal(SkUiSemanticsRole.CheckBox, nodes[0].Role);
        Assert.Equal(SkUiCheckState.Checked, nodes[0].CheckState);
        Assert.Equal([SkUiStrings.Current.RemoveText], nodes[1].CustomActions!);
        Assert.True(owner.PerformCustom(nodes[1].Id, 0));
        Assert.Equal(1, removed);

        Assert.True(input.Focus());
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Delete));
        Assert.Equal(2, removed);
        Assert.True(filter.Focus());
        Assert.False(root.FocusManager.KeyDown(SkUiKey.Backspace)); // no ×: the key goes on
    }

    [Fact]
    public void SelectedChipsFillWithTheirColorAndCoreChipsMatch()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var chip = Chip("Mine", SkUiChipKind.Filter);
        chip.IsSelected = true;
        chip.SelectedColor = Colors.Red;
        chip.WidthRequest = 120;
        chip.HeightRequest = 32;
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = chip, Background = Colors.White }, 120, 32);
        var expected = surface.Frame().Copy();
        Assert.Equal(SKColors.Red, expected.GetPixel(110, 6));

        var core = new SkUiCoreChip().SetKind(SkUiChipKind.Filter).SetIsSelected(true).SetSelectedColor(Colors.Red);
        core.SetText("Mine").SetFontFamily(SkUiTestHelpers.BundledFontFamily);
        core.SetWidth(120).SetHeight(32);
        using var coreSurface = new SkUiTestSurface(new SkUiCoreHost { Background = Colors.White }.SetContent(core), 120, 32);
        var actual = coreSurface.Frame();
        for (var y = 0; y < 32; y += 2)
        for (var x = 0; x < 120; x += 2)
            Assert.Equal(expected.GetPixel(x, y), actual.GetPixel(x, y));
    }

    [Fact]
    public void CoreChipsToggleAndRemove()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var filter = new SkUiCoreChip().SetKind(SkUiChipKind.Filter);
        filter.SetText("Late").SetFontFamily(SkUiTestHelpers.BundledFontFamily);
        var input = new SkUiCoreChip().SetKind(SkUiChipKind.Input);
        input.SetText("Anna").SetFontFamily(SkUiTestHelpers.BundledFontFamily);
        var removed = 0;
        input.Removed += (_, _) => removed++;
        var stack = new SkUiCoreVerticalStackLayout();
        stack.Add(filter.SetHorizontalAlignment(LayoutAlignment.Start));
        stack.Add(input.SetHorizontalAlignment(LayoutAlignment.Start));
        var host = new SkUiCoreHost().SetContent(stack);
        SkUiTestHelpers.Arrange(host, 300, 200);

        Tap(host, new Point(filter.Frame.Width / 2 - 4, filter.Frame.Height / 2));
        Assert.True(filter.IsSelected);
        Tap(host, new Point(input.Frame.Right - 14, input.Frame.Y + input.Frame.Height / 2));
        Assert.Equal(1, removed);
        Assert.Throws<ArgumentOutOfRangeException>(() => filter.SetKind((SkUiChipKind)9));
    }
}
