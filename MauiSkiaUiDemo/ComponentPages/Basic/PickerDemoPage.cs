using MauiSkiaUi;
using MauiSkiaUi.CommunityToolkit;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiPicker"/> (C6) next to MAUI's <see cref="Picker"/>: MAUI's properties set on both, the drawn list opened on
/// the app's popup system (CommunityToolkit's popup, SkiaUi.Maui.CommunityToolkit; MAUI's action sheet to compare), and SkiaUi's own:
/// where the list opens, its height, drawn rows. Tap the field, or focus it and use the keyboard: arrows change the selection,
/// Alt+Down, Space or Enter open the list (arrows, Home / End, typing, Enter, Escape there).
/// </summary>
public sealed class PickerDemoPage : ComponentDemoPage
{
    private const string Monkeys = "Monkeys (6)";
    private const string Numbers = "Numbers (500)";
    private const string Colours = "Objects (ItemDisplayBinding)";
    private const string ToolkitPresenter = "CommunityToolkit Popup";
    private const string ActionSheetPresenter = "MAUI action sheet";
    private const string AppPresenter = "App's (SkUiPopups.Presenter)";
    private const string Title1 = "Select a monkey";
    private const string AppAcceptText = "(app's: SkUiStrings.Current)";
    private const string LocalAcceptText = "Done";

    private static readonly string[] MonkeyNames = ["Baboon", "Capuchin Monkey", "Blue Monkey", "Squirrel Monkey", "Golden Lion Tamarin", "Howler Monkey"];
    private static readonly string[] NumberNames = [.. Enumerable.Range(1, 500).Select(number => $"Item {number}")];
    private static readonly DemoColour[] ColourItems =
    [
        new("Teal", DemoColors.Accent), new("Coral", DemoColors.SampleA), new("Sky", DemoColors.SampleB), new("Ink", DemoColors.Ink),
        new("Sun", Color.FromArgb("#F2B33D")), new("Leaf", Color.FromArgb("#4C9A2A"))
    ];

    private readonly SkUiPicker _skia;
    private readonly Picker _native;
    private int _opened;
    private int _closed;

    /// <summary>A named color (the objects list).</summary>
    public sealed record DemoColour(string Name, Color Color);

    public PickerDemoPage()
        : base(nameof(SkUiPicker), new SkUiPicker { Title = Title1 }, new Picker { Title = Title1 }, widthRange: (100, 360, 240), heightRange: (30, 120, 48))
    {
        _skia = (SkUiPicker)SkiaControl;
        _native = (Picker)NativeControl!;
        _skia.SelectedIndexChanged += (_, _) => Status();
        _native.SelectedIndexChanged += (_, _) => Status();
        _skia.Opened += (_, _) => { _opened++; Status(); };
        _skia.Closed += (_, _) => { _closed++; Status(); };

        Choice("Items", [Monkeys, Numbers, Colours], Monkeys, SetItems,
            () => ItemsName(_skia.ItemsSource), () => ItemsName(_native.ItemsSource));
        Text(nameof(Picker.Title), Title1, value => { _skia.Title = value; _native.Title = value; }, () => _skia.Title ?? "", () => _native.Title ?? "");
        // Within the shortest list (six items), where both pickers keep the index.
        Number(nameof(Picker.SelectedIndex), -1, 5, -1, value => { _skia.SelectedIndex = (int)value; _native.SelectedIndex = (int)value; },
            () => _skia.SelectedIndex, () => _native.SelectedIndex, whole: true);
        ColorEditor(nameof(Picker.TextColor), DemoColors.Ink, value => { _skia.TextColor = value; _native.TextColor = value; },
            () => _skia.TextColor ?? DemoColors.Ink, () => _native.TextColor ?? DemoColors.Ink);
        ColorEditor(nameof(Picker.TitleColor), DemoColors.Caption, value => { _skia.TitleColor = value; _native.TitleColor = value; },
            () => _skia.TitleColor ?? DemoColors.Caption, () => _native.TitleColor ?? DemoColors.Caption);
        Number(nameof(Picker.FontSize), 10, 30, 16, value => { _skia.FontSize = value; _native.FontSize = value; }, () => _skia.FontSize, () => _native.FontSize);
        Choice(nameof(Picker.FontAttributes), [FontAttributes.None, FontAttributes.Bold, FontAttributes.Italic], FontAttributes.None,
            value => { _skia.FontAttributes = value; _native.FontAttributes = value; }, () => _skia.FontAttributes, () => _native.FontAttributes);
        Number(nameof(Picker.CharacterSpacing), 0, 6, 0, value => { _skia.CharacterSpacing = value; _native.CharacterSpacing = value; },
            () => _skia.CharacterSpacing, () => _native.CharacterSpacing);
        Choice(nameof(Picker.HorizontalTextAlignment), [TextAlignment.Start, TextAlignment.Center, TextAlignment.End], TextAlignment.Start,
            value => { _skia.HorizontalTextAlignment = value; _native.HorizontalTextAlignment = value; },
            () => _skia.HorizontalTextAlignment, () => _native.HorizontalTextAlignment);
        Choice(nameof(Picker.VerticalTextAlignment), [TextAlignment.Start, TextAlignment.Center, TextAlignment.End], TextAlignment.Center,
            value => { _skia.VerticalTextAlignment = value; _native.VerticalTextAlignment = value; },
            () => _skia.VerticalTextAlignment, () => _native.VerticalTextAlignment);
        Toggle(nameof(Picker.FontAutoScalingEnabled), true, value => { _skia.FontAutoScalingEnabled = value; _native.FontAutoScalingEnabled = value; },
            () => _skia.FontAutoScalingEnabled, () => _native.FontAutoScalingEnabled);
        ActionButton("Open (IsOpen = true)", () => { _skia.IsOpen = true; _native.IsOpen = true; });

        // SkiaUi only.
        Choice(nameof(SkUiPicker.PopupPlacement), Enum.GetValues<SkUiPopupPlacement>(), SkUiPopupPlacement.Default,
            value => _skia.PopupPlacement = value, () => _skia.PopupPlacement);
        Number(nameof(SkUiPicker.MaxPopupHeight), -1, 600, -1, value => _skia.MaxPopupHeight = value < 0 ? -1 : value, () => _skia.MaxPopupHeight, whole: true);
        Choice(nameof(SkUiPickerBase.UpdateMode), [SkUiPickerUpdateMode.Immediately, SkUiPickerUpdateMode.WhenFinished], SkUiPickerUpdateMode.Immediately,
            value => _skia.UpdateMode = value, () => _skia.UpdateMode);
        Choice(nameof(SkUiPickerBase.AcceptText), [AppAcceptText, LocalAcceptText], AppAcceptText,
            value => _skia.AcceptText = value == AppAcceptText ? null : value, () => _skia.AcceptText ?? AppAcceptText);
        Toggle("ItemTemplate (drawn rows)", false, value => _skia.ItemTemplate = value ? new DataTemplate(Row) : null, () => _skia.ItemTemplate is not null);
        Toggle("SelectedItemTemplate (drawn field)", false, value => _skia.SelectedItemTemplate = value ? new DataTemplate(Row) : null,
            () => _skia.SelectedItemTemplate is not null);
        Text(nameof(SkUiPicker.Placeholder), "", value => _skia.Placeholder = value.Length == 0 ? null : value, () => _skia.Placeholder ?? "");
        Toggle(nameof(SkUiPicker.IsClearButtonVisible), false, value => _skia.IsClearButtonVisible = value, () => _skia.IsClearButtonVisible);
        Toggle(nameof(SkUiPicker.IsPopupClearButtonVisible), false, value => _skia.IsPopupClearButtonVisible = value, () => _skia.IsPopupClearButtonVisible);
        Choice(nameof(SkUiPicker.UpdateMode), [SkUiPickerUpdateMode.Immediately, SkUiPickerUpdateMode.WhenFinished], SkUiPickerUpdateMode.Immediately,
            value => _skia.UpdateMode = value, () => _skia.UpdateMode);
        Number(nameof(SkUiPicker.PopupWidth), -1, 400, -1, value => _skia.PopupWidth = value < 0 ? -1 : value, () => _skia.PopupWidth, whole: true);
        Toggle("Items 2 and 4 cannot be chosen (ItemEnabledBinding)", false,
            value => _skia.ItemEnabledBinding = value ? new Binding(".", converter: EvenItems.Instance) : null, () => _skia.ItemEnabledBinding is not null);
        Toggle("PopupHeaderTemplate / PopupFooterTemplate", false, value =>
        {
            _skia.PopupHeaderTemplate = value ? new DataTemplate(() => Hint("Pick one; disabled items are dimmed.")) : null;
            _skia.PopupFooterTemplate = value ? new DataTemplate(() => Hint("A footer of the app's.")) : null;
        }, () => _skia.PopupHeaderTemplate is not null);
        Choice(nameof(SkUiPicker.PopupPresenter), [ToolkitPresenter, ActionSheetPresenter, AppPresenter], ToolkitPresenter,
            value => _skia.PopupPresenter = value switch
            {
                ToolkitPresenter => SkUiToolkitPopupPresenter.Instance,
                ActionSheetPresenter => SkUiActionSheetPopupPresenter.Instance,
                _ => null
            },
            () => _skia.PopupPresenter switch
            {
                SkUiToolkitPopupPresenter => ToolkitPresenter,
                SkUiActionSheetPopupPresenter => ActionSheetPresenter,
                _ => AppPresenter
            });
        Status();
    }

    /// <summary>A hint in the popup (the app's header and footer).</summary>
    private static SkUiLabel Hint(string text) => new() { Text = text, FontSize = 13, TextColor = DemoColors.Caption, Padding = new Thickness(16, 8) };

    /// <summary>Items 2 and 4 (indices 1 and 3) cannot be chosen.</summary>
    private sealed class EvenItems : IValueConverter
    {
        public static EvenItems Instance { get; } = new();

        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            value switch
            {
                DemoColour colour => Array.IndexOf(ColourItems, colour) is not (1 or 3),
                string text => Array.IndexOf(MonkeyNames, text) is not (1 or 3) && Array.IndexOf(NumberNames, text) is not (1 or 3),
                _ => true
            };

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }

    private void SetItems(string name)
    {
        // MAUI's picker reads ItemDisplayBinding when the source is set: set it first.
        var display = name == Colours ? Binding.Create(static (DemoColour colour) => colour.Name) : null;
        _skia.ItemDisplayBinding = display;
        _native.ItemDisplayBinding = name == Colours ? Binding.Create(static (DemoColour colour) => colour.Name) : null;
        System.Collections.IList items = name switch
        {
            Numbers => NumberNames,
            Colours => ColourItems,
            _ => MonkeyNames
        };
        _skia.ItemsSource = items;
        _native.ItemsSource = items;
    }

    private static string ItemsName(System.Collections.IList? items) =>
        ReferenceEquals(items, NumberNames) ? Numbers : ReferenceEquals(items, ColourItems) ? Colours : Monkeys;

    /// <summary>A drawn row: a swatch (the objects list's color) and the text, larger than the plain rows.</summary>
    private static SkUiHorizontalStackLayout Row()
    {
        var swatch = new SkUiBox { WidthRequest = 22, HeightRequest = 22, CornerRadius = new CornerRadius(11), VerticalOptions = LayoutOptions.Center };
        var text = new SkUiLabel { FontSize = 17, TextColor = DemoColors.Ink, VerticalOptions = LayoutOptions.Center };
        var row = new SkUiHorizontalStackLayout { Spacing = 12, Padding = new Thickness(16, 12), Children = { swatch, text } };
        // Rows bind to the items: a color object, or a plain text (the monkeys and numbers lists).
        row.BindingContextChanged += (_, _) =>
        {
            swatch.Color = row.BindingContext is DemoColour colour ? colour.Color : DemoColors.Accent;
            text.Text = row.BindingContext is DemoColour named ? named.Name : row.BindingContext?.ToString() ?? "";
        };
        return row;
    }

    private void Status() => Feedback(
        $"SelectedIndex {_skia.SelectedIndex} ({_skia.SelectedItem switch { DemoColour colour => colour.Name, { } item => item, _ => "none" }}) · opened {_opened}, closed {_closed}",
        $"SelectedIndex {_native.SelectedIndex}");
}
