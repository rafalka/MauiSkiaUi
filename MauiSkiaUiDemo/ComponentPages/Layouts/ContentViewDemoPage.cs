using System.ComponentModel;
using System.Diagnostics;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Property playground for <see cref="SkUiContentView"/>: padding, background and content, plus
/// <see cref="SkUiContentView.ContentTemplate"/> and <see cref="SkUiContentView.ContentLoading"/>. Three tabs of
/// sections whose content comes from a template that runs only when a tab is first shown; the status line counts the
/// sections created, and each creation is written to the trace output (IDE output window, device log). Bindings show
/// how the binding context flows: the header binds to the page's model, each section's template to its own model.
/// </summary>
public sealed class ContentViewDemoPage : ComponentDemoPage
{
    private const int TabCount = 3;
    private const int SectionsPerTab = 4;
    private static readonly SkUiViewAnimation SectionFadeIn = SkUiViewAnimation.FadeIn(200);
    private readonly PageModel _model = new();
    private readonly SkUiVerticalStackLayout _body = new() { Spacing = 8 };
    private readonly SkUiGrid _panes = new();
    private readonly Stopwatch _sinceBuilt = new();
    private bool _deferred = true;
    private bool _fadeIn = true;
    private double _delay;
    private int _tab;
    private int _created;

    public ContentViewDemoPage()
        : base(nameof(SkUiContentView), new SkUiContentView(), widthRange: (200, 360, 300), heightRange: (220, 520, 400))
    {
        var view = (SkUiContentView)SkiaControl;
        view.BindingContext = _model;
        var header = new SkUiLabel { TextColor = Ink, FontSize = 16 };
        header.SetBinding(SkUiLabel.TextProperty, new Binding(nameof(PageModel.Title)));
        var tabs = new SkUiHorizontalStackLayout { Spacing = 6 };
        for (var tab = 0; tab < TabCount; tab++)
        {
            var index = tab;
            var button = new SkUiButton { Text = $"Tab {tab + 1}", FontSize = 13 };
            button.Clicked += (_, _) => ShowTab(index);
            tabs.Children.Add(button);
        }
        _body.Children.Add(header);
        _body.Children.Add(tabs);
        _body.Children.Add(_panes);
        view.Content = _body;

        Number(nameof(SkUiContentView.Padding), 0, 28, 12, value => view.Padding = value, () => view.Padding.Left);
        ColorEditor(nameof(VisualElement.Background), DemoColors.SoftSurface, value => view.Background = value, () => ((SolidColorBrush)view.Background).Color);
        Toggle("HasContent", true, value => view.Content = value ? _body : null, () => view.Content is not null);
        Text("Title", "Quarterly report", value => _model.Title = value, () => _model.Title);
        Toggle(nameof(SkUiContentLoading.WhenShown), true, value => { _deferred = value; Rebuild(); }, () => _deferred);
        Number("DelayMs", 0, 1000, 0, value => { _delay = value; Rebuild(); }, () => _delay, whole: true);
        Toggle("FadeIn", true, value => { _fadeIn = value; Rebuild(); }, () => _fadeIn);
    }

    private void ShowTab(int tab)
    {
        _tab = tab;
        for (var index = 0; index < _panes.Children.Count; index++)
            ((SkUiView)_panes.Children[index]).IsVisible = index == tab;
    }

    // Loading settings apply before a section is shown, so every change builds the tabs again.
    private void Rebuild()
    {
        _created = 0;
        _sinceBuilt.Restart();
        Trace.WriteLine($"ContentViewDemo: tabs built (deferred {_deferred}, delay {_delay:0} ms, fade-in {_fadeIn})");
        _panes.Children.Clear();
        var template = new DataTemplate(Section);
        for (var tab = 0; tab < TabCount; tab++)
        {
            var pane = new SkUiVerticalStackLayout { Spacing = 8, IsVisible = tab == _tab };
            for (var index = 0; index < SectionsPerTab; index++)
            {
                pane.Children.Add(new SkUiContentView
                {
                    ContentLoading = _deferred ? SkUiContentLoading.WhenShown : SkUiContentLoading.Immediate,
                    ContentLoadingDelay = TimeSpan.FromMilliseconds(_delay),
                    ContentLoadedAnimation = _fadeIn ? SectionFadeIn : null,
                    HeightRequest = 64,
                    BindingContext = new SectionModel(tab * SectionsPerTab + index + 1, tab + 1),
                    ContentTemplate = template
                });
            }
            _panes.Children.Add(pane);
        }
        Report();
    }

    // Stands for an expensive section (a chart, a card with images). Its views bind to the section's model, which they
    // inherit from the section once the template's content is attached.
    private SkUiBorder Section()
    {
        _created++;
        Trace.WriteLine($"ContentViewDemo: created a section {_sinceBuilt.ElapsedMilliseconds} ms after the tabs were built; {_created} created");
        Report();
        var title = new SkUiLabel { TextColor = Ink, FontSize = 14, VerticalOptions = LayoutOptions.Center };
        title.SetBinding(SkUiLabel.TextProperty, new Binding(nameof(SectionModel.Title)));
        var bars = new SkUiHorizontalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.End };
        for (var bar = 0; bar < 8; bar++)
        {
            var box = new SkUiBox { WidthRequest = 10, VerticalOptions = LayoutOptions.End };
            box.SetBinding(SkUiBox.ColorProperty, new Binding(nameof(SectionModel.Color)));
            box.SetBinding(HeightRequestProperty, new Binding($"{nameof(SectionModel.Bars)}[{bar}]"));
            bars.Children.Add(box);
        }
        var border = new SkUiBorder { StrokeThickness = 2, CornerRadius = 10, Padding = new Thickness(10, 6), Background = Colors.White, Content = new SkUiGrid { Children = { title, bars } } };
        border.SetBinding(SkUiBorder.StrokeProperty, new Binding(nameof(SectionModel.Color)));
        return border;
    }

    private void Report() => Feedback($"Created {_created} of {TabCount * SectionsPerTab} sections");

    private sealed class PageModel : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs TitleChanged = new(nameof(Title));

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Title
        {
            get => field;
            set { field = value; PropertyChanged?.Invoke(this, TitleChanged); }
        } = "Quarterly report";
    }

    private sealed class SectionModel(int number, int tab)
    {
        public string Title { get; } = $"Section {number} (tab {tab})";
        public Color Color { get; } = number % 3 == 0 ? Accent : number % 3 == 1 ? DemoColors.SampleA : DemoColors.SampleB;
        public double[] Bars { get; } = [.. Enumerable.Range(0, 8).Select(bar => 8d + (number * 7 + bar * 11) % 30)];
    }
}
