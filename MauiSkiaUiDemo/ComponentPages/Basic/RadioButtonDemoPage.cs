using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Side-by-side group of <see cref="SkUiRadioButton"/>s: three radio buttons with their <c>Content</c> (text, or a drawn view
/// of a swatch and a label) in a vertical stack, grouped with MAUI's <see cref="RadioButtonGroup"/> on the stack, and the
/// group's <c>SelectedValue</c> shown below them. Editors for the content's text properties and the border chrome (P9).
/// </summary>
/// <remarks>
/// The MAUI side uses <see cref="RadioButton.DefaultTemplate"/> on every platform (iOS and Mac Catalyst always do; Android
/// otherwise draws a native radio whose color cannot be set, and shows a view content as its type name). That template
/// takes its colors from the app resources <c>RadioButtonThemeColor</c> / <c>RadioButtonCheckMarkThemeColor</c> when it is
/// built, else white in dark mode, which would vanish on the white preview panel.
/// </remarks>
public sealed class RadioButtonDemoPage : ComponentDemoPage
{
    private static readonly string[] Values = ["Red", "Green", "Blue"];
    private static readonly Color[] Swatches = [Color.FromArgb("#C62828"), Color.FromArgb("#2E7D32"), Color.FromArgb("#1565C0")];
    private const string Initial = "Green";
    private const string TextContent = "Text";
    private const string ViewContent = "Drawn view (swatch + label)";

    public RadioButtonDemoPage() : base(nameof(SkUiRadioButton), CreateSkiaGroup(out var skiaRadios, out var skiaSelected), CreateNativeGroup(out var nativeRadios, out var nativeSelected),
        widthRange: (120, 320, 220), heightRange: (120, 360, 240))
    {
        var skia = (SkUiVerticalStackLayout)SkiaControl;
        var native = (VerticalStackLayout)NativeControl!;
        ShowSelectedValue(skia, value => skiaSelected.Text = value);
        ShowSelectedValue(native, value => nativeSelected.Text = value);
        RadioButtonGroup.SetSelectedValue(skia, Initial);
        RadioButtonGroup.SetSelectedValue(native, Initial);

        void Each(Action<SkUiRadioButton> skiaApply, Action<RadioButton> nativeApply)
        {
            foreach (var radio in skiaRadios) skiaApply(radio);
            foreach (var radio in nativeRadios) nativeApply(radio);
        }

        Choice(nameof(SkUiRadioButton.Content), [TextContent, ViewContent], TextContent, kind =>
        {
            for (var index = 0; index < Values.Length; index++)
            {
                skiaRadios[index].Content = kind == TextContent ? Values[index] : CreateSkiaContent(index);
                nativeRadios[index].Content = kind == TextContent ? Values[index] : CreateNativeContent(index);
            }
        }, () => skiaRadios[0].Content is string ? TextContent : ViewContent, () => nativeRadios[0].Content is string ? TextContent : ViewContent);
        ColorEditor(nameof(SkUiRadioButton.Color), Accent, value =>
        {
            foreach (var radio in skiaRadios) radio.Color = value;
            ApplyNativeColor(nativeRadios, value);
        }, () => skiaRadios[0].Color);
        ColorEditor(nameof(SkUiRadioButton.TextColor), Ink, value => Each(radio => radio.TextColor = value, radio => radio.TextColor = value),
            () => skiaRadios[0].TextColor, () => nativeRadios[0].TextColor);
        Number(nameof(SkUiRadioButton.FontSize), 10, 28, 15, value => Each(radio => radio.FontSize = value, radio => radio.FontSize = value),
            () => skiaRadios[0].FontSize, () => nativeRadios[0].FontSize);
        Number(nameof(SkUiRadioButton.CharacterSpacing), 0, 6, 0, value => Each(radio => radio.CharacterSpacing = value, radio => radio.CharacterSpacing = value),
            () => skiaRadios[0].CharacterSpacing, () => nativeRadios[0].CharacterSpacing);
        Choice(nameof(SkUiRadioButton.TextTransform), [TextTransform.Default, TextTransform.Uppercase, TextTransform.Lowercase], TextTransform.Default,
            value => Each(radio => radio.TextTransform = value, radio => radio.TextTransform = value), () => skiaRadios[0].TextTransform, () => nativeRadios[0].TextTransform);
        Number(nameof(SkUiRadioButton.BorderWidth), 0, 6, 0, value => Each(radio => radio.BorderWidth = value, radio => radio.BorderWidth = value),
            () => skiaRadios[0].BorderWidth, () => nativeRadios[0].BorderWidth);
        ColorEditor(nameof(SkUiRadioButton.BorderColor), Ink, value => Each(radio => radio.BorderColor = value, radio => radio.BorderColor = value),
            () => skiaRadios[0].BorderColor, () => nativeRadios[0].BorderColor);
        // MAUI's default template applies Padding as the content's margin (the circle keeps its place); SkiaUi insets
        // circle and content inside the border.
        Number(nameof(SkUiRadioButton.Padding), 0, 16, 0, value => Each(radio => radio.Padding = new Thickness(value), radio => radio.Padding = new Thickness(value)),
            () => skiaRadios[0].Padding.Left, () => nativeRadios[0].Padding.Left);
        Number(nameof(SkUiRadioButton.CornerRadius), 0, 24, 0, value => Each(radio => radio.CornerRadius = (int)Math.Round(value), radio => radio.CornerRadius = (int)Math.Round(value)),
            () => skiaRadios[0].CornerRadius, () => nativeRadios[0].CornerRadius, whole: true);
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

    private static SkUiHorizontalStackLayout CreateSkiaContent(int index) => new()
    {
        Spacing = 6,
        Children =
        {
            new SkUiBox { Color = Swatches[index], WidthRequest = 14, HeightRequest = 14, CornerRadius = 3, VerticalOptions = LayoutOptions.Center },
            new SkUiLabel { Text = Values[index], TextColor = Ink, FontSize = 15, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }
        }
    };

    private static HorizontalStackLayout CreateNativeContent(int index) => new()
    {
        Spacing = 6,
        Children =
        {
            new BoxView { Color = Swatches[index], WidthRequest = 14, HeightRequest = 14, CornerRadius = 3, VerticalOptions = LayoutOptions.Center },
            new Label { Text = Values[index], TextColor = Ink, FontSize = 15, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }
        }
    };

    // The radio buttons share the stack with the selection label; the group is named by RadioButtonGroup.GroupName on the
    // stack, as in MAUI.
    private static SkUiVerticalStackLayout CreateSkiaGroup(out SkUiRadioButton[] radios, out SkUiLabel selected)
    {
        var stack = new SkUiVerticalStackLayout { Spacing = 8 };
        RadioButtonGroup.SetGroupName(stack, "SkiaColors");
        radios = new SkUiRadioButton[Values.Length];
        for (var index = 0; index < Values.Length; index++)
        {
            var radio = radios[index] = new SkUiRadioButton
            {
                Content = Values[index], Value = Values[index], TextColor = Ink, FontSize = 15, AutomationId = "SkiaRadio" + Values[index]
            };
            stack.Children.Add(radio);
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
