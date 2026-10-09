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
#elif WINDOWS
    /// <summary>A WinUI pointer.</summary>
    public static SkUiPointerDevice From(Microsoft.UI.Input.PointerDeviceType type) => type switch
    {
        Microsoft.UI.Input.PointerDeviceType.Mouse => SkUiPointerDevice.Mouse,
        Microsoft.UI.Input.PointerDeviceType.Pen => SkUiPointerDevice.Pen,
        _ => SkUiPointerDevice.Touch
    };
#endif
}
