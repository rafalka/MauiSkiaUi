using System.ComponentModel;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A Skia-decoded image supporting file, packaged raw asset, stream, and HTTPS image sources.</summary>
public class SkUiImage : SkUiView, IDisposable
{
    private static readonly HttpClient Http = new();
    private ImageSource? _source;
    private Aspect _aspect = Aspect.AspectFit;
    private CancellationTokenSource? _loading;
    private SKImage? _image;
    private int _generation;
    private bool _disposed;

    /// <summary>Bindable MAUI image source. Relative files refer to Resources/Raw, not generated MauiImage assets.</summary>
    public static readonly BindableProperty SourceProperty = BindableProperty.Create(nameof(Source), typeof(ImageSource), typeof(SkUiImage), null,
        propertyChanged: (view, _, value) => ((SkUiImage)view).SetSource((ImageSource?)value));
    /// <summary>Bindable aspect mode.</summary>
    public static readonly BindableProperty AspectProperty = BindableProperty.Create(nameof(Aspect), typeof(Aspect), typeof(SkUiImage), Aspect.AspectFit,
        propertyChanged: (view, _, value) => ((SkUiImage)view).SetAspect((Aspect)value));
    /// <summary>Image source; asynchronous loading starts when it changes.</summary>
    public ImageSource? Source { get => _source; set => SetValue(SourceProperty, value); }
    /// <summary>Fit, fill, or stretch within the arranged bounds.</summary>
    public Aspect Aspect { get => _aspect; set => SetValue(AspectProperty, value); }
    /// <summary>Current asynchronous load, including error-state publication.</summary>
    public Task LoadingTask { get; private set; } = Task.CompletedTask;
    /// <summary>Whether the current source is loading.</summary>
    public bool IsLoading { get; private set; }
    /// <summary>Last current-source error, or null on success.</summary>
    public Exception? LoadError { get; private set; }
    /// <summary>Decoded source dimensions; one source pixel maps to one intrinsic DIP.</summary>
    public Size ImageSize => _image is null ? Size.Zero : new Size(_image.Width, _image.Height);

    /// <summary>Sets source without bindable write-back. Call on the UI thread; streams are owned and disposed by this control.</summary>
    public SkUiImage SetSource(ImageSource? value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ReferenceEquals(_source, value)) return this;
        if (_source is not null) _source.PropertyChanged -= OnSourceChanged;
        _source = value;
        if (_source is not null) _source.PropertyChanged += OnSourceChanged;
        LoadingTask = ReloadAsync();
        return this;
    }

    /// <summary>Sets aspect without bindable write-back.</summary>
    public SkUiImage SetAspect(Aspect value) { _aspect = value; InvalidatePaint(); return this; }
    private void OnSourceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(FileImageSource.File) or nameof(StreamImageSource.Stream) or nameof(UriImageSource.Uri))
            LoadingTask = ReloadAsync();
    }

    /// <summary>Reloads the current source; errors are exposed through LoadError, cancellation is not an error.</summary>
    public async Task ReloadAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Captured up front (synchronously, on the caller's thread) so the completion below can marshal back
        // even for hosted images, which never get a handler to resolve a dispatcher from.
        var dispatcher = Microsoft.Maui.Dispatching.Dispatcher.GetForCurrentThread();
        var version = ++_generation;
        _loading?.Cancel();
        _loading?.Dispose();
        _loading = new CancellationTokenSource();
        var token = _loading.Token;
        var current = _source;
        _image?.Dispose();
        _image = null;
        LoadError = null;
        IsLoading = current is not null;
        PublishState();
        if (current is null) return;
        SKImage? decoded = null;
        Exception? failure = null;
        var cancelled = false;
        try
        {
            using var stream = await OpenSourceAsync(current, token);
            using var bytes = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer, token)) != 0)
            {
                if (bytes.Length + read > 32 * 1024 * 1024) throw new InvalidDataException("Image exceeds the 32 MiB encoded limit.");
                bytes.Write(buffer, 0, read);
            }
            var data = bytes.ToArray();
            decoded = await Task.Run(() => Decode(data), token);
            token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) { cancelled = true; }
        catch (Exception error) { failure = error; }

        // Task.Run's continuation may resume on a thread-pool thread; apply the resulting state where
        // MAUI expects layout invalidation to happen.
        await RunOnDispatcherAsync(dispatcher, () =>
        {
            if (cancelled || version != _generation || _disposed)
            {
                decoded?.Dispose();
                return;
            }
            if (failure is not null) LoadError = failure;
            else _image = decoded;
            IsLoading = false;
            PublishState();
        });
    }

    private static Task RunOnDispatcherAsync(IDispatcher? dispatcher, Action action)
    {
        if (dispatcher is null || !dispatcher.IsDispatchRequired)
        {
            action();
            return Task.CompletedTask;
        }
        return dispatcher.DispatchAsync(action);
    }

    private static async Task<Stream> OpenSourceAsync(ImageSource value, CancellationToken token)
    {
        return value switch
        {
            StreamImageSource stream => await stream.Stream(token),
            FileImageSource file when Path.IsPathRooted(file.File) => File.OpenRead(file.File),
            FileImageSource file => await Microsoft.Maui.Storage.FileSystem.Current.OpenAppPackageFileAsync(file.File),
            UriImageSource uri when uri.Uri?.Scheme == Uri.UriSchemeHttps => await Http.GetStreamAsync(uri.Uri, token),
            _ => throw new NotSupportedException("Use a file, raw package asset, stream, or HTTPS source.")
        };
    }

    private static SKImage Decode(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("Unsupported or corrupt image.");
        if ((long)codec.Info.Width * codec.Info.Height > 16 * 1024 * 1024) throw new InvalidDataException("Image exceeds the 16 megapixel decoded limit.");
        using var bitmap = SKBitmap.Decode(codec) ?? throw new InvalidDataException("Image decoding failed.");
        return SKImage.FromBitmap(bitmap);
    }

    private void PublishState()
    {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(LoadError));
        OnPropertyChanged(nameof(ImageSize));
        InvalidateMeasureOverride();
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => ImageSize;

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        if (_image is null) return;
        SkUiLook.Current.DrawImage(canvas, _image, (float)Width, (float)Height, _aspect);
    }

    /// <summary>Cancels loading and releases decoded image resources; a disposed control cannot be reused.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _generation++;
        _loading?.Cancel();
        _loading?.Dispose();
        _loading = null;
        if (_source is not null) _source.PropertyChanged -= OnSourceChanged;
        _image?.Dispose();
        _image = null;
        IsLoading = false;
        PublishState();
        GC.SuppressFinalize(this);
    }
}