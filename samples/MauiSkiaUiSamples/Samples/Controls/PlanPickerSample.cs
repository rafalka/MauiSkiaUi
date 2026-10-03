using MauiSkiaUi;
using Microsoft.Maui.Controls.Shapes;
using ImagePosition = Microsoft.Maui.Controls.Button.ButtonContentLayout.ImagePosition;

namespace MauiSkiaUiSamples.Samples.Controls;

/// <summary>
/// HOWTO: radio buttons with a label or rich content, radio buttons drawn by your own template, and buttons with an icon.
/// <see cref="SkUiRadioButton.Content"/> takes text (drawn with <c>TextColor</c>, the font properties, <c>CharacterSpacing</c>
/// and <c>TextTransform</c>) or a drawn view; <see cref="SkUiRadioButton.ControlTemplate"/> replaces the circle with any
/// drawn tree, where an <see cref="SkUiContentPresenter"/> shows the content and the root gets the <c>Checked</c> /
/// <c>Unchecked</c> visual states. <see cref="SkUiButton.ImageSource"/> with <see cref="SkUiButton.ContentLayout"/> puts an
/// image beside a button's text, as MAUI's Button.
/// <para>
/// The page shows a billing period picked with plain radio buttons, plan tiles (a template: a rounded card whose border and
/// check mark follow the checked state, the plan's name and price as content), the chosen values, and a toolbar of icon
/// buttons with the icon on each side.
/// </para>
/// </summary>
public sealed class PlanPickerSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Plan picker and icon buttons",
        Summary: "Radio buttons with text, plan tiles drawn by a ControlTemplate, and buttons with an icon beside the text.",
        HowTo:
        [
            "Label a radio button: `<sk:SkUiRadioButton Content=\"Monthly\" Value=\"Monthly\" />`; style the text with " +
                "`TextColor`, `FontSize`, `FontAttributes`, `TextTransform`.",
            "Rich content: put a drawn view inside it, e.g. `<sk:SkUiRadioButton><sk:SkUiVerticalStackLayout>…</sk:SkUiVerticalStackLayout></sk:SkUiRadioButton>`.",
            "Your own look: a `ControlTemplate` whose root is a drawn view (`SkUiBorder`, `SkUiGrid`, …) with an " +
                "`SkUiContentPresenter` for the content; react to the `Checked` / `Unchecked` visual states on the root.",
            "Group them with `RadioButtonGroup.GroupName` and bind `RadioButtonGroup.SelectedValue` on the layout, as in MAUI.",
            "An icon button: `ImageSource` (a file, a `MauiImage`, a `FontImageSource` glyph) and `ContentLayout=\"Top, 6\"`."
        ],
        ThingsToKnow:
        [
            "`{TemplateBinding}` and `RelativeSource TemplatedParent` do not reach drawn controls; bind with " +
                "`RelativeSource AncestorType={x:Type sk:SkUiRadioButton}` instead. The template root inherits the binding context.",
            "`BorderColor`, `BorderWidth`, `CornerRadius` and `Padding` outline a radio button without a template.",
            "A button image keeps its size and is only scaled down to fit; the spacing applies only when there is text, and " +
                "Left / Right swap in right-to-left layouts.",
            "The whole radio button is the tap target, also its content."
        ]);

    private static readonly (string Name, string Price, string Detail)[] Plans =
    [
        ("Basic", "€4", "1 user"), ("Pro", "€12", "5 users"), ("Team", "€30", "Unlimited")
    ];

    private readonly SkUiLabel _summary = new() { FontSize = 14, TextColor = SampleColors.Caption };
    private readonly SkUiVerticalStackLayout _periods = new() { Spacing = 6 };
    private readonly SkUiHorizontalStackLayout _plans = new() { Spacing = 10 };

    public PlanPickerSample() : base(Info) => SampleContent = Build();

    private View Build()
    {
        var column = new SkUiVerticalStackLayout { Spacing = 16, Padding = new Thickness(16), BackgroundColor = SampleColors.Surface };

        // Plain radio buttons with text content.
        RadioButtonGroup.SetGroupName(_periods, "period");
        foreach (var period in new[] { "Monthly", "Yearly (2 months free)" })
            _periods.Children.Add(new SkUiRadioButton { Content = period, Value = period, TextColor = SampleColors.Ink, FontSize = 15, Color = SampleColors.Accent });
        RadioButtonGroup.SetSelectedValue(_periods, "Monthly");
        column.Children.Add(Heading("Billing"));
        column.Children.Add(_periods);

        // Tiles: the same radio buttons, drawn by a template.
        RadioButtonGroup.SetGroupName(_plans, "plan");
        foreach (var plan in Plans)
            _plans.Children.Add(new SkUiRadioButton { Value = plan.Name, ControlTemplate = TileTemplate, Content = PlanContent(plan) });
        RadioButtonGroup.SetSelectedValue(_plans, "Pro");
        column.Children.Add(Heading("Plan"));
        column.Children.Add(_plans);

        foreach (var group in new BindableObject[] { _periods, _plans })
            group.PropertyChanged += (_, args) => { if (args.PropertyName == RadioButtonGroup.SelectedValueProperty.PropertyName) UpdateSummary(); };
        UpdateSummary();
        column.Children.Add(_summary);

        // Icon buttons: a glyph beside, above and after the text, and alone.
        var toolbar = new SkUiHorizontalStackLayout { Spacing = 8 };
        toolbar.Children.Add(IconButton("Add", "+", ImagePosition.Left));
        toolbar.Children.Add(IconButton("Star", "★", ImagePosition.Top));
        toolbar.Children.Add(IconButton("Next", "›", ImagePosition.Right));
        toolbar.Children.Add(IconButton("", "?", ImagePosition.Left));
        column.Children.Add(Heading("Icon buttons"));
        column.Children.Add(toolbar);
        return column;
    }

    private void UpdateSummary() =>
        _summary.Text = $"{RadioButtonGroup.GetSelectedValue(_plans) ?? "No plan"}, billed {RadioButtonGroup.GetSelectedValue(_periods) ?? "–"}";

    private static SkUiLabel Heading(string text) =>
        new() { Text = text, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Caption, TextTransform = TextTransform.Uppercase };

    private static SkUiButton IconButton(string text, string glyph, ImagePosition position) => new()
    {
        Text = text,
        FillColor = SampleColors.Accent,
        CornerRadius = 8,
        Padding = new Thickness(12, 8),
        ImageSource = new FontImageSource { Glyph = glyph, Size = 18, Color = Colors.White },
        ContentLayout = new Button.ButtonContentLayout(position, 6),
    };

    private static SkUiVerticalStackLayout PlanContent((string Name, string Price, string Detail) plan) => new()
    {
        Spacing = 2,
        VerticalOptions = LayoutOptions.End,
        Children =
        {
            new SkUiLabel { Text = plan.Name, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Ink },
            new SkUiLabel { Text = plan.Price + " / month", FontSize = 13, TextColor = SampleColors.Ink },
            new SkUiLabel { Text = plan.Detail, FontSize = 12, TextColor = SampleColors.Caption },
        }
    };

    /// <summary>A rounded card: the border turns the accent color and a check dot appears while checked.</summary>
    private static readonly ControlTemplate TileTemplate = new(() =>
    {
        var check = new SkUiEllipse { Fill = SampleColors.Accent, WidthRequest = 10, HeightRequest = 10, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        var root = new SkUiBorder
        {
            Stroke = SampleColors.Border, StrokeThickness = 2, StrokeShape = new RoundRectangle { CornerRadius = 12 }, Padding = new Thickness(10),
            BackgroundColor = SampleColors.Surface, WidthRequest = 104, HeightRequest = 104,
            Content = new SkUiGrid
            {
                Children =
                {
                    new SkUiGrid
                    {
                        WidthRequest = 18, HeightRequest = 18, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start,
                        Children =
                        {
                            new SkUiEllipse { Stroke = SampleColors.Border, StrokeThickness = 2, Fill = Colors.White },
                            check
                        }
                    },
                    new SkUiContentPresenter()
                }
            }
        };
        // The states' setters find the dot by name.
        Microsoft.Maui.Controls.Internals.INameScope scope = new Microsoft.Maui.Controls.Internals.NameScope();
        Microsoft.Maui.Controls.Internals.NameScope.SetNameScope(root, scope);
        scope.RegisterName("check", check);
        var isChecked = new VisualState { Name = SkUiRadioButton.CheckedVisualState };
        isChecked.Setters.Add(new Setter { Property = SkUiBorder.StrokeProperty, Value = new SolidColorBrush(SampleColors.Accent) });
        isChecked.Setters.Add(new Setter { TargetName = "check", Property = VisualElement.OpacityProperty, Value = 1d });
        var isUnchecked = new VisualState { Name = SkUiRadioButton.UncheckedVisualState };
        isUnchecked.Setters.Add(new Setter { TargetName = "check", Property = VisualElement.OpacityProperty, Value = 0d });
        var group = new VisualStateGroup { Name = "CheckedStates" };
        group.States.Add(isChecked);
        group.States.Add(isUnchecked);
        VisualStateManager.SetVisualStateGroups(root, [group]);
        return root;
    });
}
