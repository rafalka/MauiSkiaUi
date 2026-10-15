#if WINDOWS
using Microsoft.UI.Xaml.Input;
using Windows.System;
using FocusState = Microsoft.UI.Xaml.FocusState;
using RoutedEventArgs = Microsoft.UI.Xaml.RoutedEventArgs;
using UIElement = Microsoft.UI.Xaml.UIElement;
using XamlThickness = Microsoft.UI.Xaml.Thickness;
using XamlVisibility = Microsoft.UI.Xaml.Visibility;

namespace MauiSkiaUi;

/// <summary>
/// The text input of one surface on Windows (<see cref="ISkUiTextInputProxy"/>): while a drawn field is focused, a transparent WinUI
/// <see cref="Microsoft.UI.Xaml.Controls.TextBox"/> over the field's text has keyboard focus. It brings the input methods (TSF composition), the touch keyboard,
/// dictation and input scopes; the field stays the source of truth: navigation, deletion and shortcut keys go to the field, and the
/// box's text changes (typed characters, the input method's text) reach the field as one diff each, its composition as the field's
/// composition. The field's changes from keys and the app are copied back into the box.
/// </summary>
/// <remarks>
/// CoreTextEditContext (the protocol on the surface itself) needs <c>CoreTextServicesManager.GetForCurrentView()</c>, which depends
/// on a CoreWindow WinUI 3 desktop apps do not have; a hidden text box works in every host (see docs/design/TextInput.md).
/// </remarks>
internal sealed class SkUiWindowsTextInput : ISkUiTextInputProxy
{
    private readonly SkUiOverlayContainer _container;
    private readonly Microsoft.UI.Xaml.Controls.TextBox _box;
    private ISkUiTextInputClient? _client;
    private int _pushing;
    private bool _composing;
    private readonly HashSet<VirtualKey> _handledKeys = [];

    public SkUiWindowsTextInput(SkUiOverlayContainer container)
    {
        _container = container;
        _box = new Microsoft.UI.Xaml.Controls.TextBox
        {
            Opacity = 0,
            IsHitTestVisible = false,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new XamlThickness(0),
            BorderThickness = new XamlThickness(0),
            UseSystemFocusVisuals = false,
            AllowFocusOnInteraction = false,
            Visibility = XamlVisibility.Collapsed
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAccessibilityView(_box, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
        _box.TextChanged += OnBoxTextChanged;
        _box.SelectionChanged += OnBoxSelectionChanged;
        _box.TextCompositionStarted += (_, _) => _composing = true;
        _box.TextCompositionChanged += OnCompositionChanged;
        _box.TextCompositionEnded += OnCompositionEnded;
        _box.PreviewKeyDown += OnBoxPreviewKeyDown;
        _box.KeyUp += OnBoxKeyUp;
        _box.LosingFocus += OnBoxLosingFocus;
        container.Children.Add(_box);
    }

    /// <summary>Typed characters arrive through the text box (its text changes), not as key events.</summary>
    public bool ReceivesCharacters => true;

    /// <summary>Whether the text box has keyboard focus (the surface has it through the box).</summary>
    internal bool HasFocus => _box.FocusState != FocusState.Unfocused;

    /// <summary>Whether <paramref name="element"/> is the text box (the container's own focus checks).</summary>
    internal bool IsTextBox(object? element) => ReferenceEquals(element, _box);

    public void Attach(ISkUiTextInputClient client)
    {
        _client = client;
        client.InputAttached(this);
        Configure(client);
        Copy(client);
        UpdateBounds(client);
        _box.Visibility = XamlVisibility.Visible;
        if (!HasFocus)
            _box.Focus(FocusState.Programmatic);
    }

    public void Detach(ISkUiTextInputClient client)
    {
        client.InputAttached(null);
        if (!ReferenceEquals(_client, client))
            return;
        _client = null;
        _composing = false;
        // Another field taking focus keeps the box (and the touch keyboard); another drawn node gives focus to the container.
        var next = _container.KeyboardFocus?.Focused;
        if (next is ISkUiTextInputNode { WantsTextInput: true })
            return;
        if (next is not null && HasFocus)
            _container.Focus(FocusState.Programmatic);
        _box.Visibility = XamlVisibility.Collapsed;
    }

    public void ShowKeyboard(ISkUiTextInputClient client)
    {
        if (ReferenceEquals(_client, client) && !HasFocus)
            _box.Focus(FocusState.Programmatic);
    }

    public void HideKeyboard(ISkUiTextInputClient client)
    {
        // The touch keyboard hides with focus on the container; the field stays focused.
        if (ReferenceEquals(_client, client) && HasFocus)
        {
            _pushing++;
            try
            {
                _container.Focus(FocusState.Programmatic);
            }
            finally
            {
                _pushing--;
            }
        }
    }

    public void StateChanged(ISkUiTextInputClient client, bool textChanged)
    {
        if (ReferenceEquals(_client, client) && !_composing)
            Copy(client);
    }

    public void OptionsChanged(ISkUiTextInputClient client)
    {
        if (ReferenceEquals(_client, client))
            Configure(client);
    }

    public void GeometryChanged(ISkUiTextInputClient client)
    {
        if (ReferenceEquals(_client, client))
            UpdateBounds(client);
    }

    private Microsoft.UI.Xaml.Controls.MenuFlyout? _menu;

    /// <summary>A context menu of the actions the field can run now (texts from <see cref="SkUiStrings"/>), at <paramref name="anchor"/>.</summary>
    public void ShowEditMenu(ISkUiTextInputClient client, Rect anchor)
    {
        if (!ReferenceEquals(_client, client))
            return;
        _menu?.Hide();
        var strings = SkUiStrings.Current;
        var menu = new Microsoft.UI.Xaml.Controls.MenuFlyout();
        void Add(SkUiTextEditAction action, string text)
        {
            var item = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = text, IsEnabled = client.CanPerformEditAction(action) };
            item.Click += (_, _) => client.PerformEditAction(action);
            menu.Items.Add(item);
        }
        Add(SkUiTextEditAction.Cut, strings.CutText);
        Add(SkUiTextEditAction.Copy, strings.CopyText);
        Add(SkUiTextEditAction.Paste, strings.PasteText);
        menu.Items.Add(new Microsoft.UI.Xaml.Controls.MenuFlyoutSeparator());
        Add(SkUiTextEditAction.SelectAll, strings.SelectAllText);
        _menu = menu;
        menu.ShowAt(_container, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions
        {
            Position = new Windows.Foundation.Point(anchor.X, anchor.Bottom)
        });
    }

    public void HideEditMenu(ISkUiTextInputClient client)
    {
        _menu?.Hide();
        _menu = null;
    }

    private void UpdateBounds(ISkUiTextInputClient client)
    {
        // Over the field's text (container coordinates are surface DIPs): the input method places its candidates there.
        var bounds = client.GetTextBounds();
        Microsoft.UI.Xaml.Controls.Canvas.SetLeft(_box, bounds.X);
        Microsoft.UI.Xaml.Controls.Canvas.SetTop(_box, bounds.Y);
        _box.Width = Math.Max(1, bounds.Width);
        _box.Height = Math.Max(1, bounds.Height);
    }

    private void Configure(ISkUiTextInputClient client)
    {
        var options = client.Options;
        _box.AcceptsReturn = options.IsMultiline;
        _box.TextWrapping = options.IsMultiline ? Microsoft.UI.Xaml.TextWrapping.Wrap : Microsoft.UI.Xaml.TextWrapping.NoWrap;
        _box.IsSpellCheckEnabled = options.IsSpellCheckEnabled && !options.IsPassword;
        _box.IsTextPredictionEnabled = options.IsTextPredictionEnabled && !options.IsPassword;
        _box.IsReadOnly = options.IsReadOnly;
        _box.CharacterCasing = options.TextTransform switch
        {
            TextTransform.Uppercase => Microsoft.UI.Xaml.Controls.CharacterCasing.Upper,
            TextTransform.Lowercase => Microsoft.UI.Xaml.Controls.CharacterCasing.Lower,
            _ => Microsoft.UI.Xaml.Controls.CharacterCasing.Normal
        };
        var scope = options.IsPassword ? InputScopeNameValue.Password
            : options.IsNumeric ? InputScopeNameValue.Number
            : options.IsTelephone ? InputScopeNameValue.TelephoneNumber
            : options.IsEmail ? InputScopeNameValue.EmailSmtpAddress
            : options.IsUrl ? InputScopeNameValue.Url
            : options.IsChat ? InputScopeNameValue.Chat
            : options.IsSearch ? InputScopeNameValue.Search
            : InputScopeNameValue.Default;
        var inputScope = new InputScope();
        inputScope.Names.Add(new InputScopeName(scope));
        _box.InputScope = inputScope;
    }

    /// <summary>The field's text and selection into the box (not reported back).</summary>
    private void Copy(ISkUiTextInputClient client)
    {
        var editor = client.Editor;
        _pushing++;
        try
        {
            if (_box.Text != editor.Text)
                _box.Text = editor.Text;
            if (_box.SelectionStart != editor.SelectionStart || _box.SelectionLength != editor.SelectionLength)
                _box.Select(editor.SelectionStart, editor.SelectionLength);
        }
        finally
        {
            _pushing--;
        }
    }

    /// <summary>Typed characters or the input method changed the box: the field takes the change as one edit.</summary>
    private void OnBoxTextChanged(object sender, Microsoft.UI.Xaml.Controls.TextChangedEventArgs args)
    {
        if (_pushing > 0 || _client is not { } client)
            return;
        var text = _box.Text ?? string.Empty;
        var old = client.Editor.Text;
        client.BeginInputBatch();
        try
        {
            if (text != old)
            {
                var prefix = 0;
                var limit = Math.Min(text.Length, old.Length);
                while (prefix < limit && text[prefix] == old[prefix])
                    prefix++;
                var suffix = 0;
                while (suffix < limit - prefix && text[text.Length - 1 - suffix] == old[old.Length - 1 - suffix])
                    suffix++;
                client.ReplaceText(prefix, old.Length - suffix, text.Substring(prefix, text.Length - suffix - prefix));
            }
            client.SetSelection(_box.SelectionStart, _box.SelectionStart + _box.SelectionLength);
        }
        finally
        {
            client.EndInputBatch();
        }
        // The field changed the edit (a max length, a transform): the box follows it.
        if (!_composing && client.Editor.Text != text)
            Copy(client);
    }

    private void OnBoxSelectionChanged(object sender, RoutedEventArgs args)
    {
        if (_pushing > 0 || _client is not { } client || _box.Text != client.Editor.Text)
            return;
        client.SetSelection(_box.SelectionStart, _box.SelectionStart + _box.SelectionLength);
    }

    private void OnCompositionChanged(Microsoft.UI.Xaml.Controls.TextBox sender, Microsoft.UI.Xaml.Controls.TextCompositionChangedEventArgs args)
    {
        _composing = true;
        if (_pushing == 0 && _client is { } client && args.Length > 0)
            client.SetComposingRegion(args.StartIndex, args.StartIndex + args.Length);
    }

    private void OnCompositionEnded(Microsoft.UI.Xaml.Controls.TextBox sender, Microsoft.UI.Xaml.Controls.TextCompositionEndedEventArgs args)
    {
        _composing = false;
        if (_pushing == 0 && _client is { } client)
        {
            client.FinishComposing();
            Copy(client);
        }
    }

    /// <summary>
    /// Navigation, deletion, Return, Tab, Escape and shortcut keys go to the drawn tree (the field, then the focus manager); typed
    /// characters stay with the box. While the input method composes, every key is its own.
    /// </summary>
    private void OnBoxPreviewKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (_composing || _client is null || _container.KeyboardFocus is not { } focus)
            return;
        var modifiers = SkUiWindowsKeys.Modifiers();
        var key = SkUiWindowsKeys.Map(args.Key);
        var character = '\0';
        if (key == SkUiKey.None && (modifiers & SkUiKeyModifiers.Control) != 0 && args.Key is >= VirtualKey.A and <= VirtualKey.Z)
        {
            key = SkUiKey.Character;
            character = (char)('a' + (args.Key - VirtualKey.A));
        }
        // Space types into the box.
        if (key is SkUiKey.None or SkUiKey.Space)
            return;
        if (focus.KeyDown(key, modifiers, isRepeat: args.KeyStatus.WasKeyDown, character))
        {
            _handledKeys.Add(args.Key);
            args.Handled = true;
        }
    }

    private void OnBoxKeyUp(object sender, KeyRoutedEventArgs args)
    {
        if (_handledKeys.Remove(args.Key))
            args.Handled = true;
    }

    /// <summary>Focus leaving the box for something else than the container (a native control): the drawn field loses focus.</summary>
    private void OnBoxLosingFocus(UIElement sender, LosingFocusEventArgs args)
    {
        if (_client is null || ReferenceEquals(args.NewFocusedElement, _container) || ReferenceEquals(args.NewFocusedElement, _box))
            return;
        _container.KeyboardFocus?.OnNativeFocusChanged(false);
    }
}
#endif
