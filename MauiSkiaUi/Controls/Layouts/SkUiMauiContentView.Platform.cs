#if ANDROID || IOS || MACCATALYST || WINDOWS
using Microsoft.Maui.Platform;
using SkiaSharp;
#if ANDROID
using SkiaSharp.Views.Android;
using PlatformView = Android.Views.View;
#elif IOS || MACCATALYST
using SkiaSharp.Views.iOS;
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
        if (_content is null) return;
        var root = SkUiViewHandler.FindRoot(this);
        if (ReferenceEquals(root, _attachedRoot)) return;
        // Re-hosted (e.g. content moved out of a root before that root disconnected): leave the old root first.
        DetachOverlay();
        if (root is null) return;
        _attachedRoot = root;
        _nativeView = _content.ToPlatform(root.MauiContext!);
        root.AttachOverlay(_nativeView, this);
        SyncOverlayBounds();
        if (IsNativeHidden)
            SetNativeHidden(true);
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
        _attachedRoot.UpdateOverlayBounds(_nativeView, ComputeRootRelativeFrame(), ComputeRootRelativeClip());
    }

    partial void SetNativeHidden(bool hidden)
    {
        if (_attachedRoot is not null && _nativeView is not null)
            _attachedRoot.SetOverlayHidden(_nativeView, hidden);
    }

    partial void StartCapture(Action<SKImage?> done, ref bool started)
    {
        if (_nativeView is not { } view)
            return;
#if ANDROID
        if (view.Width <= 0 || view.Height <= 0)
            return;
        // Software draw of the view hierarchy: works for text inputs and WebView, and is unaffected by what covers the
        // view on screen (a window pixel copy would also capture neighbours of a partly clipped overlay).
        using var bitmap = Android.Graphics.Bitmap.CreateBitmap(view.Width, view.Height, Android.Graphics.Bitmap.Config.Argb8888!)!;
        using (var canvas = new Android.Graphics.Canvas(bitmap))
            view.Draw(canvas);
        started = true;
        done(bitmap.ToSKImage());
#elif IOS || MACCATALYST
        var size = view.Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
            return;
        using var renderer = new UIKit.UIGraphicsImageRenderer(size);
        using var image = renderer.CreateImage(_ => view.DrawViewHierarchy(view.Bounds, afterScreenUpdates: false));
        started = true;
        done(image.ToSKImage());
#elif WINDOWS
        if (view.ActualWidth <= 0 || view.ActualHeight <= 0)
            return;
        started = true;
        CaptureWindows(view, done);
#endif
    }

#if WINDOWS
    private static async void CaptureWindows(PlatformView view, Action<SKImage?> done)
    {
        SKImage? image = null;
        try
        {
            // RenderTargetBitmap cannot capture WebView2 content (it comes back blank); ask WebView2 itself.
            if (view is Microsoft.UI.Xaml.Controls.WebView2 { CoreWebView2: { } core })
            {
                using var stream = new global::Windows.Storage.Streams.InMemoryRandomAccessStream();
                await core.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, stream);
                using var reader = new global::Windows.Storage.Streams.DataReader(stream.GetInputStreamAt(0));
                var bytes = new byte[stream.Size];
                await reader.LoadAsync((uint)bytes.Length);
                reader.ReadBytes(bytes);
                image = SKImage.FromEncodedData(bytes);
            }
            else
            {
                var target = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();
                await target.RenderAsync(view);
                var pixels = System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.ToArray(await target.GetPixelsAsync());
                image = SKImage.FromPixelCopy(new SKImageInfo(target.PixelWidth, target.PixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul), pixels);
            }
        }
        catch (Exception)
        {
            image = null; // capture unavailable: stay live
        }
        done(image);
    }
#endif
}
#endif
