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
    private SkUiViewHandler? _attachedRoot;
    private PlatformView? _nativeView;

    partial void AttachOverlayIfPossible()
    {
        if (_content is null || _attachedRoot is not null) return;
        if (SkUiViewHandler.FindRoot(this) is not { } root) return;
        _attachedRoot = root;
        _nativeView = _content.ToPlatform(root.MauiContext!);
        root.AttachOverlay(_nativeView);
        SyncOverlayBounds();
    }

    partial void DetachOverlay()
    {
        if (_attachedRoot is null) return;
        if (_nativeView is not null) _attachedRoot.DetachOverlay(_nativeView);
        _content?.Handler?.DisconnectHandler();
        _nativeView = null;
        _attachedRoot = null;
    }

    partial void SyncOverlayBounds()
    {
        if (_attachedRoot is null || _nativeView is null) return;
        _attachedRoot.UpdateOverlayBounds(_nativeView, ComputeRootRelativeFrame());
    }
}
#endif
