namespace SmartInspectCsvToDxf.Models;

public sealed class Feature
{
    public string Name { get; init; } = string.Empty;
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
    public double Diameter { get; init; }
    public double Radius { get; init; }

    public Feature WithMirrorY()
    {
        return new Feature
        {
            Name = Name,
            X = -X,
            Y = Y,
            Z = Z,
            Diameter = Diameter,
            Radius = Radius
        };
    }
}
