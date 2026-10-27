namespace MauiSkiaUiDemo;

/// <summary>Display and navigation metadata for one component demo page.</summary>
/// <param name="ComponentType">Concrete <c>SkUiView</c> type under test.</param>
/// <param name="PageType">Dedicated demo page type.</param>
/// <param name="Counterpart">MAUI counterpart name, or <see cref="SkUiOnlyCounterpart"/> when none.</param>
/// <param name="Category">Gallery section.</param>
/// <param name="Create">Factory that builds a fresh page instance.</param>
/// <param name="Key">Optional name for a second scenario page of the same control (route, automation id).</param>
/// <param name="Scenario">
/// The page's purpose, shown after the control's name in its title, when the name alone does not say it: every page of a
/// control with several pages (its main page too), and pages that show the control in a list or another setting.
/// </param>
public sealed record ComponentDemo(Type ComponentType, Type PageType, string Counterpart, ComponentCategory Category, Func<ComponentDemoPage> Create, string? Key = null,
    string? Scenario = null)
{
    /// <summary>Counterpart label when a control has no MAUI twin in the side-by-side preview.</summary>
    public const string SkUiOnlyCounterpart = "SkUi only";

    /// <summary>Shell route prefix shared by every component demo.</summary>
    public const string RoutePrefix = "demo-";

    /// <summary>Unique identifier of the demo (route, automation id): <see cref="Key"/>, else the <c>Name</c> of <see cref="ComponentType"/>.</summary>
    public string Name => Key ?? ComponentType.Name;

    /// <summary>Shell route for this demo (<see cref="RoutePrefix"/> + <see cref="Name"/>).</summary>
    public string Route => RoutePrefix + Name;

    /// <summary>The control's type as written in C# (<c>SkUiStepper&lt;decimal&gt;</c>), shown in the gallery.</summary>
    public string DisplayName => TypeName(ComponentType);

    /// <summary>Gallery and page title: the control's <see cref="DisplayName"/>, then the <see cref="Scenario"/> (what the page is for).</summary>
    public string Title => Scenario is null ? DisplayName : DisplayName + ": " + Scenario;

    /// <summary>Gallery caption: the MAUI control the drawn one replaces.</summary>
    public string Description
    {
        get
        {
            // Every Core node is SkUi only; saying so on each Core entry adds nothing.
            var counterpart = Counterpart == SkUiOnlyCounterpart
                ? Category == ComponentCategory.Core ? null : "No MAUI counterpart"
                : "Replaces " + Counterpart;
            return counterpart ?? "";
        }
    }

    private static string TypeName(Type type) => type switch
    {
        _ when type == typeof(decimal) => "decimal",
        _ when type == typeof(double) => "double",
        _ when type == typeof(int) => "int",
        { IsGenericType: true } => type.Name[..type.Name.IndexOf('`')] + "<" + string.Join(", ", type.GetGenericArguments().Select(TypeName)) + ">",
        _ => type.Name
    };
}
