using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Tests that replace process-wide state every drawn control reads (<see cref="SkUiLook.Current"/>,
/// <see cref="SkUiColorScheme.Current"/>, <see cref="SkUiTextOptions.DefaultRendering"/>, <see cref="SkUiFontScaling.Factor"/>,
/// <see cref="SkUiMotion.ReduceMotion"/>, image cache settings, accessibility). They run alone, after the parallel tests:
/// otherwise other classes would measure or paint with the temporary value and fail at random.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalStateCollection
{
    public const string Name = "Global look and color scheme";
}
