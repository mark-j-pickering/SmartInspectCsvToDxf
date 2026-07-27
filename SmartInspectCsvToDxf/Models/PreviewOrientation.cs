namespace SmartInspectCsvToDxf.Models;

// Cumulative preview/export orientation: how many 90-degree clockwise rotations to
// apply, then whether to mirror about the X and/or Y axis. Always applied fresh to a
// file's original, untransformed features (never accumulated onto already-transformed
// coordinates) so toggling any one control always reflects the full combined state
// rather than compounding on top of a previous click.
public readonly record struct PreviewOrientation(int RotationSteps, bool MirrorX, bool MirrorY)
{
    public static readonly PreviewOrientation Identity = new(0, false, false);

    public bool IsIdentity => RotationSteps == 0 && !MirrorX && !MirrorY;

    public PreviewOrientation RotatedRight() => this with { RotationSteps = (RotationSteps + 1) % 4 };

    public PreviewOrientation RotatedLeft() => this with { RotationSteps = (RotationSteps + 3) % 4 };

    public PreviewOrientation ToggledMirrorX() => this with { MirrorX = !MirrorX };

    public PreviewOrientation ToggledMirrorY() => this with { MirrorY = !MirrorY };

    public Feature Apply(Feature feature)
    {
        var result = feature;
        for (var i = 0; i < RotationSteps; i++)
            result = result.WithRotatedRight90();

        if (MirrorX)
            result = result.WithMirrorX();

        if (MirrorY)
            result = result.WithMirrorY();

        return result;
    }

    public string Describe()
    {
        var parts = new List<string>();
        if (RotationSteps != 0)
            parts.Add($"rotated {RotationSteps * 90}° CW");

        if (MirrorX)
            parts.Add("mirrored about X axis");

        if (MirrorY)
            parts.Add("mirrored about Y axis");

        return string.Join(", ", parts);
    }

    public string FileNameSuffix()
    {
        var suffix = string.Empty;
        if (RotationSteps != 0)
            suffix += $"_rot{RotationSteps * 90}";

        if (MirrorX)
            suffix += "_mirrored_x";

        if (MirrorY)
            suffix += "_mirrored_y";

        return suffix;
    }
}
