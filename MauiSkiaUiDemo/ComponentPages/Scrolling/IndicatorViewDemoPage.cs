using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiIndicatorView"/> (C4) on its own, next to MAUI's <see cref="IndicatorView"/>: tap a dot to select it (the drawn
/// selection moves there with the look's transition). Editors set MAUI's properties on both; SkiaUi-only editors set the
/// spacing and the direction. The look draws the indicators: dots or a pill (Look &amp; colors page), or a painter of the app's.
/// </summary>
public sealed class IndicatorViewDemoPage : ComponentDemoPage
{
    private readonly SkUiIndicatorView _skia;
    private readonly IndicatorView _native;

    public IndicatorViewDemoPage()
        : base(nameof(SkUiIndicatorView), new SkUiIndicatorView(), new IndicatorView(), widthRange: (60, 400, 200), heightRange: (10, 200, 40))
    {
        _skia = (SkUiIndicatorView)SkiaControl;
        _native = (IndicatorView)NativeControl!;
        _native.IndicatorColor = _skia.IndicatorColor;
        _native.SelectedIndicatorColor = _skia.SelectedIndicatorColor;
        _skia.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SkUiIndicatorView.Position))
                Status();
        };
        _native.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(IndicatorView.Position))
                Status();
        };

        Number(nameof(IndicatorView.Count), 0, 12, 5, value => { _skia.Count = (int)value; _native.Count = (int)value; },
            () => _skia.Count, () => _native.Count, whole: true);
        Number(nameof(IndicatorView.Position), 0, 11, 0, value => { _skia.Position = (int)value; _native.Position = (int)value; },
            () => _skia.Position, () => _native.Position, whole: true);
        Number(nameof(IndicatorView.IndicatorSize), 2, 24, 6, value => { _skia.IndicatorSize = value; _native.IndicatorSize = value; },
            () => _skia.IndicatorSize, () => _native.IndicatorSize, whole: true);
        Choice(nameof(IndicatorView.IndicatorsShape), Enum.GetValues<IndicatorShape>(), IndicatorShape.Circle,
            value => { _skia.IndicatorsShape = value; _native.IndicatorsShape = value; },
            () => _skia.IndicatorsShape, () => _native.IndicatorsShape);
        Number(nameof(IndicatorView.MaximumVisible), 1, 12, 12, value => { _skia.MaximumVisible = (int)value; _native.MaximumVisible = (int)value; },
            () => _skia.MaximumVisible, () => _native.MaximumVisible, whole: true);
        Toggle(nameof(IndicatorView.HideSingle), true, value => { _skia.HideSingle = value; _native.HideSingle = value; },
            () => _skia.HideSingle, () => _native.HideSingle);
        ColorEditor(nameof(IndicatorView.IndicatorColor), SkUiColors.TrackOff,
            value => { _skia.IndicatorColor = value; _native.IndicatorColor = value; },
            () => _skia.IndicatorColor, () => _native.IndicatorColor);
        ColorEditor(nameof(IndicatorView.SelectedIndicatorColor), SkUiColors.Accent,
            value => { _skia.SelectedIndicatorColor = value; _native.SelectedIndicatorColor = value; },
            () => _skia.SelectedIndicatorColor, () => _native.SelectedIndicatorColor);
        // SkiaUi only.
        Number(nameof(SkUiIndicatorView.IndicatorSpacing), -1, 24, -1, value => _skia.IndicatorSpacing = value, () => _skia.IndicatorSpacing, whole: true);
        Choice(nameof(SkUiIndicatorView.Orientation), Enum.GetValues<StackOrientation>(), StackOrientation.Horizontal,
            value => _skia.Orientation = value, () => _skia.Orientation);
        Status();
    }

    private void Status() => Feedback($"Position {_skia.Position}", $"Position {_native.Position}");
}
