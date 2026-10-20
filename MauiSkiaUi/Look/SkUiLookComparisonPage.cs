using System.Globalization;
using System.Text;

namespace MauiSkiaUi;

/// <summary>
/// A page that shows MAUI's native controls next to SkiaUi's drawn ones under a chosen look, row by row, with each side's
/// measured size, so an app can check on each device that a look (a stock platform look or its own) matches the native
/// controls it replaces. Push it from a debug menu: <c>await Navigation.PushAsync(new SkUiLookComparisonPage());</c>.
/// </summary>
/// <remarks>
/// <para>
/// The native column shows the platform the app runs on, so compare a platform's look on that platform. "App styles" keeps
/// the app's implicit styles on both columns (what the app's screens look like); off, both columns get empty implicit
/// styles and show the platform's and the look's defaults. "Overlay" draws the SkiaUi control at half opacity over the
/// native one. "Look's colors" makes the look's color scheme (<see cref="DefaultSkUiLook.GetColorScheme"/>) current while
/// the page is shown.
/// </para>
/// <para>
/// After each layout the page writes a report of every row's sizes to the console (lines starting with
/// <see cref="LogTag"/>), keeps it in <see cref="Report"/> and copies it to the clipboard on "Copy report", for scripts and
/// coding agents that compare looks.
/// </para>
/// </remarks>
public class SkUiLookComparisonPage : ContentPage
{
    /// <summary>The prefix of the report lines the page writes to the console.</summary>
    public const string LogTag = "[SkUiLookComparison]";

    private const string AppStylesTitle = "App styles";
    private const string OverlayTitle = "Overlay";
    private const string LookColorsTitle = "Look's colors";
    private const string NativeColumnTitle = "MAUI";
    private const string SkiaColumnTitle = "SkiaUi";
    private const string SampleText = "Sample";
    private const string CopyReportTitle = "Copy report";

    /// <summary>Sizes closer than this count as equal, DIPs.</summary>
    private const double SizeTolerance = 0.5;

    private static readonly Color MismatchColor = Color.FromArgb("#D32F2F");
    private static readonly Color MatchColor = Color.FromArgb("#388E3C");
    private static readonly string[] PickerItems = [SampleText];
    private static readonly Color AppFill = Color.FromArgb("#EEF1F3");

    /// <summary>The stock looks, in the order the look picker lists them.</summary>
    public static IReadOnlyList<(string Name, SkUiLook Look)> StockLooks { get; } =
    [
        ("Default", DefaultSkUiLook.Instance),
        ("Android (Material 2)", AndroidSkUiLook.Instance),
        ("Android (Material 3)", AndroidMaterial3SkUiLook.Instance),
        ("iOS", IosSkUiLook.Instance),
        ("Mac Catalyst", MacSkUiLook.Instance),
        ("Windows", WindowsSkUiLook.Instance)
    ];

    private readonly List<(string Name, SkUiLook Look)> _looks = [.. StockLooks];
    private readonly List<ComparisonRow> _rows = StockRows();
    private readonly Picker _lookPicker = new() { HorizontalOptions = LayoutOptions.Fill };
    private readonly VerticalStackLayout _rowsHost = new() { Spacing = 12 };
    private readonly Switch _appStylesSwitch;
    private readonly Switch _overlaySwitch;
    private readonly Switch _lookColorsSwitch;
    private readonly List<RowView> _rowViews = [];
    private SkUiLook _look;
    private bool _appStyles = true;
    private bool _overlay;
    private bool _lookColors;
    private SkUiColorScheme? _appScheme;
    private bool _reportPending;

    /// <summary>Creates the page showing the app's look (<see cref="SkUiLook.Current"/>).</summary>
    public SkUiLookComparisonPage() : this(null)
    {
    }

    /// <summary>Creates the page showing <paramref name="look"/> (<c>null</c>: the app's, <see cref="SkUiLook.Current"/>).</summary>
    public SkUiLookComparisonPage(SkUiLook? look)
    {
        Title = "Look comparison";
        _look = look ?? SkUiLook.Current;
        if (_looks.All(entry => entry.Look != _look))
            _looks.Insert(0, (_look.GetType().Name, _look));
        _lookPicker.SelectedIndexChanged += (_, _) =>
        {
            if (_lookPicker.SelectedIndex < 0) return;
            _look = _looks[_lookPicker.SelectedIndex].Look;
            ApplyLookColors();
            Rebuild();
        };
        // Wraps on narrow screens.
        var options = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            Children =
            {
                Option(AppStylesTitle, _appStyles, value => { _appStyles = value; Rebuild(); }, out _appStylesSwitch),
                Option(OverlayTitle, _overlay, value => { _overlay = value; Rebuild(); }, out _overlaySwitch),
                Option(LookColorsTitle, _lookColors, value => { _lookColors = value; ApplyLookColors(); Rebuild(); }, out _lookColorsSwitch)
            }
        };
        var copy = new Button { Text = CopyReportTitle, HorizontalOptions = LayoutOptions.Start };
        copy.Clicked += async (_, _) => await Clipboard.Default.SetTextAsync(Report);
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 12,
                Children = { _lookPicker, options, copy, _rowsHost }
            }
        };
        RefreshLookPicker();
    }

    /// <summary>Whether both columns keep the app's implicit styles (default <c>true</c>); <c>false</c> shows the platform's and the look's defaults.</summary>
    public bool ShowAppStyles { get => _appStyles; set => _appStylesSwitch.IsToggled = value; }

    /// <summary>Whether the SkiaUi control is drawn at half opacity over the native one instead of beside it.</summary>
    public bool Overlay { get => _overlay; set => _overlaySwitch.IsToggled = value; }

    /// <summary>Whether the look's color scheme is <see cref="SkUiColorScheme.Current"/> while the page is shown.</summary>
    public bool UseLookColors { get => _lookColors; set => _lookColorsSwitch.IsToggled = value; }

    /// <summary>Shows the look named <paramref name="name"/> in the picker (<see cref="StockLooks"/> or <see cref="AddLook"/>'s), ignoring case; <c>false</c> when there is none.</summary>
    public bool SelectLook(string name)
    {
        var index = _looks.FindIndex(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;
        _lookPicker.SelectedIndex = index;
        return true;
    }

    /// <summary>The last measured report: one line per row with the native and the SkiaUi size.</summary>
    public string Report { get; private set; } = string.Empty;

    /// <summary>Adds a look to the picker (the app's own) and shows it; a look already listed takes <paramref name="name"/>.</summary>
    public SkUiLookComparisonPage AddLook(string name, SkUiLook look)
    {
        ArgumentNullException.ThrowIfNull(look);
        var index = _looks.FindIndex(entry => entry.Look == look);
        if (index >= 0)
            _looks[index] = (name, look);
        else
            _looks.Add((name, look));
        _look = look;
        RefreshLookPicker();
        return this;
    }

    /// <summary>
    /// Adds a row comparing the app's own pair of controls (a styled native control and its SkiaUi replacement). The
    /// factories run again whenever an option changes.
    /// </summary>
    public SkUiLookComparisonPage AddRow(string name, Func<View> createNative, Func<SkUiView> createSkia)
    {
        ArgumentNullException.ThrowIfNull(createNative);
        ArgumentNullException.ThrowIfNull(createSkia);
        _rows.Add(new ComparisonRow(name, createNative, createSkia, Stretch: false));
        Rebuild();
        return this;
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLookColors();
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        RestoreAppColors();
        base.OnDisappearing();
    }

    private static List<ComparisonRow> StockRows() =>
    [
        new(nameof(Label), () => new Label { Text = SampleText }, () => new SkUiLabel { Text = SampleText }),
        new(nameof(Button), () => new Button { Text = nameof(Button) }, () => new SkUiButton { Text = nameof(Button) }),
        new("Switch (on)", () => new Switch { IsToggled = true }, () => new SkUiSwitch { IsToggled = true }),
        new("Switch (off)", () => new Switch(), () => new SkUiSwitch()),
        new("CheckBox (checked)", () => new CheckBox { IsChecked = true }, () => new SkUiCheckBox { IsChecked = true }),
        new("CheckBox (unchecked)", () => new CheckBox(), () => new SkUiCheckBox()),
        new("RadioButton (checked)", () => new RadioButton { Content = nameof(RadioButton), IsChecked = true },
            () => new SkUiRadioButton { Content = nameof(RadioButton), IsChecked = true }),
        new("RadioButton (unchecked)", () => new RadioButton { Content = nameof(RadioButton) }, () => new SkUiRadioButton { Content = nameof(RadioButton) }),
        new(nameof(Slider), () => new Slider { Maximum = 1, Value = 0.4 }, () => new SkUiSlider { Maximum = 1, Value = 0.4 }, Stretch: true),
        new(nameof(ProgressBar), () => new ProgressBar { Progress = 0.6 }, () => new SkUiProgressBar { Progress = 0.6 }, Stretch: true),
        new(nameof(ActivityIndicator), () => new ActivityIndicator { IsRunning = true }, () => new SkUiActivityIndicator { IsRunning = true }),
        new(nameof(Entry), () => new Entry { Text = SampleText }, () => new SkUiEntry { Text = SampleText }, Stretch: true),
        // A field's own fill (MAUI's background) on the look's chrome: a filled field without its idle outline.
        new("Entry (app fill)", () => new Entry { Text = SampleText, BackgroundColor = AppFill },
            () => new SkUiEntry { Text = SampleText, FillColor = AppFill, BorderWidth = 0 }, Stretch: true),
        new(nameof(Picker), () => new Picker { ItemsSource = PickerItems, SelectedIndex = 0 }, () => new SkUiPicker { ItemsSource = PickerItems, SelectedIndex = 0 }),
        new(nameof(Stepper), () => new Stepper { Value = 1 }, () => new SkUiStepper { Value = 1 })
    ];

    private static HorizontalStackLayout Option(string title, bool value, Action<bool> changed, out Switch toggle)
    {
        toggle = new Switch { IsToggled = value, VerticalOptions = LayoutOptions.Center };
        toggle.Toggled += (_, args) => changed(args.Value);
        return new HorizontalStackLayout
        {
            Spacing = 4,
            Margin = new Thickness(0, 0, 16, 8),
            Children = { toggle, new Label { Text = title, VerticalOptions = LayoutOptions.Center } }
        };
    }

    private void RefreshLookPicker()
    {
        _lookPicker.ItemsSource = _looks.Select(entry => entry.Name).ToList();
        _lookPicker.SelectedIndex = _looks.FindIndex(entry => entry.Look == _look);
        Rebuild();
    }

    private void ApplyLookColors()
    {
        if (_lookColors && _look is DefaultSkUiLook look)
        {
            _appScheme ??= SkUiColorScheme.Current;
            SkUiColorScheme.Current = look.GetColorScheme(Application.Current?.RequestedTheme == AppTheme.Dark);
        }
        else
        {
            RestoreAppColors();
        }
    }

    private void RestoreAppColors()
    {
        if (_appScheme is null) return;
        SkUiColorScheme.Current = _appScheme;
        _appScheme = null;
    }

    private void Rebuild()
    {
        _rowsHost.Clear();
        _rowViews.Clear();
        _rowsHost.Add(ColumnHeaders());
        foreach (var row in _rows)
        {
            var native = row.CreateNative();
            SkUiView skia;
            // Created under the look, so values read at creation (padding, colors) are the look's too.
            using (SkUiLookScope.Enter(_look))
                skia = row.CreateSkia();
            skia.Look = _look;
            // Aligned to the start, each side keeps its own size; a stretching row (Slider, Entry) fills the column's width.
            Align(native, row.Stretch);
            Align(skia, row.Stretch);
            var view = new RowView(row.Name, native, skia, new Label { FontSize = 12 });
            _rowViews.Add(view);
            native.SizeChanged += (_, _) => Measured(view);
            skia.SizeChanged += (_, _) => Measured(view);
            _rowsHost.Add(RowLayout(view));
        }
    }

    private static void Align(View view, bool stretch)
    {
        if (!view.IsSet(View.VerticalOptionsProperty))
            view.VerticalOptions = LayoutOptions.Start;
        if (!view.IsSet(View.HorizontalOptionsProperty))
            view.HorizontalOptions = stretch ? LayoutOptions.Fill : LayoutOptions.Start;
    }

    private Grid ColumnHeaders()
    {
        var grid = new Grid { ColumnDefinitions = Columns() };
        if (_overlay)
        {
            grid.Add(Header($"{NativeColumnTitle} + {SkiaColumnTitle}"));
            return grid;
        }
        grid.Add(Header(NativeColumnTitle));
        grid.Add(Header(SkiaColumnTitle), 1);
        return grid;
    }

    private static Label Header(string text) => new() { Text = text, FontAttributes = FontAttributes.Bold };

    private ColumnDefinitionCollection Columns() => _overlay
        ? [new ColumnDefinition(GridLength.Star)]
        : [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)];

    /// <summary>
    /// One row: its name, the two controls (side by side, or the SkiaUi one over the native one) and the size caption.
    /// Without app styles, empty implicit styles for the row's control types hide the app's.
    /// </summary>
    private View RowLayout(RowView view)
    {
        var cells = new Grid { ColumnDefinitions = Columns(), ColumnSpacing = 12 };
        if (_overlay)
        {
            view.Skia.Opacity = 0.5;
            view.Skia.InputTransparent = true;
            cells.Add(view.Native);
            cells.Add(view.Skia);
        }
        else
        {
            cells.Add(view.Native);
            cells.Add(view.Skia, 1);
        }
        var row = new VerticalStackLayout { Spacing = 4, Children = { new Label { Text = view.Name, FontSize = 12, Opacity = 0.7 }, cells, view.Caption } };
        if (!_appStyles)
        {
            cells.Resources.Add(new Style(view.Native.GetType()));
            cells.Resources.Add(new Style(view.Skia.GetType()));
        }
        return row;
    }

    private void Measured(RowView view)
    {
        var native = view.Native.Bounds.Size;
        var skia = view.Skia.Bounds.Size;
        var matches = Matches(native, skia);
        view.Caption.Text = $"{NativeColumnTitle} {Format(native)}   {SkiaColumnTitle} {Format(skia)}";
        view.Caption.TextColor = matches ? MatchColor : MismatchColor;
        if (_reportPending) return;
        _reportPending = true;
        // One report after the layout pass settles.
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500), WriteReport);
    }

    private void WriteReport()
    {
        _reportPending = false;
        var lookName = _looks.FirstOrDefault(entry => entry.Look == _look).Name ?? _look.GetType().Name;
        var report = new StringBuilder();
        report.Append(CultureInfo.InvariantCulture,
            $"{LogTag} look={lookName} platform={DeviceInfo.Current.Platform} idiom={DeviceInfo.Current.Idiom} appStyles={_appStyles} lookColors={_lookColors}").AppendLine();
        foreach (var view in _rowViews)
        {
            var native = view.Native.Bounds.Size;
            var skia = view.Skia.Bounds.Size;
            report.Append(CultureInfo.InvariantCulture,
                $"{LogTag} {view.Name}: native={Format(native)} skiaui={Format(skia)} {(Matches(native, skia) ? "same" : "DIFFERS")}").AppendLine();
        }
        Report = report.ToString();
        Console.Write(Report);
    }

    /// <summary>Whether two controls have the same size (a stretching row's width is the column's on both sides).</summary>
    private static bool Matches(Size native, Size skia) =>
        Math.Abs(native.Width - skia.Width) < SizeTolerance && Math.Abs(native.Height - skia.Height) < SizeTolerance;

    private static string Format(Size size) => string.Create(CultureInfo.InvariantCulture, $"{size.Width:0.#}x{size.Height:0.#}");

    private sealed record ComparisonRow(string Name, Func<View> CreateNative, Func<SkUiView> CreateSkia, bool Stretch = false);

    private sealed record RowView(string Name, View Native, SkUiView Skia, Label Caption);
}
