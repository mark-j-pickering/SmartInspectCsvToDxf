namespace SmartInspectCsvToDxf.Models;

public sealed class Feature
{
    public string Name { get; init; } = string.Empty;
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
    public double Diameter { get; init; }
    public double Radius { get; init; }

    // Set only for line features (e.g. a PDF report's "2D Line N" blocks), where the
    // measured geometry is a segment between two actual points rather than a centre.
    public double? X2 { get; init; }
    public double? Y2 { get; init; }
    public double? Z2 { get; init; }

    public bool IsLine => X2.HasValue && Y2.HasValue;

    public Feature WithMirrorY()
    {
        return new Feature
        {
            Name = Name,
            X = -X,
            Y = Y,
            Z = Z,
            Diameter = Diameter,
            Radius = Radius,
            X2 = X2.HasValue ? -X2.Value : null,
            Y2 = Y2,
            Z2 = Z2
        };
    }

    public Feature WithMirrorX()
    {
        return new Feature
        {
            Name = Name,
            X = X,
            Y = -Y,
            Z = Z,
            Diameter = Diameter,
            Radius = Radius,
            X2 = X2,
            Y2 = Y2.HasValue ? -Y2.Value : null,
            Z2 = Z2
        };
    }

    // Rotates X/Y by 90 degrees about Z (clockwise, in a standard Y-up axis
    // convention); Z is elevation and is never touched by rotation.
    public Feature WithRotatedRight90()
    {
        return new Feature
        {
            Name = Name,
            X = Y,
            Y = -X,
            Z = Z,
            Diameter = Diameter,
            Radius = Radius,
            X2 = Y2,
            Y2 = X2.HasValue ? -X2.Value : null,
            Z2 = Z2
        };
    }

    public Feature WithRotatedLeft90()
    {
        return new Feature
        {
            Name = Name,
            X = -Y,
            Y = X,
            Z = Z,
            Diameter = Diameter,
            Radius = Radius,
            X2 = Y2.HasValue ? -Y2.Value : null,
            Y2 = X2,
            Z2 = Z2
        };
    }

    // Arbitrary-angle rotation about whichever axis the given drawing plane drops (Z for
    // XY, Y for XZ, X for YZ), matching WithRotatedRight90's clockwise-positive convention
    // (verified: at exactly 90 degrees this produces the same (x,y) -> (y,-x) mapping).
    // Unlike WithRotatedRight90/WithRotatedLeft90 - which always rotate raw X/Y regardless
    // of plane, a pre-existing limitation of the 90-degree-step buttons - this rotates only
    // the two axes the active plane actually maps to, so it stays correct for reports whose
    // flat face is XZ or YZ (e.g. holes drilled along an axis).
    public Feature WithRotatedInPlane(double angleDegrees, DrawingPlane plane)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);

        (double U, double V) Rotate(double u, double v) => (u * cos + v * sin, -u * sin + v * cos);

        var x = X;
        var y = Y;
        var z = Z;
        double? x2 = X2;
        double? y2 = Y2;
        double? z2 = Z2;

        switch (plane)
        {
            case DrawingPlane.XY:
                (x, y) = Rotate(X, Y);
                if (X2.HasValue && Y2.HasValue)
                    (x2, y2) = Rotate(X2.Value, Y2.Value);
                break;
            case DrawingPlane.XZ:
                (x, z) = Rotate(X, Z);
                if (X2.HasValue && Z2.HasValue)
                    (x2, z2) = Rotate(X2.Value, Z2.Value);
                break;
            case DrawingPlane.YZ:
                (y, z) = Rotate(Y, Z);
                if (Y2.HasValue && Z2.HasValue)
                    (y2, z2) = Rotate(Y2.Value, Z2.Value);
                break;
        }

        return new Feature
        {
            Name = Name,
            X = x,
            Y = y,
            Z = z,
            Diameter = Diameter,
            Radius = Radius,
            X2 = x2,
            Y2 = y2,
            Z2 = z2
        };
    }
}
