using System.Diagnostics;
using MauiSkiaUi;
using SkiaSharp;

namespace MauiSkiaUiDemo;

public sealed class StressPage : ContentPage
{
    private readonly SkUiScrollView _scroller;
    private readonly Label _metrics;
    private readonly Label _selected;
    private IDisposable? _motion;

    public StressPage()
    {
        Title = "1,000 hosted controls";
        Background = DemoColors.PageBackground;
        var grid = new SkUiGrid { ColumnSpacing = 8, RowSpacing = 4, Padding = new Thickness(8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        _selected = new Label { Text = "No selection", TextColor = DemoColors.Ink, FontFamily = DemoFonts.OpenSansRegular };
        for (var index = 0; index < 1000; index++)
        {
            if (index % 2 == 0) grid.RowDefinitions.Add(new RowDefinition(new GridLength(48)));
            var itemNumber = index + 1;
            var button = new SkUiButton
            {
                Text = $"Item {itemNumber:0000}", FontSize = 14, Padding = new Thickness(6),
                FillColor = index % 4 < 2 ? Colors.White : DemoColors.StressAlt,
                TextColor = DemoColors.Ink, BorderColor = DemoColors.Border, BorderWidth = 1,
                AutomationId = $"StressItem{itemNumber}",
                Command = new Command(() => _selected.Text = $"Selected item {itemNumber:0000}")
            };
            Grid.SetRow(button, index / 2);
            Grid.SetColumn(button, index % 2);
            grid.Children.Add(button);
        }
        _scroller = new SkUiScrollView { Content = grid, AutomationId = "StressScroller", Background = Colors.White };
        _metrics = new Label { Text = "1,000 controls / one surface", TextColor = DemoColors.Caption, FontSize = 13 };
        var layout = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)], Padding = 16, RowSpacing = 10 };
        layout.Add(_metrics);
        layout.Add(_scroller, 0, 1);
        layout.Add(_selected, 0, 2);
        Content = layout;
        ToolbarItems.Add(new ToolbarItem("Record", null, MeasureRecording));
        ToolbarItems.Add(new ToolbarItem("Scroll", null, StartScroll));
        ToolbarItems.Add(new ToolbarItem("Top", null, () => _scroller.ScrollTo(0, 0)));
    }

    private void StartScroll()
    {
        _motion?.Dispose();
        _scroller.ScrollTo(0, 0);
        _motion = _scroller.AnimateScrollTo(0, Math.Max(0, _scroller.ContentSize.Height - _scroller.Height), TimeSpan.FromSeconds(12));
    }

    private void MeasureRecording()
    {
        if (_scroller.Width <= 0 || _scroller.Height <= 0) return;
        using var recorder = new SKPictureRecorder();
        void Record()
        {
            var canvas = recorder.BeginRecording(new SKRect(0, 0, (float)_scroller.Width, (float)_scroller.Height));
            _scroller.Paint(canvas);
            using var picture = recorder.EndRecording();
        }
        Record();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        for (var frame = 0; frame < 30; frame++) Record();
        timer.Stop();
        var bytes = (GC.GetAllocatedBytesForCurrentThread() - allocated) / 30;
        _metrics.Text = $"CPU record: {timer.Elapsed.TotalMilliseconds / 30:F2} ms / {bytes:N0} B per frame";
    }

    protected override void OnDisappearing()
    {
        _motion?.Dispose();
        _scroller.ScrollTo(_scroller.ScrollX, _scroller.ScrollY);
        base.OnDisappearing();
    }
}