namespace MauiSkiaUiDemo;

/// <summary>App-package asset file names used by more than one demo surface.</summary>
public static class DemoAssets
{
    /// <summary>Bundled Earth sample image under Resources/Raw (a MauiAsset).</summary>
    public const string EarthImage = "earth.jpg";

    /// <summary>MauiImage PNG under Resources/Images (BaseSize 300,185).</summary>
    public const string DotnetBotImage = "dotnet_bot.png";

    /// <summary>MauiImage SVG under Resources/Images, referenced by the PNG Resizetizer generates.</summary>
    public const string BadgeImage = "skui_badge.png";

    /// <summary>Android vector drawable under Platforms/Android/Resources/drawable (48 × 48 dp), by its resource name.</summary>
    public const string AndroidVectorImage = "skui_vector_star";

    /// <summary>An icon drawn at runtime (a MauiAsset under Resources/Raw): all <c>currentColor</c>.</summary>
    public const string VectorGear = "vector_gear.svg";

    /// <summary>An icon drawn at runtime: a stroke in <c>currentColor</c>.</summary>
    public const string VectorCheck = "vector_check.svg";

    /// <summary>An icon drawn at runtime: its pole in <c>currentColor</c>, its flag in a color written in the file.</summary>
    public const string VectorFlag = "vector_flag.svg";

    /// <summary>An SVG with a gradient, which the runtime reader rejects with a <c>LoadError</c> naming it.</summary>
    public const string VectorGradient = "vector_gradient.svg";

    /// <summary>A web image (the one MAUI's Image docs use for <c>UriImageSource</c>).</summary>
    public const string WebImage = "https://aka.ms/campus.jpg";
}
