using SmartInspectCsvToDxf.Services;

namespace SmartInspectCsvToDxf.Tests;

public sealed class ReportFileReaderTests : IDisposable
{
    [Fact]
    public void Read_DispatchesToTabDelimitedReader_ForTxtExtension()
    {
        var path = WriteTempFile(".txt",
            "\tHOLE1\t\tReadings:2.\r\nCenter.x\t\t10mm\r\nCenter.y\t\t20mm\r\nDiameter\t\t8mm");

        var feature = Assert.Single(ReportFileReader.Read(path));

        Assert.Equal("HOLE1", feature.Name);
        Assert.Equal(10, feature.X);
    }

    [Fact]
    public void Read_DispatchesToXmlReader_ForXmlExtension()
    {
        var path = WriteTempFile(".xml",
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

        var feature = Assert.Single(ReportFileReader.Read(path));

        Assert.Equal("HOLE1", feature.Name);
        Assert.Equal(10, feature.X);
    }

    [Fact]
    public void Read_DispatchesToCsvReader_ForCsvExtension()
    {
        var path = WriteTempFile(".csv", "name,x,y,radius\r\nHOLE1,10,20,4");

        var feature = Assert.Single(ReportFileReader.Read(path));

        Assert.Equal("HOLE1", feature.Name);
        Assert.Equal(10, feature.X);
    }

    [Fact]
    public void Read_DispatchesToPdfReader_ForPdfExtension()
    {
        // No lightweight way to hand-author a synthetic PDF, so this dispatches
        // against the real sample fixture instead of a minimal temp file.
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "tabularReport.pdf");

        var features = ReportFileReader.Read(path);

        Assert.Contains(features, f => f.Name == "RIGHT DOWL");
    }

    private string WriteTempFile(string extension, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
        File.WriteAllText(path, content);
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
