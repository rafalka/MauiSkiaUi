using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Controls.Pickers;

/// <summary>
/// HOWTO: pickers of your own, for values SkiaUi has no picker for. Each derives <see cref="SkUiValuePicker{T}"/> (or the Core
/// <c>SkUiCoreValuePicker&lt;T&gt;</c>), formats its value for the field and returns a drawn popup body; SkiaUi keeps the field, the
/// popup's placement and presenter, OK / Cancel, Clear, keyboard and screen readers. Four pickers, in their own files:
/// <list type="bullet">
/// <item><c>ColorPicker</c>: swatches; a tap chooses and closes; the field shows the color's name and a swatch.</item>
/// <item><c>DurationPicker</c>: <see cref="SkUiPickerWheels"/> as the popup body, with OK (<c>UpdateMode="WhenFinished"</c>).</item>
/// <item><c>ActionSheetColorPicker</c>: the app's own UI for the typed request (MAUI's action sheet) instead of the swatches.</item>
/// <item><c>CoreRatingPicker</c>: a Core picker of one to five stars, in an <see cref="MauiSkiaUi.Core.SkUiCoreHost"/>.</item>
/// </list>
/// </summary>
public sealed class CustomPickersSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Pickers of your own (color, duration, rating)",
        Summary: "A color, a duration and a rating picked with SkiaUi's picker field and popups: each picker only formats its value " +
                 "and draws the popup's body.",
        HowTo:
        [
            "Derive a closed type: `class ColorPicker : SkUiValuePicker<Color?>` (a value type that can be empty: `SkUiValuePicker<TimeSpan?>`).",
            "Override `FormatValue` for the field's text (or set `ValueFormat`), and `DescribeValue` when screen readers should read something else.",
            "Return the popup's body from `CreateValueContent`: an `SkUiValuePopupContent<T>` whose `CreateBody` draws it, reading `Value`.",
            "In the body, call `SetValue(value)` as the user changes it (the popup stays open) or `Complete(value)` to choose and close; " +
                "follow `OnValueChanged` to show the Clear button's or the app's change.",
            "For the app's own UI instead, override `ShowPopupAsync` and answer `SkUiValuePopupRequest<T>` with `Complete(value)` or `Clear()`."
        ],
        ThingsToKnow:
        [
            "With `UpdateMode=\"WhenFinished\"` the popup shows OK and Cancel and `SetValue` keeps a draft: OK applies it, Cancel and a dismissal drop it.",
            "`Value` binds two-way; the user's choices are written as handler values, so one-way bindings keep working.",
            "Placement, `Title`, `Placeholder`, the clear buttons, `PopupHeaderTemplate` / `PopupFooterTemplate` and `LeadingView` work as on every picker.",
            "None of these pickers uses anything internal to SkiaUi: the samples app has no access to it."
        ])
    {
        MoreSources = ["ColorPicker.cs", "DurationPicker.cs", "CoreRatingPicker.cs"]
    };

    private readonly SkUiLabel _summary = new() { FontSize = 15, TextColor = SampleColors.Ink };

    public CustomPickersSample() : base(Info)
    {
        LabelColor = new ColorPicker { Title = "Label color", Placeholder = "Choose a color", IsClearButtonVisible = true, TextColor = SampleColors.Ink };
        MeetingLength = new DurationPicker
        {
            Title = "Meeting length",
            Placeholder = "Choose a length",
            UpdateMode = SkUiPickerUpdateMode.WhenFinished,
            PopupPlacement = SkUiPopupPlacement.Window,
            Value = TimeSpan.FromMinutes(45),
            TextColor = SampleColors.Ink
        };
        FolderColor = new ActionSheetColorPicker { Title = "Folder color", Placeholder = "Choose a color", TextColor = SampleColors.Ink };
        Rating = new CoreRatingPicker { Title = "Rating", Placeholder = "Rate it" };
        LabelColor.ValueChanged += (_, _) => UpdateSummary();
        MeetingLength.ValueChanged += (_, _) => UpdateSummary();
        FolderColor.ValueChanged += (_, _) => UpdateSummary();
        Rating.ValueChanged += (_, _) => UpdateSummary();

        SampleContent = new SkUiVerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(16),
            BackgroundColor = SampleColors.Surface,
            Children =
            {
                Heading("Swatches"), LabelColor,
                Heading("Wheels, with OK"), MeetingLength,
                Heading("The app's own UI (action sheet)"), FolderColor,
                Heading("Core"), new MauiSkiaUi.Core.SkUiCoreHost().SetContent(Rating),
                _summary
            }
        };
        UpdateSummary();
    }

    /// <summary>The swatch picker (tests).</summary>
    public ColorPicker LabelColor { get; }

    /// <summary>The duration picker (tests).</summary>
    public DurationPicker MeetingLength { get; }

    /// <summary>The picker with the action sheet (tests).</summary>
    public ActionSheetColorPicker FolderColor { get; }

    /// <summary>The Core rating picker (tests).</summary>
    public CoreRatingPicker Rating { get; }

    private void UpdateSummary() =>
        _summary.Text = $"Label {ColorPicker.NameOf(LabelColor.Value) ?? "none"} · meeting {(MeetingLength.Value is { } length ? DurationPicker.Describe(length) : "none")} · " +
                        $"folder {ColorPicker.NameOf(FolderColor.Value) ?? "none"} · rating {Rating.Value?.ToString() ?? "none"}";

    private static SkUiLabel Heading(string text) =>
        new() { Text = text, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Caption, TextTransform = TextTransform.Uppercase };
}
