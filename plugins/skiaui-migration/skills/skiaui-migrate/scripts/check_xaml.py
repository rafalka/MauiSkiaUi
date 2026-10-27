#!/usr/bin/env python3
"""Checks SkiaUi XAML for MAUI content that drawn trees do not run.

Usage: check_xaml.py [--root DIR] [--fix [--all-platforms]] PATH [PATH ...]

PATH is a .xaml file or a folder (searched recursively). --root is the project folder indexed for custom controls and
styles (default: the current folder). Prints `file:line: error|warning: message` and exits with 1 when there are errors.

--fix rewrites the XAML the documented conversions cover, in place, and prints `file:line: fixed: message` for each:
- {TemplateBinding X} and RelativeSource TemplatedParent in a ControlTemplate of a drawn control become
  {Binding X, Source={RelativeSource AncestorType={x:Type local:Control}}} (the template's TargetType, its Style's, or the
  control whose ControlTemplate it is);
- a data header (Header / Footer / EmptyView="{Binding Info}" on SkUiCollectionView, EmptyView on SkUiCarouselView) with
  an inline template: the attribute goes and the template binds through the list's context ({Binding Title} becomes
  {Binding Info.Title});
- platform-specifics with a drawn member: iOS Entry.CursorColor -> CaretColor, Android Entry.ImeOptions -> ReturnType,
  iOS Picker / DatePicker / TimePicker UpdateMode -> UpdateMode, as {OnPlatform iOS=..., MacCatalyst=...} (Android=...)
  so they still apply on that platform only; --all-platforms writes the plain value instead.
What it cannot convert stays reported.

A drawn tree is everything inside a SkiaUi view (an element from the MauiSkiaUi XAML namespace). Inside one, it reports:
- native MAUI views that are not wrapped in SkUiMauiContentView, with the drawn replacement;
- custom controls that do not derive from a SkiaUi view;
- gesture recognizers drawn views do not run (drag and drop, pans with TouchPoints other than 1, taps without a count or a
  primary or secondary button);
- behaviors (platform behaviors such as TouchBehavior do not run) and effects (never run);
- BindableLayout content that is not drawn (item and empty view templates are checked like other drawn content; a string
  EmptyView becomes a MAUI Label), the Community Toolkit's StateContainer / StateView on drawn layouts (SkUiStateContainer /
  SkUiStateView; state views are checked like other drawn content), the Community Toolkit's Expander in a drawn tree
  (SkUiExpander; headers, content and content templates of drawn views are checked like other drawn content), the
  Community Toolkit's AvatarView in a drawn tree (SkUiAvatarView), MAUI's
  SwipeItemView in an SkUiSwipeView (SkUiSwipeItemView; MAUI's SwipeItems and SwipeItem are what it draws), an
  SkUiCollectionView with its own pull-to-refresh inside an SkUiRefreshView (both would refresh),
  TemplateBinding / RelativeSource TemplatedParent on drawn views (RelativeSource
  AncestorType), platform-specifics on drawn views (ignored: the ones with a drawn member convert), and styles that
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
from xml.sax.saxutils import escape

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
    "RefreshView": "SkUiRefreshView (around a list, SkUiCollectionView's own IsPullToRefreshEnabled also works)",
    "CarouselView": "SkUiCarouselView (ItemsLayout ports as is)",
    "IndicatorView": "SkUiIndicatorView (the look draws the indicators; IndicatorTemplate makes a drawn view per indicator)",
    "Picker": "SkUiPicker (its list opens on the app's popup system: register a presenter with UseSkiaUi(options => options.PopupPresenter = ...) or SkUiPopups.Presenter; without one, MAUI's action sheet)",
    "DatePicker": "SkUiDatePicker (same Date / MinimumDate / MaximumDate / Format / DateSelected; its wheels open on the app's popup system: register a presenter with UseSkiaUi(options => options.PopupPresenter = ...) or SkUiPopups.Presenter)",
    "Entry": "SkUiEntry (drawn; the platform keyboard types into it: CaretColor / SelectionColor / the look replace handler tweaks, AutofillHint replaces content types)",
    "Editor": "SkUiEditor (drawn; AutoSize as MAUI's)",
    "SearchBar": "SkUiSearchBar (drawn; same SearchCommand / SearchButtonPressed)",
    "Stepper": "SkUiStepper (same Value / Minimum / Maximum / Increment / ValueChanged; Orientation=Vertical stacks the buttons)",
    "TimePicker": "SkUiTimePicker (same Time / Format / TimeSelected; its wheels open on the app's popup system: register a presenter with UseSkiaUi(options => options.PopupPresenter = ...) or SkUiPopups.Presenter)",
}
WRAP = {"WebView", "HybridWebView", "BlazorWebView", "Map"}
NO_DRAWN = {
    "CollectionView": "use sk:SkUiCollectionView (MAUI's ItemsLayout, ScrollTo and ItemsUpdatingScrollMode port; SelectionChanged / Scrolled / SelectionMode in C# convert, see references/collection-view.md); reorderable lists (CanReorderItems): keep the list outside the drawn tree",
    "ListView": "obsolete in MAUI, no drawn equivalent: keep it outside the drawn tree",
    "TableView": "obsolete in MAUI, no drawn equivalent: keep it outside the drawn tree",
}
GESTURE_ADVICE = {
    "DragGestureRecognizer": "drag and drop is not available on drawn views",
    "DropGestureRecognizer": "drag and drop is not available on drawn views",
}
# MAUI CollectionView members an SkUiCollectionView does not have, with what to do instead.
COLLECTION_VIEW_MAUI_ONLY = {
    "CanReorderItems": "reordering items is not available: keep a reorderable list native",
    "ItemTemplateSelector": "set the DataTemplateSelector as ItemTemplate",
    "Command": "on a RefreshView this is RefreshCommand",
}
# MAUI CarouselView / IndicatorView members the drawn ones do not have, with what to do instead.
CAROUSEL_MAUI_ONLY = {
    "VisibleViews": "not available: use GetRealizedView(index) or the Scrolled event",
}
INDICATOR_MAUI_ONLY = {}
SKIA_BASE_RE = re.compile(r"^(SkUi\w+|ISkUiView)$")
# Drawn views or text in XAML; MAUI's data form (a bound object shown through the template) converts.
DATA_VIEW_PROPERTIES = {"SkUiCollectionView": ("Header", "Footer", "EmptyView"), "SkUiCarouselView": ("EmptyView",)}
BINDING_RE = re.compile(r"^\{\s*Binding\b")

TEMPLATED_PARENT_RE = re.compile(r"\{\s*TemplateBinding\b|RelativeSource\s+(?:Mode\s*=\s*)?TemplatedParent\b")
RELATIVE_TEMPLATED_PARENT_RE = re.compile(r"RelativeSource\s+(?:Mode\s*=\s*)?TemplatedParent\b")
# Bindings whose source is not the binding context (left alone when a data header's template is rebound).
OWN_SOURCE_RE = re.compile(r"\b(?:Source|RelativeSource|ElementName|x:Reference)\b")
# A first argument that names the path (Path=...), not a positional path that starts with "Path".
PATH_ARGUMENT_RE = re.compile(r"Path\s*=")
PLATFORM_NS = {"iOSSpecific": "iOS", "AndroidSpecific": "Android", "WindowsSpecific": "Windows"}
# (platform, attached property) -> (drawn member, value map or None for the same value); OnPlatform names per platform.
PLATFORM_CONVERSIONS = {
    ("iOS", "Entry.CursorColor"): ("CaretColor", None),
    ("Android", "Entry.ImeOptions"): ("ReturnType", {"Done": "Done", "Go": "Go", "Next": "Next", "Search": "Search", "Send": "Send"}),
    ("iOS", "Picker.UpdateMode"): ("UpdateMode", None),
    ("iOS", "DatePicker.UpdateMode"): ("UpdateMode", None),
    ("iOS", "TimePicker.UpdateMode"): ("UpdateMode", None),
}
PLATFORM_ADVICE = {
    ("iOS", "Entry.AdjustsFontSizeToFitWidth"): "the drawn entry does not shrink its text: drop it (a smaller FontSize fits long text)",
}
ON_PLATFORM_NAMES = {"iOS": ("iOS", "MacCatalyst"), "Android": ("Android",), "Windows": ("WinUI",)}
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
                "SkUiVerticalShrinkLayout", "SkUiHorizontalAlignStackLayout", "SkUiScrollView", "SkUiBorder", "SkUiCoreHost"}


class Node:
    __slots__ = ("uri", "name", "attrs", "line", "children", "parent", "qname", "qnames", "prefixes")

    def __init__(self, uri, name, attrs, line, parent, qname=None, qnames=None):
        self.uri, self.name, self.attrs, self.line, self.parent = uri or "", name, attrs, line, parent
        self.qname, self.qnames = qname or name, qnames or {}
        self.children = []
        self.prefixes = None  # the root's: namespace URI -> prefix

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
        self.current = {}  # namespace URI -> the prefix in scope (expat reports no qualified names)

    def setDocumentLocator(self, locator):
        self.locator = locator

    def startPrefixMapping(self, prefix, uri):
        self.prefixes.setdefault(uri, prefix)
        self.current[uri] = prefix

    def qualified(self, uri, local):
        prefix = self.current.get(uri) if uri else None
        return f"{prefix}:{local}" if prefix else local

    def startElementNS(self, name, qname, attrs):
        uri, local = name
        node = Node(uri, local, dict(attrs.items()),
                    self.locator.getLineNumber() if self.locator else 0, self.stack[-1] if self.stack else None,
                    self.qualified(uri, local), {key: self.qualified(*key) for key in attrs.keys()})
        if self.stack:
            self.stack[-1].children.append(node)
        else:
            self.root = node
            node.prefixes = self.prefixes
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


def root_of(node):
    while node.parent is not None:
        node = node.parent
    return node


def find_markup(value, start):
    """The end (exclusive) of the markup extension whose '{' is at `start`, braces balanced; -1 when unbalanced."""
    depth = 0
    for index in range(start, len(value)):
        if value[index] == "{":
            depth += 1
        elif value[index] == "}":
            depth -= 1
            if depth == 0:
                return index + 1
    return -1


def split_arguments(body):
    """A markup extension's arguments at the top level (commas inside nested braces stay)."""
    parts, depth, current = [], 0, ""
    for char in body:
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
        if char == "," and depth == 0:
            parts.append(current.strip())
            current = ""
        else:
            current += char
    if current.strip():
        parts.append(current.strip())
    return parts


def rewrite_template_bindings(value, ancestor):
    """{TemplateBinding X, …} -> {Binding X, …, Source={RelativeSource AncestorType=…}}; TemplatedParent -> AncestorType."""
    source = "{RelativeSource AncestorType={x:Type " + ancestor + "}}"
    result, index = "", 0
    while True:
        match = re.compile(r"\{\s*TemplateBinding\b").search(value, index)
        if not match:
            break
        end = find_markup(value, match.start())
        if end < 0:
            return None
        body = value[match.end():end - 1].strip()
        arguments = split_arguments(body)
        result += value[index:match.start()] + "{Binding " + ", ".join(arguments + ["Source=" + source]) + "}"
        index = end
    result += value[index:]
    return RELATIVE_TEMPLATED_PARENT_RE.sub("RelativeSource AncestorType={x:Type " + ancestor + "}", result)


def prefix_binding(value, path):
    """{Binding Title} -> {Binding Info.Title} for a template bound through the list's context; None to leave it."""
    stripped = value.strip()
    match = re.match(r"\{\s*Binding\b", stripped)
    if not match or find_markup(stripped, 0) != len(stripped):
        return None
    arguments = split_arguments(stripped[match.end():-1])
    if any(OWN_SOURCE_RE.match(argument) for argument in arguments):
        return None
    named_path = bool(arguments) and PATH_ARGUMENT_RE.match(arguments[0]) is not None
    if not arguments or "=" in arguments[0] and not named_path:
        arguments.insert(0, path)
    elif named_path:
        key, _, own = arguments[0].partition("=")
        own = own.strip()
        arguments[0] = f"{key.strip()}={path if own in ('', '.') else path + '.' + own}"
    else:
        own = arguments[0]
        arguments[0] = path if own == "." else f"{path}.{own}"
    return "{Binding " + ", ".join(arguments) + "}"


class Fixes:
    """Attribute edits per file, applied to the start tags in place (the rest of the file is kept as written)."""

    def __init__(self):
        self.edits = {}  # path -> [(line, element qname, attribute qname, new attribute text or None)]

    def add(self, path, node, attribute, text):
        self.edits.setdefault(path, []).append((node.line, node.qname, attribute, text))

    def apply(self):
        for path, edits in self.edits.items():
            # As written: the byte order mark and the line endings stay.
            with open(path, "rb") as file:
                bom = file.read(3) == b"\xef\xbb\xbf"
            with open(path, encoding="utf-8-sig", newline="") as file:
                source = file.read()
            # From the end: an edit never moves the lines of the ones still to do.
            for line, element, attribute, text in sorted(edits, key=lambda edit: -edit[0]):
                source = self._apply(source, line, element, attribute, text)
            with open(path, "w", encoding="utf-8-sig" if bom else "utf-8", newline="") as file:
                file.write(source)

    @staticmethod
    def _apply(source, line, element, attribute, text):
        offset = 0
        for _ in range(line - 1):
            offset = source.index("\n", offset) + 1
        # The parser reports the line the start tag begins on.
        match = re.compile(r"<" + re.escape(element) + r"(?=[\s/>])").search(source, offset)
        if match is None:
            return source
        end, quote = match.end(), None
        while end < len(source):
            char = source[end]
            if quote:
                quote = None if char == quote else quote
            elif char in "\"'":
                quote = char
            elif char == ">":
                break
            end += 1
        start_tag = source[match.start():end]
        found = re.search(r"(\s+)" + re.escape(attribute) + r"\s*=\s*(\"[^\"]*\"|'[^']*')", start_tag)
        if not found:
            return source
        replacement = "" if text is None else found.group(1) + text
        start_tag = start_tag[:found.start()] + replacement + start_tag[found.end():]
        return source[:match.start()] + start_tag + source[end:]


def attribute_text(name, value):
    return name + '="' + escape(value, {'"': "&quot;"}) + '"'


def is_skia_uri(uri):
    return "MauiSkiaUi" in uri


def in_refresh_view(node):
    """Whether an SkUiRefreshView encloses the element."""
    parent = node.parent
    while parent is not None:
        if parent.name == "SkUiRefreshView" and is_skia_uri(parent.uri):
            return True
        parent = parent.parent
    return False


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
    def __init__(self, index, fixes=None, all_platforms=False):
        self.index = index
        self.fixes = fixes
        self.all_platforms = all_platforms
        self.errors = 0
        self.warnings = 0
        self.fixed = 0
        self.implicit_reported = set()  # (path, drawn type) already warned

    def report(self, path, node, severity, message):
        if severity == "error":
            self.errors += 1
        elif severity == "fixed":
            self.fixed += 1
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
        self.check_templated_parent(path, root, drawn_nodes=self.drawn_nodes(root))

    def drawn_nodes(self, root):
        """The ids of the elements visit() treated as drawn (TemplateBinding is reported on those)."""
        found = set()

        def walk(node, drawn):
            if node.is_property_element:
                prop = node.name.rsplit(".", 1)[1]
                for child in node.children:
                    walk(child, drawn and (prop in CONTENT_PROPERTIES or prop in DRAWN_VALUE_PROPERTIES))
                return
            is_drawn = is_skia_uri(node.uri) or (drawn and clr_namespace(node.uri) and self.index.drawn(node.name) is True)
            if is_drawn or drawn:
                found.add(id(node))
            for child in node.children:
                walk(child, is_drawn or (drawn and (node.uri != MAUI_NS or node.name in ("ControlTemplate", "DataTemplate"))))

        walk(root, False)
        return found

    def templated_type(self, node):
        """The prefixed type a ControlTemplate around `node` templates, and whether it is a drawn control (None: unknown)."""
        current = node
        while current is not None:
            if current.name == "ControlTemplate" and current.uri == MAUI_NS:
                target = current.attr("TargetType")
                owner = current.parent
                if not target and owner is not None and owner.is_property_element and owner.name.endswith(".ControlTemplate"):
                    target = owner.qname.rsplit(".", 1)[0]
                if not target:
                    setter = owner if owner is not None and owner.name == "Setter" else (owner.parent if owner is not None and owner.name == "Setter.Value" else None)
                    style = setter.parent if setter is not None else None
                    while style is not None and style.name != "Style":
                        style = style.parent
                    target = style.attr("TargetType") if style is not None else None
                if not target:
                    return None, None
                target = re.sub(r"^\{\s*x:Type\s+(.*?)\s*\}$", r"\1", target.strip())
                local = target.split(":")[-1]
                return target, True if SKIA_BASE_RE.match(local) else self.index.drawn(local)
            current = current.parent
        return None, None

    def check_templated_parent(self, path, root, drawn_nodes):
        x_prefix = (root.prefixes or {}).get(X_NS)
        stack = [root]
        while stack:
            node = stack.pop()
            stack.extend(node.children)
            for key, value in node.attrs.items():
                if not TEMPLATED_PARENT_RE.search(value):
                    continue
                local = key[1]
                target, drawn = self.templated_type(node)
                if drawn is not True and id(node) not in drawn_nodes:
                    continue  # a native control's template: MAUI resolves it
                fixed = rewrite_template_bindings(value, target) if target and drawn is True and x_prefix == "x" else None
                if fixed is not None and self.fixes is not None:
                    self.fixes.add(path, node, node.qnames.get(key, local), attribute_text(node.qnames.get(key, local), fixed))
                    self.report(path, node, "fixed", f"{local}=\"{value}\" on {node.name} -> \"{fixed}\"")
                elif fixed is not None:
                    self.report(path, node, "error", f"{local}=\"{value}\" on {node.name}: TemplateBinding / RelativeSource TemplatedParent do not reach drawn controls; use \"{fixed}\" (--fix rewrites it)")
                else:
                    self.report(path, node, "error", f"{local}=\"{value}\" on {node.name}: TemplateBinding / RelativeSource TemplatedParent do not reach drawn controls; use {{Binding Path, Source={{RelativeSource AncestorType={{x:Type local:TemplatedControl}}}}}}")

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
            if drawn and owner == "SkUiCollectionView" and prop == "ItemsLayout":
                return  # MAUI's items layouts (snap points included) port as they are
            if drawn and owner == "SkUiCarouselView" and prop in CAROUSEL_MAUI_ONLY:
                self.report(path, node, "error", f"{node.name}: {CAROUSEL_MAUI_ONLY[prop]}")
                return
            if drawn and owner == "SkUiIndicatorView" and prop in INDICATOR_MAUI_ONLY:
                self.report(path, node, "error", f"{node.name}: {INDICATOR_MAUI_ONLY[prop]}")
                return
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
        elif drawn and is_toolkit_uri(node.uri) and node.name == "AvatarView":
            self.report(path, node, "error", "Community Toolkit AvatarView inside a drawn tree: use sk:SkUiAvatarView (same properties; corners are fully rounded until CornerRadius is set, the background and text color come from the look until set)")
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
            elif node.name == "SkUiCarouselView" and local in CAROUSEL_MAUI_ONLY:
                self.report(path, node, "error", f"{local} on SkUiCarouselView: {CAROUSEL_MAUI_ONLY[local]}")
            elif node.name == "SkUiIndicatorView" and local in INDICATOR_MAUI_ONLY:
                self.report(path, node, "error", f"{local} on SkUiIndicatorView: {INDICATOR_MAUI_ONLY[local]}")
            elif node.name == "SkUiCollectionView" and local in COLLECTION_VIEW_MAUI_ONLY:
                self.report(path, node, "error", f"{local} on SkUiCollectionView: {COLLECTION_VIEW_MAUI_ONLY[local]}")
            elif local in DATA_VIEW_PROPERTIES.get(node.name, ()) and BINDING_RE.match(value.strip()):
                self.check_data_view(path, node, local, value)
            elif PLATFORM_NS.get((clr_namespace(uri or "") or "").rsplit(".", 1)[-1]):
                self.check_platform_specific(path, node, (uri, local), value)
            elif node.name == "SkUiCollectionView" and local == "IsPullToRefreshEnabled" and value.strip().lower() != "false" and in_refresh_view(node):
                self.report(path, node, "error", "IsPullToRefreshEnabled on an SkUiCollectionView inside an SkUiRefreshView: both would refresh; keep one (the refresh view's Command, or the list's RefreshCommand)")
            elif local == "Style":
                key = re.match(r"\{\s*(?:StaticResource|DynamicResource)\s+(?:Key=)?\s*([\w.]+)\s*\}", value)
                target = self.index.styles.get(key.group(1)) if key else None
                if target and "SkUi" not in target:
                    self.report(path, node, "error", f"style {key.group(1)} targets {target}, which does not apply to {node.name}: add a style with TargetType=\"sk:{node.name}\"")

    def check_data_view(self, path, node, local, value):
        """Header="{Binding Info}" with an inline HeaderTemplate: the template binds through the list's context instead."""
        warning = f"{local}=\"{value}\" on {node.name}: data is not shown through {local}Template here (unless the binding yields a drawn view); drop it and bind the template through the list's context ({{Binding Info.Title}} instead of {{Binding Title}})"
        arguments = split_arguments(value.strip()[len("{Binding"):-1].strip()) if value.strip().endswith("}") else []
        path_argument = arguments[0] if len(arguments) == 1 else None
        if path_argument and path_argument.startswith("Path="):
            path_argument = path_argument[len("Path="):].strip()
        template = next((child for child in node.children if child.is_property_element and child.name == f"{node.name}.{local}Template"), None)
        roots = [child for child in template.children if not child.is_property_element] if template is not None else []
        rebind = []
        if path_argument and re.fullmatch(r"[\w.\[\]]+", path_argument) and roots:
            stack = list(roots)
            while stack:
                current = stack.pop()
                if current.attr("DataType", X_NS):
                    rebind = None  # compiled bindings: x:DataType would have to become the list's view model
                    break
                for key, attribute in current.attrs.items():
                    rebound = prefix_binding(attribute, path_argument)
                    if rebound is not None:
                        rebind.append((current, key, rebound))
                for child in current.children:
                    # Nested templates bind to their own items.
                    if not (child.uri == MAUI_NS and child.name in ("DataTemplate", "ControlTemplate")):
                        stack.append(child)
        if not path_argument or not roots or rebind is None or self.fixes is None:
            advice = " (--fix rewrites it)" if path_argument and roots and rebind is not None else ""
            self.report(path, node, "warning", warning + advice)
            return
        self.fixes.add(path, node, node.qnames.get((None, local), local), None)
        for current, key, rebound in rebind:
            name = current.qnames.get(key, key[1])
            self.fixes.add(path, current, name, attribute_text(name, rebound))
        self.report(path, node, "fixed", f"{local}=\"{value}\" on {node.name}: removed; {len(rebind)} binding(s) in {local}Template now start at {path_argument}")

    def check_platform_specific(self, path, node, key, value):
        """An attached platform-specific on a drawn view: ignored, unless SkiaUi has a member for it (converted)."""
        uri, local = key
        platform = PLATFORM_NS[clr_namespace(uri).rsplit(".", 1)[-1]]
        conversion = PLATFORM_CONVERSIONS.get((platform, local))
        if conversion is None:
            advice = PLATFORM_ADVICE.get((platform, local), "drawn views do not read platform-specifics; remove it (or keep this part native)")
            self.report(path, node, "warning", f"{platform} platform-specific {local} on {node.name} is ignored: {advice}")
            return
        member, values = conversion
        mapped = value if values is None or value.strip().startswith("{") else values.get(value.strip())
        if mapped is None:
            self.report(path, node, "warning", f"{platform} platform-specific {local}=\"{value}\" on {node.name} is ignored: {member} has no such value; set {member} instead")
            return
        if node.attr(member) is not None:
            self.report(path, node, "warning", f"{platform} platform-specific {local} on {node.name} is ignored: {member} is set too; remove {local}")
            return
        converted = mapped if self.all_platforms else "{OnPlatform " + ", ".join(f"{name}={mapped}" for name in ON_PLATFORM_NAMES[platform]) + "}"
        qname = node.qnames.get(key, local)
        if self.fixes is not None:
            self.fixes.add(path, node, qname, attribute_text(member, converted))
            self.report(path, node, "fixed", f"{qname}=\"{value}\" on {node.name} -> {member}=\"{converted}\"")
        else:
            self.report(path, node, "error", f"{qname}=\"{value}\" on {node.name} is ignored by drawn views: use {member}=\"{converted}\" (--fix rewrites it)")

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
            if taps.isdigit() and int(taps) < 1:
                self.report(path, node, "error", f"TapGestureRecognizer NumberOfTapsRequired={taps}: it never taps")
            elif not buttons.startswith("{") and "Primary" not in buttons and "Secondary" not in buttons:
                self.report(path, node, "error", f"TapGestureRecognizer Buttons={buttons}: drawn taps are primary- or secondary-button taps")
            return
        if name == "PanGestureRecognizer":
            points = node.attr("TouchPoints") or "1"
            if points != "1" and not points.startswith("{"):
                self.report(path, node, "error", f"PanGestureRecognizer TouchPoints={points}: drawn pans follow one pointer; use PinchUpdated or SkUiPointerGestureRecognizer in Gestures")
            return
        if name in ("SwipeGestureRecognizer", "PinchGestureRecognizer", "PointerGestureRecognizer"):
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
    parser.add_argument("--fix", action="store_true", help="rewrite the XAML the documented conversions cover, in place")
    parser.add_argument("--all-platforms", action="store_true", help="with --fix: platform-specifics become plain values, not OnPlatform")
    args = parser.parse_args()
    fixes = Fixes() if args.fix else None
    checker = Checker(ProjectIndex(args.root), fixes, args.all_platforms)
    for path in xaml_files(args.paths):
        checker.check_file(path)
    if fixes is not None:
        fixes.apply()
    print(f"{checker.errors} error(s), {checker.warnings} warning(s), {checker.fixed} fixed", file=sys.stderr)
    sys.exit(1 if checker.errors else 0)


if __name__ == "__main__":
    main()
