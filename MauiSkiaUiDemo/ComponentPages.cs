using MauiSkiaUi;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Storage;
using NativeShapes = Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUiDemo;

public sealed class LabelDemoPage : ComponentDemoPage
{
    public LabelDemoPage() : base("SkUiLabel", new SkUiLabel(), new Label())
    {
        var skia = (SkUiLabel)SkiaControl;
        var native = (Label)NativeControl!;
        MultilineText("Text", "Earth is our home.\nOceans cover 71 percent of its surface.", value => { skia.Text = value; native.Text = value; }, () => skia.Text, () => native.Text);
        Choice("FontFamily", new[] { "Default", "OpenSansRegular", "OpenSansSemibold", "Lobster", "RobotoMono" }, "Default",
            value => { var family = value == "Default" ? null : value; skia.FontFamily = family; native.FontFamily = family; },
            () => skia.FontFamily ?? "Default", () => native.FontFamily ?? "Default");
        Number("FontSize", 10, 36, 18, value => { skia.FontSize = value; native.FontSize = value; }, () => skia.FontSize, () => native.FontSize);
        Choice("LineBreakMode", Enum.GetValues<LineBreakMode>(), LineBreakMode.WordWrap, value => { skia.LineBreakMode = value; native.LineBreakMode = value; }, () => skia.LineBreakMode, () => native.LineBreakMode);
        Choice("HorizontalTextAlignment", Enum.GetValues<TextAlignment>(), TextAlignment.Start, value => { skia.HorizontalTextAlignment = value; native.HorizontalTextAlignment = value; }, () => skia.HorizontalTextAlignment, () => native.HorizontalTextAlignment);
        Choice("FontAttributes", new[] { FontAttributes.None, FontAttributes.Bold, FontAttributes.Italic }, FontAttributes.None, value => { skia.FontAttributes = value; native.FontAttributes = value; }, () => skia.FontAttributes, () => native.FontAttributes);
        ColorEditor("TextColor", Ink, value => { skia.TextColor = value; native.TextColor = value; }, () => skia.TextColor, () => native.TextColor);
        Choice("VerticalTextAlignment", Enum.GetValues<TextAlignment>(), TextAlignment.Start, value => { skia.VerticalTextAlignment = value; native.VerticalTextAlignment = value; }, () => skia.VerticalTextAlignment, () => native.VerticalTextAlignment);
        Number("Padding", 0, 24, 0, value => { skia.Padding = value; native.Padding = value; }, () => skia.Padding.Left, () => native.Padding.Left);
    }
}

public sealed class ButtonDemoPage : ComponentDemoPage
{
    public ButtonDemoPage() : base("SkUiButton", new SkUiButton(), new Button())
    {
        var skia = (SkUiButton)SkiaControl;
        var native = (Button)NativeControl!;
        var skiaClicks = 0;
        var nativeClicks = 0;
        var canExecute = true;
        void Counts() => Feedback($"Clicks: {skiaClicks}", $"Clicks: {nativeClicks}");
        var skiaCommand = new Command(() => { skiaClicks++; Counts(); }, () => canExecute);
        var nativeCommand = new Command(() => { nativeClicks++; Counts(); }, () => canExecute);
        skia.Command = skiaCommand;
        native.Command = nativeCommand;
        native.TextColor = Colors.White;
        skia.TextColor = Colors.White;
        Text("Text", "Add observation", value => { skia.Text = value; native.Text = value; }, () => skia.Text, () => native.Text);
        Number("FontSize", 10, 30, 16, value => { skia.FontSize = value; native.FontSize = value; }, () => skia.FontSize, () => native.FontSize);
        Number("CornerRadius", 0, 30, 6, value => { skia.CornerRadius = value; native.CornerRadius = (int)Math.Round(value); }, () => skia.CornerRadius);
        Number("BorderWidth", 0, 8, 1, value => { skia.BorderWidth = value; native.BorderWidth = value; }, () => skia.BorderWidth, () => native.BorderWidth);
        ColorEditor("Fill", Accent, value => { skia.FillColor = value; native.Background = value; }, () => skia.FillColor, () => ((SolidColorBrush)native.Background).Color);
        ColorEditor("Border", Ink, value => { skia.BorderColor = value; native.BorderColor = value; }, () => skia.BorderColor, () => native.BorderColor);
        Toggle("CanExecute", true, value => { canExecute = value; skiaCommand.ChangeCanExecute(); nativeCommand.ChangeCanExecute(); }, () => skia.Command.CanExecute(null), () => native.Command.CanExecute(null));
        OnReset(() => { skiaClicks = nativeClicks = 0; Counts(); });
    }
}

public sealed class GridDemoPage : ComponentDemoPage
{
    public GridDemoPage() : base("SkUiGrid", new SkUiGrid(), new Grid())
    {
        var skia = (SkUiGrid)SkiaControl;
        var native = (Grid)NativeControl!;
        skia.RowDefinitions = [new(GridLength.Star), new(GridLength.Star)];
        native.RowDefinitions = [new(GridLength.Star), new(GridLength.Star)];
        skia.ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)];
        native.ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)];
        for (var index = 0; index < 3; index++)
        {
            var color = index == 0 ? Accent : index == 1 ? Color.FromArgb("#A12842") : Color.FromArgb("#285C9C");
            var drawn = new SkUiLabel { Text = $"Cell {index + 1}", Background = color, TextColor = Colors.White, FontSize = 14 };
            var standard = new Label { Text = drawn.Text, Background = color, TextColor = Colors.White, FontSize = 14 };
            Grid.SetRow(drawn, index / 2);
            Grid.SetColumn(drawn, index % 2);
            Grid.SetRow(standard, index / 2);
            Grid.SetColumn(standard, index % 2);
            skia.Children.Add(drawn);
            native.Children.Add(standard);
        }
        Number("Padding", 0, 24, 8, value => { skia.Padding = value; native.Padding = value; }, () => skia.Padding.Left, () => native.Padding.Left);
        Number("RowSpacing", 0, 24, 6, value => { skia.RowSpacing = value; native.RowSpacing = value; }, () => skia.RowSpacing, () => native.RowSpacing);
        Number("ColumnSpacing", 0, 24, 6, value => { skia.ColumnSpacing = value; native.ColumnSpacing = value; }, () => skia.ColumnSpacing, () => native.ColumnSpacing);
        Choice("FirstColumn", new[] { GridLength.Auto, GridLength.Star, new GridLength(64) }, GridLength.Star,
            value => { skia.ColumnDefinitions[0].Width = value; native.ColumnDefinitions[0].Width = value; }, () => skia.ColumnDefinitions[0].Width, () => native.ColumnDefinitions[0].Width);
        Toggle("SpanLastCell", true, value => { Grid.SetColumnSpan((BindableObject)skia.Children[2], value ? 2 : 1); Grid.SetColumnSpan((BindableObject)native.Children[2], value ? 2 : 1); },
            () => Grid.GetColumnSpan((BindableObject)skia.Children[2]) == 2, () => Grid.GetColumnSpan((BindableObject)native.Children[2]) == 2);
        Toggle("FirstCellVisible", true, value => { ((View)skia.Children[0]).IsVisible = value; ((View)native.Children[0]).IsVisible = value; },
            () => ((View)skia.Children[0]).IsVisible, () => ((View)native.Children[0]).IsVisible);
    }
}

public sealed class MauiContentViewDemoPage : ComponentDemoPage
{
    private readonly Editor editor = new() { AutoSize = EditorAutoSizeOption.TextChanges, BackgroundColor = Colors.White, TextColor = Ink, FontFamily = "RobotoMono" };
    private readonly WebView webView = new();

    public MauiContentViewDemoPage() : base("SkUiMauiContentView", new SkUiGrid { RowSpacing = 8, Padding = 12 },
        widthRange: (220, 420, 320), heightRange: (300, 560, 440))
    {
        editor.Text = "<h3>Live HTML</h3>\n<p>Edit this HTML \u2014 the WebView below updates as you type.</p>";
        var grid = (SkUiGrid)SkiaControl;
        grid.RowDefinitions = [new(GridLength.Auto), new(new GridLength(140)), new(GridLength.Auto), new(GridLength.Auto), new(new GridLength(220))];
        var sourceLabel = new SkUiLabel { Text = "HTML source (edit me)", FontAttributes = FontAttributes.Bold, TextColor = Ink };
        var editorHost = new SkUiMauiContentView { Content = editor };
        var refresh = new SkUiButton { Text = "Refresh preview", FillColor = Accent, FontSize = 14 };
        var previewLabel = new SkUiLabel { Text = "Live preview", FontAttributes = FontAttributes.Bold, TextColor = Ink };
        var webHost = new SkUiMauiContentView { Content = webView };
        Grid.SetRow(editorHost, 1);
        Grid.SetRow(refresh, 2);
        Grid.SetRow(previewLabel, 3);
        Grid.SetRow(webHost, 4);
        grid.Children.Add(sourceLabel);
        grid.Children.Add(editorHost);
        grid.Children.Add(refresh);
        grid.Children.Add(previewLabel);
        grid.Children.Add(webHost);

        void Apply() => webView.Source = new HtmlWebViewSource { Html = editor.Text };
        Apply();
        editor.TextChanged += (_, _) => Apply();
        refresh.Clicked += (_, _) => Apply();
    }
}

public sealed class ContentViewDemoPage : ComponentDemoPage
{
    public ContentViewDemoPage() : base("SkUiContentView", new SkUiContentView(), new ContentView())
    {
        var skia = (SkUiContentView)SkiaControl;
        var native = (ContentView)NativeControl!;
        var drawn = new SkUiLabel { Background = Accent, TextColor = Colors.White, FontSize = 16 };
        var standard = new Label { Background = Accent, TextColor = Colors.White, FontSize = 16 };
        skia.Content = drawn;
        native.Content = standard;
        Text("ContentText", "Hosted content", value => { drawn.Text = value; standard.Text = value; }, () => drawn.Text, () => standard.Text);
        Number("Padding", 0, 28, 12, value => { skia.Padding = value; native.Padding = value; }, () => skia.Padding.Left, () => native.Padding.Left);
        ColorEditor("Background", Color.FromArgb("#DCE8EA"), value => { skia.Background = value; native.Background = value; },
            () => ((SolidColorBrush)skia.Background).Color, () => ((SolidColorBrush)native.Background).Color);
        Toggle("HasContent", true, value => { skia.Content = value ? drawn : null; native.Content = value ? standard : null; }, () => skia.Content is not null, () => native.Content is not null);
    }
}

public sealed class BorderDemoPage : ComponentDemoPage
{
    public BorderDemoPage() : base("SkUiBorder", new SkUiBorder(), new Border())
    {
        var skia = (SkUiBorder)SkiaControl;
        var native = (Border)NativeControl!;
        var drawn = new SkUiLabel { Text = "Bordered", Background = Colors.White, TextColor = Ink, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
        var standard = new Label { Text = "Bordered", Background = Colors.White, TextColor = Ink, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
        skia.Content = drawn;
        native.Content = standard;
        native.Stroke = Accent;
        native.StrokeThickness = 2;
        native.StrokeShape = new NativeShapes.RoundRectangle { CornerRadius = 10 };
        ColorEditor("Stroke", Accent, value => { skia.Stroke = value; native.Stroke = new SolidColorBrush(value); }, () => skia.Stroke!, () => ((SolidColorBrush)native.Stroke).Color);
        Number("StrokeThickness", 0, 8, 2, value => { skia.StrokeThickness = value; native.StrokeThickness = value; }, () => skia.StrokeThickness, () => native.StrokeThickness);
        Number("CornerRadius", 0, 30, 10, value => { skia.CornerRadius = value; native.StrokeShape = new NativeShapes.RoundRectangle { CornerRadius = value }; }, () => skia.CornerRadius);
    }
}

public sealed class ActivityIndicatorDemoPage : ComponentDemoPage
{
    public ActivityIndicatorDemoPage() : base("SkUiActivityIndicator", new SkUiActivityIndicator(), new ActivityIndicator())
    {
        var skia = (SkUiActivityIndicator)SkiaControl;
        var native = (ActivityIndicator)NativeControl!;
        Toggle("IsRunning", true, value => { skia.IsRunning = value; native.IsRunning = value; }, () => skia.IsRunning, () => native.IsRunning);
        ColorEditor("Color", Accent, value => { skia.Color = value; native.Color = value; }, () => skia.Color, () => native.Color);
    }
}

public sealed class ImageButtonDemoPage : ComponentDemoPage
{
    public ImageButtonDemoPage() : base("SkUiImageButton", new SkUiImageButton(), new ImageButton())
    {
        var skia = (SkUiImageButton)SkiaControl;
        var native = (ImageButton)NativeControl!;
        var skiaClicks = 0;
        var nativeClicks = 0;
        void Source()
        {
            skia.Source = ImageSource.FromFile("earth.jpg");
            native.Source = ImageSource.FromFile("earth.jpg");
        }
        Source();
        native.BackgroundColor = Colors.Transparent;
        void Counts() => Feedback($"Clicks: {skiaClicks}", $"Clicks: {nativeClicks}");
        skia.Clicked += (_, _) => { skiaClicks++; Counts(); };
        native.Clicked += (_, _) => { nativeClicks++; Counts(); };
        Number("CornerRadius", 0, 30, 8, value => { skia.CornerRadius = value; native.CornerRadius = (int)Math.Round(value); }, () => skia.CornerRadius);
        Toggle("Enabled", true, value => { skia.IsEnabled = value; native.IsEnabled = value; }, () => skia.IsEnabled, () => native.IsEnabled);
        OnReset(() => { skiaClicks = nativeClicks = 0; Counts(); });
    }
}

public sealed class SwitchDemoPage : ComponentDemoPage
{
    public SwitchDemoPage() : base("SkUiSwitch", new SkUiSwitch(), new Switch())
    {
        var skia = (SkUiSwitch)SkiaControl;
        var native = (Switch)NativeControl!;
        Toggle("IsChecked", false, value => { skia.IsChecked = value; native.IsToggled = value; }, () => skia.IsChecked, () => native.IsToggled);
        ColorEditor("OnColor", Accent, value => { skia.OnColor = value; native.OnColor = value; }, () => skia.OnColor, () => native.OnColor);
    }
}

public sealed class CheckBoxDemoPage : ComponentDemoPage
{
    public CheckBoxDemoPage() : base("SkUiCheckBox", new SkUiCheckBox(), new CheckBox())
    {
        var skia = (SkUiCheckBox)SkiaControl;
        var native = (CheckBox)NativeControl!;
        Toggle("IsChecked", false, value => { skia.IsChecked = value; native.IsChecked = value; }, () => skia.IsChecked, () => native.IsChecked);
        ColorEditor("Color", Accent, value => { skia.Color = value; native.Color = value; }, () => skia.Color, () => native.Color);
    }
}

public sealed class RadioButtonDemoPage : ComponentDemoPage
{
    public RadioButtonDemoPage() : base("SkUiRadioButton", new SkUiRadioButton(), new RadioButton())
    {
        var skia = (SkUiRadioButton)SkiaControl;
        var native = (RadioButton)NativeControl!;
        native.Content = "Option";
        Toggle("IsChecked", false, value => { skia.IsChecked = value; native.IsChecked = value; }, () => skia.IsChecked, () => native.IsChecked);
        ColorEditor("Color", Accent, value => skia.Color = value, () => skia.Color);
    }
}

public sealed class VerticalStackLayoutDemoPage : ComponentDemoPage
{
    public VerticalStackLayoutDemoPage() : base("SkUiVerticalStackLayout", new SkUiVerticalStackLayout(), new VerticalStackLayout())
    {
        var skia = (SkUiVerticalStackLayout)SkiaControl;
        var native = (VerticalStackLayout)NativeControl!;
        for (var index = 0; index < 3; index++)
        {
            var color = index == 0 ? Accent : index == 1 ? Color.FromArgb("#A12842") : Color.FromArgb("#285C9C");
            skia.Children.Add(new SkUiBox { Color = color, HeightRequest = 24 });
            native.Add(new BoxView { Color = color, HeightRequest = 24 });
        }
        Number("Spacing", 0, 24, 6, value => { skia.Spacing = value; native.Spacing = value; }, () => skia.Spacing, () => native.Spacing);
    }
}

public sealed class HorizontalStackLayoutDemoPage : ComponentDemoPage
{
    public HorizontalStackLayoutDemoPage() : base("SkUiHorizontalStackLayout", new SkUiHorizontalStackLayout(), new HorizontalStackLayout())
    {
        var skia = (SkUiHorizontalStackLayout)SkiaControl;
        var native = (HorizontalStackLayout)NativeControl!;
        for (var index = 0; index < 3; index++)
        {
            var color = index == 0 ? Accent : index == 1 ? Color.FromArgb("#A12842") : Color.FromArgb("#285C9C");
            skia.Children.Add(new SkUiBox { Color = color, WidthRequest = 24 });
            native.Add(new BoxView { Color = color, WidthRequest = 24 });
        }
        Number("Spacing", 0, 24, 6, value => { skia.Spacing = value; native.Spacing = value; }, () => skia.Spacing, () => native.Spacing);
    }
}

public sealed class AbsoluteLayoutDemoPage : ComponentDemoPage
{
    public AbsoluteLayoutDemoPage() : base("SkUiAbsoluteLayout", new SkUiAbsoluteLayout(), new AbsoluteLayout())
    {
        var skia = (SkUiAbsoluteLayout)SkiaControl;
        var native = (AbsoluteLayout)NativeControl!;
        var drawn = new SkUiBox { Color = Accent };
        var standard = new BoxView { Color = Accent };
        SkUiAbsoluteLayout.SetLayoutBounds(drawn, new Rect(0.1, 0.1, 60, 40));
        AbsoluteLayout.SetLayoutBounds(standard, new Rect(0.1, 0.1, 60, 40));
        AbsoluteLayout.SetLayoutFlags(standard, AbsoluteLayoutFlags.PositionProportional);
        SkUiAbsoluteLayout.SetLayoutFlags(drawn, AbsoluteLayoutFlags.PositionProportional);
        skia.Children.Add(drawn);
        native.Add(standard);
        Number("X", 0, 1, 0.1, value => { SkUiAbsoluteLayout.SetLayoutBounds(drawn, new Rect(value, SkUiAbsoluteLayout.GetLayoutBounds(drawn).Y, 60, 40)); AbsoluteLayout.SetLayoutBounds(standard, new Rect(value, AbsoluteLayout.GetLayoutBounds(standard).Y, 60, 40)); },
            () => SkUiAbsoluteLayout.GetLayoutBounds(drawn).X, () => AbsoluteLayout.GetLayoutBounds(standard).X);
        Number("Y", 0, 1, 0.1, value => { SkUiAbsoluteLayout.SetLayoutBounds(drawn, new Rect(SkUiAbsoluteLayout.GetLayoutBounds(drawn).X, value, 60, 40)); AbsoluteLayout.SetLayoutBounds(standard, new Rect(AbsoluteLayout.GetLayoutBounds(standard).X, value, 60, 40)); },
            () => SkUiAbsoluteLayout.GetLayoutBounds(drawn).Y, () => AbsoluteLayout.GetLayoutBounds(standard).Y);
    }
}

public sealed class ImageDemoPage : ComponentDemoPage
{
    private readonly SkUiImage skia;
    private readonly Image native;
    private string sample = "Earth";

    public ImageDemoPage() : base("SkUiImage", new SkUiImage(), new Image())
    {
        skia = (SkUiImage)SkiaControl;
        native = (Image)NativeControl!;
        Choice("Aspect", new[] { Aspect.AspectFit, Aspect.AspectFill, Aspect.Fill }, Aspect.AspectFit,
            value => { skia.Aspect = value; native.Aspect = value; }, () => skia.Aspect, () => native.Aspect);
        Choice("Source", new[] { "Earth", "Empty", "Invalid" }, "Earth", SetSample, () => sample);
        skia.PropertyChanged += (_, args) => { if (args.PropertyName is nameof(SkUiImage.IsLoading) or nameof(SkUiImage.LoadError)) UpdateImageStatus(); };
        native.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(Image.IsLoading)) UpdateImageStatus(); };
        ActionButton("Reload image", () => SetSample(sample));
        UpdateImageStatus();
    }

    private void SetSample(string value)
    {
        sample = value;
        ImageSource? Source() => value == "Empty" ? null : new StreamImageSource
        {
            Stream = async token =>
            {
                if (value == "Invalid") return new MemoryStream([1, 2, 3]);
                using var stream = await FileSystem.Current.OpenAppPackageFileAsync("earth.jpg");
                var bytes = new MemoryStream();
                try { await stream.CopyToAsync(bytes, token); bytes.Position = 0; return bytes; }
                catch { bytes.Dispose(); throw; }
            }
        };
        skia.Source = Source();
        native.Source = Source();
    }

    private void UpdateImageStatus() => Feedback(skia.IsLoading ? "Loading" : skia.LoadError is not null ? "Decode failed" : $"Image {skia.ImageSize.Width:F0} x {skia.ImageSize.Height:F0}",
        native.IsLoading ? "Loading" : native.Source is null ? "Empty" : "Source assigned");

    protected override void OnDisappearing()
    {
        skia.Source = null;
        native.Source = null;
        base.OnDisappearing();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (skia.Source is null) SetSample(sample);
    }
}

public sealed class ScrollViewDemoPage : ComponentDemoPage
{
    private readonly SkUiScrollView skia;
    private readonly ScrollView native;

    public ScrollViewDemoPage() : base("SkUiScrollView", new SkUiScrollView(), new ScrollView())
    {
        skia = (SkUiScrollView)SkiaControl;
        native = (ScrollView)NativeControl!;
        var drawn = new SkUiGrid { WidthRequest = 400, HeightRequest = 640, RowSpacing = 8 };
        var standard = new Grid { WidthRequest = 400, HeightRequest = 640, RowSpacing = 8 };
        var skiaClicks = 0;
        var nativeClicks = 0;
        void Status() => Feedback($"Offset {skia.ScrollX:F0}, {skia.ScrollY:F0}; taps {skiaClicks}", $"Offset {native.ScrollX:F0}, {native.ScrollY:F0}; taps {nativeClicks}");
        for (var index = 0; index < 10; index++)
        {
            drawn.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            standard.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            var button = new SkUiButton { Text = $"Row {index + 1}", FillColor = Accent, FontSize = 14 };
            var counterpart = new Button { Text = button.Text, Background = Accent, TextColor = Colors.White, FontSize = 14 };
            button.Clicked += (_, _) => { skiaClicks++; Status(); };
            counterpart.Clicked += (_, _) => { nativeClicks++; Status(); };
            Grid.SetRow(button, index);
            drawn.Children.Add(button);
            standard.Add(counterpart, 0, index);
        }
        skia.Content = drawn;
        native.Content = standard;
        skia.Scrolled += (_, _) => Status();
        native.Scrolled += (_, _) => Status();
        Choice("Orientation", Enum.GetValues<ScrollOrientation>(), ScrollOrientation.Vertical, value => { skia.Orientation = value; native.Orientation = value; }, () => skia.Orientation, () => native.Orientation);
        Number("ContentHeight", 200, 1200, 640, value => { drawn.HeightRequest = value; standard.HeightRequest = value; }, () => drawn.HeightRequest, () => standard.HeightRequest);
        Number("ContentWidth", 200, 800, 400, value => { drawn.WidthRequest = value; standard.WidthRequest = value; }, () => drawn.WidthRequest, () => standard.WidthRequest);
        Number("Padding", 0, 24, 0, value => { skia.Padding = value; native.Padding = value; }, () => skia.Padding.Left, () => native.Padding.Left);
        ActionButton("Scroll to middle", () => ScrollBoth(100, 250));
        ActionButton("Scroll to start", () => ScrollBoth(0, 0));
        OnReset(() => { skiaClicks = nativeClicks = 0; ScrollBoth(0, 0); });
    }

    private async void ScrollBoth(double horizontal, double vertical)
    {
        skia.ScrollTo(horizontal, vertical);
        if (native.Handler is not null) await native.ScrollToAsync(horizontal, vertical, false);
    }

    protected override void OnDisappearing()
    {
        skia.ScrollTo(skia.ScrollX, skia.ScrollY);
        base.OnDisappearing();
    }
}

public sealed class ViewDemoPage : ComponentDemoPage
{
    public ViewDemoPage() : base("SkUiView", new SkUiView())
    {
        ColorEditor("Background", Accent, value => SkiaControl.Background = value, () => ((SolidColorBrush)SkiaControl.Background).Color);
        Number("Rotation", -45, 45, 0, value => SkiaControl.Rotation = value, () => SkiaControl.Rotation);
        Number("Scale", 0.25, 1.25, 1, value => SkiaControl.Scale = value, () => SkiaControl.Scale);
        var taps = 0;
        SkiaControl.Tapped += (_, _) => Feedback($"Taps: {++taps}");
        Toggle("InputTransparent", false, value => SkiaControl.InputTransparent = value, () => SkiaControl.InputTransparent);
        OnReset(() => taps = 0);
    }
}

public sealed class LayoutDemoPage : ComponentDemoPage
{
    public LayoutDemoPage() : base("SkUiLayout", new SkUiLayout())
    {
        var layout = (SkUiLayout)SkiaControl;
        var first = new SkUiBox { Color = Accent, Margin = new Thickness(0, 0, 35, 25) };
        var second = new SkUiEllipse { Color = Color.FromArgb("#A12842"), Margin = new Thickness(35, 25, 0, 0) };
        layout.Children.Add(first);
        layout.Children.Add(second);
        first.Tapped += (_, _) => Feedback("Tapped box");
        second.Tapped += (_, _) => Feedback("Tapped ellipse");
        Number("Padding", 0, 24, 8, value => layout.Padding = value, () => layout.Padding.Left);
        Toggle("BoxOnTop", false, value => first.ZIndex = value ? 1 : 0, () => first.ZIndex == 1);
        Number("EllipseOpacity", 0, 1, 0.75, value => second.Opacity = value, () => second.Opacity);
        Toggle("EllipseInputTransparent", false, value => second.InputTransparent = value, () => second.InputTransparent);
    }
}

public abstract class ShapeDemoPage : ComponentDemoPage
{
    protected ShapeDemoPage(string title, SkUiShape skia, View native) : base(title, skia, native)
    {
        ColorEditor("Color", Accent, value =>
        {
            skia.Color = value;
            if (native is BoxView box) box.Color = value;
            else if (native is NativeShapes.Line line) line.Stroke = value;
            else ((NativeShapes.Shape)native).Fill = value;
        }, () => skia.Color);
        Number("Rotation", -45, 45, 0, value => { skia.Rotation = value; native.Rotation = value; }, () => skia.Rotation, () => native.Rotation);
        if (native is NativeShapes.Line line)
        {
            void Endpoints() { line.X1 = line.StrokeThickness / 2; line.Y1 = line.StrokeThickness / 2; line.X2 = Math.Max(line.X1, line.Width - line.X1); line.Y2 = Math.Max(line.Y1, line.Height - line.Y1); }
            line.SizeChanged += (_, _) => Endpoints();
            Number("StrokeWidth", 0, 16, 2, value => { skia.StrokeWidth = value; line.StrokeThickness = value; Endpoints(); }, () => skia.StrokeWidth, () => line.StrokeThickness);
        }
    }
}

public sealed class BoxDemoPage() : ShapeDemoPage("SkUiBox", new SkUiBox(), new BoxView());
public sealed class EllipseDemoPage() : ShapeDemoPage("SkUiEllipse", new SkUiEllipse(), new NativeShapes.Ellipse { StrokeThickness = 0 });
public sealed class LineDemoPage() : ShapeDemoPage("SkUiLine", new SkUiLine(), new NativeShapes.Line { Aspect = Stretch.Fill });

/// <summary>The gallery section a component belongs to.</summary>
public enum ComponentCategory
{
    /// <summary>Leaf, non-layout, non-shape controls (SkUiView, SkUiLabel, SkUiButton, SkUiImage, ...).</summary>
    BasicControls,
    /// <summary>Composition hosts and multi/single-child layouts (SkUiContentView, SkUiLayout, SkUiGrid, ...).</summary>
    Layouts,
    /// <summary>Shape primitives drawn directly with SkiaSharp (SkUiBox, SkUiEllipse, SkUiLine, ...).</summary>
    Graphics,
    /// <summary>SkUiScrollView and, later, virtualizing collection view controls.</summary>
    ScrollingAndCollections
}

/// <summary>Display metadata for a <see cref="ComponentCategory"/>.</summary>
public static class ComponentCategoryInfo
{
    /// <summary>Gallery section titles, in display order.</summary>
    public static readonly IReadOnlyList<ComponentCategory> Order =
        [ComponentCategory.BasicControls, ComponentCategory.Layouts, ComponentCategory.Graphics, ComponentCategory.ScrollingAndCollections];

    /// <summary>Maps a category to its gallery section title.</summary>
    public static string Title(ComponentCategory category) => category switch
    {
        ComponentCategory.BasicControls => "Basic controls",
        ComponentCategory.Layouts => "Layouts",
        ComponentCategory.Graphics => "Graphics",
        ComponentCategory.ScrollingAndCollections => "Scrolling & collections",
        _ => category.ToString()
    };
}

public sealed record ComponentDemo(Type ComponentType, Type PageType, string Counterpart, ComponentCategory Category, Func<ComponentDemoPage> Create)
{
    public string Name => ComponentType.Name;
    public string Route => "demo-" + Name;
}

public static class ComponentDemos
{
    public static IReadOnlyList<ComponentDemo> All { get; } = new ComponentDemo[]
    {
        new(typeof(SkUiView), typeof(ViewDemoPage), "SkUi only", ComponentCategory.BasicControls, () => new ViewDemoPage()),
        new(typeof(SkUiLabel), typeof(LabelDemoPage), "Label", ComponentCategory.BasicControls, () => new LabelDemoPage()),
        new(typeof(SkUiButton), typeof(ButtonDemoPage), "Button", ComponentCategory.BasicControls, () => new ButtonDemoPage()),
        new(typeof(SkUiImage), typeof(ImageDemoPage), "Image", ComponentCategory.BasicControls, () => new ImageDemoPage()),
        new(typeof(SkUiImageButton), typeof(ImageButtonDemoPage), "ImageButton", ComponentCategory.BasicControls, () => new ImageButtonDemoPage()),
        new(typeof(SkUiActivityIndicator), typeof(ActivityIndicatorDemoPage), "ActivityIndicator", ComponentCategory.BasicControls, () => new ActivityIndicatorDemoPage()),
        new(typeof(SkUiSwitch), typeof(SwitchDemoPage), "Switch", ComponentCategory.BasicControls, () => new SwitchDemoPage()),
        new(typeof(SkUiCheckBox), typeof(CheckBoxDemoPage), "CheckBox", ComponentCategory.BasicControls, () => new CheckBoxDemoPage()),
        new(typeof(SkUiRadioButton), typeof(RadioButtonDemoPage), "RadioButton", ComponentCategory.BasicControls, () => new RadioButtonDemoPage()),
        new(typeof(SkUiContentView), typeof(ContentViewDemoPage), "ContentView", ComponentCategory.Layouts, () => new ContentViewDemoPage()),
        new(typeof(SkUiMauiContentView), typeof(MauiContentViewDemoPage), "Editor / WebView (hosted natively)", ComponentCategory.Layouts, () => new MauiContentViewDemoPage()),
        new(typeof(SkUiBorder), typeof(BorderDemoPage), "Border", ComponentCategory.Layouts, () => new BorderDemoPage()),
        new(typeof(SkUiLayout), typeof(LayoutDemoPage), "SkUi only", ComponentCategory.Layouts, () => new LayoutDemoPage()),
        new(typeof(SkUiGrid), typeof(GridDemoPage), "Grid", ComponentCategory.Layouts, () => new GridDemoPage()),
        new(typeof(SkUiVerticalStackLayout), typeof(VerticalStackLayoutDemoPage), "VerticalStackLayout", ComponentCategory.Layouts, () => new VerticalStackLayoutDemoPage()),
        new(typeof(SkUiHorizontalStackLayout), typeof(HorizontalStackLayoutDemoPage), "HorizontalStackLayout", ComponentCategory.Layouts, () => new HorizontalStackLayoutDemoPage()),
        new(typeof(SkUiAbsoluteLayout), typeof(AbsoluteLayoutDemoPage), "AbsoluteLayout", ComponentCategory.Layouts, () => new AbsoluteLayoutDemoPage()),
        new(typeof(SkUiBox), typeof(BoxDemoPage), "BoxView", ComponentCategory.Graphics, () => new BoxDemoPage()),
        new(typeof(SkUiEllipse), typeof(EllipseDemoPage), "Ellipse", ComponentCategory.Graphics, () => new EllipseDemoPage()),
        new(typeof(SkUiLine), typeof(LineDemoPage), "Line", ComponentCategory.Graphics, () => new LineDemoPage()),
        new(typeof(SkUiScrollView), typeof(ScrollViewDemoPage), "ScrollView", ComponentCategory.ScrollingAndCollections, () => new ScrollViewDemoPage())
    };
}