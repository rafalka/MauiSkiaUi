using MauiSkiaUi;

namespace MauiSkiaUiDemo;

public partial class MainPage : ContentPage
{
	private int tapCount;
	private IDisposable? animation;

	public MainPage()
	{
		InitializeComponent();
		Scene.Loaded += OnSceneLoaded;
		Scene.AnimationClock.RunningChanged += OnAnimationRunningChanged;
	}

	protected override void OnDisappearing()
	{
		animation?.Dispose();
		animation = null;
		base.OnDisappearing();
	}

	private void OnSceneLoaded(object? sender, EventArgs args) => StartAnimation();

	private void OnAnimateClicked(object? sender, EventArgs args) => StartAnimation();

	private void StartAnimation()
	{
		animation?.Dispose();
		animation = Scene.AnimationClock.Start(progress =>
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
		tapCount++;
		TapStatus.Text = $"Taps: {tapCount}";
		if (sender is SkUiShape shape)
			shape.Color = tapCount % 2 == 0 ? Color.FromArgb("#C54150") : Color.FromArgb("#087F83");
	}
}
