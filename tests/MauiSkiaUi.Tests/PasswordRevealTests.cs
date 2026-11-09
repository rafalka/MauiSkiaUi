using System.ComponentModel;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// E1: a drawn password field shows its text while <c>IsPasswordRevealed</c> (the reveal button, a screen reader's activation, or
/// the app), stays a password field for the platform, keeps the caret and selection on the same characters, and is masked again
/// when it loses focus (<c>HidesPasswordOnUnfocus</c>), on both layers.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PasswordRevealTests : IDisposable
{
    private const string Secret = "pa👍s";
    private const string Bullets = "••••";
    private const string FieldId = "Password";
    private const string RevealId = FieldId + SkUiAccessibility.PasswordRevealButtonAutomationIdSuffix;

    private readonly TestUiContext _ui = TestUiContext.Install();
    private static int _pointer;

    public void Dispose() => _ui.Dispose();

    private sealed class Credentials
    {
        public bool Revealed { get; set; }
    }

    private static SkUiVerticalStackLayout Root(SkUiView view)
    {
        var root = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        root.Children.Add(view);
        SkUiTestHelpers.Arrange(root, 300, 300);
        return root;
    }

    private static SkUiEntry Password(bool button = true) => new()
    {
        Text = Secret, IsPassword = true, ShowsPasswordRevealButton = button, AutomationId = FieldId, FontFamily = SkUiTestHelpers.BundledFontFamily
    };

    private static SkUiCoreEntry CorePassword()
    {
        var core = new SkUiCoreEntry().SetText(Secret).SetIsPassword(true).SetShowsPasswordRevealButton(true);
        core.SetFontFamily(SkUiTestHelpers.BundledFontFamily);
        core.SetAutomationId(FieldId);
        return core;
    }

    private static SkUiSemanticsNode? Reveal(SkUiView root) =>
        SkUiSemanticsTree.Build(root).Flatten().SingleOrDefault(node => node.AutomationId == RevealId);

    private static string Shown(SkUiTextFieldModel model)
    {
        model.Measure(300, 200);
        return model.Layout.Text;
    }

    private static SkUiSemanticsInfo Semantics(ISkUiAccessibleNode node)
    {
        var info = new SkUiSemanticsInfo();
        node.GetSemantics(info);
        return info;
    }

    private static void Tap(SkUiView root, Point at, SkUiPointerDevice device = SkUiPointerDevice.Touch)
    {
        var id = ++_pointer;
        root.Touch(new SkUiTouchEvent(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(id), Device: device));
        root.Touch(new SkUiTouchEvent(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(id) + TimeSpan.FromMilliseconds(30), Device: device));
    }

    [Fact]
    public void ARevealedPasswordIsDrawnAndReadButStaysAPasswordForThePlatform()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = Password(button: false);
        Root(entry);
        Assert.Equal(Bullets, Shown(entry.Model));

        entry.IsPasswordRevealed = true;
        Assert.Equal(Secret, Shown(entry.Model));
        var info = Semantics(entry);
        Assert.Equal(Secret, info.Value);
        Assert.False(info.TextField!.Value.IsPassword); // read as text while it shows
        // The keyboard, autofill and password managers still see a password field, and copying stays off.
        var options = ((ISkUiTextInputClient)entry.Model).Options;
        Assert.True(options.IsPassword);
        Assert.Equal(SkUiAutofillHints.Password, options.AutofillHint);
        Assert.False(options.IsTextPredictionEnabled);
        entry.Model.Editor.SelectAll();
        Assert.False(entry.Model.CanPerformEditAction(SkUiTextEditAction.Copy));
        Assert.False(entry.Model.CanPerformEditAction(SkUiTextEditAction.Cut));

        entry.IsPasswordRevealed = false;
        Assert.Equal(Bullets, Shown(entry.Model));
        Assert.Equal(Bullets, Semantics(entry).Value);
    }

    [Fact]
    public void RevealingRestartsNothingAndKeepsTheCaretAndSelection()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = Password(button: false);
        var root = Root(entry);
        entry.Focus();
        var calls = new List<string>();
        root.FocusManager.TextInput = new RecordingProxy(calls);
        entry.Focus();
        // From after "p" to after the emoji (a surrogate pair): bullets 1..3, characters 1..4.
        entry.Model.Editor.Select(1, 4);
        Shown(entry.Model);
        var masked = Semantics(entry).TextField!.Value;
        Assert.Equal((1, 3), (masked.SelectionStart, masked.SelectionEnd));
        calls.Clear();

        entry.IsPasswordRevealed = true;
        Assert.Equal((1, 4), (entry.Model.Editor.SelectionStart, entry.Model.Editor.SelectionEnd));
        Assert.Equal((1, 3), (entry.CursorPosition, entry.SelectionLength));
        var revealed = Semantics(entry).TextField!.Value;
        Assert.Equal((1, 4), (revealed.SelectionStart, revealed.SelectionEnd));
        Assert.DoesNotContain("options", calls); // the platform's input keeps going as a password field
        Assert.DoesNotContain("text", calls);
    }

    [Fact]
    public void TheRevealButtonTogglesWithoutTakingFocus()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = Password();
        var root = Root(entry);
        var changes = new List<string?>();
        entry.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        var button = Reveal(root)!;
        // At the field's end, as wide as the look's button.
        Assert.Equal(SkUiLook.Current.TextFieldPasswordRevealButtonWidth, button.Bounds.Width, 3);
        Assert.True(button.Bounds.Right <= button.Parent!.Bounds.Right + 0.5);

        Tap(root, button.Bounds.Center);
        Assert.True(entry.IsPasswordRevealed);
        Assert.False(entry.IsFocused); // no keyboard comes up
        Assert.Contains(nameof(SkUiEntry.IsPasswordRevealed), changes);
        Assert.Equal(Secret, Shown(entry.Model));

        Tap(root, button.Bounds.Center, SkUiPointerDevice.Mouse);
        Assert.False(entry.IsPasswordRevealed);

        // A mouse press dragged off the button and released does nothing.
        var id = ++_pointer;
        root.Touch(new SkUiTouchEvent(id, SkUiTouchAction.Pressed, button.Bounds.Center, TimeSpan.FromSeconds(id), Device: SkUiPointerDevice.Mouse));
        root.Touch(new SkUiTouchEvent(id, SkUiTouchAction.Moved, new Point(button.Parent.Bounds.Left + 20, button.Bounds.Center.Y),
            TimeSpan.FromSeconds(id) + TimeSpan.FromMilliseconds(20), Device: SkUiPointerDevice.Mouse));
        root.Touch(new SkUiTouchEvent(id, SkUiTouchAction.Released, new Point(button.Parent.Bounds.Left + 20, button.Bounds.Center.Y),
            TimeSpan.FromSeconds(id) + TimeSpan.FromMilliseconds(40), Device: SkUiPointerDevice.Mouse));
        Assert.False(entry.IsPasswordRevealed);

        // Elsewhere, a tap focuses the field as before.
        Tap(root, new Point(button.Parent.Bounds.Left + 20, button.Bounds.Center.Y));
        Assert.True(entry.IsFocused);
        Assert.False(entry.IsPasswordRevealed);
    }

    [Fact]
    public void TheButtonShowsOnlyOnAPasswordFieldThatAsksForIt()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = Password(button: false);
        var root = Root(entry);
        Assert.Null(Reveal(root));
        var plainWidth = entry.Model.Measure(double.PositiveInfinity, 200).Width;

        entry.ShowsPasswordRevealButton = true;
        Assert.NotNull(Reveal(root));
        // The text box gives the button its room.
        Assert.Equal(plainWidth + SkUiLook.Current.TextFieldPasswordRevealButtonWidth, entry.Model.Measure(double.PositiveInfinity, 200).Width, 3);

        entry.IsPassword = false;
        Assert.Null(Reveal(root));
        Assert.Equal(Secret, Shown(entry.Model));
    }

    [Fact]
    public void LosingFocusMasksTheTextAgainUnlessTurnedOff()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = Password();
        var model = new Credentials();
        entry.BindingContext = model;
        entry.SetBinding(SkUiEntry.IsPasswordRevealedProperty, nameof(Credentials.Revealed)); // two-way by default
        var root = Root(entry);
        entry.Focus();
        Tap(root, Reveal(root)!.Bounds.Center);
        Assert.True(model.Revealed);

        entry.Unfocus();
        Assert.False(entry.IsPasswordRevealed);
        Assert.False(model.Revealed);
        Assert.Equal(Bullets, Shown(entry.Model));

        entry.HidesPasswordOnUnfocus = false;
        entry.Focus();
        entry.IsPasswordRevealed = true;
        entry.Unfocus();
        Assert.True(entry.IsPasswordRevealed);
        Assert.True(model.Revealed);
    }

    [Fact]
    public void ScreenReadersReadTheButtonsStateAndActivateIt()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = Password();
        var root = Root(entry);
        var button = Reveal(root)!;
        Assert.Equal(SkUiSemanticsRole.Button, button.Role);
        Assert.Equal(SkUiStrings.Current.ShowPasswordText, button.Label);
        Assert.Equal(SkUiSemanticsActions.Activate, button.Actions);

        var owner = new SkUiSemanticsOwner(root);
        Assert.True(owner.Perform(button.Id, SkUiSemanticsActions.Activate));
        Assert.True(entry.IsPasswordRevealed);
        var hide = Reveal(root)!;
        Assert.Equal(button.Id, hide.Id); // a stable id
        Assert.Equal(SkUiStrings.Current.HidePasswordText, hide.Label);

        // A disabled field keeps the button drawn (dimmed) but offers no element and ignores taps.
        entry.IsEnabled = false;
        Assert.Null(Reveal(root));
        Assert.False(owner.Perform(button.Id, SkUiSemanticsActions.Activate));
        Tap(root, hide.Bounds.Center);
        Assert.True(entry.IsPasswordRevealed);
    }

    [Fact]
    public void TheLookDrawsTheButtonWithTheState()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var look = SkUiLook.Current;
        var painted = new List<SkUiPasswordRevealButtonPaint>();
        look.TextFieldPasswordRevealButtonPainter = (_, paint) => painted.Add(paint);
        try
        {
            var entry = Password();
            using var surface = new SkUiTestSurface(new SkUiContentView { Content = entry }, 300, 60);
            surface.Frame();
            var masked = Assert.Single(painted);
            Assert.False(masked.IsRevealed);
            Assert.True(masked.IsEnabled);
            Assert.Equal((float)look.TextFieldPasswordRevealButtonWidth, masked.Bounds.Width, 3);

            entry.IsPasswordRevealed = true;
            surface.Frame();
            Assert.True(painted[^1].IsRevealed);
        }
        finally
        {
            look.TextFieldPasswordRevealButtonPainter = null;
        }
    }

    [Fact]
    public void TheDefaultGlyphDiffersBetweenTheStates()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = Password();
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = entry }, 300, 60);
        using var masked = surface.Frame().Copy();
        entry.IsPasswordRevealed = true;
        using var revealed = surface.Frame().Copy();
        // The text and the eye's slash change; the field's start (its chrome and padding) does not.
        Assert.NotEqual(Pixels(masked, 250, 299), Pixels(revealed, 250, 299));
        Assert.Equal(masked.GetPixel(1, 30), revealed.GetPixel(1, 30));
    }

    private static string Pixels(SKBitmap bitmap, int from, int to)
    {
        var builder = new System.Text.StringBuilder();
        for (var x = from; x < to; x++)
            for (var y = 0; y < bitmap.Height; y += 2)
                builder.Append(bitmap.GetPixel(x, y).Alpha > 0 ? bitmap.GetPixel(x, y).Red : 0).Append(',');
        return builder.ToString();
    }

    [Fact]
    public void ACoreEntryRevealsTheSameWay()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var core = CorePassword();
        var root = Root(new SkUiCoreHost().SetContent(core));
        var changes = new List<string?>();
        ((INotifyPropertyChanged)core).PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        Assert.Equal(Bullets, Shown(core.Model));

        Tap(root, Reveal(root)!.Bounds.Center);
        Assert.True(core.IsPasswordRevealed);
        Assert.Equal([nameof(SkUiCoreEntry.IsPasswordRevealed)], changes);
        Assert.Equal(Secret, Shown(core.Model));
        Assert.True(((ISkUiTextInputClient)core.Model).Options.IsPassword);
        Assert.Equal(SkUiStrings.Current.HidePasswordText, Reveal(root)!.Label);

        // Focus lost: masked again; turned off, it stays.
        Assert.True(core.Focus());
        core.Unfocus();
        Assert.False(core.IsPasswordRevealed);
        Assert.Equal(Bullets, Shown(core.Model));
        core.SetHidesPasswordOnUnfocus(false).SetIsPasswordRevealed(true);
        Assert.True(core.Focus());
        core.Unfocus();
        Assert.True(core.IsPasswordRevealed);

        changes.Clear();
        core.SetIsPasswordRevealed(true).SetShowsPasswordRevealButton(true); // unchanged: no notification
        Assert.Empty(changes);
        core.SetShowsPasswordRevealButton(false);
        Assert.Equal([nameof(SkUiCoreEntry.ShowsPasswordRevealButton)], changes);
        Assert.Null(Reveal(root));
    }

    /// <summary>A text input proxy that records what the field asked of it.</summary>
    private sealed class RecordingProxy(List<string> calls) : ISkUiTextInputProxy
    {
        public bool ReceivesCharacters => false;
        public void Attach(ISkUiTextInputClient client) => client.InputAttached(this);
        public void Detach(ISkUiTextInputClient client) => client.InputAttached(null);
        public void ShowKeyboard(ISkUiTextInputClient client) { }
        public void HideKeyboard(ISkUiTextInputClient client) { }
        public void StateChanged(ISkUiTextInputClient client, bool textChanged) => calls.Add(textChanged ? "text" : "selection");
        public void OptionsChanged(ISkUiTextInputClient client) => calls.Add("options");
        public void GeometryChanged(ISkUiTextInputClient client) { }
        public void ShowEditMenu(ISkUiTextInputClient client, Rect anchor, bool byTouch) { }
        public void HideEditMenu(ISkUiTextInputClient client) { }
        public void ShowMagnifier(ISkUiTextInputClient client, Rect caret) { }
        public void HideMagnifier(ISkUiTextInputClient client) { }
    }
}
