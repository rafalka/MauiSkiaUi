using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Controls.Pickers;

/// <summary>
/// A color picker of the app's: <see cref="SkUiValuePicker{T}"/> of <c>Color?</c> whose popup is a grid of swatches. The field shows
/// the color's name with a swatch in front (the picker's <c>LeadingView</c>); screen readers read the name. Built on SkiaUi's public
/// API only.
/// </summary>
public class ColorPicker : SkUiValuePicker<Color?>
{
    /// <summary>The swatches, with the names the field shows.</summary>
    public static readonly (string Name, Color Color)[] Swatches =
    [
        ("Red", Color.FromArgb("#E5484D")), ("Orange", Color.FromArgb("#F76B15")), ("Amber", Color.FromArgb("#FFC53D")),
        ("Green", Color.FromArgb("#30A46C")), ("Teal", Color.FromArgb("#12A594")), ("Blue", Color.FromArgb("#0090FF")),
        ("Indigo", Color.FromArgb("#3E63DD")), ("Purple", Color.FromArgb("#8E4EC6")), ("Pink", Color.FromArgb("#D6409F")),
        ("Brown", Color.FromArgb("#AD7F58")), ("Gray", Color.FromArgb("#8B8D98")), ("Black", Color.FromArgb("#1C2024"))
    ];

    private readonly SkUiBox _swatch = new() { WidthRequest = 18, HeightRequest = 18, CornerRadius = 9, Margin = new Thickness(10, 0, 0, 0) };

    public ColorPicker()
    {
        LeadingView = _swatch;
        ShowSwatch();
        ValueChanged += (_, _) => ShowSwatch();
    }

    /// <summary>The name of a swatch's color; <c>null</c> for any other color.</summary>
    public static string? NameOf(Color? color) =>
        color is null ? null : Swatches.FirstOrDefault(swatch => swatch.Color.Equals(color)).Name;

    protected override string? FormatValue(Color? value) => NameOf(value) ?? value?.ToHex();

    protected override SkUiValuePopupContent<Color?> CreateValueContent(SkUiPickerPopupContext context) => new SwatchGrid();

    private void ShowSwatch()
    {
        _swatch.IsVisible = Value is not null;
        _swatch.Color = Value ?? Colors.Transparent;
    }

    /// <summary>
    /// The popup: four swatches a row, the chosen one ringed. A tap chooses the color and closes; while the popup waits for OK
    /// (<c>UpdateMode="WhenFinished"</c>) it only moves the ring, and OK applies it.
    /// </summary>
    private sealed class SwatchGrid : SkUiValuePopupContent<Color?>
    {
        private const int Columns = 4;
        private readonly List<(Color Color, SkUiButton Button)> _buttons = [];

        protected override SkUiView CreateBody()
        {
            var grid = new SkUiGrid { Padding = new Thickness(16), RowSpacing = 12, ColumnSpacing = 12 };
            for (var column = 0; column < Columns; column++)
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            for (var index = 0; index < Swatches.Length; index++)
            {
                var (name, color) = Swatches[index];
                if (index % Columns == 0)
                    grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var button = new SkUiButton
                {
                    BackgroundColor = color,
                    WidthRequest = 44,
                    HeightRequest = 44,
                    CornerRadius = 22,
                    BorderColor = SampleColors.Ink,
                    HorizontalOptions = LayoutOptions.Center
                };
                SemanticProperties.SetDescription(button, name);
                button.Clicked += (_, _) => Tap(color);
                grid.Add(button, index % Columns, index / Columns);
                _buttons.Add((color, button));
            }
            ShowChoice();
            return grid;
        }

        private void Tap(Color color)
        {
            if (Context.RequiresAccept)
                SetValue(color);
            else
                Complete(color);
        }

        protected override void OnValueChanged() => ShowChoice();

        private void ShowChoice()
        {
            foreach (var (color, button) in _buttons)
                button.BorderWidth = color.Equals(Value) ? 3 : 0;
        }
    }
}

/// <summary>
/// The same color picker with the app's own UI instead of the drawn swatches: <see cref="ShowPopupAsync"/> answers the typed request
/// (<see cref="SkUiValuePopupRequest{T}"/>) with MAUI's action sheet, as a platform color dialog would. The picker applies the answer
/// and raises its events as with the drawn popup.
/// </summary>
public sealed class ActionSheetColorPicker : ColorPicker
{
    private const string None = "None";

    protected override async Task ShowPopupAsync(SkUiPopupRequest request, CancellationToken cancellationToken)
    {
        if (request is not SkUiValuePopupRequest<Color?> colors || request.Page is not { } page)
        {
            await base.ShowPopupAsync(request, cancellationToken);
            return;
        }
        var names = Swatches.Select(swatch => swatch.Name).Append(None).ToArray();
        var chosen = await page.DisplayActionSheetAsync(request.Title ?? Title ?? "Color", request.CancelText, null, names);
        if (cancellationToken.IsCancellationRequested || chosen is null || chosen == request.CancelText)
            return; // closed by the picker, or dismissed: nothing changes
        if (chosen == None)
            colors.Clear();
        else
            colors.Complete(Swatches.First(swatch => swatch.Name == chosen).Color);
    }
}
