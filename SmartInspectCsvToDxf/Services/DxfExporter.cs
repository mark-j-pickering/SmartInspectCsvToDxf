using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using SmartInspectCsvToDxf.Models;

namespace SmartInspectCsvToDxf.Services;

public static class DxfExporter
{
    // drawingPlane: null means auto-detect from this file's own features (same rule
    // PreviewPanel uses by default). Pass an explicit plane to honor a manual override
    // the user made in the preview for this specific file.
    public static void Export(string outputPath, IEnumerable<Feature> inputFeatures, bool mirrorAboutYAxis, DrawingPlane? drawingPlane = null)
    {
        var features = mirrorAboutYAxis
            ? inputFeatures.Select(f => f.WithMirrorY()).ToList()
            : inputFeatures.ToList();

        var plane = drawingPlane ?? DrawingPlaneDetector.Detect(features);

        var doc = new DxfDocument();

        var circlesLayer = new Layer("FEATURE_CIRCLES");
        var centresLayer = new Layer("FEATURE_CENTRES");
        var labelsLayer = new Layer("FEATURE_LABELS");
        var linesLayer = new Layer("FEATURE_LINES");

        doc.Layers.Add(circlesLayer);
        doc.Layers.Add(centresLayer);
        doc.Layers.Add(labelsLayer);
        doc.Layers.Add(linesLayer);

        foreach (var f in features)
        {
            var (u, v, elevation) = DrawingPlaneMapper.Project(f, plane);
            var centre = new Vector3(u, v, elevation);

            if (f.IsLine)
            {
                var (u2, v2, elevation2) = DrawingPlaneMapper.Project(f.X2!.Value, f.Y2!.Value, f.Z2 ?? 0.0, plane);
                var end = new Vector3(u2, v2, elevation2);

                doc.Entities.Add(new Line(centre, end) { Layer = linesLayer });

                var midU = (u + u2) / 2.0;
                var midV = (v + v2) / 2.0;
                var midElevation = (elevation + elevation2) / 2.0;
                var lineLabel = new Text(f.Name, new Vector3(midU, midV + 3.0, midElevation), 2.5)
                {
                    Layer = labelsLayer
                };
                doc.Entities.Add(lineLabel);
                continue;
            }

            // Centre-only points (no Diameter/Radius in the source report) have Radius 0 -
            // netDxf's Circle requires a positive radius, so skip it and fall back to just
            // the centre cross and label for those.
            if (f.Radius > 0)
            {
                var circle = new Circle(centre, f.Radius)
                {
                    Layer = circlesLayer
                };
                doc.Entities.Add(circle);
            }

            // Small centre cross using two short lines.
            var crossSize = Math.Max(f.Radius * 0.08, 1.0);
            doc.Entities.Add(new Line(
                new Vector3(u - crossSize, v, elevation),
                new Vector3(u + crossSize, v, elevation))
            { Layer = centresLayer });
            doc.Entities.Add(new Line(
                new Vector3(u, v - crossSize, elevation),
                new Vector3(u, v + crossSize, elevation))
            { Layer = centresLayer });

            var labelOffset = Math.Max(f.Radius * 0.12, 3.0);
            var labelHeight = Math.Max(f.Radius * 0.18, 2.5);
            var text = new Text(
                f.Name,
                new Vector3(u + labelOffset, v + labelOffset, elevation),
                labelHeight)
            {
                Layer = labelsLayer
            };
            doc.Entities.Add(text);
        }

        doc.Save(outputPath);
    }
}
