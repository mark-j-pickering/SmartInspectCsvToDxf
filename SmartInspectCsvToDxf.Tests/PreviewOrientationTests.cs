using SmartInspectCsvToDxf.Models;

namespace SmartInspectCsvToDxf.Tests;

public sealed class PreviewOrientationTests
{
    [Fact]
    public void Identity_IsIdentity()
    {
        Assert.True(PreviewOrientation.Identity.IsIdentity);
    }

    [Fact]
    public void RotatedRight_WrapsAfterFourSteps()
    {
        var orientation = PreviewOrientation.Identity;

        for (var i = 0; i < 4; i++)
            orientation = orientation.RotatedRight();

        Assert.Equal(0, orientation.RotationSteps);
    }

    [Fact]
    public void RotatedLeft_WrapsBackwardsFromZero()
    {
        var orientation = PreviewOrientation.Identity.RotatedLeft();

        Assert.Equal(3, orientation.RotationSteps);
    }

    [Fact]
    public void ToggledMirrorX_ThenToggledAgain_ReturnsToIdentity()
    {
        var orientation = PreviewOrientation.Identity.ToggledMirrorX().ToggledMirrorX();

        Assert.True(orientation.IsIdentity);
    }

    [Fact]
    public void Apply_RotatesBeforeMirroring()
    {
        // Rotate right once: (10, 20) -> (20, -10); then mirror about Y (negate X): (-20, -10).
        var orientation = new PreviewOrientation(1, false, true);
        var feature = new Feature { Name = "hole1", X = 10, Y = 20 };

        var result = orientation.Apply(feature);

        Assert.Equal(-20, result.X);
        Assert.Equal(-10, result.Y);
    }

    [Fact]
    public void Apply_DoesNotMutateOriginalFeature()
    {
        var orientation = new PreviewOrientation(1, true, true);
        var feature = new Feature { Name = "hole1", X = 10, Y = 20 };

        orientation.Apply(feature);

        Assert.Equal(10, feature.X);
        Assert.Equal(20, feature.Y);
    }

    [Fact]
    public void FileNameSuffix_IsEmpty_ForIdentity()
    {
        Assert.Equal(string.Empty, PreviewOrientation.Identity.FileNameSuffix());
    }

    [Fact]
    public void FileNameSuffix_CombinesAllActiveTransforms()
    {
        var orientation = new PreviewOrientation(2, true, true);

        Assert.Equal("_rot180_mirrored_x_mirrored_y", orientation.FileNameSuffix());
    }

    [Fact]
    public void Describe_IsEmpty_ForIdentity()
    {
        Assert.Equal(string.Empty, PreviewOrientation.Identity.Describe());
    }
}
