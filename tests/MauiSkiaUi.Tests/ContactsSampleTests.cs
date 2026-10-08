using System.Collections.ObjectModel;
using System.Collections.Specialized;
using MauiSkiaUiSamples.Samples.Controls.Contacts;
using Xunit;
using Contact = MauiSkiaUiSamples.Samples.Controls.Contacts.Contact;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The Contacts sample (samples app): its live grouped list helper (<see cref="LiveGroupedList{TItem, TKey}"/>) and the
/// screen built on <see cref="SkUiCollectionView"/> (grouping, search, selection mode, actions, adaptive preview, tiles).
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class ContactsSampleTests
{
    private sealed class Person(string name, params string[] tags) : Observable
    {
        private string _name = name;
        private string[] _tags = tags;

        public string Name { get => _name; set => Set(ref _name, value); }

        public string[] Tags { get => _tags; set => Set(ref _tags, value); }

        public override string ToString() => Name;
    }

    private static readonly IComparer<Person> ByName = Comparer<Person>.Create((a, b) => string.CompareOrdinal(a.Name, b.Name));

    private static List<string> Log<T>(ObservableCollection<T> collection)
    {
        var log = new List<string>();
        collection.CollectionChanged += (_, args) => log.Add(args.Action switch
        {
            NotifyCollectionChangedAction.Add => $"add {args.NewItems![0]} at {args.NewStartingIndex}",
            NotifyCollectionChangedAction.Remove => $"remove {args.OldItems![0]} at {args.OldStartingIndex}",
            NotifyCollectionChangedAction.Move => $"move {args.OldItems![0]} {args.OldStartingIndex}->{args.NewStartingIndex}",
            _ => args.Action.ToString().ToLowerInvariant()
        });
        return log;
    }

    #region LiveGroupedList

    [Fact]
    public void AFlatViewFiltersSortsAndFollowsTheSourceItemByItem()
    {
        var source = new ObservableCollection<Person> { new("Cleo"), new("Ada"), new("Ben"), new("Zed") };
        using var view = new LiveGroupedList<Person, string>(source);
        view.Configure(person => person.Name != "Zed", ByName, null);
        Assert.Equal(["Ada", "Ben", "Cleo"], view.Items.Select(person => person.Name));
        var log = Log(view.Items);

        source.Add(new Person("Bea"));
        source.Remove(source[0]);           // Cleo
        source.Add(new Person("Zara"));     // filtered out? no: only "Zed" is
        Assert.Equal(["Ada", "Bea", "Ben", "Zara"], view.Items.Select(person => person.Name));
        Assert.Equal(["add Bea at 1", "remove Cleo at 3", "add Zara at 3"], log);

        // A rename moves the item (one move: a list control keeps it, and its selection).
        log.Clear();
        source.First(person => person.Name == "Ada").Name = "Cyd";
        Assert.Equal(["Bea", "Ben", "Cyd", "Zara"], view.Items.Select(person => person.Name));
        Assert.Equal(["move Cyd 0->2"], log);

        // A change that the filter refuses removes it; a setting rebuilds with one reset.
        log.Clear();
        source.First(person => person.Name == "Ben").Name = "Zed";
        view.Sort = Comparer<Person>.Create((a, b) => string.CompareOrdinal(b.Name, a.Name));
        Assert.Equal(["remove Zed at 1", "reset"], log);
        Assert.Equal(["Zara", "Cyd", "Bea"], view.Items.Select(person => person.Name));
    }

    [Fact]
    public void ItemsShowInEachOfTheirGroupsAndMoveToNewGroupsBeforeLeavingOldOnes()
    {
        var ada = new Person("Ada", "family", "work");
        var ben = new Person("Ben", "work");
        var source = new ObservableCollection<Person> { ada, ben };
        using var view = new LiveGroupedList<Person, string>(source);
        view.Configure(null, ByName, person => person.Tags);
        Assert.Equal(["family", "work"], view.Groups.Select(group => group.Key));
        Assert.Equal([ada], view.Groups[0]);
        Assert.Equal([ada, ben], view.Groups[1]);
        Assert.Equal(2, view.GroupsOf(ada).Count);

        // Ben joins "family" and leaves "work": added to the new group first, so he is shown throughout.
        var events = new List<string>();
        view.Groups[0].CollectionChanged += (_, args) => events.Add($"family {args.Action}");
        view.Groups[1].CollectionChanged += (_, args) => events.Add($"work {args.Action}");
        ben.Tags = ["family"];
        Assert.Equal(["family Add", "work Remove"], events);
        Assert.Equal([ada, ben], view.Groups[0]);

        // A new group comes in at its place; a group goes with its last item.
        var groups = Log(view.Groups);
        source.Add(new Person("Cy", "club"));
        ada.Tags = ["family"];
        source.Remove(source[2]);
        Assert.Equal(["add club at 0", "remove work at 2", "remove club at 0"], groups);
        Assert.Equal(["family"], view.Groups.Select(group => group.Key));
    }

    [Fact]
    public void CollapsedGroupsStayCollapsedWhenTheViewIsRebuilt()
    {
        var source = new ObservableCollection<Person> { new("Ada", "a"), new("Ben", "b") };
        using var view = new LiveGroupedList<Person, string>(source);
        view.Configure(null, ByName, person => person.Tags);
        view.Groups[1].IsExpanded = false;
        view.Sort = Comparer<Person>.Create((x, y) => string.CompareOrdinal(y.Name, x.Name));
        Assert.True(view.Groups[0].IsExpanded);
        Assert.False(view.Groups[1].IsExpanded);
    }

    #endregion

    #region The screen

    private static (ContactsSample Page, SkUiCollectionView List) Screen(double width = 900, double height = 700)
    {
        var page = new ContactsSample();
        var root = (SkUiView)page.SampleContent!;
        SkUiTestHelpers.Arrange(root, width, height);
        var list = Find<SkUiCollectionView>(root);
        SkUiTestHelpers.Arrange(root, width, height); // after the size reached the page's layout
        return (page, list);
    }

    private static T Find<T>(SkUiView view) where T : SkUiView =>
        view as T ?? view.SkiaChildren.OfType<SkUiView>().Select(child => Find<T>(child) as T).FirstOrDefault(found => found is not null)!;

    [Fact]
    public void ContactsAreGroupedByNameOrByGroupWithContactsInSeveralGroups()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var (page, list) = Screen();
        var model = page.Model;
        Assert.True(list.IsGrouped);
        Assert.Equal(30, list.ItemCount);
        Assert.Equal("A", model.View.Groups[0].Key);

        model.Grouping = ContactGrouping.Groups;
        Assert.Equal(["★ Starred", "Family", "Business", "Other"], model.View.Groups.Select(group => group.Key));
        // Brian is starred, in Family and in Business: three times.
        var brian = model.All.Single(contact => contact.FirstName == "Brian");
        Assert.Equal(3, model.View.GroupsOf(brian).Count);
        Assert.Equal(model.View.Groups.Sum(group => group.Count), list.ItemCount);

        model.Grouping = ContactGrouping.None;
        Assert.False(list.IsGrouped);
        Assert.Equal(30, list.ItemCount);

        model.SearchText = "ann";
        Assert.Equal(["Anna Lindqvist", "Hannah Svensson"], model.View.Items.Select(contact => contact.DisplayName));
    }

    [Fact]
    public void TheSelectionModeChecksContactsAndGroupsAndItsActionsApplyToThem()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var (page, list) = Screen();
        var model = page.Model;
        var groupA = model.View.Groups[0];
        var adam = groupA[0];

        model.BeginSelection([adam]);
        Assert.Equal(SkUiSelectionMode.Multiple, list.SelectionMode);
        Assert.Equal([adam], list.SelectedItems);
        Assert.Equal(SkUiCheckState.Indeterminate, groupA.CheckState);
        Assert.Equal("1 selected", model.Status);

        model.ToggleGroup(groupA);
        Assert.Equal(SkUiCheckState.Checked, groupA.CheckState);
        model.ToggleGroup(groupA);
        Assert.Equal(SkUiCheckState.Unchecked, groupA.CheckState);

        // A rename moves a checked contact to another letter: it stays checked.
        model.BeginSelection([adam]);
        adam.FirstName = "Zbigniew";
        Assert.Contains(adam, list.SelectedItems);
        Assert.Equal("Z", model.View.GroupsOf(adam).Single().Key);

        // Star and delete the checked contacts.
        model.ToggleStarOfSelected();
        Assert.True(adam.IsStarred);
        model.DeleteSelected();
        Assert.Equal(29, model.All.Count);
        Assert.Equal(29, list.ItemCount);
        Assert.Empty(list.SelectedItems);

        model.EndSelection();
        Assert.Equal(SkUiSelectionMode.Single, list.SelectionMode);
    }

    [Fact]
    public void ThePreviewIsBesideTheListOnWideScreensAndAPopupOnNarrowOnes()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var (wide, wideList) = Screen(900, 700);
        Assert.True(wide.IsWide);
        var contact = wide.Model.All[5];
        wide.Model.Current = contact;
        Assert.Same(contact, wideList.SelectedItem);

        var (narrow, _) = Screen(360, 700);
        Assert.False(narrow.IsWide);
    }

    [Fact]
    public void TilesShareRowsByWidth()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var (page, list) = Screen(900, 700);
        page.Model.Display = ContactDisplay.Tiles;
        Assert.True(list.Span > 1);
        page.Model.Display = ContactDisplay.List;
        Assert.Equal(1, list.Span);
    }

    [Fact]
    public void AnAddedContactIsShownAndPreviewed()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var (page, list) = Screen();
        var contact = new Contact { FirstName = "Aaron", LastName = "Abbott", Groups = ContactGroups.Family };
        page.Model.Add(contact);
        Assert.Equal(31, list.ItemCount);
        Assert.Same(contact, page.Model.Current);
        Assert.Same(contact, list.SelectedItem);
        Assert.Contains(contact, page.Model.View.Groups[0]);
    }

    #endregion
}
