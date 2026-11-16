using System.Globalization;
using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Controls.Pickers;

/// <summary>
/// A duration picker of the app's: <see cref="SkUiValuePicker{T}"/> of <c>TimeSpan?</c> (nullable, so "none" is a value) whose popup
/// body is <see cref="SkUiPickerWheels"/>, hours and minutes in steps of five. The wheels write their columns once they settle; the
/// content turns that into the picker's value (at once, or a draft that OK applies). Built on SkiaUi's public API only.
/// </summary>
public sealed class DurationPicker : SkUiValuePicker<TimeSpan?>
{
    protected override string? FormatValue(TimeSpan? value) => value is { } duration ? Describe(duration) : null;

    protected override SkUiValuePopupContent<TimeSpan?> CreateValueContent(SkUiPickerPopupContext context) => new Wheels(context);

    /// <summary>"1 h 30 min", "45 min", "2 h".</summary>
    public static string Describe(TimeSpan duration)
    {
        var hours = (int)duration.TotalHours;
        return (hours, duration.Minutes) switch
        {
            (0, var minutes) => string.Create(CultureInfo.CurrentCulture, $"{minutes} min"),
            (var h, 0) => string.Create(CultureInfo.CurrentCulture, $"{h} h"),
            (var h, var minutes) => string.Create(CultureInfo.CurrentCulture, $"{h} h {minutes} min")
        };
    }

    /// <summary>The popup: two inline wheels in the picker's font; a change of either is the value.</summary>
    private sealed class Wheels(SkUiPickerPopupContext context) : SkUiValuePopupContent<TimeSpan?>
    {
        private readonly SkUiPickerColumn _hours = SkUiPickerColumn.Range(0, 23).SetHeader("Hours");
        private readonly SkUiPickerColumn _minutes = SkUiPickerColumn.Range(0, 55, 5, "00").SetHeader("Minutes");
        private bool _showing;

        protected override SkUiView CreateBody()
        {
            ShowValue();
            var wheels = new SkUiPickerWheels
            {
                FontSize = context.FontSize,
                TextColor = context.TextColor,
                Margin = new Thickness(16, 8),
                CommitDelay = TimeSpan.Zero,
                Columns = { _hours, _minutes }
            };
            wheels.SelectionChanged += (_, _) =>
            {
                if (!_showing && _hours.SelectedItem is int hours && _minutes.SelectedItem is int minutes)
                    SetValue(new TimeSpan(hours, minutes, 0));
            };
            return wheels;
        }

        /// <summary>The value changed (the user, Clear, the app): the wheels show it, without that being a choice.</summary>
        protected override void OnValueChanged() => ShowValue();

        private void ShowValue()
        {
            _showing = true;
            try
            {
                var value = Value ?? TimeSpan.FromMinutes(30);
                _hours.SelectedItem = (int)value.TotalHours;
                _minutes.SelectedItem = value.Minutes / 5 * 5;
            }
            finally
            {
                _showing = false;
            }
        }
    }
}
