# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A small WinForms (net8.0-windows) desktop utility that watches a folder for SmartInspect-style extracted-feature CSV files, previews them as circles/centre-marks/labels, and exports them to DXF via the `netDxf` NuGet package.

Expected CSV columns: `name,x,y,z,diameter,radius`. `radius` is preferred; if absent, `diameter / 2` is used. `z` is optional and defaults to `0`.

## Commands

Build the whole solution:

```bash
dotnet build SmartInspectCsvToDxf.sln
```

Run the app:

```bash
dotnet run --project SmartInspectCsvToDxf/SmartInspectCsvToDxf.csproj
```

Run all tests:

```bash
dotnet test SmartInspectCsvToDxf.Tests/SmartInspectCsvToDxf.Tests.csproj
```

Run a single test or test class (xUnit filter):

```bash
dotnet test SmartInspectCsvToDxf.Tests/SmartInspectCsvToDxf.Tests.csproj --filter "FullyQualifiedName~CsvFeatureReaderTests.Read_PrefersRadiusOverDiameter_WhenBothPresent"
```

There is no lint step and no CI workflow configured for this repo.

## Architecture

The app is a single `MainForm` orchestrating a few small, independently testable pieces — most of the logic worth understanding lives in `Services/`, not in the form itself:

- **`Models/Feature.cs`** — immutable feature record (`Name, X, Y, Z, Diameter, Radius`) with a `WithMirrorY()` method that returns a copy with `X` negated. Mirroring is always done by producing a new `Feature`, never by mutating in place — both `DxfExporter` and `PreviewPanel` rely on this to avoid corrupting the caller's feature list.
- **`Services/CsvFeatureReader.cs`** — static `Read(path)`. Hand-rolled, quote-aware CSV line splitter (handles embedded commas and escaped `""`). Headers are matched case-insensitively and trimmed. Throws `InvalidDataException` if `name`/`x`/`y` are missing, or if neither `radius` nor `diameter` is present. Rows that are blank or have too few columns are silently skipped rather than erroring.
- **`Services/DxfExporter.cs`** — static `Export(path, features, mirrorAboutYAxis)`. Builds a `netDxf.DxfDocument` with three fixed layers: `FEATURE_CIRCLES`, `FEATURE_CENTRES`, `FEATURE_LABELS`. Per feature: one `Circle` (true radius), two short `Line`s forming a centre cross, and one `Text` label — all offset/sized relative to the feature's radius. If mirroring, `WithMirrorY()` is applied before export.
- **`Services/AppSettings.cs`** — plain JSON settings (`InputFolder`, `OutputFolder`, `UsbFolder`, `MirrorAboutYAxis`) persisted to `%APPDATA%\SmartInspectCsvToDxf\settings.json`. `Load()` swallows all errors and falls back to defaults (missing/corrupt file is not fatal).
- **`UI/PreviewPanel.cs`** — a custom `Panel` that owner-draws the current feature set on `OnPaint`: auto-fits/centres the view to the feature bounds (+20% padding) on every repaint, draws X/Y axes through the origin, and optionally draws name labels (`showText`).
- **`MainForm.cs`** — wires everything together:
  - Three folder pickers (CSV input / DXF output / USB) that save to `AppSettings` on `Leave`/Enter and on browse.
  - A `FileSystemWatcher` on the CSV input folder, debounced through a 350ms `System.Windows.Forms.Timer` (`QueueFileRefresh`) so rapid-fire filesystem events collapse into one list refresh. `IOException` on load (file still being written by SmartInspect) is treated as transient and re-queues a retry rather than surfacing an error.
  - The CSV file `ListBox` supports multi-select (`SelectionMode.MultiExtended`). Selecting one file drives the preview; **Export DXF** / **Write to USB** operate on the full selection — each selected file is read and exported independently in a loop, with a per-file success/failure summary shown at the end (one bad CSV in a batch doesn't abort the rest).
  - Output filenames are derived from the source CSV name (`+ "_mirrored_y"` suffix when mirroring is on); if a file of that name already exists, a timestamp suffix is appended instead of overwriting.

## Tests

`SmartInspectCsvToDxf.Tests` is an xUnit project targeting `net8.0-windows` (must match the main project's TFM — a plain `net8.0` test project cannot reference it). It covers only the two pure/testable services:

- `CsvFeatureReaderTests` — parsing edge cases (radius/diameter precedence, optional `z`, quoting, blank/short rows, missing-column errors, culture-invariant number parsing).
- `DxfExporterTests` — round-trips exported files back through `netDxf`'s own `DxfDocument.Load` and asserts on the real entity collections (layers, circle count/centre/radius, centre-cross line count, label text, Y-axis mirroring, empty-input behavior).

`MainForm` and `PreviewPanel` are not covered by automated tests (WinForms UI, no desktop automation harness in this environment) — verify UI changes by running the app manually.
