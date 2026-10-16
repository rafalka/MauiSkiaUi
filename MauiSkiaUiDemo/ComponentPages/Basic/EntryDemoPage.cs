using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiEntry"/> (drawn text input) and MAUI's <see cref="Entry"/>.</summary>
public sealed class EntryDemoPage : ComponentDemoPage
{
    private const string Placeholder = "Type here";
    private const string NoFormatters = "None";
    private const string DigitsOnly = "Digits only";
    private const string PhoneMask = "Phone (000) 000-0000";
    private const string CardNumber = "Card 0000 0000 0000 0000";
    private const string NoSpaces = "No spaces, lower case";
    private const string PhoneMaskText = "(000) 000-0000";
    private const string CardMaskText = "0000 0000 0000 0000";

    public EntryDemoPage() : base(nameof(SkUiEntry), new SkUiEntry { Placeholder = Placeholder }, new Entry { Placeholder = Placeholder })
    {
        var skia = (SkUiEntry)SkiaControl;
        var native = (Entry)NativeControl!;
        skia.TextChanged += (_, args) => Feedback($"Text \"{args.NewTextValue}\" (cursor {skia.CursorPosition}, selection {skia.SelectionLength})",
            $"Text \"{native.Text}\"");
        native.TextChanged += (_, args) => Feedback($"Text \"{skia.Text}\"", $"Text \"{args.NewTextValue}\"");
        skia.Completed += (_, _) => Feedback($"Completed: \"{skia.Text}\"");
        native.Completed += (_, _) => Feedback($"Text \"{skia.Text}\"", $"Completed: \"{native.Text}\"");
        Text(nameof(SkUiEntry.Text), string.Empty, value => { skia.Text = value; native.Text = value; }, () => skia.Text ?? string.Empty, () => native.Text ?? string.Empty);
        Text(nameof(SkUiEntry.Placeholder), Placeholder, value => { skia.Placeholder = value; native.Placeholder = value; }, () => skia.Placeholder ?? string.Empty,
            () => native.Placeholder ?? string.Empty);
        Toggle(nameof(SkUiEntry.IsPassword), false, value => { skia.IsPassword = value; native.IsPassword = value; }, () => skia.IsPassword, () => native.IsPassword);
        Toggle(nameof(SkUiEntry.IsReadOnly), false, value => { skia.IsReadOnly = value; native.IsReadOnly = value; }, () => skia.IsReadOnly, () => native.IsReadOnly);
        Choice(nameof(SkUiEntry.Keyboard), ["Default", "Text", "Numeric", "Telephone", "Email", "Url", "Chat", "Plain"], "Default",
            value => { skia.Keyboard = KeyboardOf(value); native.Keyboard = KeyboardOf(value); }, () => NameOf(skia.Keyboard), () => NameOf(native.Keyboard));
        Choice(nameof(SkUiEntry.ReturnType), Enum.GetValues<ReturnType>(), ReturnType.Default,
            value => { skia.ReturnType = value; native.ReturnType = value; }, () => skia.ReturnType, () => native.ReturnType);
        Choice(nameof(SkUiEntry.ClearButtonVisibility), Enum.GetValues<ClearButtonVisibility>(), ClearButtonVisibility.Never,
            value => { skia.ClearButtonVisibility = value; native.ClearButtonVisibility = value; }, () => skia.ClearButtonVisibility, () => native.ClearButtonVisibility);
        Choice(nameof(SkUiEntry.HorizontalTextAlignment), [TextAlignment.Start, TextAlignment.Center, TextAlignment.End], TextAlignment.Start,
            value => { skia.HorizontalTextAlignment = value; native.HorizontalTextAlignment = value; }, () => skia.HorizontalTextAlignment, () => native.HorizontalTextAlignment);
        Choice(nameof(SkUiEntry.TextTransform), Enum.GetValues<TextTransform>(), TextTransform.Default,
            value => { skia.TextTransform = value; native.TextTransform = value; }, () => skia.TextTransform, () => native.TextTransform);
        Number(nameof(SkUiEntry.MaxLength), 1, 40, 40, value =>
        {
            var length = value >= 40 ? int.MaxValue : (int)value;
            skia.MaxLength = length;
            native.MaxLength = length;
        }, () => Math.Min(40, skia.MaxLength), () => Math.Min(40, native.MaxLength), whole: true);
        Number(nameof(SkUiEntry.FontSize), 10, 32, 16, value => { skia.FontSize = value; native.FontSize = value; }, () => skia.FontSize, () => native.FontSize);
        Number(nameof(SkUiEntry.CharacterSpacing), 0, 6, 0, value => { skia.CharacterSpacing = value; native.CharacterSpacing = value; },
            () => skia.CharacterSpacing, () => native.CharacterSpacing);
        Choice(nameof(SkUiEntry.InputFormatters), [NoFormatters, DigitsOnly, PhoneMask, CardNumber, NoSpaces], NoFormatters,
            value => skia.SetInputFormatters(FormattersOf(value)), () => NameOf(skia.InputFormatters));
        Toggle(nameof(SkUiEntry.FormatsAssignedText), false, value => skia.FormatsAssignedText = value, () => skia.FormatsAssignedText);
        ColorEditor(nameof(SkUiEntry.TextColor), Ink, value => { skia.TextColor = value; native.TextColor = value; }, () => skia.TextColor ?? Ink, () => native.TextColor ?? Ink);
        ColorEditor(nameof(SkUiEntry.CaretColor), Accent, value => skia.CaretColor = value, () => skia.CaretColor ?? Accent);
        ActionButton("Select all", () =>
        {
            skia.CursorPosition = 0;
            skia.SelectionLength = skia.Text?.Length ?? 0;
            native.CursorPosition = 0;
            native.SelectionLength = native.Text?.Length ?? 0;
        });
        ActionButton("Focus", () => skia.Focus());
    }

    // SkiaUi only (MAUI's Entry has no formatters): filters, a mask, a digit cap with groups, a function.
    private static SkUiTextInputFormatter[] FormattersOf(string name) => name switch
    {
        DigitsOnly => [SkUiFilteringFormatter.DigitsOnly],
        PhoneMask => [new SkUiMaskFormatter(PhoneMaskText)],
        CardNumber => [new SkUiMaskFormatter(CardMaskText)],
        NoSpaces => [new SkUiFilteringFormatter(@"\s", SkUiFilterMode.Deny),
            SkUiTextInputFormatter.Create((_, value) => value with { Text = value.Text.ToLowerInvariant() })],
        _ => []
    };

    private static string NameOf(IList<SkUiTextInputFormatter> formatters) => formatters switch
    {
        [] => NoFormatters,
        [SkUiMaskFormatter { Mask: PhoneMaskText }] => PhoneMask,
        [SkUiMaskFormatter { Mask: CardMaskText }] => CardNumber,
        [SkUiFilteringFormatter] => DigitsOnly,
        _ => NoSpaces
    };

    private static Keyboard KeyboardOf(string name) => name switch
    {
        "Text" => Keyboard.Text,
        "Numeric" => Keyboard.Numeric,
        "Telephone" => Keyboard.Telephone,
        "Email" => Keyboard.Email,
        "Url" => Keyboard.Url,
        "Chat" => Keyboard.Chat,
        "Plain" => Keyboard.Plain,
        _ => Keyboard.Default
    };

    private static string NameOf(Keyboard keyboard) =>
        ReferenceEquals(keyboard, Keyboard.Text) ? "Text"
        : ReferenceEquals(keyboard, Keyboard.Numeric) ? "Numeric"
        : ReferenceEquals(keyboard, Keyboard.Telephone) ? "Telephone"
        : ReferenceEquals(keyboard, Keyboard.Email) ? "Email"
        : ReferenceEquals(keyboard, Keyboard.Url) ? "Url"
        : ReferenceEquals(keyboard, Keyboard.Chat) ? "Chat"
        : ReferenceEquals(keyboard, Keyboard.Plain) ? "Plain"
        : "Default";
}
