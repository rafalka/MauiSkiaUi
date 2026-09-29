namespace MauiSkiaUi.LeakTests;

/// <summary>
/// A memory-leak scenario shared by the headless tests (<c>tests/MauiSkiaUi.Tests/MemoryLeakTests</c>) and the
/// on-device test app (<c>tests/MauiSkiaUi.DeviceTests</c>): it builds a UI, exercises it (clicks, re-layout, scrolling, gestures,
/// animations) and every tracked object must then be collectable.
/// </summary>
/// <param name="Name">Unique key (automation ids, test names).</param>
/// <param name="Group">Display group.</param>
/// <param name="Description">What the scenario does.</param>
/// <param name="Create">Creates a fresh run: each run builds new views, so nothing is shared between runs.</param>
public sealed record LeakScenario(string Name, string Group, string Description, Func<LeakScenarioRun> Create)
{
    /// <summary>Automation id of the scenario's row on the leak page.</summary>
    public string AutomationId => "Leak_" + Name;
}

/// <summary>
/// One run of a <see cref="LeakScenario"/>. Fields may hold the built views: the run object is dropped with the page,
/// so it never keeps them alive past the check.
/// </summary>
public abstract class LeakScenarioRun
{
    /// <summary>Builds the scenario UI (a drawn surface root, or MAUI views hosting several).</summary>
    public abstract View Build(LeakScenarioContext context);

    /// <summary>Exercises the UI once it is on screen (headless: arranged on a test surface).</summary>
    public virtual Task InteractAsync(LeakScenarioContext context) => Task.CompletedTask;

    /// <summary>
    /// After <see cref="InteractAsync"/>: why the interaction did not take effect (a tap that hit nothing, a drag that
    /// scrolled nothing), or <c>null</c>. A leak check over an interaction that silently did nothing proves nothing.
    /// </summary>
    public virtual string? CheckInteraction() => null;
}
