using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// The same controls under each platform look at once (<see cref="SkUiView.Look"/> on one border per look), next to MAUI's
/// native controls on the platform the demo runs on. The color scheme is the app's; each section only paints its own
/// background from the matching scheme.
/// </summary>
public sealed class LooksSideBySidePage : ContentPage
{
    private const string NativeTitle = "Native MAUI controls";

    public LooksSideBySidePage()
    {
        Title = "Looks side by side";
        var stack = new VerticalStackLayout { Padding = 16, Spacing = 16 };
        foreach (var (name, look) in new (string, DefaultSkUiLook)[]
                 {
                     ("Default look", DefaultSkUiLook.Instance), ("Android (Material 3)", AndroidSkUiLook.Instance),
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
        stack.Add(NativeSection());
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

    private static Border NativeSection() => new()
    {
        Padding = 12,
        StrokeThickness = 1,
        Content = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = NativeTitle, FontAttributes = FontAttributes.Bold },
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
