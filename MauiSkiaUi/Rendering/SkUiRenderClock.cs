using System.Diagnostics;

namespace MauiSkiaUi.Rendering;

/// <summary>
/// The one time base of every surface's render thread (Metal, GL, Windows, software): composite-time animations that follow
/// the clock instead of their own start (spinners, sliding content, shimmers) are in phase on every surface of the app.
/// </summary>
internal static class SkUiRenderClock
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    /// <summary>Monotonic time since the first use, from any thread.</summary>
    public static TimeSpan Now => Clock.Elapsed;
}
