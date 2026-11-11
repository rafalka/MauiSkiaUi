// SkiaUi DevFlow extension: copy this file into a .NET MAUI app that uses SkiaUi (SkiaUi.Maui) and the DevFlow agent
// (Microsoft.Maui.DevFlow.Agent), then register it: builder.AddMauiDevFlowAgent(SkiaUiDevFlowExtension.Register).
// Setup and tool reference: https://github.com/rafalka/MauiSkiaUi/blob/master/docs/DevFlow.md
// The SkiaUi demo app compiles this same file, so it builds against the current SkiaUi and DevFlow versions.
#if MAUI_DEVFLOW
#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using MauiSkiaUi;
using MauiSkiaUi.Core;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.DevFlow.Agent.Core;
using Microsoft.Maui.Graphics;

namespace SkiaUiDevFlow;

/// <summary>
/// DevFlow agent extension <c>dev.skiaui</c> (Debug builds). Drawn elements (<c>SkUi*</c> views and Core nodes) have no
/// platform views, so the agent's own tree reports parent-relative frames for them, has no bounds for Core nodes and
/// cannot tap them; these tools use <see cref="SkUiDiagnostics"/> instead:
/// <code>
/// maui devflow extensions call dev.skiaui tree
/// maui devflow extensions call dev.skiaui tap '{"automationId":"SaveButton"}'
/// maui devflow extensions call dev.skiaui tap '{"text":"Label 2","type":"SkUiCoreLabel"}'
/// maui devflow extensions call dev.skiaui tapAt '{"x":120,"y":300}'
/// maui devflow extensions call dev.skiaui hit '{"x":120,"y":300}'
/// </code>
/// A tap is released only when a drawn tap recognizer takes it (a button, a toggle, a tap gesture, a text field); otherwise
/// it is cancelled, so a missed tap has no side effects, and the response says what happened (<c>taken</c>, <c>winner</c>,
/// <c>passedToNative</c>). Pass <c>"force": true</c> to release it anyway.
/// </summary>
internal static class SkiaUiDevFlowExtension
{
    private const string ExtensionId = "dev.skiaui";

    public static void Register(AgentOptions options)
    {
        var extension = options.RegisterExtension(ExtensionId, "SkiaUi drawn elements (SkUi* and Core): tree with window bounds, tap, hit-test");
        extension.MapTool("tree", "Drawn SkiaUi elements of the page on screen with window bounds (DIPs), text and automationId.",
            "GET", "/tree", request => OnUi(() => HttpResponse.Json(DrawnElements().Select(Describe).ToList())));
        extension.MapTool("tap", "Tap a drawn element at its center. Released only when a drawn tap recognizer takes it, else cancelled (force: true releases anyway). "
                + "Reports taken, winner, passedToNative, released. Parameters: automationId, text, type, index (nth match), force.",
            "POST", "/tap", request => OnUi(() => Tap(Parameters(request))));
        extension.MapTool("tapAt", "Tap the drawn content at a window point, as tap does. Parameters: x, y (DIPs), force.",
            "POST", "/tapAt", request => OnUi(() => TapAt(Parameters(request))));
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
        // A tap lands on whatever is at the element's center: something covering it, or what is there instead when the
        // element is scrolled out of its scroller's viewport. Don't press then: the tap would go to another element.
        // 422, not 409: the DevFlow CLI shows only the status code, and 409 is also its mutation-lease conflict.
        if (SkUiDiagnostics.GetSurfaceRoot(target) is not { } root || SkUiDiagnostics.GetRootBounds(target) is not { } bounds
            || SkUiDiagnostics.HitTest(root, bounds.Center) is not { } atCenter)
            return HttpResponse.Error("Element is not on screen (clipped or detached).", 422);
        if (!IsSelfOrDescendant(atCenter, target))
            return HttpResponse.Error($"Element is covered or scrolled out of view: its center hits {atCenter.GetType().Name}. Scroll it into view first.", 422);
        return TapResponse(target, SkUiDiagnostics.SimulateTap(target, releaseOnlyIfTap: !Force(parameters)));
    }

    private static HttpResponse TapAt(Dictionary<string, string> parameters)
    {
        if (!TryPoint(parameters, out var point))
            return HttpResponse.Error("x and y are required.");
        if (VisiblePage() is not { } page
            || SkUiDiagnostics.HitTestWindow(page, point) is not { } hit
            || SkUiDiagnostics.GetSurfaceRoot(hit) is not { } root
            || SkUiDiagnostics.GetWindowBounds(root) is not { } rootBounds)
            return HttpResponse.NotFound("No drawn element at that point.");
        var result = SkUiDiagnostics.SimulateTapAt(root, new Point(point.X - rootBounds.X, point.Y - rootBounds.Y), releaseOnlyIfTap: !Force(parameters));
        return TapResponse(target: null, result);
    }

    private static HttpResponse TapResponse(IVisualTreeElement? target, SkUiSimulatedTap result) =>
        HttpResponse.Json(new
        {
            tapped = target is null ? null : Describe(target),
            hit = result.Hit is { } hit ? Describe(hit) : null,
            // A drawn tap recognizer took it: its action (Clicked, a command, a toggle) ran, or runs when released.
            taken = result.IsTap,
            winner = result.Winner?.GetType().Name,
            winnerOwner = result.Winner?.Owner is IVisualTreeElement owner ? Describe(owner) : null,
            released = result.Released,
            // Nothing drawn took it: a real touch would reach native views around the surface instead.
            passedToNative = result.PassedToNative
        });

    private static HttpResponse Hit(Dictionary<string, string> parameters)
    {
        if (!TryPoint(parameters, out var point))
            return HttpResponse.Error("x and y are required.");
        return VisiblePage() is { } page && SkUiDiagnostics.HitTestWindow(page, point) is { } hit
            ? HttpResponse.Json(Describe(hit))
            : HttpResponse.NotFound("No drawn element at that point.");
    }

    private static bool IsSelfOrDescendant(IVisualTreeElement element, IVisualTreeElement ancestor)
    {
        for (var current = element; current is not null; current = current.GetVisualParent())
            if (ReferenceEquals(current, ancestor))
                return true;
        return false;
    }

    private static bool TryPoint(Dictionary<string, string> parameters, out Point point)
    {
        var ok = double.TryParse(parameters.GetValueOrDefault("x"), NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            & double.TryParse(parameters.GetValueOrDefault("y"), NumberStyles.Float, CultureInfo.InvariantCulture, out var y);
        point = new Point(x, y);
        return ok;
    }

    private static bool Force(Dictionary<string, string> parameters) =>
        parameters.TryGetValue("force", out var value) && bool.TryParse(value, out var force) && force;

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

    /// <summary>The page on screen: the top modal page, else the Shell's or navigation page's current page, else the window's page.</summary>
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
