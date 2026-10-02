using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// Composite buttons built from several Core nodes (a border holding a grid of an icon and labels, chips, tiles) that
/// show the look's press feedback as one control: <see cref="SkUiCoreNode.ShowsPressEffect"/> plus a
/// <see cref="SkUiCoreNode.Tapped"/> handler. A Core button inside a card takes its own presses. The press effect (dim
/// or ripple) and the transition speed are set on the Look &amp; colors page.
/// </summary>
public sealed class CorePressEffectDemoPage : ComponentDemoPage
{
    public CorePressEffectDemoPage()
        : base("Composite buttons", CreateHost(out var pressables, out var inner), native: null,
            widthRange: (220, 420, 340), heightRange: (260, 520, 420))
    {
        SinglePanelHeight = 440;
        var taps = new Dictionary<string, int>();
        foreach (var (name, node) in pressables)
            node.Tapped += (_, _) =>
            {
                taps[name] = taps.GetValueOrDefault(name) + 1;
                Feedback($"{name}: {taps[name]}");
            };
        inner.Clicked += (_, _) =>
        {
            taps["Details button"] = taps.GetValueOrDefault("Details button") + 1;
            Feedback($"Details button: {taps["Details button"]} (the card around it is not pressed)");
        };
        Toggle(nameof(SkUiCoreNode.ShowsPressEffect), true, value =>
        {
            foreach (var (_, node) in pressables)
                node.SetShowsPressEffect(value);
        }, () => pressables[0].Node.ShowsPressEffect);
        OnReset(taps.Clear);
    }

    private static SkUiCoreHost CreateHost(out List<(string Name, SkUiCoreNode Node)> pressables, out SkUiCoreButton inner)
    {
        pressables = [];
        var content = new SkUiCoreVerticalStackLayout().SetSpacing(12).SetPadding(new Thickness(12));

        // A settings row: icon, title and subtitle, chevron.
        var settings = Card(Row(Icon(DemoColors.Accent), "Notifications", "Sounds, badges, banners", Chevron()));
        pressables.Add(("Settings card", settings));
        content.Add(settings);

        // A card with its own button: pressing the button does not press the card.
        inner = new SkUiCoreButton();
        inner.SetText("Details").SetFontSize(13).SetWidth(76).SetHeight(34);
        var order = Card(Row(Icon(Color.FromArgb("#D97706")), "Order #1024", "Arrives tomorrow", inner));
        pressables.Add(("Order card", order));
        content.Add(order);

        // Chips: single labels with rounded chrome.
        var chips = new SkUiCoreHorizontalStackLayout().SetSpacing(8);
        foreach (var text in new[] { "All", "Unread", "Starred" })
        {
            var chip = new SkUiCoreLabel().SetText(text).SetFontSize(14).SetTextColor(DemoColors.Ink)
                .SetFillColor(DemoColors.SoftSurface).SetBorderColor(DemoColors.Border).SetBorderWidth(1)
                .SetCornerRadius(100).SetPadding(new Thickness(14, 6));
            chip.SetShowsPressEffect(true);
            pressables.Add(($"Chip {text}", chip));
            chips.Add(chip);
        }
        content.Add(chips);

        // Tiles: a border holding an icon over a caption.
        var tiles = new SkUiCoreGrid().SetColumnSpacing(12)
            .SetColumnDefinitions([new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star), new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star)])
            .SetRowDefinitions([new SkUiCoreRowDefinition(new SkUiCoreGridLength(96))]);
        var column = 0;
        foreach (var (text, color) in new[] { ("Photos", DemoColors.Accent), ("Files", Color.FromArgb("#6B3FA0")) })
        {
            var tile = new SkUiCoreGrid().SetRowSpacing(6)
                .SetRowDefinitions([new SkUiCoreRowDefinition(SkUiCoreGridLength.Star), new SkUiCoreRowDefinition(SkUiCoreGridLength.Auto)]);
            var icon = Icon(color);
            icon.SetHorizontalAlignment(LayoutAlignment.Center);
            icon.SetVerticalAlignment(LayoutAlignment.End);
            tile.Add(icon, 0, 0);
            tile.Add(new SkUiCoreLabel().SetText(text).SetFontSize(14).SetTextColor(DemoColors.Ink)
                .SetHorizontalTextAlignment(TextAlignment.Center), 1, 0);
            var card = Card(tile, padding: 10);
            pressables.Add(($"Tile {text}", card));
            tiles.Add(card, 0, column++);
        }
        content.Add(tiles);

        return new SkUiCoreHost().SetContent(new SkUiCoreScrollView().SetContent(content));
    }

    private static SkUiCoreBorder Card(SkUiCoreNode content, double padding = 12)
    {
        var card = new SkUiCoreBorder()
            .SetBackgroundColor(Colors.White)
            .SetStroke(DemoColors.Border)
            .SetStrokeThickness(1)
            .SetCornerRadius(12)
            .SetPadding(new Thickness(padding))
            .SetContent(content);
        card.SetShowsPressEffect(true);
        return card;
    }

    private static SkUiCoreGrid Row(SkUiCoreNode icon, string title, string subtitle, SkUiCoreNode trailing)
    {
        var texts = new SkUiCoreVerticalStackLayout().SetSpacing(2);
        texts.Add(new SkUiCoreLabel().SetText(title).SetFontSize(16).SetTextColor(DemoColors.Ink));
        texts.Add(new SkUiCoreLabel().SetText(subtitle).SetFontSize(13).SetTextColor(DemoColors.Caption));
        texts.SetVerticalAlignment(LayoutAlignment.Center);
        icon.SetVerticalAlignment(LayoutAlignment.Center);
        trailing.SetVerticalAlignment(LayoutAlignment.Center);
        var row = new SkUiCoreGrid().SetColumnSpacing(12)
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Auto),
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star),
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Auto)
            ])
            .SetRowDefinitions([new SkUiCoreRowDefinition(new SkUiCoreGridLength(44))]);
        row.Add(icon, 0, 0);
        row.Add(texts, 0, 1);
        row.Add(trailing, 0, 2);
        return row;
    }

    private static SkUiCoreEllipse Icon(Color color)
    {
        var icon = new SkUiCoreEllipse();
        icon.SetFill(color);
        icon.SetWidth(36);
        icon.SetHeight(36);
        return icon;
    }

    private static SkUiCoreLabel Chevron() =>
        new SkUiCoreLabel().SetText("›").SetFontSize(24).SetTextColor(DemoColors.Caption);
}
