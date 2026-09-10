using MauiSkiaUi;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace MauiSkiaUiDemo;

public abstract class ComponentDemoPage : ContentPage
{
    protected static readonly Color Ink = Color.FromArgb("#202A2C");
    protected static readonly Color Accent = Color.FromArgb("#087F83");
    private readonly Grid comparisons = new() { ColumnSpacing = 16, RowSpacing = 12 };
    private readonly Grid editors = new() { RowSpacing = 10, Padding = new Thickness(0, 8, 0, 24) };
    private readonly List<Action> resets = [];
    private readonly List<(string Name, Func<bool> Check)> checks = [];
    private readonly View? nativePanel;
    private readonly Label result;
    private readonly Label skiaStatus;
    private readonly Label? nativeStatus;
    private readonly SkUiContentView host;

    internal SkUiView SkiaControl { get; }
    internal View? NativeControl { get; }
    internal bool IsWide { get; private set; }
    internal Grid Editors => editors;

    protected ComponentDemoPage(string name, SkUiView skia, View? native = null)
    {
        Title = name;
        Background = Color.FromArgb("#F4F6F6");
        SkiaControl = skia;
        NativeControl = native;
        skia.AutomationId = "SkiaPreview";
        if (native is not null) native.AutomationId = "NativePreview";
        host = new SkUiContentView { Content = skia, Background = Colors.White, AutomationId = "PreviewHost" };
        skia.HorizontalOptions = LayoutOptions.Center;
        skia.VerticalOptions = LayoutOptions.Center;
        if (native is not null)
        {
            native.HorizontalOptions = LayoutOptions.Center;
            native.VerticalOptions = LayoutOptions.Center;
        }
        var skiaPanel = MakePanel("SkUi", host, out skiaStatus);
        comparisons.Add(skiaPanel);
        if (native is not null)
        {
            nativePanel = MakePanel("MAUI", native, out var status);
            nativeStatus = status;
            comparisons.Add(nativePanel);
        }
        result = Caption("", "PropertyCheckResult");
        result.HeightRequest = 40;
        var propertyArea = new ScrollView { Content = editors, AutomationId = "PropertyEditors" };
        var root = new Grid { Padding = 12, RowSpacing = 8, RowDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)] };
        root.Add(comparisons);
        root.Add(propertyArea, 0, 1);
        root.Add(result, 0, 2);
        Content = root;
        ToolbarItems.Add(new ToolbarItem("Reset", null, ResetProperties));
        ToolbarItems.Add(new ToolbarItem("Check properties", null, () => CheckProperties()));
        SizeChanged += (_, _) => UpdateComparisonLayout(Width);
        UpdateComparisonLayout(0);
        skia.SizeChanged += (_, _) => UpdateBounds();
        if (native is not null) native.SizeChanged += (_, _) => UpdateBounds();
        Number("Width", 60, 260, 220, value => SetBoth(View.WidthRequestProperty, value), () => skia.WidthRequest, native is null ? null : () => native.WidthRequest);
        Number("Height", 40, 160, 120, value => SetBoth(View.HeightRequestProperty, value), () => skia.HeightRequest, native is null ? null : () => native.HeightRequest);
        Number("Opacity", 0, 1, 1, value => SetBoth(VisualElement.OpacityProperty, value), () => skia.Opacity, native is null ? null : () => native.Opacity);
        Toggle("Enabled", true, value => SetBoth(VisualElement.IsEnabledProperty, value), () => skia.IsEnabled, native is null ? null : () => native.IsEnabled);
        Toggle("Visible", true, value => SetBoth(VisualElement.IsVisibleProperty, value), () => skia.IsVisible, native is null ? null : () => native.IsVisible);
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
        IsWide = width >= 720 && nativePanel is not null;
        comparisons.ColumnDefinitions = IsWide ? [new(GridLength.Star), new(GridLength.Star)] : [new(GridLength.Star)];
        comparisons.RowDefinitions = nativePanel is not null && !IsWide ? [new(GridLength.Star), new(GridLength.Star)] : [new(GridLength.Star)];
        if (nativePanel is not null)
        {
            Grid.SetColumn(nativePanel, IsWide ? 1 : 0);
            Grid.SetRow(nativePanel, IsWide ? 0 : 1);
        }
        comparisons.HeightRequest = nativePanel is not null && !IsWide ? 424 : 206;
    }

    protected static Label Caption(string text, string? automationId = null) => new()
    {
        Text = text, TextColor = Ink, FontSize = 13, FontFamily = "OpenSansRegular",
        AutomationId = automationId, VerticalTextAlignment = TextAlignment.Center
    };

    protected void AddEditor(string title, View editor)
    {
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

    protected void Number(string name, double minimum, double maximum, double initial, Action<double> apply, Func<double> skia, Func<double>? native = null)
    {
        var slider = new Slider { Minimum = minimum, Maximum = maximum, Value = initial, MinimumTrackColor = Accent, AutomationId = "Edit" + name };
        var valueLabel = Caption(initial.ToString("0.##"));
        var row = new Grid { ColumnDefinitions = [new(GridLength.Star), new(new GridLength(48))] };
        row.Add(slider);
        row.Add(valueLabel, 1);
        slider.ValueChanged += (_, args) => { apply(args.NewValue); valueLabel.Text = args.NewValue.ToString("0.##"); };
        AddEditor(name, row);
        resets.Add(() => { slider.Value = initial; apply(initial); });
        checks.Add((name, () => Math.Abs(skia() - slider.Value) < 0.001 && (native is null || Math.Abs(native() - slider.Value) < 0.001)));
        apply(initial);
    }

    protected void Toggle(string name, bool initial, Action<bool> apply, Func<bool> skia, Func<bool>? native = null)
    {
        var toggle = new Switch { IsToggled = initial, OnColor = Accent, HorizontalOptions = LayoutOptions.Start, AutomationId = "Edit" + name };
        toggle.Toggled += (_, args) => apply(args.Value);
        AddEditor(name, toggle);
        resets.Add(() => { toggle.IsToggled = initial; apply(initial); });
        checks.Add((name, () => skia() == toggle.IsToggled && (native is null || native() == toggle.IsToggled)));
        apply(initial);
    }

    protected void Text(string name, string initial, Action<string> apply, Func<string> skia, Func<string>? native = null)
    {
        var entry = new Entry { Text = initial, Background = Colors.White, TextColor = Ink, PlaceholderColor = Color.FromArgb("#526164"), AutomationId = "Edit" + name };
        entry.TextChanged += (_, args) => apply(args.NewTextValue ?? string.Empty);
        AddEditor(name, entry);
        resets.Add(() => { entry.Text = initial; apply(initial); });
        checks.Add((name, () => skia() == entry.Text && (native is null || native() == entry.Text)));
        apply(initial);
    }

    /// <summary>A multi-line text editor (e.g. for Label's Text) using an <see cref="Editor"/> instead of a single-line Entry.</summary>
    protected void MultilineText(string name, string initial, Action<string> apply, Func<string> skia, Func<string>? native = null)
    {
        var editor = new Editor
        {
            Text = initial, Background = Colors.White, TextColor = Ink, PlaceholderColor = Color.FromArgb("#526164"),
            AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 90, AutomationId = "Edit" + name
        };
        editor.TextChanged += (_, args) => apply(args.NewTextValue ?? string.Empty);
        AddEditor(name, editor);
        resets.Add(() => { editor.Text = initial; apply(initial); });
        checks.Add((name, () => skia() == editor.Text && (native is null || native() == editor.Text)));
        apply(initial);
    }

    protected void Choice<T>(string name, T[] values, T initial, Action<T> apply, Func<T> skia, Func<T>? native = null) where T : notnull
    {
        var picker = new Picker { Title = name, Background = Colors.White, TextColor = Ink, TitleColor = Color.FromArgb("#526164"), AutomationId = "Edit" + name };
        foreach (var value in values) picker.Items.Add(value.ToString()!);
        picker.SelectedIndex = Array.IndexOf(values, initial);
        picker.SelectedIndexChanged += (_, _) => { if (picker.SelectedIndex >= 0) apply(values[picker.SelectedIndex]); };
        AddEditor(name, picker);
        resets.Add(() => { picker.SelectedIndex = Array.IndexOf(values, initial); apply(initial); });
        checks.Add((name, () => picker.SelectedIndex >= 0 && EqualityComparer<T>.Default.Equals(skia(), values[picker.SelectedIndex])
            && (native is null || EqualityComparer<T>.Default.Equals(native(), values[picker.SelectedIndex]))));
        apply(initial);
    }

    protected void ColorEditor(string name, Color initial, Action<Color> apply, Func<Color> skia, Func<Color>? native = null)
    {
        var selected = initial;
        var palette = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star), new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 8 };
        var colors = new[] { initial, Color.FromArgb("#A12842"), Color.FromArgb("#285C9C"), Color.FromArgb("#202A2C") };
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
        resets.Add(() => { selected = initial; apply(initial); });
        checks.Add((name, () => skia().Equals(selected) && (native is null || native().Equals(selected))));
        apply(initial);
    }

    protected void ActionButton(string title, Action action)
    {
        var button = new Button { Text = title, Background = Accent, TextColor = Colors.White, AutomationId = title.Replace(" ", "") };
        button.Clicked += (_, _) => action();
        AddEditor(title, button);
    }

    protected void OnReset(Action action) => resets.Add(action);
    protected void Feedback(string skia, string? native = null)
    {
        skiaStatus.Text = skia;
        if (nativeStatus is not null) nativeStatus.Text = native ?? string.Empty;
    }

    private void UpdateBounds() => Feedback($"Bounds {SkiaControl.Width:F0} x {SkiaControl.Height:F0}", NativeControl is null ? null : $"Bounds {NativeControl.Width:F0} x {NativeControl.Height:F0}");

    internal string[] CheckProperties()
    {
        var failures = checks.Where(check => !check.Check()).Select(check => check.Name).ToArray();
        result.Text = failures.Length == 0 ? $"PASS: {checks.Count} property checks" : "FAIL: " + string.Join(", ", failures);
        result.TextColor = failures.Length == 0 ? Color.FromArgb("#14633D") : Color.FromArgb("#A12842");
        return failures;
    }

    internal void ResetProperties()
    {
        foreach (var reset in resets) reset();
        result.Text = string.Empty;
        UpdateBounds();
    }

    protected override void OnDisappearing()
    {
        host.AnimationClock.StopAll();
        base.OnDisappearing();
    }
}