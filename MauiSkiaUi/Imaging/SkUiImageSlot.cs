using System.Diagnostics;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// One displayed image of a node, shared by both layers (images, image buttons, slider thumbs): loads through
/// <see cref="SkUiImageLoader"/>, holds the lease on the cached image, publishes loading / error state, raises the
/// load events, shows the loading / error placeholders, and plays animated images on the node's UI clock. A cached
/// source applies synchronously (no empty frame); otherwise the image clears (the loading placeholder shows) while
/// loading and the result is applied on the dispatcher that started the load. UI thread only.
/// </summary>
internal sealed class SkUiImageSlot
{
    private readonly ISkUiTransitionHost _host;
    private readonly Action _changed;
    private SkUiCachedImage? _image;
    private CancellationTokenSource? _loading;
    private int _generation;
    private bool _disposed;
    private bool _playing;
    private IDisposable? _animation;
    private int _frame;
    private long _frameOffset;

    // The load in progress, for its LoadingFinished.
    private SkUiImageSource? _pending;
    private long _pendingStarted;

    // Placeholders: shown by a nested slot while the main image loads or after it failed.
    private SkUiImageOptions _options;
    private SkUiImageSource? _loadingPlaceholder;
    private SkUiImageSource? _errorPlaceholder;
    private bool _transformPlaceholders = true;
    private SkUiImageSlot? _placeholder;
    private SkUiImageSource? _shownPlaceholder;

    /// <param name="host">The node: its clock plays animations, its paint invalidation shows new frames.</param>
    /// <param name="changed">Called after the image, a placeholder, <see cref="IsLoading"/> or <see cref="LoadError"/> changed.</param>
    public SkUiImageSlot(ISkUiTransitionHost host, Action changed)
    {
        _host = host;
        _changed = changed;
    }

    /// <summary>Raised when a load of a source starts (after the state shows it loading).</summary>
    public Action<SkUiImageLoadStartedEventArgs>? LoadingStarted { get; set; }

    /// <summary>Raised once per started load: succeeded, failed or cancelled (after the state is published).</summary>
    public Action<SkUiImageLoadFinishedEventArgs>? LoadingFinished { get; set; }

    /// <summary>The current load, including the publication of its result (completed when idle).</summary>
    public Task LoadingTask { get; private set; } = Task.CompletedTask;

    /// <summary>Whether a load is in progress.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>Why the last load failed, or <c>null</c>.</summary>
    public Exception? LoadError { get; private set; }

    /// <summary>The loaded image's intrinsic size in DIPs (zero without one, also while a placeholder shows).</summary>
    public Size Size => _image?.Size ?? Size.Zero;

    /// <summary>The size of what is drawn: the image, else the placeholder showing (measure uses it).</summary>
    public Size DisplayedSize => _image?.Size ?? _placeholder?.Size ?? Size.Zero;

    /// <summary>Whether a loading or error placeholder is drawn instead of the image.</summary>
    public bool IsShowingPlaceholder => _image is null && _placeholder?.Image is not null;

    /// <summary>The frame to draw, or <c>null</c>.</summary>
    public SKImage? Image => _image?.Frames[_frame];

    /// <summary>The shown entry (tests: shared between views of one source).</summary>
    internal SkUiCachedImage? Entry => _image;

    /// <summary>The shown placeholder's entry (tests).</summary>
    internal SkUiCachedImage? PlaceholderEntry => _image is null ? _placeholder?.Entry : null;

    /// <summary>Whether animated images play (MAUI's <c>IsAnimationPlaying</c>); paused, the current frame stays.</summary>
    public bool IsAnimationPlaying
    {
        get => _playing;
        set
        {
            if (_playing == value)
                return;
            _playing = value;
            if (!value)
                StopAnimation();
            _host.InvalidateTransition(); // painting starts the animation on the node's clock
        }
    }

    /// <summary>
    /// Sets the placeholders: <paramref name="loading"/> while a load is in progress, <paramref name="error"/> after one
    /// failed; with <paramref name="transform"/>, through the same transformations and decode bounds as the image.
    /// </summary>
    public void SetPlaceholders(SkUiImageSource? loading, SkUiImageSource? error, bool transform)
    {
        var transformChanged = _transformPlaceholders != transform;
        _loadingPlaceholder = loading;
        _errorPlaceholder = error;
        _transformPlaceholders = transform;
        if (transformChanged)
            _shownPlaceholder = null; // reload it with the other options
        if (_image is null)
            ShowPlaceholder(IsLoading ? loading : LoadError is not null ? error : null);
    }

    /// <summary>Loads <paramref name="source"/> (<c>null</c> clears). Errors land in <see cref="LoadError"/>; a replaced load is dropped.</summary>
    public Task Load(SkUiImageSource? source, in SkUiImageOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var placeholderOptionsChanged = _options != options;
        _options = options;
        if (placeholderOptionsChanged)
            _shownPlaceholder = null;
        var version = CancelLoad();
        if (source is null)
        {
            Replace(null);
            ShowPlaceholder(null);
            Publish(error: null);
            return LoadingTask = Task.CompletedTask;
        }
        // Captured synchronously so completion marshals back also for hosted nodes, which have no handler.
        var dispatcher = Microsoft.Maui.Dispatching.Dispatcher.GetForCurrentThread();
        var cancellation = _loading = new CancellationTokenSource();
        _pending = source;
        _pendingStarted = Stopwatch.GetTimestamp();
        Task<SkUiImageLoad> load;
        try
        {
            load = SkUiImageLoader.LoadAsync(source, options, cancellation.Token);
        }
        catch (Exception error)
        {
            load = Task.FromException<SkUiImageLoad>(error);
        }
        if (load.IsCompleted)
        {
            LoadingStarted?.Invoke(new SkUiImageLoadStartedEventArgs(source));
            Complete(load, version);
            return LoadingTask = Task.CompletedTask;
        }
        Replace(null);
        LoadError = null;
        IsLoading = true;
        ShowPlaceholder(_loadingPlaceholder);
        _changed();
        LoadingStarted?.Invoke(new SkUiImageLoadStartedEventArgs(source));
        return LoadingTask = CompleteAsync(load, version, dispatcher);
    }

    /// <summary>Shows <paramref name="image"/> directly (Core's <c>SetImage</c>); disposed with the slot when <paramref name="ownsImage"/>.</summary>
    public void SetImage(SKImage? image, bool ownsImage)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CancelLoad();
        if (image is null)
        {
            Replace(null);
        }
        else
        {
            // The same image again (to change ownership): the old entry must not dispose it.
            if (_image is { Key: null } current && current.Frames.Length == 1 && ReferenceEquals(current.Frames[0], image))
                current.Disown();
            Replace(new SkUiCachedImage(new SkUiDecodedImage([image], [], new Size(image.Width, image.Height)), key: null, ownsImage));
        }
        ShowPlaceholder(null);
        Publish(error: null);
    }

    /// <summary>Draws the current frame (or the placeholder) into <paramref name="area"/> with <paramref name="aspect"/>; starts playback when due.</summary>
    public void Paint(SKCanvas canvas, SKRect area, Aspect aspect)
    {
        if (_image is not { } image)
        {
            _placeholder?.Paint(canvas, area, aspect);
            return;
        }
        if (_playing && image.IsAnimated && _animation is null)
            StartAnimation(image);
        SkUiImageDrawing.Draw(canvas, image.Frames[_frame], image.Size, area, aspect);
    }

    /// <summary>Cancels loading and releases the image; the slot cannot be used again.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        CancelLoad();
        Replace(null);
        _placeholder?.Dispose();
        _placeholder = null;
        IsLoading = false;
        LoadError = null;
    }

    /// <summary>Cancels the load in progress (raising its cancelled <see cref="LoadingFinished"/>); returns the new generation.</summary>
    private int CancelLoad()
    {
        _loading?.Cancel();
        _loading?.Dispose();
        _loading = null;
        var generation = ++_generation;
        if (_pending is { } pending)
            Finish(pending, SkUiImageLoadStatus.Cancelled, origin: null, error: null);
        return generation;
    }

    private async Task CompleteAsync(Task<SkUiImageLoad> load, int version, IDispatcher? dispatcher)
    {
        try
        {
            await load.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Read from the task in Complete.
        }
        if (dispatcher is null || !dispatcher.IsDispatchRequired)
            Complete(load, version);
        else
            await dispatcher.DispatchAsync(() => Complete(load, version)).ConfigureAwait(false);
    }

    private void Complete(Task<SkUiImageLoad> load, int version)
    {
        if (version != _generation || _disposed)
        {
            if (load.IsCompletedSuccessfully)
                load.Result.Image.Release(); // replaced meanwhile (its cancelled event was raised then)
            return;
        }
        _loading?.Dispose();
        _loading = null;
        var source = _pending!;
        if (load.IsCompletedSuccessfully)
        {
            Replace(load.Result.Image);
            ShowPlaceholder(null);
            Publish(error: null);
            Finish(source, SkUiImageLoadStatus.Succeeded, load.Result.Origin, error: null);
            return;
        }
        // Not cancelled by this slot (that changes the generation): a timeout or the source's own cancellation is an error.
        var error = load.Exception?.InnerException ?? new OperationCanceledException("The image load was cancelled.");
        Replace(null);
        LoadError = error;
        ShowPlaceholder(_errorPlaceholder);
        Publish(error);
        Finish(source, SkUiImageLoadStatus.Failed, origin: null, error);
    }

    private void Finish(SkUiImageSource source, SkUiImageLoadStatus status, SkUiImageOrigin? origin, Exception? error)
    {
        _pending = null;
        LoadingFinished?.Invoke(new SkUiImageLoadFinishedEventArgs(source, status, origin, error,
            status == SkUiImageLoadStatus.Succeeded ? Size : Size.Zero, Stopwatch.GetElapsedTime(_pendingStarted)));
    }

    private void Publish(Exception? error)
    {
        LoadError = error;
        IsLoading = false;
        _changed();
    }

    /// <summary>Shows <paramref name="source"/> as the placeholder (<c>null</c>: none), unless it already shows.</summary>
    private void ShowPlaceholder(SkUiImageSource? source)
    {
        if (source is null)
        {
            if (_shownPlaceholder is not null || _placeholder?.Entry is not null || _placeholder?.IsLoading == true)
                _placeholder!.Load(null, default);
            _shownPlaceholder = null;
            return;
        }
        if (Equals(_shownPlaceholder, source))
            return;
        // Animated placeholders are loading indicators: they always play.
        _placeholder ??= new SkUiImageSlot(_host, _changed) { _playing = true };
        _shownPlaceholder = source;
        _placeholder.Load(source, _transformPlaceholders ? _options : _options with { Transformations = null });
    }

    private void Replace(SkUiCachedImage? image)
    {
        var old = _image;
        if (ReferenceEquals(old, image))
        {
            image?.Release(); // a second lease on the entry already shown
            return;
        }
        StopAnimation();
        _image = image;
        _frame = 0;
        _frameOffset = 0;
        old?.Release();
    }

    private void StartAnimation(SkUiCachedImage image)
    {
        if (_host.TransitionClock is not { } clock)
            return; // not on a surface yet: painting there starts it
        var offset = _frameOffset;
        IDisposable? handle = null;
        handle = clock.Start(progress =>
        {
            if (!ReferenceEquals(_animation, handle) && handle is not null)
                return;
            if (!ReferenceEquals(_host.TransitionClock, clock))
            {
                // Detached or moved to another surface: stop here; the next paint there resumes from this frame.
                StopAnimation();
                return;
            }
            var time = offset + (long)(progress * image.TotalDuration);
            var frame = image.FrameAt(time);
            if (frame == _frame)
                return;
            _frame = frame;
            _frameOffset = time;
            _host.InvalidateTransition();
        }, TimeSpan.FromMilliseconds(image.TotalDuration), Easing.Linear, repeat: true, stopped: () =>
        {
            if (ReferenceEquals(_animation, handle))
                _animation = null;
        });
        _animation = handle;
    }

    private void StopAnimation()
    {
        var animation = _animation;
        _animation = null;
        animation?.Dispose();
    }
}
