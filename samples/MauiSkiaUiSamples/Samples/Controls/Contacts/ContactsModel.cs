using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Controls.Contacts;

/// <summary>The groups a contact can be in (several at once); starred is a flag of its own.</summary>
[Flags]
public enum ContactGroups
{
    None = 0,
    Family = 1,
    Business = 2
}

/// <summary>How the list groups contacts.</summary>
public enum ContactGrouping
{
    /// <summary>By the first letter of the name the list is sorted by.</summary>
    Name,
    /// <summary>Starred, Family, Business, Other: a contact shows in each of its groups.</summary>
    Groups,
    /// <summary>One list.</summary>
    None
}

/// <summary>The order of the contacts.</summary>
public enum ContactSort
{
    FirstName,
    LastName,
    RecentlyAdded
}

/// <summary>How the contacts are shown.</summary>
public enum ContactDisplay
{
    List,
    Tiles
}

public abstract class Observable : INotifyPropertyChanged
{
    // One event args per property name, shared by every instance: raising a change allocates nothing.
    private static readonly ConcurrentDictionary<string, PropertyChangedEventArgs> ChangedArgs = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, ChangedArgs.GetOrAdd(name ?? "", static key => new PropertyChangedEventArgs(key)));
}

public sealed class Contact : Observable
{
    private static readonly Color[] AvatarColors =
        [Color.FromArgb("#087F83"), Color.FromArgb("#A12842"), Color.FromArgb("#5B4FCF"), Color.FromArgb("#C26A12"), Color.FromArgb("#2E7D32"), Color.FromArgb("#1565C0")];

    private string _firstName = "";
    private string _lastName = "";
    private ImageSource? _photo;
    private bool _isStarred;
    private ContactGroups _groups;

    public string FirstName { get => _firstName; set { if (Set(ref _firstName, value)) NameChanged(); } }

    public string LastName { get => _lastName; set { if (Set(ref _lastName, value)) NameChanged(); } }

    public string Phone { get; init; } = "";

    public string Email { get; init; } = "";

    /// <summary>A photo, or <c>null</c> (the avatar shows the initials).</summary>
    public ImageSource? Photo { get => _photo; set => Set(ref _photo, value); }

    public bool IsStarred { get => _isStarred; set { if (Set(ref _isStarred, value)) OnPropertyChanged(nameof(StarGlyph)); } }

    public ContactGroups Groups { get => _groups; set { if (Set(ref _groups, value)) OnPropertyChanged(nameof(GroupsText)); } }

    /// <summary>When the contact was added (the "recently added" order).</summary>
    public DateTime Added { get; init; } = DateTime.Now;

    public string DisplayName => $"{FirstName} {LastName}".Trim();

    public string Initials => $"{FirstName.FirstOrDefault()}{LastName.FirstOrDefault()}".ToUpperInvariant();

    /// <summary>A color from the name (the same in every run: string hash codes change between runs).</summary>
    public Color AvatarColor => AvatarColors[DisplayName.Sum(character => character) % AvatarColors.Length];

    public string StarGlyph => IsStarred ? "★" : "";

    public string GroupsText => _groups == ContactGroups.None ? "No groups" : string.Join(", ", Enum.GetValues<ContactGroups>().Where(group => group != 0 && _groups.HasFlag(group)));

    private void NameChanged()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Initials));
        OnPropertyChanged(nameof(AvatarColor));
    }
}

/// <summary>
/// The contacts screen: the contacts, how they are shown (<see cref="Shown"/>: filtered by <see cref="SearchText"/>, sorted,
/// grouped), the contact previewed (<see cref="Current"/>) and the selection mode (<see cref="IsSelecting"/>, the checked
/// contacts in <see cref="Selected"/>, the actions on them).
/// </summary>
public sealed class ContactsViewModel : Observable, IDisposable
{
    // Group keys: the letter groups ("#": names that start with no letter), and the groups by group, in this order.
    private const string NoLetter = "#";
    private const string StarredGroup = "★ Starred";
    private const string FamilyGroup = "Family";
    private const string BusinessGroup = "Business";
    private const string OtherGroup = "Other";
    private static readonly string[] GroupOrder = [StarredGroup, FamilyGroup, BusinessGroup, OtherGroup];

    private ContactGrouping _grouping = ContactGrouping.Name;
    private ContactSort _sort = ContactSort.FirstName;
    private ContactDisplay _display = ContactDisplay.List;
    private string _searchText = "";
    private Contact? _current;
    private bool _isSelecting;

    public ContactsViewModel(IEnumerable<Contact> contacts)
    {
        All = new ObservableCollection<Contact>(contacts);
        View = new LiveGroupedList<Contact, string>(All)
        {
            GroupTitle = key => key,
            WatchedProperties = { nameof(Contact.FirstName), nameof(Contact.LastName), nameof(Contact.IsStarred), nameof(Contact.Groups) }
        };
        Selected.CollectionChanged += OnSelectedChanged;
        All.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Status));
        Apply();
    }

    /// <summary>Every contact.</summary>
    public ObservableCollection<Contact> All { get; }

    /// <summary>What the list shows: filtered, sorted, grouped; follows <see cref="All"/> and each contact.</summary>
    public LiveGroupedList<Contact, string> View { get; }

    /// <summary>The list's source: groups, or contacts.</summary>
    public System.Collections.IEnumerable Shown => View.Shown;

    public bool IsGrouped => View.IsGrouped;

    /// <summary>The checked contacts while selecting (the list's <c>SelectedItems</c>).</summary>
    public ObservableCollection<object> Selected { get; } = [];

    public ContactGrouping Grouping { get => _grouping; set { if (Set(ref _grouping, value)) Apply(); } }

    public ContactSort Sort { get => _sort; set { if (Set(ref _sort, value)) Apply(); } }

    public ContactDisplay Display { get => _display; set => Set(ref _display, value); }

    public string SearchText { get => _searchText; set { if (Set(ref _searchText, value ?? "")) Apply(); } }

    /// <summary>The contact the preview shows.</summary>
    public Contact? Current { get => _current; set => Set(ref _current, value); }

    /// <summary>The selection mode: contacts are checked (long press to start), actions apply to them.</summary>
    public bool IsSelecting
    {
        get => _isSelecting;
        private set
        {
            if (Set(ref _isSelecting, value))
                OnPropertyChanged(nameof(Status));
        }
    }

    public string Status => IsSelecting ? $"{Selected.Count} selected" : $"{All.Count} contacts";

    /// <summary>Whether every checked contact is starred (the star action then removes the star).</summary>
    public bool SelectionIsStarred => Selected.Count > 0 && Selected.Cast<Contact>().All(contact => contact.IsStarred);

    /// <summary>Starts the selection mode with <paramref name="contacts"/> checked.</summary>
    public void BeginSelection(IEnumerable<Contact> contacts)
    {
        IsSelecting = true;
        foreach (var contact in contacts)
            if (!Selected.Contains(contact))
                Selected.Add(contact);
    }

    public void EndSelection()
    {
        Selected.Clear();
        IsSelecting = false;
    }

    /// <summary>Checks the group's contacts, or unchecks them when all are checked.</summary>
    public void ToggleGroup(ItemGroup<string, Contact> group)
    {
        var check = group.CheckState != SkUiCheckState.Checked;
        foreach (var contact in group)
            if (check && !Selected.Contains(contact))
                Selected.Add(contact);
            else if (!check)
                Selected.Remove(contact);
    }

    public void DeleteSelected()
    {
        foreach (var contact in Selected.Cast<Contact>().ToList())
        {
            All.Remove(contact);
            if (ReferenceEquals(Current, contact))
                Current = null;
        }
        Selected.Clear();
    }

    /// <summary>Stars the checked contacts, or removes their star when all are starred.</summary>
    public void ToggleStarOfSelected()
    {
        var star = !SelectionIsStarred;
        foreach (var contact in Selected.Cast<Contact>())
            contact.IsStarred = star;
    }

    /// <summary>Adds the checked contacts to <paramref name="group"/>, or (<see cref="ContactGroups.None"/>) takes them out of every group.</summary>
    public void SetGroupOfSelected(ContactGroups group)
    {
        foreach (var contact in Selected.Cast<Contact>())
            contact.Groups = group == ContactGroups.None ? ContactGroups.None : contact.Groups | group;
    }

    public void Add(Contact contact)
    {
        All.Add(contact);
        Current = contact;
    }

    /// <summary>The groups a contact shows in, for the current grouping.</summary>
    private IEnumerable<string> GroupsOf(Contact contact)
    {
        if (_grouping == ContactGrouping.Name)
        {
            var name = _sort == ContactSort.LastName ? contact.LastName : contact.FirstName;
            yield return name.Length > 0 && char.IsLetter(name[0]) ? char.ToUpperInvariant(name[0]).ToString() : NoLetter;
            yield break;
        }
        if (contact.IsStarred)
            yield return StarredGroup;
        if (contact.Groups.HasFlag(ContactGroups.Family))
            yield return FamilyGroup;
        if (contact.Groups.HasFlag(ContactGroups.Business))
            yield return BusinessGroup;
        if ((contact.Groups & (ContactGroups.Family | ContactGroups.Business)) == 0)
            yield return OtherGroup;
    }

    /// <summary>Applies the grouping, the order and the search to the view (one rebuild).</summary>
    private void Apply()
    {
        IComparer<Contact> sort = _sort switch
        {
            ContactSort.LastName => Comparer<Contact>.Create((a, b) => Compare(a.LastName, b.LastName, a.FirstName, b.FirstName)),
            ContactSort.RecentlyAdded => Comparer<Contact>.Create((a, b) => b.Added.CompareTo(a.Added)),
            _ => Comparer<Contact>.Create((a, b) => Compare(a.FirstName, b.FirstName, a.LastName, b.LastName))
        };
        IComparer<string> groupOrder = _grouping == ContactGrouping.Groups
            ? Comparer<string>.Create((a, b) => Array.IndexOf(GroupOrder, a).CompareTo(Array.IndexOf(GroupOrder, b)))
            : StringComparer.CurrentCultureIgnoreCase;
        var search = _searchText.Trim();
        View.Configure(
            search.Length == 0 ? null : contact => contact.DisplayName.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                                                  || contact.Phone.Contains(search, StringComparison.Ordinal),
            sort,
            _grouping == ContactGrouping.None ? null : GroupsOf,
            groupOrder);
        // A rebuilt view has new groups: their checkboxes follow the selection again.
        UpdateGroupChecks();
        OnPropertyChanged(nameof(Shown));
        OnPropertyChanged(nameof(IsGrouped));
    }

    private static int Compare(string first, string other, string thenFirst, string thenOther)
    {
        var result = string.Compare(first, other, StringComparison.CurrentCultureIgnoreCase);
        return result != 0 ? result : string.Compare(thenFirst, thenOther, StringComparison.CurrentCultureIgnoreCase);
    }

    private void OnSelectedChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        UpdateGroupChecks();
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(SelectionIsStarred));
    }

    /// <summary>Each group header's checkbox: all, none or some of its contacts checked.</summary>
    private void UpdateGroupChecks()
    {
        if (Selected.Count == 0)
        {
            foreach (var group in View.Groups)
                group.CheckState = SkUiCheckState.Unchecked;
            return;
        }
        var selected = new HashSet<object>(Selected);
        foreach (var group in View.Groups)
        {
            var count = group.Count(selected.Contains);
            group.CheckState = count == 0 ? SkUiCheckState.Unchecked : count == group.Count ? SkUiCheckState.Checked : SkUiCheckState.Indeterminate;
        }
    }

    public void Dispose() => View.Dispose();

    /// <summary>Sample contacts (fictional people; the photos: Resources/Images/README.md).</summary>
    public static IEnumerable<Contact> SampleContacts()
    {
        (string First, string Last, int Photo, ContactGroups Groups, bool Starred)[] people =
        [
            ("Adam", "Kowal", 1, ContactGroups.Business, false),
            ("Alicia", "Moreno", 2, ContactGroups.Family, true),
            ("Anna", "Lindqvist", 3, ContactGroups.None, false),
            ("Bianca", "Ferri", 0, ContactGroups.Business, false),
            ("Brian", "Okafor", 0, ContactGroups.Business | ContactGroups.Family, true),
            ("Carla", "Nowicka", 4, ContactGroups.Family, false),
            ("Daniel", "Weber", 0, ContactGroups.None, false),
            ("Edward", "Hughes", 5, ContactGroups.Family, true),
            ("Emma", "Duarte", 0, ContactGroups.Business, false),
            ("Felix", "Brandt", 6, ContactGroups.Business, false),
            ("Greta", "Holm", 0, ContactGroups.None, false),
            ("Hannah", "Svensson", 7, ContactGroups.Family | ContactGroups.Business, false),
            ("Igor", "Petrenko", 0, ContactGroups.Business, false),
            ("Julia", "Rossi", 0, ContactGroups.Family, false),
            ("Kai", "Tanaka", 0, ContactGroups.None, true),
            ("Lena", "Fischer", 0, ContactGroups.Business, false),
            ("Marco", "Bianchi", 8, ContactGroups.Business, false),
            ("Nina", "Kral", 0, ContactGroups.Family, false),
            ("Oscar", "Lund", 0, ContactGroups.None, false),
            ("Paula", "Sousa", 0, ContactGroups.Business, false),
            ("Quentin", "Moreau", 0, ContactGroups.None, false),
            ("Rosa", "Jimenez", 0, ContactGroups.Family, false),
            ("Stefan", "Novak", 0, ContactGroups.Business, true),
            ("Tara", "Quinn", 0, ContactGroups.None, false),
            ("Uma", "Patel", 0, ContactGroups.Business, false),
            ("Victor", "Almeida", 0, ContactGroups.Family, false),
            ("Wanda", "Zielinska", 0, ContactGroups.None, false),
            ("Xavier", "Dubois", 0, ContactGroups.Business, false),
            ("Yara", "Haddad", 0, ContactGroups.Family, false),
            ("Zoe", "Carter", 0, ContactGroups.None, false)
        ];
        var start = DateTime.Now.AddDays(-people.Length);
        return people.Select((person, index) => new Contact
        {
            FirstName = person.First,
            LastName = person.Last,
            Photo = person.Photo > 0 ? ImageSource.FromFile($"contact_photo_{person.Photo}.png") : null,
            Groups = person.Groups,
            IsStarred = person.Starred,
            Phone = $"+1 555 01{index:00}",
            Email = $"{person.First.ToLowerInvariant()}.{person.Last.ToLowerInvariant()}@example.com",
            Added = start.AddDays(index)
        });
    }
}
