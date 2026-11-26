using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary><see cref="SkUiChip"/> (1.18 K, #130): one chip with editors for its kind, selection, remove button, badge, image and colors.</summary>
public sealed class ChipsDemoPage : ComponentDemoPage
{
    public ChipsDemoPage() : base(nameof(SkUiChip), new SkUiChip { Text = "Overtime", Kind = SkUiChipKind.Filter })
    {
        var chip = (SkUiChip)SkiaControl;
        chip.Clicked += (_, _) => Feedback($"Clicked (selected: {chip.IsSelected})");
        chip.Removed += (_, _) => Feedback("Removed");
        Choice(nameof(SkUiChip.Kind), Enum.GetValues<SkUiChipKind>(), SkUiChipKind.Filter, value => chip.Kind = value, () => chip.Kind);
        Toggle(nameof(SkUiChip.IsSelected), false, value => chip.IsSelected = value, () => chip.IsSelected);
        Toggle(nameof(SkUiChip.ShowsRemoveButton), false, value => chip.ShowsRemoveButton = value, () => chip.ShowsRemoveButton);
        Text(nameof(SkUiChip.BadgeText), "", value => chip.BadgeText = string.IsNullOrEmpty(value) ? null : value, () => chip.BadgeText ?? "");
        ColorEditor(nameof(SkUiChip.BadgeColor), SkUiColors.ButtonBackground, value => chip.BadgeColor = value, () => chip.BadgeColor ?? SkUiColors.ButtonBackground);
        ColorEditor(nameof(SkUiChip.BadgeTextColor), SkUiColors.ButtonForeground, value => chip.BadgeTextColor = value,
            () => chip.BadgeTextColor ?? SkUiColors.ButtonForeground);
        Number(nameof(SkUiChip.BadgeFontSize), 8, 24, SkUiLook.Current.ChipBadgeFontSize, value => chip.BadgeFontSize = value,
            () => chip.BadgeFontSize < 0 ? SkUiLook.Current.ChipBadgeFontSize : chip.BadgeFontSize);
        Choice(nameof(SkUiChip.BadgeFontAttributes), [FontAttributes.None, FontAttributes.Bold, FontAttributes.Italic], FontAttributes.Bold,
            value => chip.BadgeFontAttributes = value, () => chip.BadgeFontAttributes);
        Toggle("Image", false, value => chip.ImageSource = value ? new FontImageSource { Glyph = "★", FontFamily = DemoFonts.OpenSansSemibold, Color = Accent, Size = 16 } : null,
            () => chip.ImageSource is not null);
        ColorEditor(nameof(SkUiChip.SelectedColor), SkUiColors.Accent.WithAlpha(0.2f), value => chip.SelectedColor = value, () => chip.SelectedColor ?? Colors.Transparent);
    }
}
