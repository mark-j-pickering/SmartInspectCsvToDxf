using SmartInspectCsvToDxf.Models;

namespace SmartInspectCsvToDxf.Services;

public static class SmartInspectReportReader
{
    public static List<Feature> Read(string path)
    {
        var lines = File.ReadAllLines(path);
        var features = new List<Feature>();

        string? currentName = null;
        double? centerX = null;
        double? centerY = null;
        double? centerZ = null;
        double? diameter = null;
        double? radius = null;

        void FlushCurrent()
        {
            if (currentName is not null && centerX.HasValue && centerY.HasValue)
            {
                double r, d;
                if (radius.HasValue)
                {
                    r = radius.Value;
                    d = radius.Value * 2.0;
                }
                else if (diameter.HasValue)
                {
                    d = diameter.Value;
                    r = diameter.Value / 2.0;
                }
                else
                {
                    r = 0.0;
                    d = 0.0;
                }

                features.Add(new Feature
                {
                    Name = currentName,
                    X = centerX.Value,
                    Y = centerY.Value,
                    Z = centerZ ?? 0.0,
                    Diameter = d,
                    Radius = r
                });
            }
        }

        foreach (var line in lines)
        {
            var cells = line.Split('\t');
            var col0 = cells.Length > 0 ? cells[0].Trim() : string.Empty;
            var col1 = cells.Length > 1 ? cells[1].Trim() : string.Empty;

            if (col0.Length == 0 && col1.Length > 0)
            {
                FlushCurrent();
                currentName = col1;
                centerX = centerY = centerZ = diameter = radius = null;
                continue;
            }

            if (col0.Length == 0 || currentName is null || cells.Length <= 2)
                continue;

            var value = ReportValueParser.ParseLeadingNumber(cells[2]);
            if (value is null)
                continue;

            switch (col0.ToLowerInvariant())
            {
                case "center.x":
                    centerX = value;
                    break;
                case "center.y":
                    centerY = value;
                    break;
                case "center.z":
                    centerZ = value;
                    break;
                case "diameter":
                    diameter = value;
                    break;
                case "radius":
                    radius = value;
                    break;
            }
        }

        FlushCurrent();
        return features;
    }
}
