---
name: skiaui-migrate
description: Converts .NET MAUI XAML pages, views and custom controls to SkiaUi's drawn controls (SkiaUi.Maui package, SkUi* types) - placing the drawn surface, renaming controls, wrapping native-only controls, moving styles, and converting gesture recognizers and TouchBehavior to drawn gesture events. Use when asked to migrate, port or convert a MAUI screen or control to SkiaUi, or to fix SkiaUi XAML that has native MAUI views (also in BindableLayout templates) or unsupported gestures inside drawn trees.
argument-hint: "[page.xaml | folder]"
---

# Migrate MAUI XAML to SkiaUi

SkiaUi draws a subtree of a MAUI page on one Skia surface. Pages, Shell, navigation, view models, bindings, converters, resources and `AppThemeBinding` stay as they are; the controls inside the drawn region become `SkUi*` views with MAUI's property names.

## Rules

1. **Only drawn views inside a drawn tree.** Every element inside a `SkUi*` view must be a `SkUi*` view, a custom control deriving from one, or a native control wrapped in `<sk:SkUiMauiContentView>`. A bare `Label` or `Entry` there throws at runtime ("Only SkiaUi children are supported").
2. **One surface per region.** Put one surface root (`SkUiContentView` or a drawn layout) around the region, with `SkUiScrollView` inside it. Never sprinkle single drawn controls into MAUI layouts: each becomes a separate surface.
3. **Do not invent API.** Use only types and members named in this skill's references or found in the SkiaUi package (IntelliSense, the build, the repo docs at github.com/rafalka/MauiSkiaUi/tree/master/docs/controls). If a MAUI feature has no drawn equivalent (see [gaps.md](references/gaps.md)), keep that part native rather than approximating it silently.
4. **Keep behavior.** Keep every binding, `x:Name`, command, `AutomationId`, `SemanticProperties` and visual state. A converted screen must do what the native one did.
5. **Do not touch** pages, Shell, navigation, view models or native-only parts outside the region. Keep MAUI styles that native controls still use.

## Workflow

1. **Setup** (once per app): the app references `SkiaUi.Maui`, `MauiProgram` calls `.UseSkiaUi()`, and each converted XAML file declares `xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi"`.
2. **Read before editing:** the XAML, its code-behind, the custom controls it uses, the styles it references (`Resources/Styles`, app resources), and how the code-behind touches named elements.
3. **Choose the region.** Usually the page body under the toolbar. Find blockers inside it with [gaps.md](references/gaps.md): `CollectionView`, `SwipeView`, `RefreshView`, `CarouselView`, third-party controls, controls with custom handlers. For list pages, read [collection-view.md](references/collection-view.md): `SkUiCollectionView` is SkUi-first, not a MAUI rename. Shrink the region around blockers, wrap them, or stop and report if they are the heart of the page.
4. **Convert** with [controls.md](references/controls.md): wrap the region, rename controls, map MAUI-only properties, wrap native-only controls.
5. **Gestures and behaviors** with [gestures.md](references/gestures.md): `TapGestureRecognizer` (1 or 2 taps) stays; swipe, pan, pinch, pointer recognizers, `TouchBehavior` (and subclasses) and effects are converted.
6. **Styles:** for every implicit or keyed style the region uses that targets a MAUI type, add a style with `TargetType="sk:SkUi…"` and the same setters (keep the MAUI style if native controls still use it). Visual state groups move with the style.
7. **Custom controls** with [custom-controls.md](references/custom-controls.md): port shared ones first (they convert every page that uses them), or wrap them in `SkUiMauiContentView`.
8. **Code-behind:** update element types of named fields used in code (`Label` → `SkUiLabel`), views created in code inside the region (`new Label` → `new SkUiLabel`), gesture code (`new PanGestureRecognizer` → `PanUpdated`), animations (`FadeToAsync` / `TranslateToAsync` / MAUI `Animation` → `AnimateAsync` / `SkUiViewAnimation`, [controls.md](references/controls.md#animations-code-behind)), and `DataTemplate`s created in code for `BindableLayout` (their content → drawn views).
9. **Check:** run the checker on the changed files, then build.

   ```bash
   python3 "${CLAUDE_SKILL_DIR}/scripts/check_xaml.py" --root <app project folder> <changed .xaml files or folders>
   ```

   It reports native views inside drawn trees, unsupported gesture recognizers, platform behaviors, effects, BindableLayout templates and empty views that are not drawn, `IsClippedToBounds`, `TemplateBinding` / `RelativeSource TemplatedParent` on drawn views, styles targeting MAUI types, custom controls that are not drawn, and drawn controls placed alone in MAUI content. Fix every error; justify any warning you leave. (Without `CLAUDE_SKILL_DIR`, run the script from this skill's `scripts` folder.)
10. **Build** for at least one platform (`dotnet build -f net10.0-android` or the app's target) and fix errors. Compile-time XAML (`x:DataType`, XamlC) catches misspelled properties.
11. **Report** what changed, what stayed native and why, and what the user should check on a device: taps and drags in scrollers, light / dark theme, large OS text, screen reader, and the debug output for `SkiaUi:` lines (drawn views log MAUI gesture input they ignore on the first press).

## Example

```xml
<!-- MAUI -->
<ScrollView>
    <VerticalStackLayout Padding="16" Spacing="8">
        <Frame CornerRadius="8">
            <Grid ColumnDefinitions="*,Auto">
                <Grid.GestureRecognizers>
                    <TapGestureRecognizer Command="{Binding OpenCommand}" CommandParameter="{Binding .}" />
                </Grid.GestureRecognizers>
                <Label Text="{Binding Title}" Style="{StaticResource Title}" />
                <Image Grid.Column="1" Source="chevron.png" />
            </Grid>
        </Frame>
        <Entry Text="{Binding Note}" />
    </VerticalStackLayout>
</ScrollView>

<!-- SkiaUi -->
<sk:SkUiContentView>
    <sk:SkUiScrollView>
        <sk:SkUiVerticalStackLayout Padding="16" Spacing="8">
            <sk:SkUiBorder StrokeShape="RoundRectangle 8" Padding="20">
                <sk:SkUiGrid ColumnDefinitions="*,Auto" TappedCommand="{Binding OpenCommand}" TappedCommandParameter="{Binding .}" ShowsPressEffect="True">
                    <sk:SkUiLabel Text="{Binding Title}" Style="{StaticResource DrawnTitle}" />
                    <sk:SkUiImage Grid.Column="1" Source="chevron.png" />
                </sk:SkUiGrid>
            </sk:SkUiBorder>
            <sk:SkUiMauiContentView>
                <Entry Text="{Binding Note}" />
            </sk:SkUiMauiContentView>
        </sk:SkUiVerticalStackLayout>
    </sk:SkUiScrollView>
</sk:SkUiContentView>
<!-- DrawnTitle: a copy of the Title style with TargetType="sk:SkUiLabel" -->
```

Keeping the `TapGestureRecognizer` would also work; `TappedCommand` is shorter. `ShowsPressEffect` adds the press feedback the native tap did not have; leave it out to keep the original look.

## References

- [controls.md](references/controls.md): MAUI → SkiaUi controls and properties.
- [gestures.md](references/gestures.md): gesture recognizers, `TouchBehavior`, effects, input blockers.
- [custom-controls.md](references/custom-controls.md): composite controls, canvas controls, handlers and renderers, templates.
- [gaps.md](references/gaps.md): what has no drawn equivalent yet, and what to do instead.
