using SmartInspectCsvToDxf.Services;

namespace SmartInspectCsvToDxf.Tests;

public sealed class CsvFeatureReaderTests : IDisposable
{
    [Fact]
    public void Read_PrefersRadiusOverDiameter_WhenBothPresent()
    {
        var path = WriteTempCsv(
            "name,x,y,z,diameter,radius",
            "hole1,10,20,0,100,4");

        var features = CsvFeatureReader.Read(path);

        var feature = Assert.Single(features);
        Assert.Equal(4, feature.Radius);
        Assert.Equal(8, feature.Diameter);
    }

    [Fact]
    public void Read_DerivesRadiusFromDiameter_WhenRadiusAbsent()
    {
        var path = WriteTempCsv(
            "name,x,y,z,diameter",
            "hole1,10,20,0,10");

        var features = CsvFeatureReader.Read(path);

        var feature = Assert.Single(features);
        Assert.Equal(5, feature.Radius);
        Assert.Equal(10, feature.Diameter);
    }

    [Fact]
    public void Read_DefaultsZToZero_WhenColumnAbsent()
    {
        var path = WriteTempCsv(
            "name,x,y,radius",
            "hole1,1,2,3");

        var features = CsvFeatureReader.Read(path);

        Assert.Equal(0, Assert.Single(features).Z);
    }

    [Fact]
    public void Read_ParsesZ_WhenColumnPresent()
    {
        var path = WriteTempCsv(
            "name,x,y,z,radius",
            "hole1,1,2,7.5,3");

        var features = CsvFeatureReader.Read(path);

        Assert.Equal(7.5, Assert.Single(features).Z);
    }

    [Fact]
    public void Read_IsCaseInsensitiveAndTrimsHeaders()
    {
        var path = WriteTempCsv(
            " Name , X , Y , Radius ",
            "hole1,1,2,3");

        var features = CsvFeatureReader.Read(path);

        Assert.Equal("hole1", Assert.Single(features).Name);
    }

    [Fact]
    public void Read_HandlesQuotedFieldsWithEmbeddedCommas()
    {
        var path = WriteTempCsv(
            "name,x,y,radius",
            "\"hole, 1\",1,2,3");

        var features = CsvFeatureReader.Read(path);

        Assert.Equal("hole, 1", Assert.Single(features).Name);
    }

    [Fact]
    public void Read_HandlesEscapedDoubleQuotes()
    {
        var path = WriteTempCsv(
            "name,x,y,radius",
            "\"hole \"\"A\"\"\",1,2,3");

        var features = CsvFeatureReader.Read(path);

        Assert.Equal("hole \"A\"", Assert.Single(features).Name);
    }

    [Fact]
    public void Read_SkipsBlankLines()
    {
        var path = WriteTempCsv(
            "name,x,y,radius",
            "hole1,1,2,3",
            "",
            "   ",
            "hole2,4,5,6");

        var features = CsvFeatureReader.Read(path);

        Assert.Equal(2, features.Count);
    }

    [Fact]
    public void Read_SkipsRowsWithTooFewColumns()
    {
        var path = WriteTempCsv(
            "name,x,y,radius",
            "hole1,1,2,3",
            "hole2,4");

        var features = CsvFeatureReader.Read(path);

        Assert.Equal("hole1", Assert.Single(features).Name);
    }

    [Fact]
    public void Read_ReturnsEmptyList_WhenOnlyHeaderPresent()
    {
        var path = WriteTempCsv("name,x,y,radius");

        var features = CsvFeatureReader.Read(path);

        Assert.Empty(features);
    }

    [Fact]
    public void Read_Throws_WhenNameColumnMissing()
    {
        var path = WriteTempCsv(
            "x,y,radius",
            "1,2,3");

        Assert.Throws<InvalidDataException>(() => CsvFeatureReader.Read(path));
    }

    [Fact]
    public void Read_Throws_WhenXColumnMissing()
    {
        var path = WriteTempCsv(
            "name,y,radius",
            "hole1,2,3");

        Assert.Throws<InvalidDataException>(() => CsvFeatureReader.Read(path));
    }

    [Fact]
    public void Read_Throws_WhenNeitherRadiusNorDiameterPresent()
    {
        var path = WriteTempCsv(
            "name,x,y",
            "hole1,1,2");

        Assert.Throws<InvalidDataException>(() => CsvFeatureReader.Read(path));
    }

    [Fact]
    public void Read_Throws_WhenNumericValueIsUnparsable()
    {
        var path = WriteTempCsv(
            "name,x,y,radius",
            "hole1,notanumber,2,3");

        Assert.Throws<FormatException>(() => CsvFeatureReader.Read(path));
    }

    [Fact]
    public void Read_UsesInvariantCulture_ForDecimalPoints()
    {
        var path = WriteTempCsv(
            "name,x,y,radius",
            "hole1,1.5,2.5,3.5");

        var features = CsvFeatureReader.Read(path);

        var feature = Assert.Single(features);
        Assert.Equal(1.5, feature.X);
        Assert.Equal(2.5, feature.Y);
        Assert.Equal(3.5, feature.Radius);
    }

    private string WriteTempCsv(params string[] lines)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
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
