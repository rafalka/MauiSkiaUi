using SkiaSharp;

namespace MauiSkiaUi.Rendering;

/// <summary>How a <see cref="SkUiRenderReveal"/> answers its scroller's movement.</summary>
internal enum SkUiRenderRevealResponse : byte
{
    /// <summary>The progress moves with the scroll: <see cref="SkUiRenderRevealSettings.Distance"/> DIPs from shown to hidden.</summary>
    Tracked,

    /// <summary>
    /// After <see cref="SkUiRenderRevealSettings.Threshold"/> DIPs in one direction, the progress moves fully in or out over
    /// <see cref="SkUiRenderRevealSettings.Duration"/>, whatever the scroll does after that.
    /// </summary>
    Triggered,

    /// <summary>The scroll does not move it: only <see cref="SkUiRenderRevealRequest.Show"/> and <see cref="SkUiRenderRevealRequest.Hide"/> do.</summary>
    Manual
}

/// <summary>A request from the UI side to a <see cref="SkUiRenderReveal"/>.</summary>
internal enum SkUiRenderRevealRequest
{
    None,

    /// <summary>The scroller came to rest: a tracked progress between the ends moves to the nearer one.</summary>
    Settle,
    Show,
    Hide
}

/// <summary>A <see cref="SkUiRenderReveal"/>'s settings: immutable, replaced as a whole by the UI side.</summary>
/// <param name="Response">How it answers the scroll.</param>
/// <param name="Distance">Tracked: DIPs of scroll from shown to hidden; both: the band at the start in which it is capped.</param>
/// <param name="Threshold">Triggered: DIPs in one direction that start a show or hide.</param>
/// <param name="Duration">Seconds of a full move (trigger, settle, show, hide); shorter moves take their share.</param>
/// <param name="ShowsNearStart">The progress never exceeds the offset over <paramref name="Distance"/>: fully shown at the start.</param>
internal sealed record SkUiRenderRevealSettings(
    SkUiRenderRevealResponse Response, float Distance, float Threshold, float Duration, bool ShowsNearStart)
{
    public static SkUiRenderRevealSettings Default { get; } = new(SkUiRenderRevealResponse.Tracked, 56, 24, 0.2f, true);
}

/// <summary>
/// A scroll-direction input (1.16 D10): turns a scroller's movement into a progress from 0 (shown) to 1 (hidden) that
/// <see cref="SkUiRenderLink"/>s read instead of an offset, so views hide while the list scrolls one way and come back when
/// it scrolls the other, anywhere in it. The progress depends on the path, not the offset, so it is state: it lives here
/// and is advanced on the render thread, from the source's offset each time a node that reads it is drawn (idempotent
/// within a frame: the offset and the clock are the same), and is what is drawn. The UI side reads the published value
/// (<see cref="Progress"/>, a frame behind at most) for hit-testing and screen readers, and hears of the end of a timed move
/// through <see cref="MoveEnded"/>.
/// </summary>
/// <remarks>
/// Threads: the UI side writes <see cref="Settings"/>, the range and requests (lock-free: a whole object, single floats, a
/// serial number); the render thread owns everything else. Shared by every channel (and node) it drives, so they move as
/// one. Holds no native objects.
/// </remarks>
internal sealed class SkUiRenderReveal(SkUiRenderNode source, bool horizontal)
{
    /// <summary>A jump of more than this many viewports in one frame resets the progress to shown (a jump, not a scroll).</summary>
    private const float JumpViewports = 2;

    private SkUiRenderRevealSettings _settings = SkUiRenderRevealSettings.Default;
    private float _maxOffset = float.PositiveInfinity;
    private volatile SkUiRenderRevealRequest _request;
    private int _requestSerial;
    private float _published;

    // Render thread only.
    private bool _started;
    private float _progress;
    private float _lastInput;
    private SKPoint _lastCorrection;
    private TimeSpan _lastTime;
    private float _run;
    private float _target = float.NaN;
    private int _seenSerial;

    /// <summary>The scroller whose offset drives it.</summary>
    public SkUiRenderNode Source { get; } = source;

    /// <summary>Whether the horizontal offset drives it.</summary>
    public bool Horizontal { get; } = horizontal;

    /// <summary>The settings (UI side writes, render thread reads).</summary>
    public SkUiRenderRevealSettings Settings
    {
        get => Volatile.Read(ref _settings);
        set => Volatile.Write(ref _settings, value);
    }

    /// <summary>
    /// The scroller's largest offset on the driving axis (UI side, on each scroll report): an offset past it or below 0 is
    /// overscroll, which does not move the progress; <see cref="float.PositiveInfinity"/> until known.
    /// </summary>
    public float MaxOffset
    {
        get => Volatile.Read(ref _maxOffset);
        set => Volatile.Write(ref _maxOffset, value);
    }

    /// <summary>The progress last drawn, 0 (shown) to 1 (hidden): what the UI side reads.</summary>
    public float Progress => Volatile.Read(ref _published);

    /// <summary>Whether a timed move (trigger, settle, show, hide) is running: the compositor keeps frames coming.</summary>
    public bool IsMoving => !float.IsNaN(_target);

    /// <summary>
    /// Raised on the UI thread when a timed move ended (the compositor posts it with the frame's reports), so the UI side
    /// re-reads <see cref="Progress"/> without a scroll report.
    /// </summary>
    public event Action<SkUiRenderReveal>? MoveEnded;

    /// <summary>Asks for a settle, show or hide (UI side); the render thread acts on the latest request once.</summary>
    public void Request(SkUiRenderRevealRequest request)
    {
        _request = request;
        Interlocked.Increment(ref _requestSerial);
    }

    /// <summary>Raises <see cref="MoveEnded"/> (UI thread, from the compositor's reports).</summary>
    internal void ReportMoveEnded() => MoveEnded?.Invoke(this);

    /// <summary>
    /// Advances the progress to the source's current offset at <paramref name="now"/> (render thread) and returns it. Calls
    /// in the same frame see the same offset and time and change nothing. <paramref name="ended"/> is true when a timed
    /// move ended in this call.
    /// </summary>
    public float Advance(TimeSpan now, out bool ended)
    {
        ended = false;
        var settings = Settings;
        ref readonly var props = ref Source.Props;
        var input = Math.Clamp(Horizontal ? props.ChildrenOffsetX : props.ChildrenOffsetY, 0, Math.Max(0, MaxOffset));
        var correction = Source.ScrollCorrection;
        if (!_started)
        {
            _started = true;
            _lastInput = input;
            _lastCorrection = correction;
            _lastTime = now;
            _seenSerial = Volatile.Read(ref _requestSerial);
        }
        var seconds = (float)Math.Max(0, (now - _lastTime).TotalSeconds);
        _lastTime = now;
        // What the scroll moved, without the corrections (rows inserted above move the offset, not the list).
        var shift = Horizontal ? correction.X - _lastCorrection.X : correction.Y - _lastCorrection.Y;
        var delta = input - _lastInput - shift;
        _lastInput = input;
        _lastCorrection = correction;

        var viewport = Horizontal
            ? props.ChildrenClipRect.IsEmpty ? props.Width : props.ChildrenClipRect.Width
            : props.ChildrenClipRect.IsEmpty ? props.Height : props.ChildrenClipRect.Height;
        if (settings.Response == SkUiRenderRevealResponse.Manual)
        {
            // Only the app's requests move it.
        }
        else if (viewport > 0 && Math.Abs(delta) > JumpViewports * viewport)
        {
            // A jump (a scroll to somewhere far without animation): back to shown.
            ended |= EndMove();
            _progress = 0;
            _run = 0;
        }
        else if (delta != 0) // exact: no movement at all is the common case between frames
        {
            if (settings.Response == SkUiRenderRevealResponse.Tracked)
            {
                // The finger (or a fling) takes over from a settle or a show / hide.
                ended |= EndMove();
                if (settings.Distance > 0)
                    _progress = Math.Clamp(_progress + delta / settings.Distance, 0, 1);
            }
            else
            {
                // A run in one direction; past the threshold, a move to that end.
                _run = Math.Sign(delta) == Math.Sign(_run) ? _run + delta : delta;
                if (_run >= settings.Threshold && !Targets(1))
                    StartMove(1);
                else if (_run <= -settings.Threshold && !Targets(0))
                    StartMove(0);
            }
        }

        var serial = Volatile.Read(ref _requestSerial);
        if (serial != _seenSerial)
        {
            _seenSerial = serial;
            switch (_request)
            {
                case SkUiRenderRevealRequest.Settle when settings.Response == SkUiRenderRevealResponse.Tracked && !IsMoving:
                    if (_progress is > 0 and < 1)
                        StartMove(_progress < 0.5f ? 0 : 1);
                    break;
                case SkUiRenderRevealRequest.Show:
                    StartMove(0);
                    break;
                case SkUiRenderRevealRequest.Hide:
                    StartMove(1);
                    break;
            }
        }

        if (IsMoving)
        {
            var step = settings.Duration > 0 ? seconds / settings.Duration : 1;
            _progress = _target > _progress ? Math.Min(_target, _progress + step) : Math.Max(_target, _progress - step);
            // Exact: the move lands on its target value, set above.
            if (_progress == _target)
                ended |= EndMove();
        }

        if (settings.ShowsNearStart && settings.Distance > 0 && settings.Response != SkUiRenderRevealResponse.Manual)
        {
            var cap = input / settings.Distance;
            if (_progress > cap)
            {
                _progress = cap;
                // A hide held down by the start would never arrive: it ends where the cap holds it.
                if (Targets(1))
                    ended |= EndMove();
            }
        }
        Volatile.Write(ref _published, _progress);
        return _progress;
    }

    // Exact comparison: targets are the literal ends 0 and 1.
    private bool Targets(float end) => _target == end;

    private void StartMove(float target)
    {
        _target = target;
        _run = 0;
    }

    /// <summary>Ends a running move; true when one was running.</summary>
    private bool EndMove()
    {
        if (!IsMoving)
            return false;
        _target = float.NaN;
        return true;
    }
}
