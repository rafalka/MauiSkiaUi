using System.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A Skia-decoded image supporting file, packaged raw asset, stream, and HTTPS image sources.</summary>
public class SkUiImage : SkUiView, IDisposable
{
    private static readonly HttpClient Http = new();
    private ImageSource? source;
    private Aspect aspect = Aspect.AspectFit;
    private CancellationTokenSource? loading;
    private SKImage? image;
    private int generation;
    private bool disposed;

    /// <summary>Bindable MAUI image source. Relative files refer to Resources/Raw, not generated MauiImage assets.</summary>
    public static readonly BindableProperty SourceProperty = BindableProperty.Create(nameof(Source), typeof(ImageSource), typeof(SkUiImage), null,
        propertyChanged: (view, _, value) => ((SkUiImage)view).SetSource((ImageSource?)value));
    /// <summary>Bindable aspect mode.</summary>
    public static readonly BindableProperty AspectProperty = BindableProperty.Create(nameof(Aspect), typeof(Aspect), typeof(SkUiImage), Aspect.AspectFit,
        propertyChanged: (view, _, value) => ((SkUiImage)view).SetAspect((Aspect)value));
    /// <summary>Image source; asynchronous loading starts when it changes.</summary>
    public ImageSource? Source { get => source; set => SetValue(SourceProperty, value); }
    /// <summary>Fit, fill, or stretch within the arranged bounds.</summary>
    public Aspect Aspect { get => aspect; set => SetValue(AspectProperty, value); }
    /// <summary>Current asynchronous load, including error-state publication.</summary>
    public Task LoadingTask { get; private set; } = Task.CompletedTask;
    /// <summary>Whether the current source is loading.</summary>
    public bool IsLoading { get; private set; }
    /// <summary>Last current-source error, or null on success.</summary>
    public Exception? LoadError { get; private set; }
    /// <summary>Decoded source dimensions; one source pixel maps to one intrinsic DIP.</summary>
    public Size ImageSize => image is null ? Size.Zero : new Size(image.Width, image.Height);

    /// <summary>Sets source without bindable write-back. Call on the UI thread; streams are owned and disposed by this control.</summary>
    public SkUiImage SetSource(ImageSource? value)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (ReferenceEquals(source, value)) return this;
        if (source is not null) source.PropertyChanged -= OnSourceChanged;
        source = value;
        if (source is not null) source.PropertyChanged += OnSourceChanged;
        LoadingTask = ReloadAsync();
        return this;
    }

    /// <summary>Sets aspect without bindable write-back.</summary>
    public SkUiImage SetAspect(Aspect value) { aspect = value; InvalidatePaint(); return this; }
    private void OnSourceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(FileImageSource.File) or nameof(StreamImageSource.Stream) or nameof(UriImageSource.Uri))
            LoadingTask = ReloadAsync();
    }

    /// <summary>Reloads the current source; errors are exposed through LoadError, cancellation is not an error.</summary>
    public async Task ReloadAsync()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var version = ++generation;
        loading?.Cancel();
        loading?.Dispose();
        loading = new CancellationTokenSource();
        var token = loading.Token;
        var current = source;
        image?.Dispose();
        image = null;
        LoadError = null;
        IsLoading = current is not null;
        PublishState();
        if (current is null) return;
        SKImage? decoded = null;
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
            if (version != generation || disposed) return;
            image = decoded;
            decoded = null;
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (version == generation && !disposed) LoadError = error;
        }
        finally
        {
            decoded?.Dispose();
            if (version == generation && !disposed)
            {
                IsLoading = false;
                PublishState();
            }
        }
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
        if (image is null) return;
        var scale = aspect == Aspect.AspectFill ? Math.Max(Width / image.Width, Height / image.Height) : Math.Min(Width / image.Width, Height / image.Height);
        var width = aspect == Aspect.Fill ? Width : image.Width * scale;
        var height = aspect == Aspect.Fill ? Height : image.Height * scale;
        var destination = SKRect.Create((float)((Width - width) / 2), (float)((Height - height) / 2), (float)width, (float)height);
        canvas.DrawImage(image, destination, new SKSamplingOptions(SKFilterMode.Linear));
    }

    /// <summary>Cancels loading and releases decoded image resources; a disposed control cannot be reused.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        generation++;
        loading?.Cancel();
        loading?.Dispose();
        loading = null;
        if (source is not null) source.PropertyChanged -= OnSourceChanged;
        image?.Dispose();
        image = null;
        IsLoading = false;
        PublishState();
        GC.SuppressFinalize(this);
    }
}