using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// Small icons in a tight toolbar, on both layers, with <see cref="SkUiView.HitSlop"/> and
/// <see cref="SkUiView.MinimumTouchTargetSize"/>: a press just past an icon still taps it where no other control is under
/// the finger; an exact hit on a neighbour wins, and between two touch areas the nearer icon does. The tinted squares show
/// each icon's touch area (also its frame for screen readers); they take no input.
/// </summary>
public sealed class TouchTargetsDemoPage : ComponentDemoPage
{
    private const double IconSize = 22;
    private const double RowHeight = 80;
    private const double Tolerance = 0.001; // editor values compared back (both layers hold the same)
    private const string Gap = "Gap between icons";
    private const string ShowTouchAreas = "Show touch areas";
    private static readonly string[] Names = ["Back", "Share", "Star", "Edit", "More"];
    private static readonly Color[] IconColors =
        [DemoColors.Accent, Color.FromArgb("#D97706"), Color.FromArgb("#6B3FA0"), Color.FromArgb("#2E7D32"), Color.FromArgb("#546E7A")];
    private static readonly Color AreaColor = DemoColors.Accent.WithAlpha(0.18f);

    private readonly SkUiAbsoluteLayout _row = new() { HeightRequest = RowHeight };
    private readonly SkUiCoreAbsoluteLayout _coreRow = new();
    private readonly List<SkUiBox> _icons = [];
    private readonly List<SkUiBox> _areas = [];
    private readonly List<SkUiCoreBox> _coreIcons = [];
    private readonly List<SkUiCoreBox> _coreAreas = [];
    private double _slop;
    private double _minimum;
    private double _gap = 8;
    private bool _showAreas = true;

    public TouchTargetsDemoPage() : base("Touch targets", new SkUiVerticalStackLayout { Spacing = 6, Padding = new Thickness(12) }, native: null,
        widthRange: (280, 420, 320))
    {
        var taps = new Dictionary<string, int>();
        void Count(string name)
        {
            taps[name] = taps.GetValueOrDefault(name) + 1;
            Feedback($"{name}: {taps[name]}");
        }
        foreach (var (name, index) in Names.Select((name, index) => (name, index)))
        {
            var area = new SkUiBox { Color = AreaColor, InputTransparent = true };
            var icon = new SkUiBox { Color = IconColors[index], CornerRadius = IconSize / 2 };
            SemanticProperties.SetDescription(icon, name);
            icon.Tapped += (_, _) => Count(name);
            _areas.Add(area);
            _icons.Add(icon);

            var coreArea = new SkUiCoreBox().SetColor(AreaColor);
            coreArea.SetInputTransparent(true);
            var coreIcon = new SkUiCoreBox().SetColor(IconColors[index]).SetCornerRadius(IconSize / 2);
            coreIcon.SetSemanticDescription("Core " + name);
            coreIcon.Tapped += (_, _) => Count("Core " + name);
            _coreAreas.Add(coreArea);
            _coreIcons.Add(coreIcon);
        }
        // The touch areas under the icons, drawn first.
        foreach (var area in _areas)
            _row.Children.Add(area);
        foreach (var icon in _icons)
            _row.Children.Add(icon);
        foreach (var area in _coreAreas)
            _coreRow.Add(area);
        foreach (var icon in _coreIcons)
            _coreRow.Add(icon);
        _coreRow.SetHeight(RowHeight);

        var stack = (SkUiVerticalStackLayout)SkiaControl;
        stack.Children.Add(Caption("Drawn views (SkUiBox)"));
        stack.Children.Add(_row);
        stack.Children.Add(Caption("Core nodes (SkUiCoreBox)"));
        stack.Children.Add(new SkUiCoreHost { HeightRequest = RowHeight }.SetContent(_coreRow));
        Place();

        DocumentTypes(typeof(SkUiView));
        Number(nameof(SkUiView.HitSlop), 0, 24, 0, value =>
        {
            _slop = value;
            foreach (var icon in _icons)
                icon.HitSlop = new Thickness(value);
            foreach (var icon in _coreIcons)
                icon.SetHitSlop(new Thickness(value));
            Place();
        }, () => _coreIcons[0].HitSlop == _icons[0].HitSlop ? _icons[0].HitSlop.Left : double.NaN, whole: true);
        Number(nameof(SkUiView.MinimumTouchTargetSize), 0, 64, 0, value =>
        {
            _minimum = value;
            foreach (var icon in _icons)
                icon.MinimumTouchTargetSize = value;
            foreach (var icon in _coreIcons)
                icon.SetMinimumTouchTargetSize(value);
            Place();
        }, () => Math.Abs(_coreIcons[0].MinimumTouchTargetSize - _icons[0].MinimumTouchTargetSize) < Tolerance ? _icons[0].MinimumTouchTargetSize : double.NaN, whole: true);
        // At most 20: five icons with the largest slop still fit the narrowest preview.
        Number(Gap, 0, 20, _gap, value =>
        {
            _gap = value;
            Place();
        }, () => _gap, whole: true);
        Toggle(ShowTouchAreas, true, value =>
        {
            _showAreas = value;
            Place();
        }, () => _showAreas);
        OnReset(taps.Clear);
    }

    /// <summary>Places the icons <see cref="_gap"/> apart and sizes each touch area as the slop and minimum make it.</summary>
    private void Place()
    {
        // The larger of the slop and what the minimum adds on each side (the icons are square, the slop even).
        var reach = Math.Max(_slop, Math.Max(0, (_minimum - IconSize) / 2));
        var top = (RowHeight - IconSize) / 2;
        for (var index = 0; index < Names.Length; index++)
        {
            var left = 24 + index * (IconSize + _gap);
            var iconBounds = new Rect(left, top, IconSize, IconSize);
            var areaBounds = new Rect(left - reach, top - reach, IconSize + 2 * reach, IconSize + 2 * reach);
            SkUiAbsoluteLayout.SetLayoutBounds(_icons[index], iconBounds);
            SkUiAbsoluteLayout.SetLayoutBounds(_areas[index], areaBounds);
            _areas[index].IsVisible = _showAreas && reach > 0;
            _coreRow.SetLayoutBounds(_coreIcons[index], iconBounds);
            _coreRow.SetLayoutBounds(_coreAreas[index], areaBounds);
            _coreAreas[index].SetIsVisible(_showAreas && reach > 0);
        }
    }
}
