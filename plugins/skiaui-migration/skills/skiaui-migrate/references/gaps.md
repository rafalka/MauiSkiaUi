# Not available in drawn trees (yet)

Check the region for these before converting. If one is central to the page (the page is a list), convert around it or leave the page native and say so.

| MAUI | Status | Do instead |
| --- | --- | --- |
| `CollectionView` with `CanReorderItems` or snap points on its items layout | Not available | Keep the native `CollectionView` (outside the drawn region; its item templates stay native). Every other list (grouped, grid, horizontal, multiple selection) ports to `sk:SkUiCollectionView` member by member ([collection-view.md](collection-view.md)) |
| `Stepper` | Planned | Two `SkUiButton`s |
| `ListView`, `TableView`, cells, compatibility layouts | Obsolete in MAUI; not planned | `SkUiCollectionView` (or `SkUiVirtualScrollView` for plain lists); `SkUiBorder` for `Frame` |
| Drag and drop, tooltips, context flyouts, `ImageBrush` | Not planned | Keep native |
| `Entry`, `Editor`, `SearchBar` | Native for now; drawn versions planned (they use the platform keyboard) | `SkUiMauiContentView` |
| Pickers, `WebView`, maps, media | Native by design | `SkUiMauiContentView` |
| Third-party controls | Native | `SkUiMauiContentView` or outside the region |
| Pages, Shell, navigation, toolbar, popups | Stay MAUI | — |

Native islands (`SkUiMauiContentView`) sit above the drawn surface: drawn content cannot cover them, and while their scroller moves they show a snapshot on Android and Windows. Avoid drawn popovers or dropdowns that open over native islands.
