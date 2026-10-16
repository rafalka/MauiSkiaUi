using System.ComponentModel;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Validation chrome of drawn text fields (D10): <c>HasError</c>, <c>ErrorText</c> and <c>HelperText</c> on both layers, the message
/// band under the field, the look's error frame, the validity visual states and what screen readers are told. In the global
/// collection: the tests set the shared look's painter and the keyboard / pointer mode is process-wide.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class TextFieldValidationTests
{
    private const double Width = 300;
    private const double Tolerance = 0.01;
    private static readonly double MinimumHeight = SkUiLook.Current.DefaultTextFieldMinimumHeight;
    private static readonly double Spacing = SkUiLook.Current.TextFieldMessageSpacing;
    private static readonly Thickness Padding = SkUiLook.Current.TextFieldPadding;

    private static (SkUiVerticalStackLayout Root, T Field) Field<T>(T field) where T : SkUiInputView
    {
        field.FontFamily = SkUiTestHelpers.BundledFontFamily;
        var root = new SkUiVerticalStackLayout { Children = { field } };
        SkUiTestHelpers.Arrange(root, Width, 400);
        return (root, field);
    }

    // The height of one line of message text (the label engine at the look's message size).
    private static double MessageLine()
    {
        var label = new SkUiLabel { Text = "x", FontFamily = SkUiTestHelpers.BundledFontFamily, FontSize = SkUiLook.Current.TextFieldMessageFontSize };
        return label.Measure(Width, double.PositiveInfinity).Height;
    }

    private static void Tap(SkUiView root, Point point, double seconds)
    {
        var id = (long)(seconds * 1000);
        root.Touch(new SkUiTouchEvent(id, SkUiTouchAction.Pressed, point, TimeSpan.FromSeconds(seconds), Device: SkUiPointerDevice.Touch));
        root.Touch(new SkUiTouchEvent(id, SkUiTouchAction.Released, point, TimeSpan.FromSeconds(seconds + 0.05), Device: SkUiPointerDevice.Touch));
    }

    private static int Count(SKBitmap bitmap, int fromY, Func<SKColor, bool> match)
    {
        var count = 0;
        for (var y = fromY; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                if (match(bitmap.GetPixel(x, y)))
                    count++;
        return count;
    }

    private static bool IsErrorRed(SKColor color) => color.Red > 140 && color.Green < 100 && color.Blue < 100;

    [Fact]
    public void AHelperTextGoesUnderTheFieldAndMakesItTaller()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiEntry { Text = "text" });
        Assert.Equal(MinimumHeight, entry.Height, 3);

        entry.HelperText = "As on your passport";
        SkUiTestHelpers.Arrange(root, Width, 400);
        Assert.Equal(MinimumHeight + Spacing + MessageLine(), entry.Height, 1);
        // The text box stays in the field above the message.
        var text = entry.Model.GetTextBounds();
        Assert.Equal(MinimumHeight - Padding.Bottom, text.Bottom, 3);
        Assert.Equal("As on your passport", entry.Model.Message);

        entry.HelperText = null;
        SkUiTestHelpers.Arrange(root, Width, 400);
        Assert.Equal(MinimumHeight, entry.Height, 3);
    }

    [Fact]
    public void ANewMessageIsLaidOutAgain()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        // As the demo's editor sets it: one letter, then more, the field drawn in between.
        var entry = new SkUiEntry { FontFamily = SkUiTestHelpers.BundledFontFamily, HelperText = "e", VerticalOptions = LayoutOptions.Start };
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = entry, Background = Colors.White }, 300, 200);
        surface.Frame();
        var oneLine = entry.Height;
        var band = (int)Math.Ceiling(MinimumHeight + 1);
        var letter = Count(surface.Frame(), band, color => color.Red < 200);

        entry.HelperText = string.Join(' ', Enumerable.Repeat("Enter email", 6));
        var bitmap = surface.Frame();
        Assert.True(entry.Height > oneLine + MessageLine() / 2, "the longer message wraps");
        Assert.True(Count(bitmap, band, color => color.Red < 200) > letter * 4, "the new message is drawn");

        // The error text in its place, and back.
        entry.ErrorText = "No";
        entry.HasError = true;
        surface.Frame();
        Assert.Equal(oneLine, entry.Height, 3);
        entry.HasError = false;
        surface.Frame();
        Assert.True(entry.Height > oneLine + MessageLine() / 2);
    }

    [Fact]
    public void TheErrorTextReplacesTheHelperTextOnlyWhileThereIsAnError()
    {
        var entry = new SkUiEntry { HelperText = "help", ErrorText = "wrong" };
        Assert.Equal("help", entry.Model.Message);
        entry.HasError = true;
        Assert.Equal("wrong", entry.Model.Message);
        entry.ErrorText = "";
        Assert.Equal("help", entry.Model.Message);
        entry.HelperText = null;
        Assert.Null(entry.Model.Message);
        entry.SetErrorText("again").SetHasError(false).SetHelperText("back");
        Assert.Equal("back", entry.Model.Message);
    }

    [Fact]
    public void ALongMessageWrapsAtTheFieldsWidthAndAnUnconstrainedFieldTakesItsWidth()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var message = string.Join(' ', Enumerable.Repeat("word", 30));
        var (_, entry) = Field(new SkUiEntry { HasError = true, ErrorText = message });
        Assert.True(entry.Height > MinimumHeight + Spacing + MessageLine() * 2, "wraps on several lines");

        var free = new SkUiEntry { FontFamily = SkUiTestHelpers.BundledFontFamily, HelperText = "A helper text longer than the field" };
        var size = free.Measure(double.PositiveInfinity, double.PositiveInfinity);
        var empty = new SkUiEntry { FontFamily = SkUiTestHelpers.BundledFontFamily }.Measure(double.PositiveInfinity, double.PositiveInfinity);
        Assert.True(size.Width > empty.Width, "the message's line widens the field");
        Assert.Equal(MinimumHeight + Spacing + MessageLine(), size.Height, 1);
    }

    [Fact]
    public void TheLookDrawsTheFieldInTheErrorColorAboveTheMessage()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var look = SkUiLook.Current;
        SkUiTextFieldPaint? painted = null;
        look.TextFieldPainter = (_, paint) => painted = paint;
        try
        {
            var entry = new SkUiEntry { FontFamily = SkUiTestHelpers.BundledFontFamily, HelperText = "help", VerticalOptions = LayoutOptions.Start };
            using var surface = new SkUiTestSurface(new SkUiContentView { Content = entry, Background = Colors.White }, 300, 80);
            surface.Frame();
            Assert.False(painted!.Value.HasError);
            Assert.Equal(MinimumHeight, painted.Value.Bounds.Height, 3);

            entry.HasError = true;
            surface.Frame();
            Assert.True(painted.Value.HasError);
            Assert.Equal(SkUiToggleDrawing.ToSkColor(SkUiColors.Error), painted.Value.Error);
        }
        finally
        {
            look.TextFieldPainter = null;
        }
    }

    [Fact]
    public void TheMessageIsMutedAndRedWithAnError()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = new SkUiEntry
        {
            FontFamily = SkUiTestHelpers.BundledFontFamily, HelperText = "Helper text", ErrorText = "Error text", VerticalOptions = LayoutOptions.Start
        };
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = entry, Background = Colors.White }, 300, 80);
        var band = (int)Math.Ceiling(MinimumHeight + 1);
        var bitmap = surface.Frame();
        Assert.Equal(0, Count(bitmap, 0, IsErrorRed));
        Assert.True(Count(bitmap, band, color => color.Red < 200) > 10, "the helper text is drawn under the field");

        entry.HasError = true;
        bitmap = surface.Frame();
        Assert.True(Count(bitmap, band, IsErrorRed) > 10, "the error text in the error color");
        Assert.True(Count(bitmap, 0, IsErrorRed) > Count(bitmap, band, IsErrorRed), "the outline in the error color");
    }

    [Fact]
    public void AnErrorColorOfTheFieldReplacesTheSchemes()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var look = SkUiLook.Current;
        var painted = new List<SKColor>();
        look.TextFieldPainter = (_, paint) => painted.Add(paint.Error);
        try
        {
            var entry = new SkUiEntry
            {
                FontFamily = SkUiTestHelpers.BundledFontFamily, HasError = true, ErrorText = "Error text", ErrorColor = Colors.Blue,
                VerticalOptions = LayoutOptions.Start
            };
            var core = new SkUiCoreEntry().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetHasError(true).SetErrorText("Error text")
                .SetErrorColor(Colors.Lime);
            var host = new SkUiCoreHost { VerticalOptions = LayoutOptions.Start }.SetContent(core);
            using var surface = new SkUiTestSurface(new SkUiContentView
            {
                Content = new SkUiVerticalStackLayout { Children = { entry, host } }, Background = Colors.White
            }, 300, 200);
            var bitmap = surface.Frame();
            Assert.Equal([SKColors.Blue, SKColors.Lime], painted);
            // The messages in the fields' colors, none in the scheme's.
            var entryBand = (int)Math.Ceiling(MinimumHeight + 1);
            Assert.True(Count(bitmap, entryBand, color => color.Blue > 200 && color.Red < 100 && color.Green < 100) > 10, "the entry's message in blue");
            Assert.True(Count(bitmap, (int)Math.Ceiling(entry.Height + MinimumHeight + 1),
                color => color.Green > 200 && color.Red < 100 && color.Blue < 100) > 10, "the Core entry's message in lime");
            Assert.Equal(0, Count(bitmap, 0, IsErrorRed));

            painted.Clear();
            entry.ErrorColor = null;
            surface.Frame();
            Assert.Equal([SkUiToggleDrawing.ToSkColor(SkUiColors.Error)], painted);
        }
        finally
        {
            look.TextFieldPainter = null;
        }
    }

    [Fact]
    public void ASearchBarAndAnEditorDrawTheirErrorsToo()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var look = SkUiLook.Current;
        SkUiSearchFieldPaint? search = null;
        SkUiTextFieldPaint? editor = null;
        look.SearchFieldPainter = (_, paint) => search = paint;
        look.TextFieldPainter = (_, paint) => editor = paint;
        try
        {
            var bar = new SkUiSearchBar { FontFamily = SkUiTestHelpers.BundledFontFamily, HasError = true, ErrorText = "No results" };
            var field = new SkUiEditor { FontFamily = SkUiTestHelpers.BundledFontFamily, HeightRequest = 120, HasError = true, ErrorText = "Too long" };
            using var surface = new SkUiTestSurface(new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { bar, field } }, Background = Colors.White }, 300, 300);
            surface.Frame();
            Assert.True(search!.Value.HasError);
            Assert.True(editor!.Value.HasError);
            // The editor's fixed height holds the field and the message.
            Assert.Equal(120, field.Height, 3);
            Assert.Equal(120 - Spacing - MessageLine(), editor.Value.Bounds.Height, 1);
        }
        finally
        {
            look.SearchFieldPainter = null;
            look.TextFieldPainter = null;
        }
    }

    [Fact]
    public void AFieldOfFixedSizeKeepsItsHeightAndItsMessageHangsBelowIt()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        // A size fixed to the field's natural size before it had a message (the demo's previews, a style's HeightRequest).
        var entry = new SkUiEntry { FontFamily = SkUiTestHelpers.BundledFontFamily, Placeholder = "Type here", ErrorText = "Enter an email address" };
        var natural = ((IView)entry).Measure(double.PositiveInfinity, double.PositiveInfinity);
        entry.WidthRequest = Math.Ceiling(natural.Width);
        entry.HeightRequest = Math.Ceiling(natural.Height);
        entry.VerticalOptions = LayoutOptions.Start;
        entry.HorizontalOptions = LayoutOptions.Start;
        entry.Text = "text";
        entry.HasError = true;
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = entry, Background = Colors.White }, 300, 200);
        var bitmap = surface.Frame();

        Assert.Equal(MinimumHeight, entry.Height, 3);
        Assert.Equal(MinimumHeight - Padding.VerticalThickness, entry.Model.GetTextBounds().Height, 3);
        var overflow = entry.Model.GetOverflow((float)entry.Width, (float)entry.Height);
        Assert.NotNull(overflow);
        Assert.True(overflow.Value.Bottom > Spacing + MessageLine(), "the wrapped message hangs below the field");
        Assert.True(Count(bitmap, (int)Math.Ceiling(entry.Height + 1), IsErrorRed) > 10, "the message is drawn below the node");
    }

    [Fact]
    public void ATapOnTheMessageDoesNotFocusTheField()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiEntry { HelperText = "help" });
        Tap(root, new Point(Padding.Left + 4, entry.Height - 2), 1);
        Assert.False(entry.IsFocused);
        Tap(root, new Point(Padding.Left + 4, MinimumHeight / 2), 2);
        Assert.True(entry.IsFocused);
    }

    [Fact]
    public void TheValidityStatesFollowTheErrorAndFocus()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (_, entry) = Field(new SkUiEntry());
        VisualStateManager.SetVisualStateGroups(entry, [new VisualStateGroup
        {
            Name = "ValidityStates",
            States =
            {
                State(SkUiInputView.ValidVisualState, Colors.Black),
                State(SkUiInputView.InvalidVisualState, Colors.Red),
                State(SkUiInputView.InvalidFocusedVisualState, Colors.Orange)
            }
        }]);
        Assert.Equal(Colors.Black, entry.TextColor);
        entry.HasError = true;
        Assert.Equal(Colors.Red, entry.TextColor);
        entry.Focus();
        Assert.Equal(Colors.Orange, entry.TextColor);
        entry.Unfocus();
        Assert.Equal(Colors.Red, entry.TextColor);
        entry.HasError = false;
        Assert.Equal(Colors.Black, entry.TextColor);

        // Without an InvalidFocused state, a focused field with an error is Invalid.
        var (_, other) = Field(new SkUiEntry());
        VisualStateManager.SetVisualStateGroups(other, [new VisualStateGroup
        {
            Name = "ValidityStates",
            States = { State(SkUiInputView.ValidVisualState, Colors.Black), State(SkUiInputView.InvalidVisualState, Colors.Red) }
        }]);
        other.Focus();
        other.HasError = true;
        Assert.Equal(Colors.Red, other.TextColor);

        static VisualState State(string name, Color color)
        {
            var state = new VisualState { Name = name };
            state.Setters.Add(new Setter { Property = SkUiInputView.TextColorProperty, Value = color });
            return state;
        }
    }

    [Fact]
    public void ScreenReadersReadTheMessageAndTheError()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiEntry { Placeholder = "Email", HelperText = "Your work address" });
        var owner = new SkUiSemanticsOwner(root);
        var node = owner.Tree.Flatten().Single(n => n.Role == SkUiSemanticsRole.TextField);
        Assert.Equal("Your work address", node.Hint);
        Assert.False(node.TextField!.Value.HasError);

        entry.HasError = true;
        entry.ErrorText = "Not an email address";
        SemanticProperties.SetHint(entry, "Used to sign in");
        owner = new SkUiSemanticsOwner(root);
        node = owner.Tree.Flatten().Single(n => n.Role == SkUiSemanticsRole.TextField);
        Assert.Equal("Not an email address, Used to sign in", node.Hint);
        Assert.True(node.TextField!.Value.HasError);
        Assert.Equal("Not an email address", node.TextField.Value.Message);
    }

    [Fact]
    public void CoreFieldsShowTheirMessagesLikeTheSkUiFields()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var core = new SkUiCoreEntry().SetFontFamily(SkUiTestHelpers.BundledFontFamily);
        var changed = new List<string?>();
        ((INotifyPropertyChanged)core).PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        core.SetHelperText("help").SetErrorText("wrong").SetHasError(true);
        Assert.Equal([nameof(SkUiCoreEntry.HelperText), nameof(SkUiCoreEntry.ErrorText), nameof(SkUiCoreEntry.HasError)], changed);
        Assert.Equal("wrong", core.Model.Message);
        var root = new SkUiVerticalStackLayout { Children = { new SkUiCoreHost().SetContent(core) } };
        SkUiTestHelpers.Arrange(root, Width, 400);
        var (_, entry) = Field(new SkUiEntry { HelperText = "help", ErrorText = "wrong", HasError = true });
        Assert.Equal(entry.Height, core.Frame.Height, 3);

        core.HasError = false;
        Assert.Equal("help", core.Model.Message);
        Assert.True(Math.Abs(entry.Height - core.Measure(Width, 400).Height) < Tolerance);
    }
}
