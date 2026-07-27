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
}
