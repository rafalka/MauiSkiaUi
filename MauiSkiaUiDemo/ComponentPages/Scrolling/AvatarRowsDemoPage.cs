using CommunityToolkit.Maui.Views;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// A contact list: <see cref="SkUiAvatarView"/>s in the rows of an <see cref="SkUiCollectionView"/> next to the Community
/// Toolkit's <c>AvatarView</c>s in a <see cref="CollectionView"/>. Rows recycle as the list scrolls: a new name or photo
/// re-records only the avatar, and the repeated photos decode once (the shared image cache).
/// </summary>
public sealed class AvatarRowsDemoPage : ComponentDemoPage
{
    private static readonly string[] FirstNames = ["Ada", "Grace", "Alan", "Edsger", "Barbara", "Donald", "Margaret", "Linus", "Frances", "Ken", "Radia", "Tim"];
    private static readonly string[] LastNames = ["Lovelace", "Hopper", "Turing", "Dijkstra", "Liskov", "Knuth", "Hamilton", "Torvalds", "Allen", "Thompson", "Perlman", "Berners-Lee"];

    public AvatarRowsDemoPage()
        : base("SkUiAvatarView rows", new SkUiCollectionView(), new CollectionView(), widthRange: (200, 400, 300), heightRange: (160, 600, 400))
    {
        var skia = (SkUiCollectionView)SkiaControl;
        var native = (CollectionView)NativeControl!;
        var contacts = Enumerable.Range(0, 500).Select(Contact.Create).ToList();
        skia.ItemsSource = contacts;
        native.ItemsSource = contacts;
        skia.ItemTemplate = new DataTemplate(SkiaRow);
        native.ItemTemplate = new DataTemplate(NativeRow);
        Feedback("Scroll: the drawn rows recycle their avatars");
    }

    private static SkUiGrid SkiaRow()
    {
        var avatar = new SkUiAvatarView { WidthRequest = 40, HeightRequest = 40, AvatarColorMode = SkUiAvatarColorMode.LightBackground, Initials = SkUiAvatarInitials.FirstAndLastWord, BorderWidth = 0 };
        avatar.SetBinding(SkUiAvatarView.TextProperty, static (Contact contact) => contact.Name);
        avatar.SetBinding(SkUiAvatarView.ImageSourceProperty, static (Contact contact) => contact.Photo);
        avatar.SetBinding(SkUiAvatarView.BadgeColorProperty, static (Contact contact) => contact.Status);
        var name = new SkUiLabel { FontSize = 15, TextColor = DemoColors.Ink, VerticalOptions = LayoutOptions.Center };
        name.SetBinding(SkUiLabel.TextProperty, static (Contact contact) => contact.Name);
        var grid = new SkUiGrid { Padding = new Thickness(12, 6), ColumnSpacing = 12, ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)] };
        grid.Children.Add(avatar);
        grid.Children.Add(name);
        Grid.SetColumn(name, 1);
        return grid;
    }

    private static Grid NativeRow()
    {
        var avatar = new AvatarView { WidthRequest = 40, HeightRequest = 40, CornerRadius = 20, BorderWidth = 0, BackgroundColor = DemoColors.SampleA };
        avatar.SetBinding(AvatarView.TextProperty, static (Contact contact) => contact.Initials);
        avatar.SetBinding(AvatarView.ImageSourceProperty, static (Contact contact) => contact.Photo);
        var name = new Label { FontSize = 15, TextColor = DemoColors.Ink, VerticalOptions = LayoutOptions.Center };
        name.SetBinding(Label.TextProperty, static (Contact contact) => contact.Name);
        var grid = new Grid { Padding = new Thickness(12, 6), ColumnSpacing = 12, ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)] };
        grid.Add(avatar);
        grid.Add(name, 1);
        return grid;
    }

    /// <summary>One contact: a name, every third with a photo, some with a status.</summary>
    public sealed record Contact(string Name, string Initials, ImageSource? Photo, Color? Status)
    {
        public static Contact Create(int index)
        {
            var first = FirstNames[index % FirstNames.Length];
            var last = LastNames[index / FirstNames.Length % LastNames.Length];
            var photo = index % 3 == 0 ? ImageSource.FromFile(index % 2 == 0 ? DemoAssets.EarthImage : DemoAssets.DotnetBotImage) : null;
            var status = (index % 5) switch { 0 => Colors.LimeGreen, 1 => Colors.Orange, _ => null };
            return new Contact($"{first} {last}", $"{first[0]}{last[0]}", photo, status);
        }
    }
}
