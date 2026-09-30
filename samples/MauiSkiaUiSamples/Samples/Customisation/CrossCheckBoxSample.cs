using MauiSkiaUi;
using MauiSkiaUi.Core;
using SkiaSharp;

namespace MauiSkiaUiSamples.Samples.Customisation;

/// <summary>
/// HOWTO: draw a check box's mark as an X whose arms spread out from a dot in the centre, with a custom look.
/// <para>
/// A look (<see cref="SkUiLook"/>) draws every stock control. For state changes, controls call the look's painter on
/// every frame of the transition and pass how far it is in the paint struct (<see cref="SkUiCheckBoxPaint.Visual"/>).
/// So a custom look only describes the drawing as a function of that progress; timing, reversing mid-way, reduce motion
/// and the Core layer come with it. See <see cref="CrossCheckBoxLook"/> for the drawing and docs/design/ControlLook.md
/// ("State-change transitions") for the whole API.
/// </para>
/// </summary>
public sealed class CrossCheckBoxSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Customisation,
        Title: "Check box with a cross",
        Summary: "A custom look replaces the check box's tick with an X. When the box is checked, the arms spread out from " +
            "a dot in the centre; when it is unchecked, they shrink back.",
        HowTo:
        [
            "Subclass `DefaultSkUiLook` and override `DrawCheckBoxCore(SKCanvas, SkUiCheckBoxPaint)`.",
            "Draw the box with `DrawRoundedBox`, blending its colors with `box.Visual.Blend(unchecked, checked, indeterminate)`.",
            "Draw the X with arms as long as `box.Visual.Weight(SkUiCheckState.Checked)` times their full length: 0 when " +
                "unchecked, 1 when checked, in between while the transition runs.",
            "Optionally override `GetTransitionCore` to set the check box transition's duration and easing.",
            "Make it the app's look once at startup: `SkUiLook.Current = new CrossCheckBoxLook();`."
        ],
        ThingsToKnow:
        [
            "The painter runs for every frame of a transition. Reversing mid-way, reduce motion and the Core " +
                "`SkUiCoreCheckBox` work without extra code.",
            "Overriding `DrawCheckBoxCore` replaces the whole check box: draw the box and, for three-state check boxes, " +
                "the indeterminate dash too (`Weight(SkUiCheckState.Indeterminate)`).",
            "Painters run on the UI thread for every frame: reuse paints and paths instead of allocating them.",
            "Round stroke caps turn the nearly zero-length arms at the start into a dot.",
            "Without subclassing: set `CheckBoxPainter` and `TransitionProvider` on a look instance.",
            "A look is app-wide. This page makes its look current only while it is shown."
        ]);

    private readonly CrossCheckBoxLook _look = new();

    public CrossCheckBoxSample() : base(Info) => SampleContent = Build();

    /// <inheritdoc />
    protected override SkUiLook Look => _look;

    private static View Build()
    {
        var boxes = new List<SkUiCheckBox>();
        SkUiHorizontalStackLayout Row(SkUiCheckBox box, string text)
        {
            boxes.Add(box);
            return new SkUiHorizontalStackLayout
            {
                Spacing = 12,
                Children = { box, new SkUiLabel { Text = text, FontSize = 16, VerticalOptions = LayoutOptions.Center } }
            };
        }

        // The same look draws the Core check box (a lightweight node inside SkUiCoreHost).
        var core = new SkUiCoreCheckBox();
        var coreRow = new SkUiCoreHorizontalStackLayout().SetSpacing(12);
        coreRow.Add(core);
        coreRow.Add(new SkUiCoreLabel().SetText("Core check box").SetFontSize(16).SetVerticalTextAlignment(TextAlignment.Center).SetHeight(24));

        var surface = new SkUiContentView
        {
            Background = Colors.White,
            Padding = new Thickness(16),
            Content = new SkUiVerticalStackLayout
            {
                Spacing = 16,
                Children =
                {
                    Row(new SkUiCheckBox(), "Tap to check"),
                    Row(new SkUiCheckBox { IsChecked = true }, "Checked at start (no animation before it is shown)"),
                    Row(new SkUiCheckBox { IsThreeState = true }, "Three states: X, then a dash"),
                    new SkUiCoreHost { HeightRequest = 24 }.SetContent(coreRow)
                }
            }
        };

        // Several transitions at once, each reversible mid-way (tap twice quickly).
        var toggleAll = new Button { Text = "Toggle all", HorizontalOptions = LayoutOptions.Start, BackgroundColor = SampleColors.Accent, TextColor = Colors.White };
        toggleAll.Clicked += (_, _) =>
        {
            foreach (var box in boxes)
                box.IsChecked = !box.IsChecked;
            core.SetIsChecked(!core.IsChecked);
        };
        return new VerticalStackLayout { Spacing = 12, Children = { surface, toggleAll } };
    }
}

/// <summary>
/// The look: <see cref="DefaultSkUiLook"/> with the check box drawn as an X.
/// <list type="bullet">
/// <item><see cref="SkUiCheckBoxPaint.Visual"/> describes the transition: <c>State</c> (target), <c>From</c>,
/// <c>Progress</c> (eased), <c>Pressed</c>. <c>Weight(state)</c> says how much of a state to show (the three weights
/// add up to 1); <c>Blend(unchecked, checked, indeterminate)</c> mixes one value per state (numbers or colors).</item>
/// <item>Colors in the paint struct are already resolved (accent, unchecked background and border, disabled
/// dimming); the look only picks and blends them.</item>
/// <item>The drawing is a square of <see cref="SkUiCheckBoxPaint.Size"/> at the origin; the control places it
/// (right-to-left layouts included).</item>
/// </list>
/// </summary>
public sealed class CrossCheckBoxLook : DefaultSkUiLook
{
    // Reused on every frame: painters run for each frame of a transition, so they should not allocate.
    private readonly SKPaint _mark = new() { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, IsAntialias = true };

    /// <inheritdoc />
    protected override void DrawCheckBoxCore(SKCanvas canvas, SkUiCheckBoxPaint box)
    {
        var size = box.Size;
        var visual = box.Visual;

        // The box: unchecked background and border blend into the accent while checking.
        DrawRoundedBox(canvas, new SKRect(0, 0, size, size), size * 0.2f,
            visual.Blend(box.Background, box.Color, box.Color), visual.Blend(box.Border, box.Color, box.Color), 1.5f);

        _mark.StrokeWidth = size * 0.12f;
        var center = size / 2;

        // The X: 0 draws nothing, just above 0 a dot (round caps), 1 the full cross. With SpringOut (below) the arms
        // spread slightly past their length, then settle.
        var cross = visual.Weight(SkUiCheckState.Checked);
        if (cross > 0)
        {
            var arm = size * 0.26f * cross;
            canvas.DrawLine(center - arm, center - arm, center + arm, center + arm, _mark);
            canvas.DrawLine(center - arm, center + arm, center + arm, center - arm, _mark);
        }

        // Three-state check boxes: the indeterminate dash grows from the centre the same way.
        var dash = visual.Weight(SkUiCheckState.Indeterminate);
        if (dash > 0)
        {
            var half = size * 0.24f * dash;
            canvas.DrawLine(center - half, center, center + half, center, _mark);
        }
    }

    /// <inheritdoc />
    protected override SkUiTransition GetTransitionCore(SkUiTransitionKind kind) =>
        kind == SkUiTransitionKind.CheckBox
            ? SkUiTransition.FromMilliseconds(320, Easing.SpringOut)
            : base.GetTransitionCore(kind);
}
