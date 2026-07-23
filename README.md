# SmartInspect CSV to DXF

Small WinForms utility for converting FARO CAM2 SmartInspect tabular report exports into DXF.

## Supported input formats

The app reads FARO CAM2 SmartInspect's report export — four formats, auto-detected by extension:

- **`.txt`** — the tab-delimited **tabular report** export (`Report` tool → save/export as text)
- **`.xml`** — the newer, structured report export (some SmartInspect versions produce this instead)
- **`.pdf`** — the printed/PDF version of the same report, parsed by reading each word's position on the page
- **`.csv`** — a legacy `name,x,y,z,diameter,radius` schema kept for older exports already in circulation. This isn't actual SmartInspect output — it's a simpler hand-rolled format from before the app read real SmartInspect reports, and may need updating if a different/newer CSV shape comes along.

The `.txt`/`.xml`/`.pdf` formats represent each inspected feature as a block/element carrying whichever of these properties are present:

```text
Center.x
Center.y
Center.z
Diameter   (or Radius, preferred if both are present)
```

`Center.z` defaults to `0` if absent. If neither `Diameter` nor `Radius` is present, the feature is still placed as a centre-only point (radius `0`). Blocks/features with no `Center.x`/`Center.y` at all (e.g. Plane/`Flatness`, Line/`Straightness` features, or reference sections like `World`/`Coordinate System`) are skipped, since there's nothing to place.

The `.csv` format instead expects a literal `name,x,y,z,diameter,radius` header row; `radius` is preferred if both `radius` and `diameter` are present, and `z` defaults to `0` if the column is absent.

## UI

- Configure a **Report folder**
- Configure a default **DXF folder**
- Configure a **USB folder** for one-click removable-drive export
- Report file pane updates automatically when report files are added, deleted, renamed, or overwritten
- Select a report file from the list
- Preview true-radius circles, centre marks, labels and X/Y axes
- Toggle **Mirror about Y axis**
- Export selected report to the configured DXF folder
- Write selected report directly to the configured USB folder

Mirror about Y axis applies:

```csharp
x = -x;
```

The app saves the configured folders and mirror setting to:

```text
%APPDATA%\SmartInspectCsvToDxf\settings.json
```

## DXF layers

- `FEATURE_CIRCLES`
- `FEATURE_CENTRES`
- `FEATURE_LABELS`

## Build

Open `SmartInspectCsvToDxf.sln` in Visual Studio 2022 or later.

The project targets:

```text
net8.0-windows
```

NuGet packages used:

```text
netDxf 2022.11.2
UglyToad.PdfPig 1.7.0-custom-5
```

`UglyToad.PdfPig` currently has no plain stable release published (its maintainer transition is in progress) — the `-custom-5` prerelease is the best available and is what's pinned; revisit once a stable release resumes.
