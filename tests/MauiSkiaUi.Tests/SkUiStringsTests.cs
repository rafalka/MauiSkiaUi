using System.Collections.ObjectModel;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// <see cref="SkUiStrings"/>: SkiaUi's own texts in one app-wide object, replaced as a whole, changed in place or read from a
/// derived class; screen readers' states and drawn texts follow.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class SkUiStringsTests : IDisposable
{
    private readonly SkUiStrings _original = SkUiStrings.Current;

    public void Dispose() => SkUiStrings.Current = _original;

    private static SkUiStrings Polish() => new()
    {
        AcceptText = "Zatwierdź",
        CancelText = "Anuluj",
        LoadMoreText = "Wczytaj więcej",
        SelectedStateText = "Zaznaczone",
        ExpandedStateText = "Rozwinięte",
        CollapsedStateText = "Zwinięte"
    };

    [Fact]
    public void TheDefaultsAreEnglish()
    {
        var strings = new SkUiStrings();

        Assert.Equal(("OK", "Cancel", "Load more"), (strings.AcceptText, strings.CancelText, strings.LoadMoreText));
        Assert.Equal(("Selected", "Expanded", "Collapsed"), (strings.SelectedStateText, strings.ExpandedStateText, strings.CollapsedStateText));
        Assert.Throws<ArgumentNullException>(() => strings.AcceptText = null!);
        Assert.Throws<ArgumentNullException>(() => SkUiStrings.Current = null!);
    }

    [Fact]
    public void ScreenReadersReadTheCurrentTexts()
    {
        SkUiStrings.Current = Polish();
        var expander = new SkUiExpander { Header = new SkUiLabel { Text = "Szczegóły", WidthRequest = 80, HeightRequest = 20 }, Content = new SkUiBox { HeightRequest = 30 } };
        var list = new SkUiCollectionView { ItemsSource = new[] { "a", "b" }, SelectionMode = SkUiSelectionMode.Single, SelectedItem = "b" };
        var root = new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { expander, list } } };
        SkUiTestHelpers.Arrange(root, 200, 300);
        var tree = SkUiSemanticsTree.Build(root);

        Assert.Equal("Zwinięte", tree.Find(expander.HeaderHost)!.Value);
        Assert.Contains(tree.Flatten(), element => element.Value == "Zaznaczone");
        expander.IsExpanded = true;
        Assert.Equal("Rozwinięte", SkUiSemanticsTree.Build(root).Find(expander.HeaderHost)!.Value);
    }

    [Fact]
    public void ReplacingOrChangingTheCurrentTextsIsAnnounced()
    {
        var raised = 0;
        void OnChanged(object? sender, EventArgs args) => raised++;
        SkUiStrings.CurrentChanged += OnChanged;
        try
        {
            var polish = Polish();
            SkUiStrings.Current = polish;
            Assert.Equal(1, raised);
            polish.LoadMoreText = "Więcej";
            Assert.Equal(2, raised);
            polish.LoadMoreText = "Więcej"; // unchanged
            new SkUiStrings().LoadMoreText = "Not current";
            Assert.Equal(2, raised);
            SkUiStrings.NotifyChanged(); // a culture switch read by overridden getters
            Assert.Equal(3, raised);
        }
        finally
        {
            SkUiStrings.CurrentChanged -= OnChanged;
        }
    }

    [Fact]
    public void TheLoadMoreButtonTakesNewTexts()
    {
        var list = new SkUiCollectionView
        {
            ItemsSource = new ObservableCollection<string> { "a", "b" }, LoadMoreMode = SkUiLoadMoreMode.Manual,
            LoadMoreCommand = new Command(() => { }), Overscroll = SkUiOverscrollMode.None
        };
        SkUiTestHelpers.Arrange(list, 300, 500);
        SkUiButton Button() => Assert.Single(Descendants(list.LoadMoreRow!).OfType<SkUiButton>());
        Assert.Equal("Load more", Button().Text);

        SkUiStrings.Current = Polish();
        Assert.Equal("Wczytaj więcej", Button().Text);
        SkUiStrings.Current.LoadMoreText = "Więcej";
        Assert.Equal("Więcej", Button().Text);
    }

    private static IEnumerable<ISkUiView> Descendants(ISkUiView view)
    {
        yield return view;
        if (view is SkUiView node)
            foreach (var child in node.SkiaChildren)
                foreach (var descendant in Descendants(child))
                    yield return descendant;
    }
}
