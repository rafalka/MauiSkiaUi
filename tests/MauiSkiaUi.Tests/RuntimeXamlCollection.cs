using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Test classes that inflate XAML at runtime (<c>LoadFromXaml</c>). MAUI's runtime XAML loader fills a shared lookup
/// table without locking, so two classes loading XAML in parallel can fail with "An item with the same key has already
/// been added". Classes in one collection run one after another; other collections still run in parallel.
/// </summary>
[CollectionDefinition(Name)]
public sealed class RuntimeXamlCollection
{
    public const string Name = "Runtime XAML";
}
