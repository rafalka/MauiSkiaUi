using System.Diagnostics;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>The migration skill's XAML checker (plugins/skiaui-migration/.../check_xaml.py).</summary>
public class CheckXamlTests
{
    [Fact]
    public void ReportsTemplateBindingsOnCustomDrawnControls()
    {
        var project = Directory.CreateTempSubdirectory("skiaui-check-xaml");
        try
        {
            File.WriteAllText(Path.Combine(project.FullName, "CardView.cs"), "public class CardView : SkUiContentView { }");
            File.WriteAllText(Path.Combine(project.FullName, "Page.xaml"), """
                <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                             xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi"
                             xmlns:local="clr-namespace:App">
                  <sk:SkUiVerticalStackLayout>
                    <local:CardView Title="{TemplateBinding Title}" />
                    <local:CardView Title="{Binding Title, Source={RelativeSource AncestorType={x:Type local:CardView}}}" />
                  </sk:SkUiVerticalStackLayout>
                </ContentPage>
                """);
            var (exitCode, output) = Run(project.FullName);
            Assert.Equal(1, exitCode);
            var error = Assert.Single(output.Split('\n'), line => line.Contains("error:"));
            Assert.Contains("Page.xaml:5:", error);
            Assert.Contains("TemplateBinding", error);
        }
        finally
        {
            project.Delete(recursive: true);
        }
    }

    [Fact]
    public void ReportsMauiOnlyCollectionViewMembersAndNativeLists()
    {
        var project = Directory.CreateTempSubdirectory("skiaui-check-xaml");
        try
        {
            File.WriteAllText(Path.Combine(project.FullName, "Page.xaml"), """
                <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                             xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
                  <sk:SkUiGrid>
                    <sk:SkUiCollectionView ItemsSource="{Binding Orders}" IsGrouped="True" Header="Orders"
                                           SelectionMode="Multiple" ItemsLayout="VerticalGrid, 2" CanReorderItems="True" />
                    <CollectionView />
                  </sk:SkUiGrid>
                </ContentPage>
                """);
            var (exitCode, output) = Run(project.FullName);
            Assert.Equal(1, exitCode);
            var errors = output.Split('\n').Where(line => line.Contains("error:")).ToArray();
            Assert.Equal(4, errors.Length); // grouping and multiple selection are supported
            Assert.Contains(errors, error => error.Contains("ItemsLayout") && error.Contains("Span="));
            Assert.Contains(errors, error => error.Contains("CanReorderItems"));
            Assert.Contains(errors, error => error.Contains("Header=\"Orders\"") && error.Contains("<sk:SkUiCollectionView.Header>"));
            Assert.Contains(errors, error => error.Contains("Page.xaml:6:") && error.Contains("sk:SkUiCollectionView"));
        }
        finally
        {
            project.Delete(recursive: true);
        }
    }

    [Fact]
    public void AcceptsMauiSwipeItemsInSwipeViewsAndReportsNativeItemViews()
    {
        var project = Directory.CreateTempSubdirectory("skiaui-check-xaml");
        try
        {
            File.WriteAllText(Path.Combine(project.FullName, "Page.xaml"), """
                <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                             xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
                  <sk:SkUiVerticalStackLayout>
                    <sk:SkUiSwipeView>
                      <sk:SkUiSwipeView.LeftItems>
                        <SwipeItems Mode="Execute">
                          <SwipeItem Text="Delete" BackgroundColor="Red" />
                          <sk:SkUiSwipeItemView>
                            <sk:SkUiLabel Text="Drawn" />
                          </sk:SkUiSwipeItemView>
                          <SwipeItemView>
                            <Label Text="Native" />
                          </SwipeItemView>
                        </SwipeItems>
                      </sk:SkUiSwipeView.LeftItems>
                      <sk:SkUiSwipeView.RightItems>
                        <SwipeItems>
                          <sk:SkUiSwipeItemView>
                            <Label Text="Native label" />
                          </sk:SkUiSwipeItemView>
                        </SwipeItems>
                      </sk:SkUiSwipeView.RightItems>
                      <sk:SkUiLabel Text="Row" />
                    </sk:SkUiSwipeView>
                    <SwipeView />
                  </sk:SkUiVerticalStackLayout>
                </ContentPage>
                """);
            var (exitCode, output) = Run(project.FullName);
            Assert.Equal(1, exitCode);
            Assert.DoesNotContain("warning:", output); // drawn item views are not surfaces of their own
            var errors = output.Split('\n').Where(line => line.Contains("error:")).ToArray();
            Assert.Equal(3, errors.Length);
            Assert.Contains(errors, error => error.Contains("Page.xaml:11:") && error.Contains("sk:SkUiSwipeItemView"));
            Assert.Contains(errors, error => error.Contains("Page.xaml:19:") && error.Contains("SkUiLabel"));
            Assert.Contains(errors, error => error.Contains("Page.xaml:25:") && error.Contains("SkUiSwipeView"));
        }
        finally
        {
            project.Delete(recursive: true);
        }
    }

    [Fact]
    public void MapsRefreshViewsAndReportsAListRefreshingInsideOne()
    {
        var project = Directory.CreateTempSubdirectory("skiaui-check-xaml");
        try
        {
            File.WriteAllText(Path.Combine(project.FullName, "Page.xaml"), """
                <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                             xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
                  <sk:SkUiVerticalStackLayout>
                    <sk:SkUiRefreshView Command="{Binding Refresh}">
                      <sk:SkUiScrollView>
                        <sk:SkUiLabel Text="Feed" />
                      </sk:SkUiScrollView>
                    </sk:SkUiRefreshView>
                    <sk:SkUiRefreshView>
                      <sk:SkUiCollectionView IsPullToRefreshEnabled="True" />
                    </sk:SkUiRefreshView>
                    <sk:SkUiRefreshView>
                      <sk:SkUiCollectionView IsPullToRefreshEnabled="False" />
                    </sk:SkUiRefreshView>
                    <sk:SkUiCollectionView IsPullToRefreshEnabled="True" />
                    <RefreshView />
                  </sk:SkUiVerticalStackLayout>
                </ContentPage>
                """);
            var (exitCode, output) = Run(project.FullName);
            Assert.Equal(1, exitCode);
            var errors = output.Split('\n').Where(line => line.Contains("error:")).ToArray();
            Assert.Equal(2, errors.Length);
            Assert.Contains(errors, error => error.Contains("Page.xaml:10:") && error.Contains("both would refresh"));
            Assert.Contains(errors, error => error.Contains("Page.xaml:16:") && error.Contains("SkUiRefreshView"));
        }
        finally
        {
            project.Delete(recursive: true);
        }
    }

    private static (int ExitCode, string Output) Run(string project)
    {
        var start = new ProcessStartInfo("python3") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { Script(), "--root", project, project })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    private static string Script()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var script = Path.Combine(directory.FullName, "plugins", "skiaui-migration", "skills", "skiaui-migrate", "scripts", "check_xaml.py");
            if (File.Exists(script))
                return script;
        }
        throw new FileNotFoundException("check_xaml.py not found above the test output folder.");
    }
}
