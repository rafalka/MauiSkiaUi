# SkUiRefreshView

Pull-to-refresh around drawn content: a feed, an inbox, a dashboard pulled down to load what is new.

**MAUI counterpart:** `RefreshView`. Its XAML ports by changing the prefix, with drawn content. A list can instead refresh by itself: [`SkUiCollectionView`](SkUiCollectionView.md)'s `IsPullToRefreshEnabled` has the same indicator and rules, and keeps the indicator below a sticky header. For a pull that does something other than refreshing (or a refresh with an indicator of your own), use the scroll views' overscroll events ([SkUiScrollView.md](SkUiScrollView.md#how-it-works): `Overscrolled`, `PullReleased`, `PullEdges`).

## How it works

- **Refreshing.** `IsRefreshing` (two-way) set to `true`, by a pull or by the app, raises `Refreshing` and then runs `Command` with `CommandParameter`, as MAUI's. By default (`RefreshCompletion="Manual"`, MAUI's rule) the app sets it back to `false` when the refresh is done; the indicator then goes.
- **Automatic completion** (`RefreshCompletion="Automatic"`, a SkiaUi extension). The view sets `IsRefreshing` back to `false` (and through a two-way binding, the view model's) when the refresh's work is done:
  - an async command that cannot execute while it runs (the MVVM Toolkit's `AsyncRelayCommand`, or any command that reports itself busy) is waited for until it can execute again;
  - a command that can still execute right after it ran (a plain `Command`) counts as done then. A MAUI `Command` with an `async` lambda returns at its first `await`, so give it a `canExecute` that is `false` while it runs, or use a deferral;
  - each deferral taken in `Refreshing` (`e.GetDeferral()`, completed with `Complete()` or `Dispose()`, from any thread) keeps the refresh running until it completes.
  Setting `IsRefreshing` to `false` still ends a refresh at once; a deferral completed later changes nothing.
- **Pulling through a scroller.** A drag that starts in a vertical drawn scroller of the content (`SkUiScrollView`, `SkUiVirtualScrollView`, `SkUiCollectionView`, a Core `SkUiCoreScrollView`) pulls once the scroller is at its top: also when its content does not overflow, when overscroll is off, and when the same drag first scrolled it up to the top. With nested vertical scrollers, the outermost one under the finger is pulled (inner ones chain to it at their top). A scroller with bounce overscroll bounces as well. Horizontal scrollers keep their sideways drags; a downward drag on them pulls.
- **Pulling the view itself.** A downward drag elsewhere in the content pulls the view: content that does not scroll, or a header above a list. It pulls only while the content's first vertical scroller is at its top (a header does not refresh a list scrolled down). As an overlay the content does not move and the indicator follows the finger with a rubber band; inline the content itself moves down. Released early, it goes back up.
- **Indicator.** `RefreshIndicator`, a public [`SkUiCoreRefreshIndicator`](SkUiCore.md#refresh-indicator) drawn by the look (`SkUiLook.DrawRefreshIndicator`; by default the look's activity indicator), the same one `SkUiCollectionView` shows. Its color is `RefreshColor` (the accent by default). It fades in (and as a badge turns) with the pull, and spins on the render thread while refreshing; moving it re-records nothing. A custom look draws a custom indicator (see below).
- **Styles** (`RefreshStyle`; `Default` follows the look, whose default follows the platform: `Inline` on iOS and Mac Catalyst, `Overlay` elsewhere):
  - `Overlay` (Android's): a round badge comes down over the content, which stays where it is; while refreshing it rests with its bottom at `SkUiLook.RefreshRestDistance` (64 DIPs).
  - `Inline` (iOS's): the pulled content moves down and the indicator shows in the room above it, starting at the pulled scroller's top (a header above the list stays); while refreshing the content stays down by the rest distance and the indicator is centered in that room. A drag takes the room back; the content comes back up when the refresh ends. A pulled scroller moves its content only with `Bounce` overscroll (the default on iOS); with other modes the indicator draws over the top of the content. Content that does not scroll moves as a whole.
- **Trigger distance.** A release past `RefreshTriggerDistance` (shown past the top, in DIPs; 0: the look's, 64) starts a refresh. The indicator's progress is the pull over that distance.
- **Mouse.** Touch and pen drags pull. Mouse drags (also a trackpad click on Mac Catalyst and the iPad pointer) pull only with `IsMousePullEnabled` (default `false`, as desktop apps expect; laptops' touch screens still pull). A scroller with bounce overscroll still bounces for the mouse, but nothing refreshes.
- **Turning the pull off** (as MAUI's). `IsRefreshEnabled = false` turns the pull off while the content stays interactive, and ends a running refresh. A `Command` that cannot execute turns the pull off too, but does not end a refresh it started (an async command that cannot run twice); MAUI then also reports `IsRefreshEnabled` as `false`, SkiaUi keeps the value set. `IsRefreshing` cannot become `true` while the pull is off or the view is disabled. Disabling the view (`IsEnabled = false`) ends a refresh.
- **Gestures.** The pull is an arena member ([EventMechanism.md](../design/EventMechanism.md)): taps in the content work as without it, and a drag that starts on a button and pulls is a pull, not a tap. A refresh view nested in the content pulls for itself.
- **Clipping.** The view clips its content (`ClipToBounds` is `true`, as MAUI's `IsClippedToBounds`).
- **Lifetime.** The view keeps no scroller alive that left its content, and a scroller kept by the app keeps no view alive.

Not available: a screen-reader action that starts a refresh (set `IsRefreshing` from a button for those users), pulling from other edges.

## Custom indicator

The indicator is drawn by the current look ([ControlLook.md](../design/ControlLook.md)), for every pull-to-refresh at once:

- `SkUiLook.DrawRefreshIndicator(canvas, SkUiRefreshIndicatorPaint)` (or the `RefreshIndicatorPainter` delegate) draws it into its square (`Bounds`), with the `Color`, the `Style` and whether it `IsRefreshing`. The default draws `DrawActivityIndicator` on a badge of the default background (overlay) or bare (inline). It is recorded once per state; while refreshing the compositor spins it (`RefreshIndicatorSpinPeriod`, 1 s; 0: no spin), so draw centered, rotation-symmetric geometry.
- `GetRefreshPullFeedback(progress, style)` returns the opacity and rotation a pull shows (default: fade in; turn up to 270° as a badge): composite-time, nothing re-records. A look that draws the pull itself (a growing arc) returns `true` from `RefreshIndicatorDrawsPullProgress` and reads `PullProgress` in the paint; the indicator then records again as the pull changes.
- Sizes: `RefreshIndicatorSize` (40), `RefreshTriggerDistance` (64), `RefreshRestDistance` (64); `DefaultRefreshStyle`; `GetRefreshIndicatorShadow(style)` (the default look: a soft shadow under the badge).

```csharp
public sealed class BrandLook : DefaultSkUiLook
{
    public override double RefreshIndicatorSize => 32;

    protected override void DrawRefreshIndicatorCore(SKCanvas canvas, SkUiRefreshIndicatorPaint paint)
    {
        using var dot = new SKPaint { IsAntialias = true, Color = paint.Color };
        var r = paint.Bounds.Width / 8;
        for (var i = 0; i < 3; i++) // three dots around the center: spun while refreshing
        {
            var angle = i * 2 * MathF.PI / 3;
            canvas.DrawCircle(paint.Bounds.MidX + MathF.Cos(angle) * r * 2.5f, paint.Bounds.MidY + MathF.Sin(angle) * r * 2.5f, r, dot);
        }
    }
}
```

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`refresh.SetContent(feed).SetCommand(reload)`). Getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10).
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

## How to use

```xml
<sk:SkUiRefreshView IsRefreshing="{Binding IsRefreshing}"
                    RefreshColor="Teal"
                    Command="{Binding RefreshCommand}">
  <sk:SkUiScrollView>
    <sk:SkUiVerticalStackLayout BindableLayout.ItemsSource="{Binding Posts}" Padding="12">
      <BindableLayout.ItemTemplate>
        <DataTemplate x:DataType="local:Post">
          <sk:SkUiLabel Text="{Binding Title}" Padding="4,12" />
        </DataTemplate>
      </BindableLayout.ItemTemplate>
    </sk:SkUiVerticalStackLayout>
  </sk:SkUiScrollView>
</sk:SkUiRefreshView>
```

```csharp
[RelayCommand]
private async Task RefreshAsync()
{
    foreach (var post in await _feed.LoadNewerAsync())
        Posts.Insert(0, post);
    IsRefreshing = false; // the indicator goes (not needed with RefreshCompletion="Automatic")
}
```

With `RefreshCompletion="Automatic"` the view ends the refresh when `RefreshAsync` completes (the generated `AsyncRelayCommand` cannot execute while it runs), and the view model's `IsRefreshing` follows through the binding. From code-behind, a deferral does the same:

```csharp
refresh.Refreshing += async (_, e) =>
{
    using var deferral = e.GetDeferral();
    await LoadAsync();
};
```

From MAUI, change `RefreshView` to `sk:SkUiRefreshView` and its content to drawn views. Do not also turn on `IsPullToRefreshEnabled` on a `SkUiCollectionView` inside it: both would refresh (`check_xaml.py` reports it).

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `Content` | — | Drawn view that is refreshed |
| `IsRefreshing` | `false` | Two-way; `true` raises `Refreshing` and runs `Command` |
| `Command` / `CommandParameter` | — | Runs when a refresh starts; while it cannot execute, the pull is off |
| `RefreshColor` | `null` | The indicator's arc; `null`: the accent color |
| `IsRefreshEnabled` | `true` | `false`: no pull, the content stays interactive; ends a refresh |
| `RefreshStyle` | `Default` | `Overlay` (badge over the content), `Inline` (content moves down, iOS), `Default`: the look's (the platform's) |
| `RefreshTriggerDistance` | `0` | DIPs a release must pass; 0: the look's (64) |
| `IsMousePullEnabled` | `false` | Mouse drags pull too |
| `RefreshCompletion` | `Manual` | `Automatic`: the view ends the refresh when an async command and the deferrals are done |
| `RefreshIndicator` | — | The `SkUiCoreRefreshIndicator` shown (read-only) |
| `Refreshing` (event) | — | Before `Command` runs; `SkUiRefreshingEventArgs` (`GetDeferral()`); MAUI handlers taking `EventArgs` still attach |

## Related

[SkUiCollectionView](SkUiCollectionView.md) (built-in pull-to-refresh) · [SkUiScrollView](SkUiScrollView.md) (overscroll events) · Gallery: `RefreshViewDemoPage` (next to MAUI's `RefreshView`: a scroll view, a header above a scroll view, short content without a scroller) · Tests: `RefreshViewTests`, `RefreshViewXamlTests`; leak scenario `RefreshPulled`
