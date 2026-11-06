# Glossary

Terms used across the code, the contributor documents and the user guide. A term here is the
preferred word; the `_Avoid_:` list names the variants that must not appear in new text, as the
[documentation policy](docs/policies/documentation.md) requires.

## Plugin

A Revit extension registered by a `.addin` manifest: a DLL plus its registration, shown as one row in
the plugin list. The user-facing word, used in the user guide and in the interface text.
_Avoid_: add-in in user-facing text, addin (as a word), extension.

## Add-in

The Autodesk name for a plugin, kept where it is part of an external name: the `.addin` file
extension, the `AddInId` element, the `Autodesk\Revit\Addins` folder. Not a synonym for the product.
_Avoid_: using it for the product or the application itself.

## Manifest

The `.addin` XML file that registers one or more plugins: one XML document under a Revit version and
scope.
_Avoid_: config file, settings file.

## Entry

One `<AddIn>` element inside a manifest: a single registered plugin with its own `AddInId`, assembly
and class. A manifest has zero or more entries.
_Avoid_: row, item, record.

## Scope

The installation root a manifest lives in: `User` (`%APPDATA%\Autodesk\Revit\Addins`) or `Machine`
(`%PROGRAMDATA%\Autodesk\Revit\Addins`).
_Avoid_: location, area.

## Revit version

The Revit release a manifest belongs to, named by year (`2021` … `2027`). Not the product version of
the manager.
_Avoid_: Revit release, Revit year (except where the year is meant literally).

## Disabled file

A manifest that was moved into the `disabled/` subfolder of its version and scope, so Revit no longer
loads it. Enable and disable move the file; they do not edit its content.
_Avoid_: off file, turned-off plugin.

## Conflict

A condition the manager reports but does not fix silently: a duplicate `AddInId` within one manifest
or across manifests of the same Revit version, a missing `Assembly` or `FullClassName`, or an empty
manifest. A duplicate across different Revit versions is not a conflict.
_Avoid_: error, warning (use the specific reason), duplicate (for the condition itself).

## Markup

The raw XML text of a manifest as the editor shows it, as opposed to the structured form that edits
one entry or the manifest settings.
_Avoid_: raw view, source code.

## Markup editor

The raw-XML editing pane of the launcher, with its find bar. A manifest may be edited through the
structured form or the markup editor, not both at once.
_Avoid_: code editor, XML editor (when the feature is meant).

## Manifest settings

The file-level `<ManifestSettings>` block (Revit context and context name), as opposed to the
per-entry fields. Supported from Revit 2026.
_Avoid_: global settings, file settings.

## Zone

A named area of the launcher window with its own view and view model: list, entries, editor,
form, markup, settings, toolbar.
_Avoid_: panel (when the code artifact is meant), region.

## Chip

A toggle button used for filtering, sorting and selecting an option (version, scope, entry type,
theme).
_Avoid_: tag, pill, filter button.

## Launcher

The WPF application this repository builds: it manages manifests before Revit starts.
_Avoid_: manager (ambiguous), app.

## Sandbox

The `sandbox/AddinManager.Playground` WPF playground that previews a XAML fragment. Not part of the
product and never shipped.
_Avoid_: playground (when the build unit is meant), test app.
