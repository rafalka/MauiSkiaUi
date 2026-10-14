using System.ComponentModel;
using System.Windows.Input;
using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Controls;

/// <summary>
/// HOWTO: a loading skeleton for a page that stays in MAUI. The leaderboard is plain MAUI XAML; its skeleton is one
/// <see cref="SkUiShimmer"/> laid over it in a <c>Grid</c>, with <see cref="SkUiShimmerMode.Bones"/>: only the
/// <see cref="SkUiShimmerBone"/>s shimmer (one wave across all of them), while the skeleton's cards, divider and Cancel
/// button draw as they are. The bones are drawn by the render thread from the shared clock, with no layer and
/// nothing recorded per frame.
/// </summary>
public sealed partial class LeaderboardLoadingSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Loading skeleton over a MAUI page",
        Summary: "A leaderboard page in plain MAUI with a drawn loading skeleton over it: bones that shimmer among cards and " +
                 "a Cancel button that do not, faded out when the data arrives.",
        HowTo:
        [
            "Keep the page in MAUI. Put an `SkUiShimmer` over it in the same `Grid` cell, with the skeleton as its " +
                "`SkeletonContent` and no `Content`.",
            "`Mode=\"Bones\"`: only `SkUiShimmerBone`s shimmer (`CornerRadius` half the size makes a circle); cards, dividers " +
                "and buttons in the skeleton draw as they are.",
            "Give the skeleton the page's background so the page does not show through while it loads.",
            "Fade it out: `BeforeStateChangeAnimation=\"FadeOut 350 CubicInOut\"`, then hide it: " +
                "`IsVisible=\"{Binding IsShowingSkeleton, Source={RelativeSource Self}, x:DataType=sk:SkUiShimmer}\"`."
        ],
        ThingsToKnow:
        [
            "The skeleton is one drawn surface whatever its size: one platform view for the whole page instead of one per bone.",
            "Hidden, it draws nothing and requests no frames; setting `IsLoading` back to `true` shows it again.",
            "Effects, colors and timing are the shimmer's (`Effect`, `BoneColor`, `HighlightColor`, `Duration`, …); a bone " +
                "can have its own `Color`.",
            "With many repeated placeholders, Core bones (`SkUiCoreShimmerBone` in an `SkUiCoreHost`) build faster: no " +
                "bindable properties."
        ]);

    public LeaderboardLoadingSample() : base(Info, fillsPage: true)
    {
        InitializeComponent();
        BindingContext = new LeaderboardLoadingModel();
    }
}

/// <summary>One row of the leaderboard.</summary>
public sealed record LeaderboardEntry(int Rank, string Name, string Team, string Value, string Change);

/// <summary>One placeholder card: its bones' widths, so the skeleton does not look stamped.</summary>
public sealed record LeaderboardPlaceholder(double Name, double Team, double Value, double Change);

/// <summary>Loads the leaderboard (a delay standing for a request); cancel ends the loading at once.</summary>
public sealed class LeaderboardLoadingModel : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs IsLoadingChanged = new(nameof(IsLoading));
    private CancellationTokenSource? _loading;

    public LeaderboardLoadingModel()
    {
        ReloadCommand = new Command(Reload);
        CancelCommand = new Command(() =>
        {
            _loading?.Cancel();
            IsLoading = false;
        });
        Reload();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsLoading
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            PropertyChanged?.Invoke(this, IsLoadingChanged);
        }
    }

    public ICommand ReloadCommand { get; }

    public ICommand CancelCommand { get; }

    public IReadOnlyList<LeaderboardEntry> Entries { get; } =
    [
        new(1, "Maya Chen", "North", "$48,210", "+12%"),
        new(2, "Tom Becker", "Central", "$45,900", "+8%"),
        new(3, "Amara Okafor", "South", "$41,330", "+5%"),
        new(4, "Luis Ortega", "West", "$39,870", "+3%"),
        new(5, "Hanna Nowak", "East", "$37,020", "+1%")
    ];

    public IReadOnlyList<LeaderboardPlaceholder> Placeholders { get; } =
    [
        new(100, 80, 60, 40), new(90, 70, 55, 35), new(110, 75, 58, 38), new(85, 65, 52, 32), new(95, 72, 56, 36)
    ];

    private async void Reload()
    {
        _loading?.Cancel();
        var loading = _loading = new CancellationTokenSource();
        IsLoading = true;
        try
        {
            await Task.Delay(2500, loading.Token);
            IsLoading = false;
        }
        catch (OperationCanceledException)
        {
            // Reloaded or cancelled meanwhile.
        }
    }
}
