using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiSheet"/> (1.18 phase S, #136): a time-clock screen with the request's four cases. A confirm dialog and an action
/// sheet with a destructive row open with <see cref="SkUiSheet.ShowAsync{TResult}"/> over the preview's surface; a list of break types
/// (fitted to its content) and a form with a time picker are sheets placed on the page. Sheets open from sheets, each over the one it
/// came from: Delete in the action sheet asks in a dialog, the form's time pickers open their wheels in a sheet
/// (<see cref="SkUiSheetPopupPresenter"/>), and its "Add a break…" opens the break sheet, placed before the form on the page. Editors for the placed sheet's edge, presentation, detent and
/// options; the feedback line reports what each sheet returned.
/// </summary>
public sealed class SheetDemoPage : ComponentDemoPage
{
    private const string Lunch = "Lunch";
    private const string Coffee = "Coffee";
    private const string Personal = "Personal";
    private const string Edit = "Edit";
    private const string Duplicate = "Duplicate";
    private const string Delete = "Delete";

    public SheetDemoPage()
        : base(nameof(SkUiSheet), CreateSample(out var page, out var form, out var breaks), native: null, widthRange: (260, 480, 360),
            heightRange: (420, 760, 600))
    {
        form.Closed += (_, args) => Feedback(args.IsDismissed ? "Form dismissed" : $"Form saved: {args.Result}");
        breaks.Closed += (_, args) => Feedback(args.IsDismissed ? "No break chosen" : $"Break: {args.Result}");
        form.DetentChanged += (_, args) => Feedback($"Form detent {args.CurrentIndex} (was {args.PreviousIndex})");
        page.Confirm.Clicked += async (_, _) =>
        {
            var clockOut = new SkUiButton { Text = "Clock out" };
            var cancel = new SkUiButton { Text = "Cancel" };
            var result = await SkUiSheet.ShowAsync<bool>(page.Confirm, new SkUiLabel { Text = "You worked 7 h 40 min today.", Margin = new Thickness(24, 0) },
                sheet =>
                {
                    sheet.Presentation = SkUiSheetPresentation.Dialog;
                    sheet.Title = "Clock out now?";
                    sheet.Actions = new SkUiHorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.End, Children = { cancel, clockOut } };
                    cancel.Clicked += (_, _) => sheet.Close(false);
                    clockOut.Clicked += (_, _) => sheet.Close(true);
                });
            Feedback(result ? "Clocked out" : "Still clocked in");
        };
        page.Actions.Clicked += async (_, _) =>
        {
            var rows = new SkUiVerticalStackLayout { Padding = new Thickness(0, 0, 0, 8) };
            SkUiSheet? shown = null;
            foreach (var action in new[] { Edit, Duplicate, Delete })
            {
                var row = new SkUiButton
                {
                    Text = action, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.Transparent, CornerRadius = 0,
                    TextColor = action == Delete ? Colors.Red : Ink
                };
                // Delete asks first, in a dialog opened from this sheet: it goes over it, and this sheet waits for its answer.
                row.Clicked += async (_, _) =>
                {
                    if (action != Delete || await ConfirmDeleteAsync(row))
                        shown?.Close(action);
                };
                rows.Children.Add(row);
            }
            var result = await SkUiSheet.ShowAsync<string>(page.Actions, rows, sheet =>
            {
                shown = sheet;
                sheet.Title = "Shift on Thursday";
                sheet.Detents = [SkUiSheetDetent.FitContent];
            });
            Feedback(result is null ? "No action" : $"Action: {result}");
        };
        page.Breaks.Clicked += (_, _) => breaks.IsOpen = true;
        page.Form.Clicked += (_, _) => form.IsOpen = true;

        Choice(nameof(SkUiSheet.Edge), Enum.GetValues<SkUiSheetEdge>(), SkUiSheetEdge.Bottom, value => form.Edge = value, () => form.Edge);
        Choice(nameof(SkUiSheet.Presentation), Enum.GetValues<SkUiSheetPresentation>(), SkUiSheetPresentation.Sheet,
            value => form.Presentation = value, () => form.Presentation);
        Number(nameof(SkUiSheet.SelectedDetentIndex), 0, 1, 0, value => form.SelectedDetentIndex = (int)value, () => form.SelectedDetentIndex, whole: true);
        Toggle(nameof(SkUiSheet.IsOpen), false, value => form.IsOpen = value, () => form.IsOpen);
        Toggle(nameof(SkUiSheet.IsModal), true, value => form.IsModal = value, () => form.IsModal);
        Toggle(nameof(SkUiSheet.IsDragEnabled), true, value => form.IsDragEnabled = value, () => form.IsDragEnabled);
        Toggle(nameof(SkUiSheet.HasGrabber), true, value => form.HasGrabber = value, () => form.HasGrabber);
        Toggle(nameof(SkUiSheet.DismissOnScrimTap), true, value => form.DismissOnScrimTap = value, () => form.DismissOnScrimTap);
        Number(nameof(SkUiSheet.CornerRadius), -1, 40, -1, value => form.CornerRadius = value < 0 ? -1 : value, () => form.CornerRadius);
    }

    /// <summary>A dialog over whatever sheet <paramref name="anchor"/> is in; <c>true</c> when the user confirms.</summary>
    private static Task<bool> ConfirmDeleteAsync(SkUiView anchor)
    {
        var delete = new SkUiButton { Text = Delete, TextColor = Colors.White, BackgroundColor = Colors.Red };
        var keep = new SkUiButton { Text = "Keep" };
        return SkUiSheet.ShowAsync<bool>(anchor, new SkUiLabel { Text = "The shift goes for everyone on it.", Margin = new Thickness(24, 0) }, sheet =>
        {
            sheet.Presentation = SkUiSheetPresentation.Dialog;
            sheet.Title = "Delete the shift?";
            sheet.Actions = new SkUiHorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.End, Children = { keep, delete } };
            keep.Clicked += (_, _) => sheet.Close(false);
            delete.Clicked += (_, _) => sheet.Close(true);
        });
    }

    /// <summary>The page's buttons that open the four sheets.</summary>
    internal sealed record PageButtons(SkUiButton Confirm, SkUiButton Actions, SkUiButton Breaks, SkUiButton Form);

    internal static SkUiGrid CreateSample(out PageButtons page, out SkUiSheet form, out SkUiSheet breaks)
    {
        page = new PageButtons(
            new SkUiButton { Text = "Clock out…" },
            new SkUiButton { Text = "Shift actions…" },
            new SkUiButton { Text = "Take a break…" },
            new SkUiButton { Text = "Edit the shift…" });
        var buttons = new SkUiVerticalStackLayout
        {
            Padding = new Thickness(16), Spacing = 12,
            Children =
            {
                new SkUiLabel { Text = "Thursday", FontSize = 22, FontAttributes = FontAttributes.Bold },
                new SkUiLabel { Text = "Clocked in at 8:02", TextColor = SkUiColors.Muted },
                page.Confirm, page.Actions, page.Breaks, page.Form
            }
        };

        // A list of break types, fitted to its content: a choice closes the sheet with it.
        var options = new SkUiVerticalStackLayout { Padding = new Thickness(0, 0, 0, 8) };
        breaks = new SkUiSheet { Title = "Take a break", Description = "Your manager sees the type you choose.", Detents = [SkUiSheetDetent.FitContent] };
        var breakSheet = breaks;
        foreach (var type in new[] { Lunch, Coffee, Personal })
        {
            var row = new SkUiButton { Text = type, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.Transparent, TextColor = Ink, CornerRadius = 0 };
            row.Clicked += (_, _) => breakSheet.Close(type);
            options.Children.Add(row);
        }
        breaks.Content = options;

        // A form with a time picker whose wheels open in a sheet on top of this one.
        var start = new SkUiTimePicker { Time = new TimeSpan(8, 0, 0), PopupPresenter = new SkUiSheetPopupPresenter() };
        var end = new SkUiTimePicker { Time = new TimeSpan(16, 0, 0), PopupPresenter = new SkUiSheetPopupPresenter() };
        var note = new SkUiEntry { Placeholder = "Note for your manager" };
        // A sheet opened from this one: the break sheet, placed before this one on the page, opens over it.
        var takeBreak = new SkUiButton { Text = "Add a break…", HorizontalOptions = LayoutOptions.Start };
        takeBreak.Clicked += (_, _) => breakSheet.IsOpen = true;
        var fields = new SkUiVerticalStackLayout
        {
            Padding = new Thickness(16, 0), Spacing = 8,
            Children = { new SkUiLabel { Text = "Start" }, start, new SkUiLabel { Text = "End" }, end, note, takeBreak }
        };
        var save = new SkUiButton { Text = "Save", HorizontalOptions = LayoutOptions.End };
        form = new SkUiSheet
        {
            Title = "Edit the shift",
            Content = new SkUiScrollView { Content = fields },
            Actions = save,
            AutomationId = "ShiftForm"
        };
        var formSheet = form;
        save.Clicked += (_, _) => formSheet.Close($"{start.Time:hh\\:mm}–{end.Time:hh\\:mm}");
        return new SkUiGrid { WidthRequest = 360, HeightRequest = 600, Children = { buttons, breaks, form } };
    }
}
