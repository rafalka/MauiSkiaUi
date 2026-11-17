using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace MauiSkiaUi.Tests;

/// <summary>A memory-cache hit allocates no task and no load context (NFR-2).</summary>
[Collection(GlobalStateCollection.Name)]
public sealed class ImageAllocationTests(ITestOutputHelper output) : IDisposable
{
    private readonly long _memoryBudget = SkUiImageCache.MemoryCacheMaxBytes;

    public void Dispose()
    {
        SkUiImageCache.ClearMemoryCache();
        SkUiImageCache.MemoryCacheMaxBytes = _memoryBudget;
    }

    private static SkUiImageSource Source(byte[] png, string key) =>
        SkUiImageSource.FromStream(_ => Task.FromResult<Stream>(new MemoryStream(png)), key);

    [Fact]
    public async Task LoadsOfCachedImagesThroughASlotAllocateABoundedAmount()
    {
        SkUiImageCache.MemoryCacheMaxBytes = 64L * 1024 * 1024;
        SkUiImageCache.ClearMemoryCache();
        var red = Source(ImageLoadingTests.Png(SKColors.Red), "alloc-red");
        var blue = Source(ImageLoadingTests.Png(SKColors.Blue), "alloc-blue");
        var image = new SkUiCoreImage().SetSource(red);
        await image.LoadingTask;
        image.SetSource(blue);
        await image.LoadingTask;
        Assert.Equal(2, SkUiImageCache.MemoryCacheCount);

        void Step(int round)
        {
            image.SetSource(round % 2 == 0 ? red : blue);
            Assert.True(image.LoadingTask.IsCompleted); // a hit completes synchronously
        }

        for (var round = 0; round < 20; round++) Step(round);
        var fewest = long.MaxValue;
        for (var batch = 0; batch < 3; batch++)
        {
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            for (var round = 0; round < 100; round++) Step(round);
            fewest = Math.Min(fewest, GC.GetAllocatedBytesForCurrentThread() - allocated);
        }
        output.WriteLine($"{fewest / 100} bytes per cached load");

        Assert.True(fewest / 100 <= BytesPerCachedLoad, $"{fewest / 100} bytes per cached load");
        Assert.Equal(2, SkUiImageCache.MemoryCacheCount);
        Assert.NotNull(image.CachedImage);
        image.Clear();
    }

    /// <summary>
    /// What a cached load through a Core image still allocates: the slot's cancellation source, the memory-cache key (a
    /// string built from the source key and the decode options) and the image's and slot's own bookkeeping. 736 bytes at
    /// 1.13.0, when a hit also allocated a load context, a completed task and the task the origin was copied into.
    /// </summary>
    private const long BytesPerCachedLoad = 520;
}
