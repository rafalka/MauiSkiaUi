using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// Live playground for FR-18 control look and FR-19 color scheme: swap light/dark/brand accents,
/// alternate look packs, and edit per-control default sizes. Changes <see cref="SkUiLook.Current"/> and
/// <see cref="SkUiColorScheme.Current"/> for the process; restores defaults when the page disappears.
/// </summary>
public sealed class LookAndColorSchemePage : ContentPage
{
    private readonly Label _status;
    private readonly Picker _schemePicker;
    private readonly Picker _lookPicker;
    private readonly Picker _accentPicker;
    private readonly Grid _sizeGrid;
    private readonly DemoConfigurableLook _look = new();
    private readonly List<SizeRow> _sizeRows = [];
    private bool? _widePreviews;

    /// <summary>Width at which MAUI and Core previews sit side by side (tablets / landscape phones).</summary>
    private const double WidePreviewBreakpoint = 600;
    private SkUiColorScheme _savedScheme = LightSkUiColorScheme.Instance;
    private SkUiLook _savedLook = DefaultSkUiLook.Instance;
    private bool _applying;
    private bool _restored;
    private bool _syncingSizes;

    private static readonly (string Name, Color Value)[] AccentPresets =
    [
        ("Teal (default)", Color.FromArgb("#087F83")),
        ("Coral", Color.FromArgb("#C54150")),
        ("Blue", Color.FromArgb("#285C9C")),
        ("Purple", Color.FromArgb("#6B3FA0")),
        ("Orange", Color.FromArgb("#D97706"))
    ];

    /// <summary>Builds editors and a per-control size grid with live Skia previews.</summary>
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
                "Each row previews SkUi* and SkUiCore* (stacked on narrow screens, side by side when width ≥ 600). " +
                "Tap the monospace property name under width/height to copy a SkUiLook override snippet. " +
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
        _lookPicker.SelectedIndexChanged += (_, _) =>
        {
            _look.ClearSizeOverrides();
            Apply();
        };

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
            _look.Style = DemoLookStyle.Default;
            _look.ClearSizeOverrides();
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

        _sizeGrid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(new GridLength(168)),
                new ColumnDefinition(new GridLength(108)),
                new ColumnDefinition(new GridLength(108))
            ],
            ColumnSpacing = 10,
            RowSpacing = 14,
            AutomationId = "LookSizeGrid"
        };

        BuildSizeRows();

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
                reset,
                Section("Default sizes (preview | width | height)"),
                _sizeGrid,
                _status
            }
        };

        Content = new ScrollView { Content = editors, AutomationId = "LookScroll" };
        Apply();
    }

    /// <inheritdoc />
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0) return;
        UpdatePreviewOrientation(width >= WidePreviewBreakpoint);
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_restored)
        {
            _savedScheme = SkUiColorScheme.Current;
            _savedLook = SkUiLook.Current;
            _restored = false;
        }
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_restored) return;
        _restored = true;
        SkUiColorScheme.Current = _savedScheme ?? LightSkUiColorScheme.Instance;
        SkUiLook.Current = _savedLook ?? DefaultSkUiLook.Instance;
    }

    private void BuildSizeRows()
    {
        _sizeGrid.Children.Clear();
        _sizeGrid.RowDefinitions.Clear();
        _sizeRows.Clear();

        AddHeaderRow();

        AddSizeRow(new SizeRow(
            MauiTypeName: "SkUiButton",
            CoreTypeName: "SkUiCoreButton",
            PropertyName: "DefaultButtonMinimumHeight",
            HasWidth: false,
            Read: look => (null, look.DefaultButtonMinimumHeight),
            Write: (look, _, h) => look.OverrideButtonMinimumHeight = h,
            FormatCode: (_, h) =>
                $"public override double DefaultButtonMinimumHeight => {FormatDip(h)};",
            BuildMaui: CreateMauiButton,
            BuildCore: CreateCoreButton));

        AddSizeRow(new SizeRow(
            MauiTypeName: "SkUiSwitch",
            CoreTypeName: "SkUiCoreSwitch",
            PropertyName: "DefaultSwitchSize",
            HasWidth: true,
            Read: look =>
            {
                var size = look.DefaultSwitchSize;
                return (size.Width, size.Height);
            },
            Write: (look, w, h) => look.OverrideSwitchSize = new Size(w!.Value, h),
            FormatCode: (w, h) =>
                $"public override Size DefaultSwitchSize => new({FormatDip(w!.Value)}, {FormatDip(h)});",
            BuildMaui: CreateMauiSwitch,
            BuildCore: CreateCoreSwitch));

        AddSizeRow(new SizeRow(
            MauiTypeName: "SkUiCheckBox",
            CoreTypeName: "SkUiCoreCheckBox",
            PropertyName: "DefaultCheckBoxSize",
            HasWidth: true,
            Read: look =>
            {
                var size = look.DefaultCheckBoxSize;
                return (size.Width, size.Height);
            },
            Write: (look, w, h) => look.OverrideCheckBoxSize = new Size(w!.Value, h),
            FormatCode: (w, h) =>
                $"public override Size DefaultCheckBoxSize => new({FormatDip(w!.Value)}, {FormatDip(h)});",
            BuildMaui: CreateMauiCheckBox,
            BuildCore: CreateCoreCheckBox));

        AddSizeRow(new SizeRow(
            MauiTypeName: "SkUiRadioButton",
            CoreTypeName: "SkUiCoreRadioButton",
            PropertyName: "DefaultRadioButtonSize",
            HasWidth: true,
            Read: look =>
            {
                var size = look.DefaultRadioButtonSize;
                return (size.Width, size.Height);
            },
            Write: (look, w, h) => look.OverrideRadioButtonSize = new Size(w!.Value, h),
            FormatCode: (w, h) =>
                $"public override Size DefaultRadioButtonSize => new({FormatDip(w!.Value)}, {FormatDip(h)});",
            BuildMaui: CreateMauiRadio,
            BuildCore: CreateCoreRadio));

        AddSizeRow(new SizeRow(
            MauiTypeName: "SkUiActivityIndicator",
            CoreTypeName: "SkUiCoreActivityIndicator",
            PropertyName: "DefaultActivityIndicatorSize",
            HasWidth: true,
            Read: look =>
            {
                var size = look.DefaultActivityIndicatorSize;
                return (size.Width, size.Height);
            },
            Write: (look, w, h) => look.OverrideActivityIndicatorSize = new Size(w!.Value, h),
            FormatCode: (w, h) =>
                $"public override Size DefaultActivityIndicatorSize => new({FormatDip(w!.Value)}, {FormatDip(h)});",
            BuildMaui: CreateMauiSpinner,
            BuildCore: CreateCoreSpinner));
    }

    private void UpdatePreviewOrientation(bool wide)
    {
        if (_widePreviews == wide) return;
        _widePreviews = wide;
        _sizeGrid.ColumnDefinitions[0] = new ColumnDefinition(wide ? new GridLength(300) : new GridLength(160));
        foreach (var row in _sizeRows)
            LayoutPreviewPair(row, wide);
    }

    private static void LayoutPreviewPair(SizeRow row, bool wide)
    {
        var pair = row.PreviewPair;
        pair.Children.Clear();
        pair.RowDefinitions.Clear();
        pair.ColumnDefinitions.Clear();
        pair.RowSpacing = wide ? 0 : 10;
        pair.ColumnSpacing = wide ? 12 : 0;

        if (wide)
        {
            pair.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            pair.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            pair.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            pair.Add(row.MauiBlock, 0, 0);
            pair.Add(row.CoreBlock, 1, 0);
        }
        else
        {
            pair.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            pair.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            pair.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            pair.Add(row.MauiBlock, 0, 0);
            pair.Add(row.CoreBlock, 0, 1);
        }
    }

    private void AddHeaderRow()
    {
        _sizeGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        const int row = 0;
        _sizeGrid.Add(Header("Preview"), 0, row);
        _sizeGrid.Add(Header("Width"), 1, row);
        _sizeGrid.Add(Header("Height"), 2, row);
    }

    private void AddSizeRow(SizeRow row)
    {
        var rowIndex = _sizeGrid.RowDefinitions.Count;
        _sizeGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        row.MauiPreviewHost = new ContentView { MinimumHeightRequest = 48 };
        row.CorePreviewHost = new ContentView { MinimumHeightRequest = 48 };
        row.MauiBlock = CreateNamedPreviewBlock(row.MauiTypeName, row.MauiPreviewHost);
        row.CoreBlock = CreateNamedPreviewBlock(row.CoreTypeName, row.CorePreviewHost);
        row.PreviewPair = new Grid { HorizontalOptions = LayoutOptions.Fill };
        LayoutPreviewPair(row, _widePreviews ?? false);
        _sizeGrid.Add(row.PreviewPair, 0, rowIndex);

        if (row.HasWidth)
        {
            row.WidthEntry = CreateDipEntry($"Look{row.MauiTypeName}Width");
            row.WidthEntry.Completed += (_, _) => CommitSizeRow(row);
            row.WidthEntry.Unfocused += (_, _) => CommitSizeRow(row);
        }

        row.HeightEntry = CreateDipEntry($"Look{row.MauiTypeName}Height");
        row.HeightEntry.Completed += (_, _) => CommitSizeRow(row);
        row.HeightEntry.Unfocused += (_, _) => CommitSizeRow(row);

        var editors = CreateSizeEditors(row);
        _sizeGrid.Add(editors, 1, rowIndex);
        Grid.SetColumnSpan(editors, 2);

        _sizeRows.Add(row);
    }

    private View CreateSizeEditors(SizeRow row)
    {
        var editors = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            ],
            RowDefinitions =
            [
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            ],
            ColumnSpacing = 10,
            RowSpacing = 4,
            VerticalOptions = LayoutOptions.Center
        };

        if (row.WidthEntry is not null)
            editors.Add(row.WidthEntry, 0, 0);
        else
        {
            editors.Add(new Label
            {
                Text = "—",
                TextColor = DemoColors.Caption,
                FontFamily = DemoFonts.OpenSansRegular,
                VerticalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center
            }, 0, 0);
        }

        editors.Add(row.HeightEntry, 1, 0);

        var fieldLabel = new Label
        {
            Text = row.PropertyName,
            FontFamily = DemoFonts.RobotoMono,
            FontSize = 10,
            TextColor = DemoColors.Accent,
            LineBreakMode = LineBreakMode.TailTruncation,
            HorizontalTextAlignment = TextAlignment.Start,
            Margin = new Thickness(0, 2, 0, 0)
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await CopySetterSnippetAsync(row);
        fieldLabel.GestureRecognizers.Add(tap);
        editors.Add(fieldLabel, 0, 1);
        Grid.SetColumnSpan(fieldLabel, 2);

        return editors;
    }

    private static VerticalStackLayout CreateNamedPreviewBlock(string typeName, ContentView host) => new()
    {
        Spacing = 2,
        HorizontalOptions = LayoutOptions.Fill,
        Children =
        {
            host,
            new Label
            {
                Text = typeName,
                FontFamily = DemoFonts.RobotoMono,
                FontSize = 10,
                TextColor = DemoColors.Ink,
                HorizontalTextAlignment = TextAlignment.Center
            }
        }
    };

    private async Task CopySetterSnippetAsync(SizeRow row)
    {
        CommitSizeRow(row, printSnippet: false);
        var (width, height) = row.Read(_look);
        if (row.HasWidth && width is null)
            return;
        var snippet = row.FormatCode(width, height);
        Console.WriteLine(snippet);
        await Clipboard.Default.SetTextAsync(snippet);
        _status.Text = $"Copied {row.PropertyName} setter to clipboard.";
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

        _look.Style = _lookPicker.SelectedIndex switch
        {
            1 => DemoLookStyle.Chunky,
            2 => DemoLookStyle.Minimal,
            _ => DemoLookStyle.Default
        };
        SkUiLook.Current = _look;

        SyncSizeEntriesFromLook();
        RebuildAllPreviews();
        _status.Text =
            $"Scheme: {SkUiColorScheme.Current.GetType().Name} · Accent {ToHex(SkUiColorScheme.Current.Accent)} · " +
            $"Look: {_look.Style}";
    }

    private void SyncSizeEntriesFromLook()
    {
        _syncingSizes = true;
        try
        {
            foreach (var row in _sizeRows)
            {
                var (width, height) = row.Read(_look);
                if (row.WidthEntry is not null && width is not null)
                    row.WidthEntry.Text = FormatDip(width.Value);
                row.HeightEntry.Text = FormatDip(height);
            }
        }
        finally
        {
            _syncingSizes = false;
        }
    }

    private void CommitSizeRow(SizeRow row, bool printSnippet = false)
    {
        if (_syncingSizes || _applying) return;

        var (currentWidth, _) = row.Read(_look);
        double? width = currentWidth;
        if (row.HasWidth)
        {
            if (!TryParseDip(row.WidthEntry!.Text, out var parsedWidth))
            {
                SyncSizeEntriesFromLook();
                return;
            }
            width = parsedWidth;
        }

        if (!TryParseDip(row.HeightEntry.Text, out var height))
        {
            SyncSizeEntriesFromLook();
            return;
        }

        row.Write(_look, width, height);
        if (printSnippet)
            Console.WriteLine(row.FormatCode(width, height));
        RebuildPreview(row);
    }

    private void RebuildAllPreviews()
    {
        foreach (var row in _sizeRows)
            RebuildPreview(row);
    }

    private void RebuildPreview(SizeRow row)
    {
        var scheme = SkUiColorScheme.Current;
        row.MauiPreviewHost.Content = CreateSkiaHost(scheme, row.BuildMaui(scheme, _look));
        row.CorePreviewHost.Content = CreateCoreHost(scheme, row.BuildCore(scheme, _look));
    }

    private static SkUiContentView CreateSkiaHost(SkUiColorScheme scheme, SkUiView content)
    {
        var host = new SkUiContentView
        {
            HwAccelerated = false,
            BackgroundColor = scheme.DefaultBackground,
            HeightRequest = 56,
            HorizontalOptions = LayoutOptions.Fill
        };
        host.SetContent(content);
        return host;
    }

    private static SkUiCoreHost CreateCoreHost(SkUiColorScheme scheme, SkUiCoreNode content)
    {
        var host = new SkUiCoreHost
        {
            HwAccelerated = false,
            BackgroundColor = scheme.DefaultBackground,
            HeightRequest = 56,
            HorizontalOptions = LayoutOptions.Fill
        };
        host.SetContent(content);
        return host;
    }

    private static SkUiView CreateMauiButton(SkUiColorScheme scheme, SkUiLook look)
    {
        var button = new SkUiButton();
        button.SetText("Go");
        button.SetFillColor(scheme.Accent);
        button.SetCornerRadius(look.DefaultButtonCornerRadius);
        button.MinimumHeightRequest = look.DefaultButtonMinimumHeight;
        button.HorizontalOptions = LayoutOptions.Center;
        button.VerticalOptions = LayoutOptions.Center;
        return button;
    }

    private static SkUiCoreNode CreateCoreButton(SkUiColorScheme scheme, SkUiLook look)
    {
        var button = new SkUiCoreButton();
        button.SetText("Go");
        button.SetFillColor(scheme.Accent);
        button.SetCornerRadius(look.DefaultButtonCornerRadius);
        button.SetMinimumHeight(look.DefaultButtonMinimumHeight);
        button.SetHorizontalAlignment(LayoutAlignment.Center);
        button.SetVerticalAlignment(LayoutAlignment.Center);
        return button;
    }

    private static SkUiView CreateMauiSwitch(SkUiColorScheme scheme, SkUiLook look)
    {
        var size = look.DefaultSwitchSize;
        var sw = new SkUiSwitch();
        sw.SetIsChecked(true);
        sw.SetOnColor(scheme.Accent);
        sw.WidthRequest = size.Width;
        sw.HeightRequest = size.Height;
        sw.HorizontalOptions = LayoutOptions.Center;
        sw.VerticalOptions = LayoutOptions.Center;
        return sw;
    }

    private static SkUiCoreNode CreateCoreSwitch(SkUiColorScheme scheme, SkUiLook look)
    {
        var size = look.DefaultSwitchSize;
        var sw = new SkUiCoreSwitch();
        sw.SetIsChecked(true);
        sw.SetOnColor(scheme.Accent);
        sw.SetWidth(size.Width);
        sw.SetHeight(size.Height);
        sw.SetHorizontalAlignment(LayoutAlignment.Center);
        sw.SetVerticalAlignment(LayoutAlignment.Center);
        return sw;
    }

    private static SkUiView CreateMauiCheckBox(SkUiColorScheme scheme, SkUiLook look)
    {
        var size = look.DefaultCheckBoxSize;
        var check = new SkUiCheckBox();
        check.SetIsChecked(true);
        check.SetColor(scheme.Accent);
        check.WidthRequest = size.Width;
        check.HeightRequest = size.Height;
        check.HorizontalOptions = LayoutOptions.Center;
        check.VerticalOptions = LayoutOptions.Center;
        return check;
    }

    private static SkUiCoreNode CreateCoreCheckBox(SkUiColorScheme scheme, SkUiLook look)
    {
        var size = look.DefaultCheckBoxSize;
        var check = new SkUiCoreCheckBox();
        check.SetIsChecked(true);
        check.SetColor(scheme.Accent);
        check.SetWidth(size.Width);
        check.SetHeight(size.Height);
        check.SetHorizontalAlignment(LayoutAlignment.Center);
        check.SetVerticalAlignment(LayoutAlignment.Center);
        return check;
    }

    private static SkUiView CreateMauiRadio(SkUiColorScheme scheme, SkUiLook look)
    {
        var size = look.DefaultRadioButtonSize;
        var radio = new SkUiRadioButton();
        radio.SetIsChecked(true);
        radio.SetColor(scheme.Accent);
        radio.WidthRequest = size.Width;
        radio.HeightRequest = size.Height;
        radio.HorizontalOptions = LayoutOptions.Center;
        radio.VerticalOptions = LayoutOptions.Center;
        return radio;
    }

    private static SkUiCoreNode CreateCoreRadio(SkUiColorScheme scheme, SkUiLook look)
    {
        var size = look.DefaultRadioButtonSize;
        var radio = new SkUiCoreRadioButton();
        radio.SetIsChecked(true);
        radio.SetColor(scheme.Accent);
        radio.SetWidth(size.Width);
        radio.SetHeight(size.Height);
        radio.SetHorizontalAlignment(LayoutAlignment.Center);
        radio.SetVerticalAlignment(LayoutAlignment.Center);
        return radio;
    }

    private static SkUiView CreateMauiSpinner(SkUiColorScheme scheme, SkUiLook look)
    {
        var size = look.DefaultActivityIndicatorSize;
        var spinner = new SkUiActivityIndicator();
        spinner.SetIsRunning(true);
        spinner.SetColor(scheme.Muted);
        spinner.WidthRequest = size.Width;
        spinner.HeightRequest = size.Height;
        spinner.HorizontalOptions = LayoutOptions.Center;
        spinner.VerticalOptions = LayoutOptions.Center;
        return spinner;
    }

    private static SkUiCoreNode CreateCoreSpinner(SkUiColorScheme scheme, SkUiLook look)
    {
        var size = look.DefaultActivityIndicatorSize;
        var spinner = new SkUiCoreActivityIndicator();
        spinner.SetIsRunning(true);
        spinner.SetColor(scheme.Muted);
        spinner.SetWidth(size.Width);
        spinner.SetHeight(size.Height);
        spinner.SetHorizontalAlignment(LayoutAlignment.Center);
        spinner.SetVerticalAlignment(LayoutAlignment.Center);
        return spinner;
    }

    private static Entry CreateDipEntry(string automationId) => new()
    {
        Keyboard = Keyboard.Numeric,
        FontFamily = DemoFonts.OpenSansRegular,
        FontSize = 13,
        TextColor = DemoColors.Ink,
        BackgroundColor = Colors.White,
        AutomationId = automationId,
        VerticalOptions = LayoutOptions.Center,
        Placeholder = "DIPs"
    };

    private static Label Header(string text) => new()
    {
        Text = text,
        FontFamily = DemoFonts.OpenSansSemibold,
        FontSize = 12,
        TextColor = DemoColors.Caption
    };

    private static Label Section(string title) => new()
    {
        Text = title,
        TextColor = DemoColors.Ink,
        FontFamily = DemoFonts.OpenSansSemibold,
        FontSize = 14,
        Margin = new Thickness(0, 8, 0, 0)
    };

    private static SkUiColorScheme CloneLight(Color accent) => new LightSkUiColorScheme { Accent = accent };

    private static SkUiColorScheme CloneDark(Color accent) => new DarkSkUiColorScheme { Accent = accent };

    private static string ToHex(Color color) =>
        $"#{(int)(color.Red * 255):X2}{(int)(color.Green * 255):X2}{(int)(color.Blue * 255):X2}";

    private static string FormatDip(double value) =>
        Math.Abs(value - Math.Round(value)) < 0.01 ? ((int)Math.Round(value)).ToString() : value.ToString("0.##");

    private static bool TryParseDip(string? text, out double value)
    {
        if (double.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value)
            || double.TryParse(text, out value))
        {
            if (value > 0 && value < 1000)
                return true;
        }
        value = 0;
        return false;
    }

    private sealed class SizeRow
    {
        public SizeRow(
            string MauiTypeName,
            string CoreTypeName,
            string PropertyName,
            bool HasWidth,
            Func<DemoConfigurableLook, (double? Width, double Height)> Read,
            Action<DemoConfigurableLook, double?, double> Write,
            Func<double?, double, string> FormatCode,
            Func<SkUiColorScheme, SkUiLook, SkUiView> BuildMaui,
            Func<SkUiColorScheme, SkUiLook, SkUiCoreNode> BuildCore)
        {
            this.MauiTypeName = MauiTypeName;
            this.CoreTypeName = CoreTypeName;
            this.PropertyName = PropertyName;
            this.HasWidth = HasWidth;
            this.Read = Read;
            this.Write = Write;
            this.FormatCode = FormatCode;
            this.BuildMaui = BuildMaui;
            this.BuildCore = BuildCore;
        }

        public string MauiTypeName { get; }
        public string CoreTypeName { get; }
        public string PropertyName { get; }
        public bool HasWidth { get; }
        public Func<DemoConfigurableLook, (double? Width, double Height)> Read { get; }
        public Action<DemoConfigurableLook, double?, double> Write { get; }
        public Func<double?, double, string> FormatCode { get; }
        public Func<SkUiColorScheme, SkUiLook, SkUiView> BuildMaui { get; }
        public Func<SkUiColorScheme, SkUiLook, SkUiCoreNode> BuildCore { get; }
        public Grid PreviewPair { get; set; } = null!;
        public View MauiBlock { get; set; } = null!;
        public View CoreBlock { get; set; } = null!;
        public ContentView MauiPreviewHost { get; set; } = null!;
        public ContentView CorePreviewHost { get; set; } = null!;
        public Entry? WidthEntry { get; set; }
        public Entry HeightEntry { get; set; } = null!;
    }
}
