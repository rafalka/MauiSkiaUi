using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// A skeleton made from the content: a feed of cards (avatars, names, messages, a photo, a Like button, a switch) that is its
/// own skeleton while it loads. The page title stays as it is (<see cref="SkUiShimmer.SkeletonProperty"/> <c>Keep</c>), the
/// cards keep their chrome in <see cref="SkUiShimmerMode.Bones"/>, empty texts show placeholder lines. Compare with a
/// hand-made skeleton of the same feed. The page scrolls: the scroller is outside the shimmer, whose content takes no input
/// while it is a skeleton.
/// </summary>
public sealed class ShimmerContentDemoPage : ComponentDemoPage
{
    private const string FeedTitle = "Team updates";

    /// <summary>Where the skeleton comes from.</summary>
    private enum SkeletonSource
    {
        /// <summary>The content itself (no <c>SkeletonContent</c>).</summary>
        Content,

        /// <summary>A hand-made skeleton of shimmer bones.</summary>
        HandMade
    }

    private static readonly (string Name, string Message)[] Posts =
    [
        ("Ada Lovelace", "The engine draws the cards while the feed loads; their bones come from their own layout."),
        ("Grace Hopper", "Shipped the release notes."),
        ("Alan Turing", "Reviewing the skeleton of the profile page next.")
    ];

    private readonly System.Collections.ObjectModel.ObservableCollection<string> _members = [];
    private readonly List<SkUiLabel> _names = [];
    private readonly List<SkUiLabel> _messages = [];

    public ShimmerContentDemoPage()
        : base("Skeleton from the content", Create(out var shimmer, out var feed), widthRange: (220, 420, 340), heightRange: (200, 700, 460))
    {
        foreach (var card in feed.Children.OfType<SkUiBorder>())
            if (card.Content is SkUiGrid grid)
            {
                _names.Add((SkUiLabel)grid.Children[1]);
                _messages.Add((SkUiLabel)grid.Children[2]);
            }
        feed.Children.OfType<SkUiCollectionView>().Single().ItemsSource = _members;
        Toggle(nameof(SkUiShimmer.IsLoading), true, value =>
        {
            shimmer.IsLoading = value;
            // The members arrive with the rest: while loading the empty list shows placeholder rows of its template.
            _members.Clear();
            if (!value)
                foreach (var post in Posts)
                    _members.Add(post.Name);
        }, () => shimmer.IsLoading);
        Choice("Skeleton", [SkeletonSource.Content, SkeletonSource.HandMade], SkeletonSource.Content,
            value => shimmer.SkeletonContent = value == SkeletonSource.HandMade ? HandMade() : null,
            () => shimmer.SkeletonContent is null ? SkeletonSource.Content : SkeletonSource.HandMade);
        Choice(nameof(SkUiShimmer.Mode), [SkUiShimmerMode.Bones, SkUiShimmerMode.Silhouette], SkUiShimmerMode.Bones,
            value => shimmer.Mode = value, () => shimmer.Mode);
        Choice(nameof(SkUiShimmer.Effect), [SkUiShimmerEffect.Wave, SkUiShimmerEffect.Pulse, SkUiShimmerEffect.Color, SkUiShimmerEffect.None], SkUiShimmerEffect.Wave,
            value => shimmer.Effect = value, () => shimmer.Effect);
        Toggle("Empty texts while loading", false, ShowEmptyTexts, () => _names[0].Text.Length == 0);
        Feedback("Each control draws its bones: a bar per line, shapes, outlines; the title keeps its text (Skeleton = Keep)");
    }

    /// <summary>Clears the names and messages (placeholder lines, one per name and two per message) or shows them again.</summary>
    private void ShowEmptyTexts(bool empty)
    {
        for (var index = 0; index < _names.Count; index++)
        {
            _names[index].Text = empty ? string.Empty : Posts[index].Name;
            _messages[index].Text = empty ? string.Empty : Posts[index].Message;
        }
    }

    /// <summary>
    /// The feed in a shimmer, in a scroller: a skeleton takes no input, so the scroller is outside the shimmer to scroll the
    /// skeleton too.
    /// </summary>
    private static SkUiScrollView Create(out SkUiShimmer shimmer, out SkUiVerticalStackLayout feed)
    {
        feed = new SkUiVerticalStackLayout { Spacing = 12, Padding = new Thickness(12), BackgroundColor = DemoColors.PageBackground };
        var title = new SkUiLabel { Text = FeedTitle, FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = DemoColors.Ink };
        SkUiShimmer.SetSkeleton(title, SkUiSkeleton.Keep);
        feed.Children.Add(title);
        for (var index = 0; index < Posts.Length; index++)
            feed.Children.Add(Card(Posts[index].Name, Posts[index].Message, photo: index == 0));
        var settings = new SkUiHorizontalStackLayout { Spacing = 8 };
        settings.Children.Add(new SkUiSwitch { IsToggled = true, VerticalOptions = LayoutOptions.Center });
        settings.Children.Add(new SkUiLabel { Text = "Notify me of new posts", TextColor = DemoColors.Ink, VerticalOptions = LayoutOptions.Center });
        feed.Children.Add(settings);
        feed.Children.Add(new SkUiLabel { Text = "Members", FontAttributes = FontAttributes.Bold, TextColor = DemoColors.Ink });
        feed.Children.Add(new SkUiCollectionView
        {
            HeightRequest = 150, ItemSpacing = 8, BackgroundColor = Colors.White,
            EmptyView = new SkUiLabel { Text = "No members", TextColor = DemoColors.Caption, Padding = new Thickness(12) },
            ItemTemplate = new DataTemplate(Member)
        });
        shimmer = new SkUiShimmer { Mode = SkUiShimmerMode.Bones, Content = feed };
        return new SkUiScrollView { BackgroundColor = DemoColors.PageBackground, Content = shimmer };
    }

    /// <summary>A white card: an avatar, a name, a message, an optional photo and a Like button.</summary>
    private static SkUiBorder Card(string name, string message, bool photo)
    {
        var grid = new SkUiGrid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
            RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)],
            ColumnSpacing = 12, RowSpacing = 6
        };
        var avatar = new SkUiAvatarView { Text = Initials(name), WidthRequest = 40, HeightRequest = 40, CornerRadius = 20, BackgroundColor = DemoColors.SoftSurface };
        Grid.SetRowSpan(avatar, 2);
        grid.Children.Add(avatar);
        var nameLabel = new SkUiLabel { Text = name, FontAttributes = FontAttributes.Bold, TextColor = DemoColors.Ink };
        Grid.SetColumn(nameLabel, 1);
        grid.Children.Add(nameLabel);
        var messageLabel = new SkUiLabel { Text = message, TextColor = DemoColors.Caption };
        SkUiShimmer.SetSkeletonLines(messageLabel, 2);
        Grid.SetColumn(messageLabel, 1);
        Grid.SetRow(messageLabel, 1);
        grid.Children.Add(messageLabel);
        if (photo)
        {
            var image = new SkUiBox { Color = DemoColors.SampleB, CornerRadius = 8, HeightRequest = 90 };
            Grid.SetColumnSpan(image, 2);
            Grid.SetRow(image, 2);
            grid.Children.Add(image);
        }
        var like = new SkUiButton { Text = "Like", CornerRadius = 16, Padding = new Thickness(16, 6), HorizontalOptions = LayoutOptions.End };
        Grid.SetColumnSpan(like, 2);
        Grid.SetRow(like, 3);
        grid.Children.Add(like);
        return new SkUiBorder
        {
            Stroke = DemoColors.Border, StrokeThickness = 1, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            BackgroundColor = Colors.White, Padding = new Thickness(12), Content = grid
        };
    }

    /// <summary>A member row: an avatar and a name bound to the item (a placeholder row has no item: a circle and a line).</summary>
    private static SkUiHorizontalStackLayout Member()
    {
        var avatar = new SkUiAvatarView
        {
            WidthRequest = 28, HeightRequest = 28, CornerRadius = 14, BackgroundColor = DemoColors.SoftSurface, Initials = SkUiAvatarInitials.FirstAndLastWord
        };
        avatar.SetBinding(SkUiAvatarView.TextProperty, Binding.SelfPath);
        var name = new SkUiLabel { TextColor = DemoColors.Ink, VerticalOptions = LayoutOptions.Center };
        name.SetBinding(SkUiLabel.TextProperty, Binding.SelfPath);
        return new SkUiHorizontalStackLayout { Spacing = 10, Padding = new Thickness(12, 0), Children = { avatar, name } };
    }

    private static string Initials(string name) => string.Concat(name.Split(' ').Select(part => part[0]));

    /// <summary>The same feed as a hand-made skeleton: the title and three cards of bones.</summary>
    private static SkUiVerticalStackLayout HandMade()
    {
        var skeleton = new SkUiVerticalStackLayout { Spacing = 12, Padding = new Thickness(12), BackgroundColor = DemoColors.PageBackground };
        skeleton.Children.Add(new SkUiLabel { Text = FeedTitle, FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = DemoColors.Ink });
        for (var card = 0; card < Posts.Length; card++)
        {
            var lines = new SkUiVerticalStackLayout { Spacing = 8 };
            lines.Children.Add(new SkUiShimmerBone { WidthRequest = 120, HeightRequest = 12, CornerRadius = 4, HorizontalOptions = LayoutOptions.Start });
            lines.Children.Add(new SkUiShimmerBone { HeightRequest = 10, CornerRadius = 4 });
            lines.Children.Add(new SkUiShimmerBone { WidthRequest = 140, HeightRequest = 10, CornerRadius = 4, HorizontalOptions = LayoutOptions.Start });
            var row = new SkUiGrid { ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)], ColumnSpacing = 12 };
            row.Children.Add(new SkUiShimmerBone { WidthRequest = 40, HeightRequest = 40, CornerRadius = 20, VerticalOptions = LayoutOptions.Start });
            Grid.SetColumn(lines, 1);
            row.Children.Add(lines);
            skeleton.Children.Add(new SkUiBorder
            {
                Stroke = DemoColors.Border, StrokeThickness = 1, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                BackgroundColor = Colors.White, Padding = new Thickness(12), Content = row
            });
        }
        return skeleton;
    }
}
