using System.Globalization;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The numeric entry (D16): the value shown formatted and edited plain, the input rules (digits, the culture's separator, the sign,
/// paste clean-up, decimal digits, overflow), commit and clamping, <c>AllowNull</c>, <c>ValueChangeMode</c>, the step buttons in
/// the field's row (placements, no focus, repeat), keys, the wheel, screen readers, the keyboard picked from the type and range,
/// the typed and Core twins. Each entry sets its culture: the tests do not depend on the machine's.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class NumericEntryTests
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    private sealed class TestProxy : ISkUiTextInputProxy
    {
        public readonly List<string> Calls = [];
        public ISkUiTextInputClient? Client;
        public bool ReceivesCharacters => false;
        public void Attach(ISkUiTextInputClient client) { Client = client; client.InputAttached(this); Calls.Add("attach"); }
        public void Detach(ISkUiTextInputClient client) { client.InputAttached(null); Client = null; Calls.Add("detach"); }
        public void ShowKeyboard(ISkUiTextInputClient client) => Calls.Add("show");
        public void HideKeyboard(ISkUiTextInputClient client) => Calls.Add("hide");
        public void StateChanged(ISkUiTextInputClient client, bool textChanged) { }
        public void OptionsChanged(ISkUiTextInputClient client) => Calls.Add("options");
        public void GeometryChanged(ISkUiTextInputClient client) { }
        public void ShowEditMenu(ISkUiTextInputClient client, Rect anchor, bool byTouch) { }
        public void HideEditMenu(ISkUiTextInputClient client) { }
    }

    private static (SkUiVerticalStackLayout Root, T Entry) Field<T>(T entry, TestProxy? proxy = null, double width = 300) where T : SkUiNumericEntryBase
    {
        entry.FontFamily = SkUiTestHelpers.BundledFontFamily;
        entry.Culture ??= English;
        var root = new SkUiVerticalStackLayout { Children = { entry } };
        SkUiTestHelpers.Arrange(root, width, 200);
        if (proxy is not null)
            root.FocusManager.TextInput = proxy;
        return (root, entry);
    }

    private static void Type(SkUiView root, string text)
    {
        foreach (var character in text)
            root.FocusManager.KeyDown(SkUiKey.Character, character: character);
    }

    private static void Key(SkUiView root, SkUiKey key, SkUiKeyModifiers modifiers = SkUiKeyModifiers.None) => root.FocusManager.KeyDown(key, modifiers);

    private static void Tap(SkUiView root, Point point, double atMs = 0)
    {
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, point, TimeSpan.FromMilliseconds(atMs)));
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, point, TimeSpan.FromMilliseconds(atMs + 30)));
    }

    private static Point Center(SKRect rect) => new(rect.MidX, rect.MidY);

    [Fact]
    public void ShowsTheValueFormattedAndThePlainNumberWhileEditedWithEverythingSelected()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (_, entry) = Field(new SkUiNumericEntry { Culture = German, Format = "N2", Value = 1234.5 });
        Assert.Equal("1.234,50", entry.Text);
        Assert.True(entry.Focus());
        Assert.Equal("1234,5", entry.Text);
        Assert.Equal((0, 6), (entry.CursorPosition, entry.SelectionLength));
        entry.Unfocus();
        Assert.Equal("1.234,50", entry.Text);
        entry.Value = null;
        Assert.Equal(string.Empty, entry.Text);
        // The format does not round the value.
        entry.Value = 0.126;
        Assert.Equal(("0,13", 0.126), (entry.Text, entry.Value));
    }

    [Fact]
    public void TypedTextIsFilteredAndCommittedOnReturnAndFocusLossClampedOnlyThen()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiNumericEntry<int> { Minimum = 10, Maximum = 20 });
        var changes = new List<(int?, int?)>();
        entry.ValueChanged += (_, args) => changes.Add((args.OldValue, args.NewValue));
        Assert.True(entry.Focus());
        Type(root, "1a5");
        // Letters are refused; the 1 of 15 is typeable below the minimum.
        Assert.Equal("15", entry.Text);
        Assert.Null(entry.Value);
        var completed = 0;
        entry.Completed += (_, _) => completed++;
        Key(root, SkUiKey.Enter);
        Assert.Equal((15, 1), (entry.Value, completed));
        entry.Model.Editor.SelectAll();
        Type(root, "007");
        Assert.Equal("007", entry.Text);
        entry.Unfocus();
        // Clamped on commit, leading zeros gone.
        Assert.Equal((10, "10"), (entry.Value, entry.Text));
        Assert.Equal([(null, 15), (15, 10)], changes);
    }

    [Fact]
    public void TheDecimalSeparatorIsTypedOnceAndGetsALeadingZero()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiNumericEntry { Culture = German, MaximumDecimalDigits = 2 });
        Assert.True(entry.Focus());
        // Both '.' and ',' type the culture's separator; first, it gets a 0.
        Type(root, ".5");
        Assert.Equal("0,5", entry.Text);
        Type(root, "25");
        // A third decimal digit is refused.
        Assert.Equal("0,52", entry.Text);
        entry.CursorPosition = 0;
        Type(root, ",");
        // The second separator steps the caret over the one there.
        Assert.Equal(("0,52", 2), (entry.Text, entry.CursorPosition));
        Key(root, SkUiKey.Enter);
        Assert.Equal(0.52, entry.Value);
    }

    [Fact]
    public void IntegersRefuseTheSeparatorAndWhatWouldOverflowTheType()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiNumericEntry<byte>());
        Assert.True(entry.Focus());
        Type(root, "2,5-");
        Assert.Equal("25", entry.Text);
        Type(root, "6");
        Assert.Equal("25", entry.Text);
        Type(root, "5");
        Assert.Equal("255", entry.Text);
        entry.Unfocus();
        Assert.Equal((byte)255, entry.Value);
    }

    [Fact]
    public void TheSignTogglesWhereverTheCaretIsAndOnlyWithANegativeMinimum()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiNumericEntry<int> { Value = 123 });
        Assert.True(entry.Focus());
        entry.Model.Editor.Select(2, 2);
        Type(root, "-");
        Assert.Equal(("-123", 3), (entry.Text, entry.CursorPosition));
        // Nothing goes before the sign.
        entry.Model.Editor.Select(0, 0);
        Type(root, "4");
        Assert.Equal("-4123", entry.Text);
        Type(root, "+");
        Assert.Equal("4123", entry.Text);
        entry.Unfocus();
        Assert.Equal(4123, entry.Value);
        var (positiveRoot, positive) = Field(new SkUiNumericEntry<int> { Minimum = 0, Value = 5 });
        Assert.True(positive.Focus());
        Type(positiveRoot, "-");
        Assert.Equal("5", positive.Text);
    }

    [Fact]
    public void PastedTextIsCleanedOrRefused()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (_, entry) = Field(new SkUiNumericEntry { Culture = German });
        Assert.True(entry.Focus());
        var editor = entry.Model.Editor;
        editor.InsertText("1 234,50 €", SkUiTextEditKind.Other);
        Assert.Equal("1234,50", entry.Text);
        editor.SelectAll();
        editor.InsertText("abc", SkUiTextEditKind.Other);
        Assert.Equal("1234,50", entry.Text);
        editor.SelectAll();
        editor.InsertText("-1.000,5", SkUiTextEditKind.Other);
        Assert.Equal("-1000,5", entry.Text);
        entry.Unfocus();
        Assert.Equal(-1000.5, entry.Value);
    }

    [Fact]
    public void OnKeystrokeTakesEachNumberInTheRange()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiNumericEntry<int> { Minimum = 10, Maximum = 99, ValueChangeMode = SkUiValueChangeMode.OnKeystroke });
        Assert.True(entry.Focus());
        Type(root, "1");
        // Below the minimum while typing: not yet.
        Assert.Null(entry.Value);
        Type(root, "2");
        Assert.Equal((12, "12"), (entry.Value, entry.Text));
        Key(root, SkUiKey.Backspace);
        Key(root, SkUiKey.Backspace);
        Assert.Null(entry.Value);
    }

    [Fact]
    public void WithoutAllowNullEmptyCommitsZeroWithinTheRange()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiNumericEntry<int> { Minimum = 5, Maximum = 50, AllowNull = false });
        Assert.Equal((5, "5"), (entry.Value, entry.Text));
        entry.Value = null;
        Assert.Equal(5, entry.Value);
        Assert.True(entry.Focus());
        entry.Model.Editor.SelectAll();
        Key(root, SkUiKey.Backspace);
        Key(root, SkUiKey.Enter);
        Assert.Equal((5, "5"), (entry.Value, entry.Text));
    }

    [Fact]
    public void AppValuesAreClampedAndComeBackWhenTheRangeWidens()
    {
        var (_, entry) = Field(new SkUiNumericEntry<decimal> { Maximum = 10, MaximumDecimalDigits = 1 });
        entry.Value = 25.25m;
        Assert.Equal(10m, entry.Value);
        entry.Maximum = 100;
        Assert.Equal(25.3m, entry.Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => entry.SetMinimum(200));
        entry.MaximumDecimalDigits = 99;
        Assert.Equal(1, entry.MaximumDecimalDigits);
    }

    [Fact]
    public void EndButtonsStepWithoutFocusingAndInsetTheText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var proxy = new TestProxy();
        var (root, entry) = Field(new SkUiNumericEntry<int> { Minimum = 0, Maximum = 2, ButtonPlacement = SkUiStepButtonPlacement.End }, proxy);
        var (minus, plus) = entry.Model.StepButtonBounds();
        var button = SkUiLook.Current.TextFieldStepButtonSize.Width;
        Assert.Equal(entry.Width, plus.Right, 3);
        Assert.Equal(button, plus.Width, 3);
        Assert.True(minus.Right <= plus.Left + 0.01);
        // The text box ends before the buttons and the padding.
        Assert.True(entry.Model.GetTextBounds().Right <= minus.Left - SkUiLook.Current.TextFieldPadding.Right + 0.01);
        Tap(root, Center(plus));
        Assert.Equal(1, entry.Value);
        Assert.False(entry.IsFocused);
        Assert.DoesNotContain("show", proxy.Calls);
        Tap(root, Center(plus), 1000);
        Assert.Equal(2, entry.Value);
        Assert.False(((ISkUiTextFieldStepOwner)entry.Numeric).CanStep(1));
        Tap(root, Center(plus), 2000);
        Assert.Equal(2, entry.Value);
        Tap(root, Center(minus), 3000);
        Assert.Equal(1, entry.Value);
    }

    [Fact]
    public void AStepCommitsTheTypedTextFirstAndKeepsFocus()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiNumericEntry<int> { ButtonPlacement = SkUiStepButtonPlacement.Split });
        Assert.True(entry.Focus());
        Type(root, "41");
        var (_, plus) = entry.Model.StepButtonBounds();
        Tap(root, Center(plus));
        Assert.Equal((42, "42"), (entry.Value, entry.Text));
        Assert.True(entry.IsFocused);
    }

    [Fact]
    public void AHeldButtonRepeatsAndAutoReverseWraps()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var (root, entry) = Field(new SkUiNumericEntry<int> { Minimum = 0, Maximum = 3, Value = 0, ButtonPlacement = SkUiStepButtonPlacement.Start });
        var (_, plus) = entry.Model.StepButtonBounds();
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, Center(plus), TimeSpan.Zero, Device: SkUiPointerDevice.Mouse));
        Assert.Equal(1, entry.Value);
        TestDispatcherProvider.RunDelayed();
        TestDispatcherProvider.RunDelayed();
        Assert.Equal(3, entry.Value);
        // At the end it stops.
        TestDispatcherProvider.RunDelayed();
        Assert.Equal(3, entry.Value);
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, Center(plus), TimeSpan.FromSeconds(1), Device: SkUiPointerDevice.Mouse));
        entry.AutoReverse = true;
        Assert.True(((ISkUiTextFieldStepOwner)entry.Numeric).CanStep(1));
        Assert.True(entry.Numeric.Step(1, large: false));
        Assert.Equal(0, entry.Value);
        Assert.True(entry.Numeric.Step(-1, large: false));
        Assert.Equal(3, entry.Value);
    }

    [Fact]
    public void PlacementsAndOrientationsLayTheButtonsOut()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var size = SkUiLook.Current.TextFieldStepButtonSize;
        var (_, split) = Field(new SkUiNumericEntry { ButtonPlacement = SkUiStepButtonPlacement.Split });
        var (minus, plus) = split.Model.StepButtonBounds();
        Assert.Equal((0f, (float)split.Width), (minus.Left, plus.Right));
        var (_, spinner) = Field(new SkUiNumericEntry { ButtonPlacement = SkUiStepButtonPlacement.End, ButtonOrientation = StackOrientation.Vertical });
        (minus, plus) = spinner.Model.StepButtonBounds();
        Assert.Equal(plus.Left, minus.Left);
        Assert.True(plus.Bottom <= minus.Top + 0.01);
        Assert.Equal(size.Width, plus.Width, 3);
        var plain = Field(new SkUiNumericEntry()).Entry;
        var (_, bars) = Field(new SkUiNumericEntry { ButtonPlacement = SkUiStepButtonPlacement.Split, ButtonOrientation = StackOrientation.Vertical });
        (minus, plus) = bars.Model.StepButtonBounds();
        // Bars across the field above and below the text: the field grows by them.
        Assert.Equal(((float)bars.Width, 0f), (plus.Width, plus.Top));
        Assert.Equal((float)bars.Height, minus.Bottom, 3);
        Assert.True(bars.Height >= 2 * size.Height + SkUiLook.Current.TextFieldPadding.VerticalThickness + plain.Model.Layout.Height - 0.5);
        var (_, rtl) = Field(new SkUiNumericEntry { ButtonPlacement = SkUiStepButtonPlacement.End, FlowDirection = FlowDirection.RightToLeft });
        (minus, plus) = rtl.Model.StepButtonBounds();
        // The end side is the left; − before + in reading order (right to left).
        Assert.Equal(0f, plus.Left);
        Assert.True(minus.Left >= plus.Right - 0.01);
    }

    [Fact]
    public void ButtonTextsReplaceTheGlyphsAndAreDrawn()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        SkUiTextFieldStepButtonsPaint? painted = null;
        var previous = SkUiLook.Current;
        SkUiLook.Current = new DefaultSkUiLook { TextFieldStepButtonsPainter = (_, paint) => painted = paint };
        SkUiNumericEntry entry;
        try
        {
            entry = Field(new SkUiNumericEntry { ButtonPlacement = SkUiStepButtonPlacement.End, IncrementText = "More" }).Entry;
            using var surface = SKSurface.Create(new SKImageInfo((int)entry.Width, (int)entry.Height));
            entry.Model.Draw(surface.Canvas, (float)entry.Width, (float)entry.Height);
        }
        finally
        {
            SkUiLook.Current = previous;
        }
        Assert.NotNull(painted);
        Assert.True(painted!.Value.HasIncrementText);
        Assert.False(painted.Value.HasDecrementText);
        // A text wider than the look's button widens both.
        Assert.True(painted.Value.IncrementBounds.Width > previous.TextFieldStepButtonSize.Width);
        Assert.Equal(painted.Value.IncrementBounds.Width, painted.Value.DecrementBounds.Width, 3);
    }

    [Fact]
    public void KeysStepCommitAndRevert()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiNumericEntry<int> { Value = 5, Increment = 2 });
        Assert.True(entry.Focus());
        Key(root, SkUiKey.Up);
        Assert.Equal((7, "7"), (entry.Value, entry.Text));
        Key(root, SkUiKey.PageDown);
        Assert.Equal(-13, entry.Value);
        entry.LargeIncrement = 3;
        Key(root, SkUiKey.PageUp);
        Assert.Equal(-10, entry.Value);
        Key(root, SkUiKey.Down);
        Assert.Equal(-12, entry.Value);
        entry.Model.Editor.SelectAll();
        Type(root, "99");
        Key(root, SkUiKey.Escape);
        Assert.Equal(("-12", -12), (entry.Text, entry.Value));
        Assert.Equal((0, 3), (entry.CursorPosition, entry.SelectionLength));
    }

    [Fact]
    public void TheWheelStepsOnlyWhileFocused()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiNumericEntry<int> { Value = 0 });
        var point = new Point(20, entry.Height / 2);
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Wheel, point, WheelDelta: 40, Device: SkUiPointerDevice.Mouse));
        Assert.Equal(0, entry.Value);
        Assert.True(entry.Focus());
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Wheel, point, WheelDelta: 40, Device: SkUiPointerDevice.Mouse));
        Assert.Equal(2, entry.Value);
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Wheel, point, WheelDelta: -10, Device: SkUiPointerDevice.Mouse));
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Wheel, point, WheelDelta: -10, Device: SkUiPointerDevice.Mouse));
        Assert.Equal(1, entry.Value);
    }

    [Fact]
    public void ScreenReadersGetATextFieldThatIncrementsAndDecrements()
    {
        var (_, entry) = Field(new SkUiNumericEntry<int> { Minimum = 0, Maximum = 1, Value = 0 });
        var info = new SkUiSemanticsInfo();
        ((ISkUiAccessibleNode)entry).GetSemantics(info);
        Assert.Equal(SkUiSemanticsRole.TextField, info.Role);
        Assert.Equal("0", info.Value);
        Assert.True(info.Actions.HasFlag(SkUiSemanticsActions.Increment));
        Assert.False(info.Actions.HasFlag(SkUiSemanticsActions.Decrement));
        Assert.True(((ISkUiAccessibleNode)entry).PerformSemanticsAction(SkUiSemanticsActions.Increment));
        Assert.Equal(1, entry.Value);
        Assert.False(((ISkUiAccessibleNode)entry).PerformSemanticsAction(SkUiSemanticsActions.Increment));
    }

    [Fact]
    public void TheKeyboardFollowsTheTypeAndTheRange()
    {
        var (_, integer) = Field(new SkUiNumericEntry<uint>());
        Assert.Equal(SkUiNumberKeyboard.Digits, integer.Model.Options.NumberKeyboard);
        Assert.False(integer.Model.Options.IsTextPredictionEnabled);
        var (_, signed) = Field(new SkUiNumericEntry());
        Assert.Equal(SkUiNumberKeyboard.Digits | SkUiNumberKeyboard.Decimal | SkUiNumberKeyboard.Signed, signed.Model.Options.NumberKeyboard);
        signed.Minimum = 0;
        Assert.Equal(SkUiNumberKeyboard.Digits | SkUiNumberKeyboard.Decimal, signed.Model.Options.NumberKeyboard);
        signed.MaximumDecimalDigits = 0;
        Assert.Equal(SkUiNumberKeyboard.Digits, signed.Model.Options.NumberKeyboard);
    }

    [Fact]
    public void NotEditableStepsWithoutTypingAndReadOnlyChangesNothing()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiNumericEntry<int> { Value = 1, IsEditable = false, Format = "0 pcs" });
        Assert.True(entry.Focus());
        // Still formatted: it is not edited.
        Assert.Equal("1 pcs", entry.Text);
        Type(root, "5");
        Assert.Equal("1 pcs", entry.Text);
        Key(root, SkUiKey.Up);
        Assert.Equal(2, entry.Value);
        Assert.False(entry.IsReadOnly);
        entry.IsReadOnly = true;
        Key(root, SkUiKey.Up);
        Assert.Equal(2, entry.Value);
        entry.IsReadOnly = false;
        entry.IsEditable = true;
        Assert.Equal("2", entry.Text);
        Type(root, "5");
        Assert.Equal("25", entry.Text);
    }

    [Fact]
    public void ATapThatFocusesKeepsEverythingSelected()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiNumericEntry<int> { Value = 1234 });
        Tap(root, new Point(30, entry.Height / 2));
        Assert.True(entry.IsFocused);
        Assert.Equal((0, 4), (entry.CursorPosition, entry.SelectionLength));
        Type(root, "7");
        Assert.Equal("7", entry.Text);
    }

    [Fact]
    public void AppTextIsCommittedAsTypedText()
    {
        var (_, entry) = Field(new SkUiNumericEntry<int> { Maximum = 10 });
        entry.SetText("42");
        Assert.Equal((10, "10"), (entry.Value, entry.Text));
        entry.SetText("x");
        Assert.Equal((10, "10"), (entry.Value, entry.Text));
    }

    [Fact]
    public void BinaryFloatingPointStepsAreRoundedByTheIncrement()
    {
        var (_, entry) = Field(new SkUiNumericEntry<float> { Value = 0.1f, Increment = 0.1f });
        entry.Numeric.Step(1, large: false);
        entry.Numeric.Step(1, large: false);
        Assert.Equal(0.3f, entry.Value);
        Assert.Equal("0.3", entry.Text);
        var (_, big) = Field(new SkUiNumericEntry { Value = 1e20 });
        Assert.True(big.Focus());
        Assert.Equal("100000000000000000000", big.Text);
    }

    [Fact]
    public void CoreNumericEntryWorksLikeTheSkUiOne()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var core = new SkUiCoreNumericEntry<int>().SetRange(0, 50).SetButtonPlacement(SkUiStepButtonPlacement.End).SetCulture(English)
            .SetFontFamily(SkUiTestHelpers.BundledFontFamily);
        var root = new SkUiVerticalStackLayout { Children = { new SkUiCoreHost().SetContent(core) } };
        SkUiTestHelpers.Arrange(root, 300, 200);
        var changes = 0;
        core.ValueChanged += (_, _) => changes++;
        var (_, plus) = core.Model.StepButtonBounds();
        // From no value: one step from 0.
        Tap(root, Center(plus));
        Assert.Equal((1, "1"), (core.Value, core.Text));
        Assert.False(core.IsFocused);
        Assert.True(core.Focus());
        Type(root, "77");
        Key(root, SkUiKey.Enter);
        Assert.Equal((50, "50"), (core.Value, core.Text));
        core.SetMaximum(40);
        Assert.Equal(40, core.Value);
        core.SetMaximum(60);
        // The user's 77 was clamped by the commit: the value asked for was 50.
        Assert.Equal(50, core.Value);
        Assert.Equal(4, changes);
        var plainCore = new SkUiCoreNumericEntry().SetFormat("F1").SetCulture(German).SetValue(2.26);
        Assert.Equal("2,3", plainCore.Text);
        Assert.Throws<ArgumentOutOfRangeException>(() => core.SetMaximumDecimalDigits(-1));
    }
}

/// <summary>Numeric entries in XAML: the <see cref="double"/> one and a typed one through <c>x:TypeArguments</c>.</summary>
[Collection(RuntimeXamlCollection.Name)]
public class NumericEntryXamlTests
{
    [Fact]
    public void NumericEntriesLoadFromXaml()
    {
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <VerticalStackLayout>
                <sk:SkUiNumericEntry Value="2.5" Minimum="0" Maximum="10" Increment="0.5" Format="N1" ButtonPlacement="End" />
                <sk:SkUiNumericEntry x:TypeArguments="x:Int32" Value="7" Minimum="-5" ButtonPlacement="Split" ButtonOrientation="Vertical" AllowNull="False" />
              </VerticalStackLayout>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var children = ((VerticalStackLayout)root.Content).Children;
        var plain = Assert.IsType<SkUiNumericEntry>(children[0]);
        Assert.Equal((2.5, 0d, 10d, 0.5, SkUiStepButtonPlacement.End), (plain.Value, plain.Minimum, plain.Maximum, plain.Increment, plain.ButtonPlacement));
        var typed = Assert.IsType<SkUiNumericEntry<int>>(children[1]);
        Assert.Equal((7, -5, StackOrientation.Vertical, false), (typed.Value, typed.Minimum, typed.ButtonOrientation, typed.AllowNull));
    }
}
