#if ANDROID || IOS || MACCATALYST || WINDOWS
using Microsoft.Maui.Platform;
#if ANDROID
using PlatformView = Android.Views.View;
#elif IOS || MACCATALYST
using PlatformView = UIKit.UIView;
#elif WINDOWS
using PlatformView = Microsoft.UI.Xaml.FrameworkElement;
#endif

namespace MauiSkiaUi;

public partial class SkUiMauiContentView
{
    private SkUiViewHandler? attachedRoot;
    private PlatformView? nativeView;

    partial void AttachOverlayIfPossible()
    {
        if (content is null || attachedRoot is not null) return;
        if (SkUiViewHandler.FindRoot(this) is not { } root) return;
        attachedRoot = root;
        nativeView = content.ToPlatform(root.MauiContext!);
        root.AttachOverlay(nativeView);
        SyncOverlayBounds();
    }

    partial void DetachOverlay()
    {
        if (attachedRoot is null) return;
        if (nativeView is not null) attachedRoot.DetachOverlay(nativeView);
        content?.Handler?.DisconnectHandler();
        nativeView = null;
        attachedRoot = null;
    }

    partial void SyncOverlayBounds()
    {
        if (attachedRoot is null || nativeView is null) return;
        attachedRoot.UpdateOverlayBounds(nativeView, ComputeRootRelativeFrame());
    }
}
#endif
