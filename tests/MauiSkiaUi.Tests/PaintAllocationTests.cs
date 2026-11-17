using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Look and painter drawing takes its paints from a per-thread stack (<see cref="SkUiScratchPaint"/>, 1.14 A4) instead of a new
/// <see cref="SKPaint"/> per call, so recording chrome allocates nothing once warm.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PaintAllocationTests(ITestOutputHelper output)
{
    private static long BytesPerCall(Action<SKCanvas> draw)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, 200, 200));
        for (var i = 0; i < 20; i++) draw(canvas);
        var fewest = long.MaxValue;
        for (var batch = 0; batch < 3 && fewest > 0; batch++)
        {
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++) draw(canvas);
            fewest = Math.Min(fewest, GC.GetAllocatedBytesForCurrentThread() - allocated);
        }
        recorder.EndRecording().Dispose();
        return fewest / 100;
    }

    public static TheoryData<string> Draws() => [.. DrawsByName.Keys];

    private static readonly Dictionary<string, Action<SKCanvas>> DrawsByName = new()
    {
        ["token"] = canvas => SkUiLook.Current.DrawPickerToken(canvas,
            new SkUiPickerTokenPaint(new SKRect(0, 0, 60, 20), new SKRect(44, 0, 60, 20), SKColors.LightGray, SKColors.Black, false, true, false)),
        ["more token"] = canvas => SkUiLook.Current.DrawPickerToken(canvas,
            new SkUiPickerTokenPaint(new SKRect(0, 0, 30, 20), SKRect.Empty, SKColors.LightGray, SKColors.Black, false, true, true)),
        ["clear button"] = canvas => SkUiLook.Current.DrawPickerClearButton(canvas,
            new SkUiClearButtonPaint(new SKRect(0, 0, 24, 24), SKColors.Gray, SKColors.White, false, true)),
        ["close button"] = canvas => SkUiLook.Current.DrawPopupCloseButton(canvas,
            new SkUiClearButtonPaint(new SKRect(0, 0, 36, 36), SKColors.Gray, SKColors.White, true, true)),
        ["selection handle"] = canvas => SkUiLook.Current.DrawTextSelectionHandle(canvas,
            new SkUiTextSelectionHandlePaint { Edge = new SKRect(20, 10, 20, 30), Color = SKColors.Blue, IsStart = true }),
        ["avatar badge"] = canvas => SkUiLook.Current.DrawAvatarBadge(canvas,
            new SkUiAvatarBadgePaint(new SKRect(0, 0, 40, 40), SkUiAvatarBadgePosition.BottomRight, new SKRect(28, 28, 40, 40), SKColors.Green, SKColors.White, 2)),
        ["content opacity"] = canvas => canvas.RestoreToCount(SkUiButtonFill.BeginContentOpacity(canvas, 0.5)),
    };

    [Theory]
    [MemberData(nameof(Draws))]
    public void DrawingChromeAllocatesNothing(string name)
    {
        var bytes = BytesPerCall(DrawsByName[name]);
        output.WriteLine($"{name}: {bytes} bytes per call");
        Assert.Equal(0, bytes);
    }

    [Fact]
    public void NestedRentalsGetTheirOwnPaints()
    {
        using var outer = SkUiScratchPaint.Rent(SKColors.Red);
        outer.Paint.Style = SKPaintStyle.Stroke;
        using (var inner = SkUiScratchPaint.Rent())
        {
            Assert.NotSame(outer.Paint, inner.Paint);
            Assert.Equal(SKPaintStyle.Fill, inner.Paint.Style); // nothing the caller set leaks in
            inner.Paint.Color = SKColors.Blue;
        }
        Assert.Equal(SKColors.Red, outer.Paint.Color);
        Assert.Equal(SKPaintStyle.Stroke, outer.Paint.Style);
    }

    [Fact]
    public void AReturnedPaintComesBackInItsDefaultState()
    {
        using var fresh = new SKPaint();
        SKPaint used;
        using (var scratch = SkUiScratchPaint.Rent(SKColors.Red))
        {
            used = scratch.Paint;
            used.Style = SKPaintStyle.Stroke;
            used.StrokeWidth = 4;
            used.BlendMode = SKBlendMode.Multiply;
            used.Shader = SKShader.CreateColor(SKColors.Green);
            used.PathEffect = SKPathEffect.CreateDash([2, 2], 0);
        }
        using var next = SkUiScratchPaint.Rent();
        Assert.Same(used, next.Paint);
        Assert.Equal(fresh.Color, next.Paint.Color);
        Assert.Equal(fresh.IsAntialias, next.Paint.IsAntialias);
        Assert.Equal(fresh.Style, next.Paint.Style);
        Assert.Equal(fresh.StrokeWidth, next.Paint.StrokeWidth);
        Assert.Equal(fresh.BlendMode, next.Paint.BlendMode);
        Assert.Null(next.Paint.Shader);
        Assert.Null(next.Paint.PathEffect);
    }

    [Fact]
    public void RentalsDeeperThanTheStackStillWork()
    {
        var seen = new HashSet<SKPaint>(ReferenceEqualityComparer.Instance);
        void Nest(int depth)
        {
            using var scratch = SkUiScratchPaint.Rent();
            Assert.True(seen.Add(scratch.Paint)); // a paint is never handed out twice at once
            if (depth < 12)
                Nest(depth + 1);
        }
        Nest(1);
        Assert.Equal(12, seen.Count);
        using var after = SkUiScratchPaint.Rent(); // the stack unwound: the first paint again
        Assert.Contains(after.Paint, seen);
    }
}
