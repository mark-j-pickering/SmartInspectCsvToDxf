using SmartInspectCsvToDxf.Models;

namespace SmartInspectCsvToDxf.Services;

public static class DrawingPlaneDetector
{
    // Drops whichever axis varies least across the feature set - that's the part's
    // face normal / depth direction - and lays the drawing out on the other two.
    // Ties favor XY, then XZ: this matches the common case where every feature's
    // Center.z is exported as an exact 0 (a genuinely flat part).
    public static DrawingPlane Detect(IReadOnlyCollection<Feature> features)
    {
        if (features.Count == 0)
            return DrawingPlane.XY;

        var rangeX = Range(features.Select(f => f.X));
        var rangeY = Range(features.Select(f => f.Y));
        var rangeZ = Range(features.Select(f => f.Z));

        if (rangeZ <= rangeX && rangeZ <= rangeY)
            return DrawingPlane.XY;

        if (rangeY <= rangeX && rangeY <= rangeZ)
            return DrawingPlane.XZ;

        return DrawingPlane.YZ;
    }

    private static double Range(IEnumerable<double> values)
    {
        var min = double.MaxValue;
        var max = double.MinValue;

        foreach (var value in values)
        {
            if (value < min) min = value;
            if (value > max) max = value;
        }

        return max - min;
    }
}
