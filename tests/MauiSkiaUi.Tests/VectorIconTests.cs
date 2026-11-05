using System.Net;
using System.Net.Sockets;
using System.Text;
using SkiaSharp;
using Svg.Skia;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.7 R1–R3: the vector icon reader. Pixels are compared with Svg.Skia, the renderer MAUI's Resizetizer uses.
/// <c>SKUI_VECTOR_CORPUS=&lt;folder&gt;</c> adds every <c>.svg</c> in a folder, so an app's own icons can be checked.
/// </summary>
public sealed class VectorIconTests
{
    private const string Open = """<svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 48 48">""";
    private const string Close = "</svg>";
    private const int Scale = 3;
    private const int ChannelTolerance = 48;
    private const double MaxDifferingPercent = 2; // a few edge pixels of a small icon
    private const string CorpusVariable = "SKUI_VECTOR_CORPUS";

    public static TheoryData<string> Documents => new()
    {
        // Shapes
        Open + """<path fill="#336699" d="M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5"/>""" + Close,
        Open + """<path fill="#E11D48" d="M4 24 A20 20 0 1 1 44 24 A20 20 0 1 1 4 24 Z"/>""" + Close,
        Open + """<rect x="4" y="6" width="30" height="20" fill="red"/>""" + Close,
        Open + """<rect x="4" y="6" width="30" height="20" rx="6" fill="teal"/>""" + Close,
        Open + """<rect x="4" y="6" width="30" height="20" rx="6" ry="3" fill="teal"/>""" + Close,
        Open + """<rect x="4" y="6" width="30" height="20" ry="40" fill="teal"/>""" + Close,
        Open + """<rect width="0" height="20" fill="teal"/><circle r="0" cx="9" cy="9"/>""" + Close,
        Open + """<circle cx="24" cy="24" r="18" fill="gold"/>""" + Close,
        Open + """<ellipse cx="24" cy="24" rx="20" ry="9" fill="navy"/>""" + Close,
        Open + """<line x1="4" y1="4" x2="40" y2="30" stroke="black" stroke-width="3"/>""" + Close,
        Open + """<polyline points="4,4 20,30 40,8" fill="none" stroke="#000" stroke-width="2"/>""" + Close,
        Open + """<polyline points="4,4 20,30 40,8" fill="orange"/>""" + Close,
        Open + """<polygon points="4 4 44 4 24-40 24 44" fill="purple"/>""" + Close,
        // Fill and stroke
        Open + """<path fill-rule="evenodd" fill="#000" d="M4 4h40v40H4zM14 14h20v20H14z"/>""" + Close,
        Open + """<path d="M4 4h40v40H4zM14 14h20v20H14z"/>""" + Close,
        Open + """<path fill="none" stroke="red" stroke-width="6" stroke-linecap="round" stroke-linejoin="round" d="M8 8L40 8L24 40"/>""" + Close,
        Open + """<path fill="none" stroke="red" stroke-width="6" stroke-linecap="square" stroke-linejoin="bevel" d="M8 8L40 8L24 40"/>""" + Close,
        Open + """<path fill="none" stroke="red" stroke-width="6" stroke-miterlimit="1.5" d="M8 8L40 8L24 40"/>""" + Close,
        Open + """<circle cx="24" cy="24" r="16" fill="#2563EB" fill-opacity=".5" stroke="#E11D48" stroke-opacity=".5" stroke-width="6"/>""" + Close,
        // Dashes, which one color would hide
        Open + """<circle cx="24" cy="24" r="16" fill="none" stroke="#94A3B8" stroke-width="6"/><circle cx="24" cy="24" r="16" fill="none" stroke="#E11D48" stroke-width="6" stroke-dasharray="37.7 50.3"/>""" + Close,
        Open + """<circle cx="24" cy="24" r="16" fill="none" stroke="#2563EB" stroke-width="6" stroke-dasharray="20 80" stroke-dashoffset="-30"/>""" + Close,
        Open + """<line x1="2" y1="24" x2="46" y2="24" stroke="#000" stroke-width="4" stroke-dasharray="5,3,1"/>""" + Close,
        // Colors
        Open + """<rect width="24" height="24" fill="#f00"/><rect x="24" width="24" height="24" fill="#f008"/><rect y="24" width="24" height="24" fill="#00ff0080"/><rect x="24" y="24" width="24" height="24" fill="rgb(10,20,200)"/>""" + Close,
        Open + """<rect width="24" height="24" fill="rgba(10,20,200,.5)"/><rect x="24" width="24" height="24" fill="rebeccapurple"/><rect y="24" width="24" height="24" fill="DarkSlateGray"/><rect x="24" y="24" width="24" height="24" fill="transparent"/>""" + Close,
        // Inheritance, transforms, groups
        Open + """<g fill="red" stroke="blue" stroke-width="2"><rect x="4" y="4" width="10" height="10"/><g fill="green"><circle cx="30" cy="10" r="6"/></g><rect x="4" y="24" width="10" height="10" stroke="none"/></g>""" + Close,
        Open + """<g transform="translate(10 4) scale(1.5)"><rect width="12" height="12" fill="red"/><rect x="12" width="6" height="6" transform="rotate(30)" fill="blue"/></g>""" + Close,
        Open + """<rect x="10" y="10" width="14" height="14" fill="red" transform="rotate(20,17,17)"/><rect x="24" y="4" width="10" height="10" fill="blue" transform="skewX(20)"/><rect x="4" y="30" width="10" height="10" fill="green" transform="matrix(1 .2 .3 1 2 -3)"/>""" + Close,
        Open + """<g opacity=".5"><rect x="4" y="4" width="24" height="24" fill="red"/><rect x="16" y="16" width="24" height="24" fill="blue"/></g>""" + Close,
        Open + """<g display="none"><rect width="48" height="48" fill="red"/></g><rect width="9" height="9" fill="blue"/>""" + Close,
        Open + """<g visibility="hidden"><rect width="20" height="20" fill="red"/><rect x="24" width="20" height="20" fill="blue" visibility="visible"/></g>""" + Close,
        // Clips and masks, the references after their use
        Open + """<g clip-path="url(#c)"><rect width="48" height="48" fill="red"/></g><defs><clipPath id="c"><circle cx="24" cy="24" r="14"/></clipPath></defs>""" + Close,
        Open + """<rect width="48" height="48" fill="red" clip-path="url(#c)"/><clipPath id="c"><rect x="4" y="4" width="10" height="10"/><circle cx="30" cy="30" r="10" transform="translate(4 4)"/></clipPath>""" + Close,
        Open + """<g mask="url(#m)"><rect width="48" height="48" fill="red"/></g><mask id="m" maskUnits="userSpaceOnUse" x="0" y="0" width="48" height="48"><rect width="48" height="48" fill="#fff"/><circle cx="24" cy="24" r="12" fill="#000"/></mask>""" + Close,
        Open + """<g mask="url(#m)"><rect width="48" height="48" fill="blue"/></g><mask id="m" maskUnits="userSpaceOnUse" x="0" y="0" width="48" height="48" style="mask-type:alpha"><circle cx="24" cy="24" r="12" fill="#000"/></mask>""" + Close,
        // A clip's and a mask's content inherits from what is around them, not from what uses them
        """<svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 48 48" fill="#fff" stroke-width="4"><mask id="m" maskUnits="userSpaceOnUse" x="0" y="0" width="48" height="48"><circle cx="24" cy="24" r="16"/><circle cx="24" cy="24" r="8" stroke="#000" fill="none"/></mask><rect width="48" height="48" fill="red" mask="url(#m)"/></svg>""",
        Open + """<defs fill="#fff"><g fill="none"><mask id="m" maskUnits="userSpaceOnUse" x="0" y="0" width="48" height="48"><circle cx="24" cy="24" r="16" stroke="#fff" stroke-width="4"/></mask></g></defs><g fill="blue" stroke="red" stroke-width="20"><rect width="48" height="48" mask="url(#m)"/></g>""" + Close,
        Open + """<clipPath id="c" clip-rule="evenodd"><path d="M4 4h40v40H4zM14 14h20v20H14z"/></clipPath><rect width="48" height="48" fill="red" clip-path="url(#c)"/>""" + Close,
        Open + """<clipPath id="c"><rect width="20" height="48"/><rect x="28" width="20" height="48" visibility="hidden"/></clipPath><rect width="48" height="48" fill="red" clip-path="url(#c)"/>""" + Close,
        Open + """<a href="https://example.com/"><rect width="20" height="20" fill="teal"/></a>""" + Close,
        // The cascade: attribute, then class rule, then inline style
        Open + """<style>.a{fill:red}.b,.c{fill:blue;stroke:#000}</style><rect class="a" width="10" height="10" fill="green"/><rect class="b" x="12" width="10" height="10"/><rect class="c" x="24" width="10" height="10" style="fill:orange"/>""" + Close,
        Open + """<style><![CDATA[ /* note */ .a { fill: #2563EB !important } ]]></style><rect class="x a" width="20" height="20"/>""" + Close,
        Open + """<rect width="20" height="20" style="fill:#E11D48;stroke:#000;stroke-width:2"/>""" + Close,
        // Sizes and placement
        """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><rect x="2" y="2" width="20" height="20" fill="red"/></svg>""",
        """<svg xmlns="http://www.w3.org/2000/svg" viewBox="10 10 24 24" width="48" height="48"><circle cx="22" cy="22" r="10" fill="red"/></svg>""",
        """<svg xmlns="http://www.w3.org/2000/svg" width="30px" height="20px"><rect width="30" height="20" fill="red"/></svg>""",
        """<svg xmlns="http://www.w3.org/2000/svg" width="100%" height="100%" viewBox="0 0 24 24"><rect width="24" height="24" fill="red"/></svg>""",
        """<svg xmlns="http://www.w3.org/2000/svg" width="48" height="24" viewBox="0 0 24 24"><rect width="24" height="24" fill="red"/></svg>""",
        """<svg xmlns="http://www.w3.org/2000/svg" width="48" height="24" viewBox="0 0 24 24" preserveAspectRatio="xMinYMin meet"><rect width="24" height="24" fill="red"/></svg>""",
        """<svg xmlns="http://www.w3.org/2000/svg" width="48" height="24" viewBox="0 0 24 24" preserveAspectRatio="xMaxYMid slice"><rect width="24" height="24" fill="red"/><rect y="12" width="24" height="12" fill="blue"/></svg>""",
        """<svg xmlns="http://www.w3.org/2000/svg" width="48" height="24" viewBox="0 0 24 24" preserveAspectRatio="none"><circle cx="12" cy="12" r="12" fill="red"/></svg>""",
        // What surrounds the drawing
        """<?xml version="1.0" encoding="UTF-8"?><!-- comment --><!DOCTYPE svg PUBLIC "-//W3C//DTD SVG 1.1//EN" "http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd"><svg xmlns="http://www.w3.org/2000/svg" width="24" height="24"><title>t</title><desc>d</desc><metadata/><rect width="9" height="9" fill="red"/></svg>""",
        """<svg xmlns="http://www.w3.org/2000/svg" xmlns:x="urn:x" width="24" height="24"><x:rect width="24" height="24" fill="blue"/><x:a><rect width="24" height="24" fill="blue"/></x:a><rect width="9" height="9" fill="red"/></svg>""",
        """<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24"><sc-for list="{{ a }}"><rect width="24" height="24" fill="blue" stroke-dasharray="{{ b }}"/></sc-for><rect width="9" height="9" fill="red"/></svg>"""
    };

    [Theory]
    [MemberData(nameof(Documents))]
    public void Draws_as_Svg_Skia_does(string svg) =>
        AssertMatchesSvgSkia(Encoding.UTF8.GetBytes(svg), svg);

    [Fact]
    public void Corpus_draws_as_Svg_Skia_does()
    {
        if (Environment.GetEnvironmentVariable(CorpusVariable) is not { Length: > 0 } folder)
            return;
        var failures = new List<string>();
        var drawn = 0;
        foreach (var file in Directory.EnumerateFiles(folder, "*.svg", SearchOption.AllDirectories))
        {
            try
            {
                AssertMatchesSvgSkia(File.ReadAllBytes(file), file);
                drawn++;
            }
            catch (Exception error) when (error is InvalidDataException or Xunit.Sdk.XunitException)
            {
                failures.Add($"{Path.GetFileName(file)}: {error.Message}");
            }
        }
        Assert.True(failures.Count == 0, $"{drawn} drawn, {failures.Count} not:\n{string.Join('\n', failures)}");
    }

    [Theory]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg" width="10" height="20"/>""", 10, 20)]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 12"/>""", 24, 12)]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg" width="48" viewBox="0 0 24 12"/>""", 48, 24)]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg" height="48" viewBox="0 0 24 12"/>""", 96, 48)]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg" width="100%" viewBox="0 0 24 12"/>""", 24, 12)]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg" width="12pt" height="24pt"/>""", 16, 32)]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg" width="1em" height="1em" viewBox="0 0 24 12"/>""", 24, 12)]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg" width="auto" height="48" viewBox="0 0 24 12"/>""", 96, 48)]
    public void Reads_the_intrinsic_size(string svg, float width, float height)
    {
        var size = SkUiVectorDocument.Parse(Encoding.UTF8.GetBytes(svg)).Size;
        Assert.Equal(width, size.Width, 3);
        Assert.Equal(height, size.Height, 3);
    }

    [Fact]
    public void CurrentColor_takes_the_requested_color_and_keeps_written_colors()
    {
        var document = Parse(Open + """<rect width="24" height="48" fill="currentColor"/><rect x="24" width="24" height="48" fill="#00FF00"/>""" + Close);
        using var image = SkUiVectorPainter.Render(document, new SKSizeI(48, 48), SKColors.Red);
        using var pixels = image.PeekPixels();
        Assert.Equal(SKColors.Red, pixels.GetPixelColor(12, 24));
        Assert.Equal(new SKColor(0, 255, 0), pixels.GetPixelColor(36, 24));
    }

    [Fact]
    public void CurrentColor_follows_the_color_property_where_it_is_used()
    {
        var document = Parse(Open + """<g fill="currentColor" color="#00FF00"><rect width="24" height="48"/><rect x="24" width="24" height="48" color="#0000FF"/></g>""" + Close);
        using var image = SkUiVectorPainter.Render(document, new SKSizeI(48, 48), SKColors.Red);
        using var pixels = image.PeekPixels();
        Assert.Equal(new SKColor(0, 255, 0), pixels.GetPixelColor(12, 24));
        Assert.Equal(new SKColor(0, 0, 255), pixels.GetPixelColor(36, 24));
    }

    [Fact]
    public void Text_uses_a_registered_font()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var document = Parse($"""<svg xmlns="http://www.w3.org/2000/svg" width="96" height="48"><text x="48" y="32" font-size="24" font-family="'{SkUiTestHelpers.BundledFontFamily}', serif" text-anchor="middle" fill="#000">TT</text></svg>""");
        using var image = SkUiVectorPainter.Render(document, new SKSizeI(96, 48), SKColors.Black);
        Assert.True(Inked(image) > 0);
        Assert.True(Inked(image, 0, 28) == 0, "centered text leaves the left of the image empty");
    }

    [Theory]
    [InlineData("<image href=\"a.png\"/>", "<image>")]
    [InlineData("<use href=\"#a\"/>", "<use>")]
    [InlineData("<symbol id=\"a\"/>", "<symbol>")]
    [InlineData("<defs><pattern id=\"a\"/></defs>", "<pattern>")]
    [InlineData("<defs><linearGradient id=\"a\"/></defs>", "<linearGradient>")]
    [InlineData("<defs><radialGradient id=\"a\"/></defs>", "<radialGradient>")]
    [InlineData("<filter id=\"a\"/>", "<filter>")]
    [InlineData("<foreignObject/>", "<foreignObject>")]
    [InlineData("<text>a<tspan>b</tspan></text>", "<tspan>")]
    [InlineData("<svg/>", "nested <svg>")]
    [InlineData("<rect fill=\"url(#a)\" width=\"1\" height=\"1\"/>", "fill")]
    [InlineData("<rect style=\"stroke:url(#a)\" width=\"1\" height=\"1\"/>", "stroke")]
    [InlineData("<rect filter=\"url(#a)\" width=\"1\" height=\"1\"/>", "filter")]
    [InlineData("<style>rect{fill:red}</style>", "rect")]
    [InlineData("<style>@import url(http://127.0.0.1/a.css);</style>", "@import")]
    [InlineData("<clipPath id=\"a\" clipPathUnits=\"objectBoundingBox\"/>", "objectBoundingBox")]
    public void What_cannot_be_drawn_fails_and_says_what(string element, string named)
    {
        var error = Assert.Throws<InvalidDataException>(() => Parse(Open + element + Close));
        Assert.Contains(named, error.Message);
    }

    [Theory]
    [InlineData("<html/>")]
    [InlineData("<svg")]
    [InlineData("not xml at all")]
    [InlineData("")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"/>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 0 0\"/>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"a b c d\"/>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"-4\" height=\"4\"/>")]
    public void Bad_files_fail_with_one_exception(string text) =>
        Assert.Throws<InvalidDataException>(() => Parse(text));

    [Theory]
    [InlineData("""<path d="M4 4 L nonsense"/>""")]
    [InlineData("""<rect width="abc" height="4"/>""")]
    [InlineData("""<rect width="10%" height="4"/>""")]
    [InlineData("""<rect width="4" height="4" fill="#12"/>""")]
    [InlineData("""<rect width="4" height="4" fill="hsl(1,2%,3%)"/>""")]
    [InlineData("""<rect width="4" height="4" transform="wobble(1)"/>""")]
    public void Bad_values_fail_when_drawn(string element)
    {
        var document = Parse(Open + element + Close);
        Assert.Throws<InvalidDataException>(() => SkUiVectorPainter.Render(document, new SKSizeI(48, 48), SKColors.Black).Dispose());
    }

    [Theory]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg" width="1e30" height="1e30"/>""")]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg" height="3e38" viewBox="0 0 3e38 1"/>""")]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="3e38 3e38 3e38 3e38"/>""")]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg" width="1em" height="1em"/>""")]
    public void A_size_out_of_range_or_missing_fails(string svg) =>
        Assert.Throws<InvalidDataException>(() => SkUiVectorDocument.Parse(Encoding.UTF8.GetBytes(svg)));

    [Theory]
    [InlineData(""".a{fill:#f00!important}""", """fill="#00f" style="fill:#0f0" """, 0xFF, 0x00)] // beats a later inline style
    [InlineData(""".a{fill:#f00!important}.a{fill:#0f0}""", "", 0xFF, 0x00)] // beats a later rule
    [InlineData(""".a{fill:#f00!important}""", """style="fill:#0f0!important" """, 0x00, 0xFF)] // only !important replaces it
    [InlineData(""".a{fill:#f00}.a{fill:#0f0}""", "", 0x00, 0xFF)] // otherwise the later rule wins
    public void The_cascade_honors_important(string css, string attributes, byte red, byte green)
    {
        var document = Parse($"""{Open}<style>{css}</style><rect class="a" width="48" height="48" {attributes}/>{Close}""");

        using var image = SkUiVectorPainter.Render(document, new SKSizeI(4, 4), SKColors.Black);
        using var pixels = SKBitmap.FromImage(image);

        Assert.Equal((red, green), (pixels.GetPixel(2, 2).Red, pixels.GetPixel(2, 2).Green));
    }

    [Fact]
    public void Rules_of_several_classes_apply_in_their_order_not_the_attributes()
    {
        var document = Parse(Open + """<style>.b{fill:#f00}.a{fill:#0f0}</style><rect class="a b" width="48" height="48"/>""" + Close);

        using var image = SkUiVectorPainter.Render(document, new SKSizeI(4, 4), SKColors.Black);
        using var pixels = SKBitmap.FromImage(image);

        Assert.Equal(SKColors.Lime, pixels.GetPixel(2, 2));
    }

    [Theory]
    [InlineData("""<rect width="9" height="9" style="mix-blend-mode:multiply"/>""", "mix-blend-mode")]
    [InlineData("""<rect width="9" height="9" paint-order="stroke"/>""", "paint-order")]
    [InlineData("""<rect width="9" height="9" paint-order="stroke fill"/>""", "paint-order")]
    [InlineData("""<rect width="9" height="9" vector-effect="non-scaling-stroke"/>""", "vector-effect")]
    [InlineData("""<rect width="9" height="9" transform-origin="4 4"/>""", "transform-origin")]
    [InlineData("""<rect width="9" height="9" transform-origin="0"/>""", "transform-origin")]
    [InlineData("""<mask id="m" width="100%" height="100%"/>""", "region")]
    [InlineData("""<clipPath id="c" clip-path="url(#d)"/>""", "clip-path")]
    [InlineData("""<mask id="m" mask="url(#n)"/>""", "mask")]
    [InlineData("""<mask id="m" x="10%" y="0" width="50%" height="100%"/>""", "region")]
    [InlineData("""<mask id="m" x=".1" y="0" width=".5" height="1"/>""", "region")]
    [InlineData("<style>.a\nrect{fill:red}</style>", "selector")]
    public void What_would_be_ignored_otherwise_fails_when_read(string element, string named)
    {
        var error = Assert.Throws<InvalidDataException>(() => Parse(Open + element + Close));

        Assert.Contains(named, error.Message);
    }

    [Theory]
    [InlineData("""<rect width="48" height="48" fill="#f00" paint-order="normal"/>""")]
    [InlineData("""<rect width="48" height="48" fill="#f00" paint-order="fill stroke"/>""")]
    [InlineData("""<rect width="48" height="48" fill="#f00" style="paint-order:markers fill stroke"/>""")]
    [InlineData("""<rect width="48" height="48" fill="#f00" paint-order="markers"/>""")]
    [InlineData("""<rect width="48" height="48" fill="#f00" style="transform-origin:0px 0px"/>""")]
    [InlineData("""<rect width="48" height="48" fill="#f00" transform-origin="0 0"/>""")]
    [InlineData("""<rect width="48" height="48" fill="#f00" mask="url(#m)"/><mask id="m" x="-5%"><rect width="48" height="48" fill="#fff"/></mask>""")]
    [InlineData("""<rect width="48" height="48" fill="#f00" mask="url(#m)"/><mask id="m" x="0" y="0" width="100%" height="100%"><rect width="48" height="48" fill="#fff"/></mask>""")]
    [InlineData("""<rect width="48" height="48" fill="#f00" mask="url(#m)"/><mask id="m" x="-10%" y="-10%" width="120%" height="120%"><rect width="48" height="48" fill="#fff"/></mask>""")]
    [InlineData("""<rect width="48" height="48" fill="#f00" mask="url(#m)"/><mask id="m" x="0" y="0" width="1" height="1"><rect width="48" height="48" fill="#fff"/></mask>""")]
    public void Values_that_change_nothing_draw(string element)
    {
        using var image = SkUiVectorPainter.Render(Parse(Open + element + Close), new SKSizeI(48, 48), SKColors.Black);
        using var pixels = SKBitmap.FromImage(image);

        Assert.Equal(SKColors.Red, pixels.GetPixel(24, 24));
    }

    [Fact]
    public void A_few_layers_draw_at_the_largest_bitmap()
    {
        var layers = string.Concat(Enumerable.Repeat("""<rect width="48" height="48" fill="#f00" opacity=".5"/>""", 40));

        SkUiVectorPainter.Render(Parse(Open + layers + Close), new SKSizeI(2048, 2048), SKColors.Black).Dispose();
    }

    [Theory]
    [InlineData("""<text x="1 2 3" y="9">a</text>""", "positions")]
    [InlineData("""<text x="1" y="9" dx="2">a</text>""", "dx")]
    [InlineData("""<mask id="m" maskUnits="userSpaceOnUse" width="50%" height="50%"/><rect width="9" height="9" mask="url(#m)"/>""", "width")]
    public void What_would_be_ignored_otherwise_fails_when_drawn(string element, string named)
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var document = Parse(Open + element + Close);

        var error = Assert.Throws<InvalidDataException>(() => SkUiVectorPainter.Render(document, new SKSizeI(48, 48), SKColors.Black).Dispose());

        Assert.Contains(named, error.Message);
    }

    [Fact]
    public void An_error_shows_little_of_the_file()
    {
        var error = Assert.Throws<InvalidDataException>(() => Parse(Open + "<style>" + new string('a', 50_000) + "</style>" + Close));

        Assert.True(error.Message.Length < 200, error.Message);
    }

    [Fact]
    public void Cancelling_stops_reading_and_drawing()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var svg = Open + """<rect width="9" height="9"/>""" + Close;

        Assert.ThrowsAny<OperationCanceledException>(() => SkUiVectorDocument.Parse(Encoding.UTF8.GetBytes(svg), cancelled.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => SkUiVectorPainter.Render(Parse(svg), new SKSizeI(48, 48), SKColors.Black, cancelled.Token));
    }

    public static TheoryData<string, string> HostileFiles
    {
        get
        {
            const string Rect = """<rect width="48" height="48" """;
            static string Wavy(int points)
            {
                var path = new StringBuilder("M0 0");
                for (var index = 0; index < points; index++)
                    path.Append(index % 2 == 0 ? "L900 " : "L0 ").Append(index % 900).Append(' ');
                return path.ToString();
            }
            var mask = new StringBuilder(Rect).Append("""mask="url(#m0)"/>""");
            for (var level = 0; level < 5; level++)
            {
                mask.Append($"""<mask id="m{level}">""");
                for (var copy = 0; copy < 20; copy++)
                    mask.Append(level == 4 ? $"""<path fill="#fff" d="{Wavy(3_000)}"/>""" : $"""{Rect}mask="url(#m{level + 1})"/>""");
                mask.Append("</mask>");
            }
            var classes = string.Concat(Enumerable.Range(0, 4_000).Select(static index => $".c{index},")) + ".z{fill:red}";
            return new TheoryData<string, string>
            {
                { "comments", "<style>" + string.Concat(Enumerable.Repeat("/**/", 250_000)) + "</style>" },
                { "selectors for each element", "<style>" + string.Concat(Enumerable.Repeat(".b,", 190_000)) + ".b{fill:red}</style>" + string.Concat(Enumerable.Repeat("""<g class="b"/>""", 19_000)) },
                { "class names", "<style>" + classes + """</style><g class=" """ + string.Concat(Enumerable.Repeat("z ", 300_000)) + """ "/>""" },
                { "declarations for each element", "<style>.a{" + string.Concat(Enumerable.Repeat("fill:red;", 60_000)) + "}</style>" + string.Concat(Enumerable.Repeat("""<g class="a"/>""", 19_000)) },
                { "empty rules for each class name", "<style>" + string.Concat(Enumerable.Repeat(".a,", 4_990)) + """.a{}</style><g class=" """ + string.Concat(Enumerable.Repeat("a ", 400_000)) + """ "/>""" },
                { "many attributes read many times", ManyAttributes() },
                { "layers", string.Concat(Enumerable.Repeat(Rect + """opacity=".5"/>""", 19_000)) },
                { "masks over a long path", mask.Append(Close[..0]).ToString() },
                { "a long round stroke", $"""<path d="{Wavy(110_000)}" fill="none" stroke="#000" stroke-width="300" stroke-linejoin="round" stroke-linecap="round"/>""" },
                { "a stroke just within the budget", $"""<path d="{Wavy(25_000)}" fill="none" stroke="#000" stroke-width="300" stroke-linejoin="round" stroke-linecap="round"/>""" },
                { "many dashed lines", string.Concat(Enumerable.Repeat("""<line x2="100000" y2="100000" stroke="#000" stroke-dasharray=".1"/>""", 14_000)) },
                { "a clip of many shapes", """<clipPath id="c">""" + string.Concat(Enumerable.Repeat("""<circle cx="9" cy="9" r="9"/>""", 15_000)) + "</clipPath>" + Rect + """clip-path="url(#c)"/>""" }
            };
        }
    }

    // A path with many empty attributes, visited many times through masks.
    private static string ManyAttributes()
    {
        var attributes = string.Concat(Enumerable.Range(0, 20_000).Select(static index => $"""a{index}="" """));
        var svg = new StringBuilder("""<rect width="48" height="48" mask="url(#m0)"/><mask id="m0">""");
        for (var copy = 0; copy < 200; copy++)
            svg.Append("""<rect width="48" height="48" mask="url(#m1)"/>""");
        svg.Append("""</mask><mask id="m1">""");
        for (var copy = 0; copy < 200; copy++)
            svg.Append(copy == 0 ? $"""<path id="p" d="M0 0h9v9z" {attributes}/>""" : """<g/>""");
        return svg.Append("</mask>").ToString();
    }

    [Fact]
    public void Attribute_names_count_as_work()
    {
        var document = Parse(Open + ManyAttributes() + Close);

        var error = Assert.Throws<InvalidDataException>(() => SkUiVectorPainter.Render(document, new SKSizeI(48, 48), SKColors.Black).Dispose());

        Assert.Contains("work", error.Message);
    }

    [Theory]
    [MemberData(nameof(HostileFiles))]
    public async Task A_small_file_cannot_hold_a_thread(string name, string content)
    {
        var svg = Open + content + Close;
        Assert.True(svg.Length <= SkUiVectorDocument.MaxBytes, name); // within the limit on size, or it would prove nothing
        var work = Task.Run(() =>
        {
            try
            {
                SkUiVectorPainter.Render(Parse(svg), new SKSizeI(2048, 2048), SKColors.Black).Dispose();
            }
            catch (InvalidDataException)
            {
                // Refusing it is fine; finishing is the point.
            }
        });

        Assert.Same(work, await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(30))));
        await work;
    }

    [Fact]
    public void Deep_nesting_fails_instead_of_overflowing_the_stack()
    {
        // 512 levels overflowed a 512 KB stack in Svg.Skia, which cannot be caught.
        var error = Assert.Throws<InvalidDataException>(() =>
            Parse(Open + string.Concat(Enumerable.Repeat("<g>", 100_000)) + string.Concat(Enumerable.Repeat("</g>", 100_000)) + Close));
        Assert.Contains("nest", error.Message);
    }

    [Fact]
    public void Deep_nesting_of_skipped_elements_is_harmless()
    {
        var document = Parse(Open + string.Concat(Enumerable.Repeat("<metadata>", 20_000)) + string.Concat(Enumerable.Repeat("</metadata>", 20_000)) + Close);
        SkUiVectorPainter.Render(document, new SKSizeI(48, 48), SKColors.Black).Dispose();
    }

    [Fact]
    public void Limits_on_size_and_elements_apply()
    {
        Assert.Throws<InvalidDataException>(() => SkUiVectorDocument.Parse(new byte[SkUiVectorDocument.MaxBytes + 1]));
        Assert.Throws<InvalidDataException>(() => Parse(Open + string.Concat(Enumerable.Repeat("<g/>", SkUiVectorDocument.MaxElements + 1)) + Close));
    }

    [Fact]
    public void Entities_are_not_expanded()
    {
        const string Bomb = """<?xml version="1.0"?><!DOCTYPE svg [<!ENTITY a "aaaaaaaaaa"><!ENTITY b "&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;"><!ENTITY c "&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;">]><svg xmlns="http://www.w3.org/2000/svg" width="4" height="4"><text>&c;</text></svg>""";
        Assert.Throws<InvalidDataException>(() => Parse(Bomb));
    }

    [Fact]
    public void A_mask_cycle_fails()
    {
        var document = Parse(Open + """<rect width="9" height="9" mask="url(#a)"/><mask id="a"><rect width="9" height="9" mask="url(#b)"/></mask><mask id="b"><rect width="9" height="9" mask="url(#a)"/></mask>""" + Close);
        var error = Assert.Throws<InvalidDataException>(() => SkUiVectorPainter.Render(document, new SKSizeI(48, 48), SKColors.Black).Dispose());
        Assert.Contains("itself", error.Message);
    }

    [Fact]
    public void Masks_multiplying_their_work_stop()
    {
        // Each mask draws the next one 20 times: 20^5 elements from a small file.
        var svg = new StringBuilder(Open).Append("""<rect width="9" height="9" mask="url(#m0)"/>""");
        for (var level = 0; level < 5; level++)
        {
            svg.Append($"<mask id=\"m{level}\">");
            for (var copy = 0; copy < 20; copy++)
                svg.Append(level == 4 ? """<rect width="9" height="9" fill="#fff"/>""" : $"<g><rect width=\"9\" height=\"9\" mask=\"url(#m{level + 1})\"/></g>");
            svg.Append("</mask>");
        }
        var document = Parse(svg.Append(Close).ToString());
        Assert.Throws<InvalidDataException>(() => SkUiVectorPainter.Render(document, new SKSizeI(48, 48), SKColors.Black).Dispose());
    }

    [Fact]
    public void Masks_chaining_nesting_stop_within_a_small_stack()
    {
        // Each mask's content nests as deep as the reader allows and then uses the next mask.
        var svg = new StringBuilder(Open).Append("""<rect width="9" height="9" mask="url(#m0)"/>""");
        const int Levels = 5;
        var depth = SkUiVectorDocument.MaxDepth - 3;
        for (var level = 0; level < Levels; level++)
        {
            svg.Append($"<mask id=\"m{level}\">").Append(string.Concat(Enumerable.Repeat("<g>", depth)));
            svg.Append(level == Levels - 1 ? """<rect width="9" height="9" fill="#fff"/>""" : $"<rect width=\"9\" height=\"9\" mask=\"url(#m{level + 1})\"/>");
            svg.Append(string.Concat(Enumerable.Repeat("</g>", depth))).Append("</mask>");
        }
        var document = Parse(svg.Append(Close).ToString());
        Exception? failure = null;
        // A stack overflow would end the test process, which is the point: 256 KB is below any worker thread's.
        var thread = new Thread(() =>
        {
            try
            {
                SkUiVectorPainter.Render(document, new SKSizeI(48, 48), SKColors.Black).Dispose();
            }
            catch (Exception error)
            {
                failure = error;
            }
        }, maxStackSize: 256 * 1024);
        thread.Start();
        thread.Join();
        Assert.IsType<InvalidDataException>(failure);
    }

    [Fact]
    public void Nesting_at_the_limit_draws_on_a_small_stack()
    {
        var depth = SkUiVectorDocument.MaxDepth - 2;
        var document = Parse(Open + string.Concat(Enumerable.Repeat("<g>", depth)) + """<rect width="9" height="9"/>""" + string.Concat(Enumerable.Repeat("</g>", depth)) + Close);
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                SkUiVectorPainter.Render(document, new SKSizeI(48, 48), SKColors.Black).Dispose();
            }
            catch (Exception error)
            {
                failure = error;
            }
        }, maxStackSize: 256 * 1024);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }

    [Fact]
    public void Nested_layers_are_limited()
    {
        var count = SkUiVectorPainter.MaxLayers + 1;
        var document = Parse(Open + string.Concat(Enumerable.Repeat("""<g opacity=".9">""", count)) + """<rect width="9" height="9"/>""" + string.Concat(Enumerable.Repeat("</g>", count)) + Close);
        var error = Assert.Throws<InvalidDataException>(() => SkUiVectorPainter.Render(document, new SKSizeI(48, 48), SKColors.Black).Dispose());
        Assert.Contains("layers", error.Message);
    }

    [Theory]
    [InlineData("""<path d="M0 0L100000 100000" stroke="#000" stroke-dasharray=".001"/>""")]
    [InlineData("""<path d="M0 0L9 9" stroke="#000" stroke-width="100000000"/>""")]
    [InlineData("""<rect width="1000000000" height="1000000000" rx="999"/>""")]
    public async Task Costly_values_finish(string element)
    {
        var document = Parse(Open + element + Close);
        var draw = Task.Run(() =>
        {
            try
            {
                SkUiVectorPainter.Render(document, new SKSizeI(48, 48), SKColors.Black).Dispose();
            }
            catch (InvalidDataException)
            {
                // Refusing it is fine; finishing is the point.
            }
        });
        Assert.Same(draw, await Task.WhenAny(draw, Task.Delay(TimeSpan.FromSeconds(10))));
        await draw;
    }

    [Theory]
    [InlineData("""<!DOCTYPE svg SYSTEM "http://127.0.0.1:{0}/a.dtd"><svg xmlns="http://www.w3.org/2000/svg" width="4" height="4"/>""")]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg" width="4" height="4"><image href="http://127.0.0.1:{0}/a.png"/></svg>""")]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg" width="4" height="4"><style>@import url(http://127.0.0.1:{0}/a.css);</style></svg>""")]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg" width="4" height="4"><rect width="4" height="4" fill="url(http://127.0.0.1:{0}/a#b)"/></svg>""")]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg" width="4" height="4"><rect width="4" height="4" mask="url(http://127.0.0.1:{0}/a#b)"/></svg>""")]
    public void No_file_makes_a_request(string template)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            try
            {
                using var image = SkUiVectorPainter.Render(Parse(string.Format(template, port)), new SKSizeI(4, 4), SKColors.Black);
            }
            catch (InvalidDataException)
            {
                // Refusing is as good as drawing without it.
            }
            Assert.False(listener.Pending());
        }
        finally
        {
            listener.Stop();
        }
    }

    [Theory]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF })]
    [InlineData(new byte[] { (byte)' ', (byte)'\n' })]
    public void Reads_past_a_byte_order_mark_and_white_space(byte[] prefix)
    {
        var bytes = prefix.Concat(Encoding.UTF8.GetBytes("""<?xml version="1.0"?><svg xmlns="http://www.w3.org/2000/svg" width="4" height="4"/>""")).ToArray();
        Assert.True(SkUiVectorDocument.LooksLikeSvg(bytes));
        Assert.Equal(4, SkUiVectorDocument.Parse(bytes).Size.Width);
    }

    [Fact]
    public void Reads_UTF16()
    {
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("""<svg xmlns="http://www.w3.org/2000/svg" width="4" height="4"/>""")).ToArray();
        Assert.True(SkUiVectorDocument.LooksLikeSvg(bytes));
        Assert.Equal(4, SkUiVectorDocument.Parse(bytes).Size.Width);
    }

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47 })]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { (byte)' ' })]
    public void Other_bytes_do_not_look_like_an_SVG(byte[] bytes) =>
        Assert.False(SkUiVectorDocument.LooksLikeSvg(bytes));

    private static SkUiVectorDocument Parse(string svg) => SkUiVectorDocument.Parse(Encoding.UTF8.GetBytes(svg));

    private static int Inked(SKImage image, int left = 0, int right = int.MaxValue)
    {
        using var pixels = image.PeekPixels();
        var inked = 0;
        for (var y = 0; y < pixels.Height; y++)
        {
            for (var x = left; x < Math.Min(right, pixels.Width); x++)
            {
                if (pixels.GetPixelColor(x, y).Alpha > 0)
                    inked++;
            }
        }
        return inked;
    }

    private static void AssertMatchesSvgSkia(byte[] bytes, string name)
    {
        var document = SkUiVectorDocument.Parse(bytes);
        // Svg.Skia rounds a fractional size to whole units, which moves everything: only whole sizes compare fairly.
        var size = new SKSizeI((int)Math.Ceiling(document.Size.Width * Scale), (int)Math.Ceiling(document.Size.Height * Scale));
        using var mine = SkUiVectorPainter.Render(document, size, SKColors.Black);

        using var svg = new SKSvg();
        using var stream = new MemoryStream(bytes);
        var picture = svg.Load(stream) ?? throw new InvalidDataException("Svg.Skia read nothing.");
        using var surface = SKSurface.Create(new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        var cull = picture.CullRect;
        surface.Canvas.Scale(size.Width / cull.Width, size.Height / cull.Height);
        surface.Canvas.Translate(-cull.Left, -cull.Top);
        surface.Canvas.DrawPicture(picture);
        surface.Flush();

        using var a = mine.PeekPixels();
        using var b = surface.PeekPixels();
        long inked = 0;
        long differing = 0;
        for (var y = 0; y < size.Height; y++)
        {
            for (var x = 0; x < size.Width; x++)
            {
                var p = a.GetPixelColor(x, y);
                var q = b.GetPixelColor(x, y);
                if (p.Alpha == 0 && q.Alpha == 0)
                    continue;
                inked++;
                if (Math.Abs(p.Red - q.Red) > ChannelTolerance || Math.Abs(p.Green - q.Green) > ChannelTolerance
                    || Math.Abs(p.Blue - q.Blue) > ChannelTolerance || Math.Abs(p.Alpha - q.Alpha) > ChannelTolerance)
                    differing++;
            }
        }
        var percent = inked == 0 ? 0 : 100d * differing / inked;
        Assert.True(percent < MaxDifferingPercent, $"{percent:F2}% of {inked} pixels differ from Svg.Skia: {name}");
    }
}
