using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Base page that hosts a SkiaUi preview, optional MAUI counterpart, and property editors.</summary>
public abstract class ComponentDemoPage : ContentPage
{
    /// <summary>Primary body text color for captions and editors.</summary>
    protected static readonly Color Ink = DemoColors.Ink;
    /// <summary>Interactive accent used by editors and sample fills.</summary>
    protected static readonly Color Accent = DemoColors.Accent;
    private readonly Grid _comparisons = new() { ColumnSpacing = 16, RowSpacing = 12 };
    // Control-specific editors (added by the derived page) come first; the common ones the base page adds for every
    // control (HwAccelerated, sizes, opacity, ...) follow under their own heading.
    private readonly Grid _specificEditors = new() { RowSpacing = 10 };
    private readonly Grid _commonEditors = new() { RowSpacing = 10 };
    private readonly Grid _editors = new() { RowSpacing = 16, Padding = new Thickness(0, 8, 0, 24) };
    private bool _addingCommonEditors;
    private readonly List<Action> _resets = [];
    private readonly List<(string Name, Func<bool> Check)> _checks = [];
    private readonly View? _nativePanel;
    private readonly Grid? _nativeArea;
    private readonly Grid _skiaPanel;
    private readonly Label _result;
    private readonly Label _skiaStatus;
    private readonly Label? _nativeStatus;
    private SkUiContentView _host;
    private bool _hwAccelerated = true;

    internal SkUiView SkiaControl { get; }
    internal View? NativeControl { get; }
    internal bool IsWide { get; private set; }
    internal Grid Editors => _editors;

    /// <summary>Whether the preview host uses a GPU Skia surface (<see cref="SkUiView.HwAccelerated"/>).</summary>
    protected bool HwAccelerated => _hwAccelerated;

    protected ComponentDemoPage(string name, SkUiView skia, View? native = null, (double Min, double Max, double Initial)? widthRange = null, (double Min, double Max, double Initial)? heightRange = null)
    {
        Title = name;
        Background = DemoColors.PageBackground;
        SkiaControl = skia;
        NativeControl = native;
        skia.AutomationId = "SkiaPreview";
        if (native is not null) native.AutomationId = "NativePreview";
        _host = CreatePreviewHost(skia, hwAccelerated: true);
        skia.HorizontalOptions = LayoutOptions.Center;
        skia.VerticalOptions = LayoutOptions.Center;
        if (native is not null)
        {
            native.HorizontalOptions = LayoutOptions.Center;
            native.VerticalOptions = LayoutOptions.Center;
        }
        _skiaPanel = MakePanel("SkUi", _host, out _skiaStatus);
        _comparisons.Add(_skiaPanel);
        if (native is not null)
        {
            // Clipped like the drawn host, so a native control larger than its area cannot cover the other panel or the editors.
            _nativeArea = new Grid { IsClippedToBounds = true, Children = { native } };
            _nativePanel = MakePanel("MAUI", _nativeArea, out var status);
            _nativeStatus = status;
            _comparisons.Add(_nativePanel);
        }
        _result = Caption("", "PropertyCheckResult");
        _result.HeightRequest = 40;
        var propertyArea = new ScrollView { Content = _editors, AutomationId = "PropertyEditors" };
        var root = new Grid { Padding = 12, RowSpacing = 8, RowDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)] };
        root.Add(_comparisons);
        root.Add(propertyArea, 0, 1);
        root.Add(_result, 0, 2);
        Content = root;
        ToolbarItems.Add(new ToolbarItem("Reset", null, ResetProperties));
        ToolbarItems.Add(new ToolbarItem("Check properties", null, () => CheckProperties()));
        // Blocks the UI thread to demonstrate that HW surfaces keep compositing, spinning and flinging on the
        // render thread (start a fling or watch a spinner, then tap this).
        // Primary order: iOS Shell turns Secondary items into UIMenu actions that require an icon
        // (a null IconImageSource crashes in UIImage.FromBundle).
        ToolbarItems.Add(new ToolbarItem("Stall 2s", null, () => Thread.Sleep(2000)) { AutomationId = "StallUi" });
        SizeChanged += (_, _) => UpdateComparisonLayout(Width);
        UpdateComparisonLayout(0);
        skia.SizeChanged += (_, _) => UpdateBounds();
        if (native is not null) native.SizeChanged += (_, _) => UpdateBounds();
        _editors.RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto)];
        _editors.Add(_specificEditors, 0, 0);
        _editors.Add(new Label
        {
            Text = "Common properties", TextColor = DemoColors.Caption, FontSize = 13, FontFamily = DemoFonts.OpenSansSemibold,
            Margin = new Thickness(0, 8, 0, 0)
        }, 0, 1);
        _editors.Add(_commonEditors, 0, 2);
        _addingCommonEditors = true;
        Toggle("HwAccelerated", true, ApplyHwAcceleration, () => _hwAccelerated);
        // Without explicit ranges the sizes switch to the control's natural size (its SkUiLook default), measured once
        // the page is loaded, and the preview area is twice the initial height.
        _autoSize = widthRange is null && heightRange is null;
        Number(nameof(View.WidthRequest), widthRange?.Min ?? 60, widthRange?.Max ?? 260, widthRange?.Initial ?? 220, value => SetBoth(View.WidthRequestProperty, value), () => skia.WidthRequest, native is null ? null : () => native.WidthRequest);
        Number(nameof(View.HeightRequest), heightRange?.Min ?? 40, heightRange?.Max ?? 160, heightRange?.Initial ?? 120, value => SetBoth(View.HeightRequestProperty, value), () => skia.HeightRequest, native is null ? null : () => native.HeightRequest);
        if (_autoSize)
            Loaded += OnLoadedAutoSize;
        Number(nameof(VisualElement.Opacity), 0, 1, 1, value => SetBoth(VisualElement.OpacityProperty, value), () => skia.Opacity, native is null ? null : () => native.Opacity);
        Toggle(nameof(VisualElement.IsEnabled), true, value => SetBoth(VisualElement.IsEnabledProperty, value), () => skia.IsEnabled, native is null ? null : () => native.IsEnabled);
        Toggle(nameof(VisualElement.IsVisible), true, value => SetBoth(VisualElement.IsVisibleProperty, value), () => skia.IsVisible, native is null ? null : () => native.IsVisible);
        // Set on both preview panels so the SkUi host (and any recreated host) and the native control inherit it.
        Choice(nameof(VisualElement.FlowDirection), [FlowDirection.MatchParent, FlowDirection.LeftToRight, FlowDirection.RightToLeft], FlowDirection.MatchParent,
            value => { _skiaPanel.FlowDirection = value; if (_nativePanel is not null) _nativePanel.FlowDirection = value; },
            () => _skiaPanel.FlowDirection, _nativePanel is null ? null : () => _nativePanel.FlowDirection);
        _addingCommonEditors = false;
    }

    private readonly bool _autoSize;
    private double _areaHeight = -1;

    private void OnLoadedAutoSize(object? sender, EventArgs args)
    {
        Loaded -= OnLoadedAutoSize;
        // The fallback size was applied at construction; clear it to measure the natural size.
        SkiaControl.WidthRequest = -1;
        SkiaControl.HeightRequest = -1;
        var natural = ((IView)SkiaControl).Measure(double.PositiveInfinity, double.PositiveInfinity);
        // Controls without a natural size (empty views, layouts) keep the old defaults. Scrollers measure to their whole
        // content, so their pages pass explicit size ranges instead.
        var width = natural.Width >= 1 ? Math.Ceiling(natural.Width) : 220;
        var height = natural.Height >= 1 ? Math.Ceiling(natural.Height) : 120;
        SetNumberInitial(nameof(View.WidthRequest), width, Math.Max(8, Math.Floor(width / 4)), Math.Max(width * 2, 320));
        SetNumberInitial(nameof(View.HeightRequest), height, Math.Max(8, Math.Floor(height / 4)), height * 2);
        _areaHeight = height * 2;
        ApplyAreaHeight();
        UpdateComparisonLayout(Width);
    }

    /// <summary>Auto-sized pages: preview (and native comparison) area of <see cref="_areaHeight"/>.</summary>
    private void ApplyAreaHeight()
    {
        if (_areaHeight <= 0)
            return;
        _host.HeightRequest = _areaHeight;
        if (_nativeArea is not null)
            _nativeArea.HeightRequest = _areaHeight;
    }

    private static SkUiContentView CreatePreviewHost(ISkUiView? content, bool hwAccelerated)
    {
        // Ctor defaults HwAccelerated=true; set before the host joins the visual tree / gets a handler.
        var host = new SkUiContentView
        {
            Background = Colors.White,
            BackgroundColor = Colors.White,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            AutomationId = "PreviewHost",
        };
        host.HwAccelerated = hwAccelerated;
        host.Content = content;
        return host;
    }

    /// <summary>
    /// Recreates the preview <see cref="SkUiContentView"/> host with the given acceleration mode
    /// (required because <see cref="SkUiView.HwAccelerated"/> is frozen at handler creation).
    /// </summary>
    private void ApplyHwAcceleration(bool enabled)
    {
        if (_hwAccelerated == enabled && _host.HwAccelerated == enabled)
            return;

        _hwAccelerated = enabled;
        var content = _host.Content;
        _host.Content = null;
        _host.AnimationClock.StopAll();
        _skiaPanel.Remove(_host);
        _host = CreatePreviewHost(content, enabled);
        ApplyAreaHeight();
        _skiaPanel.Add(_host, 0, 1);
        UpdateBounds();
    }

    private static Grid MakePanel(string title, View view, out Label status)
    {
        status = Caption("", title + "Status");
        status.HeightRequest = 32;
        var grid = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)], Background = Colors.White };
        grid.Add(Caption(title, title + "Heading"));
        grid.Add(view, 0, 1);
        grid.Add(status, 0, 2);
        return grid;
    }

    internal void UpdateComparisonLayout(double width)
    {
        IsWide = width >= 720 && _nativePanel is not null;
        _comparisons.ColumnDefinitions = IsWide ? [new(GridLength.Star), new(GridLength.Star)] : [new(GridLength.Star)];
        // Auto-sized pages: the panels take their content height (preview area = 2 x the control's initial height).
        var row = _areaHeight > 0 ? GridLength.Auto : GridLength.Star;
        _comparisons.RowDefinitions = _nativePanel is not null && !IsWide ? [new(row), new(row)] : [new(row)];
        if (_nativePanel is not null)
        {
            Grid.SetColumn(_nativePanel, IsWide ? 1 : 0);
            Grid.SetRow(_nativePanel, IsWide ? 0 : 1);
        }
        // Narrow screens stack the two panels, each as tall as a single one.
        _comparisons.HeightRequest = _areaHeight > 0 ? -1
            : _nativePanel is not null && !IsWide ? 2 * _singlePanelHeight + _comparisons.RowSpacing : _singlePanelHeight;
    }

    private double _singlePanelHeight = 206;

    /// <summary>Height of the preview area on pages without a MAUI counterpart (default 206).</summary>
    protected double SinglePanelHeight
    {
        get => _singlePanelHeight;
        set
        {
            _singlePanelHeight = value;
            UpdateComparisonLayout(Width);
        }
    }

    protected static Label Caption(string text, string? automationId = null) => new()
    {
        Text = text, TextColor = Ink, FontSize = 13, FontFamily = DemoFonts.OpenSansRegular,
        AutomationId = automationId, VerticalTextAlignment = TextAlignment.Center
    };

    protected void AddEditor(string title, View editor)
    {
        var editors = _addingCommonEditors ? _commonEditors : _specificEditors;
        var row = editors.RowDefinitions.Count;
        editors.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        var field = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto)], RowSpacing = 2 };
        field.Add(Caption(title));
        field.Add(editor, 0, 1);
        editors.Add(field, 0, row);
    }

    private void SetBoth(BindableProperty property, object value)
    {
        SkiaControl.SetValue(property, value);
        NativeControl?.SetValue(property, value);
    }

    /// <param name="whole">Snaps the slider to whole numbers (for <c>int</c> properties such as a button's <c>CornerRadius</c>).</param>
    protected void Number(string name, double minimum, double maximum, double initial, Action<double> apply, Func<double> skia, Func<double>? native = null, bool whole = false)
    {
        var slider = new Slider { Minimum = minimum, Maximum = maximum, Value = initial, MinimumTrackColor = Accent, AutomationId = "Edit" + name };
        var valueLabel = Caption(initial.ToString("0.##"));
        var row = new Grid { ColumnDefinitions = [new(GridLength.Star), new(new GridLength(48))] };
        row.Add(slider);
        row.Add(valueLabel, 1);
        void Apply(double value) { apply(value); valueLabel.Text = value.ToString("0.##"); }
        slider.ValueChanged += (_, args) =>
        {
            if (whole && args.NewValue != Math.Round(args.NewValue))
                slider.Value = Math.Round(args.NewValue); // raises ValueChanged again with the whole number
            else
                Apply(args.NewValue);
        };
        AddEditor(name, row);
        _numbers[name] = (slider, initial, Apply);
        _resets.Add(() => SetSlider(_numbers[name].Slider, _numbers[name].Initial, Apply));
        _checks.Add((name, () => Math.Abs(skia() - slider.Value) < 0.001 && (native is null || Math.Abs(native() - slider.Value) < 0.001)));
        apply(initial);
    }

    // Assigning the same Value does not raise ValueChanged; only then apply explicitly.
    private static void SetSlider(Slider slider, double value, Action<double> apply)
    {
        if (Math.Abs(slider.Value - value) < 0.001) apply(value);
        else slider.Value = value;
    }

    private readonly Dictionary<string, (Slider Slider, double Initial, Action<double> Apply)> _numbers = [];

    /// <summary>
    /// Replaces the initial (and Reset) value of a <see cref="Number"/> editor and applies it, clamped to its range
    /// (optionally replaced too); e.g. a size known only after layout.
    /// </summary>
    protected void SetNumberInitial(string name, double value, double? minimum = null, double? maximum = null)
    {
        if (!_numbers.TryGetValue(name, out var number))
            return;
        var slider = number.Slider;
        if (minimum is { } min && maximum is { } max)
        {
            // Keep Minimum <= Maximum at every step.
            if (min > slider.Maximum) { slider.Maximum = max; slider.Minimum = min; }
            else { slider.Minimum = min; slider.Maximum = max; }
        }
        value = Math.Clamp(value, slider.Minimum, slider.Maximum);
        _numbers[name] = (slider, value, number.Apply);
        SetSlider(slider, value, number.Apply);
    }

    protected void Toggle(string name, bool initial, Action<bool> apply, Func<bool> skia, Func<bool>? native = null)
    {
        var toggle = new Switch { IsToggled = initial, OnColor = Accent, HorizontalOptions = LayoutOptions.Start, AutomationId = "Edit" + name };
        toggle.Toggled += (_, args) => apply(args.Value);
        AddEditor(name, toggle);
        _resets.Add(() =>
        {
            if (toggle.IsToggled == initial) apply(initial);
            else toggle.IsToggled = initial;
        });
        _checks.Add((name, () => skia() == toggle.IsToggled && (native is null || native() == toggle.IsToggled)));
        apply(initial);
    }

    protected void Text(string name, string initial, Action<string> apply, Func<string> skia, Func<string>? native = null)
    {
        var entry = new Entry { Text = initial, Background = Colors.White, TextColor = Ink, PlaceholderColor = DemoColors.Caption, AutomationId = "Edit" + name };
        entry.TextChanged += (_, args) => apply(args.NewTextValue ?? string.Empty);
        AddEditor(name, entry);
        _resets.Add(() =>
        {
            if (entry.Text == initial) apply(initial);
            else entry.Text = initial;
        });
        _checks.Add((name, () => skia() == entry.Text && (native is null || native() == entry.Text)));
        apply(initial);
    }

    /// <summary>A multi-line text editor (e.g. for Label's Text) using an <see cref="Editor"/> instead of a single-line Entry.</summary>
    protected Editor MultilineText(string name, string initial, Action<string> apply, Func<string> skia, Func<string>? native = null)
    {
        var editor = new Editor
        {
            Text = initial, Background = Colors.White, TextColor = Ink, PlaceholderColor = DemoColors.Caption,
            AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 90, AutomationId = "Edit" + name
        };
        editor.TextChanged += (_, args) => apply(args.NewTextValue ?? string.Empty);
        AddEditor(name, editor);
        _resets.Add(() =>
        {
            if (editor.Text == initial) apply(initial);
            else editor.Text = initial;
        });
        _checks.Add((name, () => skia() == editor.Text && (native is null || native() == editor.Text)));
        apply(initial);
        return editor;
    }

    protected void Choice<T>(string name, T[] values, T initial, Action<T> apply, Func<T> skia, Func<T>? native = null) where T : notnull
    {
        var picker = new Picker { Title = name, Background = Colors.White, TextColor = Ink, TitleColor = DemoColors.Caption, AutomationId = "Edit" + name };
        foreach (var value in values) picker.Items.Add(value.ToString()!);
        picker.SelectedIndex = Array.IndexOf(values, initial);
        picker.SelectedIndexChanged += (_, _) => { if (picker.SelectedIndex >= 0) apply(values[picker.SelectedIndex]); };
        AddEditor(name, picker);
        var initialIndex = Array.IndexOf(values, initial);
        _resets.Add(() =>
        {
            if (picker.SelectedIndex == initialIndex) apply(initial);
            else picker.SelectedIndex = initialIndex;
        });
        _checks.Add((name, () => picker.SelectedIndex >= 0 && EqualityComparer<T>.Default.Equals(skia(), values[picker.SelectedIndex])
            && (native is null || EqualityComparer<T>.Default.Equals(native(), values[picker.SelectedIndex]))));
        apply(initial);
    }

    protected void ColorEditor(string name, Color initial, Action<Color> apply, Func<Color> skia, Func<Color>? native = null)
    {
        var selected = initial;
        var palette = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star), new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 8 };
        var colors = new[] { initial, DemoColors.SampleA, DemoColors.SampleB, DemoColors.Ink };
        for (var index = 0; index < colors.Length; index++)
        {
            var color = colors[index];
            var swatch = new Button { Background = color, HeightRequest = 44, BorderColor = Colors.Gray, BorderWidth = 1, CornerRadius = 4, AutomationId = $"Edit{name}{index}" };
            SemanticProperties.SetDescription(swatch, name + " " + color.ToArgbHex());
            ToolTipProperties.SetText(swatch, color.ToArgbHex());
            swatch.Clicked += (_, _) => { selected = color; apply(color); };
            palette.Add(swatch, index);
        }
        AddEditor(name, palette);
        _resets.Add(() => { selected = initial; apply(initial); });
        _checks.Add((name, () => skia().Equals(selected) && (native is null || native().Equals(selected))));
        apply(initial);
    }

    private static readonly string[] BrushNames = ["Solid", "Linear gradient", "Radial gradient"];
    private static readonly string[] ShadowNames = ["None", "Soft", "Sharp", "Gradient"];
    private static readonly string[] ClipNames = ["None", "Ellipse", "Rounded rectangle"];

    /// <summary>
    /// MAUI parity P7 editors on both previews: a solid or gradient <c>Background</c> (<paramref name="setBackground"/> applies a
    /// gradient, or <c>null</c> for the page's solid fill, e.g. a BoxView's color), a <c>Shadow</c> and a <c>Clip</c> geometry.
    /// </summary>
    protected void EffectEditors(VisualElement skia, VisualElement? native, Action<VisualElement, Brush?>? setBackground = null)
    {
        setBackground ??= (view, brush) => view.Background = brush;
        Choice("Background brush", BrushNames, "Solid", name =>
        {
            setBackground(skia, CreateBrush(name));
            if (native is not null) setBackground(native, CreateBrush(name));
        }, () => BrushName(skia.Background), native is null ? null : () => BrushName(native.Background));
        Choice(nameof(VisualElement.Shadow), ShadowNames, "None", name =>
        {
            skia.Shadow = CreateShadow(name)!;
            if (native is not null) native.Shadow = CreateShadow(name)!;
        }, () => ShadowName(skia.Shadow), native is null ? null : () => ShadowName(native.Shadow));
        Choice(nameof(VisualElement.Clip), ClipNames, "None", name =>
        {
            skia.Clip = CreateClip(name, skia);
            if (native is not null) native.Clip = CreateClip(name, native);
        }, () => ClipName(skia.Clip), native is null ? null : () => ClipName(native.Clip));
    }

    private static Brush? CreateBrush(string name) => name switch
    {
        "Linear gradient" => new LinearGradientBrush([new GradientStop(Accent, 0), new GradientStop(DemoColors.SampleA, 1)], new Point(0, 0), new Point(1, 1)),
        "Radial gradient" => new RadialGradientBrush([new GradientStop(DemoColors.SampleA, 0), new GradientStop(Accent, 1)], new Point(0.5, 0.5), 0.6),
        _ => null
    };

    private static string BrushName(Brush? brush) => brush switch
    {
        LinearGradientBrush => "Linear gradient",
        RadialGradientBrush => "Radial gradient",
        _ => "Solid"
    };

    private static Shadow? CreateShadow(string name) => name switch
    {
        "Soft" => new Shadow { Brush = Colors.Black, Offset = new Point(6, 8), Radius = 14, Opacity = 0.45f },
        "Sharp" => new Shadow { Brush = DemoColors.Ink, Offset = new Point(8, 8), Radius = 0, Opacity = 0.8f },
        "Gradient" => new Shadow
        {
            Brush = new LinearGradientBrush([new GradientStop(DemoColors.SampleA, 0), new GradientStop(Accent, 1)], new Point(0, 0), new Point(1, 0)),
            Offset = new Point(0, 10), Radius = 12, Opacity = 0.9f
        },
        _ => null
    };

    private static string ShadowName(Shadow? shadow) => shadow switch
    {
        null => "None",
        { Brush: GradientBrush } => "Gradient",
        { Radius: 0 } => "Sharp",
        _ => "Soft"
    };

    /// <summary>A clip in the view's coordinates (MAUI geometries do not follow the size): sized from its current bounds.</summary>
    private static Microsoft.Maui.Controls.Shapes.Geometry? CreateClip(string name, VisualElement view)
    {
        var width = view.Width > 0 ? view.Width : 160;
        var height = view.Height > 0 ? view.Height : 100;
        return name switch
        {
            "Ellipse" => new Microsoft.Maui.Controls.Shapes.EllipseGeometry { Center = new Point(width / 2, height / 2), RadiusX = width / 2, RadiusY = height / 2 },
            "Rounded rectangle" => new Microsoft.Maui.Controls.Shapes.RoundRectangleGeometry(new CornerRadius(24, 4, 24, 4), new Rect(0, 0, width, height)),
            _ => null
        };
    }

    private static string ClipName(Microsoft.Maui.Controls.Shapes.Geometry? clip) => clip switch
    {
        Microsoft.Maui.Controls.Shapes.EllipseGeometry => "Ellipse",
        Microsoft.Maui.Controls.Shapes.RoundRectangleGeometry => "Rounded rectangle",
        _ => "None"
    };

    protected void ActionButton(string title, Action action)
    {
        var button = new Button { Text = title, Background = Accent, TextColor = Colors.White, AutomationId = title.Replace(" ", "") };
        button.Clicked += (_, _) => action();
        AddEditor(title, button);
    }

    protected void OnReset(Action action) => _resets.Add(action);
    protected void Feedback(string skia, string? native = null)
    {
        _skiaStatus.Text = skia;
        if (_nativeStatus is not null) _nativeStatus.Text = native ?? string.Empty;
    }

    private void UpdateBounds()
    {
        var hw = _hwAccelerated ? "on" : "off";
        Feedback(
            $"Bounds {SkiaControl.Width:F0} x {SkiaControl.Height:F0}  ·  HW {hw}",
            NativeControl is null ? null : $"Bounds {NativeControl.Width:F0} x {NativeControl.Height:F0}");
    }

    internal string[] CheckProperties()
    {
        var failures = _checks.Where(check => !check.Check()).Select(check => check.Name).ToArray();
        _result.Text = failures.Length == 0 ? $"PASS: {_checks.Count} property checks" : "FAIL: " + string.Join(", ", failures);
        _result.TextColor = failures.Length == 0 ? DemoColors.Pass : DemoColors.Fail;
        return failures;
    }

    internal void ResetProperties()
    {
        foreach (var reset in _resets) reset();
        _result.Text = string.Empty;
        UpdateBounds();
    }

    protected override void OnDisappearing()
    {
        _host.AnimationClock.StopAll();
        base.OnDisappearing();
    }
}
