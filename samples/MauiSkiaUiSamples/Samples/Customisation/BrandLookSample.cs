using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Customisation;

/// <summary>
/// HOWTO: start an app's own look from a platform look, change a few shapes and sizes, and check it against the native MAUI
/// controls it replaces with <see cref="SkUiLookComparisonPage"/>.
/// </summary>
public sealed class BrandLookSample : SamplePage, ISample
{
    private const string CompareTitle = "Compare with MAUI controls";
    private const string BrandLookName = "Brand look";
    private const string PayText = "Pay";

    public static SampleInfo Info { get; } = new(
        SampleSection.Customisation,
        Title: "Your own look, checked against MAUI",
        Summary: "A brand look derived from the iOS look: taller buttons with 12 pt corners and a thinner slider track. The " +
            "comparison page shows each SkiaUi control next to MAUI's native one with both sizes, side by side or overlaid.",
        HowTo:
        [
            "Derive from the look closest to your design (`IosSkUiLook`, `AndroidSkUiLook`, `AndroidMaterial3SkUiLook`, " +
                "`MacSkUiLook`, `WindowsSkUiLook` or `DefaultSkUiLook`) and override the sizes and shapes you change.",
            "Make it the app's look at startup: `UseSkiaUi(options => options.Look = new BrandLook())`.",
            "Push `new SkUiLookComparisonPage()` from a debug menu. `AddLook` lists your look in its picker, `AddRow` adds " +
                "your own pairs of styled controls.",
            "Run it on every platform you ship. A row's sizes turn green when both sides measure the same; \"Overlay\" draws " +
                "the SkiaUi control at half opacity over the native one.",
            "Turn \"App styles\" off to compare the platform's defaults, on to compare what your styled screens show."
        ],
        ThingsToKnow:
        [
            "The native column shows the platform the app runs on: compare the iOS look on iOS, the Material looks on Android.",
            "Each row's SkiaUi control is its own surface, which the page does on purpose to compare one control at a time; " +
                "real screens put one surface around a region.",
            "The page writes its report to the console (lines starting with `[SkUiLookComparison]`) and copies it with " +
                "\"Copy report\", so a script or a coding agent can compare looks too.",
            "Colors come from the color scheme, not the look: \"Look's colors\" shows the look's matching scheme while the page " +
                "is open."
        ]);

    private readonly BrandLook _look = new();

    public BrandLookSample() : base(Info) => SampleContent = Build();

    /// <inheritdoc />
    protected override SkUiLook Look => _look;

    private View Build()
    {
        var compare = new Button { Text = CompareTitle, HorizontalOptions = LayoutOptions.Start };
        compare.Clicked += async (_, _) => await Navigation.PushAsync(new SkUiLookComparisonPage(_look)
            .AddLook(BrandLookName, _look)
            // An app's own pair: a styled native button and the drawn one that replaces it.
            .AddRow("Pay button", () => new Button { Text = PayText, CornerRadius = 12, MinimumHeightRequest = 50 },
                () => new SkUiButton { Text = PayText }));
        return new VerticalStackLayout
        {
            Spacing = 16,
            Children =
            {
                new SkUiContentView
                {
                    Background = Colors.White,
                    Padding = new Thickness(16),
                    Content = new SkUiVerticalStackLayout
                    {
                        Spacing = 16,
                        Children =
                        {
                            new SkUiButton { Text = PayText, HorizontalOptions = LayoutOptions.Start },
                            new SkUiSlider { Maximum = 1, Value = 0.4 },
                            new SkUiSwitch { IsToggled = true, HorizontalOptions = LayoutOptions.Start }
                        }
                    }
                },
                compare
            }
        };
    }

    /// <summary>The brand's look: iOS's, with taller buttons, rounder corners and a thinner slider track.</summary>
    private sealed class BrandLook : IosSkUiLook
    {
        public override double DefaultButtonMinimumHeight => 50;

        public override double DefaultButtonCornerRadius => 12;

        public override Thickness DefaultButtonPadding => new(20, 12);

        public override float SliderTrackHeight => 4;
    }
}
