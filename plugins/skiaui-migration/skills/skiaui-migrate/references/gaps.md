# Not available in drawn trees (yet)

Check the region for these before converting. If one is central to the page (the page is a list), convert around it or leave the page native and say so.

| MAUI | Status | Do instead |
| --- | --- | --- |
| `CollectionView` | Planned (virtualized `SkUiCollectionView`) | Keep the native `CollectionView` (outside the drawn region; its item templates stay native), or for up to a few hundred items: a drawn stack inside `SkUiScrollView` with items added in code |
| `SwipeView` | Planned | `Swiped` / `PanUpdated` on the row for simple reveal actions, or keep the list native |
| `RefreshView` | Planned | Put a MAUI `RefreshView` around the surface root; a drawn scroller hands the drag to native parents at its top edge (verify on device) |
| `CarouselView`, `IndicatorView` | Planned | `SkUiScrollView Orientation="Horizontal" SnapPointsType="MandatorySingle"`; dots with `SkUiEllipse` |
| `Stepper` | Planned | Two `SkUiButton`s |
| `ListView`, `TableView`, cells, compatibility layouts | Obsolete in MAUI; not planned | `CollectionView` when available; `SkUiBorder` for `Frame` |
| Drag and drop, tooltips, context flyouts, `ImageBrush` | Not planned | Keep native |
| `Entry`, `Editor`, `SearchBar` | Native for now; drawn versions planned (they use the platform keyboard) | `SkUiMauiContentView` |
| Pickers, `WebView`, maps, media | Native by design | `SkUiMauiContentView` |
| Third-party controls | Native | `SkUiMauiContentView` or outside the region |
| Pages, Shell, navigation, toolbar, popups | Stay MAUI | — |

Native islands (`SkUiMauiContentView`) sit above the drawn surface: drawn content cannot cover them, and while their scroller moves they show a snapshot on Android and Windows. Avoid drawn popovers or dropdowns that open over native islands.
