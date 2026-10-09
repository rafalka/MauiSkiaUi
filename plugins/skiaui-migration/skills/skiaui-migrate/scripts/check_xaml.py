#!/usr/bin/env python3
"""Checks SkiaUi XAML for MAUI content that drawn trees do not run.

Usage: check_xaml.py [--root DIR] PATH [PATH ...]

PATH is a .xaml file or a folder (searched recursively). --root is the project folder indexed for custom controls and
styles (default: the current folder). Prints `file:line: error|warning: message` and exits with 1 when there are errors.

A drawn tree is everything inside a SkiaUi view (an element from the MauiSkiaUi XAML namespace). Inside one, it reports:
- native MAUI views that are not wrapped in SkUiMauiContentView, with the drawn replacement;
- custom controls that do not derive from a SkiaUi view;
- gesture recognizers drawn views do not run (all but TapGestureRecognizer with 1 or 2 taps and the primary button);
- behaviors (platform behaviors such as TouchBehavior do not run) and effects (never run);
- BindableLayout content that is not drawn (item and empty view templates are checked like other drawn content; a string
  EmptyView becomes a MAUI Label), the Community Toolkit's StateContainer / StateView on drawn layouts (SkUiStateContainer /
  SkUiStateView; state views are checked like other drawn content), the Community Toolkit's Expander in a drawn tree
  (SkUiExpander; headers, content and content templates of drawn views are checked like other drawn content), MAUI's
  SwipeItemView in an SkUiSwipeView (SkUiSwipeItemView; MAUI's SwipeItems and SwipeItem are what it draws),
  IsClippedToBounds (ClipToBounds), TemplateBinding / RelativeSource TemplatedParent on drawn views (RelativeSource
  AncestorType), and styles that
  target MAUI types:
  keyed styles a drawn view uses, and implicit styles (no x:Key) of the MAUI type a drawn view replaces when no implicit
  style targets the drawn type (warned once per file and type; styles are indexed project-wide, so App.xaml's count).
"""

import argparse
import os
import re
import sys
import xml.sax
from xml.sax.handler import feature_namespaces

MAUI_NS = "http://schemas.microsoft.com/dotnet/2021/maui"
X_NS = "http://schemas.microsoft.com/winfx/2009/xaml"

# MAUI view -> (severity, advice)
REPLACE = {
    "ContentView": "SkUiContentView",
    "Grid": "SkUiGrid",
    "VerticalStackLayout": "SkUiVerticalStackLayout",
    "HorizontalStackLayout": "SkUiHorizontalStackLayout",
    "StackLayout": "SkUiVerticalStackLayout or SkUiHorizontalStackLayout (by Orientation)",
    "AbsoluteLayout": "SkUiAbsoluteLayout",
    "FlexLayout": "SkUiFlexLayout",
    "ScrollView": "SkUiScrollView",
    "Border": "SkUiBorder",
    "Frame": "SkUiBorder (CornerRadius -> StrokeShape=\"RoundRectangle N\", HasShadow -> Shadow, BorderColor -> Stroke)",
    "Label": "SkUiLabel",
    "Button": "SkUiButton",
    "Image": "SkUiImage",
    "ImageButton": "SkUiImageButton",
    "BoxView": "SkUiBox",
    "Ellipse": "SkUiEllipse",
    "Line": "SkUiLine",
    "Rectangle": "SkUiRectangle",
    "RoundRectangle": "SkUiRoundRectangle",
    "Path": "SkUiPath",
    "Polygon": "SkUiPolygon",
    "Polyline": "SkUiPolyline",
    "CheckBox": "SkUiCheckBox",
    "Switch": "SkUiSwitch",
    "RadioButton": "SkUiRadioButton",
    "Slider": "SkUiSlider",
    "ProgressBar": "SkUiProgressBar",
    "ActivityIndicator": "SkUiActivityIndicator",
    "ContentPresenter": "SkUiContentPresenter (inside a drawn ControlTemplate)",
    "GraphicsView": "a SkUiView subclass (MeasureContent + OnPaintContent)",
    "SwipeView": "SkUiSwipeView (SwipeItems and SwipeItem stay MAUI's)",
    "SwipeItemView": "SkUiSwipeItemView",
}
WRAP = {"Entry", "Editor", "SearchBar", "Picker", "DatePicker", "TimePicker", "WebView", "HybridWebView", "BlazorWebView", "Map"}
NO_DRAWN = {
    "CollectionView": "use sk:SkUiCollectionView (SkiaUi's own API: convert member by member, see references/collection-view.md); reorderable lists (CanReorderItems): keep the list outside the drawn tree",
    "ListView": "obsolete in MAUI, no drawn equivalent: keep it outside the drawn tree",
    "TableView": "obsolete in MAUI, no drawn equivalent: keep it outside the drawn tree",
    "CarouselView": "no drawn CarouselView yet: SkUiScrollView Orientation=\"Horizontal\" SnapPointsType=\"MandatorySingle\", or keep it outside",
    "IndicatorView": "no drawn IndicatorView yet: keep it outside, or draw dots with SkUiEllipse",
    "RefreshView": "around a list: SkUiCollectionView's IsPullToRefreshEnabled / IsRefreshing / RefreshCommand; around other content: put the MAUI RefreshView around the surface root",
    "Stepper": "no drawn Stepper yet: two SkUiButtons",
}
GESTURE_ADVICE = {
    "SwipeGestureRecognizer": "use SwipeDirections + Swiped / SwipedCommand (parameter = direction), or SkUiSwipeGestureRecognizer in Gestures",
    "PanGestureRecognizer": "use PanUpdated (MAUI's GestureStatus, TotalX / TotalY) and PanAxis",
    "PinchGestureRecognizer": "use PinchUpdated (Scale as on MAUI; Origin in DIPs instead of ScaleOrigin 0-1)",
    "PointerGestureRecognizer": "use IsPointerOver / the PointerOver visual state, or SkUiPointerGestureRecognizer in Gestures",
    "DragGestureRecognizer": "drag and drop is not available on drawn views",
    "DropGestureRecognizer": "drag and drop is not available on drawn views",
}
# MAUI CollectionView members an SkUiCollectionView does not have, with what to do instead.
COLLECTION_VIEW_MAUI_ONLY = {
    "ItemsLayout": "no ItemsLayout object: Orientation=\"Horizontal\", Span=\"<columns>\", ItemSpacing (between rows) and SpanSpacing (within a row); snap points are not available",
    "CanReorderItems": "reordering items is not available: keep a reorderable list native",
    "ItemSizingStrategy": "every item is measured; MeasureFirstItem becomes ItemExtent=\"<height>\"",
    "ItemsUpdatingScrollMode": "items inserted above the first visible one keep what shows in place; remove it",
    "ItemTemplateSelector": "set the DataTemplateSelector as ItemTemplate",
    "Command": "on a RefreshView this is RefreshCommand",
}
# CollectionView members that take a drawn view: a string value would become a MAUI label.
COLLECTION_VIEW_VIEWS = ("Header", "Footer", "EmptyView")
SKIA_BASE_RE = re.compile(r"^(SkUi\w+|ISkUiView)$")
TEMPLATED_PARENT_RE = re.compile(r"\{\s*TemplateBinding\b|RelativeSource\s+(?:Mode\s*=\s*)?TemplatedParent\b")
# Drawn view -> the MAUI types it replaces (implicit styles of those no longer reach it).
REPLACED_BY = {}
for maui_name, advice in REPLACE.items():
    for drawn_name in re.findall(r"SkUi\w+", advice):
        REPLACED_BY.setdefault(drawn_name, []).append(maui_name)
CONTENT_PROPERTIES = {"Content", "Children"}
# SkUiSwipeView's item sides: MAUI's SwipeItems of MAUI SwipeItems (drawn by the view) or drawn item views.
SWIPE_ITEMS_PROPERTIES = {"LeftItems", "RightItems", "TopItems", "BottomItems"}
# Property elements whose values are drawn content too (views, or templates of views).
DRAWN_VALUE_PROPERTIES = {"ControlTemplate", "ItemTemplate", "EmptyView", "EmptyViewTemplate", "StateViews", "Header",
                          "ContentTemplate", "AlternateContent", "AlternateContentTemplate"}
# Drawn views that make good surface roots: a region of the page. Any other drawn view directly in MAUI content is a
# surface of its own.
REGION_ROOTS = {"SkUiContentView", "SkUiLayout", "SkUiGrid", "SkUiVerticalStackLayout", "SkUiHorizontalStackLayout",
                "SkUiAbsoluteLayout", "SkUiFlexLayout", "SkUiWrapLayout", "SkUiHorizontalShrinkLayout",
                "SkUiVerticalShrinkLayout", "SkUiScrollView", "SkUiBorder", "SkUiCoreHost"}


class Node:
    __slots__ = ("uri", "name", "attrs", "line", "children", "parent")

    def __init__(self, uri, name, attrs, line, parent):
        self.uri, self.name, self.attrs, self.line, self.parent = uri or "", name, attrs, line, parent
        self.children = []

    @property
    def is_property_element(self):
        return "." in self.name

    def owner(self):
        """The nearest ancestor that is an object element (not a property element)."""
        node = self.parent
        while node is not None and node.is_property_element:
            node = node.parent
        return node

    def attr(self, local, uri=None):
        return self.attrs.get((uri, local))


class TreeBuilder(xml.sax.ContentHandler):
    def __init__(self):
        super().__init__()
        self.root = None
        self.stack = []
        self.locator = None
        self.prefixes = {}

    def setDocumentLocator(self, locator):
        self.locator = locator

    def startPrefixMapping(self, prefix, uri):
        self.prefixes.setdefault(uri, prefix)

    def startElementNS(self, name, qname, attrs):
        uri, local = name
        node = Node(uri, local, dict(attrs.items()),
                    self.locator.getLineNumber() if self.locator else 0, self.stack[-1] if self.stack else None)
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
    return handler.root


def is_skia_uri(uri):
    return "MauiSkiaUi" in uri


def is_toolkit_uri(uri):
    return "maui/toolkit" in uri or (clr_namespace(uri) or "").startswith("CommunityToolkit.Maui")


def clr_namespace(uri):
    match = re.match(r"(?:clr-namespace|using):([\w.]+)", uri)
    return match.group(1) if match else None


class ProjectIndex:
    """Class name -> base type name, from C# declarations and XAML x:Class roots; style keys -> target types; the target
    types of implicit styles."""

    CLASS_RE = re.compile(r"\bclass\s+(\w+)\s*(?:<[^>{]*>)?\s*:\s*([\w.]+)")

    def __init__(self, root):
        self.root = root
        self.bases = {}
        self.styles = {}
        self.implicit_styles = {}  # target type (no prefix) -> first "path:line" declaring one
        for folder, dirs, files in os.walk(root):
            dirs[:] = [d for d in dirs if d not in ("bin", "obj", ".git", "node_modules") and not d.startswith(".")]
            for file in files:
                path = os.path.join(folder, file)
                if file.endswith(".cs"):
                    self._index_cs(path)
                elif file.endswith(".xaml"):
                    self._index_xaml(path)

    def _index_cs(self, path):
        try:
            text = open(path, encoding="utf-8-sig", errors="replace").read()
        except OSError:
            return
        for name, base in self.CLASS_RE.findall(text):
            base = base.split(".")[-1]
            # Partial classes repeat without a base; interfaces after the base are ignored by the regex.
            self.bases.setdefault(name, base)

    def _index_xaml(self, path):
        try:
            root = parse(path)
        except Exception:
            return
        if root is None:
            return
        x_class = root.attr("Class", X_NS)
        if x_class:
            self.bases[x_class.split(".")[-1]] = root.name
        stack = [root]
        while stack:
            node = stack.pop()
            stack.extend(node.children)
            if node.name == "Style" and node.attr("TargetType"):
                if node.attr("Key", X_NS):
                    self.styles[node.attr("Key", X_NS)] = node.attr("TargetType")
                else:
                    target = node.attr("TargetType").split(":")[-1].strip()
                    self.implicit_styles.setdefault(target, f"{os.path.relpath(path, self.root)}:{node.line}")

    def drawn(self, name, seen=None):
        """True when `name` derives from a SkiaUi view, False when from something else, None when unknown."""
        seen = seen or set()
        while name and name not in seen:
            if SKIA_BASE_RE.match(name):
                return True
            seen.add(name)
            base = self.bases.get(name)
            if base is None:
                return None if name not in REPLACE and name not in WRAP and name not in NO_DRAWN and name not in ("View", "VisualElement", "Layout") else False
            name = base
        return None

    def platform_behavior(self, name):
        seen = set()
        while name and name not in seen:
            if name in ("TouchBehavior", "PlatformBehavior"):
                return True
            seen.add(name)
            name = self.bases.get(name)
        return False


class Checker:
    def __init__(self, index):
        self.index = index
        self.errors = 0
        self.warnings = 0
        self.implicit_reported = set()  # (path, drawn type) already warned

    def report(self, path, node, severity, message):
        if severity == "error":
            self.errors += 1
        else:
            self.warnings += 1
        print(f"{path}:{node.line}: {severity}: {message}")

    def check_file(self, path):
        try:
            root = parse(path)
        except Exception as error:
            self.report(path, Node("", "", {}, 0, None), "error", f"cannot parse: {error}")
            return
        self.visit(path, root, drawn=False)

    def visit(self, path, node, drawn):
        if node.is_property_element:
            owner, prop = node.name.rsplit(".", 1)
            if drawn and prop == "GestureRecognizers":
                for child in node.children:
                    self.check_gesture(path, child)
                return
            if drawn and prop == "Behaviors":
                for child in node.children:
                    self.check_behavior(path, child)
                return
            if drawn and prop == "Effects":
                for child in node.children:
                    self.report(path, child, "error", f"{child.name}: effects do not run on drawn views; use SkUiBorder StrokeShape, Clip or Shadow, or redraw the control")
                return
            if drawn and owner == "StateContainer" and not is_skia_uri(node.uri):
                self.report(path, node, "error", f"{node.name} on a drawn layout: use sk:SkUi{node.name}")
            if drawn and prop in SWIPE_ITEMS_PROPERTIES and owner == "SkUiSwipeView":
                for child in node.children:
                    self.check_swipe_items(path, child)
                return
            # Children of other property elements (StrokeShape, Shadow, Resources, VisualStateGroups, …) are values,
            # not content: only Content / Children (and templates) carry drawn content.
            if prop not in CONTENT_PROPERTIES and prop not in DRAWN_VALUE_PROPERTIES:
                for child in node.children:
                    self.visit(path, child, drawn=False)
                return
            for child in node.children:
                self.visit(path, child, drawn)
            return

        is_skia = is_skia_uri(node.uri)
        if is_skia:
            owner = node.owner()
            if not drawn and owner is not None and not is_skia_uri(owner.uri) and node.name not in REGION_ROOTS:
                self.report(path, node, "warning", f"{node.name} directly in MAUI content is a surface of its own: put the drawn region under one SkUiContentView or drawn layout instead of many single drawn views")
            self.check_attributes(path, node)
            self.check_implicit_styles(path, node)
            if node.name == "SkUiMauiContentView":
                for child in node.children:
                    if child.is_property_element:
                        self.visit(path, child, drawn=True)
                return  # native content is allowed inside
            for child in node.children:
                self.visit(path, child, drawn=True)
            return

        if drawn and node.uri == MAUI_NS:
            name = node.name
            if name in REPLACE:
                self.report(path, node, "error", f"native {name} inside a drawn tree: use {REPLACE[name]}")
            elif name in WRAP:
                self.report(path, node, "error", f"native {name} inside a drawn tree: wrap it in <sk:SkUiMauiContentView>")
            elif name in NO_DRAWN:
                self.report(path, node, "error", f"{name} inside a drawn tree: {NO_DRAWN[name]}")
            elif name in ("ControlTemplate", "DataTemplate"):
                for child in node.children:
                    self.visit(path, child, drawn=True)
                return
        elif drawn and is_toolkit_uri(node.uri) and node.name == "Expander":
            self.report(path, node, "error", "Community Toolkit Expander inside a drawn tree: use sk:SkUiExpander (Header and Content become drawn views; ExpandDirection / ExpandedChangedEventArgs become SkUiExpandDirection / SkUiExpandedChangedEventArgs)")
        elif drawn and clr_namespace(node.uri):
            kind = self.index.drawn(node.name)
            if kind is True:
                self.check_attributes(path, node)
            elif kind is False:
                self.report(path, node, "error", f"custom control {node.name} is not a drawn view: port it to a SkUiContentView / SkUiView subclass, or wrap it in <sk:SkUiMauiContentView>")
            elif kind is None:
                self.report(path, node, "warning", f"custom control {node.name}: base type not found; it must derive from a SkiaUi view (or be wrapped in <sk:SkUiMauiContentView>)")
        for child in node.children:
            # Below a non-drawn element, the drawn state continues only for drawn custom controls' content.
            self.visit(path, child, drawn and (node.uri != MAUI_NS or node.name in ("ControlTemplate", "DataTemplate")))

    def check_attributes(self, path, node):
        for (uri, local), value in node.attrs.items():
            if local.startswith(("StateContainer.", "StateView.")) and not is_skia_uri(uri or ""):
                self.report(path, node, "error", f"Community Toolkit {local} on {node.name}: use sk:SkUi{local} (the toolkit's StateContainer only runs on MAUI layouts)")
            elif local == "BindableLayout.EmptyView" and not value.lstrip().startswith("{"):
                self.report(path, node, "error", f"BindableLayout.EmptyView=\"{value}\" on {node.name}: a string empty view becomes a MAUI Label; use <BindableLayout.EmptyView><sk:SkUiLabel Text=\"{value}\" /></BindableLayout.EmptyView>")
            elif node.name == "SkUiCollectionView" and local in COLLECTION_VIEW_MAUI_ONLY:
                self.report(path, node, "error", f"{local} on SkUiCollectionView: {COLLECTION_VIEW_MAUI_ONLY[local]}")
            elif node.name == "SkUiCollectionView" and local in COLLECTION_VIEW_VIEWS and not value.lstrip().startswith("{"):
                self.report(path, node, "error", f"{local}=\"{value}\" on SkUiCollectionView: {local} takes a drawn view; use <sk:SkUiCollectionView.{local}><sk:SkUiLabel Text=\"{value}\" /></sk:SkUiCollectionView.{local}>")
            elif local == "IsClippedToBounds":
                self.report(path, node, "error", f"IsClippedToBounds on {node.name}: use ClipToBounds")
            elif TEMPLATED_PARENT_RE.search(value):
                self.report(path, node, "error", f"{local}=\"{value}\" on {node.name}: TemplateBinding / RelativeSource TemplatedParent do not reach drawn controls; use {{Binding Path, Source={{RelativeSource AncestorType={{x:Type local:TemplatedControl}}}}}}")
            elif local == "Style":
                key = re.match(r"\{\s*(?:StaticResource|DynamicResource)\s+(?:Key=)?\s*([\w.]+)\s*\}", value)
                target = self.index.styles.get(key.group(1)) if key else None
                if target and "SkUi" not in target:
                    self.report(path, node, "error", f"style {key.group(1)} targets {target}, which does not apply to {node.name}: add a style with TargetType=\"sk:{node.name}\"")

    def check_implicit_styles(self, path, node):
        if node.name in self.index.implicit_styles or (path, node.name) in self.implicit_reported:
            return
        for maui_name in REPLACED_BY.get(node.name, ()):
            declared = self.index.implicit_styles.get(maui_name)
            if declared:
                self.implicit_reported.add((path, node.name))
                self.report(path, node, "warning", f"the implicit {maui_name} style ({declared}) does not apply to {node.name}: add <Style TargetType=\"sk:{node.name}\"> with the same setters")
                return

    def check_swipe_items(self, path, node):
        """An SkUiSwipeView side: MAUI's SwipeItems (or one item set directly) of SwipeItems and drawn item views."""
        if node.uri == MAUI_NS and node.name == "SwipeItems":
            for child in node.children:
                if not child.is_property_element:
                    self.check_swipe_items(path, child)
        elif node.uri == MAUI_NS and node.name == "SwipeItem":
            return
        elif node.uri == MAUI_NS and node.name == "SwipeItemView":
            self.report(path, node, "error", "SwipeItemView in an SkUiSwipeView: use sk:SkUiSwipeItemView with drawn content (MAUI's hosts native views)")
        else:
            self.visit(path, node, drawn=True)

    def check_gesture(self, path, node):
        name = node.name
        if name == "TapGestureRecognizer":
            taps = node.attr("NumberOfTapsRequired") or "1"
            buttons = node.attr("Buttons") or "Primary"
            if taps not in ("1", "2"):
                self.report(path, node, "error", f"TapGestureRecognizer NumberOfTapsRequired={taps}: drawn views run 1 or 2 taps")
            elif "Primary" not in buttons:
                self.report(path, node, "error", f"TapGestureRecognizer Buttons={buttons}: drawn taps are primary-button taps")
            return
        advice = GESTURE_ADVICE.get(name, "not run on drawn views; use the view's gesture events")
        self.report(path, node, "error", f"{name} is not run on drawn views: {advice}")

    def check_behavior(self, path, node):
        if node.name == "TouchBehavior" or self.index.platform_behavior(node.name):
            self.report(path, node, "error", f"{node.name} is a platform behavior and does not run on drawn views: use TappedCommand / LongPressedCommand (+ parameters) and ShowsPressEffect or a Pressed visual state")
        else:
            self.report(path, node, "warning", f"{node.name}: behaviors run on drawn views only if they are not platform behaviors (no native view)")


def xaml_files(paths):
    for path in paths:
        if os.path.isdir(path):
            for folder, dirs, files in os.walk(path):
                dirs[:] = [d for d in dirs if d not in ("bin", "obj", ".git") and not d.startswith(".")]
                for file in sorted(files):
                    if file.endswith(".xaml"):
                        yield os.path.join(folder, file)
        else:
            yield path


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("paths", nargs="+")
    parser.add_argument("--root", default=".", help="project folder indexed for custom controls and styles")
    args = parser.parse_args()
    checker = Checker(ProjectIndex(args.root))
    for path in xaml_files(args.paths):
        checker.check_file(path)
    print(f"{checker.errors} error(s), {checker.warnings} warning(s)", file=sys.stderr)
    sys.exit(1 if checker.errors else 0)


if __name__ == "__main__":
    main()
