using System.Reflection;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Native objects of drawn text are released deterministically, not by the finalizer: fonts and the text paint are shared
/// (<see cref="SkUiTextResources"/>), a layout disposes the text blobs of lines it replaces, and labels dispose theirs when
/// they leave their drawn parent or their surface is torn down.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class TextNativeResourcesTests
{
    // Shaped text (not the simple fast path): its lines are drawn from text blobs.
    private const string Hebrew = "שלום עולם, האוקיינוסים מכסים את רוב כדור הארץ";

    private static readonly SKTypeface Mono = SKTypeface.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "RobotoMono-Regular.ttf"));

    private static T Field<T>(object owner, string name) =>
        (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner)!;

    private static List<SkUiShaping.Line> Lines(SkUiTextLayout layout) => Field<List<SkUiShaping.Line>>(layout, "_lines");

    private static SKTextBlob[] Blobs(SkUiTextLayout layout) => Lines(layout).Select(line => line.Blob).OfType<SKTextBlob>().ToArray();

    /// <summary>A surface drawing <paramref name="root"/> (disposing it tears the surface down).</summary>
    private static SkUiFrameRenderer Surface(SkUiView root)
    {
        SkUiTestHelpers.Arrange(root, 300, 200);
        var renderer = new SkUiFrameRenderer(root, _ => { }, () => { }, () => { });
        Draw(renderer, root);
        return renderer;
    }

    /// <summary>Records what changed and composites it.</summary>
    private static void Draw(SkUiFrameRenderer renderer, SkUiView root)
    {
        SkUiTestHelpers.Arrange(root, 300, 200);
        renderer.PresentFrame();
        using var bitmap = new SKBitmap(300, 200);
        using var canvas = new SKCanvas(bitmap);
        renderer.Render(canvas, bitmap.Info, TimeSpan.Zero);
    }

    [Fact]
    public void LayoutsShareFontsAndThePaint()
    {
        var font = SkUiTextResources.Font(Mono, 16, FontAttributes.None);
        Assert.Same(font, SkUiTextResources.Font(Mono, 16, FontAttributes.None));
        Assert.NotSame(font, SkUiTextResources.Font(Mono, 17, FontAttributes.None));
        Assert.NotSame(font, SkUiTextResources.Font(Mono, 16, FontAttributes.Bold));
        Assert.Same(SkUiTextResources.TextPaint, SkUiTextResources.TextPaint);

        // Labels keep no font or paint of their own: two layouts measure with the one shared font.
        var style = new SkUiTextStyle(Mono, 16, Rendering: SkUiTextRendering.Shaped);
        var (first, second) = (new SkUiTextLayout(), new SkUiTextLayout());
        first.Measure("abc", style, default, 500);
        second.Measure("abc", style, default, 500);
        Assert.Same(Field<Func<SKTypeface, SKFont>>(first, "_fontFor")(Mono), Field<Func<SKTypeface, SKFont>>(second, "_fontFor")(Mono));
    }

    [Fact]
    public void ReplacedLinesDisposeTheirBlobs()
    {
        var layout = new SkUiTextLayout();
        var style = new SkUiTextStyle(Mono, 16, Rendering: SkUiTextRendering.Shaped);
        layout.Measure(Hebrew, style, default, 1000);
        var before = Blobs(layout);
        Assert.NotEmpty(before);
        layout.Measure(Hebrew, style, default, 120); // wraps: new lines
        Assert.All(before, blob => Assert.Equal(IntPtr.Zero, blob.Handle));
        Assert.All(Blobs(layout), blob => Assert.NotEqual(IntPtr.Zero, blob.Handle));

        layout.Release();
        Assert.Empty(Lines(layout));
        Assert.True(layout.Measure(Hebrew, style, default, 120).Height > 0); // laid out again on demand

        // Spans: the pieces' blobs.
        var rich = new SkUiRichTextLayout();
        var text = new SkUiRichText.Builder().Add(Hebrew, new SkUiTextSpanStyle(Mono, 16), new SkUiTextSpanPaint(SKColors.Black)).Build();
        rich.Measure(text, style, default, 1000);
        var lines = Field<List<SkUiShaping.StyledLine>>(rich, "_lines");
        var pieces = lines.SelectMany(line => line.Pieces).Select(piece => piece.Blob).OfType<SKTextBlob>().ToArray();
        Assert.NotEmpty(pieces);
        rich.Measure(text, style, default, 120);
        Assert.All(pieces, blob => Assert.Equal(IntPtr.Zero, blob.Handle));
    }

    [Fact]
    public void ALabelThatLeavesItsDrawnParentReleasesItsBlobsAndDrawsAgainWhenBack()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = new SkUiLabel { Text = Hebrew, FontFamily = SkUiTestHelpers.BundledFontFamily, TextRendering = SkUiTextRendering.Shaped };
        var stack = new SkUiVerticalStackLayout { Children = { label } };
        using var surface = Surface(stack);
        var layout = Field<SkUiTextLayout>(label, "_layout");
        var drawn = Blobs(layout);
        Assert.NotEmpty(drawn);

        stack.Children.Remove(label);
        Assert.All(drawn, blob => Assert.Equal(IntPtr.Zero, blob.Handle));
        Assert.Empty(Lines(layout));

        stack.Children.Add(label);
        Draw(surface, stack);
        Assert.NotEmpty(Blobs(layout));
    }

    [Fact]
    public void TearingTheSurfaceDownReleasesBlobsOnBothLayers()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = new SkUiLabel { Text = Hebrew, FontFamily = SkUiTestHelpers.BundledFontFamily, TextRendering = SkUiTextRendering.Shaped };
        var core = new SkUiCoreLabel().SetText(Hebrew).SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetTextRendering(SkUiTextRendering.Shaped);
        var host = new SkUiCoreHost();
        host.SetContent(core);
        var root = new SkUiVerticalStackLayout { Children = { label, host } };
        var surface = Surface(root);
        Assert.NotEmpty(Blobs(Field<SkUiTextLayout>(label, "_layout")));
        Assert.NotEmpty(Blobs(Field<SkUiTextLayout>(core, "_layout")));
        surface.Dispose();
        Assert.Empty(Lines(Field<SkUiTextLayout>(label, "_layout")));
        Assert.Empty(Lines(Field<SkUiTextLayout>(core, "_layout")));

        // A Core label taken out of its host releases too.
        using var again = Surface(root);
        Assert.NotEmpty(Lines(Field<SkUiTextLayout>(core, "_layout")));
        host.SetContent(null);
        Assert.Empty(Lines(Field<SkUiTextLayout>(core, "_layout")));
    }
}
