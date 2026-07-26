using SmartInspectCsvToDxf.Models;
using SmartInspectCsvToDxf.Services;

namespace SmartInspectCsvToDxf.Tests;

public sealed class DrawingPlaneDetectorTests
{
    [Fact]
    public void Detect_ReturnsXY_WhenZBarelyVaries()
    {
        // Typical SmartInspect report: a flat part face, Center.z ~ 0 for every feature.
        var features = new List<Feature>
        {
            new() { Name = "a", X = 10, Y = 20, Z = 0 },
            new() { Name = "b", X = -240, Y = -135, Z = 0 },
            new() { Name = "c", X = 139, Y = -276, Z = 0 }
        };

        Assert.Equal(DrawingPlane.XY, DrawingPlaneDetector.Detect(features));
    }

    [Fact]
    public void Detect_ReturnsXZ_WhenYBarelyVaries()
    {
        // Matches the 8HP-K24 Honda report: holes drilled along the Y axis, so Y is
        // near-constant while X and Z carry the real layout.
        var features = new List<Feature>
        {
            new() { Name = "a", X = -365.224, Y = -149.637, Z = 234.619 },
            new() { Name = "b", X = -358.282, Y = -149.656, Z = 400.023 },
            new() { Name = "c", X = 8.299, Y = -149.377, Z = 351.202 }
        };

        Assert.Equal(DrawingPlane.XZ, DrawingPlaneDetector.Detect(features));
    }

    [Fact]
    public void Detect_ReturnsYZ_WhenXBarelyVaries()
    {
        var features = new List<Feature>
        {
            new() { Name = "a", X = 5.001, Y = -20, Z = 100 },
            new() { Name = "b", X = 5.002, Y = 40, Z = -300 },
            new() { Name = "c", X = 4.998, Y = 10, Z = 50 }
        };

        Assert.Equal(DrawingPlane.YZ, DrawingPlaneDetector.Detect(features));
    }

    [Fact]
    public void Detect_ReturnsXY_ForEmptyFeatureList()
    {
        Assert.Equal(DrawingPlane.XY, DrawingPlaneDetector.Detect([]));
    }

    [Fact]
    public void Detect_ReturnsXY_WhenAllRangesAreEqual()
    {
        // A degenerate tie (e.g. a single feature, or truly cubic spread) should fall
        // back to the app's historical default rather than an arbitrary axis.
        var features = new List<Feature> { new() { Name = "a", X = 1, Y = 1, Z = 1 } };

        Assert.Equal(DrawingPlane.XY, DrawingPlaneDetector.Detect(features));
    }
}
