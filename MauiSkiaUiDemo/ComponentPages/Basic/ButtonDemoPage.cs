using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiButton"/>.</summary>
public sealed class ButtonDemoPage : ComponentDemoPage
{
    public ButtonDemoPage() : base(nameof(SkUiButton), new SkUiButton(), new Button())
    {
        var skia = (SkUiButton)SkiaControl;
        var native = (Button)NativeControl!;
        var skiaClicks = 0;
        var nativeClicks = 0;
        var canExecute = true;
        var skiaPress = "";
        var nativePress = "";
        void Counts() => Feedback($"Clicks: {skiaClicks}{skiaPress}", $"Clicks: {nativeClicks}{nativePress}");
        skia.Pressed += (_, _) => { skiaPress = " · pressed"; Counts(); };
        skia.Released += (_, _) => { skiaPress = " · released"; Counts(); };
        native.Pressed += (_, _) => { nativePress = " · pressed"; Counts(); };
        native.Released += (_, _) => { nativePress = " · released"; Counts(); };
        var skiaCommand = new Command(() => { skiaClicks++; Counts(); }, () => canExecute);
        var nativeCommand = new Command(() => { nativeClicks++; Counts(); }, () => canExecute);
        skia.Command = skiaCommand;
        native.Command = nativeCommand;
        native.TextColor = Colors.White;
        skia.TextColor = Colors.White;
        Text(nameof(SkUiButton.Text), "Add observation", value => { skia.Text = value; native.Text = value; }, () => skia.Text, () => native.Text);
        Number(nameof(SkUiButton.FontSize), 10, 30, 16, value => { skia.FontSize = value; native.FontSize = value; }, () => skia.FontSize, () => native.FontSize);
        Number(nameof(SkUiButton.CornerRadius), 0, 30, 6, value => { skia.CornerRadius = (int)Math.Round(value); native.CornerRadius = (int)Math.Round(value); }, () => skia.CornerRadius, () => native.CornerRadius, whole: true);
        Number(nameof(SkUiButton.BorderWidth), 0, 8, 1, value => { skia.BorderWidth = value; native.BorderWidth = value; }, () => skia.BorderWidth, () => native.BorderWidth);
        ColorEditor(nameof(SkUiButton.FillColor), Accent, value => { skia.FillColor = value; native.Background = value; }, () => skia.FillColor, () => ((SolidColorBrush)native.Background).Color);
        ColorEditor(nameof(SkUiButton.BorderColor), Ink, value => { skia.BorderColor = value; native.BorderColor = value; }, () => skia.BorderColor, () => native.BorderColor);
        Toggle(nameof(Command.CanExecute), true, value => { canExecute = value; skiaCommand.ChangeCanExecute(); nativeCommand.ChangeCanExecute(); }, () => skia.Command.CanExecute(null), () => native.Command.CanExecute(null));
        OnReset(() => { skiaClicks = nativeClicks = 0; skiaPress = nativePress = ""; Counts(); });
    }
}
