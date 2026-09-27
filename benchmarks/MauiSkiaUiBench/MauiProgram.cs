using MauiSkiaUi;

namespace MauiSkiaUiBench;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().UseSkiaUi();
        return builder.Build();
    }
}

public sealed class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState) => new(new BenchPage()) { Title = "SkiaUi Bench" };
}

/// <summary>Launch options set by the platform entry points before the page starts.</summary>
public static class BenchLaunch
{
    public static MauiSkiaUi.Benchmarks.BenchArgs Options { get; set; } = MauiSkiaUi.Benchmarks.BenchArgs.Parse([]);
}
