# Custom controls

Find every custom element the region uses (`local:`, `controls:` and other `clr-namespace` prefixes) and its base class. A custom control used on many pages is worth porting once; a one-off can be wrapped.

| The control is | Port it to |
| --- | --- |
| A `ContentView` (or layout) composing MAUI views, XAML or C# | A `SkUiContentView` subclass composing drawn views. Same bindable properties, same XAML with `sk:` controls |
| Drawn with `GraphicsView` / `IDrawable` / `SKCanvasView` | A `SkUiView` subclass (below) |
| A MAUI control with a custom handler, mapper changes (`AppendToMapping`), renderer or effect | Redraw it with drawn views, or keep it native in `SkUiMauiContentView` |
| A templated control (`ControlTemplate`, `TemplateBinding`) | A `SkUiContentView` subclass that composes the parts directly. Only `SkUiRadioButton` takes a drawn `ControlTemplate`; there, bind with `{Binding X, Source={RelativeSource AncestorType={x:Type sk:SkUiRadioButton}}}` because `TemplateBinding` does not reach drawn controls |
| A third-party control (charts, calendars, data grids, signature pads, list views) | Native: `SkUiMauiContentView`, or outside the drawn region |
| Many small repeated parts where allocation matters (grid cells, chips, tiles) | Core nodes (`MauiSkiaUi.Core`, `SkUiCore*` fluent nodes) under a `SkUiCoreHost`: no `BindableObject` per node |

## Composite control

```xml
<!-- Before: <ContentView x:Class="App.Controls.InfoRow" ...> -->
<sk:SkUiContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                    xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi"
                    x:Class="App.Controls.InfoRow" x:Name="Root">
    <sk:SkUiGrid ColumnDefinitions="Auto,*" ColumnSpacing="12">
        <sk:SkUiImage Source="{Binding Icon, Source={x:Reference Root}}" WidthRequest="24" HeightRequest="24" />
        <sk:SkUiLabel Grid.Column="1" Text="{Binding Title, Source={x:Reference Root}}" />
    </sk:SkUiGrid>
</sk:SkUiContentView>
```

```csharp
public partial class InfoRow : SkUiContentView   // was ContentView
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(InfoRow));
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    // … unchanged
    public InfoRow() => InitializeComponent();
}
```

A drawn custom control can only be placed inside drawn trees (or be a surface root itself). If native pages still use the old control, keep both until they are converted.

## Drawn-from-scratch control

```csharp
public sealed class Meter : SkUiView
{
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value), typeof(double), typeof(Meter), 0.0,
        propertyChanged: (view, _, _) => ((Meter)view).InvalidatePaint());

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => new(120, 12);

    protected override void OnPaintContent(SKCanvas canvas)
    {
        using var paint = new SKPaint { Color = SKColors.SteelBlue, IsAntialias = true };
        canvas.DrawRect(0, 0, (float)(Width * Math.Clamp(Value, 0, 1)), (float)Height, paint);
    }
}
```

Coordinates are DIPs in the view's own space. Call `InvalidatePaint()` when a property changes the drawing and `InvalidateMeasure()` when it changes the size. Input: the gesture events (`Tapped`, `PanUpdated`, …) or a recognizer in `Gestures`.

## Platform handler tweaks

Custom handlers and mapper changes registered for MAUI types (`Label`, `Button`, `Entry`, `ScrollView`, …) do not affect drawn controls. For each one that applied to converted controls, find what it changed (removed button padding or shadow, auto-fit text, disabled scroll bars, …) and set the drawn equivalent properties (`Padding`, `MaxLines` / `LineBreakMode`, `VerticalScrollBarVisibility`, …). Handlers for native islands (`Entry`, `Editor`, `WebView`) still apply inside `SkUiMauiContentView`.
