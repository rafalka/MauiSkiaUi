using MauiSkiaUi;
using Microsoft.Maui.Controls.Internals;
using Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiRadioButton.ControlTemplate"/> with an <see cref="SkUiContentPresenter"/>: the tile template of MAUI's
/// RadioButton docs, built from drawn views (border, grid, ellipses, presenter) next to the same template of MAUI views.
/// The template root gets the <c>Checked</c> / <c>Unchecked</c> states, which recolor the stroke and show the check dot.
/// </summary>
public sealed class RadioButtonTemplateDemoPage : ComponentDemoPage
{
    private static readonly string[] Values = ["Cat", "Dog", "Fish"];
    private const string Initial = "Dog";
    private const string TextContent = "Text";
    private const string ViewContent = "Drawn view (image + label)";
    private static readonly Color TileColor = Color.FromArgb("#F3F2F1");
    private static readonly Color CheckedStroke = Color.FromArgb("#FF3300");

    public RadioButtonTemplateDemoPage() : base(nameof(SkUiContentPresenter), CreateSkiaGroup(out var skiaRadios), CreateNativeGroup(out var nativeRadios),
        widthRange: (200, 360, 320), heightRange: (100, 220, 120))
    {
        var skia = (SkUiHorizontalStackLayout)SkiaControl;
        var native = (HorizontalStackLayout)NativeControl!;
        RadioButtonGroup.SetSelectedValue(skia, Initial);
        RadioButtonGroup.SetSelectedValue(native, Initial);

        Toggle(nameof(SkUiRadioButton.ControlTemplate), true, value =>
        {
            foreach (var radio in skiaRadios) radio.ControlTemplate = value ? SkiaTemplate : null;
            foreach (var radio in nativeRadios) radio.ControlTemplate = value ? NativeTemplate : RadioButton.DefaultTemplate;
        }, () => skiaRadios[0].ControlTemplate is not null, () => nativeRadios[0].ControlTemplate == NativeTemplate);
        Choice(nameof(SkUiRadioButton.Content), [TextContent, ViewContent], ViewContent, kind =>
        {
            for (var index = 0; index < Values.Length; index++)
            {
                skiaRadios[index].Content = kind == TextContent ? Values[index] : CreateSkiaContent(Values[index]);
                nativeRadios[index].Content = kind == TextContent ? Values[index] : CreateNativeContent(Values[index]);
            }
        }, () => skiaRadios[0].Content is string ? TextContent : ViewContent, () => nativeRadios[0].Content is string ? TextContent : ViewContent);
        ColorEditor(nameof(SkUiRadioButton.TextColor), Ink, value =>
        {
            foreach (var radio in skiaRadios) radio.TextColor = value;
            foreach (var radio in nativeRadios) radio.TextColor = value;
        }, () => skiaRadios[0].TextColor, () => nativeRadios[0].TextColor);
        OnReset(() =>
        {
            RadioButtonGroup.SetSelectedValue(skia, Initial);
            RadioButtonGroup.SetSelectedValue(native, Initial);
        });
    }

    private static readonly ControlTemplate SkiaTemplate = new(() =>
    {
        var check = new SkUiEllipse { Fill = Colors.Blue, WidthRequest = 8, HeightRequest = 8, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        var root = new SkUiBorder
        {
            Stroke = TileColor, StrokeThickness = 2, StrokeShape = new RoundRectangle { CornerRadius = 10 }, BackgroundColor = TileColor,
            WidthRequest = 90, HeightRequest = 90, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
            Content = new SkUiGrid
            {
                Margin = 4,
                Children =
                {
                    new SkUiGrid
                    {
                        Margin = new Thickness(0, 0, 4, 0), WidthRequest = 18, HeightRequest = 18, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start,
                        Children =
                        {
                            new SkUiEllipse { Stroke = Colors.Blue, Fill = Colors.White, WidthRequest = 16, HeightRequest = 16, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
                            check
                        }
                    },
                    new SkUiContentPresenter()
                }
            }
        };
        Name(root, check);
        VisualStateManager.SetVisualStateGroups(root, CheckedStates(SkUiBorder.StrokeProperty));
        return root;
    });

    private static readonly ControlTemplate NativeTemplate = new(() =>
    {
        var check = new Ellipse { Fill = Colors.Blue, WidthRequest = 8, HeightRequest = 8, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        var root = new Border
        {
            Stroke = TileColor, StrokeThickness = 2, StrokeShape = new RoundRectangle { CornerRadius = 10 }, BackgroundColor = TileColor,
            WidthRequest = 90, HeightRequest = 90, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
            Content = new Grid
            {
                Margin = 4,
                Children =
                {
                    new Grid
                    {
                        Margin = new Thickness(0, 0, 4, 0), WidthRequest = 18, HeightRequest = 18, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start,
                        Children =
                        {
                            new Ellipse { Stroke = Colors.Blue, Fill = Colors.White, WidthRequest = 16, HeightRequest = 16, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
                            check
                        }
                    },
                    new ContentPresenter()
                }
            }
        };
        Name(root, check);
        VisualStateManager.SetVisualStateGroups(root, CheckedStates(Border.StrokeProperty));
        return root;
    });

    private static void Name(BindableObject root, Element check)
    {
        INameScope scope = new NameScope();
        NameScope.SetNameScope(root, scope);
        scope.RegisterName("check", check);
    }

    /// <summary>The docs sample's states: a red stroke and the dot while checked.</summary>
    private static VisualStateGroupList CheckedStates(BindableProperty stroke)
    {
        var isChecked = new VisualState { Name = SkUiRadioButton.CheckedVisualState };
        isChecked.Setters.Add(new Setter { Property = stroke, Value = new SolidColorBrush(CheckedStroke) });
        isChecked.Setters.Add(new Setter { TargetName = "check", Property = VisualElement.OpacityProperty, Value = 1d });
        var isUnchecked = new VisualState { Name = SkUiRadioButton.UncheckedVisualState };
        isUnchecked.Setters.Add(new Setter { Property = stroke, Value = new SolidColorBrush(TileColor) });
        isUnchecked.Setters.Add(new Setter { TargetName = "check", Property = VisualElement.OpacityProperty, Value = 0d });
        var group = new VisualStateGroup { Name = "CheckedStates" };
        group.States.Add(isChecked);
        group.States.Add(isUnchecked);
        return [group];
    }

    private static SkUiVerticalStackLayout CreateSkiaContent(string value) => new()
    {
        Spacing = 4, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
        Children =
        {
            new SkUiImage { Source = DemoAssets.BadgeImage, WidthRequest = 32, HeightRequest = 32, HorizontalOptions = LayoutOptions.Center },
            new SkUiLabel { Text = value, TextColor = Ink, FontSize = 14, HorizontalOptions = LayoutOptions.Center }
        }
    };

    private static VerticalStackLayout CreateNativeContent(string value) => new()
    {
        Spacing = 4, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
        Children =
        {
            new Image { Source = DemoAssets.BadgeImage, WidthRequest = 32, HeightRequest = 32, HorizontalOptions = LayoutOptions.Center },
            new Label { Text = value, TextColor = Ink, FontSize = 14, HorizontalOptions = LayoutOptions.Center }
        }
    };

    private static SkUiHorizontalStackLayout CreateSkiaGroup(out SkUiRadioButton[] radios)
    {
        var stack = new SkUiHorizontalStackLayout { Spacing = 8 };
        RadioButtonGroup.SetGroupName(stack, "SkiaAnimals");
        radios = new SkUiRadioButton[Values.Length];
        for (var index = 0; index < Values.Length; index++)
        {
            var radio = radios[index] = new SkUiRadioButton
            {
                Value = Values[index], TextColor = Ink, FontSize = 14, ControlTemplate = SkiaTemplate, Content = CreateSkiaContent(Values[index]),
                AutomationId = "SkiaTile" + Values[index]
            };
            stack.Children.Add(radio);
        }
        return stack;
    }

    private static HorizontalStackLayout CreateNativeGroup(out RadioButton[] radios)
    {
        var stack = new HorizontalStackLayout { Spacing = 8 };
        RadioButtonGroup.SetGroupName(stack, "NativeAnimals");
        radios = new RadioButton[Values.Length];
        for (var index = 0; index < Values.Length; index++)
        {
            var radio = radios[index] = new RadioButton
            {
                Value = Values[index], TextColor = Ink, FontSize = 14, MinimumHeightRequest = 0, MinimumWidthRequest = 0,
                ControlTemplate = NativeTemplate, Content = CreateNativeContent(Values[index]), AutomationId = "NativeTile" + Values[index]
            };
            stack.Children.Add(radio);
        }
        return stack;
    }
}
