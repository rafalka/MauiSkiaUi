# SkiaUi migration skills

Agent skills that help an AI coding agent move .NET MAUI screens to SkiaUi's drawn controls. They follow the same rules as the human [migration guide](../../docs/Migration.md).

| Skill | What it does |
| --- | --- |
| `skiaui-audit` | Read-only survey of a MAUI app: controls by migration path, blockers, gesture and `TouchBehavior` work, custom handlers, third-party and custom controls, pages ranked by size. Ends with a migration plan |
| `skiaui-migrate` | Converts a page, view or custom control: places the drawn surface, renames controls, wraps native-only controls, moves styles, converts gestures; checks the result with a XAML checker and a build |

Both include a Python 3 script (standard library only): `audit_maui_app.py` writes the inventory, `check_xaml.py` reports MAUI content that drawn trees do not run (native views, unsupported gesture recognizers, platform behaviors, effects, `BindableLayout`, styles targeting MAUI types (keyed, and implicit ones the drawn replacements no longer get), lone drawn controls).

## Install in Claude Code

As a plugin, from this repository's marketplace:

```
/plugin marketplace add rafalka/MauiSkiaUi
/plugin install skiaui-migration@skiaui
```

Then ask, for example, "audit this app for a SkiaUi migration" or "migrate Views/OrderDetailPage.xaml to SkiaUi", or run `/skiaui-migration:skiaui-audit` / `/skiaui-migration:skiaui-migrate Views/OrderDetailPage.xaml`.

Or copy the skill folders (`skills/skiaui-audit`, `skills/skiaui-migrate`) into your app repository's `.claude/skills/` (for everyone working on the app) or into `~/.claude/skills/` (for you, in every project).

## Other agents

The skills use the `SKILL.md` format (YAML front matter with `name` and `description`, Markdown instructions, files loaded on demand). Agents that read that format can use the folders as they are; the instructions refer to the scripts by `${CLAUDE_SKILL_DIR}`, and otherwise run them from the skill's `scripts` folder.

## Running the scripts yourself

```bash
python3 skills/skiaui-audit/scripts/audit_maui_app.py --top 30 path/to/app > inventory.md
python3 skills/skiaui-migrate/scripts/check_xaml.py --root path/to/app path/to/app/Views
```

`check_xaml.py` exits with 1 when it finds errors, so it can also run in CI over converted folders.

## Maintaining

The skills describe SkiaUi's current API and gaps. When a change adds a MAUI-parity feature, a control, or removes a gap (for example a drawn `CollectionView` or `BindableLayout` support), update `docs/Migration.md`, the skill references (`skills/skiaui-migrate/references/*.md`, the background in `skills/skiaui-audit/SKILL.md`) and the tables in both scripts together.
