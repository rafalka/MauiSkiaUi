#if ANDROID
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using Android.Content;

namespace MauiSkiaUi;

/// <summary>
/// Android system services kept across calls. A system service (the input method manager, the accessibility manager) is one
/// Java object per process or context, so every <c>GetSystemService</c> in the app gets the same .NET peer from the JNI
/// identity map, and code that disposes its copy (MAUI 10.0.130 to 11.0.10 <c>using</c> the input method manager on every
/// native field's focus change, issue #140) disposes everyone's. A kept service is checked before each use and looked up
/// again once its peer is gone (the lookup then gets a fresh peer).
/// </summary>
internal static class SkUiAndroidServices
{
    /// <summary>Whether <paramref name="service"/> is a live peer (not <c>null</c>, not disposed).</summary>
    public static bool IsAlive([NotNullWhen(true)] this Java.Lang.Object? service) => service is { Handle: not 0 };

    /// <summary><paramref name="kept"/> while its peer is alive, otherwise <paramref name="context"/>'s service <paramref name="name"/> (kept from now on).</summary>
    public static T? Get<T>(ref T? kept, Context? context, string name) where T : Java.Lang.Object
        => kept.IsAlive() ? kept : kept = context?.GetSystemService(name) as T;

    /// <summary><paramref name="kept"/> while its peer is alive, otherwise <paramref name="context"/>'s service of type <typeparamref name="T"/> (kept from now on).</summary>
    [SupportedOSPlatform("android23.0")]
    public static T? Get<T>(ref T? kept, Context? context) where T : Java.Lang.Object
        => kept.IsAlive() ? kept : kept = context?.GetSystemService(Java.Lang.Class.FromType(typeof(T))) as T;
}
#endif
