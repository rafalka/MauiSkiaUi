using System.Diagnostics;
using MauiSkiaUi;
using SkiaSharp;

namespace MauiSkiaUiDemo;

public sealed class StressPage : ContentPage
{
    private readonly SkUiScrollView scroller;
    private readonly Label metrics;
    private readonly Label selected;
    private IDisposable? motion;

    public StressPage()
    {
        Title = "1,000 hosted controls";
        Background = Color.FromArgb("#F4F6F6");
        var grid = new SkUiGrid { ColumnSpacing = 8, RowSpacing = 4, Padding = new Thickness(8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        selected = new Label { Text = "No selection", TextColor = Color.FromArgb("#202A2C"), FontFamily = "OpenSansRegular" };
        for (var index = 0; index < 1000; index++)
        {
            if (index % 2 == 0) grid.RowDefinitions.Add(new RowDefinition(new GridLength(48)));
            var itemNumber = index + 1;
            var button = new SkUiButton
            {
                Text = $"Item {itemNumber:0000}", FontSize = 14, Padding = new Thickness(6),
                FillColor = index % 4 < 2 ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#E5EEEE"),
                TextColor = Color.FromArgb("#202A2C"), BorderColor = Color.FromArgb("#C5D4D6"), BorderWidth = 1,
                AutomationId = $"StressItem{itemNumber}",
                Command = new Command(() => selected.Text = $"Selected item {itemNumber:0000}")
            };
            Grid.SetRow(button, index / 2);
            Grid.SetColumn(button, index % 2);
            grid.Children.Add(button);
        }
        scroller = new SkUiScrollView { Content = grid, AutomationId = "StressScroller", Background = Colors.White };
        metrics = new Label { Text = "1,000 controls / one surface", TextColor = Color.FromArgb("#526164"), FontSize = 13 };
        var layout = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)], Padding = 16, RowSpacing = 10 };
        layout.Add(metrics);
        layout.Add(scroller, 0, 1);
        layout.Add(selected, 0, 2);
        Content = layout;
        ToolbarItems.Add(new ToolbarItem("Record", null, MeasureRecording));
        ToolbarItems.Add(new ToolbarItem("Scroll", null, StartScroll));
        ToolbarItems.Add(new ToolbarItem("Top", null, () => scroller.ScrollTo(0, 0)));
    }

    private void StartScroll()
    {
        motion?.Dispose();
        scroller.ScrollTo(0, 0);
        motion = scroller.AnimateScrollTo(0, Math.Max(0, scroller.ContentSize.Height - scroller.Height), TimeSpan.FromSeconds(12));
    }

    private void MeasureRecording()
    {
        if (scroller.Width <= 0 || scroller.Height <= 0) return;
        using var recorder = new SKPictureRecorder();
        void Record()
        {
            var canvas = recorder.BeginRecording(new SKRect(0, 0, (float)scroller.Width, (float)scroller.Height));
            scroller.Paint(canvas);
            using var picture = recorder.EndRecording();
        }
        Record();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        for (var frame = 0; frame < 30; frame++) Record();
        timer.Stop();
        var bytes = (GC.GetAllocatedBytesForCurrentThread() - allocated) / 30;
        metrics.Text = $"CPU record: {timer.Elapsed.TotalMilliseconds / 30:F2} ms / {bytes:N0} B per frame";
    }

    protected override void OnDisappearing()
    {
        motion?.Dispose();
        scroller.ScrollTo(scroller.ScrollX, scroller.ScrollY);
        base.OnDisappearing();
    }
}