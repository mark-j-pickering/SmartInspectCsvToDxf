using SmartInspectCsvToDxf.Services;

namespace SmartInspectCsvToDxf.Tests;

// Unlike the .txt/.xml readers, there's no lightweight way to hand-author a synthetic
// PDF fixture with specific word positions, so this reader is covered end-to-end
// against the real sample report rather than with small synthetic edge cases.
public sealed class SmartInspectPdfReportReaderTests
{
    [Fact]
    public void Read_RealSampleReport_ExtractsExpectedFeatures()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "tabularReport.pdf");

        var features = SmartInspectPdfReportReader.Read(path);

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

    [Fact]
    public void Read_UnmeasuredReport_SkipsFeaturesWithBlankActualColumn()
    {
        // Real export of a session where no readings were taken: every feature shows
        // "Nr. of readings: 0" and a blank Actual (mm) column, so Center.x/y/z/Diameter
        // rows only carry their Low/Up Tol words - nothing should be placed.
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "unmeasuredReport.pdf");

        var features = SmartInspectPdfReportReader.Read(path);

        Assert.Empty(features);
    }

    [Fact]
    public void Read_CardStyleReport_ExtractsNamesWithoutTemplateHeaderText()
    {
        // Newer report template: each feature name is followed by a standalone
        // "Properties" sub-heading, and "Solver method: Standard   Nr. of readings: N"
        // sometimes renders close enough vertically to merge into the same row as the
        // feature name. Both used to leak into (or replace) the extracted feature name.
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "cardStyleReport.pdf");

        var features = SmartInspectPdfReportReader.Read(path);

        Assert.Equal(2, features.Count);

        var circle1 = Assert.Single(features, f => f.Name == "Circle 1");
        Assert.Equal(-164.890, circle1.X, 3);
        Assert.Equal(-149.479, circle1.Y, 3);
        Assert.Equal(377.556, circle1.Z, 3);
        Assert.Equal(302.836, circle1.Diameter, 3);

        var circle2 = Assert.Single(features, f => f.Name == "Circle 2");
        Assert.Equal(-248.514, circle2.X, 3);
        Assert.Equal(-149.592, circle2.Y, 3);
        Assert.Equal(518.275, circle2.Z, 3);
        Assert.Equal(79.990, circle2.Diameter, 3);
    }
}
