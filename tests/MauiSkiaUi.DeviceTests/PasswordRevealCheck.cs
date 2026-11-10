using System.Diagnostics;
using MauiSkiaUi.LeakTests;

namespace MauiSkiaUi.DeviceTests;

/// <summary>
/// Password reveal (1.9 E1) with the platform's real text input: a focused password field revealed by a tap on its drawn button
/// stays a password field for the platform (Android: the same input connection, so the keyboard was not restarted, and a
/// password input type; iOS: the same first-responder input view, still <c>secureTextEntry</c>), keeps the keyboard and the
/// focus, never allows copying, is read as text by screen readers only while revealed (iOS: VoiceOver's element), and is masked
/// again when the focus leaves.
/// </summary>
internal static class PasswordRevealCheck
{
    private const string Secret = "Ab3 secret";
    private const string Placeholder = "Password";
    private const string FieldId = "RevealCheck";

    public static async Task<LeakResult> RunAsync()
    {
        var navigation = Shell.Current?.Navigation ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
        if (navigation is null)
            return new LeakResult("PasswordReveal", LeakStatus.Fail, "No navigation to host the check page.");
        var watch = Stopwatch.StartNew();
        var problems = new List<string>();
        var notes = new List<string>();
        ContentPage? page = null;
        try
        {
            var entry = new SkUiEntry
            {
                Text = Secret, Placeholder = Placeholder, IsPassword = true, ShowsPasswordRevealButton = true, AutomationId = FieldId
            };
            var root = new SkUiContentView { Content = new SkUiVerticalStackLayout { Padding = new Thickness(20), Children = { entry } } };
            page = new ContentPage { Title = "Password reveal", BackgroundColor = Colors.White, Content = root };
            await navigation.PushAsync(page, animated: false);
            await Task.Delay(500);
            if (root.Handler?.PlatformView is not SkUiOverlayContainer container)
                return new LeakResult("PasswordReveal", LeakStatus.Fail, "The root has no overlay container.");

            if (!entry.Focus())
                problems.Add("the field did not take focus");
            await Task.Delay(1000); // the keyboard comes up
            var masked = Platform(container);
            if (masked.Problem is { } maskedProblem)
                problems.Add("masked: " + maskedProblem);
            notes.Add("masked " + masked.Description);
            CheckSemantics("masked", root, revealed: false, problems);

            // Reveal with a tap on the drawn button, through the surface's touch routing.
            var button = SkUiSemanticsTree.Build(root).Flatten().FirstOrDefault(node => node.AutomationId == FieldId + SkUiAccessibility.PasswordRevealButtonAutomationIdSuffix);
            if (button is null)
            {
                problems.Add("no reveal button element");
            }
            else
            {
                Tap(root, button.Bounds.Center, watch.Elapsed);
                await Task.Delay(600);
            }
            if (!entry.IsPasswordRevealed)
                problems.Add("a tap on the button did not reveal");
            if (!entry.IsFocused)
                problems.Add("revealing took the focus away");
            var revealed = Platform(container);
            if (revealed.Problem is { } revealedProblem)
                problems.Add("revealed: " + revealedProblem);
            notes.Add("revealed " + revealed.Description);
            if (!ReferenceEquals(masked.Session, revealed.Session) || masked.Session is null)
                problems.Add("the platform's input session changed (the keyboard restarted)");
            if (masked.InputKind != revealed.InputKind)
                problems.Add($"the keyboard's input type changed ({masked.InputKind} → {revealed.InputKind})");
            CheckSemantics("revealed", root, revealed: true, problems);
            CheckPlatformAccessibility(container, revealed: true, problems, notes);
            CheckHitTest(container, root, "revealed", problems, notes);
            entry.Model.Editor.SelectAll();
            if (entry.Model.CanPerformEditAction(SkUiTextEditAction.Copy) || entry.Model.CanPerformEditAction(SkUiTextEditAction.Cut))
                problems.Add("a revealed password can be copied");

            // Hidden again by the button, then shown and masked by the focus leaving.
            if (button is not null)
            {
                Tap(root, button.Bounds.Center, watch.Elapsed);
                await Task.Delay(400);
                if (entry.IsPasswordRevealed)
                    problems.Add("a second tap did not mask");
                CheckPlatformAccessibility(container, revealed: false, problems, notes);
                CheckHitTest(container, root, "masked", problems, notes);
            }
            entry.IsPasswordRevealed = true;
            entry.Unfocus();
            await Task.Delay(800);
            if (entry.IsPasswordRevealed)
                problems.Add("the text stayed revealed after the focus left");
        }
        catch (Exception exception)
        {
            problems.Add($"{exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            if (page is not null)
                await navigation.PopAsync(animated: false);
        }
        return new LeakResult("PasswordReveal", problems.Count == 0 ? LeakStatus.Pass : LeakStatus.Fail,
            problems.Count == 0 ? string.Join("; ", notes) : string.Join(" · ", problems.Take(8)), watch.Elapsed.TotalSeconds);
    }

    private static int _pointer = 7_000;

    private static void Tap(SkUiView root, Point at, TimeSpan now)
    {
        var id = ++_pointer;
        root.Touch(new SkUiTouchEvent(id, SkUiTouchAction.Pressed, at, now));
        root.Touch(new SkUiTouchEvent(id, SkUiTouchAction.Released, at, now + TimeSpan.FromMilliseconds(40)));
    }

    private static void CheckSemantics(string step, SkUiView root, bool revealed, List<string> problems)
    {
        var field = SkUiSemanticsTree.Build(root).Flatten().FirstOrDefault(node => node.AutomationId == FieldId);
        if (field?.TextField is not { } text)
        {
            problems.Add($"{step}: no text field element");
            return;
        }
        if (text.IsPassword == revealed)
            problems.Add($"{step}: the element's password flag is {text.IsPassword}");
        if ((field.Value == Secret) != revealed)
            problems.Add($"{step}: the element's value is \"{field.Value}\"");
    }

    /// <summary>What the platform's input holds for the focused field.</summary>
    private readonly record struct PlatformInput(object? Session, string InputKind, string Description, string? Problem);

#if ANDROID
    private static PlatformInput Platform(SkUiOverlayContainer container)
    {
        if (container.TextInput is not { Client: not null } input)
            return new PlatformInput(null, "", "no attached field", "no field attached to the input");
        // The connection the input method holds: a restart (restartInput) asks the container for a new one.
        var session = typeof(SkUiAndroidTextInput).GetField("_connection", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(input);
        var type = InputTypeOf(input.Client);
        var imm = container.Context?.GetSystemService(Android.Content.Context.InputMethodService) as Android.Views.InputMethods.InputMethodManager;
        var active = imm is { IsActive: true, IsAcceptingText: true };
        var variation = type & Android.Text.InputTypes.MaskVariation;
        var password = variation is Android.Text.InputTypes.TextVariationPassword or Android.Text.InputTypes.TextVariationWebPassword
            or Android.Text.InputTypes.TextVariationVisiblePassword;
        string? problem = null;
        if (session is null)
            problem = "the input method holds no connection";
        else if (!password)
            problem = $"input type 0x{(int)type:x} is not a password";
        else if (!active)
            problem = "the input method is not active or does not accept text";
        return new PlatformInput(session, $"0x{(int)type:x}", $"(input type 0x{(int)type:x}, input method active {active})", problem);
    }

    private static Android.Text.InputTypes InputTypeOf(ISkUiTextInputClient client)
    {
        var info = new Android.Views.InputMethods.EditorInfo();
        typeof(SkUiAndroidTextInput).GetMethod("Configure", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, [info, client]);
        return info.InputType;
    }

    private static void CheckPlatformAccessibility(SkUiOverlayContainer container, bool revealed, List<string> problems, List<string> notes)
    {
        // TalkBack's nodes are built only while an accessibility service runs (checked with uiautomator, which is one).
    }

    private static void CheckHitTest(SkUiOverlayContainer container, SkUiView root, string step, List<string> problems, List<string> notes)
    {
        // TalkBack hit-tests the virtual view tree (SkUiSemanticsTree.HitTest), covered headless.
    }
#elif IOS || MACCATALYST
    private static PlatformInput Platform(SkUiOverlayContainer container)
    {
        if (container.TextInput?.View is not { } view)
            return new PlatformInput(null, "", "no input view", "no text input view");
        var secure = view.IsSecureTextEntry;
        var first = view.IsFirstResponder;
        string? problem = !first ? "the input view is not first responder" : !secure ? "the input view is not secureTextEntry" : null;
        return new PlatformInput(view, $"{view.KeyboardType}/secure={secure}", $"(secureTextEntry {secure}, first responder {first}, keyboard {view.KeyboardType})", problem);
    }

    private static void CheckPlatformAccessibility(SkUiOverlayContainer container, bool revealed, List<string> problems, List<string> notes)
    {
        var elements = container.DrawnAccessibilityElements();
        if (elements is null)
        {
            problems.Add("no VoiceOver elements (accessibility off?)");
            return;
        }
        UIKit.UIAccessibilityElement? field = null;
        for (nuint index = 0; index < elements.Count; index++)
            if (elements.GetItem<Foundation.NSObject>(index) is UIKit.UIAccessibilityElement element && element.AccessibilityLabel == Placeholder)
                field = element;
        if (field is null)
        {
            problems.Add("no VoiceOver element for the field");
            return;
        }
        var navigates = field is SkUiTextFieldAccessibilityElement;
        if ((field.AccessibilityValue == Secret) != revealed)
            problems.Add($"VoiceOver's value is \"{field.AccessibilityValue}\" ({(revealed ? "revealed" : "masked")})");
        if (navigates != revealed)
            problems.Add($"VoiceOver's element {(navigates ? "navigates" : "does not navigate")} the text ({(revealed ? "revealed" : "masked")})");
        if (revealed && field is SkUiTextFieldAccessibilityElement text)
        {
            var read = text.TextInRange(text.GetTextRange(text.BeginningOfDocument, text.EndOfDocument));
            notes.Add($"VoiceOver reads \"{read}\"");
            if (read != Secret)
                problems.Add($"VoiceOver's text navigation reads \"{read}\"");
        }
    }

    [System.Runtime.InteropServices.DllImport(ObjCRuntime.Constants.ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendHitTest(IntPtr receiver, IntPtr selector, CoreGraphics.CGPoint point, IntPtr uiEvent);

    /// <summary>
    /// VoiceOver's explore by touch: the element UIKit finds at the reveal button is the button, at the text the field (UIKit
    /// alone picks the top-left of overlapping frames, the field). Asked from the window when UIKit's accessibility runtime is
    /// loaded (an accessibility client ran), else from the field's element, which answers for the surface.
    /// </summary>
    private static void CheckHitTest(SkUiOverlayContainer container, SkUiView root, string step, List<string> problems, List<string> notes)
    {
        var nodes = SkUiSemanticsTree.Build(root).Flatten().ToList();
        var button = nodes.FirstOrDefault(node => node.AutomationId == FieldId + SkUiAccessibility.PasswordRevealButtonAutomationIdSuffix);
        var field = nodes.FirstOrDefault(node => node.AutomationId == FieldId);
        if (button is null || field is null || container.Window is not { } window || container.DrawnAccessibilityElements() is not { } elements)
        {
            problems.Add($"{step} hit test: no button, field, window or elements");
            return;
        }
        UIKit.UIAccessibilityElement? fieldElement = null;
        for (nuint index = 0; index < elements.Count; index++)
            if (elements.GetItem<Foundation.NSObject>(index) is UIKit.UIAccessibilityElement element && element.AccessibilityLabel == Placeholder)
                fieldElement = element;
        var selector = new ObjCRuntime.Selector("_accessibilityHitTest:withEvent:");
        var fromWindow = window.RespondsToSelector(selector);
        if (!fromWindow && fieldElement is null)
        {
            problems.Add($"{step} hit test: no field element");
            return;
        }
        // Accessibility's conversion gives nothing where no accessibility server runs (CI): then UIKit's screen space, as the elements do.
        var screenBounds = UIKit.UIAccessibility.ConvertFrameToScreenCoordinates(container.Bounds, container);
        var accessibilityConverts = screenBounds.Width > 0 && screenBounds.Height > 0;
        CoreGraphics.CGPoint ToScreen(Point at) => accessibilityConverts
            ? UIKit.UIAccessibility.ConvertFrameToScreenCoordinates(new CoreGraphics.CGRect(at.X, at.Y, 0, 0), container).Location
            : container.ConvertPointToCoordinateSpace(new CoreGraphics.CGPoint(at.X, at.Y), window.Screen.CoordinateSpace);
        string? LabelAt(Point at)
        {
            // UIView's hit test takes its own coordinates, an element's the screen's.
            var point = fromWindow ? container.ConvertPointToView(new CoreGraphics.CGPoint(at.X, at.Y), window) : ToScreen(at);
            var hit = ObjCRuntime.Runtime.GetNSObject(SendHitTest(fromWindow ? window.Handle : fieldElement!.Handle, selector.Handle, point, IntPtr.Zero));
            return hit switch
            {
                UIKit.UIAccessibilityElement element => element.AccessibilityLabel,
                null => null,
                _ => hit.GetType().Name
            };
        }
        var atButton = LabelAt(button.Bounds.Center);
        var atText = LabelAt(new Point(field.Bounds.Left + 24, field.Bounds.Center.Y));
        var via = fromWindow ? "window" : accessibilityConverts ? "field element" : "field element, UIKit screen space";
        notes.Add($"{step} hit test from the {via}: button \"{atButton}\", text \"{atText}\"");
        // Where it went wrong: the container in screen coordinates and the probe's point.
        var where = $" (from the {via}; container {container.Bounds} on screen {screenBounds}, button at {ToScreen(button.Bounds.Center)})";
        if (atButton != button.Label)
            problems.Add($"{step}: VoiceOver's hit test at the reveal button finds \"{atButton}\", not \"{button.Label}\"{where}");
        if (atText != Placeholder)
            problems.Add($"{step}: VoiceOver's hit test at the text finds \"{atText}\"{where}");
    }
#else
    private static PlatformInput Platform(SkUiOverlayContainer container) => new(container, "", "(not checked on this platform)", null);

    private static void CheckPlatformAccessibility(SkUiOverlayContainer container, bool revealed, List<string> problems, List<string> notes)
    {
    }

    private static void CheckHitTest(SkUiOverlayContainer container, SkUiView root, string step, List<string> problems, List<string> notes)
    {
    }
#endif
}
