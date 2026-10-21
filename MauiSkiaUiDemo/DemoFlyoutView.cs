using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// The shell's flyout content, drawn: <see cref="DemoFlyout.Groups"/> under captions, one row per root page, the current
/// one highlighted. Shell's own flyout list separates groups with lines only.
/// </summary>
public sealed class DemoFlyoutView : SkUiScrollView
{
    private readonly Shell _shell;
    private readonly List<(ShellItem Item, SkUiGrid Row, SkUiLabel Text)> _rows = [];

    public DemoFlyoutView(Shell shell, IReadOnlyDictionary<DemoFlyoutEntry, ShellItem> items)
    {
        _shell = shell;
        Background = Colors.White;
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(0, 0, 0, 24) };
        foreach (var group in DemoFlyout.Groups)
        {
            var caption = new SkUiLabel
            {
                Text = group.Title, TextTransform = TextTransform.Uppercase, CharacterSpacing = 1, TextColor = DemoColors.Caption,
                FontFamily = DemoFonts.OpenSansSemibold, FontSize = 12, Padding = new Thickness(20, 16, 20, 4)
            };
            SemanticProperties.SetHeadingLevel(caption, SemanticHeadingLevel.Level2);
            stack.Children.Add(caption);
            foreach (var entry in group.Entries)
            {
                var item = items[entry];
                var text = new SkUiLabel { Text = entry.Title, TextColor = DemoColors.Ink, FontSize = 15, VerticalOptions = LayoutOptions.Center };
                var row = new SkUiGrid { Padding = new Thickness(20, 0), HeightRequest = 40, AutomationId = "Flyout" + entry.Route, Children = { text } };
                row.Tapped += (_, _) => Open(item);
                stack.Children.Add(row);
                _rows.Add((item, row, text));
            }
        }
        Content = stack;
        shell.Navigated += (_, _) => UpdateSelection();
        UpdateSelection();
    }

    private void Open(ShellItem item)
    {
        _shell.CurrentItem = item;
        _shell.FlyoutIsPresented = false;
    }

    private void UpdateSelection()
    {
        foreach (var (item, row, text) in _rows)
        {
            var current = item == _shell.CurrentItem;
            row.Background = current ? DemoColors.SoftSurface : Colors.Transparent;
            text.FontFamily = current ? DemoFonts.OpenSansSemibold : DemoFonts.OpenSansRegular;
        }
    }
}
