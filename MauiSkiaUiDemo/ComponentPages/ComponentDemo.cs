namespace MauiSkiaUiDemo;

/// <summary>Display and navigation metadata for one component demo page.</summary>
/// <param name="ComponentType">Concrete <c>SkUiView</c> type under test.</param>
/// <param name="PageType">Dedicated demo page type.</param>
/// <param name="Counterpart">MAUI counterpart name, or <see cref="SkUiOnlyCounterpart"/> when none.</param>
/// <param name="Category">Gallery section.</param>
/// <param name="Create">Factory that builds a fresh page instance.</param>
public sealed record ComponentDemo(Type ComponentType, Type PageType, string Counterpart, ComponentCategory Category, Func<ComponentDemoPage> Create)
{
    /// <summary>Counterpart label when a control has no MAUI twin in the side-by-side preview.</summary>
    public const string SkUiOnlyCounterpart = "SkUi only";

    /// <summary>Shell route prefix shared by every component demo.</summary>
    public const string RoutePrefix = "demo-";

    /// <summary>Short control name, matching the <c>Name</c> of <see cref="ComponentType"/>.</summary>
    public string Name => ComponentType.Name;

    /// <summary>Shell route for this demo (<see cref="RoutePrefix"/> + control name).</summary>
    public string Route => RoutePrefix + Name;
}
