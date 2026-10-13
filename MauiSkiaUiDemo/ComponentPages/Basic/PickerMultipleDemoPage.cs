using MauiSkiaUi;
using MauiSkiaUi.CommunityToolkit;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiPicker"/> with <see cref="SkUiPicker.SelectionMode"/> <see cref="SkUiSelectionMode.Multiple"/> (SkiaUi's
/// extension, MAUI's picker has no counterpart): the rows are check boxes and the list stays open while items are toggled; the field
/// shows the chosen texts joined, or a count. Tap rows, or use the keys in the list (arrows move a cursor, Space toggles, Enter or
/// Escape close); dismissing the list keeps the choice.
/// </summary>
public sealed class PickerMultipleDemoPage : ComponentDemoPage
{
    private const string Joined = "(joined)";
    private const string CountFormat = "{0} selected";
    private const string ToolkitPresenter = "CommunityToolkit Popup";
    private const string ActionSheetPresenter = "MAUI action sheet";
    private const string AppPresenter = "App's (SkUiPopups.Presenter)";
    private const string TitleText = "Select monkeys";
    private const string AppAcceptText = "(app's: SkUiStrings.Current)";
    private const string LocalAcceptText = "Done";

    private static readonly string[] MonkeyNames =
        ["Baboon", "Capuchin Monkey", "Blue Monkey", "Squirrel Monkey", "Golden Lion Tamarin", "Howler Monkey", "Spider Monkey", "Proboscis Monkey"];

    private readonly SkUiPicker _picker;
    private int _opened;
    private int _closed;

    public PickerMultipleDemoPage()
        : base(nameof(SkUiPicker), new SkUiPicker { Title = TitleText, SelectionMode = SkUiSelectionMode.Multiple, ItemsSource = MonkeyNames },
            widthRange: (100, 360, 240), heightRange: (30, 120, 48))
    {
        _picker = (SkUiPicker)SkiaControl;
        _picker.SelectionChanged += (_, _) => Status();
        _picker.Opened += (_, _) => { _opened++; Status(); };
        _picker.Closed += (_, _) => { _closed++; Status(); };

        Choice(nameof(SkUiPicker.SelectionMode), [SkUiSelectionMode.Multiple, SkUiSelectionMode.Single], SkUiSelectionMode.Multiple,
            value => _picker.SelectionMode = value, () => _picker.SelectionMode);
        Text(nameof(SkUiPicker.Title), TitleText, value => _picker.Title = value, () => _picker.Title ?? "");
        Text(nameof(SkUiPicker.SelectedTextSeparator), SkUiPicker.DefaultSelectedTextSeparator, value => _picker.SelectedTextSeparator = value,
            () => _picker.SelectedTextSeparator);
        Choice(nameof(SkUiPicker.SelectedCountFormat), [Joined, CountFormat], Joined,
            value => _picker.SelectedCountFormat = value == Joined ? null : value, () => _picker.SelectedCountFormat ?? Joined);
        ColorEditor(nameof(SkUiPicker.TextColor), DemoColors.Ink, value => _picker.TextColor = value, () => _picker.TextColor ?? DemoColors.Ink);
        ColorEditor(nameof(SkUiPicker.TitleColor), DemoColors.Caption, value => _picker.TitleColor = value, () => _picker.TitleColor ?? DemoColors.Caption);
        Number(nameof(SkUiPicker.FontSize), 10, 30, 16, value => _picker.FontSize = value, () => _picker.FontSize);
        Choice(nameof(SkUiPicker.PopupPlacement), Enum.GetValues<SkUiPopupPlacement>(), SkUiPopupPlacement.Default,
            value => _picker.PopupPlacement = value, () => _picker.PopupPlacement);
        Number(nameof(SkUiPicker.MaxPopupHeight), -1, 600, -1, value => _picker.MaxPopupHeight = value < 0 ? -1 : value, () => _picker.MaxPopupHeight, whole: true);
        Choice(nameof(SkUiPicker.PopupPresenter), [ToolkitPresenter, ActionSheetPresenter, AppPresenter], ToolkitPresenter,
            value => _picker.PopupPresenter = value switch
            {
                ToolkitPresenter => SkUiToolkitPopupPresenter.Instance,
                ActionSheetPresenter => SkUiActionSheetPopupPresenter.Instance,
                _ => null
            },
            () => _picker.PopupPresenter switch
            {
                SkUiToolkitPopupPresenter => ToolkitPresenter,
                SkUiActionSheetPopupPresenter => ActionSheetPresenter,
                _ => AppPresenter
            });
        Choice(nameof(SkUiPickerBase.UpdateMode), [SkUiPickerUpdateMode.Immediately, SkUiPickerUpdateMode.WhenFinished], SkUiPickerUpdateMode.Immediately,
            value => _picker.UpdateMode = value, () => _picker.UpdateMode);
        Choice(nameof(SkUiPickerBase.AcceptText), [AppAcceptText, LocalAcceptText], AppAcceptText,
            value => _picker.AcceptText = value == AppAcceptText ? null : value, () => _picker.AcceptText ?? AppAcceptText);
        ActionButton("Open (IsOpen = true)", () => _picker.IsOpen = true);
        ActionButton("SelectAll", _picker.SelectAll);
        ActionButton("ClearSelection", _picker.ClearSelection);
        ActionButton("Add \"Baboon\" to SelectedItems", () =>
        {
            if (_picker.SelectionMode == SkUiSelectionMode.Multiple && !_picker.SelectedItems.Contains(MonkeyNames[0]))
                _picker.SelectedItems.Add(MonkeyNames[0]);
        });
        OnReset(() => _picker.ClearSelection());
        Status();
    }

    private void Status() => Feedback(_picker.SelectionMode == SkUiSelectionMode.Multiple
        ? $"SelectedIndices [{string.Join(", ", _picker.SelectedIndices)}] · opened {_opened}, closed {_closed}"
        : $"SelectedIndex {_picker.SelectedIndex} · opened {_opened}, closed {_closed}");
}
