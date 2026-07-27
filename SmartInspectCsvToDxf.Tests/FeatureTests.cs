using SmartInspectCsvToDxf.Models;

namespace SmartInspectCsvToDxf.Tests;

public sealed class FeatureTests
{
    [Fact]
    public void WithMirrorX_NegatesY_LeavesXAndZUnchanged()
    {
        var feature = new Feature { Name = "hole1", X = 10, Y = 20, Z = 5 };

        var mirrored = feature.WithMirrorX();

        Assert.Equal(10, mirrored.X);
        Assert.Equal(-20, mirrored.Y);
        Assert.Equal(5, mirrored.Z);
    }

    [Fact]
    public void WithMirrorX_NegatesSecondPoint_ForLineFeatures()
    {
        var feature = new Feature { Name = "line1", X = 1, Y = 2, X2 = 3, Y2 = 4, Z2 = 0 };

        var mirrored = feature.WithMirrorX();

        Assert.Equal(3, mirrored.X2);
        Assert.Equal(-4, mirrored.Y2);
    }

    [Fact]
    public void WithRotatedRight90_RotatesClockwise()
    {
        var feature = new Feature { Name = "hole1", X = 10, Y = 20, Z = 5 };

        var rotated = feature.WithRotatedRight90();

        Assert.Equal(20, rotated.X);
        Assert.Equal(-10, rotated.Y);
        Assert.Equal(5, rotated.Z);
    }

    [Fact]
    public void WithRotatedLeft90_RotatesCounterClockwise()
    {
        var feature = new Feature { Name = "hole1", X = 10, Y = 20, Z = 5 };

        var rotated = feature.WithRotatedLeft90();

        Assert.Equal(-20, rotated.X);
        Assert.Equal(10, rotated.Y);
        Assert.Equal(5, rotated.Z);
    }

    [Fact]
    public void RotatingRight90FourTimes_ReturnsToOriginalPosition()
    {
        var feature = new Feature { Name = "hole1", X = 10, Y = 20, Z = 5 };

        var result = feature.WithRotatedRight90().WithRotatedRight90().WithRotatedRight90().WithRotatedRight90();

        Assert.Equal(10, result.X);
        Assert.Equal(20, result.Y);
    }

    [Fact]
    public void RotatedRight90_ThenRotatedLeft90_ReturnsToOriginalPosition()
    {
        var feature = new Feature { Name = "hole1", X = 10, Y = 20, Z = 5 };

        var result = feature.WithRotatedRight90().WithRotatedLeft90();

        Assert.Equal(10, result.X);
        Assert.Equal(20, result.Y);
    }
}
