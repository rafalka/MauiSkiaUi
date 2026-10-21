using MauiSkiaUi;

namespace MauiSkiaUiSamples;

/// <summary>The examples of one section, or of every section with headers (overview); one drawn surface.</summary>
public sealed class SampleListPage : ContentPage
{
    public SampleListPage(SampleSection? section)
    {
        Title = section?.DisplayName() ?? "Overview";
        BackgroundColor = SampleColors.Page;
        SampleColors.ApplyNavigationBar(this);
        var stack = new SkUiVerticalStackLayout { Spacing = 10, Padding = new Thickness(16) };
        if (section is { } only)
            stack.Children.Add(SampleText.Paragraph(only.Description(), 14, SampleColors.Caption));
        foreach (var group in SampleCatalog.All.GroupBy(entry => entry.Info.Section).OrderBy(group => group.Key))
        {
            if (section is { } shown && group.Key != shown)
                continue;
            if (section is null)
            {
                stack.Children.Add(new SkUiLabel { Text = group.Key.DisplayName(), FontFamily = SampleFonts.Semibold, FontSize = 18, TextColor = SampleColors.Ink, Margin = new Thickness(0, 8, 0, 0) });
                stack.Children.Add(SampleText.Paragraph(group.Key.Description(), 13, SampleColors.Caption));
            }
            foreach (var entry in group)
                stack.Children.Add(Row(entry));
        }
        Content = new SkUiScrollView { Content = stack, Background = SampleColors.Page };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        SampleLooks.Show(null); // back from an example with its own look
    }

    private SkUiBorder Row(SampleEntry entry)
    {
        var row = new SkUiBorder
        {
            BackgroundColor = SampleColors.Surface,
            Stroke = SampleColors.Border,
            StrokeThickness = 1,
            CornerRadius = 8,
            ShowsPressEffect = true,
            Padding = new Thickness(14, 12),
            AutomationId = "Open" + Path.GetFileNameWithoutExtension(entry.Info.SourceFileName),
            Content = new SkUiVerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new SkUiLabel { Text = entry.Info.Title, FontFamily = SampleFonts.Semibold, FontSize = 16, TextColor = SampleColors.Ink },
                    SampleText.Paragraph(entry.Info.Summary, 13, SampleColors.Caption)
                }
            }
        };
        row.Tapped += async (_, _) => await Navigation.PushAsync(entry.Create());
        return row;
    }
}
