#if MAUI_DEVFLOW
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using MauiSkiaUi;
using MauiSkiaUi.Core;
using Microsoft.Maui.DevFlow.Agent.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// DevFlow agent extension <c>dev.skiaui</c> (Debug builds). The agent's own tree reports parent-relative frames for
/// drawn elements, has no bounds for Core nodes and cannot tap either (they have no platform views); these tools use
/// <see cref="SkUiDiagnostics"/> instead:
/// <code>
/// maui devflow extensions call dev.skiaui tree
/// maui devflow extensions call dev.skiaui tap '{"automationId":"SkiaPreview"}'
/// maui devflow extensions call dev.skiaui tap '{"text":"Label 2","type":"SkUiCoreLabel"}'
/// maui devflow extensions call dev.skiaui hit '{"x":120,"y":300}'
/// </code>
/// </summary>
internal static class SkiaUiDevFlowExtension
{
    public static void Register(AgentOptions options)
    {
        var extension = options.RegisterExtension("dev.skiaui", "SkiaUi drawn elements (SkUi* and Core): tree with window bounds, tap, hit-test");
        extension.MapTool("tree", "Drawn SkiaUi elements in the current window with window bounds (DIPs), text and automationId.",
            "GET", "/tree", request => OnUi(() => HttpResponse.Json(DrawnElements().Select(Describe).ToList())));
        extension.MapTool("tap", "Tap a drawn element (press + release at its center). Parameters: automationId, text, type, index (nth match).",
            "POST", "/tap", request => OnUi(() => Tap(Parameters(request))));
        extension.MapTool("hit", "Deepest drawn element at a window point. Parameters: x, y (DIPs).",
            "GET", "/hit", request => OnUi(() => Hit(Parameters(request))));
    }

    private static HttpResponse Tap(Dictionary<string, string> parameters)
    {
        var matches = DrawnElements().Where(element => Matches(element, parameters)).ToList();
        var index = parameters.TryGetValue("index", out var value) && int.TryParse(value, out var parsed) ? parsed : 0;
        if (index < 0 || index >= matches.Count)
            return HttpResponse.NotFound($"No drawn element matches ({matches.Count} candidates).");
        var target = matches[index];
        var hit = SkUiDiagnostics.SimulateTap(target);
        return hit is null
            // 422, not 409: the DevFlow CLI shows only the status code, and 409 is also its mutation-lease conflict.
            ? HttpResponse.Error("Element is not on screen (clipped or detached).", 422)
            : HttpResponse.Json(new { tapped = Describe(target), hit = Describe(hit) });
    }

    private static HttpResponse Hit(Dictionary<string, string> parameters)
    {
        if (!double.TryParse(parameters.GetValueOrDefault("x"), out var x) || !double.TryParse(parameters.GetValueOrDefault("y"), out var y))
            return HttpResponse.Error("x and y are required.");
        return VisiblePage() is { } page
            && SkUiDiagnostics.HitTestWindow(page, new Point(x, y)) is { } hit
            ? HttpResponse.Json(Describe(hit))
            : HttpResponse.NotFound("No drawn element at that point.");
    }

    private static bool Matches(IVisualTreeElement element, Dictionary<string, string> parameters) =>
        (!parameters.TryGetValue("automationId", out var id) || AutomationId(element) == id)
        && (!parameters.TryGetValue("text", out var text) || Text(element) == text)
        && (!parameters.TryGetValue("type", out var type) || element.GetType().Name == type);

    /// <summary>
    /// Drawn elements (under a connected surface root) of the page on screen, in tree order. Pages below it in the
    /// navigation stack or in other Shell sections keep their surfaces, so the whole window would also list (and tap)
    /// elements the user cannot see.
    /// </summary>
    private static IEnumerable<IVisualTreeElement> DrawnElements() =>
        VisiblePage() is { } page
            ? page.GetVisualTreeDescendants().Where(element =>
                element is SkUiView or ISkUiCoreNode && SkUiDiagnostics.GetWindowBounds(element) is not null)
            : [];

    /// <summary>The page on screen: the top modal page, else the Shell's current page, else the window's page.</summary>
    private static IVisualTreeElement? VisiblePage()
    {
        if (Application.Current?.Windows.FirstOrDefault()?.Page is not { } root)
            return null;
        if (root.Navigation.ModalStack.LastOrDefault() is { } modal)
            return modal;
        return root switch
        {
            Shell { CurrentPage: { } current } => current,
            NavigationPage { CurrentPage: { } current } => current,
            _ => root
        };
    }

    private static object Describe(IVisualTreeElement element)
    {
        var bounds = SkUiDiagnostics.GetWindowBounds(element);
        return new
        {
            type = element.GetType().Name,
            automationId = AutomationId(element),
            text = Text(element),
            bounds = bounds is { } b ? new { x = Math.Round(b.X, 1), y = Math.Round(b.Y, 1), width = Math.Round(b.Width, 1), height = Math.Round(b.Height, 1) } : null,
            parent = element.GetVisualParent()?.GetType().Name,
        };
    }

    private static string? AutomationId(IVisualTreeElement element) => element switch
    {
        VisualElement view => view.AutomationId,
        SkUiCoreNode node => node.AutomationId,
        _ => null
    };

    private static readonly ConcurrentDictionary<Type, PropertyInfo?> TextProperties = new();

    private static string? Text(IVisualTreeElement element) =>
        TextProperties.GetOrAdd(element.GetType(), type => type.GetProperty("Text", BindingFlags.Instance | BindingFlags.Public) is { PropertyType: var t } p && t == typeof(string) ? p : null)
            ?.GetValue(element) as string;

    private static Dictionary<string, string> Parameters(HttpRequest request)
    {
        var parameters = new Dictionary<string, string>(request.QueryParams, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(request.Body))
        {
            using var document = JsonDocument.Parse(request.Body);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
                foreach (var property in document.RootElement.EnumerateObject())
                    parameters[property.Name] = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString()! : property.Value.GetRawText();
        }
        return parameters;
    }

    private static Task<HttpResponse> OnUi(Func<HttpResponse> action) => MainThread.InvokeOnMainThreadAsync(action);
}
#endif
