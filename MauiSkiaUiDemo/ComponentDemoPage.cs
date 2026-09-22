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
    private readonly Grid _editors = new() { RowSpacing = 10, Padding = new Thickness(0, 8, 0, 24) };
    private readonly List<Action> _resets = [];
    private readonly List<(string Name, Func<bool> Check)> _checks = [];
    private readonly View? _nativePanel;
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
            _nativePanel = MakePanel("MAUI", native, out var status);
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
        SizeChanged += (_, _) => UpdateComparisonLayout(Width);
        UpdateComparisonLayout(0);
        skia.SizeChanged += (_, _) => UpdateBounds();
        if (native is not null) native.SizeChanged += (_, _) => UpdateBounds();
        Toggle("HwAccelerated", true, ApplyHwAcceleration, () => _hwAccelerated);
        Number(nameof(View.WidthRequest), widthRange?.Min ?? 60, widthRange?.Max ?? 260, widthRange?.Initial ?? 220, value => SetBoth(View.WidthRequestProperty, value), () => skia.WidthRequest, native is null ? null : () => native.WidthRequest);
        Number(nameof(View.HeightRequest), heightRange?.Min ?? 40, heightRange?.Max ?? 160, heightRange?.Initial ?? 120, value => SetBoth(View.HeightRequestProperty, value), () => skia.HeightRequest, native is null ? null : () => native.HeightRequest);
        Number(nameof(VisualElement.Opacity), 0, 1, 1, value => SetBoth(VisualElement.OpacityProperty, value), () => skia.Opacity, native is null ? null : () => native.Opacity);
        Toggle(nameof(VisualElement.IsEnabled), true, value => SetBoth(VisualElement.IsEnabledProperty, value), () => skia.IsEnabled, native is null ? null : () => native.IsEnabled);
        Toggle(nameof(VisualElement.IsVisible), true, value => SetBoth(VisualElement.IsVisibleProperty, value), () => skia.IsVisible, native is null ? null : () => native.IsVisible);
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
        _comparisons.RowDefinitions = _nativePanel is not null && !IsWide ? [new(GridLength.Star), new(GridLength.Star)] : [new(GridLength.Star)];
        if (_nativePanel is not null)
        {
            Grid.SetColumn(_nativePanel, IsWide ? 1 : 0);
            Grid.SetRow(_nativePanel, IsWide ? 0 : 1);
        }
        _comparisons.HeightRequest = _nativePanel is not null && !IsWide ? 424 : 206;
    }

    protected static Label Caption(string text, string? automationId = null) => new()
    {
        Text = text, TextColor = Ink, FontSize = 13, FontFamily = DemoFonts.OpenSansRegular,
        AutomationId = automationId, VerticalTextAlignment = TextAlignment.Center
    };

    protected void AddEditor(string title, View editor)
    {
        var row = _editors.RowDefinitions.Count;
        _editors.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        var field = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto)], RowSpacing = 2 };
        field.Add(Caption(title));
        field.Add(editor, 0, 1);
        _editors.Add(field, 0, row);
    }

    private void SetBoth(BindableProperty property, object value)
    {
        SkiaControl.SetValue(property, value);
        NativeControl?.SetValue(property, value);
    }

    protected void Number(string name, double minimum, double maximum, double initial, Action<double> apply, Func<double> skia, Func<double>? native = null)
    {
        var slider = new Slider { Minimum = minimum, Maximum = maximum, Value = initial, MinimumTrackColor = Accent, AutomationId = "Edit" + name };
        var valueLabel = Caption(initial.ToString("0.##"));
        var row = new Grid { ColumnDefinitions = [new(GridLength.Star), new(new GridLength(48))] };
        row.Add(slider);
        row.Add(valueLabel, 1);
        slider.ValueChanged += (_, args) => { apply(args.NewValue); valueLabel.Text = args.NewValue.ToString("0.##"); };
        AddEditor(name, row);
        _resets.Add(() =>
        {
            // Assigning the same Value does not raise ValueChanged; only then apply explicitly.
            if (Math.Abs(slider.Value - initial) < 0.001) apply(initial);
            else slider.Value = initial;
        });
        _checks.Add((name, () => Math.Abs(skia() - slider.Value) < 0.001 && (native is null || Math.Abs(native() - slider.Value) < 0.001)));
        apply(initial);
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
    protected void MultilineText(string name, string initial, Action<string> apply, Func<string> skia, Func<string>? native = null)
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
