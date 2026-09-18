namespace MauiSkiaUi.Tests;

internal static class SkUiTestHelpers
{
    public static void Arrange(IView view, double width, double height)
    {
        view.Measure(width, height);
        view.Arrange(new Rect(0, 0, width, height));
    }
}
