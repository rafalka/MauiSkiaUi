using Microsoft.Maui.Controls.Shapes;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// C8: the Community Toolkit's <c>AvatarView</c> samples (its docs and its sample app's styles) load with only the prefix
/// changed, and style setters reach the properties the toolkit's constructor would have set locally.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class AvatarViewXamlTests
{
    [Fact]
    public void TheToolkitsDocsSamplesLoadWithThePrefixChanged()
    {
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:toolkit="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <VerticalStackLayout>
                <toolkit:SkUiAvatarView Text="ZS" />
                <toolkit:SkUiAvatarView BackgroundColor="Red" BorderColor="Blue" BorderWidth="2" Text="ZS" TextColor="Green" />
                <toolkit:SkUiAvatarView CornerRadius="8, 12, 16, 20" HeightRequest="48" Text="ZS" WidthRequest="48" />
                <toolkit:SkUiAvatarView ImageSource="dotnet_bot.png" Padding="2" Text="ZS" />
                <toolkit:SkUiAvatarView FontAttributes="Bold" FontFamily="OpenSansRegular" FontSize="20" CharacterSpacing="2" TextTransform="Uppercase" Text="zs" />
                <toolkit:SkUiAvatarView Text="ZS">
                  <toolkit:SkUiAvatarView.Shadow>
                    <Shadow Brush="Black" Offset="20,20" Radius="40" Opacity="0.8" />
                  </toolkit:SkUiAvatarView.Shadow>
                </toolkit:SkUiAvatarView>
                <toolkit:SkUiAvatarView StrokeShape="RoundRectangle 8" Stroke="Orange" StrokeThickness="3" Text="ZS" />
                <toolkit:SkUiAvatarView Text="Ada Lovelace" Initials="FirstWord" AvatarColorMode="DarkBackground" BadgeColor="LimeGreen" BadgePosition="TopRight" />
              </VerticalStackLayout>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var avatars = ((VerticalStackLayout)root.Content).Children.Cast<SkUiAvatarView>().ToList();
        Assert.Equal("ZS", avatars[0].Text);
        Assert.Equal((Colors.Red, Colors.Blue, 2d, Colors.Green), (avatars[1].BackgroundColor, avatars[1].BorderColor, avatars[1].BorderWidth, avatars[1].TextColor));
        Assert.Equal(new CornerRadius(8, 12, 16, 20), avatars[2].CornerRadius);
        Assert.Equal(("dotnet_bot.png", new Thickness(2)), (((FileImageSource)avatars[3].ImageSource!).File, avatars[3].Padding));
        Assert.Equal((FontAttributes.Bold, "OpenSansRegular", 2d, "ZS"), (avatars[4].FontAttributes, avatars[4].FontFamily, avatars[4].CharacterSpacing, avatars[4].DisplayText));
        Assert.True(avatars[4].FontSize > 0);
        Assert.NotNull(avatars[5].Shadow);
        Assert.Equal(3d, avatars[6].StrokeThickness);
        Assert.IsType<RoundRectangle>(avatars[6].StrokeShape);
        Assert.Equal(("A", SkUiAvatarColorMode.DarkBackground, Colors.LimeGreen, SkUiAvatarBadgePosition.TopRight),
            (avatars[7].DisplayText, avatars[7].AvatarColorMode, avatars[7].BadgeColor, avatars[7].BadgePosition));
        Assert.Equal(new Size(48, 48), ((IView)avatars[0]).Measure(double.PositiveInfinity, double.PositiveInfinity));
    }

    [Fact]
    public void StylesSetWhatTheToolkitsConstructorSets()
    {
        // As the toolkit's sample pages: sizes, corner radius, padding and stroke from a style.
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                         xmlns:mct="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <ContentView.Resources>
                <Style x:Key="Base" TargetType="mct:SkUiAvatarView">
                  <Setter Property="BackgroundColor" Value="Black" />
                  <Setter Property="Text" Value="ZS" />
                  <Setter Property="TextColor" Value="White" />
                  <Setter Property="BorderColor" Value="White" />
                </Style>
                <Style x:Key="Pill" BasedOn="{StaticResource Base}" TargetType="mct:SkUiAvatarView">
                  <Setter Property="WidthRequest" Value="32" />
                  <Setter Property="HeightRequest" Value="48" />
                  <Setter Property="CornerRadius" Value="24" />
                  <Setter Property="Padding" Value="0" />
                  <Setter Property="StrokeThickness" Value="4" />
                </Style>
              </ContentView.Resources>
              <mct:SkUiAvatarView Style="{StaticResource Pill}" />
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var avatar = (SkUiAvatarView)root.Content;
        Assert.Equal(new Size(32, 48), ((IView)avatar).Measure(double.PositiveInfinity, double.PositiveInfinity));
        Assert.Equal((new CornerRadius(24), new Thickness(0), 4d), (avatar.CornerRadius, avatar.Padding, avatar.StrokeThickness));
        SkUiTestHelpers.Arrange(avatar, 32, 48);
        using var bitmap = new SkiaSharp.SKBitmap(32, 48);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        avatar.Paint(canvas);
        Assert.Equal(SkiaSharp.SKColors.White, bitmap.GetPixel(16, 1)); // the 4-wide stroke from the style, not the 1-wide default
        Assert.Equal(SkiaSharp.SKColors.Black, bitmap.GetPixel(16, 5));
    }
}
