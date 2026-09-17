using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core image node loaded from <see cref="SKImage"/>, file path, or stream.
/// Does not use MAUI <c>ImageSource</c> — keep decoding outside Controls/XAML.
/// </summary>
public class SkUiCoreImage : SkUiCoreNode, IDisposable
{
    private static readonly HttpClient Http = new();
    private SKImage? _image;
    private Aspect _aspect = Aspect.AspectFit;
    private CancellationTokenSource? _loading;
    private int _generation;
    private bool _disposed;
    private bool _ownsImage = true;

    /// <summary>Fit, fill, or stretch within the arranged bounds.</summary>
    public Aspect Aspect
    {
        get => _aspect;
        set => SetAspect(value);
    }

    /// <summary>Current asynchronous load task (completed when idle).</summary>
    public Task LoadingTask { get; private set; } = Task.CompletedTask;

    /// <summary>Whether the current source is loading.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>Last load error for the current source, or <c>null</c> on success.</summary>
    public Exception? LoadError { get; private set; }

    /// <summary>Decoded source dimensions; one source pixel maps to one intrinsic DIP.</summary>
    public Size ImageSize => _image is null ? Size.Zero : new Size(_image.Width, _image.Height);

    /// <summary>Sets aspect mode.</summary>
    public SkUiCoreImage SetAspect(Aspect value)
    {
        if (!SetProperty(ref _aspect, value, nameof(Aspect))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>
    /// Assigns a decoded image. When <paramref name="ownsImage"/> is <c>true</c>, this node disposes it on replace/dispose.
    /// </summary>
    public SkUiCoreImage SetImage(SKImage? image, bool ownsImage = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CancelLoad();
        ReplaceImage(image, ownsImage);
        LoadError = null;
        IsLoading = false;
        PublishState();
        return this;
    }

    /// <summary>Loads an image from an absolute or app-package-relative file path.</summary>
    public SkUiCoreImage SetSourceFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ObjectDisposedException.ThrowIf(_disposed, this);
        LoadingTask = LoadAsync(async token =>
        {
            if (Path.IsPathRooted(path))
                return File.OpenRead(path);
            return await FileSystem.Current.OpenAppPackageFileAsync(path);
        });
        return this;
    }

    /// <summary>Loads an image from a stream factory. The returned stream is disposed after decode.</summary>
    public SkUiCoreImage SetSourceStream(Func<CancellationToken, Task<Stream>> open)
    {
        ArgumentNullException.ThrowIfNull(open);
        ObjectDisposedException.ThrowIf(_disposed, this);
        LoadingTask = LoadAsync(open);
        return this;
    }

    /// <summary>Loads an image from an HTTPS URI.</summary>
    public SkUiCoreImage SetSourceUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (uri.Scheme != Uri.UriSchemeHttps)
            throw new NotSupportedException("Only HTTPS image URIs are supported.");
        ObjectDisposedException.ThrowIf(_disposed, this);
        LoadingTask = LoadAsync(token => Http.GetStreamAsync(uri, token));
        return this;
    }

    /// <summary>Reloads are driven by the SetSource* helpers; this clears the current image.</summary>
    public SkUiCoreImage Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CancelLoad();
        ReplaceImage(null, ownsImage: true);
        LoadError = null;
        IsLoading = false;
        PublishState();
        return this;
    }

    private async Task LoadAsync(Func<CancellationToken, Task<Stream>> open)
    {
        var version = ++_generation;
        _loading?.Cancel();
        _loading?.Dispose();
        _loading = new CancellationTokenSource();
        var token = _loading.Token;
        ReplaceImage(null, ownsImage: true);
        LoadError = null;
        IsLoading = true;
        PublishState();

        SKImage? decoded = null;
        Exception? failure = null;
        var cancelled = false;
        try
        {
            await using var stream = await open(token);
            using var bytes = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer, token)) != 0)
            {
                if (bytes.Length + read > 32 * 1024 * 1024)
                    throw new InvalidDataException("Image exceeds the 32 MiB encoded limit.");
                bytes.Write(buffer, 0, read);
            }
            var data = bytes.ToArray();
            decoded = await Task.Run(() => Decode(data), token);
            token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) { cancelled = true; }
        catch (Exception error) { failure = error; }

        if (cancelled || version != _generation || _disposed)
        {
            decoded?.Dispose();
            return;
        }

        if (failure is not null) LoadError = failure;
        else ReplaceImage(decoded, ownsImage: true);
        IsLoading = false;
        PublishState();
    }

    private static SKImage Decode(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("Unsupported or corrupt image.");
        if ((long)codec.Info.Width * codec.Info.Height > 16 * 1024 * 1024)
            throw new InvalidDataException("Image exceeds the 16 megapixel decoded limit.");
        using var bitmap = SKBitmap.Decode(codec) ?? throw new InvalidDataException("Image decoding failed.");
        return SKImage.FromBitmap(bitmap);
    }

    private void CancelLoad()
    {
        _generation++;
        _loading?.Cancel();
        _loading?.Dispose();
        _loading = null;
    }

    private void ReplaceImage(SKImage? image, bool ownsImage)
    {
        if (_ownsImage)
            _image?.Dispose();
        _image = image;
        _ownsImage = ownsImage;
    }

    private void PublishState()
    {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(LoadError));
        OnPropertyChanged(nameof(ImageSize));
        InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => ImageSize;

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        if (_image is null) return;
        SkUiLook.Current.DrawImage(canvas, _image, (float)Frame.Width, (float)Frame.Height, _aspect);
    }

    /// <summary>Cancels loading and releases owned image resources; a disposed node cannot be reused.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelLoad();
        ReplaceImage(null, ownsImage: true);
        IsLoading = false;
        PublishState();
        GC.SuppressFinalize(this);
    }
}
