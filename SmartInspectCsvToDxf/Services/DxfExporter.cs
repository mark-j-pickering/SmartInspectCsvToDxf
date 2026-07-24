using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using SmartInspectCsvToDxf.Models;

namespace SmartInspectCsvToDxf.Services;

public static class DxfExporter
{
    public static void Export(string outputPath, IEnumerable<Feature> inputFeatures, bool mirrorAboutYAxis)
    {
        var features = mirrorAboutYAxis
            ? inputFeatures.Select(f => f.WithMirrorY()).ToList()
            : inputFeatures.ToList();

        var doc = new DxfDocument();

        var circlesLayer = new Layer("FEATURE_CIRCLES");
        var centresLayer = new Layer("FEATURE_CENTRES");
        var labelsLayer = new Layer("FEATURE_LABELS");

        doc.Layers.Add(circlesLayer);
        doc.Layers.Add(centresLayer);
        doc.Layers.Add(labelsLayer);

        foreach (var f in features)
        {
            var centre = new Vector3(f.X, f.Y, f.Z);

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
                new Vector3(f.X - crossSize, f.Y, f.Z),
                new Vector3(f.X + crossSize, f.Y, f.Z))
            { Layer = centresLayer });
            doc.Entities.Add(new Line(
                new Vector3(f.X, f.Y - crossSize, f.Z),
                new Vector3(f.X, f.Y + crossSize, f.Z))
            { Layer = centresLayer });

            var labelOffset = Math.Max(f.Radius * 0.12, 3.0);
            var labelHeight = Math.Max(f.Radius * 0.18, 2.5);
            var text = new Text(
                f.Name,
                new Vector3(f.X + labelOffset, f.Y + labelOffset, f.Z),
                labelHeight)
            {
                Layer = labelsLayer
            };
            doc.Entities.Add(text);
        }

        doc.Save(outputPath);
    }
}
