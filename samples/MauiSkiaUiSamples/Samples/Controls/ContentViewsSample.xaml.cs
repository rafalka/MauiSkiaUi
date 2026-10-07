using System.ComponentModel;
using System.Windows.Input;
using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Controls;

/// <summary>
/// HOWTO: <see cref="SkUiContentView"/> in XAML. The page's XAML (ContentViewsSample.xaml) shows:
/// <list type="number">
/// <item>A templated control: <see cref="InfoCard"/> is a <see cref="SkUiContentView"/> with two bindable properties; an
/// implicit style gives every card a <see cref="ControlTemplate"/> (a header with the card's title, an
/// <see cref="SkUiContentPresenter"/> for the card's own content), and each card sets its content inline.</item>
/// <item>Swapping the template: a <c>DataTrigger</c> sets another template; the content moves into the new template's
/// presenter, it is not recreated.</item>
/// <item><see cref="SkUiContentView.ContentTemplate"/> with a <see cref="DataTemplateSelector"/>: the card's binding
/// context is an order status, and the selector picks a content template by its type. A status of the same type keeps
/// the content (it rebinds); one of another type replaces it.</item>
/// <item><see cref="SkUiContentView.ContentLoading"/> <c>WhenShown</c>: a hidden card creates neither its template nor its
/// content until it is first shown, then fades in.</item>
/// </list>
/// Template bindings to the card use <c>RelativeSource AncestorType</c>: MAUI's <c>TemplateBinding</c> only reaches its
/// own templated views. The XAML source generator compiles them with <see cref="InfoCard"/> as the source type.
/// </summary>
public sealed partial class ContentViewsSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Content views and templates",
        Summary: "SkUiContentView in XAML: a card control whose look comes from a ControlTemplate in a style, content " +
                 "chosen by a template selector, and a card created only when it is first shown.",
        HowTo:
        [
            "A templated control: derive from `SkUiContentView` and add bindable properties (`Title`, `Accent`).",
            "Its look: a `ControlTemplate` of drawn views with an `<sk:SkUiContentPresenter />` where the content goes, set " +
                "by a style: `<Setter Property=\"ControlTemplate\" Value=\"{StaticResource CardTemplate}\" />`.",
            "In the template, bind to the control by ancestor type: " +
                "`{Binding Title, Source={RelativeSource AncestorType={x:Type local:InfoCard}}}`.",
            "Content per instance: put a drawn view inside the control, or set `ContentTemplate` (a `DataTemplate` or a " +
                "`DataTemplateSelector`, which chooses by the control's `BindingContext`).",
            "Defer a pane: `ContentLoading=\"WhenShown\"`, optionally with `ContentLoadedAnimation=\"FadeIn 250\"`."
        ],
        ThingsToKnow:
        [
            "`{TemplateBinding}` and `RelativeSource TemplatedParent` do not reach drawn controls. The ancestor-type binding " +
                "is compiled (XAML source generator), so it needs no reflection.",
            "Unlike MAUI, the template root inherits the control's binding context: `{Binding X}` in a template reaches the " +
                "view model.",
            "Explicit `Content` wins over `ContentTemplate`. Content goes into the template's first presenter; without a " +
                "presenter it is not shown and `ContentTemplate` does not run.",
            "With `WhenShown`, the template is deferred with the content, and the view measures as its size requests until " +
                "then.",
            "Templates must create drawn views; put native ones inside `SkUiMauiContentView`."
        ]);

    public ContentViewsSample() : base(Info)
    {
        InitializeComponent();
        BindingContext = new ContentViewsModel();
    }
}

/// <summary>A card: <see cref="Title"/> and <see cref="Accent"/> are shown by its template, the content below them.</summary>
public class InfoCard : SkUiContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(InfoCard), string.Empty);

    public static readonly BindableProperty AccentProperty =
        BindableProperty.Create(nameof(Accent), typeof(Color), typeof(InfoCard), SampleColors.Accent);

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public Color Accent
    {
        get => (Color)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }
}

/// <summary>Picks the content template of an order status by its type.</summary>
public sealed class OrderStatusTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Packing { get; set; }
    public DataTemplate? Shipped { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
        (item is ShippedStatus ? Shipped : Packing)!;
}

public abstract record OrderStatus(string Title);

public sealed record PackingStatus(int Packed, int Total) : OrderStatus("Packing")
{
    public double Progress => (double)Packed / Total;
    public string Text => $"{Packed} of {Total} items packed";
}

public sealed record ShippedStatus(string Carrier, string TrackingNumber) : OrderStatus("Shipped");

public sealed class ContentViewsModel : INotifyPropertyChanged
{
    private static readonly OrderStatus[] Statuses =
    [
        new PackingStatus(1, 3), new PackingStatus(3, 3), new ShippedStatus("Parcel Express", "PX 4071 2290")
    ];

    private int _index;

    public ContentViewsModel() => NextStatusCommand = new Command(() =>
    {
        _index = (_index + 1) % Statuses.Length;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
    });

    public event PropertyChangedEventHandler? PropertyChanged;

    public OrderStatus Status => Statuses[_index];

    public ICommand NextStatusCommand { get; }
}
