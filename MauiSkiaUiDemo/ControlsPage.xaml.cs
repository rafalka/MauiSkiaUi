using System.Windows.Input;

namespace MauiSkiaUiDemo;

public partial class ControlsPage : ContentPage
{
    private int _observations;
    public ICommand AddObservationCommand { get; }
    public ICommand ResetCommand { get; }
    public string ObservationStatus => $"Observations: {_observations}";

    public ControlsPage()
    {
        AddObservationCommand = new Command(() => { _observations++; OnPropertyChanged(nameof(ObservationStatus)); });
        ResetCommand = new Command(() => { _observations = 0; OnPropertyChanged(nameof(ObservationStatus)); });
        InitializeComponent();
        BindingContext = this;
        Scroller.Scrolled += (_, args) => ScrollStatus.Text = $"Offset {args.ScrollY:F0}";
    }

    private void OnTopClicked(object? sender, EventArgs args) => Scroller.AnimateScrollTo(0, 0, TimeSpan.FromMilliseconds(350));

    protected override void OnDisappearing()
    {
        Scroller.ScrollTo(Scroller.ScrollX, Scroller.ScrollY);
        base.OnDisappearing();
    }
}