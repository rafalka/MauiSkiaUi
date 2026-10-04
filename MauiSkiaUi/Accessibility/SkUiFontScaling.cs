namespace MauiSkiaUi;

/// <summary>
/// The operating system's text size, applied to drawn text whose <c>FontAutoScalingEnabled</c> is set (the default, as in
/// MAUI): Android's font scale (with Android 14's non-linear curve, like <c>sp</c> units), iOS / Mac Catalyst Dynamic
/// Type (the body text style's curve, as MAUI's <c>UIFontMetrics</c> scaling) and Windows' text scaling. Labels, buttons,
/// radio button text and font images (<c>FontImageSource</c>) follow it on both layers; live surfaces re-measure when it
/// changes. Every text size is first multiplied by the current look's <see cref="SkUiLook.FontScale"/> (all text, also
/// without auto scaling), so the drawn size is that prescale × the system scale.
/// </summary>
public static class SkUiFontScaling
{
    private static readonly SkUiWeakEvent _changed = new();
    private static readonly Dictionary<double, double> _systemSizes = [];
    private static double? _factor;
    private static int _version;
#if ANDROID
    private static ConfigurationWatcher? _watcher;
#elif IOS || MACCATALYST
    private static Foundation.NSObject? _observer;
#elif WINDOWS
    private static Windows.UI.ViewManagement.UISettings? _settings;
#endif

    /// <summary>
    /// A linear factor that replaces the system text size for text that auto scales (1: no system scaling), e.g. in tests;
    /// <c>null</c> (default) follows the system. Setting it raises <see cref="Changed"/>. For an app's own text size setting
    /// that keeps the system's, use <see cref="SkUiLook.FontScale"/>.
    /// </summary>
    public static double? Factor
    {
        get => _factor;
        set
        {
            if (value is { } factor && (!double.IsFinite(factor) || factor <= 0))
                throw new ArgumentOutOfRangeException(nameof(value), value, "The factor must be finite and positive.");
            if (_factor == value) return;
            _factor = value;
            NotifyChanged();
        }
    }

    /// <summary>
    /// Raised when the text size changes (the system setting, or <see cref="Factor"/>); live surfaces then re-measure and
    /// redraw. Raised on the UI thread for system changes on Android and Apple, on a background thread on Windows.
    /// Subscribers are not kept alive by the event.
    /// </summary>
    public static event EventHandler? Changed
    {
        add => _changed.Add(value);
        remove => _changed.Remove(value);
    }

    static SkUiFontScaling() => SkUiLook.CurrentChanged += OnLookChanged;

    /// <summary>A look swap or change may change <see cref="SkUiLook.FontScale"/>: text caches rebuild (surfaces re-measure by themselves).</summary>
    private static void OnLookChanged(object? sender, EventArgs args) => Interlocked.Increment(ref _version);

    /// <summary>
    /// The size drawn for <paramref name="fontSize"/> (DIPs) when auto scaling is on: prescaled by
    /// <see cref="SkUiLook.FontScale"/>, then by the system text size (or <see cref="Factor"/>).
    /// </summary>
    public static double ScaleFontSize(double fontSize) => ScaleFontSize(fontSize, autoScalingEnabled: true);

    /// <summary>
    /// The size drawn for <paramref name="fontSize"/> (DIPs): prescaled by <see cref="SkUiLook.FontScale"/>, then, with
    /// <paramref name="autoScalingEnabled"/>, by the system text size (or <see cref="Factor"/>).
    /// </summary>
    public static double ScaleFontSize(double fontSize, bool autoScalingEnabled)
    {
        if (!double.IsFinite(fontSize) || fontSize <= 0)
            return fontSize;
        fontSize *= SkUiLook.Current.FontScale;
        return autoScalingEnabled ? SystemScaled(fontSize) : fontSize;
    }

    private static double SystemScaled(double fontSize)
    {
        if (_factor is { } factor)
            return fontSize * factor;
        lock (_systemSizes)
        {
            if (_systemSizes.TryGetValue(fontSize, out var cached))
                return cached;
        }
        if (SystemScale(fontSize) is not { } scaled)
            return fontSize;
        lock (_systemSizes)
            _systemSizes[fontSize] = scaled;
        return scaled;
    }

    /// <summary>Increments on every change (caches of text built at the old sizes compare it).</summary>
    internal static int Version => Volatile.Read(ref _version);

    private static void NotifyChanged()
    {
        lock (_systemSizes)
            _systemSizes.Clear();
        Interlocked.Increment(ref _version);
        _changed.Raise(null, EventArgs.Empty);
    }

    /// <summary>The system's size for <paramref name="fontSize"/>, or <c>null</c> when it cannot be read here (keep the size).</summary>
    private static double? SystemScale(double fontSize)
    {
#if ANDROID
        if (Android.App.Application.Context is not { Resources.DisplayMetrics: { } metrics } context || metrics.Density <= 0)
            return null;
        _watcher ??= ConfigurationWatcher.Register(context);
        // As sp units: Android 14+ grows large text less than small text (non-linear font scale).
        return Android.Util.TypedValue.ApplyDimension(Android.Util.ComplexUnitType.Sp, (float)fontSize, metrics) / metrics.Density;
#elif IOS || MACCATALYST
        // UIKit: the main thread only (a font image may be created elsewhere; it then keeps its size).
        if (!Foundation.NSThread.IsMain)
            return null;
        _observer ??= UIKit.UIApplication.Notifications.ObserveContentSizeCategoryChanged((_, _) => NotifyChanged());
        return UIKit.UIFontMetrics.DefaultMetrics.GetScaledValue((System.Runtime.InteropServices.NFloat)fontSize);
#elif WINDOWS
        try
        {
            if (_settings is null)
            {
                _settings = new Windows.UI.ViewManagement.UISettings();
                _settings.TextScaleFactorChanged += (_, _) => NotifyChanged();
            }
            return fontSize * _settings.TextScaleFactor;
        }
        catch (Exception)
        {
            return null;
        }
#else
        return null;
#endif
    }

#if ANDROID
    /// <summary>Clears the cached sizes when the font scale changes (the activity may also be recreated).</summary>
    private sealed class ConfigurationWatcher : Java.Lang.Object, Android.Content.IComponentCallbacks
    {
        private float _fontScale;

        public static ConfigurationWatcher Register(Android.Content.Context context)
        {
            var watcher = new ConfigurationWatcher { _fontScale = context.Resources?.Configuration?.FontScale ?? 1 };
            context.RegisterComponentCallbacks(watcher);
            return watcher;
        }

        public void OnConfigurationChanged(Android.Content.Res.Configuration newConfig)
        {
            if (newConfig.FontScale == _fontScale)
                return;
            _fontScale = newConfig.FontScale;
            NotifyChanged();
        }

        public void OnLowMemory() { }
    }
#endif
}
