using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiEditor"/> (drawn multiline text input) and MAUI's <see cref="Editor"/>.</summary>
public sealed class EditorDemoPage : ComponentDemoPage
{
    private const string Placeholder = "Write a few lines";

    public EditorDemoPage() : base(nameof(SkUiEditor), new SkUiEditor { Placeholder = Placeholder, MinimumHeightRequest = 100 },
        new Editor { Placeholder = Placeholder, MinimumHeightRequest = 100 })
    {
        var skia = (SkUiEditor)SkiaControl;
        var native = (Editor)NativeControl!;
        skia.TextChanged += (_, args) => Feedback($"{args.NewTextValue?.Length ?? 0} characters (cursor {skia.CursorPosition})",
            $"{native.Text?.Length ?? 0} characters");
        native.TextChanged += (_, args) => Feedback($"{skia.Text?.Length ?? 0} characters", $"{args.NewTextValue?.Length ?? 0} characters");
        skia.Completed += (_, _) => Feedback("Completed (focus left)");
        native.Completed += (_, _) => Feedback($"{skia.Text?.Length ?? 0} characters", "Completed (focus left)");
        MultilineText(nameof(SkUiEditor.Text), string.Empty, value => { skia.Text = value; native.Text = value; }, () => skia.Text ?? string.Empty,
            () => native.Text ?? string.Empty);
        Choice(nameof(SkUiEditor.AutoSize), Enum.GetValues<EditorAutoSizeOption>(), EditorAutoSizeOption.Disabled,
            value => { skia.AutoSize = value; native.AutoSize = value; }, () => skia.AutoSize, () => native.AutoSize);
        Toggle(nameof(SkUiEditor.IsReadOnly), false, value => { skia.IsReadOnly = value; native.IsReadOnly = value; }, () => skia.IsReadOnly, () => native.IsReadOnly);
        Toggle(nameof(SkUiEditor.IsSpellCheckEnabled), true, value => { skia.IsSpellCheckEnabled = value; native.IsSpellCheckEnabled = value; },
            () => skia.IsSpellCheckEnabled, () => native.IsSpellCheckEnabled);
        Choice(nameof(SkUiEditor.HorizontalTextAlignment), [TextAlignment.Start, TextAlignment.Center, TextAlignment.End], TextAlignment.Start,
            value => { skia.HorizontalTextAlignment = value; native.HorizontalTextAlignment = value; }, () => skia.HorizontalTextAlignment, () => native.HorizontalTextAlignment);
        Number(nameof(SkUiEditor.FontSize), 10, 32, 16, value => { skia.FontSize = value; native.FontSize = value; }, () => skia.FontSize, () => native.FontSize);
        Number(nameof(SkUiEditor.MaxLength), 10, 500, 500, value =>
        {
            var length = value >= 500 ? int.MaxValue : (int)value;
            skia.MaxLength = length;
            native.MaxLength = length;
        }, () => Math.Min(500, skia.MaxLength), () => Math.Min(500, native.MaxLength), whole: true);
        ColorEditor(nameof(SkUiEditor.TextColor), Ink, value => { skia.TextColor = value; native.TextColor = value; }, () => skia.TextColor ?? Ink, () => native.TextColor ?? Ink);
        ActionButton("Fill with paragraphs", () =>
        {
            var text = string.Join("\n\n", Enumerable.Range(1, 6).Select(index =>
                $"Paragraph {index}: drawn text wraps at the editor's width, and the caret keeps in view while you type."));
            skia.Text = text;
            native.Text = text;
        });
    }
}
