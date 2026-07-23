using System.Xml.Linq;
using SmartInspectCsvToDxf.Models;

namespace SmartInspectCsvToDxf.Services;

public static class SmartInspectXmlReportReader
{
    public static List<Feature> Read(string path)
    {
        var doc = XDocument.Load(path);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
        var features = new List<Feature>();

        foreach (var featureElement in doc.Descendants(ns + "feature"))
        {
            var name = (string?)featureElement.Attribute("name");
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var geometry = featureElement.Element(ns + "geometry");
            if (geometry is null)
                continue;

            double? centerX = null;
            double? centerY = null;
            double? centerZ = null;
            double? diameter = null;
            double? radius = null;

            foreach (var field in geometry.Elements(ns + "field"))
            {
                var fieldName = (string?)field.Attribute("name");
                var textValue = field.Element(ns + "textValue")?.Value;
                if (string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(textValue))
                    continue;

                var value = ReportValueParser.ParseLeadingNumber(textValue);
                if (value is null)
                    continue;

                switch (fieldName.ToLowerInvariant())
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

            if (!centerX.HasValue || !centerY.HasValue)
                continue;

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
                Name = name,
                X = centerX.Value,
                Y = centerY.Value,
                Z = centerZ ?? 0.0,
                Diameter = d,
                Radius = r
            });
        }

        return features;
    }
}
