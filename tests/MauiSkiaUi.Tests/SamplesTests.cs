using MauiSkiaUiSamples;
using MauiSkiaUiSamples.Samples.Customisation;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>The samples app (samples/MauiSkiaUiSamples): every example builds, describes itself and ships its source.</summary>
[Collection(GlobalStateCollection.Name)]
public class SamplesTests
{
    [Fact]
    public void EveryExampleBuildsDescribesItselfAndShipsItsSource()
    {
        Assert.NotEmpty(SampleCatalog.All);
        foreach (var entry in SampleCatalog.All)
        {
            var info = entry.Info;
            Assert.False(string.IsNullOrWhiteSpace(info.Title));
            Assert.False(string.IsNullOrWhiteSpace(info.Summary));
            Assert.NotEmpty(info.HowTo);
            Assert.NotEmpty(info.ThingsToKnow);
            foreach (var text in info.HowTo.Concat(info.ThingsToKnow).Append(info.Summary))
                Assert.True(text.Count(c => c == '`') % 2 == 0, $"{info.Title}: unbalanced backticks in \"{text}\"");

            var page = entry.Create();
            Assert.Equal(info.Title, page.Title);
            // [CallerFilePath] names the example's own file; the embedded copy is found by that name.
            Assert.EndsWith(info.SourceFileName, info.SourcePath.Replace('\\', '/'));
            var source = SampleSource.Load(info.SourceFileName);
            Assert.Contains($"class {Path.GetFileNameWithoutExtension(info.SourceFileName)}", source);
            Assert.Contains("SampleInfo Info", source); // the description is declared in the example's own file
            Assert.Equal(info.SourceFileName, new SourcePage(info).Title);
        }
        Assert.All(SampleCatalog.Sections, section => Assert.NotEmpty(section.Description()));
    }

    [Fact]
    public void SourceIsShownSyntaxHighlighted()
    {
        var html = SampleSource.ToHtml("public sealed class A { string s = \"<x>\"; } // note");
        Assert.Contains("<span", html);            // keywords, strings and comments get colors
        Assert.Contains("&lt;x&gt;", html);        // code is escaped
        Assert.Contains("white-space: pre", html); // not wrapped: scrolls sideways
    }

    [Fact]
    public void CrossCheckBoxArmsSpreadFromADot()
    {
        var previous = SkUiLook.Current;
        var reduce = SkUiMotion.ReduceMotion;
        try
        {
            SkUiLook.Current = new CrossCheckBoxLook();
            SkUiMotion.ReduceMotion = false;
            var box = new SkUiCheckBox();
            var root = new SkUiContentView { Content = box };
            using var surface = new SkUiTestSurface(root, 24, 24);
            surface.Frame();
            box.IsChecked = true;

            int WhiteOnDiagonal(double milliseconds)
            {
                root.AnimationClock.Tick(TimeSpan.FromMilliseconds(milliseconds));
                var bitmap = surface.Frame(milliseconds);
                return Enumerable.Range(0, 24).Count(i => bitmap.GetPixel(i, i) is { Red: > 200, Green: > 200, Blue: > 200 });
            }
            var start = WhiteOnDiagonal(20);
            var end = WhiteOnDiagonal(400);
            Assert.InRange(start, 1, end - 1); // a dot first, then the full arm
            Assert.True(end >= 8, $"full arm {end} px");
        }
        finally
        {
            SkUiLook.Current = previous;
            SkUiMotion.ReduceMotion = reduce;
        }
    }
}
