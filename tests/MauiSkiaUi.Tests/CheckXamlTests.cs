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
    public void ReportsOnlyTheGestureRecognizersDrawnViewsDoNotRun()
    {
        var project = Directory.CreateTempSubdirectory("skiaui-check-xaml");
        try
        {
            File.WriteAllText(Path.Combine(project.FullName, "Page.xaml"), """
                <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                             xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
                  <sk:SkUiGrid>
                    <sk:SkUiGrid.GestureRecognizers>
                      <TapGestureRecognizer NumberOfTapsRequired="3" Buttons="Secondary" />
                      <SwipeGestureRecognizer Direction="Left" />
                      <PanGestureRecognizer />
                      <PinchGestureRecognizer />
                      <PointerGestureRecognizer />
                      <PanGestureRecognizer TouchPoints="2" />
                      <DragGestureRecognizer />
                    </sk:SkUiGrid.GestureRecognizers>
                  </sk:SkUiGrid>
                </ContentPage>
                """);
            var (exitCode, output) = Run(project.FullName);
            Assert.Equal(1, exitCode);
            var errors = output.Split('\n').Where(line => line.Contains("error:")).ToArray();
            Assert.Equal(2, errors.Length);
            Assert.Contains(errors, error => error.Contains("Page.xaml:10:") && error.Contains("TouchPoints=2"));
            Assert.Contains(errors, error => error.Contains("Page.xaml:11:") && error.Contains("DragGestureRecognizer"));
        }
        finally
        {
            project.Delete(recursive: true);
        }
    }

    [Fact]
    public void ReportsMauiOnlyCarouselMembersAndSuggestsTheDrawnCarousel()
    {
        var project = Directory.CreateTempSubdirectory("skiaui-check-xaml");
        try
        {
            File.WriteAllText(Path.Combine(project.FullName, "Page.xaml"), """
                <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                             xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
                  <sk:SkUiGrid>
                    <sk:SkUiCarouselView ItemsSource="{Binding Photos}" Loop="False" ItemsUpdatingScrollMode="KeepLastItemInView">
                      <sk:SkUiCarouselView.ItemsLayout>
                        <LinearItemsLayout Orientation="Horizontal" ItemSpacing="12" />
                      </sk:SkUiCarouselView.ItemsLayout>
                    </sk:SkUiCarouselView>
                    <sk:SkUiIndicatorView IndicatorTemplate="{StaticResource Dot}" />
                    <CarouselView />
                  </sk:SkUiGrid>
                </ContentPage>
                """);
            var (exitCode, output) = Run(project.FullName);
            Assert.Equal(1, exitCode);
            var lines = output.Split('\n');
            // ItemsLayout, ItemsUpdatingScrollMode and IndicatorTemplate port as they are (1.2): only the native carousel
            // inside the drawn tree is reported, and the drawn one replaces it.
            var error = Assert.Single(lines, line => line.Contains("error:"));
            Assert.Contains("Page.xaml:10:", error);
            Assert.Contains("SkUiCarouselView", error);
        }
        finally
        {
            project.Delete(recursive: true);
        }
    }

    [Fact]
    public void SuggestsTheDrawnPickerAndChecksItsRows()
    {
        var project = Directory.CreateTempSubdirectory("skiaui-check-xaml");
        try
        {
            File.WriteAllText(Path.Combine(project.FullName, "Page.xaml"), """
                <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                             xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
                  <sk:SkUiVerticalStackLayout>
                    <Picker ItemsSource="{Binding Stores}" />
                    <sk:SkUiPicker ItemsSource="{Binding Paints}">
                      <sk:SkUiPicker.ItemTemplate>
                        <DataTemplate>
                          <Label Text="{Binding Name}" />
                        </DataTemplate>
                      </sk:SkUiPicker.ItemTemplate>
                    </sk:SkUiPicker>
                    <sk:SkUiMauiContentView>
                      <DatePicker />
                    </sk:SkUiMauiContentView>
                    <TimePicker Time="{Binding Alarm}" />
                  </sk:SkUiVerticalStackLayout>
                </ContentPage>
                """);
            var (exitCode, output) = Run(project.FullName);
            Assert.Equal(1, exitCode);
            var errors = output.Split('\n').Where(line => line.Contains("error:")).ToArray();
            Assert.Equal(3, errors.Length);
            // A wrapped native date picker is allowed; a bare time picker: the drawn one replaces it.
            Assert.Contains(errors, error => error.Contains("Page.xaml:15:") && error.Contains("SkUiTimePicker"));
            // The native picker: the drawn one replaces it (and needs a presenter); a native row in the drawn list's template.
            Assert.Contains(errors, error => error.Contains("Page.xaml:4:") && error.Contains("SkUiPicker") && error.Contains("PopupPresenter"));
            Assert.Contains(errors, error => error.Contains("Page.xaml:8:") && error.Contains("SkUiLabel"));
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
                    <sk:SkUiCollectionView ItemsSource="{Binding Orders}" IsGrouped="True" Header="Orders" Footer="{Binding Summary}"
                                           SelectionMode="Multiple" ItemsLayout="VerticalGrid, 2" CanReorderItems="True" />
                    <CollectionView />
                  </sk:SkUiGrid>
                </ContentPage>
                """);
            var (exitCode, output) = Run(project.FullName);
            Assert.Equal(1, exitCode);
            var errors = output.Split('\n').Where(line => line.Contains("error:")).ToArray();
            Assert.Equal(2, errors.Length); // grouping, multiple selection, ItemsLayout and a string header port as is
            Assert.Contains(errors, error => error.Contains("CanReorderItems"));
            Assert.Contains(errors, error => error.Contains("Page.xaml:6:") && error.Contains("sk:SkUiCollectionView"));
            // A data footer converts to a template bound through the list's context.
            Assert.Contains(output.Split('\n'), line => line.Contains("warning:") && line.Contains("Footer=\"{Binding Summary}\"") && line.Contains("{Binding Info.Title}"));
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

    [Fact]
    public void SuggestsTheDrawnAvatarForTheToolkitsAvatar()
    {
        var project = Directory.CreateTempSubdirectory("skiaui-check-xaml");
        try
        {
            File.WriteAllText(Path.Combine(project.FullName, "Page.xaml"), """
                <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                             xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi"
                             xmlns:mct="http://schemas.microsoft.com/dotnet/2022/maui/toolkit">
                  <sk:SkUiVerticalStackLayout>
                    <mct:AvatarView Text="ZS" />
                    <sk:SkUiAvatarView Text="ZS" />
                  </sk:SkUiVerticalStackLayout>
                  <mct:AvatarView Text="outside the drawn tree" />
                </ContentPage>
                """);
            var (exitCode, output) = Run(project.FullName);
            Assert.Equal(1, exitCode);
            var errors = output.Split('\n').Where(line => line.Contains("error:")).ToArray();
            var error = Assert.Single(errors);
            Assert.Contains("Page.xaml:5:", error);
            Assert.Contains("sk:SkUiAvatarView", error);
        }
        finally
        {
            project.Delete(recursive: true);
        }
    }

    [Fact]
    public void FixRewritesTemplateBindingsOfDrawnControls()
    {
        var project = Directory.CreateTempSubdirectory("skiaui-check-xaml");
        try
        {
            File.WriteAllText(Path.Combine(project.FullName, "CardView.cs"), "public class CardView : SkUiContentView { }");
            var page = Path.Combine(project.FullName, "Page.xaml");
            File.WriteAllText(page, """
                <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                             xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi"
                             xmlns:local="clr-namespace:App">
                  <ContentPage.Resources>
                    <Style TargetType="local:CardView">
                      <Setter Property="ControlTemplate">
                        <ControlTemplate>
                          <sk:SkUiBorder Stroke="{TemplateBinding BorderColor}">
                            <sk:SkUiLabel Text="{Binding Title, Source={RelativeSource TemplatedParent}, StringFormat='{0}!'}" />
                          </sk:SkUiBorder>
                        </ControlTemplate>
                      </Setter>
                    </Style>
                    <ControlTemplate x:Key="Native" TargetType="ContentView">
                      <Label Text="{TemplateBinding Title}" />
                    </ControlTemplate>
                  </ContentPage.Resources>
                  <sk:SkUiVerticalStackLayout>
                    <local:CardView Title="Hello" />
                  </sk:SkUiVerticalStackLayout>
                </ContentPage>
                """);
            var (exitCode, output) = Run(project.FullName, "--fix");
            Assert.Equal(0, exitCode);
            Assert.Equal(2, output.Split('\n').Count(line => line.Contains("fixed:")));
            var fixedXaml = File.ReadAllText(page);
            Assert.Contains("Stroke=\"{Binding BorderColor, Source={RelativeSource AncestorType={x:Type local:CardView}}}\"", fixedXaml);
            Assert.Contains("Text=\"{Binding Title, Source={RelativeSource AncestorType={x:Type local:CardView}}, StringFormat='{0}!'}\"", fixedXaml);
            Assert.Contains("""<Label Text="{TemplateBinding Title}" />""", fixedXaml); // a native control's template: MAUI resolves it
            Assert.Equal(0, Run(project.FullName).ExitCode); // nothing left to report
        }
        finally
        {
            project.Delete(recursive: true);
        }
    }

    [Fact]
    public void FixBindsADataHeadersTemplateThroughTheListsContext()
    {
        var project = Directory.CreateTempSubdirectory("skiaui-check-xaml");
        try
        {
            var page = Path.Combine(project.FullName, "Page.xaml");
            File.WriteAllText(page, """
                <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                             xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
                  <sk:SkUiCollectionView ItemsSource="{Binding Monkeys}"
                                         Header="{Binding Info}">
                    <sk:SkUiCollectionView.HeaderTemplate>
                      <DataTemplate>
                        <sk:SkUiVerticalStackLayout BindableLayout.ItemsSource="{Binding Tags}">
                          <sk:SkUiLabel Text="{Binding Title}" TextColor="{Binding Color, Source={StaticResource Theme}}" />
                          <sk:SkUiLabel Text="{Binding}" />
                          <BindableLayout.ItemTemplate>
                            <DataTemplate><sk:SkUiLabel Text="{Binding Name}" /></DataTemplate>
                          </BindableLayout.ItemTemplate>
                        </sk:SkUiVerticalStackLayout>
                      </DataTemplate>
                    </sk:SkUiCollectionView.HeaderTemplate>
                  </sk:SkUiCollectionView>
                </ContentPage>
                """);
            var (_, output) = Run(project.FullName, "--fix");
            Assert.Contains(output.Split('\n'), line => line.Contains("fixed:") && line.Contains("3 binding(s)"));
            var fixedXaml = File.ReadAllText(page);
            Assert.DoesNotContain("Header=", fixedXaml);
            Assert.Contains("BindableLayout.ItemsSource=\"{Binding Info.Tags}\"", fixedXaml);
            Assert.Contains("""<sk:SkUiLabel Text="{Binding Info.Title}" TextColor="{Binding Color, Source={StaticResource Theme}}" />""", fixedXaml);
            Assert.Contains("""<sk:SkUiLabel Text="{Binding Info}" />""", fixedXaml);
            Assert.Contains("""<sk:SkUiLabel Text="{Binding Name}" />""", fixedXaml); // the nested template binds to its own items
        }
        finally
        {
            project.Delete(recursive: true);
        }
    }

    [Fact]
    public void FixKeepsTheFilesEncodingAndLineEndingsAndOnlyReadsPathEqualsAsANamedPath()
    {
        var project = Directory.CreateTempSubdirectory("skiaui-check-xaml");
        try
        {
            var page = Path.Combine(project.FullName, "Page.xaml");
            var xaml = """
                <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                             xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
                  <sk:SkUiCollectionView ItemsSource="{Binding Monkeys}" Header="{Binding Info}">
                    <sk:SkUiCollectionView.HeaderTemplate>
                      <DataTemplate>
                        <sk:SkUiVerticalStackLayout>
                          <sk:SkUiLabel Text="{Binding PathName}" />
                          <sk:SkUiLabel Text="{Binding Path=Title}" />
                        </sk:SkUiVerticalStackLayout>
                      </DataTemplate>
                    </sk:SkUiCollectionView.HeaderTemplate>
                  </sk:SkUiCollectionView>
                </ContentPage>
                """.ReplaceLineEndings("\r\n");
            File.WriteAllText(page, xaml, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            Run(project.FullName, "--fix");
            var bytes = File.ReadAllBytes(page);
            Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
            var fixedXaml = System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            Assert.Equal(xaml.Split("\r\n").Length, fixedXaml.Split("\r\n").Length);
            Assert.DoesNotContain("\n", fixedXaml.Replace("\r\n", ""));
            Assert.Contains("""<sk:SkUiLabel Text="{Binding Info.PathName}" />""", fixedXaml);
            Assert.Contains("""<sk:SkUiLabel Text="{Binding Path=Info.Title}" />""", fixedXaml);
        }
        finally
        {
            project.Delete(recursive: true);
        }
    }

    [Fact]
    public void FixConvertsPlatformSpecificsWithADrawnMemberAndReportsTheRest()
    {
        var project = Directory.CreateTempSubdirectory("skiaui-check-xaml");
        try
        {
            var page = Path.Combine(project.FullName, "Page.xaml");
            File.WriteAllText(page, """
                <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                             xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi"
                             xmlns:ios="clr-namespace:Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;assembly=Microsoft.Maui.Controls"
                             xmlns:android="clr-namespace:Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;assembly=Microsoft.Maui.Controls">
                  <sk:SkUiVerticalStackLayout>
                    <sk:SkUiEntry ios:Entry.CursorColor="Red" android:Entry.ImeOptions="Search" />
                    <sk:SkUiPicker ios:Picker.UpdateMode="WhenFinished" />
                    <sk:SkUiEntry ios:Entry.AdjustsFontSizeToFitWidth="True" android:Entry.ImeOptions="Previous" />
                    <sk:SkUiSlider ios:Slider.UpdateOnTap="True" />
                  </sk:SkUiVerticalStackLayout>
                </ContentPage>
                """);
            Assert.Equal(1, Run(project.FullName).ExitCode); // the convertible ones are errors until converted
            var (exitCode, output) = Run(project.FullName, "--fix");
            Assert.Equal(0, exitCode);
            var lines = output.Split('\n');
            Assert.Equal(3, lines.Count(line => line.Contains("fixed:")));
            Assert.Equal(3, lines.Count(line => line.Contains("warning:") && line.Contains("ignored")));
            var fixedXaml = File.ReadAllText(page);
            Assert.Contains("""<sk:SkUiEntry CaretColor="{OnPlatform iOS=Red, MacCatalyst=Red}" ReturnType="{OnPlatform Android=Search}" />""", fixedXaml);
            Assert.Contains("""<sk:SkUiPicker UpdateMode="{OnPlatform iOS=WhenFinished, MacCatalyst=WhenFinished}" />""", fixedXaml);

            File.WriteAllText(page, File.ReadAllText(page).Replace("CaretColor=\"{OnPlatform iOS=Red, MacCatalyst=Red}\"", "ios:Entry.CursorColor=\"Red\""));
            Run(project.FullName, "--fix", "--all-platforms");
            Assert.Contains("CaretColor=\"Red\"", File.ReadAllText(page));
        }
        finally
        {
            project.Delete(recursive: true);
        }
    }

    private static (int ExitCode, string Output) Run(string project, params string[] options)
    {
        var start = new ProcessStartInfo("python3") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { Script(), "--root", project }.Concat(options).Append(project))
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
