using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The drawn entry (D3): MAUI's <c>Entry</c> API on <see cref="SkUiEntry"/> and <see cref="SkUiCoreEntry"/>, keys, pointer
/// selection, the caret, the clipboard, and the text input proxy contract (attach on focus, no echo of the platform's own
/// edits). In the global collection: the clipboard and the keyboard / pointer mode are process-wide.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class EntryTests
{
    /// <summary>A text input proxy that records what the field asked of it.</summary>
    private sealed class TestProxy(bool receivesCharacters = false) : ISkUiTextInputProxy
    {
        public readonly List<string> Calls = [];
        public ISkUiTextInputClient? Client;

        public bool ReceivesCharacters { get; } = receivesCharacters;

        public void Attach(ISkUiTextInputClient client)
        {
            Client = client;
            client.InputAttached(this);
            Calls.Add("attach");
        }

        public void Detach(ISkUiTextInputClient client)
        {
            client.InputAttached(null);
            Client = null;
            Calls.Add("detach");
        }

        public void ShowKeyboard(ISkUiTextInputClient client) => Calls.Add("show");
        public void HideKeyboard(ISkUiTextInputClient client) => Calls.Add("hide");
        public void StateChanged(ISkUiTextInputClient client, bool textChanged) => Calls.Add(textChanged ? "text" : "selection");
        public void OptionsChanged(ISkUiTextInputClient client) => Calls.Add("options");
        public void GeometryChanged(ISkUiTextInputClient client) => Calls.Add("geometry");
        public void ShowEditMenu(ISkUiTextInputClient client, Rect anchor) => Calls.Add("menu");
        public void HideEditMenu(ISkUiTextInputClient client) => Calls.Add("hide menu");
        public void ShowMagnifier(ISkUiTextInputClient client, Rect caret) => Calls.Add("magnifier");
        public void HideMagnifier(ISkUiTextInputClient client) => Calls.Add("hide magnifier");
    }

    /// <summary>The default look with Android's insertion handle (the tests' host decides the default).</summary>
    private sealed class HandleLook(bool handle) : DefaultSkUiLook
    {
        public override bool ShowsTextInsertionHandle => handle;
    }

    private static (SkUiVerticalStackLayout Root, SkUiEntry Entry) Field(SkUiEntry? entry = null, TestProxy? proxy = null)
    {
        entry ??= new SkUiEntry();
        entry.FontFamily = SkUiTestHelpers.BundledFontFamily;
        var root = new SkUiVerticalStackLayout { Children = { entry } };
        SkUiTestHelpers.Arrange(root, 300, 200);
        if (proxy is not null)
            root.FocusManager.TextInput = proxy;
        return (root, entry);
    }

    private static void Type(SkUiView root, string text)
    {
        foreach (var character in text)
            root.FocusManager.KeyDown(character == ' ' ? SkUiKey.Space : SkUiKey.Character, character: character);
    }

    private static void Key(SkUiView root, SkUiKey key, SkUiKeyModifiers modifiers = SkUiKeyModifiers.None) => root.FocusManager.KeyDown(key, modifiers);

    [Fact]
    public void TypingSetsTextRaisesTextChangedAndMovesTheCursor()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field();
        var changes = new List<string?>();
        entry.TextChanged += (_, args) => changes.Add(args.NewTextValue);
        Assert.True(entry.Focus());
        Type(root, "hi");
        Assert.Equal("hi", entry.Text);
        Assert.Equal(["h", "hi"], changes);
        Assert.Equal(2, entry.CursorPosition);
        Key(root, SkUiKey.Left, SkUiKeyModifiers.Shift);
        Assert.Equal((1, 1), (entry.CursorPosition, entry.SelectionLength));
        Type(root, "o");
        Assert.Equal("ho", entry.Text);
        Key(root, SkUiKey.Backspace);
        Assert.Equal("h", entry.Text);
    }

    [Fact]
    public void AppTextPutsTheCaretAtTheEndAndCursorPropertiesSelect()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (_, entry) = Field(new SkUiEntry { Text = "hello world" });
        Assert.Equal(11, entry.Model.Editor.Caret);
        entry.CursorPosition = 6;
        entry.SelectionLength = 5;
        Assert.Equal("world", entry.Model.Editor.SelectedText);
        entry.Text = "changed";
        Assert.Equal(7, entry.Model.Editor.Caret);
    }

    [Fact]
    public void ReturnCompletesAndRunsTheReturnCommand()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var order = new List<string>();
        var entry = new SkUiEntry { ReturnCommand = new Command<string>(p => order.Add($"command {p}")), ReturnCommandParameter = "x" };
        entry.Completed += (_, _) => order.Add("completed");
        var (root, _) = Field(entry);
        entry.Focus();
        Key(root, SkUiKey.Enter);
        Assert.Equal(["completed", "command x"], order);
    }

    [Fact]
    public void MaxLengthCutsTypingAndAppText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiEntry { MaxLength = 3 });
        entry.Focus();
        Type(root, "abcd");
        Assert.Equal("abc", entry.Text);
        entry.Text = "123456";
        Assert.Equal("123", entry.Text);
        entry.MaxLength = 2;
        Assert.Equal("12", entry.Text);
    }

    [Fact]
    public void TextTransformAppliesToTheTextAlreadyThere()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var source = new TextSource { Value = "Hello" };
        var entry = new SkUiEntry { BindingContext = source };
        entry.SetBinding(SkUiInputView.TextProperty, nameof(TextSource.Value));
        var (root, _) = Field(entry);
        entry.Focus();
        entry.CursorPosition = 2;
        var changes = new List<string?>();
        entry.TextChanged += (_, args) => changes.Add(args.NewTextValue);
        entry.TextTransform = TextTransform.Uppercase;
        // The field and Text show the transformed text (as MAUI's fields), the binding gets it, the caret stays.
        Assert.Equal("HELLO", entry.Model.Editor.Text);
        Assert.Equal("HELLO", entry.Text);
        Assert.Equal("HELLO", source.Value);
        Assert.Equal(2, entry.CursorPosition);
        Assert.Equal(["HELLO"], changes);
        Type(root, "x");
        Assert.Equal("HEXLLO", entry.Text);
        // Text set by the app is transformed too; its change is raised first, then the shown text's.
        entry.Text = "abc";
        Assert.Equal("ABC", entry.Text);
        Assert.Equal(["HELLO", "HEXLLO", "abc", "ABC"], changes);
        entry.TextTransform = TextTransform.Lowercase;
        Assert.Equal("abc", entry.Text);
        // Default keeps the text as it is.
        entry.TextTransform = TextTransform.Default;
        Assert.Equal("abc", entry.Text);

        var core = new SkUiCoreEntry().SetText("Hello").SetTextTransform(TextTransform.Uppercase);
        Assert.Equal("HELLO", core.Text);
        Assert.Equal("HELLO", core.Model.Editor.Text);
        core.SetText("abc");
        Assert.Equal("ABC", core.Text);
    }

    private sealed class TextSource : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public string? Value
        {
            get;
            set
            {
                field = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    [Fact]
    public void FocusAttachesTheProxyAndItsEditsAreNotEchoed()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var proxy = new TestProxy(receivesCharacters: true);
        var (root, entry) = Field(proxy: proxy);
        Assert.True(entry.Focus());
        Assert.Same(entry.Model, proxy.Client);
        proxy.Calls.Clear();

        // Characters come through the input method: character keys are ignored.
        Type(root, "x");
        Assert.Null(entry.Text);
        proxy.Client!.InsertText("abc");
        Assert.Equal("abc", entry.Text);
        Assert.DoesNotContain("text", proxy.Calls);

        // Keys and the app edit behind the input method's back: it hears of it.
        Key(root, SkUiKey.Backspace);
        Assert.Equal("ab", entry.Text);
        Assert.Contains("text", proxy.Calls);
        proxy.Calls.Clear();
        entry.Text = "new";
        Assert.Contains("text", proxy.Calls);

        entry.Unfocus();
        Assert.Contains("detach", proxy.Calls);
        Assert.Null(entry.Model.Proxy);
    }

    [Fact]
    public void CompositionFromTheInputMethodIsOneEdit()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var proxy = new TestProxy(receivesCharacters: true);
        var (_, entry) = Field(proxy: proxy);
        entry.Focus();
        var client = proxy.Client!;
        client.BeginInputBatch();
        client.SetComposingText("ni");
        client.EndInputBatch();
        Assert.True(entry.Model.Editor.HasComposition);
        client.SetComposingText("nihao");
        client.InsertText("你好");
        Assert.Equal("你好", entry.Text);
        Assert.False(entry.Model.Editor.HasComposition);
        entry.Model.Editor.Undo();
        Assert.Equal("", entry.Text);
    }

    [Fact]
    public void OptionsFollowTheProperties()
    {
        var entry = new SkUiEntry { Keyboard = Keyboard.Email, ReturnType = ReturnType.Go, IsPassword = true };
        var options = ((ISkUiTextInputClient)entry.Model).Options;
        Assert.Same(Keyboard.Email, options.Keyboard);
        Assert.Equal(ReturnType.Go, options.ReturnType);
        Assert.True(options.IsPassword);
        Assert.False(options.IsTextPredictionEnabled);
        Assert.Equal(SkUiAutofillHints.Password, options.AutofillHint);
    }

    [Fact]
    public void OptionChangesWhileFocusedRestartTheInput()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var proxy = new TestProxy();
        var (_, entry) = Field(proxy: proxy);
        entry.Focus();
        proxy.Calls.Clear();
        entry.Keyboard = Keyboard.Numeric;
        Assert.Contains("options", proxy.Calls);
    }

    [Fact]
    public void TextFieldsTakeFocusWithAccessibilityOffAndWithoutATabStop()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiEntry { IsTabStop = false });
        Assert.Empty(root.FocusManager.GetTabOrder());
        SkUiAccessibility.IsEnabled = false;
        try
        {
            Assert.True(entry.Focus());
            Type(root, "ok");
            Assert.Equal("ok", entry.Text);
        }
        finally
        {
            SkUiAccessibility.IsEnabled = true;
        }
    }

    [Fact]
    public void ATapFocusesAndPlacesTheCaret()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiEntry { Text = "abcdef" });
        var box = entry.Model.Layout;
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, new Point(5, 5), TimeSpan.Zero));
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, new Point(5, 5), TimeSpan.FromMilliseconds(50)));
        Assert.True(entry.IsFocused);
        var padding = SkUiLook.Current.TextFieldPadding.Left;
        var advance = box.Width / 6;
        var x = padding + advance * 3.2;
        root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Pressed, new Point(x, 20), TimeSpan.FromSeconds(1)));
        root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Released, new Point(x, 20), TimeSpan.FromSeconds(1.05)));
        Assert.Equal(3, entry.CursorPosition);
    }

    [Fact]
    public void MouseDragsSelectAndDoubleClicksSelectWords()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiEntry { Text = "one two three" });
        var padding = SkUiLook.Current.TextFieldPadding.Left;
        entry.Model.Measure(300, 200);
        var advance = entry.Model.Layout.Width / 13;
        Point At(double character) => new(padding + advance * character, 20);
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, At(4), TimeSpan.Zero, Device: SkUiPointerDevice.Mouse));
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Moved, At(7), TimeSpan.FromMilliseconds(20), Device: SkUiPointerDevice.Mouse));
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, At(7), TimeSpan.FromMilliseconds(40), Device: SkUiPointerDevice.Mouse));
        Assert.True(entry.IsFocused);
        Assert.Equal("two", entry.Model.Editor.SelectedText);

        root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Pressed, At(9.5), TimeSpan.FromSeconds(2), Device: SkUiPointerDevice.Mouse));
        root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Released, At(9.5), TimeSpan.FromSeconds(2.05), Device: SkUiPointerDevice.Mouse));
        root.Touch(new SkUiTouchEvent(3, SkUiTouchAction.Pressed, At(9.5), TimeSpan.FromSeconds(2.1), Device: SkUiPointerDevice.Mouse));
        root.Touch(new SkUiTouchEvent(3, SkUiTouchAction.Released, At(9.5), TimeSpan.FromSeconds(2.15), Device: SkUiPointerDevice.Mouse));
        Assert.Equal("three", entry.Model.Editor.SelectedText);
    }

    [Fact]
    public void ClipboardShortcutsCutCopyPasteAndUndo()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        using var ui = TestUiContext.Install();
        string? clipboard = null;
        var (setText, getText, hasText) = (SkUiTextClipboard.SetText, SkUiTextClipboard.GetText, SkUiTextClipboard.HasText);
        SkUiTextClipboard.SetText = text => { clipboard = text; return Task.CompletedTask; };
        SkUiTextClipboard.GetText = () => Task.FromResult(clipboard);
        SkUiTextClipboard.HasText = () => clipboard is not null;
        try
        {
            var (root, entry) = Field(new SkUiEntry { Text = "copy me" });
            entry.Focus();
            root.FocusManager.KeyDown(SkUiKey.Character, SkUiKeyModifiers.Control, character: 'a');
            Assert.Equal("copy me", entry.Model.Editor.SelectedText);
            root.FocusManager.KeyDown(SkUiKey.Character, SkUiKeyModifiers.Control, character: 'x');
            ui.RunPending();
            Assert.Equal("copy me", clipboard);
            Assert.Equal("", entry.Text);
            root.FocusManager.KeyDown(SkUiKey.Character, SkUiKeyModifiers.Control, character: 'v');
            root.FocusManager.KeyDown(SkUiKey.Character, SkUiKeyModifiers.Control, character: 'v');
            ui.RunPending();
            Assert.Equal("copy mecopy me", entry.Text);
            root.FocusManager.KeyDown(SkUiKey.Character, SkUiKeyModifiers.Control, character: 'z');
            Assert.Equal("copy me", entry.Text);
        }
        finally
        {
            (SkUiTextClipboard.SetText, SkUiTextClipboard.GetText, SkUiTextClipboard.HasText) = (setText, getText, hasText);
        }
    }

    [Fact]
    public void PasswordsShowBulletsAndCannotBeCopied()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (_, entry) = Field(new SkUiEntry { Text = "pa👍s", IsPassword = true });
        entry.Focus();
        entry.Model.Measure(300, 200);
        Assert.Equal("••••", entry.Model.Layout.Text);
        entry.Model.Editor.SelectAll();
        Assert.False(entry.Model.CanPerformEditAction(SkUiTextEditAction.Copy));
        var info = new SkUiSemanticsInfo();
        ((ISkUiAccessibleNode)entry).GetSemantics(info);
        Assert.Equal(SkUiSemanticsRole.TextField, info.Role);
        Assert.Equal("••••", info.Value);
    }

    [Fact]
    public void TheCaretIsAComposedDrawableThatBlinksWithoutRecording()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var entry = new SkUiEntry { Text = "abc", FontFamily = SkUiTestHelpers.BundledFontFamily };
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = entry }, 300, 60);
        surface.Frame();
        Assert.Null(entry.RenderState.Committed.Drawable);
        entry.Focus();
        surface.Frame();
        var caret = Assert.IsType<SkUiCaretDrawable>(entry.RenderState.Committed.Drawable);
        Assert.True(caret.Rect.Height > 10);
        var recorded = surface.RecordedPictures;
        TestDispatcherProvider.RunDelayed();
        surface.Frame();
        Assert.False(entry.Model.IsCaretShown);
        Assert.Null(entry.RenderState.Committed.Drawable);
        Assert.Equal(recorded, surface.RecordedPictures);
        TestDispatcherProvider.RunDelayed();
        surface.Frame();
        Assert.NotNull(entry.RenderState.Committed.Drawable);
        entry.Unfocus();
        surface.Frame();
        Assert.Null(entry.RenderState.Committed.Drawable);
    }

    [Fact]
    public void LongTextScrollsToKeepTheCaretInView()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = new SkUiEntry { Text = new string('w', 80), FontFamily = SkUiTestHelpers.BundledFontFamily, WidthRequest = 120 };
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = entry }, 300, 60);
        entry.Focus();
        surface.Frame();
        var caret = Assert.IsType<SkUiCaretDrawable>(entry.RenderState.Committed.Drawable);
        Assert.InRange(caret.Rect.Right, 0, entry.Width);
        entry.Model.Editor.Select(0, 0);
        surface.Frame();
        caret = Assert.IsType<SkUiCaretDrawable>(entry.RenderState.Committed.Drawable);
        Assert.InRange(caret.Rect.Left, 0, SkUiLook.Current.TextFieldPadding.Left + 2);
    }

    [Fact]
    public void MeasuresItsTextWithTheLooksMinimumHeight()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = new SkUiEntry { FontFamily = SkUiTestHelpers.BundledFontFamily };
        var empty = ((IView)entry).Measure(double.PositiveInfinity, double.PositiveInfinity);
        Assert.Equal(SkUiLook.Current.DefaultTextFieldMinimumHeight, empty.Height);
        entry.Text = "some longer text";
        var filled = ((IView)entry).Measure(double.PositiveInfinity, double.PositiveInfinity);
        Assert.True(filled.Width > empty.Width + 50);
        entry.Placeholder = "a placeholder that is longer still";
        entry.Text = null;
        Assert.True(((IView)entry).Measure(double.PositiveInfinity, double.PositiveInfinity).Width > filled.Width);
    }

    [Fact]
    public void DrawsTheTextTheSelectionAndThePlaceholder()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = new SkUiEntry { Placeholder = "Name", PlaceholderColor = Colors.Red, FontFamily = SkUiTestHelpers.BundledFontFamily };
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = entry, Background = Colors.White }, 300, 60);
        var bitmap = surface.Frame();
        Assert.True(Count(bitmap, c => c.Red > 200 && c.Green < 80) > 10, "placeholder in red");
        entry.Text = "Text";
        entry.TextColor = Colors.Blue;
        bitmap = surface.Frame();
        Assert.Equal(0, Count(bitmap, c => c.Red > 200 && c.Green < 80));
        Assert.True(Count(bitmap, c => c.Blue > 200 && c.Red < 80) > 10, "text in blue");
        entry.SelectionColor = Colors.Lime;
        entry.Focus();
        entry.Model.Editor.SelectAll();
        bitmap = surface.Frame();
        Assert.True(Count(bitmap, c => c.Green > 200 && c.Red < 80 && c.Blue < 80) > 50, "selection in lime");
    }

    private static int Count(SKBitmap bitmap, Func<SKColor, bool> match)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                if (match(bitmap.GetPixel(x, y)))
                    count++;
        return count;
    }

    [Fact]
    public void CoreEntryEditsLikeTheSkUiEntry()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var core = new SkUiCoreEntry().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetPlaceholder("Core");
        var root = new SkUiVerticalStackLayout { Children = { new SkUiCoreHost().SetContent(core) } };
        SkUiTestHelpers.Arrange(root, 300, 200);
        var changes = 0;
        core.TextChanged += (_, _) => changes++;
        Assert.True(core.Focus());
        Type(root, "core text");
        Assert.Equal("core text", core.Text);
        Assert.Equal(9, core.CursorPosition);
        Assert.Equal(9, changes);
        Key(root, SkUiKey.Left, SkUiKeyModifiers.Control | SkUiKeyModifiers.Shift);
        Assert.Equal((5, 4), (core.CursorPosition, core.SelectionLength));
        core.SetText("set");
        Assert.Equal(3, core.Model.Editor.Caret);
    }

    [Fact]
    public void ReadOnlyEntriesSelectButDoNotEdit()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiEntry { Text = "fixed", IsReadOnly = true });
        Assert.True(entry.Focus());
        Type(root, "x");
        Key(root, SkUiKey.Backspace);
        Assert.Equal("fixed", entry.Text);
        Key(root, SkUiKey.Home, SkUiKeyModifiers.Shift);
        Assert.Equal(5, entry.SelectionLength);
    }

    [Fact]
    public void TheClearButtonClearsAndKeepsFocus()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiEntry { Text = "clear me", ClearButtonVisibility = ClearButtonVisibility.WhileEditing });
        entry.Focus();
        Assert.True(entry.Model.ClearButtonShown);
        var x = entry.Width - SkUiLook.Current.TextFieldPadding.Right - SkUiLook.Current.TextFieldClearButtonWidth / 2;
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, new Point(x, entry.Height / 2), TimeSpan.Zero));
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, new Point(x, entry.Height / 2), TimeSpan.FromMilliseconds(40)));
        Assert.Equal("", entry.Text);
        Assert.True(entry.IsFocused);
        Assert.False(entry.Model.ClearButtonShown);
    }

    [Fact]
    public void ReturnTypeNextMovesFocusToTheNextField()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var first = new SkUiEntry { ReturnType = ReturnType.Next };
        var second = new SkUiEntry();
        var root = new SkUiVerticalStackLayout { Children = { first, second } };
        SkUiTestHelpers.Arrange(root, 300, 200);
        first.Focus();
        Key(root, SkUiKey.Enter);
        Assert.True(second.IsFocused);
    }

    [Fact]
    public void AFocusedFieldIsKeptAboveTheKeyboardInItsScroller()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var column = new SkUiVerticalStackLayout();
        for (var index = 0; index < 8; index++)
            column.Children.Add(new SkUiBox { HeightRequest = 60 });
        var field = new SkUiEntry { FontFamily = SkUiTestHelpers.BundledFontFamily };
        column.Children.Add(field);
        var scroller = new SkUiScrollView { Content = column };
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = scroller }, 300, 600);
        var root = surface.Root;
        surface.Frame();
        Assert.True(field.Focus());
        // The keyboard covers the surface's bottom half: the field (at y 480) scrolls above it.
        root.FocusManager.SetKeyboardTop(300);
        for (var time = 0; time <= 1000; time += 50)
            surface.Frame(time);
        Assert.Equal(300, scroller.KeyboardInset, 1);
        var bounds = SkUiDiagnostics.GetRootBounds(field)!.Value;
        Assert.True(bounds.Bottom <= 300.5, $"field bottom {bounds.Bottom}");
        // The keyboard goes: the scroller gives the room back.
        root.FocusManager.SetKeyboardTop(double.PositiveInfinity);
        Assert.Equal(0, scroller.KeyboardInset);
    }

    [Fact]
    public void TheInputMethodFollowsAScrolledField()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var proxy = new TestProxy();
        var column = new SkUiVerticalStackLayout { Children = { new SkUiEntry { FontFamily = SkUiTestHelpers.BundledFontFamily }, new SkUiBox { HeightRequest = 2000 } } };
        var scroller = new SkUiScrollView { Content = column };
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = scroller }, 300, 400);
        surface.Root.FocusManager.TextInput = proxy;
        var entry = (SkUiEntry)column.Children[0];
        entry.Focus();
        surface.Frame();
        proxy.Calls.Clear();
        surface.Frame();
        Assert.DoesNotContain("geometry", proxy.Calls);
        scroller.ScrollToAsync(0, 50, animated: false);
        surface.Frame();
        Assert.Contains("geometry", proxy.Calls);
    }

    [Fact]
    public void ScreenReadersReadSelectAndSetTheText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, entry) = Field(new SkUiEntry { Text = "hello", Placeholder = "Name" });
        entry.Focus();
        entry.CursorPosition = 1;
        entry.SelectionLength = 3;
        var owner = new SkUiSemanticsOwner(root);
        var node = owner.Tree.Flatten().Single(n => n.Role == SkUiSemanticsRole.TextField);
        Assert.Equal("Name", node.Label);
        Assert.Equal("hello", node.Value);
        Assert.Equal(new SkUiSemanticsTextField(1, 4, false, false, false), node.TextField);
        Assert.True(owner.SetText(node.Id, "world"));
        Assert.Equal("world", entry.Text);
        Assert.True(owner.SetSelection(node.Id, 0, 2));
        Assert.Equal((0, 2), (entry.CursorPosition, entry.SelectionLength));
        entry.IsReadOnly = true;
        owner.Invalidate();
        node = owner.Tree.Flatten().Single(n => n.Role == SkUiSemanticsRole.TextField);
        Assert.False(owner.SetText(node.Id, "nope"));
        Assert.Equal("world", entry.Text);
    }

    [Fact]
    public void TouchSelectionShowsHandlesAndTheEditMenu()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var proxy = new TestProxy(receivesCharacters: true);
        var (root, entry) = Field(new SkUiEntry { Text = "one two three" }, proxy);
        entry.Model.Measure(300, 200);
        var padding = SkUiLook.Current.TextFieldPadding.Left;
        var advance = entry.Model.Layout.Width / 13;
        Point At(double character) => new(padding + advance * character, entry.Height / 2);
        // A long press selects the word and, when the finger lifts, the menu opens at it.
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, At(5), TimeSpan.Zero));
        TestDispatcherProvider.RunDelayed();
        Assert.Equal("two", entry.Model.Editor.SelectedText);
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, At(5), TimeSpan.FromSeconds(1)));
        Assert.True(entry.Model.IsEditMenuShown);
        Assert.Contains("menu", proxy.Calls);
        // The end handle drags the selection's end; the menu hides while dragging and comes back after.
        var end = At(7);
        Assert.Equal(1, entry.Model.HandleAt(end));
        root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Pressed, end, TimeSpan.FromSeconds(2)));
        Assert.False(entry.Model.IsEditMenuShown);
        root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Moved, At(10.6), TimeSpan.FromSeconds(2.1)));
        root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Released, At(10.6), TimeSpan.FromSeconds(2.2)));
        Assert.Equal("two thr", entry.Model.Editor.SelectedText);
        Assert.True(entry.Model.IsEditMenuShown);
        // Typing ends the touch selection: no handles, no menu.
        proxy.Client!.InsertText("x");
        Assert.False(entry.Model.IsEditMenuShown);
        Assert.Equal(0, entry.Model.HandleAt(At(5)));
    }

    [Fact]
    public void AFingersDoubleTapSelectsTheWord()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var proxy = new TestProxy(receivesCharacters: true);
        var (root, entry) = Field(new SkUiEntry { Text = "one two three" }, proxy);
        entry.Model.Measure(300, 200);
        var padding = SkUiLook.Current.TextFieldPadding.Left;
        var advance = entry.Model.Layout.Width / 13;
        var at = new Point(padding + advance * 5, entry.Height / 2);
        void Tap(int id, double pressMs, double releaseMs, Point point)
        {
            root.Touch(new SkUiTouchEvent(id, SkUiTouchAction.Pressed, point, TimeSpan.FromMilliseconds(pressMs)));
            root.Touch(new SkUiTouchEvent(id, SkUiTouchAction.Released, point, TimeSpan.FromMilliseconds(releaseMs)));
        }
        // Each tap held 120 ms with 150 ms between them: 270 ms from press to press would also pass, 390 ms (a slower finger) must too.
        Tap(1, 0, 120, at);
        Tap(2, 390, 510, new Point(at.X + 3, at.Y + 2));
        Assert.Equal("two", entry.Model.Editor.SelectedText);
        Assert.True(entry.Model.IsEditMenuShown);
        // Too long after the release: single taps (the caret), away from the selection's handles.
        var far = new Point(padding + advance * 12, at.Y);
        Tap(3, 2000, 2100, far);
        Tap(4, 2500, 2600, far);
        Assert.False(entry.Model.Editor.HasSelection);
    }

    [Fact]
    public void WhereAPressFocusesTheFieldFocusesOnTheTapWithoutTheEditMenu()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var proxy = new TestProxy(receivesCharacters: true);
        var (root, entry) = Field(new SkUiEntry(), proxy);
        // Windows: a press focuses the control it hits, but a text field focuses itself, so the first tap on an empty field is
        // not taken for a tap on the caret of a focused one (which opens the edit menu).
        root.FocusManager.PointerPressFocuses = true;
        var at = new Point(20, entry.Height / 2);
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, at, TimeSpan.Zero));
        Assert.False(entry.IsFocused);
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, at, TimeSpan.FromMilliseconds(100)));
        Assert.True(entry.IsFocused);
        Assert.False(entry.Model.IsEditMenuShown);
        // A later tap on the caret of the focused field opens it.
        root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(2)));
        root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(2.1)));
        Assert.True(entry.Model.IsEditMenuShown);
    }

    [Fact]
    public void ATapShowsTheInsertionHandleBelowTheFieldAndDraggingItMovesTheCaret()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var previous = SkUiLook.Current;
        SkUiLook.Current = new HandleLook(handle: true);
        try
        {
            var proxy = new TestProxy(receivesCharacters: true);
            var (root, entry) = Field(new SkUiEntry { Text = "one two three" }, proxy);
            entry.Model.Measure(300, 200);
            var padding = SkUiLook.Current.TextFieldPadding.Left;
            var advance = entry.Model.Layout.Width / 13;
            Point At(double character) => new(padding + advance * character, entry.Height / 2);
            root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, At(2), TimeSpan.Zero));
            root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, At(2), TimeSpan.FromMilliseconds(50)));
            Assert.Equal(2, entry.CursorPosition);
            // The handle hangs below the field: the node draws past its bounds, unclipped.
            var props = SkUiRenderProps.Default;
            ((ISkUiRenderable)entry).GetRenderProps(ref props);
            Assert.False(props.ClipToBounds);
            Assert.True(props.Overflow.Bottom > 0);
            // A press on the handle, outside the field's rectangle, reaches the field; dragging it moves the caret with the magnifier.
            var handle = new Point(At(2).X, entry.Height + 6);
            Assert.Equal(SkUiTextFieldModel.CaretHandle, entry.Model.HandleAt(handle));
            proxy.Calls.Clear();
            root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Pressed, handle, TimeSpan.FromSeconds(1)));
            root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Moved, new Point(At(9).X, handle.Y), TimeSpan.FromSeconds(1.1)));
            Assert.Equal(9, entry.CursorPosition);
            Assert.Contains("magnifier", proxy.Calls);
            root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Released, new Point(At(9).X, handle.Y), TimeSpan.FromSeconds(1.2)));
            Assert.Equal("hide magnifier", proxy.Calls[^1]);
            Assert.Equal(SkUiTextFieldModel.CaretHandle, entry.Model.HandleAt(new Point(At(9).X, entry.Height + 6)));
            // It goes after its timeout, and when typing.
            TestDispatcherProvider.RunDelayed();
            Assert.Equal(0, entry.Model.HandleAt(new Point(At(9).X, entry.Height + 6)));
            props = SkUiRenderProps.Default;
            ((ISkUiRenderable)entry).GetRenderProps(ref props);
            Assert.True(props.ClipToBounds);
            root.Touch(new SkUiTouchEvent(3, SkUiTouchAction.Pressed, At(4), TimeSpan.FromSeconds(5)));
            root.Touch(new SkUiTouchEvent(3, SkUiTouchAction.Released, At(4), TimeSpan.FromSeconds(5.05)));
            Assert.NotEqual(0, entry.Model.HandleAt(new Point(At(4).X, entry.Height + 6)));
            proxy.Client!.InsertText("x");
            Assert.Equal(0, entry.Model.HandleAt(new Point(At(5).X, entry.Height + 6)));
        }
        finally
        {
            SkUiLook.Current = previous;
        }
    }

    [Fact]
    public void ADoubleTapSelectsTheWordAlsoNextToTheInsertionHandle()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var previous = SkUiLook.Current;
        SkUiLook.Current = new HandleLook(handle: true);
        try
        {
            var proxy = new TestProxy(receivesCharacters: true);
            var (root, entry) = Field(new SkUiEntry { Text = "one two three" }, proxy);
            entry.Model.Measure(300, 200);
            var padding = SkUiLook.Current.TextFieldPadding.Left;
            var advance = entry.Model.Layout.Width / 13;
            var at = new Point(padding + advance * 5, entry.Height / 2);
            // The first tap shows the handle under the caret; the second lands within its reach and still counts as a tap.
            root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, at, TimeSpan.Zero));
            root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, at, TimeSpan.FromMilliseconds(100)));
            Assert.NotEqual(0, entry.Model.HandleAt(new Point(at.X, at.Y + 12)));
            root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Pressed, new Point(at.X, at.Y + 4), TimeSpan.FromMilliseconds(250)));
            root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Released, new Point(at.X, at.Y + 4), TimeSpan.FromMilliseconds(350)));
            Assert.Equal("two", entry.Model.Editor.SelectedText);
            Assert.True(entry.Model.IsEditMenuShown);
        }
        finally
        {
            SkUiLook.Current = previous;
        }
    }

    [Fact]
    public void WithoutAHandleTheCaretItselfIsDragged()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var previous = SkUiLook.Current;
        SkUiLook.Current = new HandleLook(handle: false);
        try
        {
            var proxy = new TestProxy(receivesCharacters: true);
            var (root, entry) = Field(new SkUiEntry { Text = "one two three" }, proxy);
            entry.Model.Measure(300, 200);
            var padding = SkUiLook.Current.TextFieldPadding.Left;
            var advance = entry.Model.Layout.Width / 13;
            Point At(double character) => new(padding + advance * character, entry.Height / 2);
            root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, At(2), TimeSpan.Zero));
            root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, At(2), TimeSpan.FromMilliseconds(50)));
            Assert.Equal(0, entry.Model.HandleAt(new Point(At(2).X, entry.Height + 6)));
            // A finger on the caret that moves drags it (with the magnifier), as on iOS.
            proxy.Calls.Clear();
            root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Pressed, At(2), TimeSpan.FromSeconds(1)));
            root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Moved, At(10), TimeSpan.FromSeconds(1.1)));
            Assert.Equal(10, entry.CursorPosition);
            root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Released, At(10), TimeSpan.FromSeconds(1.2)));
            Assert.Contains("magnifier", proxy.Calls);
            Assert.Equal("hide magnifier", proxy.Calls[^1]);
            Assert.False(entry.Model.Editor.HasSelection);
        }
        finally
        {
            SkUiLook.Current = previous;
        }
    }

    [Fact]
    public void DraggingASelectionHandleShowsTheMagnifier()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var proxy = new TestProxy(receivesCharacters: true);
        var (root, entry) = Field(new SkUiEntry { Text = "one two three" }, proxy);
        entry.Model.Measure(300, 200);
        var padding = SkUiLook.Current.TextFieldPadding.Left;
        var advance = entry.Model.Layout.Width / 13;
        Point At(double character) => new(padding + advance * character, entry.Height / 2);
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, At(5), TimeSpan.Zero));
        TestDispatcherProvider.RunDelayed();
        Assert.Contains("magnifier", proxy.Calls);
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, At(5), TimeSpan.FromSeconds(1)));
        Assert.Contains("hide magnifier", proxy.Calls);
        proxy.Calls.Clear();
        root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Pressed, At(7), TimeSpan.FromSeconds(2)));
        root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Moved, At(10.6), TimeSpan.FromSeconds(2.1)));
        Assert.Contains("magnifier", proxy.Calls);
        root.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Released, At(10.6), TimeSpan.FromSeconds(2.2)));
        Assert.Contains("hide magnifier", proxy.Calls);
        Assert.Equal("two thr", entry.Model.Editor.SelectedText);
    }

    [Fact]
    public void ARightClickOpensTheMenuAtThePoint()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var proxy = new TestProxy(receivesCharacters: true);
        var (root, entry) = Field(new SkUiEntry { Text = "abc" }, proxy);
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, new Point(20, 20), TimeSpan.Zero, Device: SkUiPointerDevice.Mouse) { Buttons = SkUiPointerButtons.Secondary });
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, new Point(20, 20), TimeSpan.FromMilliseconds(50), Device: SkUiPointerDevice.Mouse) { Buttons = SkUiPointerButtons.Secondary });
        Assert.True(entry.IsFocused);
        Assert.True(entry.Model.IsEditMenuShown);
    }
}
