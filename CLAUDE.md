# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A small WinForms (net8.0-windows) desktop utility that watches a folder for FARO CAM2 SmartInspect report exports, previews the inspected features as circles/centre-marks/labels, and exports them to DXF via the `netDxf` NuGet package. Four input formats are supported, auto-detected by extension:

- **`.txt`** — the tab-delimited "tabular report" export (`Report` tool → save as text). Feature data is positional (column index within a block), not named.
- **`.xml`** — a newer, properly structured `reportData`/`part`/session/`group`/`feature`/`geometry`/`field` export. Far more reliable to parse since fields are named (`<field name="Center.x">`) rather than column-positional.
- **`.pdf`** — the printed/PDF version of the same report. Parsed via `UglyToad.PdfPig`, reading each word's actual position on the page rather than a flattened text stream — `pdftotext -layout` was tried first and scrambled the columns (values shifted onto the wrong rows once linearized), which is why this reader works off word bounding boxes instead.
- **`.csv`** — a legacy `name,x,y,z,diameter,radius` format kept for older exports already in circulation. Unlike the other three, this isn't real SmartInspect output — it's a hand-rolled simple schema from before this app read actual SmartInspect reports, and may need updating if a newer/different CSV shape shows up.

The `.txt`/`.xml`/`.pdf` formats represent the same underlying data (per-feature blocks with `Center.x`/`Center.y`/`Center.z` and `Diameter`/`Radius`, plus reference/tolerance data the app doesn't need) — same placement rules apply to all three: `Center.z` defaults to `0` if absent; `Diameter`/`Radius` default to `0` (a centre-only point) if neither is present; `Radius` is preferred over `Diameter` when both are present (diameter is then re-derived as `radius * 2`); and blocks/features with no `Center.x`/`Center.y` at all (Plane/`Flatness`, Line/`Straightness` features, and reference sections like `World`/`Coordinate System`) are skipped since there's nothing to place. The `.csv` reader has its own older, stricter rules — see `CsvFeatureReader.cs`.

`UglyToad.PdfPig` is currently pinned to `1.7.0-custom-5` — there's no plain stable release published at the moment (the project's maintainer transition is in progress), so this version should be revisited once a stable release resumes.

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
dotnet test SmartInspectCsvToDxf.Tests/SmartInspectCsvToDxf.Tests.csproj --filter "FullyQualifiedName~SmartInspectReportReaderTests.Read_PrefersRadiusOverDiameter_WhenBothPresent"
```

There is no lint step and no CI workflow configured for this repo.

## Architecture

The app is a single `MainForm` orchestrating a few small, independently testable pieces — most of the logic worth understanding lives in `Services/`, not in the form itself:

- **`Models/Feature.cs`** — immutable feature record (`Name, X, Y, Z, Diameter, Radius`) with a `WithMirrorY()` method that returns a copy with `X` negated. Mirroring is always done by producing a new `Feature`, never by mutating in place — both `DxfExporter` and `PreviewPanel` rely on this to avoid corrupting the caller's feature list.
- **`Services/ReportFileReader.cs`** — the single entry point `MainForm` calls. `Read(path)` dispatches to `SmartInspectXmlReportReader`, `CsvFeatureReader`, `SmartInspectPdfReportReader`, or `SmartInspectReportReader` based on file extension (`.xml`/`.csv`/`.pdf`/everything else). `FilePatterns` (`["*.txt", "*.xml", "*.csv", "*.pdf"]`) is the one place the set of supported extensions is defined; both the file-list `Directory.GetFiles` calls and the `FileSystemWatcher`s in `MainForm` iterate over it.
- **`Services/SmartInspectReportReader.cs`** — static `Read(path)` for the `.txt` format. Line-by-line state machine over the tab-delimited report: a row with an empty first column and a non-empty second column starts a new feature block (flushing the previous one first); subsequent rows with a non-empty first column are treated as `PropertyName \t \t actual \t ...` and update whichever of `Center.x/y/z`/`Diameter`/`Radius` matches (case-insensitive), reading the numeric value out of the 3rd tab-column via `ReportValueParser`. A block only becomes a `Feature` if it collected both `Center.x` and `Center.y`; everything else (session metadata, `World`/`Coordinate System` reference sections, Plane/Line features with no centre, the report footer) is silently ignored rather than erroring.
- **`Services/SmartInspectXmlReportReader.cs`** — static `Read(path)` for the `.xml` format. Loads the file as `XDocument`, reads the root's own namespace (so it doesn't hardcode the `http://www.faro.com/CAM2/ReportingEngine` URI), then walks every `feature` element regardless of nesting/grouping. For each, reads `field` children of its `geometry` element by their `name` attribute (`Center.x`, `Diameter`, etc.) out of the nested `textValue` element. Same placement rule as the `.txt` reader: only emitted if both `Center.x` and `Center.y` were found.
- **`Services/SmartInspectPdfReportReader.cs`** — static `Read(path)` for the `.pdf` format, using `UglyToad.PdfPig`. For each page, groups `Word`s into rows by vertical-center proximity (values render in a slightly larger font than labels, so raw top/bottom don't align exactly — a plain `Top` comparison under-groups them), sorts each row left-to-right, then classifies it: if the leading word isn't one of the recognized keywords (`Center.x/y/z`, `Diameter`, `Radius`, `Circularity`, `Flatness`, `Straightness`, `Readings`, `actual`) and starts with a letter, it's a feature/section-name row (flush the previous block, start a new one, stripping a trailing `Readings:N.` token and joining the rest as the name); otherwise it's a property row and the row's 2nd word (by X position) is the value. Same placement rule as the other SmartInspect readers. Pages are processed in order and treated as one continuous row stream, so a feature/reference block never needs to matter which page it's on.
- **`Services/ReportValueParser.cs`** — shared internal helper (`ParseLeadingNumber`) used by all three SmartInspect readers to pull the leading signed number out of a unit-suffixed value like `151.536mm`.
- **`Services/CsvFeatureReader.cs`** — static `Read(path)` for the legacy `.csv` format. Hand-rolled, quote-aware CSV line splitter (handles embedded commas and escaped `""`). Headers are matched case-insensitively and trimmed. Throws `InvalidDataException` if `name`/`x`/`y` are missing, or if neither `radius` nor `diameter` is present (stricter than the SmartInspect readers, which just skip what they can't place). Rows that are blank or have too few columns are silently skipped rather than erroring.
- **`Services/DxfExporter.cs`** — static `Export(path, features, mirrorAboutYAxis)`. Builds a `netDxf.DxfDocument` with three fixed layers: `FEATURE_CIRCLES`, `FEATURE_CENTRES`, `FEATURE_LABELS`. Per feature: one `Circle` (true radius), two short `Line`s forming a centre cross, and one `Text` label — all offset/sized relative to the feature's radius. If mirroring, `WithMirrorY()` is applied before export.
- **`Services/AppSettings.cs`** — plain JSON settings (`InputFolder`, `OutputFolder`, `UsbFolder`, `MirrorAboutYAxis`) persisted to `%APPDATA%\SmartInspectCsvToDxf\settings.json`. `Load()` swallows all errors and falls back to defaults (missing/corrupt file is not fatal).
- **`UI/PreviewPanel.cs`** — a custom `Panel` that owner-draws the current feature set on `OnPaint`: auto-fits/centres the view to the feature bounds (+20% padding) on every repaint, draws X/Y axes through the origin, and optionally draws name labels (`showText`).
- **`MainForm.cs`** — wires everything together:
  - Three folder pickers (report input / DXF output / USB) that save to `AppSettings` on `Leave`/Enter and on browse.
  - One `FileSystemWatcher` per pattern in `ReportFileReader.FilePatterns` on the report input folder, debounced through a 350ms `System.Windows.Forms.Timer` (`QueueFileRefresh`) so rapid-fire filesystem events collapse into one list refresh. `IOException` on load (file still being written by SmartInspect) is treated as transient and re-queues a retry rather than surfacing an error.
  - The report file `ListBox` supports multi-select (`SelectionMode.MultiExtended`). Selecting one file drives the preview; **Export DXF** / **Write to USB** operate on the full selection — each selected file is read and exported independently in a loop, with a per-file success/failure summary shown at the end (one bad report in a batch doesn't abort the rest).
  - Output filenames are derived from the source report file name (`+ "_mirrored_y"` suffix when mirroring is on); if a file of that name already exists, a timestamp suffix is appended instead of overwriting.

## Tests

`SmartInspectCsvToDxf.Tests` is an xUnit project targeting `net8.0-windows` (must match the main project's TFM — a plain `net8.0` test project cannot reference it). It covers only the pure/testable services:

- `SmartInspectReportReaderTests` — `.txt` parsing edge cases (radius/diameter precedence, optional `Center.z`, multi-feature files, non-placeable blocks, session-metadata/footer rows, reference sections), plus an end-to-end test against a real sample export checked into `TestData/tabularReport.txt`.
- `SmartInspectXmlReportReaderTests` — the same edge cases for the `.xml` format, plus an end-to-end test against `TestData/report.xml` (same underlying job as the `.txt` fixture, different export format — useful for cross-checking both readers agree).
- `SmartInspectPdfReportReaderTests` — no synthetic edge cases (no lightweight way to hand-author a PDF with specific word positions); covered solely by an end-to-end test against `TestData/tabularReport.pdf` (same underlying job again, third format).
- `CsvFeatureReaderTests` — parsing edge cases for the legacy `.csv` schema (radius/diameter precedence, optional `z`, quoting, blank/short rows, missing-column errors, culture-invariant number parsing).
- `ReportFileReaderTests` — confirms the extension-based dispatch picks the right reader for each of the four formats.
- `DxfExporterTests` — round-trips exported files back through `netDxf`'s own `DxfDocument.Load` and asserts on the real entity collections (layers, circle count/centre/radius, centre-cross line count, label text, Y-axis mirroring, empty-input behavior).

`MainForm` and `PreviewPanel` are not covered by automated tests (WinForms UI, no desktop automation harness in this environment) — verify UI changes by running the app manually.
