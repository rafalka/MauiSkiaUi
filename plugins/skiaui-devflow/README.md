# SkiaUi DevFlow skill

An agent skill that makes an app using SkiaUi controllable by [.NET MAUI DevFlow](https://github.com/dotnet/maui-labs): it adds the `dev.skiaui` agent extension, so DevFlow and AI agents can list, tap and hit-test drawn controls (`SkUi*` views and Core nodes), which have no platform views. Setup by hand and the tool reference: [docs/DevFlow.md](../../docs/DevFlow.md).

| Skill | What it does |
| --- | --- |
| `skiaui-devflow` | Copies [the extension](skills/skiaui-devflow/assets/SkiaUiDevFlowExtension.cs) into the app, matches its compile symbol to the app's DevFlow setup, registers it with `AddMauiDevFlowAgent`, builds and checks it; explains the tools and how to read a tap's result |

## Install in Claude Code

As a plugin, from this repository's marketplace:

```
/plugin marketplace add rafalka/MauiSkiaUi
/plugin install skiaui-devflow@skiaui
```

Then ask, for example, "make this app controllable by DevFlow" or "DevFlow can't tap our SkiaUi buttons", or run `/skiaui-devflow:skiaui-devflow`.

Or copy `skills/skiaui-devflow` into your app repository's `.claude/skills/` (for everyone working on the app) or into `~/.claude/skills/` (for you, in every project). Agents that read the `SKILL.md` format can use the folder as it is.
