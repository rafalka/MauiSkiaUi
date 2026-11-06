using System.Text;
using MauiSkiaUi;

namespace MauiSkiaUi.Benchmarks;

/// <summary>What the Markdown scenarios share: a 100 KB document, and the view created by name, so a baseline commit older than the package still builds.</summary>
internal static class MarkdownBench
{
    private const string ViewType = "MauiSkiaUi.Markdown.SkUiMarkdownView, MauiSkiaUi.Markdown";
    private const string StyleType = "MauiSkiaUi.Markdown.SkUiMarkdownStyle, MauiSkiaUi.Markdown";

    internal static string Document { get; } = CreateDocument(100 * 1024);

    private static string CreateDocument(int length)
    {
        var text = new StringBuilder(length + 1024);
        for (var section = 1; text.Length < length; section++)
        {
            text.Append("# Section ").Append(section).Append("\n\n");
            text.Append("A paragraph with **bold**, *italic*, `inline code` and a [link](https://example.com/").Append(section).Append("), long enough to wrap across several lines of a phone's width so the layout has some work to do.\n\n");
            text.Append("> A quote with a second sentence in it, to give the bar some height.\n\n");
            text.Append("- First item\n- Second item with `code`\n  - Nested item\n- [x] A task\n\n");
            text.Append("```csharp\nvar total = items.Sum(item => item.Price * item.Quantity);\nConsole.WriteLine(total);\n```\n\n");
            text.Append("| Name | Price | Quantity |\n|:---|--:|:-:|\n| Apple | 1.20 | 3 |\n| Pear | 0.80 | 5 |\n\n");
        }
        return text.ToString();
    }

    /// <summary>The view in a scroll view, parsing in place so a frame has its content; an empty scroll view on a commit without the package.</summary>
    internal static View Build()
    {
        if (Type.GetType(ViewType) is not { } type || Type.GetType(StyleType) is not { } styleType)
            return Scenarios.Scroll(new SkUiVerticalStackLayout());
        var view = (SkUiView)Activator.CreateInstance(type)!;
        var style = Activator.CreateInstance(styleType)!;
        BenchScenario.TrySetOption(style, "MaximumInPlaceParseLength", int.MaxValue);
        BenchScenario.TrySetOption(view, "MarkdownStyle", style);
        BenchScenario.TrySetOption(view, "Text", Document);
        return Scenarios.Scroll(view);
    }

    /// <summary>Sets the text. On a commit without the package something still changes, so a frame follows and an update's wait does not time out.</summary>
    internal static void SetText(View root, string text)
    {
        var scroll = (SkUiScrollView)root;
        if (scroll.Content is { } view && view.GetType().GetProperty("Text") is not null)
            BenchScenario.TrySetOption(view, "Text", text);
        else
            scroll.Content = new SkUiVerticalStackLayout();
    }
}

/// <summary>A 100 KB document on Core nodes: the first frame records everything, and scrolling only composites.</summary>
public sealed class MarkdownDocument : BenchScenario
{
    public override string Name => "markdown-document";
    public override string Description => "100 KB Markdown document in a scroll view (SkUiMarkdownView): parse, build, measure, record";

    public override View Build() => MarkdownBench.Build();

    public override Func<View, IDisposable?>? Motion => root =>
    {
        var scroll = (SkUiScrollView)root;
        // Longer than MotionDuration so the whole sampling window is in motion, as ScrollFling does.
        var duration = MotionDuration + TimeSpan.FromSeconds(0.5);
        var animate = typeof(SkUiScrollView).GetMethod("AnimateScrollTo", [typeof(double), typeof(double), typeof(TimeSpan)]);
        if (animate is not null)
            return animate.Invoke(scroll, [0d, scroll.ContentSize.Height, duration]) as IDisposable;
        _ = scroll.ScrollToAsync(0, scroll.ContentSize.Height, true);
        return null;
    };
}

/// <summary>A streamed answer: a growing text only rebuilds its tail.</summary>
public sealed class MarkdownAppend : BenchScenario
{
    private int _round;

    public override string Name => "markdown-append";
    public override string Description => "100 KB Markdown document; update = append a few words to the last paragraph, as a stream does";

    public override View Build() => MarkdownBench.Build();

    public override Action<View>? Update => root => MarkdownBench.SetText(root, MarkdownBench.Document + "\n\nStreaming answer " + new string('x', ++_round % 200));
}
