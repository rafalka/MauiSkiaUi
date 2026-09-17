using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// Live playground for FR-18 control look and FR-19 color scheme: swap light/dark/brand accents,
/// alternate look packs, and scale default control sizes. Changes <see cref="SkUiLook.Current"/> and
/// <see cref="SkUiColorScheme.Current"/> for the process; restores defaults when the page disappears.
/// </summary>
public sealed class LookAndColorSchemePage : ContentPage
{
    private readonly ContentView _previewHost;
    private readonly Label _status;
    private readonly Slider _sizeSlider;
    private readonly Picker _schemePicker;
    private readonly Picker _lookPicker;
    private readonly Picker _accentPicker;

    private SkUiColorScheme _savedScheme = LightSkUiColorScheme.Instance;
    private SkUiLook _savedLook = DefaultSkUiLook.Instance;
    private bool _applying;
    private bool _restored;

    private static readonly (string Name, Color Value)[] AccentPresets =
    [
        ("Teal (default)", Color.FromArgb("#087F83")),
        ("Coral", Color.FromArgb("#C54150")),
        ("Blue", Color.FromArgb("#285C9C")),
        ("Purple", Color.FromArgb("#6B3FA0")),
        ("Orange", Color.FromArgb("#D97706"))
    ];

    /// <summary>Builds editors and a Skia preview strip of stock controls.</summary>
    public LookAndColorSchemePage()
    {
        Title = "Look & colors";
        Background = DemoColors.PageBackground;
        AutomationId = "LookAndColorSchemePage";

        _savedScheme = SkUiColorScheme.Current;
        _savedLook = SkUiLook.Current;

        var description = new Label
        {
            Text =
                "Control look (shapes + default sizes) and color scheme (shared palette) are separate from MAUI Style. " +
                "Changes apply app-wide via SkUiLook.Current / SkUiColorScheme.Current. " +
                "Leaving this page restores the previous look and scheme.",
            TextColor = DemoColors.Caption,
            FontFamily = DemoFonts.OpenSansRegular,
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap
        };

        _schemePicker = new Picker
        {
            Title = "Color scheme",
            ItemsSource = new[] { "Light", "Dark", "Light + custom accent" },
            SelectedIndex = 0,
            AutomationId = "LookSchemePicker",
            TextColor = DemoColors.Ink,
            FontFamily = DemoFonts.OpenSansRegular
        };
        _schemePicker.SelectedIndexChanged += (_, _) => Apply();

        _accentPicker = new Picker
        {
            Title = "Accent",
            ItemsSource = AccentPresets.Select(p => p.Name).ToList(),
            SelectedIndex = 0,
            AutomationId = "LookAccentPicker",
            TextColor = DemoColors.Ink,
            FontFamily = DemoFonts.OpenSansRegular
        };
        _accentPicker.SelectedIndexChanged += (_, _) => Apply();

        _lookPicker = new Picker
        {
            Title = "Control look",
            ItemsSource = new[] { "Default", "Chunky (alternate)", "Minimal (alternate)" },
            SelectedIndex = 0,
            AutomationId = "LookPackPicker",
            TextColor = DemoColors.Ink,
            FontFamily = DemoFonts.OpenSansRegular
        };
        _lookPicker.SelectedIndexChanged += (_, _) => Apply();

        _sizeSlider = new Slider
        {
            Minimum = 0.75,
            Maximum = 1.5,
            Value = 1,
            AutomationId = "LookSizeScale",
            MinimumTrackColor = DemoColors.Accent,
            ThumbColor = DemoColors.Accent
        };
        _sizeSlider.ValueChanged += (_, _) => Apply();

        var sizeLabel = new Label
        {
            Text = "Size scale: 1.00",
            AutomationId = "LookSizeLabel",
            TextColor = DemoColors.Ink,
            FontFamily = DemoFonts.OpenSansSemibold,
            FontSize = 13
        };
        _sizeSlider.ValueChanged += (_, args) => sizeLabel.Text = $"Size scale: {args.NewValue:0.00}";

        var reset = new Button
        {
            Text = "Reset to light + default look",
            AutomationId = "LookReset",
            BackgroundColor = DemoColors.Accent,
            TextColor = Colors.White,
            FontFamily = DemoFonts.OpenSansSemibold,
            Padding = new Thickness(14, 8),
            HorizontalOptions = LayoutOptions.Start
        };
        reset.Clicked += (_, _) =>
        {
            _applying = true;
            _schemePicker.SelectedIndex = 0;
            _accentPicker.SelectedIndex = 0;
            _lookPicker.SelectedIndex = 0;
            _sizeSlider.Value = 1;
            _applying = false;
            Apply();
        };

        _status = new Label
        {
            TextColor = DemoColors.Caption,
            FontFamily = DemoFonts.OpenSansRegular,
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap,
            AutomationId = "LookStatus"
        };

        _previewHost = new ContentView
        {
            AutomationId = "LookPreviewHost",
            HeightRequest = 320,
            BackgroundColor = Colors.Transparent
        };

        var editors = new VerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(16, 12, 16, 24),
            Children =
            {
                description,
                Section("Color scheme"),
                _schemePicker,
                Section("Accent (used when scheme is Light + custom accent, or to retint Dark)"),
                _accentPicker,
                Section("Control look pack"),
                _lookPicker,
                Section("Default control size scale"),
                sizeLabel,
                _sizeSlider,
                reset,
                Section("Preview (SkUi* using Current look + scheme)"),
                _previewHost,
                _status
            }
        };

        Content = new ScrollView { Content = editors, AutomationId = "LookScroll" };
        Apply();
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_restored) return;
        _restored = true;
        // Shell may call Disappearing more than once; never push null into Current.
        SkUiColorScheme.Current = _savedScheme ?? LightSkUiColorScheme.Instance;
        SkUiLook.Current = _savedLook ?? DefaultSkUiLook.Instance;
    }

    private void Apply()
    {
        if (_applying) return;

        var accent = AccentPresets[Math.Clamp(_accentPicker.SelectedIndex, 0, AccentPresets.Length - 1)].Value;
        SkUiColorScheme.Current = _schemePicker.SelectedIndex switch
        {
            1 => CloneDark(accent),
            2 => CloneLight(accent),
            _ => new LightSkUiColorScheme()
        };

        var style = _lookPicker.SelectedIndex switch
        {
            1 => DemoLookStyle.Chunky,
            2 => DemoLookStyle.Minimal,
            _ => DemoLookStyle.Default
        };
        SkUiLook.Current = new DemoConfigurableLook
        {
            Style = style,
            SizeScale = _sizeSlider.Value
        };

        RebuildPreview();
        _status.Text =
            $"Scheme: {SkUiColorScheme.Current.GetType().Name} · Accent {ToHex(SkUiColorScheme.Current.Accent)} · " +
            $"Look: {style} × {_sizeSlider.Value:0.00} · Switch {SkUiLook.Current.MeasureSwitch(0, 0).Width:0}×{SkUiLook.Current.MeasureSwitch(0, 0).Height:0}";
    }

    private void RebuildPreview()
    {
        var scheme = SkUiColorScheme.Current;
        var look = SkUiLook.Current;

        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(12) };
        stack.SetSpacing(12);

        stack.Children.Add(new SkUiLabel()
            .SetText("Sample controls")
            .SetFontSize(16)
            .SetTextColor(scheme.DefaultForeground));

        var row = new SkUiHorizontalStackLayout();
        row.SetSpacing(16);

        var button = new SkUiButton();
        button.SetText("Button");
        button.SetFillColor(scheme.Accent);
        button.SetCornerRadius(look.DefaultButtonCornerRadius);
        button.Clicked += (_, _) => { };
        button.MinimumHeightRequest = look.DefaultButtonMinimumHeight;
        row.Children.Add(button);

        var sw = new SkUiSwitch();
        sw.SetIsChecked(true);
        sw.SetOnColor(scheme.Accent);
        row.Children.Add(sw);

        var check = new SkUiCheckBox();
        check.SetIsChecked(true);
        check.SetColor(scheme.Accent);
        row.Children.Add(check);

        var radio = new SkUiRadioButton();
        radio.SetIsChecked(true);
        radio.SetColor(scheme.Accent);
        row.Children.Add(radio);
        stack.Children.Add(row);

        var spinnerSize = look.MeasureActivityIndicator(0, 0);
        var spinner = new SkUiActivityIndicator();
        spinner.SetIsRunning(true);
        spinner.SetColor(scheme.Muted);
        spinner.WidthRequest = spinnerSize.Width;
        spinner.HeightRequest = spinnerSize.Height;
        stack.Children.Add(spinner);

        stack.Children.Add(new SkUiLabel()
            .SetText("Muted / track-off / disabled follow the scheme. Look paints Switch / CheckBox / Radio / spinner.")
            .SetFontSize(12)
            .SetTextColor(scheme.Muted));

        var coreRow = new SkUiCoreHorizontalStackLayout();
        coreRow.SetSpacing(16);
        var coreButton = new SkUiCoreButton();
        coreButton.SetText("Core");
        coreButton.SetFillColor(scheme.Accent);
        coreButton.SetCornerRadius(look.DefaultButtonCornerRadius);
        coreButton.SetMinimumHeight(look.DefaultButtonMinimumHeight);
        coreRow.Add(coreButton);
        var coreSwitch = new SkUiCoreSwitch();
        coreSwitch.SetIsChecked(true);
        coreSwitch.SetOnColor(scheme.Accent);
        coreRow.Add(coreSwitch);
        var coreCheck = new SkUiCoreCheckBox();
        coreCheck.SetIsChecked(true);
        coreCheck.SetColor(scheme.Accent);
        coreRow.Add(coreCheck);
        var coreHost = new SkUiCoreHost();
        coreHost.SetContent(coreRow);
        stack.Children.Add(new SkUiLabel().SetText("Core layer").SetFontSize(13).SetTextColor(scheme.DefaultForeground));
        stack.Children.Add(coreHost);

        var root = new SkUiContentView
        {
            HwAccelerated = true,
            BackgroundColor = scheme.DefaultBackground
        };
        root.SetContent(stack);
        _previewHost.Content = root;
    }

    private static SkUiColorScheme CloneLight(Color accent)
    {
        var scheme = new LightSkUiColorScheme { Accent = accent };
        return scheme;
    }

    private static SkUiColorScheme CloneDark(Color accent)
    {
        var scheme = new DarkSkUiColorScheme { Accent = accent };
        return scheme;
    }

    private static Label Section(string title) => new()
    {
        Text = title,
        TextColor = DemoColors.Ink,
        FontFamily = DemoFonts.OpenSansSemibold,
        FontSize = 14,
        Margin = new Thickness(0, 8, 0, 0)
    };

    private static string ToHex(Color color) =>
        $"#{(int)(color.Red * 255):X2}{(int)(color.Green * 255):X2}{(int)(color.Blue * 255):X2}";
}
