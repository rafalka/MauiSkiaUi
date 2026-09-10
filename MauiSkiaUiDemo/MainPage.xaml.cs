using MauiSkiaUi;

namespace MauiSkiaUiDemo;

public partial class MainPage : ContentPage
{
	private int _tapCount;
	private IDisposable? _animation;

	public MainPage()
	{
		InitializeComponent();
		Scene.Loaded += OnSceneLoaded;
		Scene.AnimationClock.RunningChanged += OnAnimationRunningChanged;
	}

	protected override void OnDisappearing()
	{
		_animation?.Dispose();
		_animation = null;
		base.OnDisappearing();
	}

	private void OnSceneLoaded(object? sender, EventArgs args) => StartAnimation();

	private void OnAnimateClicked(object? sender, EventArgs args) => StartAnimation();

	private void StartAnimation()
	{
		_animation?.Dispose();
		_animation = Scene.AnimationClock.Start(progress =>
		{
			MovingEllipse.TranslationY = -48 * Math.Sin(progress * Math.PI * 4);
			MovingEllipse.Scale = 1 + 0.12 * Math.Sin(progress * Math.PI * 4);
			TapBox.Rotation = 8 * Math.Sin(progress * Math.PI * 4);
		}, TimeSpan.FromSeconds(4));
	}

	private void OnAnimationRunningChanged(object? sender, EventArgs args)
	{
		AnimationStatus.Text = Scene.AnimationClock.IsRunning ? "Animating" : "Idle";
		AnimateButton.IsEnabled = !Scene.AnimationClock.IsRunning;
	}

	private void OnPrimitiveTapped(object? sender, SkUiTappedEventArgs args)
	{
		_tapCount++;
		TapStatus.Text = $"Taps: {_tapCount}";
		if (sender is SkUiShape shape)
			shape.Color = _tapCount % 2 == 0 ? DemoColors.TapAlternate : DemoColors.Accent;
	}
}
