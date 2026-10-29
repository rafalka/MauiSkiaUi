using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// The fade-in of a loaded image (<c>FadeAnimationEnabled</c>), shared by the image views of both layers. The view
/// records its content without the image (the placeholder that was showing, kept until the fade ends) and commits the
/// image as a <see cref="SkUiImageFadeDrawable"/> the compositor draws over it with an opacity from the render clock, so
/// nothing is recorded per frame. A UI-clock animation of the same length ends the fade: the view then records the
/// image in its content again and drops the drawable. Stopping the clock (the page closed) ends it at once, and so does
/// the host leaving the clock's tree.
/// </summary>
internal sealed class SkUiImageFade
{
    private IDisposable? _handle;
    private TimeSpan _start;
    private TimeSpan _duration;
    private Easing _easing = Easing.CubicOut;
    private SkUiDrawableSlot<(SKImage Frame, float Width, float Height, Aspect Aspect)> _drawable;

    /// <summary>Whether a fade runs.</summary>
    public bool IsActive => _handle is not null;

    /// <summary>
    /// Whether the content being recorded is the fade's own layer (the image), not the view's content (the placeholder).
    /// </summary>
    public bool PaintsImage { get; private set; }

    /// <summary>
    /// Starts a fade on <paramref name="host"/>'s clock: <paramref name="duration"/> in milliseconds, else the look's
    /// <see cref="SkUiTransitionKind.ImageFadeIn"/>. <c>false</c> (no fade) before the host was drawn (a page that is
    /// not shown yet shows its images at once), with motion reduced, or for a zero duration. <paramref name="end"/> runs
    /// on the UI thread when the fade finished or its clock stopped.
    /// </summary>
    public bool Start(ISkUiTransitionHost host, int? duration, Action end)
    {
        Finish();
        if (host.TransitionClock is not { } clock || SkUiMotion.IsMotionReduced)
            return false;
        var transition = host.TransitionLook.GetTransition(SkUiTransitionKind.ImageFadeIn);
        var length = duration is { } milliseconds ? TimeSpan.FromMilliseconds(milliseconds) : transition.Duration;
        if (length <= TimeSpan.Zero)
            return false;
        _start = SkUiRenderClock.Now;
        _duration = length;
        _easing = transition.Easing ?? Easing.CubicOut;
        // A host that left the clock's tree (removed, recycled, moved to another surface) ends its fade at the next tick,
        // so the clock does not keep it until the fade would have ended.
        _handle = clock.Start(progress =>
        {
            if (progress >= 1 || !ReferenceEquals(host.TransitionClock, clock))
                end();
        }, length, easing: null, repeat: false, stopped: end);
        return true;
    }

    /// <summary>Stops the fade; <c>false</c> when none ran (so a nested call from the clock's stop does nothing).</summary>
    public bool Finish()
    {
        if (_handle is not { } handle)
            return false;
        _handle = null;
        handle.Dispose(); // runs the stop callback, which finds no fade
        return true;
    }

    /// <summary>
    /// The drawable of <paramref name="frame"/> (the shown frame) as <paramref name="paint"/> draws it at
    /// <paramref name="width"/> × <paramref name="height"/>; kept while those stay the same, so committing again commits
    /// the same object. <c>null</c> without a fade or a frame.
    /// </summary>
    public SkUiRenderDrawable? GetDrawable(SKImage? frame, float width, float height, Aspect aspect, Action<SKCanvas> paint)
    {
        if (!IsActive || frame is null || width <= 0 || height <= 0)
        {
            _drawable.Forget();
            return null;
        }
        return _drawable.Get((frame, width, height, aspect), (this, width, height, paint), static state =>
        {
            using var recorder = new SKPictureRecorder();
            var canvas = recorder.BeginRecording(new SKRect(0, 0, state.width, state.height));
            state.Item1.PaintsImage = true;
            try
            {
                state.paint(canvas);
            }
            finally
            {
                state.Item1.PaintsImage = false;
            }
            var fade = state.Item1;
            return new SkUiImageFadeDrawable(recorder.EndRecording(), fade._start, fade._duration, fade._easing);
        });
    }

    /// <summary>Forgets the committed drawable (the fade ended, or the node was reset: the compositor disposes it).</summary>
    public void ForgetDrawable() => _drawable.Forget();
}

/// <summary>
/// An image fading in, drawn by the compositor over the node's content: its picture at the opacity
/// <paramref name="easing"/> gives the time since <paramref name="start"/> on the render clock. Owns the picture.
/// </summary>
internal sealed class SkUiImageFadeDrawable(SKPicture picture, TimeSpan start, TimeSpan duration, Easing easing) : SkUiRenderDrawable
{
    /// <inheritdoc />
    public override bool IsAnimated => true;

    /// <inheritdoc />
    public override void Draw(SKCanvas canvas, in SkUiRenderDrawContext context)
    {
        // The immediate painter's still frame (time zero) shows the image.
        var progress = context.Now == TimeSpan.Zero ? 1 : Math.Clamp((context.Now - start) / duration, 0, 1);
        var alpha = (byte)Math.Round(255 * Math.Clamp(easing.Ease(progress), 0, 1));
        if (alpha == 0)
            return;
        if (alpha == 255)
        {
            canvas.DrawPicture(picture);
            return;
        }
        context.Paint.Color = SKColors.Black.WithAlpha(alpha);
        canvas.DrawPicture(picture, context.Paint);
    }

    /// <inheritdoc />
    public override void Dispose() => picture.Dispose();
}
