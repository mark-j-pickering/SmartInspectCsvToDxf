using SmartInspectCsvToDxf.Services;

namespace SmartInspectCsvToDxf.Tests;

public sealed class SmartInspectXmlReportReaderTests : IDisposable
{
    [Fact]
    public void Read_ExtractsCenterAndDiameter_ForSingleFeature()
    {
        var path = WriteTempReport(
            """
            <reportData>
              <feature name="HOLE1">
                <geometry>
                  <field name="Center.x"><textValue>10mm</textValue></field>
                  <field name="Center.y"><textValue>20mm</textValue></field>
                  <field name="Center.z"><textValue>5mm</textValue></field>
                  <field name="Diameter"><textValue>8mm</textValue></field>
                </geometry>
              </feature>
            </reportData>
            """);

        var feature = Assert.Single(SmartInspectXmlReportReader.Read(path));

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
            """
            <reportData>
              <feature name="HOLE1">
                <geometry>
                  <field name="Center.x"><textValue>10mm</textValue></field>
                  <field name="Center.y"><textValue>20mm</textValue></field>
                  <field name="Diameter"><textValue>8mm</textValue></field>
                </geometry>
              </feature>
            </reportData>
            """);

        var feature = Assert.Single(SmartInspectXmlReportReader.Read(path));

        Assert.Equal(0, feature.Z);
    }

    [Fact]
    public void Read_ExtractsMultipleFeatures_FromOneFile()
    {
        var path = WriteTempReport(
            """
            <reportData>
              <feature name="HOLE1">
                <geometry>
                  <field name="Center.x"><textValue>10mm</textValue></field>
                  <field name="Center.y"><textValue>20mm</textValue></field>
                  <field name="Diameter"><textValue>8mm</textValue></field>
                </geometry>
              </feature>
              <feature name="HOLE2">
                <geometry>
                  <field name="Center.x"><textValue>-5mm</textValue></field>
                  <field name="Center.y"><textValue>15mm</textValue></field>
                  <field name="Diameter"><textValue>4mm</textValue></field>
                </geometry>
              </feature>
            </reportData>
            """);

        var features = SmartInspectXmlReportReader.Read(path);

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
            """
            <reportData>
              <feature name="HOLE1">
                <geometry>
                  <field name="Center.x"><textValue>10mm</textValue></field>
                  <field name="Center.y"><textValue>20mm</textValue></field>
                  <field name="Diameter"><textValue>100mm</textValue></field>
                  <field name="Radius"><textValue>3mm</textValue></field>
                </geometry>
              </feature>
            </reportData>
            """);

        var feature = Assert.Single(SmartInspectXmlReportReader.Read(path));

        Assert.Equal(3, feature.Radius);
        Assert.Equal(6, feature.Diameter);
    }

    [Fact]
    public void Read_DefaultsRadiusToZero_WhenNeitherDiameterNorRadiusPresent()
    {
        var path = WriteTempReport(
            """
            <reportData>
              <feature name="POINT1">
                <geometry>
                  <field name="Center.x"><textValue>10mm</textValue></field>
                  <field name="Center.y"><textValue>20mm</textValue></field>
                </geometry>
              </feature>
            </reportData>
            """);

        var feature = Assert.Single(SmartInspectXmlReportReader.Read(path));

        Assert.Equal(0, feature.Diameter);
        Assert.Equal(0, feature.Radius);
    }

    [Fact]
    public void Read_SkipsFeatures_WithNoGeometryFields()
    {
        var path = WriteTempReport(
            """
            <reportData>
              <feature name="World">
                <geometry />
              </feature>
              <feature name="Coordinate System">
                <geometry />
              </feature>
            </reportData>
            """);

        Assert.Empty(SmartInspectXmlReportReader.Read(path));
    }

    [Fact]
    public void Read_SkipsFeatures_WithNoCenterCoordinates()
    {
        var path = WriteTempReport(
            """
            <reportData>
              <feature name="Projection Plane 1">
                <geometry>
                  <field name="Flatness"><textValue>0.022mm</textValue></field>
                </geometry>
              </feature>
              <feature name="SUMP-PLANE">
                <geometry>
                  <field name="Straightness"><textValue>0.000mm</textValue></field>
                </geometry>
              </feature>
            </reportData>
            """);

        Assert.Empty(SmartInspectXmlReportReader.Read(path));
    }

    [Fact]
    public void Read_RealSampleReport_ExtractsExpectedFeatures()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "report.xml");

        var features = SmartInspectXmlReportReader.Read(path);

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

    private string WriteTempReport(string xml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, xml);
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
