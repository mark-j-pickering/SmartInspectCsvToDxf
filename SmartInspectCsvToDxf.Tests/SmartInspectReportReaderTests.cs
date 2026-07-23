using SmartInspectCsvToDxf.Services;

namespace SmartInspectCsvToDxf.Tests;

public sealed class SmartInspectReportReaderTests : IDisposable
{
    [Fact]
    public void Read_ExtractsCenterAndDiameter_ForSingleFeature()
    {
        var path = WriteTempReport(
            "\tHOLE1\t\tReadings:2.",
            "Center.x\t\t10mm",
            "Center.y\t\t20mm",
            "Center.z\t\t5mm",
            "Diameter\t\t8mm");

        var features = SmartInspectReportReader.Read(path);

        var feature = Assert.Single(features);
        Assert.Equal("HOLE1", feature.Name);
        Assert.Equal(10, feature.X);
        Assert.Equal(20, feature.Y);
        Assert.Equal(5, feature.Z);
        Assert.Equal(8, feature.Diameter);
        Assert.Equal(4, feature.Radius);
    }

    [Fact]
    public void Read_DefaultsZToZero_WhenCenterZAbsent()
    {
        var path = WriteTempReport(
            "\tHOLE1\t\tReadings:2.",
            "Center.x\t\t10mm",
            "Center.y\t\t20mm",
            "Diameter\t\t8mm");

        var feature = Assert.Single(SmartInspectReportReader.Read(path));

        Assert.Equal(0, feature.Z);
    }

    [Fact]
    public void Read_ExtractsMultipleFeatures_FromOneFile()
    {
        var path = WriteTempReport(
            "\tHOLE1\t\tReadings:2.",
            "Center.x\t\t10mm",
            "Center.y\t\t20mm",
            "Diameter\t\t8mm",
            "\tHOLE2\t\tReadings:2.",
            "Center.x\t\t-5mm",
            "Center.y\t\t15mm",
            "Diameter\t\t4mm");

        var features = SmartInspectReportReader.Read(path);

        Assert.Equal(2, features.Count);
        Assert.Equal("HOLE1", features[0].Name);
        Assert.Equal("HOLE2", features[1].Name);
        Assert.Equal(-5, features[1].X);
        Assert.Equal(15, features[1].Y);
        Assert.Equal(2, features[1].Radius);
    }

    [Fact]
    public void Read_PrefersRadiusOverDiameter_WhenBothPresent()
    {
        var path = WriteTempReport(
            "\tHOLE1\t\tReadings:2.",
            "Center.x\t\t10mm",
            "Center.y\t\t20mm",
            "Diameter\t\t100mm",
            "Radius\t\t3mm");

        var feature = Assert.Single(SmartInspectReportReader.Read(path));

        Assert.Equal(3, feature.Radius);
        Assert.Equal(6, feature.Diameter);
    }

    [Fact]
    public void Read_DefaultsRadiusToZero_WhenNeitherDiameterNorRadiusPresent()
    {
        var path = WriteTempReport(
            "\tPOINT1\t\tReadings:1.",
            "Center.x\t\t10mm",
            "Center.y\t\t20mm");

        var feature = Assert.Single(SmartInspectReportReader.Read(path));

        Assert.Equal(0, feature.Diameter);
        Assert.Equal(0, feature.Radius);
    }

    [Fact]
    public void Read_SkipsBlocks_WithNoCenterCoordinates()
    {
        var path = WriteTempReport(
            "\tProjection Plane 1\t\tReadings:5.",
            "Flatness\t\t0.022mm",
            "\tSUMP-PLANE\t\tReadings:2.",
            "Straightness\t\t0.000mm");

        var features = SmartInspectReportReader.Read(path);

        Assert.Empty(features);
    }

    [Fact]
    public void Read_SkipsReferenceSections_WithNoData()
    {
        var path = WriteTempReport(
            "\tWorld\t\t",
            "\t\tactual\tnominal\tdev\t - tol \t+tol\toot",
            "\tCoordinate System\t\t",
            "\t\tactual\tnominal\tdev\t - tol \t+tol\toot",
            "\tHOLE1\t\tReadings:2.",
            "Center.x\t\t10mm",
            "Center.y\t\t20mm",
            "Diameter\t\t8mm");

        var feature = Assert.Single(SmartInspectReportReader.Read(path));

        Assert.Equal("HOLE1", feature.Name);
    }

    [Fact]
    public void Read_IgnoresSessionMetadataAndFooterRows()
    {
        var path = WriteTempReport(
            "Program Name\t\t",
            "File Name\t\tjob.fcd",
            "Operator\t\tMark Pickering",
            "\tHOLE1\t\tReadings:2.",
            "Center.x\t\t10mm",
            "Center.y\t\t20mm",
            "Diameter\t\t8mm",
            "\t\t\tOverall Inspection Status\t\t\tPass",
            "CAM2 Inspection Report\t\t\t\t\t\t\t1/1");

        var feature = Assert.Single(SmartInspectReportReader.Read(path));

        Assert.Equal("HOLE1", feature.Name);
        Assert.Equal(8, feature.Diameter);
    }

    [Fact]
    public void Read_RealSampleReport_ExtractsExpectedFeatures()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "tabularReport.txt");

        var features = SmartInspectReportReader.Read(path);

        Assert.Equal(16, features.Count);

        var rightDowl = Assert.Single(features, f => f.Name == "RIGHT DOWL");
        Assert.Equal(151.536, rightDowl.X, 3);
        Assert.Equal(-135.576, rightDowl.Y, 3);
        Assert.Equal(0, rightDowl.Z, 3);
        Assert.Equal(15.857, rightDowl.Diameter, 3);

        var centerBore = Assert.Single(features, f => f.Name == "CENTER-BORE");
        Assert.Equal(-43.895, centerBore.X, 3);
        Assert.Equal(-194.683, centerBore.Y, 3);
        Assert.Equal(286.195, centerBore.Diameter, 3);

        var m8 = Assert.Single(features, f => f.Name == "M8 THREADED");
        Assert.Equal(30.684, m8.X, 3);
        Assert.Equal(2.198, m8.Y, 3);
        Assert.Equal(6.996, m8.Diameter, 3);

        Assert.DoesNotContain(features, f => f.Name is "World" or "Coordinate System" or "Projection Plane 1" or "SUMP-PLANE");
    }

    private string WriteTempReport(params string[] lines)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        File.WriteAllLines(path, lines);
        _tempFiles.Add(path);
        return path;
    }

    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { /* best effort cleanup */ }
        }
    }
}
