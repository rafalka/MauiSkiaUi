#!/usr/bin/env python3
"""Surveys a .NET MAUI app for a migration to SkiaUi's drawn controls and prints a Markdown inventory.

Usage: audit_maui_app.py [--top N] APP_FOLDER

Reads every .xaml and .cs file under APP_FOLDER (skipping bin, obj and hidden folders). Reports:
- MAUI controls by migration path (drawn equivalent, native island, no drawn equivalent yet), with the Community
  Toolkit controls SkiaUi draws (Expander);
- gesture input (recognizers by type, toolkit TouchBehavior, other behaviors, effects), in XAML and in code;
- MAUI view animations in code (render-thread conversions);
- BindableLayout, templates, custom handlers / renderers / effects, canvas views, third-party XAML namespaces;
- custom controls by how often they are used;
- pages and views ranked by size, with the blockers each one contains.

The counts are textual (XAML elements, C# patterns): a starting point for reading the code, not a verdict.
"""

import argparse
import collections
import os
import re
import sys
import xml.sax
from xml.sax.handler import feature_namespaces

MAUI_NS = "http://schemas.microsoft.com/dotnet/2021/maui"
X_NS = "http://schemas.microsoft.com/winfx/2009/xaml"

DRAWN = {
    "ContentView", "Grid", "VerticalStackLayout", "HorizontalStackLayout", "StackLayout", "AbsoluteLayout", "FlexLayout",
    "ScrollView", "Border", "Frame", "Label", "Button", "Image", "ImageButton", "BoxView", "Ellipse", "Line", "Rectangle",
    "RoundRectangle", "Path", "Polygon", "Polyline", "CheckBox", "Switch", "RadioButton", "Slider", "ProgressBar",
    "ActivityIndicator", "GraphicsView", "CollectionView", "SwipeView", "RefreshView",
}
# Community Toolkit controls with a drawn equivalent, counted as "Toolkit <name>".
TOOLKIT_DRAWN = {"Expander"}
NATIVE_ISLAND = {"Entry", "Editor", "SearchBar", "Picker", "DatePicker", "TimePicker", "WebView", "HybridWebView", "BlazorWebView", "Map"}
# CollectionViews SkUiCollectionView cannot take, counted apart from the ones that port.
BLOCKED_COLLECTION_VIEW = "CollectionView (reorderable or with snap points)"
NOT_YET = {BLOCKED_COLLECTION_VIEW, "ListView", "TableView", "CarouselView", "IndicatorView", "Stepper"}
PAGES = {"ContentPage", "TabbedPage", "FlyoutPage", "NavigationPage", "Shell"}
GESTURES = {"TapGestureRecognizer", "SwipeGestureRecognizer", "PanGestureRecognizer", "PinchGestureRecognizer",
            "PointerGestureRecognizer", "DragGestureRecognizer", "DropGestureRecognizer"}

CS_PATTERNS = {
    "gesture recognizers created in code": re.compile(r"new\s+(Tap|Swipe|Pan|Pinch|Pointer|Drag|Drop)GestureRecognizer\b"),
    "TouchBehavior created in code": re.compile(r"new\s+\w*TouchBehavior\b"),
    "custom handler registrations (ConfigureMauiHandlers / AddHandler)": re.compile(r"\bAddHandler\s*[<(]"),
    "handler mapper changes (AppendToMapping / PrependToMapping / ModifyMapping)": re.compile(r"\b(Append|Prepend|Modify)ToMapping\b"),
    "renderers (ExportRenderer / compatibility renderers)": re.compile(r"\bExportRenderer\b|\bAddCompatibilityRenderer\b"),
    "effects (PlatformEffect / RoutingEffect subclasses)": re.compile(r":\s*(PlatformEffect|RoutingEffect)\b"),
    "platform behaviors (PlatformBehavior subclasses)": re.compile(r":\s*PlatformBehavior\s*<"),
    "canvas views (SKCanvasView / SKGLView / GraphicsView / IDrawable)": re.compile(r"\b(SKCanvasView|SKGLView|GraphicsView|IDrawable)\b"),
    "BindableLayout in code": re.compile(r"\bBindableLayout\.Set\w+"),
    "MAUI view animations (FadeTo / TranslateTo / ScaleTo / RotateTo / new Animation; convert to AnimateAsync / SkUiViewAnimation)":
        re.compile(r"\b(Fade|Translate|Scale|Rotate|RelScale|RelRotate)To(Async)?\s*\(|\bnew\s+Animation\s*\("),
}



def collection_view_blocked(node):
    """Whether a CollectionView uses what SkUiCollectionView does not have: reordering, snap points.

    Conservative: a bound or resource value may be true, so anything but a literal "false" (or no value) blocks."""
    if node.attrs.get((None, "CanReorderItems"), "false").strip().lower() != "false":
        return True
    if any(child.name.endswith(".CanReorderItems") for child in node.children):
        return True
    pending = [child for child in node.children if child.name.endswith(".ItemsLayout")]
    while pending:
        child = pending.pop()
        if child.attrs.get((None, "SnapPointsType"), "None").strip() != "None":
            return True
        if child.name.endswith(".SnapPointsType"):  # a property element: its value is not kept, so assume snap points
            return True
        pending.extend(child.children)
    return False

class Node:
    __slots__ = ("uri", "name", "attrs", "children", "parent")

    def __init__(self, uri, name, attrs, parent):
        self.uri, self.name, self.attrs, self.children, self.parent = uri or "", name, attrs, [], parent

    def owner(self):
        """The nearest ancestor that is an object element (not a property element)."""
        node = self.parent
        while node is not None and "." in node.name:
            node = node.parent
        return node


class TreeBuilder(xml.sax.ContentHandler):
    def __init__(self):
        super().__init__()
        self.root, self.stack, self.namespaces = None, [], set()

    def startPrefixMapping(self, prefix, uri):
        self.namespaces.add(uri)

    def startElementNS(self, name, qname, attrs):
        node = Node(name[0], name[1], dict(attrs.items()), self.stack[-1] if self.stack else None)
        if self.stack:
            self.stack[-1].children.append(node)
        else:
            self.root = node
        self.stack.append(node)

    def endElementNS(self, name, qname):
        self.stack.pop()


def parse(path):
    handler = TreeBuilder()
    parser = xml.sax.make_parser()
    parser.setFeature(feature_namespaces, True)
    parser.setContentHandler(handler)
    parser.parse(path)
    return handler.root, handler.namespaces


def walk(node):
    stack = [node]
    while stack:
        current = stack.pop()
        yield current
        stack.extend(reversed(current.children))


def files(root, extension):
    for folder, dirs, names in os.walk(root):
        dirs[:] = sorted(d for d in dirs if d not in ("bin", "obj", "node_modules") and not d.startswith("."))
        for name in sorted(names):
            if name.endswith(extension):
                yield os.path.join(folder, name)


class FileInfo:
    def __init__(self, path, root_name):
        self.path, self.root_name = path, root_name
        self.views = 0
        self.native_islands = collections.Counter()
        self.not_yet = collections.Counter()
        self.gestures = collections.Counter()
        self.touch_behaviors = 0
        self.effects = 0
        self.third_party = collections.Counter()
        self.custom = collections.Counter()

    @property
    def blockers(self):
        items = [f"{name} ×{count}" for name, count in self.not_yet.most_common()]
        items += [f"{name} ×{count}" for name, count in self.third_party.most_common()]
        return items

    @property
    def conversions(self):
        items = [f"{name.replace('GestureRecognizer', '')} gesture ×{count}" for name, count in self.gestures.items() if name != "TapGestureRecognizer"]
        if self.touch_behaviors:
            items.append(f"TouchBehavior ×{self.touch_behaviors}")
        if self.effects:
            items.append(f"effects ×{self.effects}")
        if self.native_islands:
            items.append("native islands: " + ", ".join(f"{name} ×{count}" for name, count in self.native_islands.most_common()))
        return items


NON_VIEW_SUFFIXES = ("Behavior", "Behaviour", "Converter", "Effect", "Selector", "Extension", "Transformation", "Trigger", "Action", "Validator")


class ClassIndex:
    """Class name -> base type name, from C# declarations and XAML x:Class roots."""

    CLASS_RE = re.compile(r"\bclass\s+(\w+)\s*(?:<[^>{]*>)?\s*:\s*([\w.]+)")

    def __init__(self):
        self.bases = {}

    def add_cs(self, text):
        for name, base in self.CLASS_RE.findall(text):
            self.bases.setdefault(name, base.split(".")[-1])

    def add_xaml(self, x_class, root_name):
        if x_class:
            self.bases[x_class.split(".")[-1]] = root_name

    def derives(self, name, targets):
        seen = set()
        while name and name not in seen:
            if name in targets:
                return True
            seen.add(name)
            name = self.bases.get(name)
        return False

    def chain(self, name):
        names, seen = [], set()
        while name and name not in seen:
            seen.add(name)
            name = self.bases.get(name)
            if name and (not names or names[-1] != name):
                names.append(name)
        return names


def is_toolkit(uri):
    return "maui/toolkit" in uri or "CommunityToolkit.Maui" in uri


def is_third_party(uri, own_assemblies):
    if uri in (MAUI_NS, X_NS, "") or "MauiSkiaUi" in uri:
        return False
    if uri.startswith("http"):
        return True
    assembly = re.search(r"assembly=([\w.]+)", uri)
    if not assembly or assembly.group(1) in own_assemblies:
        return False
    return not re.match(r"(System|mscorlib|netstandard|Microsoft\.Maui)\b", assembly.group(1))


def short_namespace(uri):
    assembly = re.search(r"assembly=([\w.]+)", uri)
    return assembly.group(1) if assembly else uri


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("app")
    parser.add_argument("--top", type=int, default=25, help="number of pages / views / custom controls to list")
    args = parser.parse_args()
    root = os.path.abspath(args.app)

    own_assemblies = {os.path.splitext(os.path.basename(path))[0] for path in files(root, ".csproj")}
    totals = collections.Counter()
    gestures = collections.Counter()
    tap_details = collections.Counter()
    behaviors = collections.Counter()
    custom_usage = collections.Counter()
    third_party = collections.Counter()
    bindable_layouts = 0
    control_templates = 0
    data_templates = 0
    effects = 0
    infos = []
    unparsed = []
    uses_skiaui = False

    index = ClassIndex()
    cs_texts = {}
    for path in files(root, ".cs"):
        try:
            cs_texts[path] = open(path, encoding="utf-8-sig", errors="replace").read()
        except OSError:
            continue
        index.add_cs(cs_texts[path])
    trees = []
    for path in files(root, ".xaml"):
        try:
            tree, namespaces = parse(path)
        except Exception as error:
            unparsed.append((path, str(error)))
            continue
        if tree is None:
            continue
        index.add_xaml(tree.attrs.get((X_NS, "Class")), tree.name)
        trees.append((path, tree, namespaces))

    def is_touch_behavior(name):
        return name.endswith("TouchBehavior") or index.derives(name, {"TouchBehavior", "PlatformBehavior"})

    for path, tree, namespaces in trees:
        uses_skiaui |= any("MauiSkiaUi" in uri for uri in namespaces)
        info = FileInfo(os.path.relpath(path, root), tree.name)
        for node in walk(tree):
            if "." in node.name:
                prop = node.name.rsplit(".", 1)[1]
                if prop == "Behaviors":
                    for child in node.children:
                        behaviors[child.name] += 1
                        if is_touch_behavior(child.name):
                            info.touch_behaviors += 1
                elif prop == "Effects":
                    effects += len(node.children)
                    info.effects += len(node.children)
                continue
            for (_, local) in node.attrs:
                if local.startswith("BindableLayout.ItemsSource"):
                    bindable_layouts += 1
            if node.uri == MAUI_NS:
                name = node.name
                if name in GESTURES:
                    gestures[name] += 1
                    info.gestures[name] += 1
                    if name == "TapGestureRecognizer":
                        command = (None, "Command") in node.attrs
                        handler = (None, "Tapped") in node.attrs
                        tap_details["Command" if command and not handler else "Tapped handler" if handler else "neither (input blocker?)"] += 1
                        if node.attrs.get((None, "NumberOfTapsRequired"), "1") not in ("1", "2"):
                            tap_details["NumberOfTapsRequired > 2"] += 1
                elif name == "ControlTemplate":
                    control_templates += 1
                elif name == "DataTemplate":
                    data_templates += 1
                elif name == "CollectionView" and collection_view_blocked(node):
                    totals[BLOCKED_COLLECTION_VIEW] += 1
                    info.views += 1
                    info.not_yet[BLOCKED_COLLECTION_VIEW] += 1
                elif name in DRAWN or name in NATIVE_ISLAND or name in NOT_YET:
                    totals[name] += 1
                    info.views += 1
                    if name in NATIVE_ISLAND:
                        info.native_islands[name] += 1
                    elif name in NOT_YET:
                        info.not_yet[name] += 1
            elif is_toolkit(node.uri) and node.name in TOOLKIT_DRAWN:
                totals[f"Toolkit {node.name}"] += 1
                info.views += 1
            elif is_third_party(node.uri, own_assemblies):
                owner = node.owner()
                # Count a third-party control once, not its parts (chart series, axes, …).
                if not node.name.endswith(NON_VIEW_SUFFIXES) and not (owner is not None and is_third_party(owner.uri, own_assemblies)):
                    key = f"{short_namespace(node.uri)}: {node.name}"
                    third_party[key] += 1
                    info.third_party[node.name] += 1
                    info.views += 1
            elif node.uri.startswith(("clr-namespace:", "using:")) and "MauiSkiaUi" not in node.uri and node is not tree:
                if not node.name.endswith(NON_VIEW_SUFFIXES) and not index.derives(node.name, {"Behavior", "PlatformBehavior", "TouchBehavior"}):
                    custom_usage[node.name] += 1
                    info.custom[node.name] += 1
                    info.views += 1
        infos.append(info)

    code = collections.Counter()
    code_gestures = collections.Counter()
    code_files = collections.defaultdict(set)
    for path, text in cs_texts.items():
        uses_skiaui |= "UseSkiaUi" in text
        for label, pattern in CS_PATTERNS.items():
            matches = pattern.findall(text)
            if matches:
                code[label] += len(matches)
                code_files[label].add(os.path.relpath(path, root))
                if label.startswith("gesture recognizers"):
                    for kind in matches:
                        code_gestures[kind + "GestureRecognizer"] += 1

    out = []
    out.append(f"# SkiaUi migration inventory: {os.path.basename(root)}\n")
    out.append(f"{len(infos)} XAML files, SkiaUi {'already referenced' if uses_skiaui else 'not referenced yet'}.\n")

    out.append("## MAUI controls in XAML\n")
    out.append("| Path | Control | Count |\n| --- | --- | ---: |")
    drawn_names = DRAWN | {f"Toolkit {name}" for name in TOOLKIT_DRAWN}
    for group, names in (("Drawn equivalent", drawn_names), ("Native island (SkUiMauiContentView)", NATIVE_ISLAND), ("No drawn equivalent yet", NOT_YET)):
        for name, count in sorted(((n, totals[n]) for n in names if totals[n]), key=lambda item: -item[1]):
            out.append(f"| {group} | {name} | {count} |")
    out.append("")

    out.append("## Gesture input\n")
    out.append("| Kind | On drawn views | XAML | Code |\n| --- | --- | ---: | ---: |")
    for name in sorted(GESTURES, key=lambda n: -(gestures[n] + code_gestures[n])):
        if gestures[name] or code_gestures[name]:
            path = "runs as is (1 or 2 taps, primary button)" if name == "TapGestureRecognizer" else "convert to gesture events"
            out.append(f"| {name} | {path} | {gestures[name]} | {code_gestures[name]} |")
    touch = sum(count for name, count in behaviors.items() if is_touch_behavior(name))
    if touch or code["TouchBehavior created in code"]:
        out.append(f"| TouchBehavior | convert: TappedCommand / LongPressedCommand / ShowsPressEffect | {touch} | {code['TouchBehavior created in code']} |")
    if effects:
        out.append(f"| Effects | do not run; StrokeShape / Clip / Shadow or redraw | {effects} | |")
    out.append("")
    if tap_details:
        out.append("Tap recognizers: " + ", ".join(f"{name} {count}" for name, count in tap_details.most_common()) + ".\n")
    touch_names = sorted((name for name in behaviors if is_touch_behavior(name)), key=lambda name: -behaviors[name])
    if touch_names:
        out.append("TouchBehavior and subclasses: " + ", ".join(f"{name} {behaviors[name]}" for name in touch_names) + ".\n")
    other_behaviors = {name: count for name, count in behaviors.items() if not is_touch_behavior(name)}
    if other_behaviors:
        out.append("Other behaviors (run on drawn views unless they are platform behaviors): " +
                   ", ".join(f"{name} {count}" for name, count in sorted(other_behaviors.items(), key=lambda item: -item[1])) + ".\n")

    out.append("## Structure and platform code\n")
    out.append(f"- BindableLayout in XAML: {bindable_layouts} (works on drawn layouts; item templates and empty views are converted like the rest of the page)")
    out.append(f"- DataTemplate: {data_templates}; ControlTemplate: {control_templates} (drawn ControlTemplate: SkUiContentView and SkUiRadioButton; TemplateBinding becomes RelativeSource AncestorType)")
    for label in CS_PATTERNS:
        if code[label] and not label.startswith(("gesture recognizers", "TouchBehavior")):
            sample = ", ".join(sorted(code_files[label])[:5])
            more = f" (+{len(code_files[label]) - 5} files)" if len(code_files[label]) > 5 else ""
            out.append(f"- {label}: {code[label]} in {sample}{more}")
    out.append("")

    if third_party:
        out.append("## Third-party controls in XAML\n")
        out.append("They stay native: host them in SkUiMauiContentView or keep them outside drawn regions.\n")
        out.append("| Control | Count |\n| --- | ---: |")
        for name, count in third_party.most_common(args.top):
            out.append(f"| {name} | {count} |")
        out.append("")

    if custom_usage:
        out.append("## Most used custom controls\n")
        out.append("Porting a shared control to a drawn view (SkUiContentView / SkUiView subclass) converts every page that uses it.\n")
        out.append("| Control | Uses | Base types |\n| --- | ---: | --- |")
        for name, count in custom_usage.most_common(args.top):
            chain = index.chain(name)
            drawn = " (drawn already)" if any(base.startswith("SkUi") for base in chain) else ""
            out.append(f"| {name} | {count} | {' → '.join(chain[:4]) or 'not found'}{drawn} |")
        out.append("")

    ranked = sorted((info for info in infos if info.views), key=lambda info: -info.views)
    out.append("## Pages and views by size\n")
    out.append("Views = MAUI, third-party and custom control elements in the file. Blockers have no drawn equivalent yet; conversions are mechanical.\n")
    out.append("| File | Root | Views | Blockers | Conversions |\n| --- | --- | ---: | --- | --- |")
    for info in ranked[:args.top]:
        out.append(f"| {info.path} | {info.root_name} | {info.views} | {', '.join(info.blockers) or '—'} | {', '.join(info.conversions) or '—'} |")
    out.append("")
    clean = [info for info in ranked if not info.blockers and (info.root_name in PAGES or index.derives(info.root_name, PAGES))]
    if clean:
        out.append("Pages without blockers, largest first: " + ", ".join(f"{info.path} ({info.views})" for info in clean[:10]) + ".\n")

    if unparsed:
        out.append("## Not parsed\n")
        for path, error in unparsed:
            out.append(f"- {os.path.relpath(path, root)}: {error}")
        out.append("")

    sys.stdout.write("\n".join(out) + "\n")


if __name__ == "__main__":
    main()
