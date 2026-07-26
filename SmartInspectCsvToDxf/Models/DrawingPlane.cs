namespace SmartInspectCsvToDxf.Models;

// Which two of a feature's three coordinates form the 2D drawing (preview and DXF
// layout) - the remaining axis is carried through as elevation only. Most SmartInspect
// reports export a near-constant Center.z (a flat part face), which is why XY has
// historically been the only plane this app understood; some jobs instead have a
// near-constant Center.x or Center.y (see DrawingPlaneDetector).
public enum DrawingPlane
{
    XY,
    XZ,
    YZ
}
