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

    [Fact]
    public void WithRotatedInPlane_ZeroDegrees_IsNoOp()
    {
        var feature = new Feature { Name = "hole1", X = 10, Y = 20, Z = 5 };

        var result = feature.WithRotatedInPlane(0, DrawingPlane.XY);

        Assert.Equal(10, result.X);
        Assert.Equal(20, result.Y);
        Assert.Equal(5, result.Z);
    }

    [Fact]
    public void WithRotatedInPlane_XY_MatchesRight90Convention()
    {
        var feature = new Feature { Name = "hole1", X = 10, Y = 20, Z = 5 };

        var result = feature.WithRotatedInPlane(90, DrawingPlane.XY);

        Assert.Equal(20, result.X, precision: 10);
        Assert.Equal(-10, result.Y, precision: 10);
        Assert.Equal(5, result.Z);
    }

    [Fact]
    public void WithRotatedInPlane_XZ_RotatesXAndZ_LeavesYUnchanged()
    {
        var feature = new Feature { Name = "hole1", X = 10, Y = 99, Z = 20 };

        var result = feature.WithRotatedInPlane(90, DrawingPlane.XZ);

        Assert.Equal(20, result.X, precision: 10);
        Assert.Equal(-10, result.Z, precision: 10);
        Assert.Equal(99, result.Y);
    }

    [Fact]
    public void WithRotatedInPlane_YZ_RotatesYAndZ_LeavesXUnchanged()
    {
        var feature = new Feature { Name = "hole1", X = 99, Y = 10, Z = 20 };

        var result = feature.WithRotatedInPlane(90, DrawingPlane.YZ);

        Assert.Equal(20, result.Y, precision: 10);
        Assert.Equal(-10, result.Z, precision: 10);
        Assert.Equal(99, result.X);
    }

    [Fact]
    public void WithRotatedInPlane_LineFeature_RotatesBothEndpoints()
    {
        var feature = new Feature { Name = "line1", X = 0, Y = 0, X2 = 10, Y2 = 0, Z2 = 0 };

        var result = feature.WithRotatedInPlane(90, DrawingPlane.XY);

        Assert.Equal(0, result.X2!.Value, precision: 10);
        Assert.Equal(-10, result.Y2!.Value, precision: 10);
    }

    [Fact]
    public void WithRotatedInPlane_NonLineFeature_LeavesSecondPointNull()
    {
        var feature = new Feature { Name = "hole1", X = 10, Y = 20 };

        var result = feature.WithRotatedInPlane(90, DrawingPlane.XY);

        Assert.Null(result.X2);
        Assert.Null(result.Y2);
        Assert.Null(result.Z2);
    }

    [Fact]
    public void WithRotatedInPlane_NonRightAngle_RotatesCorrectly()
    {
        var feature = new Feature { Name = "hole1", X = 10, Y = 0 };

        var result = feature.WithRotatedInPlane(30, DrawingPlane.XY);

        // (10,0) rotated 30 degrees clockwise-positive: x' = 10*cos30, y' = -10*sin30.
        Assert.Equal(10 * Math.Cos(Math.PI / 6), result.X, precision: 10);
        Assert.Equal(-10 * Math.Sin(Math.PI / 6), result.Y, precision: 10);
    }
}
