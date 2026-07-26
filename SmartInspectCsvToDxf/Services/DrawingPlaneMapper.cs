using SmartInspectCsvToDxf.Models;

namespace SmartInspectCsvToDxf.Services;

public static class DrawingPlaneMapper
{
    // Maps a feature's 3D coordinates onto the chosen 2D drawing plane: U/V are the
    // two axes laid out on the drawing (screen X/Y in the preview, entity X/Y in the
    // DXF); Elevation is the dropped axis, carried through unchanged (matches the
    // app's existing behavior of using Center.z as a literal DXF elevation).
    public static (double U, double V, double Elevation) Project(Feature feature, DrawingPlane plane) =>
        Project(feature.X, feature.Y, feature.Z, plane);

    // Overload for a line feature's second point (Feature.X2/Y2/Z2), which isn't its own Feature.
    public static (double U, double V, double Elevation) Project(double x, double y, double z, DrawingPlane plane) => plane switch
    {
        DrawingPlane.XY => (x, y, z),
        DrawingPlane.XZ => (x, z, y),
        DrawingPlane.YZ => (y, z, x),
        _ => (x, y, z)
    };

    public static (string U, string V) AxisLabels(DrawingPlane plane) => plane switch
    {
        DrawingPlane.XY => ("X", "Y"),
        DrawingPlane.XZ => ("X", "Z"),
        DrawingPlane.YZ => ("Y", "Z"),
        _ => ("X", "Y")
    };
}
