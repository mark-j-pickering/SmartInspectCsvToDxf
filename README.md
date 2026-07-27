# SmartInspect CSV to DXF

Small WinForms utility for converting FARO CAM2 SmartInspect tabular report exports into DXF.

## Installing

1. Go to the [latest release](https://github.com/mark-j-pickering/SmartInspectCsvToDxf/releases/latest).
2. Under **Assets**, download `SmartInspectCsvToDxf-win-Setup.exe`.
3. Run it. It installs the app for the current user, creates a Start Menu (and Desktop) shortcut, and registers an uninstall entry — no admin rights needed.
4. Launch it from the Start Menu shortcut from then on, since that's what carries the app forward through automatic updates.

Each release also lists a few other assets — these are **not** for manual download, they're consumed automatically by the installed app's own update check:

| Asset | Purpose |
|---|---|
| `SmartInspectCsvToDxf-win-Setup.exe` | **The one to download** — installer |
| `SmartInspectCsvToDxf-*-full.nupkg`, `-delta.nupkg` | Update payloads, fetched automatically when a newer version is available |
| `releases.win.json`, `RELEASES` | Update feed metadata |
| `SmartInspectCsvToDxf-*-portable-win-x64.zip` | Portable edition — see [below](#portable-version) |

### Migrating from an existing portable ZIP install

If you're currently running the app from an extracted ZIP folder:

1. Close the running (portable) application.
2. Download the latest `Setup.exe` from GitHub Releases.
3. Run it to install the app.
4. Use the installed Start Menu shortcut from then on — the old portable folder can be deleted once you've confirmed the installed copy has your settings (see below; nothing needs to be copied over manually).

## Automatic updates

The installed application checks GitHub Releases for updates shortly after it starts, and silently does nothing if you're already up to date or the check fails (e.g. no internet connection) — it won't nag you during normal use. When a newer stable release is available, a dialog offers **Install and Restart** or **Later**; nothing is downloaded or installed without your say-so.

You can also check manually at any time via **Help → Check for Updates...**, which always reports one of: an update is available, you're already up to date, or the check couldn't be completed.

Settings (report/output/USB folders, mirror option) are stored per-user in `%APPDATA%\SmartInspectCsvToDxf\settings.json`, not inside the installed application folder, so they survive every update automatically.

## Portable version

Each release also publishes a `SmartInspectCsvToDxf-<version>-portable-win-x64.zip` — a self-contained, extract-and-run build for machines where installing isn't practical (e.g. a locked-down USB-only workflow).

**The portable edition does not check for or install updates.** It isn't a Velopack install, so update checks are automatically skipped rather than failing; if you need the latest version, download a newer portable ZIP (or switch to the installed edition above, which does update itself).

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
- Report file pane updates automatically when report files are added, deleted, renamed, or overwritten; each entry shows its last-modified date/time
- Select a report file from the list to preview it — true-radius circles, centre marks, labels, line features, and axes for whichever plane (XY/XZ/YZ) best matches the part's geometry (auto-detected, or cycle manually with the arrow keys)
- Adjust the preview before exporting:
  - **Mirror X** / **Mirror Y** — flip the current view about that axis
  - **Rotate Left 90°** / **Rotate Right 90°**
  - **Align** — click the button, then click a line feature in the preview to rotate the whole view so that line is exactly horizontal
  - **Reset** — discard all of the above and go back to the file's original orientation
  
  Each of these is a one-shot action applied directly to what's currently displayed (there's no undo/redo) — selecting a different report file, or reselecting the current one, always starts fresh from the file's original data.
- Export the selection to the configured DXF folder, or write it directly to the configured USB folder — multiple files can be selected for a batch export (only the file currently shown in the preview carries the rotate/mirror/align adjustment; every other file in the batch exports untouched)

The app saves the configured folders and window size/position to:

```text
%APPDATA%\SmartInspectCsvToDxf\settings.json
```

(Rotate/Mirror/Align adjustments are not saved here — they're a live, in-session editing action, not a persisted preference.)

## DXF layers

- `FEATURE_CIRCLES`
- `FEATURE_CENTRES`
- `FEATURE_LABELS`
- `FEATURE_LINES`

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
Velopack 1.2.0
```

`UglyToad.PdfPig` currently has no plain stable release published (its maintainer transition is in progress) — the `-custom-5` prerelease is the best available and is what's pinned; revisit once a stable release resumes.

## Publishing a release

Releases are cut by pushing a semantic-version git tag; `.github/workflows/release.yml` picks it up, builds, packs the Velopack installer/update packages, and publishes a GitHub Release with all required assets.

```bash
git tag v1.2.3
git push origin v1.2.3
```

The tag must match `vX.Y.Z` (e.g. `v1.0.0`, `v1.1.0`, `v1.1.1`) — anything else is rejected by the workflow before it builds anything. The version baked into the app (and shown in **Help → About...**) is the tag with the leading `v` stripped.

To re-run packaging/publishing for a tag that already exists (e.g. after fixing a failed workflow run) without pushing a new tag, trigger the workflow manually from the Actions tab (`workflow_dispatch`) and supply that tag in the `tag` input.
