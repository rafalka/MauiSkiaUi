using MauiSkiaUiSamples.Samples.Controls;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The samples app's "Loading in stages" (skeleton from the content, C9b) with its requests ended one by one: one label until
/// Load, then the page as
/// bones with placeholder rows, then rows with names and their details as bones, then each row's details as its request ends.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class StagedLoadingSampleTests
{
    private static IEnumerable<ISkUiView> Descendants(SkUiView view)
    {
        foreach (var child in view.SkiaChildren)
        {
            yield return child;
            if (child is SkUiView node)
                foreach (var descendant in Descendants(node))
                    yield return descendant;
        }
    }

    [Fact]
    public void ThePageAndThenEachRowAreTheirOwnSkeletonUntilTheirRequestsEnd()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        using var ui = TestUiContext.Install(); // the model's continuations run between the frames this test drives
        var requests = new List<TaskCompletionSource>();
        var model = new StagedLoadingModel((_, _) =>
        {
            var request = new TaskCompletionSource();
            requests.Add(request);
            return request.Task;
        });
        var page = new StagedLoadingSample(model);
        var grid = (SkUiGrid)page.SampleContent!;
        var shimmer = grid.Children.OfType<SkUiShimmer>().Single();
        grid.Children.Remove(shimmer);
        shimmer.BindingContext = model; // out of the page: no context inherited
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = shimmer }, 380, 700);
        surface.Frame();

        // Before Load: nothing requested, the page hidden behind one label.
        var idle = grid.Children.OfType<SkUiLabel>().Single(label => label.IsVisible);
        Assert.True(model.IsIdle);
        Assert.False(shimmer.IsVisible);
        Assert.Empty(requests);

        // The page's switches fade in on the render thread: frames until they end.
        var time = 0d;
        void Settle()
        {
            for (var frame = 0; frame < 100 && (frame == 0 || shimmer.IsSwitching); frame++)
            {
                surface.Frame(time += 50);
                ui.RunPending();
            }
            Assert.False(shimmer.IsSwitching);
        }

        var loading = model.LoadAsync();
        Assert.False(idle.IsVisible);
        Assert.True(shimmer.IsVisible);
        surface.Frame();
        SkUiTestHelpers.Arrange(surface.Root, 380, 700);
        surface.Frame();

        // Stage 1: the team request. The page is bones; the empty list shows placeholder rows of its template.
        var list = Descendants(shimmer).OfType<SkUiCollectionView>().Single();
        Assert.True(shimmer.ShowsContentAsSkeleton);
        Assert.True(list.ShowsPlaceholders);
        Assert.True(list.PlaceholderItems.Count >= 3);
        Assert.Single(requests);

        // Stage 2: the members arrive. The page is itself; each row's details are bones, a few requested at a time.
        requests[0].SetResult();
        ui.RunPending();
        Settle();
        Assert.False(shimmer.IsShowingSkeleton);
        Assert.False(list.ShowsPlaceholders);
        Assert.Equal(1 + StagedLoadingModel.ParallelRequests, requests.Count);
        var rows = Descendants(list).OfType<SkUiShimmer>().ToList();
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.True(row.ShowsContentAsSkeleton));
        Assert.Contains(Descendants(list).OfType<SkUiLabel>(), label => label.Text == model.Members[0].Name);

        // Stage 3: one row's details arrive; the next request starts.
        requests[1].SetResult();
        // The semaphore hands the slot on through the thread pool: the next request starts a moment later.
        Assert.True(SpinWait.SpinUntil(() =>
        {
            ui.RunPending();
            return requests.Count == 2 + StagedLoadingModel.ParallelRequests;
        }, TimeSpan.FromSeconds(5)));
        surface.Frame();
        Assert.False(model.Members[0].IsLoadingDetails);
        var first = rows.Single(row => ReferenceEquals(row.BindingContext, model.Members[0]));
        Assert.False(first.ShowsContentAsSkeleton);
        Assert.True(rows.Single(row => ReferenceEquals(row.BindingContext, model.Members[1])).ShowsContentAsSkeleton);

        var ended = 2;
        Assert.True(SpinWait.SpinUntil(() =>
        {
            while (ended < requests.Count)
                requests[ended++].SetResult();
            ui.RunPending();
            return loading.IsCompleted;
        }, TimeSpan.FromSeconds(5)));
        Assert.True(loading.IsCompletedSuccessfully);
        surface.Frame();
        Assert.Equal(1 + model.Members.Count, requests.Count);
        Assert.All(Descendants(list).OfType<SkUiShimmer>(), row => Assert.False(row.ShowsContentAsSkeleton));
        Assert.Equal("Loaded", model.Status);
    }
}
