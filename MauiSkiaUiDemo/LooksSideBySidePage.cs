using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// The same controls under each platform look at once (<see cref="SkUiView.Look"/> on one border per look), next to MAUI's
/// native controls on the platform the demo runs on. The color scheme is the app's; each section only paints its own
/// background from the matching scheme.
/// </summary>
public sealed class LooksSideBySidePage : ContentPage
{
    private const string NativeTitle = "Native MAUI controls (app styles)";
    private const string UnstyledNativeTitle = "Native MAUI controls (platform defaults)";

    public LooksSideBySidePage()
    {
        Title = "Looks side by side";
        var stack = new VerticalStackLayout { Padding = 16, Spacing = 16 };
        // A subtree's look shapes how it measures and draws; transitions (presses, toggles) run with the app's look.
        stack.Add(new Label { Text = "Each look's shapes and sizes. Press and toggle animations use the app's look, so motion is the same in every section." });
        stack.Add(UnstyledNativeSection());
        stack.Add(NativeSection(NativeTitle));
        foreach (var (name, look) in new (string, DefaultSkUiLook)[]
                 {
                     ("Default look", DefaultSkUiLook.Instance), ("Android (Material 2)", AndroidSkUiLook.Instance),
                     ("Android (Material 3)", AndroidMaterial3SkUiLook.Instance),
                     ("iOS", IosSkUiLook.Instance), ("macOS", MacSkUiLook.Instance), ("Windows (Fluent)", WindowsSkUiLook.Instance)
                 })
        {
            stack.Add(new SkUiBorder
            {
                Look = look,
                Padding = 12,
                StrokeThickness = 1,
                CornerRadius = 8,
                BackgroundColor = look.GetColorScheme(false).DefaultBackground,
                Content = Section(name, look)
            });
        }
        Content = new ScrollView { Content = stack };
    }

    private static SkUiVerticalStackLayout Section(string name, DefaultSkUiLook look)
    {
        var scheme = look.GetColorScheme(false);
        return new SkUiVerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new SkUiLabel { Text = name, FontAttributes = FontAttributes.Bold, TextColor = scheme.DefaultForeground },
                new SkUiButton { Text = "Button", BackgroundColor = scheme.Accent, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Start },
                new SkUiSwitch { IsToggled = true, OnColor = scheme.Accent, HorizontalOptions = LayoutOptions.Start },
                new SkUiCheckBox { IsChecked = true, Color = scheme.Accent, HorizontalOptions = LayoutOptions.Start },
                new SkUiRadioButton { Content = "Radio button", IsChecked = true, TextColor = scheme.DefaultForeground },
                new SkUiSlider { Value = 0.4, Maximum = 1, MinimumTrackColor = scheme.Accent, ThumbColor = scheme.Accent },
                new SkUiProgressBar { Progress = 0.6, ProgressColor = scheme.Accent },
                new SkUiActivityIndicator { IsRunning = true, Color = scheme.Accent, HorizontalOptions = LayoutOptions.Start }
            }
        };
    }

    /// <summary>
    /// Native controls without the app's implicit styles (empty implicit styles here win over the app's), so they show the
    /// platform's own defaults, which is what the platform looks mimic.
    /// </summary>
    private static Border UnstyledNativeSection()
    {
        var section = NativeSection(UnstyledNativeTitle);
        foreach (var type in new[] { typeof(Label), typeof(Button), typeof(Switch), typeof(CheckBox), typeof(RadioButton), typeof(Slider), typeof(ProgressBar), typeof(ActivityIndicator), typeof(Border) })
            section.Resources.Add(new Style(type));
        return section;
    }

    private static Border NativeSection(string title) => new()
    {
        Padding = 12,
        StrokeThickness = 1,
        Content = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = title, FontAttributes = FontAttributes.Bold },
                new Button { Text = "Button", HorizontalOptions = LayoutOptions.Start },
                new Switch { IsToggled = true, HorizontalOptions = LayoutOptions.Start },
                new CheckBox { IsChecked = true, HorizontalOptions = LayoutOptions.Start },
                new RadioButton { Content = "Radio button", IsChecked = true },
                new Slider { Value = 0.4, Maximum = 1 },
                new ProgressBar { Progress = 0.6 },
                new ActivityIndicator { IsRunning = true, HorizontalOptions = LayoutOptions.Start }
            }
        }
    };
}
