using System.Globalization;
using SmartInspectCsvToDxf.Models;

namespace SmartInspectCsvToDxf.Services;

// Legacy format, kept for older exports already in circulation. The simple
// name,x,y,z,diameter,radius schema doesn't reflect current SmartInspect output
// (see SmartInspectReportReader/SmartInspectXmlReportReader) and may need updating
// if a newer CSV export shape shows up.
public static class CsvFeatureReader
{
    public static List<Feature> Read(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 2)
            return [];

        var headers = SplitCsvLine(lines[0])
            .Select(h => h.Trim().ToLowerInvariant())
            .ToList();

        int FindRequired(string name)
        {
            var index = headers.IndexOf(name.ToLowerInvariant());
            if (index < 0)
                throw new InvalidDataException($"CSV is missing required column '{name}'.");
            return index;
        }

        var nameIndex = FindRequired("name");
        var xIndex = FindRequired("x");
        var yIndex = FindRequired("y");
        var zIndex = headers.IndexOf("z");
        var diameterIndex = headers.IndexOf("diameter");
        var radiusIndex = headers.IndexOf("radius");

        if (radiusIndex < 0 && diameterIndex < 0)
            throw new InvalidDataException("CSV must contain either 'radius' or 'diameter'.");

        var features = new List<Feature>();

        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var parts = SplitCsvLine(lines[i]);
            if (parts.Count <= Math.Max(nameIndex, Math.Max(xIndex, yIndex)))
                continue;

            var x = ParseDouble(parts[xIndex]);
            var y = ParseDouble(parts[yIndex]);
            var z = zIndex >= 0 && zIndex < parts.Count ? ParseDouble(parts[zIndex]) : 0.0;

            double radius;
            double diameter;

            if (radiusIndex >= 0 && radiusIndex < parts.Count && TryParseDouble(parts[radiusIndex], out radius))
            {
                diameter = radius * 2.0;
            }
            else if (diameterIndex >= 0 && diameterIndex < parts.Count && TryParseDouble(parts[diameterIndex], out diameter))
            {
                radius = diameter / 2.0;
            }
            else
            {
                continue;
            }

            features.Add(new Feature
            {
                Name = parts[nameIndex].Trim(),
                X = x,
                Y = y,
                Z = z,
                Diameter = diameter,
                Radius = Math.Abs(radius)
            });
        }

        return features;
    }

    private static double ParseDouble(string value)
    {
        if (!TryParseDouble(value, out var result))
            throw new FormatException($"Could not parse numeric value '{value}'.");
        return result;
    }

    private static bool TryParseDouble(string value, out double result)
    {
        return double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    // Small CSV splitter that handles quoted commas and escaped double quotes.
    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        result.Add(current.ToString());
        return result;
    }
}
