namespace MauiSkiaUiSamples;

/// <summary>The examples of one section, or of every section with headers (overview).</summary>
public sealed class SampleListPage : ContentPage
{
    public SampleListPage(SampleSection? section)
    {
        Title = section?.DisplayName() ?? "Overview";
        BackgroundColor = SampleColors.Page;
        SampleColors.ApplyNavigationBar(this);
        var stack = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(16) };
        if (section is { } only)
            stack.Add(SampleText.Paragraph(only.Description(), 14, SampleColors.Caption));
        foreach (var group in SampleCatalog.All.GroupBy(entry => entry.Info.Section).OrderBy(group => group.Key))
        {
            if (section is { } shown && group.Key != shown)
                continue;
            if (section is null)
            {
                stack.Add(new Label { Text = group.Key.DisplayName(), FontFamily = SampleFonts.Semibold, FontSize = 18, TextColor = SampleColors.Ink, Margin = new Thickness(0, 8, 0, 0) });
                stack.Add(SampleText.Paragraph(group.Key.Description(), 13, SampleColors.Caption));
            }
            foreach (var entry in group)
                stack.Add(Row(entry));
        }
        Content = new ScrollView { Content = stack };
    }

    private View Row(SampleEntry entry)
    {
        var row = new Border
        {
            BackgroundColor = SampleColors.Surface,
            Stroke = SampleColors.Border,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Padding = new Thickness(14, 12),
            AutomationId = "Open" + Path.GetFileNameWithoutExtension(entry.Info.SourceFileName),
            Content = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label { Text = entry.Info.Title, FontFamily = SampleFonts.Semibold, FontSize = 16, TextColor = SampleColors.Ink },
                    SampleText.Paragraph(entry.Info.Summary, 13, SampleColors.Caption)
                }
            }
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await Navigation.PushAsync(entry.Create());
        row.GestureRecognizers.Add(tap);
        return row;
    }
}
