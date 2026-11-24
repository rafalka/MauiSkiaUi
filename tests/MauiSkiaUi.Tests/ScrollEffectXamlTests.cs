using MauiSkiaUi.Rendering;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>1.16 phase B: scroll effects written as the docs' recipes write them in XAML.</summary>
[Collection(RuntimeXamlCollection.Name)]
public class ScrollEffectXamlTests
{
    [Fact]
    public void TheRecipesXamlLoadsAndDrives()
    {
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                         xmlns:skui="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <ContentView.Resources>
                <skui:SkUiScrollReveal x:Key="FabReveal" Source="{x:Reference List}" Response="Triggered" />
              </ContentView.Resources>
              <skui:SkUiGrid>
                <skui:SkUiScrollView x:Name="List">
                  <skui:SkUiBox HeightRequest="4000" />
                </skui:SkUiScrollView>
                <skui:SkUiBox x:Name="Hero" HeightRequest="240" VerticalOptions="Start">
                  <skui:SkUiView.ScrollEffects>
                    <skui:SkUiScrollEffect Source="{x:Reference List}" Property="ClipHeight" InputStart="0" InputEnd="184" From="240" To="56" />
                    <skui:SkUiScrollEffect Source="{x:Reference List}" Property="Scale" InputStart="-200" InputEnd="0" From="1.5" To="1" ClipEdge="End" />
                  </skui:SkUiView.ScrollEffects>
                </skui:SkUiBox>
                <skui:SkUiBox x:Name="Bar" HeightRequest="56" VerticalOptions="Start">
                  <skui:SkUiView.ScrollEffects>
                    <skui:SkUiScrollEffect Property="ClipHeight" From="56" To="0" ClipShape="Capsule">
                      <skui:SkUiScrollEffect.Reveal>
                        <skui:SkUiScrollReveal Source="{x:Reference List}" Distance="64" />
                      </skui:SkUiScrollEffect.Reveal>
                    </skui:SkUiScrollEffect>
                  </skui:SkUiView.ScrollEffects>
                </skui:SkUiBox>
                <skui:SkUiButton x:Name="Fab" Text="+" WidthRequest="56" HeightRequest="56" HorizontalOptions="End" VerticalOptions="End">
                  <skui:SkUiView.ScrollEffects>
                    <skui:SkUiScrollEffect Property="TranslationY" From="0" To="88" Reveal="{StaticResource FabReveal}" />
                    <skui:SkUiScrollEffect Property="Opacity" From="1" To="0" Reveal="{StaticResource FabReveal}" />
                  </skui:SkUiView.ScrollEffects>
                </skui:SkUiButton>
              </skui:SkUiGrid>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var grid = (SkUiGrid)root.Content;
        var list = (SkUiScrollView)grid.Children[0];
        var hero = (SkUiBox)grid.Children[1];
        var bar = (SkUiBox)grid.Children[2];
        var fab = (SkUiButton)grid.Children[3];
        Assert.Equal(2, hero.ScrollEffects.Count);
        Assert.Same(list, hero.ScrollEffects[0].Source);
        Assert.Equal(SkUiClipEdge.End, hero.ScrollEffects[1].ClipEdge);
        Assert.Equal(64, bar.ScrollEffects[0].Reveal!.Distance);
        Assert.Same(fab.ScrollEffects[0].Reveal, fab.ScrollEffects[1].Reveal);
        Assert.Equal(SkUiScrollRevealResponse.Triggered, fab.ScrollEffects[0].Reveal!.Response);

        using var surface = new SkUiTestSurface(grid, 200, 400);
        surface.Frame(0);
        list.ScrollTo(0, 92);
        surface.Frame(10);
        var props = SkUiRenderProps.Default;
        ((ISkUiRenderable)hero).GetRenderProps(ref props);
        Assert.True(props.TryGetLinkClip(out var clip));
        Assert.Equal(148, clip.Rect.Height, 0.01); // halfway from 240 to 56
        props = SkUiRenderProps.Default;
        ((ISkUiRenderable)fab).GetRenderProps(ref props);
        Assert.Equal(2, props.Links!.Length); // the button's effects resolved through the shared reveal
    }
}
