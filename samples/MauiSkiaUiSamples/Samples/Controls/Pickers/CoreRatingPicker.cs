using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiSamples.Samples.Controls.Pickers;

/// <summary>
/// A rating picker on the Core layer: <see cref="SkUiCoreValuePicker{T}"/> of <c>int?</c>, whose popup is a row of five stars. The
/// field shows the stars, screen readers read "3 of 5 stars". Built on SkiaUi's public API only.
/// </summary>
public sealed class CoreRatingPicker : SkUiCoreValuePicker<int?>
{
    private const int Stars = 5;

    protected override string? FormatValue(int? value) => value is { } rating ? new string('★', rating) + new string('☆', Stars - rating) : null;

    protected override string? DescribeValue(int? value) => value is { } rating ? $"{rating} of {Stars} stars" : null;

    protected override SkUiValuePopupContent<int?> CreateValueContent(SkUiPickerPopupContext context) => new StarRow();

    /// <summary>The popup: a star per rating; a tap chooses (and closes, unless the popup waits for OK).</summary>
    private sealed class StarRow : SkUiValuePopupContent<int?>
    {
        private readonly List<SkUiButton> _stars = [];

        protected override SkUiView CreateBody()
        {
            var row = new SkUiHorizontalStackLayout { Spacing = 4, Padding = new Thickness(16), HorizontalOptions = LayoutOptions.Center };
            for (var rating = 1; rating <= Stars; rating++)
            {
                var value = rating;
                var star = new SkUiButton { FontSize = 28, BackgroundColor = Colors.Transparent, TextColor = Color.FromArgb("#FFB224") };
                SemanticProperties.SetDescription(star, $"{rating} of {Stars} stars");
                star.Clicked += (_, _) =>
                {
                    if (Context.RequiresAccept)
                        SetValue(value);
                    else
                        Complete(value);
                };
                row.Children.Add(star);
                _stars.Add(star);
            }
            ShowValue();
            return row;
        }

        protected override void OnValueChanged() => ShowValue();

        private void ShowValue()
        {
            for (var index = 0; index < _stars.Count; index++)
                _stars[index].Text = Value > index ? "★" : "☆";
        }
    }
}
