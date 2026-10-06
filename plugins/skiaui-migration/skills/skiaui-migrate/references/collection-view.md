# MAUI `CollectionView` → `SkUiCollectionView`

`SkUiCollectionView` is **not** a MAUI API clone. It is a SkUi-first virtualized list on the drawn surface (FR-22). Use this reference when porting screens; update it whenever the control ships or gains members (together with [Migration.md](../../../../../docs/Migration.md)).

## Status

**Not available yet** (Phase B). Until then: keep a native `CollectionView` outside the drawn region, or a small drawn stack inside `SkUiScrollView` (see [gaps.md](gaps.md)).

## Design intent

| Topic | MAUI `CollectionView` | `SkUiCollectionView` |
| --- | --- | --- |
| API goal | Platform-native recycler + MAUI surface | One Skia surface, FR-21 indexed virtualization + recycling |
| Parity | Full MAUI member set | **SkUi-first**; only patterns that fit performance and the single-surface model |
| Grouping | Flat groups | Groups + **expandable** groups (`IsExpanded`; collapsed items not realized) |
| Migration | Rename prefix in XAML | Read this doc + [ScrollingAndCollectionViews.md](../../../../../docs/design/ScrollingAndCollectionViews.md#fr-22--skuicollectionview-requirements) |

## Mapping (fill in at MVP)

Document here, per MAUI member or app pattern:

- **Supported** — SkUi property / event name and any semantic difference.
- **Different** — SkUi alternative (e.g. scroll / selection / grouping model).
- **Unsupported** — keep native list, hybrid page, or workaround.

Expected sections when B2 lands:

- Data: `ItemsSource`, templates, empty view, collection changes.
- Layout: linear / grid (not necessarily MAUI's `ItemsLayout` type names).
- Header / footer / sticky behavior.
- Grouping and **expand / collapse**.
- Selection and item tap.
- Scroll, load-more threshold, pull to refresh.
- Intentional gaps (e.g. MAUI reorder APIs, `ItemsUpdatingScrollMode` equivalents if any).

## Agent workflow

1. Do not assume MAUI `CollectionView` XAML converts by prefix swap alone.
2. Compare the app's features against the tables above once this file is populated.
3. After migration, run `check_xaml.py` on the page; update [gaps.md](gaps.md) when the control is no longer listed as blocked.
