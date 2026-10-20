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
    private const string NoView = "None";
    private const string IconView = "Icon";
    private const string PrefixView = "Label \"$\"";
    private const string UnitView = "Label \"kg\"";
    private const string ButtonView = "Button (upper case)";
    private const string EmailError = "Enter an email address";
    private const string NoError = "False";
    private const string WithError = "True";
    private const string ValidateEmail = "Validate as email";

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
        // SkiaUi only (MAUI's Entry has none): drawn views inside the field; the button acts without taking focus from it.
        var leading = NoView;
        var trailing = NoView;
        Choice(nameof(SkUiEntry.LeadingView), [NoView, IconView, PrefixView], NoView,
            value => skia.LeadingView = AdornmentOf(leading = value, skia), () => leading);
        Choice(nameof(SkUiEntry.TrailingView), [NoView, UnitView, ButtonView], NoView,
            value => skia.TrailingView = AdornmentOf(trailing = value, skia), () => trailing);
        // SkiaUi only: validation chrome. The app decides when the input is invalid; here, a check on each change.
        Text(nameof(SkUiEntry.HelperText), string.Empty, value => skia.HelperText = value, () => skia.HelperText ?? string.Empty);
        Text(nameof(SkUiEntry.ErrorText), EmailError, value => skia.ErrorText = value, () => skia.ErrorText ?? string.Empty);
        skia.ErrorText = EmailError;
        // One editor owns HasError: set by hand, or by an email check on each change.
        var validates = false;
        skia.TextChanged += (_, args) =>
        {
            if (validates)
                skia.HasError = !IsEmail(args.NewTextValue);
        };
        Choice(nameof(SkUiEntry.HasError), [NoError, WithError, ValidateEmail], NoError, value =>
        {
            validates = value == ValidateEmail;
            skia.HasError = validates ? !IsEmail(skia.Text) : value == WithError;
        }, () => validates ? ValidateEmail : skia.HasError ? WithError : NoError);
        ColorEditor(nameof(SkUiEntry.TextColor), Ink, value => { skia.TextColor = value; native.TextColor = value; }, () => skia.TextColor ?? Ink, () => native.TextColor ?? Ink);
        ColorEditor(nameof(SkUiEntry.CaretColor), Accent, value => skia.CaretColor = value, () => skia.CaretColor ?? Accent);
        ColorEditor(nameof(SkUiEntry.ErrorColor), SkUiColors.Error, value => skia.ErrorColor = value, () => skia.ErrorColor ?? SkUiColors.Error);
        // SkiaUi only: the field's own chrome over the look's (MAUI's Entry takes a background, beside it here).
        Choice(nameof(SkUiEntry.FieldStyle), Enum.GetValues<SkUiTextFieldStyle>(), SkUiTextFieldStyle.Default, value => skia.FieldStyle = value, () => skia.FieldStyle);
        ColorEditor(nameof(SkUiEntry.FillColor), Colors.Transparent, value => { skia.FillColor = value; native.BackgroundColor = value; },
            () => skia.FillColor ?? Colors.Transparent, () => native.BackgroundColor ?? Colors.Transparent);
        ColorEditor(nameof(SkUiEntry.BorderColor), SkUiColors.Muted, value => skia.BorderColor = value, () => skia.BorderColor ?? SkUiColors.Muted);
        Number(nameof(SkUiEntry.BorderWidth), 0, 6, 1, value => skia.BorderWidth = value, () => skia.BorderWidth ?? 1);
        Number(nameof(SkUiEntry.CornerRadius), 0, 30, 6, value => skia.CornerRadius = (int)value, () => skia.CornerRadius ?? 6, whole: true);
        var padding = SkUiLook.Current.TextFieldPadding;
        Number(nameof(SkUiEntry.TextPadding), 0, 32, padding.Left, value => skia.TextPadding = new Thickness(value, padding.Top, value, padding.Bottom),
            () => skia.TextPadding?.Left ?? padding.Left, whole: true);
        ActionButton("Select all", () =>
        {
            skia.CursorPosition = 0;
            skia.SelectionLength = skia.Text?.Length ?? 0;
            native.CursorPosition = 0;
            native.SelectionLength = native.Text?.Length ?? 0;
        });
        ActionButton("Focus", () => skia.Focus());
    }

    // Empty is valid (nothing typed yet); otherwise one @ with text on both sides and a dot after it.
    private static bool IsEmail(string? text) =>
        string.IsNullOrEmpty(text) || System.Text.RegularExpressions.Regex.IsMatch(text, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    private ISkUiView? AdornmentOf(string name, SkUiEntry entry)
    {
        switch (name)
        {
            case IconView:
                return new SkUiImage { Source = DemoAssets.BadgeImage, WidthRequest = 20, HeightRequest = 20 };
            case PrefixView or UnitView:
                return new SkUiLabel { Text = name == PrefixView ? "$" : "kg", TextColor = DemoColors.Caption, FontSize = 15 };
            case ButtonView:
                var button = new SkUiButton { Text = "ABC", FontSize = 12, Padding = new Thickness(8, 2), HeightRequest = 28, CornerRadius = 14 };
                button.Clicked += (_, _) =>
                {
                    entry.Text = entry.Text?.ToUpperInvariant();
                    Feedback($"Button: \"{entry.Text}\" (focused: {entry.IsFocused})");
                };
                return button;
            default:
                return null;
        }
    }

    // SkiaUi only (MAUI's Entry has no formatters): filters, a mask, a digit cap with groups, a function.
    private static SkUiTextInputFormatter[] FormattersOf(string name) => name switch
    {
        DigitsOnly => [SkUiFilteringFormatter.DigitsOnly()],
        PhoneMask => [new SkUiMaskFormatter(PhoneMaskText)],
        CardNumber => [new SkUiMaskFormatter(CardMaskText)],
        NoSpaces => [new SkUiFilteringFormatter(@"\s", SkUiFilteringMode.Deny),
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
