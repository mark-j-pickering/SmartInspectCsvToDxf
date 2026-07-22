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
        var path = ExportTempDxf([Hole("hole1", 10, 20, radius: 5)], mirrorAboutYAxis: true);

        var doc = DxfDocument.Load(path);

        var circle = Assert.Single(doc.Entities.Circles);
        Assert.Equal(-10, circle.Center.X);
        Assert.Equal(20, circle.Center.Y);
    }

    [Fact]
    public void Export_DoesNotMutateOriginalFeatures_WhenMirroring()
    {
        var features = new List<Feature> { Hole("hole1", 10, 20, radius: 5) };

        ExportTempDxf(features, mirrorAboutYAxis: true);

        Assert.Equal(10, features[0].X);
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

    private static Feature Hole(string name, double x, double y, double radius) => new()
    {
        Name = name,
        X = x,
        Y = y,
        Z = 0,
        Radius = radius,
        Diameter = radius * 2
    };

    private string ExportTempDxf(IEnumerable<Feature> features, bool mirrorAboutYAxis = false)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dxf");
        _tempFiles.Add(path);
        DxfExporter.Export(path, features, mirrorAboutYAxis);
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
