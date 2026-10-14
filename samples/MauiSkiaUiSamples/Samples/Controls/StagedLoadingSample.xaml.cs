using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Controls;

/// <summary>
/// HOWTO: a page that loads in stages, its own skeleton at every stage. One request loads the team and its members (names
/// only); then one request per member loads that member's details, a few at a time. The page's <see cref="SkUiShimmer"/>
/// shows its content as bones while the team loads (the empty member list shows placeholder rows of its template); each
/// row's details are a shimmer of their own, bound to the row, so rows show their names at once and their details as they
/// arrive.
/// </summary>
public sealed partial class StagedLoadingSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Loading in stages",
        Summary: "A team page that loads like a real one: the team and its members first, then every member's details in a " +
                 "request of its own. No hand-made skeleton: the page, and each row's details, are their own skeleton while they load.",
        HowTo:
        [
            "Put the page in an `SkUiShimmer` without `SkeletonContent`: while `IsLoading`, every control draws its bones (texts " +
                "as bars, shapes, outlines) and cards keep their chrome (`Mode=\"Bones\"`).",
            "A collection view that is still empty shows placeholder rows of its `ItemTemplate` (no binding context: empty " +
                "texts draw placeholder lines), as many as fill it; set `sk:SkUiShimmer.SkeletonItemCount` for a fixed number.",
            "For details that load per row, put a shimmer around them inside the item template and bind its `IsLoading` to the " +
                "row (`IsLoadingDetails`). A shimmer decides for its own content, so rows show their names once the page has loaded.",
            "Texts known before the data (the \"Members\" title): `sk:SkUiShimmer.Skeleton=\"Keep\"`. Empty texts that will hold " +
                "more than a line: `sk:SkUiShimmer.SkeletonLines`."
        ],
        ThingsToKnow:
        [
            "`WaveRegion=\"Surface\"` on every shimmer: the page's and the rows' waves are one wave across the screen.",
            "A skeleton takes no input; the list scrolls once the page has loaded, and a row's details take taps once they have.",
            "Nothing is recorded per frame: each control records once when it enters or leaves its skeleton.",
            "Nothing loads when the page opens: it shows one label until Load is tapped. Load again to start over: the " +
                "members are cleared, so the list is empty again and its placeholder rows come back."
        ]);

    public StagedLoadingSample() : this(new StagedLoadingModel())
    {
    }

    /// <summary>The page over <paramref name="model"/> (tests drive its requests).</summary>
    public StagedLoadingSample(StagedLoadingModel model) : base(Info, fillsPage: true)
    {
        InitializeComponent();
        BindingContext = model;
    }
}

/// <summary>A member's row: its name and role come with the team; its details with a request of their own.</summary>
public sealed class MemberRow(string name, string role) : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs ActivityChanged = new(nameof(Activity));
    private static readonly PropertyChangedEventArgs ProgressChanged = new(nameof(Progress));
    private static readonly PropertyChangedEventArgs ScoreChanged = new(nameof(Score));
    private static readonly PropertyChangedEventArgs IsLoadingDetailsChanged = new(nameof(IsLoadingDetails));

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; } = name;

    public string Role { get; } = role;

    /// <summary>What the member did lately (empty until the details arrive).</summary>
    public string Activity { get; private set; } = string.Empty;

    /// <summary>The sprint's tasks done, 0–1.</summary>
    public double Progress { get; private set; }

    public string Score { get; private set; } = string.Empty;

    public bool IsLoadingDetails { get; private set; } = true;

    /// <summary>The details arrived.</summary>
    public void SetDetails(string activity, double progress, int points)
    {
        Activity = activity;
        Progress = progress;
        Score = $"{points} pts";
        IsLoadingDetails = false;
        PropertyChanged?.Invoke(this, ActivityChanged);
        PropertyChanged?.Invoke(this, ProgressChanged);
        PropertyChanged?.Invoke(this, ScoreChanged);
        PropertyChanged?.Invoke(this, IsLoadingDetailsChanged);
    }
}

/// <summary>
/// Loads the team page in stages through simulated requests (delays): the team and its members first, then each member's
/// details, <see cref="ParallelRequests"/> at a time. Nothing loads until <see cref="LoadCommand"/>; loading again cancels
/// what is still loading and starts over.
/// </summary>
public sealed class StagedLoadingModel : INotifyPropertyChanged
{
    /// <summary>Details requests in flight at once.</summary>
    public const int ParallelRequests = 3;

    private const string TeamName = "Mobile platform";
    private const string NotLoaded = "Not loaded";
    private const string LoadingTeam = "Loading the team…";
    private const string Loaded = "Loaded";

    private static readonly PropertyChangedEventArgs IsLoadingTeamChanged = new(nameof(IsLoadingTeam));
    private static readonly PropertyChangedEventArgs HasStartedChanged = new(nameof(HasStarted));
    private static readonly PropertyChangedEventArgs IsIdleChanged = new(nameof(IsIdle));
    private static readonly PropertyChangedEventArgs TeamNameChanged = new(nameof(Team));
    private static readonly PropertyChangedEventArgs TeamSummaryChanged = new(nameof(TeamSummary));
    private static readonly PropertyChangedEventArgs StatusChanged = new(nameof(Status));

    private static readonly (string Name, string Role, string Activity, double Progress, int Points)[] People =
    [
        ("Maya Chen", "Tech lead", "Reviewed the release plan and merged the navigation rewrite.", 0.9, 42),
        ("Tom Becker", "Android", "Fixed the crash on rotation.", 0.7, 35),
        ("Amara Okafor", "iOS", "Shipped the new onboarding screens and their snapshot tests.", 0.8, 38),
        ("Luis Ortega", "Backend", "Moved the sync endpoint to the new queue.", 0.5, 27),
        ("Hanna Nowak", "Design", "Shared the dark theme with the team.", 0.6, 30),
        ("Kenji Sato", "QA", "Automated the checkout regression suite; three flaky tests left.", 0.4, 22),
        ("Priya Nair", "Android", "Profiled the feed: half the frame time saved.", 0.75, 33),
        ("Jonas Berg", "iOS", "Upgraded the build pipeline.", 0.3, 18),
        ("Sofia Rossi", "Product", "Wrote the stories for the offline mode.", 0.55, 26),
        ("Omar Haddad", "Backend", "Added rate limits to the public API and documented them.", 0.65, 29)
    ];

    private readonly Func<int, CancellationToken, Task> _request;
    private CancellationTokenSource? _loading;

    /// <param name="request">
    /// A request taking about the milliseconds given (default: a delay); tests pass their own to end requests one by one.
    /// </param>
    public StagedLoadingModel(Func<int, CancellationToken, Task>? request = null)
    {
        _request = request ?? Task.Delay;
        LoadCommand = new Command(() => _ = LoadAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Loads the page (again: from the start).</summary>
    public ICommand LoadCommand { get; }

    /// <summary>Whether a load started: the page shows (before, one label).</summary>
    public bool HasStarted
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            PropertyChanged?.Invoke(this, HasStartedChanged);
            PropertyChanged?.Invoke(this, IsIdleChanged);
        }
    }

    /// <summary>Nothing loaded yet: only the label telling to tap Load shows.</summary>
    public bool IsIdle => !HasStarted;

    /// <summary>The members, added when the team request ends (empty before: the list shows placeholder rows).</summary>
    public ObservableCollection<MemberRow> Members { get; } = [];

    public bool IsLoadingTeam
    {
        get;
        private set => Set(ref field, value, IsLoadingTeamChanged);
    }

    public string Team
    {
        get;
        private set => Set(ref field, value, TeamNameChanged);
    } = string.Empty;

    public string TeamSummary
    {
        get;
        private set => Set(ref field, value, TeamSummaryChanged);
    } = string.Empty;

    /// <summary>What is loading, for the line above the page.</summary>
    public string Status
    {
        get;
        private set => Set(ref field, value, StatusChanged);
    } = NotLoaded;

    private void Set<T>(ref T field, T value, PropertyChangedEventArgs args)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, args);
    }

    /// <summary>Loads the page from the start: the team, then every member's details.</summary>
    public async Task LoadAsync()
    {
        _loading?.Cancel();
        var loading = _loading = new CancellationTokenSource();
        var token = loading.Token;
        Members.Clear();
        (Team, TeamSummary, IsLoadingTeam, Status) = (string.Empty, string.Empty, true, LoadingTeam);
        HasStarted = true; // loading already: the page shows as its skeleton
        try
        {
            // Request 1: the team and its members, names and roles only.
            await _request(1500, token);
            Team = TeamName;
            TeamSummary = $"{People.Length} members · sprint 42";
            foreach (var person in People)
                Members.Add(new MemberRow(person.Name, person.Role));
            IsLoadingTeam = false;

            // Requests 2…n: each member's details, a few at a time, in no particular order.
            var loaded = 0;
            Status = DetailsStatus(loaded);
            using var gate = new SemaphoreSlim(ParallelRequests);
            var random = new Random();
            await Task.WhenAll(Members.Select(async (row, index) =>
            {
                await gate.WaitAsync(token);
                try
                {
                    await _request(500 + random.Next(2500), token);
                    var person = People[index];
                    row.SetDetails(person.Activity, person.Progress, person.Points);
                    Status = DetailsStatus(++loaded);
                }
                finally
                {
                    gate.Release();
                }
            }));
            Status = Loaded;
        }
        catch (OperationCanceledException)
        {
            // Reloaded meanwhile.
        }
    }

    private string DetailsStatus(int loaded) => $"Loading details: {loaded} of {Members.Count}";
}
