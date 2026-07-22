# SmartInspect CSV to DXF

Small WinForms utility for converting SmartInspect-style extracted feature CSV files into DXF.

## Supported CSV columns

The MVP expects:

```csv
name,x,y,z,diameter,radius
```

`radius` is preferred. If `radius` is absent, `diameter / 2` is used.

## UI

- Configure a **CSV folder**
- Configure a default **DXF folder**
- Configure a **USB folder** for one-click removable-drive export
- CSV file pane updates automatically when CSV files are added, deleted, renamed, or overwritten
- Select a CSV file from the list
- Preview true-radius circles, centre marks, labels and X/Y axes
- Toggle **Mirror about Y axis**
- Export selected CSV to the configured DXF folder
- Write selected CSV directly to the configured USB folder

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

NuGet package used:

```text
netDxf 2022.11.2
```
