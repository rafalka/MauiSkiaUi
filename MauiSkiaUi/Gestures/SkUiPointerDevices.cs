namespace MauiSkiaUi;

/// <summary>Maps each platform's pointer kinds to <see cref="SkUiPointerDevice"/>.</summary>
internal static class SkUiPointerDevices
{
#if ANDROID || IOS || MACCATALYST || WINDOWS
    /// <summary>SkiaSharp's touch events (software surfaces, Windows).</summary>
    public static SkUiPointerDevice From(SkiaSharp.Views.Maui.SKTouchDeviceType type) => type switch
    {
        SkiaSharp.Views.Maui.SKTouchDeviceType.Mouse => SkUiPointerDevice.Mouse,
        SkiaSharp.Views.Maui.SKTouchDeviceType.Pen => SkUiPointerDevice.Pen,
        _ => SkUiPointerDevice.Touch
    };
#endif

#if ANDROID
    /// <summary>The tool of one pointer of a motion event.</summary>
    public static SkUiPointerDevice From(Android.Views.MotionEvent e, int index) => e.GetToolType(index) switch
    {
        Android.Views.MotionEventToolType.Mouse => SkUiPointerDevice.Mouse,
        Android.Views.MotionEventToolType.Stylus or Android.Views.MotionEventToolType.Eraser => SkUiPointerDevice.Pen,
        _ => SkUiPointerDevice.Touch
    };
#elif IOS || MACCATALYST
    /// <summary>
    /// A UIKit touch: the iPad pointer (mouse, trackpad) is <see cref="SkUiPointerDevice.Mouse"/>, the Apple Pencil a pen. Mac
    /// Catalyst has no touch screen: every touch comes from a mouse or a trackpad.
    /// </summary>
    public static SkUiPointerDevice From(UIKit.UITouch touch) =>
        OperatingSystem.IsMacCatalyst() ? SkUiPointerDevice.Mouse
        : touch.Type switch
        {
            UIKit.UITouchType.IndirectPointer => SkUiPointerDevice.Mouse,
            UIKit.UITouchType.Stylus => SkUiPointerDevice.Pen,
            _ => SkUiPointerDevice.Touch
        };
#endif

#if ANDROID
    /// <summary>The modifier keys of a motion event (a hardware keyboard's Shift, Ctrl, Alt or Meta).</summary>
    public static SkUiKeyModifiers ModifiersFrom(Android.Views.MetaKeyStates meta) =>
        (meta.HasFlag(Android.Views.MetaKeyStates.ShiftOn) ? SkUiKeyModifiers.Shift : 0)
        | (meta.HasFlag(Android.Views.MetaKeyStates.CtrlOn) ? SkUiKeyModifiers.Control : 0)
        | (meta.HasFlag(Android.Views.MetaKeyStates.AltOn) ? SkUiKeyModifiers.Alt : 0)
        | (meta.HasFlag(Android.Views.MetaKeyStates.MetaOn) ? SkUiKeyModifiers.Command : 0);
#elif IOS || MACCATALYST
    /// <summary>The modifier keys of a UIKit event (iOS 13.4+ and Mac Catalyst; none before).</summary>
    public static SkUiKeyModifiers ModifiersFrom(UIKit.UIEvent? evt)
    {
        if (evt is null || !(OperatingSystem.IsIOSVersionAtLeast(13, 4) || OperatingSystem.IsMacCatalystVersionAtLeast(13, 4)))
            return SkUiKeyModifiers.None;
        var flags = evt.ModifierFlags;
        return (flags.HasFlag(UIKit.UIKeyModifierFlags.Shift) ? SkUiKeyModifiers.Shift : 0)
            | (flags.HasFlag(UIKit.UIKeyModifierFlags.Control) ? SkUiKeyModifiers.Control : 0)
            | (flags.HasFlag(UIKit.UIKeyModifierFlags.Alternate) ? SkUiKeyModifiers.Alt : 0)
            | (flags.HasFlag(UIKit.UIKeyModifierFlags.Command) ? SkUiKeyModifiers.Command : 0);
    }
#endif

#if WINDOWS
    /// <summary>A WinUI pointer.</summary>
    public static SkUiPointerDevice From(Microsoft.UI.Input.PointerDeviceType type) => type switch
    {
        Microsoft.UI.Input.PointerDeviceType.Mouse => SkUiPointerDevice.Mouse,
        Microsoft.UI.Input.PointerDeviceType.Pen => SkUiPointerDevice.Pen,
        _ => SkUiPointerDevice.Touch
    };
#endif
}
