using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Side-by-side group of <see cref="SkUiRadioButton"/>s: three radio buttons in rows of a vertical stack, grouped with MAUI's
/// <see cref="RadioButtonGroup"/> on the stack, and the group's <c>SelectedValue</c> shown below them.
/// </summary>
/// <remarks>
/// The MAUI side uses <see cref="RadioButton.DefaultTemplate"/> on every platform (iOS and Mac Catalyst always do; Android
/// otherwise draws a native radio whose color cannot be set). That template takes its colors from the app resources
/// <c>RadioButtonThemeColor</c> / <c>RadioButtonCheckMarkThemeColor</c> when it is built, else white in dark mode, which
/// would vanish on the white preview panel.
/// </remarks>
public sealed class RadioButtonDemoPage : ComponentDemoPage
{
    private static readonly string[] Values = ["Red", "Green", "Blue"];
    private const string Initial = "Green";

    public RadioButtonDemoPage() : base(nameof(SkUiRadioButton), CreateSkiaGroup(out var skiaRadios, out var skiaSelected), CreateNativeGroup(out var nativeRadios, out var nativeSelected),
        widthRange: (120, 320, 200), heightRange: (120, 320, 220))
    {
        var skia = (SkUiVerticalStackLayout)SkiaControl;
        var native = (VerticalStackLayout)NativeControl!;
        ShowSelectedValue(skia, value => skiaSelected.Text = value);
        ShowSelectedValue(native, value => nativeSelected.Text = value);
        RadioButtonGroup.SetSelectedValue(skia, Initial);
        RadioButtonGroup.SetSelectedValue(native, Initial);

        ColorEditor(nameof(SkUiRadioButton.Color), Accent, value =>
        {
            foreach (var radio in skiaRadios) radio.Color = value;
            ApplyNativeColor(nativeRadios, value);
        }, () => skiaRadios[0].Color);
        ActionButton("Clear selection", () =>
        {
            RadioButtonGroup.SetSelectedValue(skia, null);
            RadioButtonGroup.SetSelectedValue(native, null);
        });
        OnReset(() =>
        {
            RadioButtonGroup.SetSelectedValue(skia, Initial);
            RadioButtonGroup.SetSelectedValue(native, Initial);
        });
    }

    /// <summary>
    /// Sets the template colors (app-wide resources: only this page shows MAUI radio buttons) and rebuilds the templates,
    /// whose checked state captured the old colors.
    /// </summary>
    private static void ApplyNativeColor(RadioButton[] radios, Color color)
    {
        if (Application.Current is not { } app)
            return; // headless tests
        app.Resources["RadioButtonThemeColor"] = new SolidColorBrush(color);
        app.Resources["RadioButtonCheckMarkThemeColor"] = new SolidColorBrush(color);
        foreach (var radio in radios)
        {
            radio.ControlTemplate = null;
            radio.ControlTemplate = RadioButton.DefaultTemplate;
        }
    }

    private static void ShowSelectedValue(BindableObject group, Action<string> show)
    {
        void Update() => show($"SelectedValue: {RadioButtonGroup.GetSelectedValue(group) ?? "(none)"}");
        group.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == RadioButtonGroup.SelectedValueProperty.PropertyName)
                Update();
        };
        Update();
    }

    // Each radio button sits in its own row with a label, so they have different parents: the group needs a name, here
    // given by RadioButtonGroup.GroupName on the stack, as in MAUI.
    private static SkUiVerticalStackLayout CreateSkiaGroup(out SkUiRadioButton[] radios, out SkUiLabel selected)
    {
        var stack = new SkUiVerticalStackLayout { Spacing = 8 };
        RadioButtonGroup.SetGroupName(stack, "SkiaColors");
        radios = new SkUiRadioButton[Values.Length];
        for (var index = 0; index < Values.Length; index++)
        {
            var radio = radios[index] = new SkUiRadioButton { Value = Values[index], AutomationId = "SkiaRadio" + Values[index] };
            var row = new SkUiHorizontalStackLayout { Spacing = 8 };
            row.Children.Add(radio);
            row.Children.Add(new SkUiLabel { Text = Values[index], TextColor = Ink, FontSize = 15, VerticalOptions = LayoutOptions.Center });
            stack.Children.Add(row);
        }
        selected = new SkUiLabel { TextColor = DemoColors.Caption, FontSize = 13, AutomationId = "SkiaSelectedValue" };
        stack.Children.Add(selected);
        return stack;
    }

    private static VerticalStackLayout CreateNativeGroup(out RadioButton[] radios, out Label selected)
    {
        var stack = new VerticalStackLayout { Spacing = 8 };
        RadioButtonGroup.SetGroupName(stack, "NativeColors");
        radios = new RadioButton[Values.Length];
        for (var index = 0; index < Values.Length; index++)
        {
            // Overrides of the app's implicit RadioButton style: its text is white in dark mode (the preview panel is
            // always white), and its 44-DIP minimum size (touch target) would space the rows much wider than the SkUi side.
            var radio = radios[index] = new RadioButton
            {
                Content = Values[index], Value = Values[index], TextColor = Ink, FontSize = 15,
                MinimumHeightRequest = 0, MinimumWidthRequest = 0,
                ControlTemplate = RadioButton.DefaultTemplate, AutomationId = "NativeRadio" + Values[index],
            };
            stack.Children.Add(radio);
        }
        selected = new Label { TextColor = DemoColors.Caption, FontSize = 13, AutomationId = "NativeSelectedValue" };
        stack.Children.Add(selected);
        return stack;
    }
}
