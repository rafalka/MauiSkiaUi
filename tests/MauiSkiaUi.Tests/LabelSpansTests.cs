using System.ComponentModel;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Label spans (Phase P5 in ImplementationPlan.md): MAUI's <c>FormattedText</c> / <c>Span</c> on <see cref="SkUiLabel"/>,
/// <see cref="SkUiCoreSpan"/> on <see cref="SkUiCoreLabel"/>, both through the formatted-text engine
/// (<see cref="SkUiRichTextLayout"/>): per-span styles wrapping as one paragraph, the label's values for what a span does
/// not set, span taps (MAUI <c>TapGestureRecognizer</c>s, Core <c>Tapped</c>) hit-tested on the shaped runs, also in RTL.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class LabelSpansTests
{
    private static readonly SKTypeface Mono = SKTypeface.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "RobotoMono-Regular.ttf"));

    private static SKFont Font(double size = 16) => new(Mono, (float)size) { LinearMetrics = true };

    /// <summary>Roboto Mono advance (every glyph) at <paramref name="size"/>.</summary>
    private static float Advance(double size = 16)
    {
        using var font = Font(size);
        return font.MeasureText("0");
    }

    private static float Spacing(double size = 16)
    {
        using var font = Font(size);
        return font.Spacing;
    }

    private static SkUiLabel Label(params Span[] spans)
    {
        var formatted = new FormattedString();
        foreach (var span in spans)
            formatted.Spans.Add(span);
        return new SkUiLabel { FontFamily = SkUiTestHelpers.BundledFontFamily, FormattedText = formatted };
    }

    private static SkUiCoreLabel Core(params SkUiCoreSpan[] spans) =>
        new SkUiCoreLabel().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetSpans(spans);

    private static Size Measure(SkUiLabel label, double width) => ((IView)label).Measure(width, double.PositiveInfinity);

    private static void Tap(SkUiLabel label, double x, double y = 8)
    {
        label.Touch(new(1, SkUiTouchAction.Pressed, new Point(x, y)));
        label.Touch(new(1, SkUiTouchAction.Released, new Point(x, y)));
    }

    private static void Tap(SkUiCoreLabel label, double x, double y = 8)
    {
        label.Touch(new(1, SkUiTouchAction.Pressed, new Point(x, y)));
        label.Touch(new(1, SkUiTouchAction.Released, new Point(x, y)));
    }

    private static Span Tappable(string text, Action<object?> tapped, object? parameter = null)
    {
        var span = new Span { Text = text };
        span.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command<object?>(tapped), CommandParameter = parameter ?? text });
        return span;
    }

    private static SKBitmap Render(SkUiLabel label, int width, int height)
    {
        SkUiTestHelpers.Arrange(label, width, height);
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        label.Paint(canvas);
        return bitmap;
    }

    [Fact]
    public void SpansInOneStyleMeasureAndWrapAsTheirTextOnBothLayers()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var spans = Label(new Span { Text = "Hello " }, new Span { Text = "world" });
        var plain = new SkUiLabel { Text = "Hello world", FontFamily = SkUiTestHelpers.BundledFontFamily };
        foreach (var width in new[] { double.PositiveInfinity, Advance() * 8 })
        {
            var expected = Measure(plain, width);
            var actual = Measure(spans, width);
            Assert.Equal(expected.Width, actual.Width, 0.5);
            Assert.Equal(expected.Height, actual.Height, 0.5);
            var core = Core(new SkUiCoreSpan("Hello "), new SkUiCoreSpan("world")).Measure(width, double.PositiveInfinity);
            Assert.Equal(actual, core);
        }
        Assert.Equal(Spacing() * 2, Measure(spans, Advance() * 8).Height, 0.5); // "Hello" / "world"
    }

    [Fact]
    public void MixedStylesWrapInsideOneParagraphAndLinesAreAsTallAsTheirTallestSpan()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label(new Span { Text = "aaaa aaaa " }, new Span { Text = "bb bb", FontSize = 32 });
        var natural = Measure(label, double.PositiveInfinity);
        Assert.Equal(Advance() * 10 + Advance(32) * 5, natural.Width, 0.5);
        Assert.Equal(Spacing(32), natural.Height, 0.5);

        // Breaks between the spans: "aaaa aaaa" / "bb bb".
        var between = Measure(label, Advance() * 12);
        Assert.Equal(Spacing() + Spacing(32), between.Height, 0.5);
        Assert.Equal(Advance(32) * 5, between.Width, 0.5);

        // Inside a span and across the boundary: "aaaa" / "aaaa" / "bb" / "bb" (the second line keeps its trailing space
        // out, the 32-DIP word does not fit after it).
        Assert.Equal(Spacing() * 2 + Spacing(32) * 2, Measure(label, Advance() * 6).Height, 0.5);

        var core = Core(new SkUiCoreSpan("aaaa aaaa "), new SkUiCoreSpan("bb bb").SetFontSize(32));
        Assert.Equal(between, core.Measure(Advance() * 12, double.PositiveInfinity));
    }

    [Fact]
    public void SpansUseTheLabelsValuesForWhatTheyDoNotSet()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var own = new Span { Text = "cd", FontSize = 32 };
        var label = Label(new Span { Text = "ab" }, own);
        Assert.Equal(Advance() * 2 + Advance(32) * 2, Measure(label, double.PositiveInfinity).Width, 0.5);
        label.FontSize = 24;
        Assert.Equal(Advance(24) * 2 + Advance(32) * 2, Measure(label, double.PositiveInfinity).Width, 0.5);

        label.CharacterSpacing = 3;
        own.CharacterSpacing = 0; // set: the span keeps it
        Assert.Equal(Advance(24) * 2 + 6 + Advance(32) * 2, Measure(label, double.PositiveInfinity).Width, 0.5);

        label.LineHeight = 2;
        own.LineHeight = 1;
        // The 24-DIP span's line box (twice its spacing) is taller than the 32-DIP span's own.
        Assert.Equal(Spacing(24) * 2, Measure(label, double.PositiveInfinity).Height, 0.5);

        var core = Core(new SkUiCoreSpan("ab"), new SkUiCoreSpan("cd").SetFontSize(32).SetCharacterSpacing(0).SetLineHeight(1))
            .SetFontSize(24).SetCharacterSpacing(3).SetLineHeight(2);
        Assert.Equal(Measure(label, double.PositiveInfinity), core.Measure(double.PositiveInfinity, double.PositiveInfinity));
    }

    [Fact]
    public void TheLabelsTextTransformRedrawsTheSpansThatInheritIt()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label(new Span { Text = "abc" }, new Span { Text = "def", TextTransform = TextTransform.None });
        using var before = Render(label, 120, 30);
        label.TextTransform = TextTransform.Uppercase;
        using var after = Render(label, 120, 30);
        var splitX = (int)(Advance() * 3);
        bool Changed(int from, int to) =>
            Enumerable.Range(from, to - from).Any(x => Enumerable.Range(0, 30).Any(y => before.GetPixel(x, y) != after.GetPixel(x, y)));
        Assert.True(Changed(0, splitX - 1)); // "ABC"
        Assert.False(Changed(splitX + 1, 120)); // "def" keeps its own transform

        var core = Core(new SkUiCoreSpan("abc"));
        SKBitmap Paint()
        {
            core.Measure(120, 30);
            core.Arrange(new Rect(0, 0, 120, 30));
            var bitmap = new SKBitmap(120, 30);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);
            core.Paint(canvas);
            return bitmap;
        }
        using var coreBefore = Paint();
        core.SetTextTransform(TextTransform.Uppercase);
        using var coreAfter = Paint();
        Assert.NotEqual(coreBefore.Pixels, coreAfter.Pixels);
    }

    [Fact]
    public void TextAndFormattedTextReplaceEachOtherAsOnMaui()
    {
        var label = Label(new Span { Text = "spans" });
        Assert.Equal(string.Empty, label.Text);
        Assert.Same(label, label.FormattedText!.Parent);
        var formatted = label.FormattedText;
        label.Text = "plain";
        Assert.Null(label.FormattedText);
        Assert.Null(formatted.Parent);
        label.FormattedText = new FormattedString { Spans = { new Span { Text = "x" } } };
        Assert.Equal(string.Empty, label.Text);

        var core = Core(new SkUiCoreSpan("spans"));
        Assert.Equal(string.Empty, core.Text);
        core.SetText("plain");
        Assert.Empty(core.Spans);
        core.AddSpan(new SkUiCoreSpan("a")).AddSpan(new SkUiCoreSpan("b"));
        Assert.Equal(2, core.Spans.Count);
        Assert.Equal(string.Empty, core.Text);
    }

    [Fact]
    public void ColorChangesRepaintAndStyleChangesRelayout()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var span = new Span { Text = "abc" };
        var label = Label(span, new Span { Text = "def" });
        var layouts = () => ((SkUiRichTextLayout)typeof(SkUiLabel)
            .GetField("_richLayout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(label)!).LayoutCount;
        void Draw()
        {
            using var bitmap = Render(label, 200, 40);
        }
        Draw();
        var before = layouts();
        span.TextColor = Colors.Red;
        span.BackgroundColor = Colors.Yellow;
        span.TextDecorations = TextDecorations.Underline;
        label.TextColor = Colors.Green;
        Draw();
        Assert.Equal(before, layouts());
        span.FontSize = 30;
        Draw();
        Assert.Equal(before + 1, layouts());

        // The engine keeps its lines for paint-only differences.
        var layout = new SkUiRichTextLayout();
        SkUiRichText Text(SKColor color, double size) =>
            new SkUiRichText.Builder().Add("ab", new SkUiTextSpanStyle(Mono, size), new SkUiTextSpanPaint(color)).Build();
        var style = new SkUiTextStyle(Mono, 16);
        layout.Measure(Text(SKColors.Red, 16), style, default, 100);
        layout.Measure(Text(SKColors.Blue, 16), style, default, 100);
        Assert.Equal(1, layout.LayoutCount);
        layout.Measure(Text(SKColors.Blue, 20), style, default, 100);
        Assert.Equal(2, layout.LayoutCount);

        var coreSpan = new SkUiCoreSpan("abc");
        var core = Core(coreSpan);
        var notified = new List<string?>();
        coreSpan.PropertyChanged += (_, e) => notified.Add(e.PropertyName);
        coreSpan.SetTextColor(Colors.Red).SetFontSize(20);
        Assert.Equal([nameof(SkUiCoreSpan.TextColor), nameof(SkUiCoreSpan.FontSize)], notified);
        Assert.Throws<ArgumentOutOfRangeException>(() => coreSpan.SetFontSize(0));
        Assert.Same(core, coreSpan.Owner);
    }

    [Fact]
    public void SpanTapsHitTheTappedSpanAndLeaveTheRestToTheLabel()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var commands = new List<object?>();
        var events = new List<(object? Sender, object? Parameter, Point? Position)>();
        var second = Tappable("BBBB", commands.Add);
        SkUiLabel? label = null;
        ((TapGestureRecognizer)second.GestureRecognizers[0]).Tapped += (sender, e) => events.Add((sender, e.Parameter, e.GetPosition(label)));
        label = Label(Tappable("AAAA", commands.Add), new Span { Text = " " }, second);
        var labelTaps = 0;
        label.Tapped += (_, _) => labelTaps++;
        SkUiTestHelpers.Arrange(label, 300, 40);

        Tap(label, Advance() * 2);
        Assert.Equal(["AAAA"], commands);
        Tap(label, Advance() * 7);
        Assert.Equal(["AAAA", "BBBB"], commands);
        var (sender, parameter, position) = Assert.Single(events);
        Assert.Same(label, sender);
        Assert.Equal("BBBB", parameter);
        Assert.Equal(new Point(Advance() * 7, 8), position);
        Assert.Equal(0, labelTaps);

        Tap(label, Advance() * 4.5); // the space span has no recognizer
        Tap(label, 250); // beside the text
        Assert.Equal(2, commands.Count);
        Assert.Equal(2, labelTaps);

        // Pressed on a span, released beside it (within the touch slop): no tap.
        label.Touch(new(1, SkUiTouchAction.Pressed, new Point(Advance() * 3.6, 8)));
        label.Touch(new(1, SkUiTouchAction.Released, new Point(Advance() * 4.5, 8)));
        Assert.Equal(2, commands.Count);
        Assert.Equal(2, labelTaps);

        Assert.Same(second, label.SpanAt(new Point(Advance() * 6, 8)));
        Assert.Null(label.SpanAt(new Point(250, 8)));
    }

    [Fact]
    public void SpanTapsFollowTheVisualOrderOfRtlText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var taps = new List<object?>();
        var label = Label(Tappable("אבג", taps.Add), Tappable("דהו", taps.Add));
        label.FlowDirection = FlowDirection.RightToLeft;
        SkUiTestHelpers.Arrange(label, 300, 40);
        var width = Measure(label, double.PositiveInfinity).Width;
        Assert.True(width > 0);
        // RTL: the line hugs the right edge and the first span is rightmost.
        Tap(label, 300 - 2);
        Tap(label, 300 - width + 2);
        Tap(label, 300 - width - 10);
        Assert.Equal(["אבג", "דהו"], taps);

        // Mixed: a Latin span inside an RTL paragraph keeps its own order and is still hit where it is drawn.
        var latin = new List<object?>();
        var mixed = Label(new Span { Text = "אבג " }, Tappable("abc", latin.Add), new Span { Text = " דהו" });
        mixed.FlowDirection = FlowDirection.RightToLeft;
        SkUiTestHelpers.Arrange(mixed, 300, 40);
        var span = Enumerable.Range(0, 300).Select(x => mixed.SpanAt(new Point(x + 0.5, 8))).ToArray();
        var latinXs = Enumerable.Range(0, 300).Where(x => ReferenceEquals(span[x], mixed.FormattedText!.Spans[1])).ToArray();
        Assert.NotEmpty(latinXs);
        Assert.Equal(latinXs.Length, latinXs[^1] - latinXs[0] + 1); // one contiguous run
        Tap(mixed, (latinXs[0] + latinXs[^1]) / 2.0);
        Assert.Equal(["abc"], latin);
    }

    [Fact]
    public void TruncationKeepsTheSpansStyles()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label(new Span { Text = "aaaa " }, new Span { Text = "bbbbbbbbbb", FontSize = 32 });
        label.LineBreakMode = LineBreakMode.TailTruncation;
        var truncated = Measure(label, Advance() * 14);
        Assert.Equal(Spacing(32), truncated.Height, 0.5); // one line, the 32-DIP span kept
        Assert.True(truncated.Width <= Advance() * 14 + 0.5);
        Assert.True(truncated.Width > Advance() * 5 + Advance(32) * 3); // "aaaa " + a 32-DIP character + its "..."

        label.LineBreakMode = LineBreakMode.WordWrap;
        label.MaxLines = 1;
        Assert.Equal(Spacing(), Measure(label, Advance() * 14).Height, 0.5); // "aaaa" alone; the rest is dropped

        label.LineBreakMode = LineBreakMode.TailTruncation;
        label.MaxLines = 2;
        var lines = Measure(label, Advance() * 14);
        Assert.Equal(Spacing() + Spacing(32), lines.Height, 0.5);
        Assert.True(lines.Width <= Advance() * 14 + 0.5);

        foreach (var mode in new[] { LineBreakMode.HeadTruncation, LineBreakMode.MiddleTruncation })
        {
            label.MaxLines = -1;
            label.LineBreakMode = mode;
            var size = Measure(label, Advance() * 14);
            Assert.Equal(Spacing(32), size.Height, 0.5);
            Assert.True(size.Width <= Advance() * 14 + 0.5, $"{mode} overflows");
        }
    }

    [Fact]
    public void EachSpanPaintsItsColorsBackgroundAndDecorations()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var second = new Span { Text = "BBBB", TextColor = Colors.Blue, BackgroundColor = Colors.Yellow };
        var label = Label(new Span { Text = "AAAA", TextColor = Colors.Red }, second);
        var splitX = (int)(Advance() * 4);
        using (var bitmap = Render(label, 200, 30))
        {
            bool Any(int from, int to, Func<SKColor, bool> match) =>
                Enumerable.Range(from, to - from).Any(x => Enumerable.Range(0, 30).Any(y => match(bitmap.GetPixel(x, y))));
            static bool Red(SKColor c) => c.Red > 180 && c.Green < 90 && c.Blue < 90;
            static bool Blue(SKColor c) => c.Blue > 180 && c.Red < 90 && c.Green < 90;
            static bool Yellow(SKColor c) => c.Red > 230 && c.Green > 230 && c.Blue < 40;
            Assert.True(Any(0, splitX - 1, Red));
            Assert.False(Any(0, splitX - 1, Blue) || Any(0, splitX - 1, Yellow));
            Assert.True(Any(splitX + 1, splitX * 2, Blue));
            Assert.True(Any(splitX + 1, splitX * 2, Yellow));
            Assert.False(Any(splitX * 2 + 1, 200, Yellow)); // only behind the span
        }

        // An underline on the second span changes only pixels under it.
        using var before = Render(label, 200, 30);
        second.TextDecorations = TextDecorations.Underline;
        using var after = Render(label, 200, 30);
        var changed = Enumerable.Range(0, 200).Where(x => Enumerable.Range(0, 30).Any(y => before.GetPixel(x, y) != after.GetPixel(x, y))).ToArray();
        Assert.NotEmpty(changed);
        Assert.All(changed, x => Assert.InRange(x, splitX - 1, splitX * 2 + 1));
    }

    [Fact]
    public void CoreSpansTapAndBelongToOneLabel()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tapped = new List<(object? Sender, Point Position)>();
        var link = new SkUiCoreSpan("BBBB").SetTextColor(Colors.Blue).SetTextDecorations(TextDecorations.Underline);
        link.Tapped += (sender, e) => tapped.Add((sender, e.Position));
        var label = Core(new SkUiCoreSpan("AAAA "), link);
        var labelTaps = 0;
        label.Tapped += (_, _) => labelTaps++;
        label.Measure(300, 40);
        label.Arrange(new Rect(0, 0, 300, 40));

        Tap(label, Advance() * 7);
        Assert.Equal((link, new Point(Advance() * 7, 8)), Assert.Single(tapped));
        Tap(label, Advance() * 2);
        Assert.Single(tapped);
        Assert.Equal(1, labelTaps);
        Assert.Same(link, label.SpanAt(new Point(Advance() * 6, 8)));

        Assert.Throws<InvalidOperationException>(() => new SkUiCoreLabel().SetSpans(link));
        var lone = new SkUiCoreSpan("x");
        Assert.Throws<InvalidOperationException>(() => new SkUiCoreLabel().SetSpans(lone, lone));
        label.SetSpans(null);
        Assert.Null(link.Owner);
        new SkUiCoreLabel().SetSpans(link); // free again
    }

    /// <summary>The MAUI docs' formatted-text samples, with only the namespace prefix changed (plus the test font).</summary>
    [Fact]
    public void MauiDocSamplesLoadBindAndTap()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        using var font = SkUiTestHelpers.UseBundledFont();
        var xaml = $$"""
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiVerticalStackLayout>
                <sk:SkUiLabel LineBreakMode="WordWrap" FontFamily="{{SkUiTestHelpers.BundledFontFamily}}">
                  <sk:SkUiLabel.FormattedText>
                    <FormattedString>
                      <Span Text="Red Bold, " TextColor="Red" FontAttributes="Bold" />
                      <Span Text="default, " FontSize="14">
                        <Span.GestureRecognizers>
                          <TapGestureRecognizer Command="{Binding TapCommand}" />
                        </Span.GestureRecognizers>
                      </Span>
                      <Span Text="italic small." FontAttributes="Italic" FontSize="12" />
                    </FormattedString>
                  </sk:SkUiLabel.FormattedText>
                </sk:SkUiLabel>
                <sk:SkUiLabel FontFamily="{{SkUiTestHelpers.BundledFontFamily}}">
                  <sk:SkUiLabel.FormattedText>
                    <FormattedString>
                      <Span Text="Alternatively, click " />
                      <Span Text="here" TextColor="Blue" TextDecorations="Underline">
                        <Span.GestureRecognizers>
                          <TapGestureRecognizer Command="{Binding TapCommand}" CommandParameter="https://learn.microsoft.com/dotnet/maui/" />
                        </Span.GestureRecognizers>
                      </Span>
                      <Span Text=" to view .NET MAUI documentation." />
                    </FormattedString>
                  </sk:SkUiLabel.FormattedText>
                </sk:SkUiLabel>
                <sk:SkUiLabel LineBreakMode="WordWrap" FontFamily="{{SkUiTestHelpers.BundledFontFamily}}">
                  <sk:SkUiLabel.FormattedText>
                    <FormattedString>
                      <Span Text="Lorem ipsum dolor sit amet, consectetur adipiscing elit. " LineHeight="1.8" />
                      <Span Text="Nullam feugiat sodales elit, et maximus nibh vulputate id." LineHeight="1.8" />
                    </FormattedString>
                  </sk:SkUiLabel.FormattedText>
                </sk:SkUiLabel>
                <sk:SkUiLabel FontFamily="{{SkUiTestHelpers.BundledFontFamily}}">
                  <sk:SkUiLabel.FormattedText>
                    <FormattedString>
                      <Span Text="Hello, " />
                      <Span Text="{Binding Name}" FontAttributes="Bold" />
                    </FormattedString>
                  </sk:SkUiLabel.FormattedText>
                </sk:SkUiLabel>
              </sk:SkUiVerticalStackLayout>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var labels = ((SkUiVerticalStackLayout)root.Content).Children.Cast<SkUiLabel>().ToArray();
        var model = new DocModel { Name = "Ann" };
        root.BindingContext = model;

        Assert.Equal(3, labels[0].FormattedText!.Spans.Count);
        Assert.Equal(Advance() * 10 + Advance(14) * 9 + Advance(12) * 13, Measure(labels[0], double.PositiveInfinity).Width, 0.5);
        SkUiTestHelpers.Arrange(labels[0], 600, 40);
        Tap(labels[0], Advance() * 10 + Advance(14) * 3);
        Assert.Equal([null], model.Taps);

        SkUiTestHelpers.Arrange(labels[1], 600, 40);
        Tap(labels[1], Advance() * 23);
        Assert.Equal([null, "https://learn.microsoft.com/dotnet/maui/"], model.Taps);
        Tap(labels[1], Advance() * 10);
        Assert.Equal(2, model.Taps.Count);

        var lineHeight = new SkUiLabel { Text = labels[2].FormattedText!.ToString(), LineBreakMode = LineBreakMode.WordWrap, FontFamily = SkUiTestHelpers.BundledFontFamily };
        Assert.Equal(Measure(lineHeight, 300).Height * 1.8, Measure(labels[2], 300).Height, 0.5);

        Assert.Equal(Advance() * 10, Measure(labels[3], double.PositiveInfinity).Width, 0.5);
        model.Name = "Annabel";
        Assert.Equal(Advance() * 14, Measure(labels[3], double.PositiveInfinity).Width, 0.5);
    }

    private sealed class DocModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;

        public DocModel() => TapCommand = new Command<object?>(Taps.Add);

        public event PropertyChangedEventHandler? PropertyChanged;

        public List<object?> Taps { get; } = [];

        public Command<object?> TapCommand { get; }

        public string Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
    }
}
