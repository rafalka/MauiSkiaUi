using System.Runtime.CompilerServices;

namespace MauiSkiaUiSamples;

/// <summary>
/// What an example shows, declared at the top of its source file and displayed by the app: <see cref="Summary"/> under
/// the title, <see cref="HowTo"/> and <see cref="ThingsToKnow"/> in collapsible panels. Plain text; <c>`code`</c> in
/// backticks is shown in a monospace font. Keep it short: the source file's comments carry the full details, and the
/// Source button shows that file.
/// </summary>
/// <param name="Section">Where the example is listed.</param>
/// <param name="Title">Page title (a few words).</param>
/// <param name="Summary">One or two sentences: what the page presents.</param>
/// <param name="HowTo">Steps to achieve it in an app.</param>
/// <param name="ThingsToKnow">Caveats, alternatives and behavior that is easy to miss.</param>
/// <param name="SourcePath">
/// The example's source file, filled in by the compiler (<see cref="CallerFilePathAttribute"/>): leave it out. Its name
/// finds the embedded copy; the full path opens the file in an IDE when it exists on this machine.
/// </param>
public sealed record SampleInfo(
    SampleSection Section,
    string Title,
    string Summary,
    IReadOnlyList<string> HowTo,
    IReadOnlyList<string> ThingsToKnow,
    [CallerFilePath] string SourcePath = "")
{
    /// <summary>The source file's name (e.g. <c>CrossCheckBoxSample.cs</c>), also on another OS than the build's.</summary>
    public string SourceFileName => Path.GetFileName(SourcePath.Replace('\\', '/'));
}

/// <summary>An example page: <see cref="Info"/> is read by the catalog without creating the page.</summary>
public interface ISample
{
    static abstract SampleInfo Info { get; }
}

/// <summary>A catalog entry: the example's description and a factory for its page.</summary>
public sealed record SampleEntry(SampleInfo Info, Func<SamplePage> Create)
{
    /// <summary>The entry for <typeparamref name="T"/>.</summary>
    public static SampleEntry For<T>() where T : SamplePage, ISample, new() => new(T.Info, () => new T());
}
