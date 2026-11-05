using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;
using MauiSkiaUi.Core;
using SkiaSharp;

namespace MauiSkiaUi.Benchmarks;

/// <summary>How an icon reaches the view in the icon scenarios.</summary>
public enum IconKind
{
    /// <summary>An <c>.svg</c> through the reader, recolored by its source.</summary>
    Svg,

    /// <summary>The same icon as a PNG of its pixels on this screen, as a <c>MauiImage</c> is, recolored by a tint.</summary>
    Png,

    /// <summary>A font glyph, recolored by its source.</summary>
    Font
}

/// <summary>The icons shared by the icon scenarios; vector members by name, so a baseline without them still builds.</summary>
internal static class BenchIcons
{
    public const int Files = 8;
    public const double Size = 24;

    private static readonly string[] Glyphs = ["★", "☆", "♥", "♦", "♣", "♠", "●", "■"];
    public static readonly Color[] Colors = [Microsoft.Maui.Graphics.Colors.Black, Microsoft.Maui.Graphics.Colors.White, Microsoft.Maui.Graphics.Colors.Teal, Microsoft.Maui.Graphics.Colors.OrangeRed];

    private static readonly Lazy<(string[] Svg, string[] Png)> Written = new(Write);

    /// <summary>Writes the files, so no scenario times it.</summary>
    public static void Prepare() => _ = Written.Value;

    public static string Svg(int index) => Written.Value.Svg[index % Files];

    public static string Png(int index) => Written.Value.Png[index % Files];

    public static string Glyph(int index) => Glyphs[index % Files];

    public static Type? VectorType => typeof(SkUiImage).Assembly.GetType("MauiSkiaUi.SkUiVectorImageSource");

    /// <summary>A gear of 46 path verbs and more, as icons have; each file has its own number of teeth.</summary>
    private static string GearPath(int teeth)
    {
        var path = new StringBuilder();
        const double center = 12, outer = 11, inner = 8.5, hole = 3.5;
        for (var tooth = 0; tooth < teeth; tooth++)
        {
            var step = 2 * Math.PI / teeth;
            var start = tooth * step;
            foreach (var (fraction, radius) in new[] { (0d, inner), (0.15, outer), (0.45, outer), (0.6, inner) })
            {
                var angle = start + fraction * step;
                path.Append(tooth == 0 && fraction == 0 ? 'M' : 'L')
                    .Append((center + radius * Math.Cos(angle)).ToString("0.###", CultureInfo.InvariantCulture)).Append(' ')
                    .Append((center + radius * Math.Sin(angle)).ToString("0.###", CultureInfo.InvariantCulture)).Append(' ');
            }
        }
        path.Append("Z M").Append((center + hole).ToString(CultureInfo.InvariantCulture)).Append(" 12 ")
            .Append("A3.5 3.5 0 1 0 ").Append((center - hole).ToString(CultureInfo.InvariantCulture)).Append(" 12 ")
            .Append("A3.5 3.5 0 1 0 ").Append((center + hole).ToString(CultureInfo.InvariantCulture)).Append(" 12 Z");
        return path.ToString();
    }

    // The display's pixels for the icon's size; headless, one per DIP.
    private static int PngPixels()
    {
        try
        {
            return (int)Math.Round(Size * Math.Max(1, DeviceDisplay.Current.MainDisplayInfo.Density));
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return (int)Size;
        }
    }

    private static (string[] Svg, string[] Png) Write()
    {
        var pngSize = PngPixels();
        var folder = Path.Combine(Path.GetTempPath(), "skiaui-bench-icons");
        Directory.CreateDirectory(folder);
        var svg = new string[Files];
        var png = new string[Files];
        for (var index = 0; index < Files; index++)
        {
            var data = GearPath(10 + index);
            svg[index] = Path.Combine(folder, $"gear{index}.svg");
            File.WriteAllText(svg[index], $"""<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24"><path fill="currentColor" fill-rule="evenodd" d="{data}"/></svg>""");
            png[index] = Path.Combine(folder, $"gear{index}.png");
            using var bitmap = new SKBitmap(pngSize, pngSize);
            using (var canvas = new SKCanvas(bitmap))
            using (var paint = new SKPaint { IsAntialias = true, Color = SKColors.Black })
            using (var path = SKPath.ParseSvgPathData(data))
            {
                path.FillType = SKPathFillType.EvenOdd;
                canvas.Clear(SKColors.Transparent);
                canvas.Scale(pngSize / (float)Size);
                canvas.DrawPath(path, paint);
            }
            using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(png[index], encoded.ToArray());
        }
        return (svg, png);
    }
}

/// <summary>
/// 1,000 icons of eight files in a list of about 50 per screen, as SVG, PNG or font glyph, on either layer. The first iteration
/// loads them into the memory cache and later ones are cache hits, so this is what they cost once loaded: building, drawing,
/// a theme change's work on the UI thread (update: every icon's color; the eight loads a color needs the first time run off it
/// and are <see cref="IconLoad"/>) and scrolling (motion).
/// </summary>
public sealed class IconGrid(bool core, IconKind kind) : BenchScenario
{
    private const int Count = 1000;
    private const int Columns = 5;
    private const double RowHeight = 72;
    // Looked up once: what a baseline without them cannot name is by reflection, and an app does not pay for reflection per icon.
    private static readonly BindableProperty? VectorColor = VectorProperty("ColorProperty");
    private static readonly BindableProperty? VectorSource = VectorProperty("SourceProperty");
    private static readonly Func<SkUiImageSource, Color?, double, SkUiImageSource>? FromVector =
        typeof(SkUiImageSource).GetMethod("FromVector", [typeof(SkUiImageSource), typeof(Color), typeof(double)])
            ?.CreateDelegate<Func<SkUiImageSource, Color?, double, SkUiImageSource>>();
    private static readonly SkUiTintTransformation[] Tints = [.. BenchIcons.Colors.Select(static color => new SkUiTintTransformation(color))];

    private int _round;

    private static BindableProperty? VectorProperty(string name) =>
        BenchIcons.VectorType?.GetField(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as BindableProperty;

    public override void Prepare() => BenchIcons.Prepare();

    public override string Name => $"{(core ? "core" : "skui")}-icons-{kind.ToString().ToLowerInvariant()}";

    public override string Description =>
        $"1,000 {(core ? "SkUiCoreImage" : "SkUiImage")} of 8 {kind} icons (24 dp, about 50 per screen); update = a new color on all (a theme change's work on the UI thread); motion = animated scroll";

    private Color CurrentColor => BenchIcons.Colors[_round % BenchIcons.Colors.Length];

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields,
        "MauiSkiaUi.SkUiVectorImageSource", "MauiSkiaUi")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(SkUiImageSource))]
    public override View Build()
    {
        if (core)
        {
            var coreRows = new SkUiCoreRowDefinition[Count / Columns];
            for (var row = 0; row < coreRows.Length; row++)
                coreRows[row] = new SkUiCoreRowDefinition(new SkUiCoreGridLength(RowHeight));
            var coreColumns = new SkUiCoreColumnDefinition[Columns];
            for (var column = 0; column < Columns; column++)
                coreColumns[column] = new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star);
            var coreGrid = new SkUiCoreGrid().SetPadding(new Thickness(8)).SetColumnDefinitions(coreColumns).SetRowDefinitions(coreRows);
            coreGrid.StartUpdating();
            for (var index = 0; index < Count; index++)
            {
                var node = new SkUiCoreImage();
                node.SetWidth(BenchIcons.Size).SetHeight(BenchIcons.Size);
                Apply(node, index);
                coreGrid.Add(node, index / Columns, index % Columns);
            }
            coreGrid.EndUpdating();
            return Scenarios.Scroll(Scenarios.Host(coreGrid));
        }
        var grid = new SkUiGrid { Padding = new Thickness(8) };
        var columns = new ColumnDefinitionCollection();
        for (var column = 0; column < Columns; column++)
            columns.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions = columns;
        var rows = new RowDefinitionCollection();
        for (var row = 0; row < Count / Columns; row++)
            rows.Add(new RowDefinition(new GridLength(RowHeight)));
        grid.RowDefinitions = rows;
        grid.StartUpdating();
        for (var index = 0; index < Count; index++)
        {
            var image = new SkUiImage { WidthRequest = BenchIcons.Size, HeightRequest = BenchIcons.Size };
            if (kind == IconKind.Png)
                image.Transformations.Add(Tints[_round % Tints.Length]);
            image.Source = kind switch
            {
                IconKind.Svg => NewVectorSource(index),
                IconKind.Png => ImageSource.FromFile(BenchIcons.Png(index)),
                _ => new FontImageSource { Glyph = BenchIcons.Glyph(index), Size = BenchIcons.Size, Color = CurrentColor }
            };
            Grid.SetRow(image, index / Columns);
            Grid.SetColumn(image, index % Columns);
            grid.Children.Add(image);
        }
        grid.EndUpdating();
        return Scenarios.Scroll(grid);
    }

    private ImageSource NewVectorSource(int index)
    {
        if (BenchIcons.VectorType is not { } type)
            return ImageSource.FromFile(BenchIcons.Svg(index));
        var source = (ImageSource)Activator.CreateInstance(type)!;
        source.SetValue(VectorSource!, BenchIcons.Svg(index));
        source.SetValue(VectorColor!, CurrentColor);
        return source;
    }

    private void Apply(SkUiCoreImage node, int index)
    {
        switch (kind)
        {
            case IconKind.Svg:
                var file = SkUiImageSource.FromFile(BenchIcons.Svg(index));
                node.SetSource(FromVector is null ? file : FromVector(file, CurrentColor, 0));
                break;
            case IconKind.Png:
                node.SetTransformations(Tints[_round % Tints.Length]);
                node.SetSourceFile(BenchIcons.Png(index));
                break;
            default:
                node.SetSourceFont(BenchIcons.Glyph(index), null, BenchIcons.Size, CurrentColor);
                break;
        }
    }

    public override Action<View>? Update => root =>
    {
        _round++;
        var index = 0;
        if (core)
        {
            foreach (var child in ((SkUiCoreGrid)((SkUiCoreHost)((SkUiScrollView)root).Content!).Content!).Children)
                Apply((SkUiCoreImage)child, index++);
            return;
        }
        var grid = (SkUiGrid)((SkUiScrollView)root).Content!;
        grid.StartUpdating();
        foreach (var child in grid.Children)
        {
            var image = (SkUiImage)child;
            switch (kind)
            {
                case IconKind.Svg when VectorColor is not null:
                    image.Source!.SetValue(VectorColor, CurrentColor);
                    break;
                case IconKind.Png:
                    image.Transformations[0] = Tints[_round % Tints.Length];
                    break;
                case IconKind.Font:
                    ((FontImageSource)image.Source!).Color = CurrentColor;
                    break;
            }
        }
        grid.EndUpdating();
    };

    public override Func<View, IDisposable?>? Motion => root =>
    {
        var scroll = (SkUiScrollView)root;
        var duration = MotionDuration + TimeSpan.FromSeconds(0.5);
        var animate = typeof(SkUiScrollView).GetMethod("AnimateScrollTo", [typeof(double), typeof(double), typeof(TimeSpan)]);
        if (animate is not null)
            return animate.Invoke(scroll, [0d, scroll.ContentSize.Height, duration]) as IDisposable;
        _ = scroll.ScrollToAsync(0, scroll.ContentSize.Height, true);
        return null;
    };
}

/// <summary>
/// What loading an icon costs, off the UI thread and not cached: <c>generate</c> is <paramref name="count"/> loads from the file
/// (a font: from the glyph). With a count of 1, one iteration and no warm-up in a new process, it is the first icon of the process.
/// </summary>
public sealed class IconLoad(IconKind kind, int count, bool first) : BenchScenario
{
    public override string Name => $"icon-{(first ? "first" : "load")}-{kind.ToString().ToLowerInvariant()}";

    public override string Description => first
        ? $"The first {kind} icon of the process (run with -w 0 -n 1, alone); generate = its load"
        : $"{count} {kind} icons loaded uncached off the UI thread; generate = all of them";

    public override void Prepare() => BenchIcons.Prepare();

    public override View Build()
    {
        Task.Run(LoadAll).GetAwaiter().GetResult();
        return Scenarios.Scroll(new SkUiLabel { Text = Name });
    }

    private async Task LoadAll()
    {
        for (var index = 0; index < count; index++)
        {
            var image = new SkUiCoreImage().SetCacheType(SkUiImageCacheType.None);
            switch (kind)
            {
                case IconKind.Svg:
                    image.SetSourceFile(BenchIcons.Svg(index));
                    break;
                case IconKind.Png:
                    image.SetSourceFile(BenchIcons.Png(index));
                    break;
                default:
                    image.SetSourceFont(BenchIcons.Glyph(index), null, BenchIcons.Size, Colors.Black);
                    break;
            }
            await image.LoadingTask.ConfigureAwait(false);
            if (image.LoadError is { } error && BenchIcons.VectorType is not null)
                throw new InvalidOperationException($"{Name}: {error.Message}", error);
            image.Clear();
        }
    }
}
