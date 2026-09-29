using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Tests that replace process-wide state every drawn control reads (<see cref="SkUiLook.Current"/>,
/// <see cref="SkUiColorScheme.Current"/>). They run alone, after the parallel tests: otherwise other classes would
/// paint with the temporary look and fail at random.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalStateCollection
{
    public const string Name = "Global look and color scheme";
}
