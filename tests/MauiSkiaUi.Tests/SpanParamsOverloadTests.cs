using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The <c>params ReadOnlySpan&lt;T&gt;</c> overloads beside the <c>params T[]</c> members (1.14 A7) set what the array overloads
/// set; an explicit array still binds to the array overload, which keeps its null check.
/// </summary>
public class SpanParamsOverloadTests
{
    private static readonly Point[] Triangle = [new(0, 0), new(10, 0), new(5, 8)];

    [Fact]
    public void DashArraysMatch()
    {
        ReadOnlySpan<double> dashes = [2, 1, 0.5];
        Assert.Equal(new SkUiRectangle().SetStrokeDashArray(2, 1, 0.5).StrokeDashArray, new SkUiRectangle().SetStrokeDashArray(dashes).StrokeDashArray);
        Assert.Equal(new SkUiBorder().SetStrokeDashArray([2, 1, 0.5]).StrokeDashArray, new SkUiBorder().SetStrokeDashArray(dashes).StrokeDashArray);
        Assert.Equal(new SkUiCoreRectangle().SetStrokeDashArray(new double[] { 2, 1, 0.5 }).StrokeDashArray,
            new SkUiCoreRectangle().SetStrokeDashArray(dashes).StrokeDashArray);
        Assert.Equal([2, 1, 0.5], ((SkUiCoreBorder)new SkUiCoreBorder().SetStrokeDashArray(dashes)).StrokeDashArray);
        Assert.Empty(new SkUiCoreRectangle().SetStrokeDashArray(2, 1).SetStrokeDashArray().StrokeDashArray); // no values: solid
    }

    [Fact]
    public void PointsMatch()
    {
        Assert.Equal(Triangle, new SkUiPolygon().SetPoints(Triangle[0], Triangle[1], Triangle[2]).Points);
        Assert.Equal(Triangle, new SkUiCorePolygon(Triangle[0], Triangle[1], Triangle[2]).Points);
        Assert.Equal(Triangle, new SkUiCorePolyline(Triangle).Points);
        Assert.Equal(Triangle, new SkUiCorePolyline().SetPoints((ReadOnlySpan<Point>)Triangle).Points);
        Assert.Empty(new SkUiCorePolygon().Points);
    }

    [Fact]
    public void TransformationsMatch()
    {
        var circle = new SkUiCircleTransformation();
        var flip = new SkUiFlipTransformation();
        Assert.Equal([circle, flip], new SkUiCoreImage().SetTransformations(circle, flip).Transformations);
        Assert.Equal([circle, flip], new SkUiImage().SetTransformations(circle, flip).Transformations);
        Assert.Throws<ArgumentException>(() => new SkUiCoreImage().SetTransformations(circle, null!));
        Assert.Empty(new SkUiCoreImage().SetTransformations(circle).SetTransformations().Transformations);
    }

    [Fact]
    public void ArraysKeepTheirNullCheck()
    {
        Assert.Throws<ArgumentNullException>(() => new SkUiCoreRectangle().SetStrokeDashArray((double[])null!));
        Assert.Throws<ArgumentNullException>(() => new SkUiCorePolygon().SetPoints((Point[])null!));
        Assert.Throws<ArgumentNullException>(() => new SkUiCoreImage().SetTransformations((ISkUiImageTransformation[])null!));
        Assert.Throws<ArgumentNullException>(() => SkUiImageCacheKeys.IgnoreQueryParameters((string[])null!));
    }

    [Fact]
    public void QueryParameterKeysMatch()
    {
        var source = SkUiImageSource.FromUri(new Uri("https://example.com/a.png?token=1&size=2"));
        var fromArray = SkUiImageCacheKeys.IgnoreQueryParameters(new[] { "TOKEN" })(source);
        var fromSpan = SkUiImageCacheKeys.IgnoreQueryParameters("TOKEN")(source);
        Assert.Equal(fromArray, fromSpan);
        Assert.DoesNotContain("token", fromSpan);
    }
}
