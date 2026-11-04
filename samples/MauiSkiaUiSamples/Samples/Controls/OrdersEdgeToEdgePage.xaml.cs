using System.ComponentModel;
using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Controls;

/// <summary>
/// HOWTO: an edge-to-edge page that follows the window's insets. Two things change at run time, for instance when the device
/// rotates (a phone's notch moves from the top to the side), and each has its own source:
/// <list type="number">
/// <item>The safe area: <see cref="SkUiView.SafeAreaInsets"/> of the surface root. It sets the bars' padding, so their content
/// stays clear of the notch while their background reaches the window's edge.</item>
/// <item>The bars' sizes, which follow from that padding and from their content: <c>SizeChanged</c>.</item>
/// </list>
/// Both end in the list's <see cref="SkUiCollectionView.ViewportInsets"/>: the bars' sizes at the top and the bottom, the safe
/// area at the sides (in landscape, rows and their buttons stay clear of the notch). The list keeps what it shows in place
/// when the insets change, so nothing here has to touch its scroll offset.
/// </summary>
public sealed partial class OrdersEdgeToEdgePage : ContentPage
{
    // What the bars keep around their content, inside the safe area.
    private const double BarMargin = 8;
    private const double BarSideMargin = 14;

    public OrdersEdgeToEdgePage(OrderListModel model)
    {
        InitializeComponent();
        BindingContext = model;
        // The first read of SafeAreaInsets (below) starts following the window; it changes when the root is laid out in a new
        // window size, so there is no rotation event to subscribe to.
        Root.PropertyChanged += OnRootPropertyChanged;
        TopBar.SizeChanged += OnBarSizeChanged;
        BottomBar.SizeChanged += OnBarSizeChanged;
        UpdateBars();
    }

    private void OnRootPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SkUiView.SafeAreaInsets))
            UpdateBars();
    }

    private void OnBarSizeChanged(object? sender, EventArgs args) => UpdateListInsets();

    /// <summary>The bars' backgrounds go edge to edge; their content stays inside the safe area.</summary>
    private void UpdateBars()
    {
        var safe = Root.SafeAreaInsets;
        TopBar.Padding = new Thickness(safe.Left + BarSideMargin, safe.Top + BarMargin, safe.Right + BarSideMargin, BarMargin);
        BottomBar.Padding = new Thickness(safe.Left + BarSideMargin, BarMargin, safe.Right + BarSideMargin, safe.Bottom + BarMargin);
        UpdateListInsets();
    }

    /// <summary>What covers the list: the bars at its ends, the safe area at its sides. A bar not laid out yet (-1) covers nothing.</summary>
    private void UpdateListInsets()
    {
        var safe = Root.SafeAreaInsets;
        Orders.ViewportInsets = new Thickness(safe.Left, Math.Max(0, TopBar.Height), safe.Right, Math.Max(0, BottomBar.Height));
        Readout.Text = $"Safe area {safe.Left:F0}, {safe.Top:F0}, {safe.Right:F0}, {safe.Bottom:F0} · list insets {Orders.ViewportInsets.Top:F0} / {Orders.ViewportInsets.Bottom:F0}";
    }

    private async void OnBackClicked(object? sender, EventArgs args) => await Navigation.PopAsync();
}
