using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.GettingStarted;

/// <summary>
/// HOWTO: use the app's own fonts in drawn text. Fonts added with MAUI's <c>ConfigureFonts</c>
/// (<c>fonts.AddFont("Lobster-Regular.ttf", "Lobster")</c>, see MauiProgram.cs) work in drawn controls by the same alias.
/// <para>
/// Drawn text is rendered by Skia, not by the platform's text stack, so SkiaUi resolves the alias itself: it asks MAUI's
/// font registrar and loads the font it returns (a file, an Android asset, a CoreText font on Apple platforms, an
/// <c>ms-appx:</c> file on Windows). Each row below shows a native MAUI label and a drawn <see cref="SkUiLabel"/> with the
/// same font: they should look alike on every platform.
/// </para>
/// </summary>
public sealed class AppFontsSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.GettingStarted,
        Title: "App fonts in drawn text",
        Summary: "Fonts registered with MAUI's `ConfigureFonts` work in drawn controls by their alias. Each row shows a " +
            "native label above a drawn one with the same font.",
        HowTo:
        [
            "Add the font file to `Resources/Fonts` (a `MauiFont` item).",
            "Register it at startup: `fonts.AddFont(\"Lobster-Regular.ttf\", \"Lobster\")` in `ConfigureFonts`.",
            "Use the alias on drawn controls: `new SkUiLabel { FontFamily = \"Lobster\" }` (Core: `SetFontFamily(\"Lobster\")`)."
        ],
        ThingsToKnow:
        [
            "No SkiaUi-specific registration is needed. `SkUiFonts.Register(alias, openStream)` registers a font for drawn " +
                "text only, or overrides one.",
            "System fonts work by their family name, e.g. `FontFamily = \"Menlo\"`.",
            "Drawn text falls back per character to a system font that has the glyph (emoji, other scripts).",
            "If a drawn row looks different from the native one above it, the alias did not resolve and the default " +
                "font is used."
        ]);

    public AppFontsSample() : base(Info) => SampleContent = Build();

    private static View Build()
    {
        var rows = new VerticalStackLayout { Spacing = 18 };
        foreach (var (alias, text) in new[]
                 {
                     ("Lobster", "Lobster — a display font"),
                     (SampleFonts.Mono, "RobotoMono: 0O 1l {}[]"),
                     (SampleFonts.Semibold, "Open Sans Semibold"),
                 })
        {
            rows.Add(new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label { Text = $"Native · {alias}", FontSize = 12, TextColor = SampleColors.Caption },
                    new Label { Text = text, FontFamily = alias, FontSize = 22, TextColor = SampleColors.Ink },
                    new Label { Text = $"Drawn · {alias}", FontSize = 12, TextColor = SampleColors.Caption, Margin = new Thickness(0, 6, 0, 0) },
                    // A drawn label: SkiaUi resolves the ConfigureFonts alias itself.
                    new SkUiContentView
                    {
                        Content = new SkUiLabel { Text = text, FontFamily = alias, FontSize = 22, TextColor = SampleColors.Ink }
                    }
                }
            });
        }
        return new Border
        {
            BackgroundColor = SampleColors.Surface,
            Stroke = SampleColors.Border,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Padding = new Thickness(16),
            Content = rows
        };
    }
}
