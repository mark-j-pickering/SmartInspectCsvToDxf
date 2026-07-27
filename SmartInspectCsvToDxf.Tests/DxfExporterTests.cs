using netDxf;
using SmartInspectCsvToDxf.Models;
using SmartInspectCsvToDxf.Services;

namespace SmartInspectCsvToDxf.Tests;

public sealed class DxfExporterTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    [Fact]
    public void Export_CreatesLayers_ForCirclesCentresAndLabels()
    {
        var path = ExportTempDxf([Hole("hole1", 10, 20, radius: 5)]);

        var doc = DxfDocument.Load(path);

        Assert.Contains(doc.Layers, l => l.Name == "FEATURE_CIRCLES");
        Assert.Contains(doc.Layers, l => l.Name == "FEATURE_CENTRES");
        Assert.Contains(doc.Layers, l => l.Name == "FEATURE_LABELS");
    }

    [Fact]
    public void Export_WritesOneCirclePerFeature_WithMatchingCentreAndRadius()
    {
        var features = new List<Feature>
        {
            Hole("hole1", 10, 20, radius: 5),
            Hole("hole2", -3, 7, radius: 2.5)
        };

        var path = ExportTempDxf(features);
        var doc = DxfDocument.Load(path);

        var circles = doc.Entities.Circles.ToList();
        Assert.Equal(2, circles.Count);

        var circle1 = Assert.Single(circles, c => c.Radius == 5);
        Assert.Equal(10, circle1.Center.X);
        Assert.Equal(20, circle1.Center.Y);

        var circle2 = Assert.Single(circles, c => c.Radius == 2.5);
        Assert.Equal(-3, circle2.Center.X);
        Assert.Equal(7, circle2.Center.Y);
    }

    [Fact]
    public void Export_WritesTwoCentreLinesPerFeature()
    {
        var path = ExportTempDxf([Hole("hole1", 10, 20, radius: 5)]);

        var doc = DxfDocument.Load(path);

        var centreLines = doc.Entities.Lines
            .Where(l => l.Layer.Name == "FEATURE_CENTRES")
            .ToList();

        Assert.Equal(2, centreLines.Count);
    }

    [Fact]
    public void Export_WritesLabelWithFeatureName()
    {
        var path = ExportTempDxf([Hole("bolt-hole", 10, 20, radius: 5)]);

        var doc = DxfDocument.Load(path);

        var text = Assert.Single(doc.Entities.Texts);
        Assert.Equal("bolt-hole", text.Value);
        Assert.Equal("FEATURE_LABELS", text.Layer.Name);
    }

    [Fact]
    public void Export_MirrorsAboutYAxis_WhenRequested()
    {
        var path = ExportTempDxf([Hole("hole1", 10, 20, radius: 5)], new PreviewOrientation(0, false, true));

        var doc = DxfDocument.Load(path);

        var circle = Assert.Single(doc.Entities.Circles);
        Assert.Equal(-10, circle.Center.X);
        Assert.Equal(20, circle.Center.Y);
    }

    [Fact]
    public void Export_DoesNotMutateOriginalFeatures_WhenMirroring()
    {
        var features = new List<Feature> { Hole("hole1", 10, 20, radius: 5) };

        ExportTempDxf(features, new PreviewOrientation(0, false, true));

        Assert.Equal(10, features[0].X);
    }

    [Fact]
    public void Export_AppliesRotationThenMirror_ForCombinedOrientation()
    {
        // Rotate right 90 first: (10, 20) -> (20, -10); then mirror about Y (negate X): (-20, -10).
        var path = ExportTempDxf([Hole("hole1", 10, 20, radius: 5)], new PreviewOrientation(1, false, true));

        var doc = DxfDocument.Load(path);

        var circle = Assert.Single(doc.Entities.Circles);
        Assert.Equal(-20, circle.Center.X);
        Assert.Equal(-10, circle.Center.Y);
    }

    [Fact]
    public void Export_WritesNoEntities_ForEmptyFeatureList()
    {
        var path = ExportTempDxf([]);

        var doc = DxfDocument.Load(path);

        Assert.Empty(doc.Entities.Circles);
        Assert.Empty(doc.Entities.Lines);
        Assert.Empty(doc.Entities.Texts);
    }

    [Fact]
    public void Export_AutoDetectsXzPlane_WhenYBarelyVaries()
    {
        var features = new List<Feature>
        {
            new() { Name = "hole1", X = -365.224, Y = -149.637, Z = 234.619, Radius = 3.283, Diameter = 6.566 },
            new() { Name = "hole2", X = -358.282, Y = -149.656, Z = 400.023, Radius = 1.411, Diameter = 2.822 },
            new() { Name = "hole3", X = 8.299, Y = -149.377, Z = 351.202, Radius = 0.465, Diameter = 0.930 }
        };

        var path = ExportTempDxf(features);
        var doc = DxfDocument.Load(path);

        var circle = Assert.Single(doc.Entities.Circles, c => c.Radius == 3.283);
        Assert.Equal(-365.224, circle.Center.X, 3);
        Assert.Equal(234.619, circle.Center.Y, 3);
        Assert.Equal(-149.637, circle.Center.Z, 3);
    }

    [Fact]
    public void Export_HonorsExplicitDrawingPlane_OverridingAutoDetection()
    {
        var features = new List<Feature> { Hole("hole1", 10, 20, radius: 5) };

        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dxf");
        _tempFiles.Add(path);
        DxfExporter.Export(path, features, PreviewOrientation.Identity, drawingPlane: DrawingPlane.XZ);

        var doc = DxfDocument.Load(path);
        var circle = Assert.Single(doc.Entities.Circles);

        // XZ plane: U=X, V=Z(=0), Elevation=Y.
        Assert.Equal(10, circle.Center.X);
        Assert.Equal(0, circle.Center.Y);
        Assert.Equal(20, circle.Center.Z);
    }

    [Fact]
    public void Export_SkipsCircle_ForCentreOnlyPoint()
    {
        // A feature with no Diameter/Radius in the source report (e.g. a SmartInspect
        // "Point") has Radius 0 - netDxf rejects a zero-radius Circle, so it should be
        // skipped while the centre cross and label are still written.
        var path = ExportTempDxf([Hole("point1", 10, 20, radius: 0)]);

        var doc = DxfDocument.Load(path);

        Assert.Empty(doc.Entities.Circles);
        Assert.Equal(2, doc.Entities.Lines.Count(l => l.Layer.Name == "FEATURE_CENTRES"));
        Assert.Single(doc.Entities.Texts, t => t.Value == "point1");
    }

    private static Feature Hole(string name, double x, double y, double radius) => new()
    {
        Name = name,
        X = x,
        Y = y,
        Z = 0,
        Radius = radius,
        Diameter = radius * 2
    };

    private string ExportTempDxf(IEnumerable<Feature> features, PreviewOrientation? orientation = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dxf");
        _tempFiles.Add(path);
        DxfExporter.Export(path, features, orientation ?? PreviewOrientation.Identity);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { /* best effort cleanup */ }
        }
    }
}
