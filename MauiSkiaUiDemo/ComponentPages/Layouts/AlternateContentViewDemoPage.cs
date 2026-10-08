using System.ComponentModel;
using System.Diagnostics;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Property playground for <see cref="SkUiAlternateContentView"/>: a read card (<c>Content</c>) and an edit card
/// created from <c>AlternateContentTemplate</c> the first time it is shown (logged to the trace output), switched by
/// <c>ShowsAlternate</c> with optional animations. Both sides bind to the same model.
/// </summary>
public sealed class AlternateContentViewDemoPage : ComponentDemoPage
{
    private static readonly string[] States = ["null", "false", "true"];
    private static readonly string[] Animations = ["None", "Fade", "Slide"];
    private static readonly SkUiViewAnimation SlideOut = new([new(SkUiAnimatableProperty.TranslationX, to: -24), new(SkUiAnimatableProperty.Opacity, to: 0)], 150, Easing.CubicIn);
    private static readonly SkUiViewAnimation SlideIn = new([new(SkUiAnimatableProperty.TranslationX, from: 24), new(SkUiAnimatableProperty.Opacity, from: 0)], 220, Easing.CubicOut);
    private readonly PersonModel _model = new();
    private int _editCardsCreated;

    public AlternateContentViewDemoPage()
        : base(nameof(SkUiAlternateContentView), new SkUiAlternateContentView(), widthRange: (160, 360, 260), heightRange: (80, 240, 120))
    {
        var view = (SkUiAlternateContentView)SkiaControl;
        view.BindingContext = _model;
        view.Content = Card(DemoColors.SampleA, "Viewing", nameof(PersonModel.Name));
        view.AlternateContentTemplate = new DataTemplate(() =>
        {
            _editCardsCreated++;
            Trace.WriteLine($"AlternateContentViewDemo: edit card created ({_editCardsCreated})");
            return Card(Accent, "Editing", nameof(PersonModel.Name));
        });
        view.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SkUiAlternateContentView.ShowsAlternate))
                Report(view);
        };

        Choice(nameof(SkUiAlternateContentView.ShowsAlternate), States, "false",
            value => view.ShowsAlternate = value == "null" ? null : value == "true", () => view.ShowsAlternate?.ToString().ToLowerInvariant() ?? "null");
        Choice("Animation", Animations, "None", value => SetAnimation(view, value), () => AnimationName(view));
        Text("Name", _model.Name, value => _model.Name = value, () => _model.Name);
    }

    private static SkUiBorder Card(Color color, string caption, string path)
    {
        var name = new SkUiLabel { TextColor = Ink, FontSize = 16 };
        name.SetBinding(SkUiLabel.TextProperty, new Binding(path));
        return new SkUiBorder
        {
            Stroke = color, StrokeThickness = 2, CornerRadius = 10, Padding = new Thickness(12, 8), Background = Colors.White,
            Content = new SkUiVerticalStackLayout { Spacing = 4, Children = { new SkUiLabel { Text = caption, TextColor = color, FontSize = 12 }, name } }
        };
    }

    private static void SetAnimation(SkUiAlternateContentView view, string name)
    {
        (view.BeforeStateChangeAnimation, view.AfterStateChangeAnimation) = name switch
        {
            "Fade" => (SkUiViewAnimation.FadeOut(), SkUiViewAnimation.FadeIn()),
            "Slide" => (SlideOut, SlideIn),
            _ => ((SkUiViewAnimation?)null, (SkUiViewAnimation?)null)
        };
    }

    private static string AnimationName(SkUiAlternateContentView view) => view.AfterStateChangeAnimation switch
    {
        null => "None",
        var after when ReferenceEquals(after, SlideIn) => "Slide",
        _ => "Fade"
    };

    private void Report(SkUiAlternateContentView view) =>
        Feedback($"ShowsAlternate {view.ShowsAlternate?.ToString() ?? "null"} · edit cards created: {_editCardsCreated}");

    private sealed class PersonModel : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs NameChanged = new(nameof(Name));

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name
        {
            get => field;
            set { field = value; PropertyChanged?.Invoke(this, NameChanged); }
        } = "Ada Lovelace";
    }
}
