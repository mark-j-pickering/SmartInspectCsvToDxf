using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using SmartInspectCsvToDxf.Models;

namespace SmartInspectCsvToDxf.Services;

public static class SmartInspectPdfReportReader
{
    // Rows whose leading word is one of these are either data we care about
    // (Center.x/y/z, Diameter, Radius) or data we deliberately ignore (Circularity,
    // Flatness, Straightness, the Readings sub-table, the actual/nominal/dev header
    // row) - anything else that starts with a letter is a feature/section name row.
    private static readonly HashSet<string> RecognizedRowKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "actual", "center.x", "center.y", "center.z", "diameter", "radius",
        "circularity", "flatness", "straightness", "readings"
    };

    private const double RowTolerance = 6.0;

    public static List<Feature> Read(string path)
    {
        using var document = PdfDocument.Open(path);
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

        for (var pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
        {
            var page = document.GetPage(pageNumber);

            foreach (var row in GroupWordsIntoRows(page.GetWords()))
            {
                var firstWord = row[0].Text;

                if (!RecognizedRowKeywords.Contains(firstWord) && firstWord.Length > 0 && char.IsLetter(firstWord[0]))
                {
                    FlushCurrent();
                    currentName = BuildFeatureName(row);
                    centerX = centerY = centerZ = diameter = radius = null;
                    continue;
                }

                if (row.Count < 2 || currentName is null)
                    continue;

                var value = ReportValueParser.ParseLeadingNumber(row[1].Text);
                if (value is null)
                    continue;

                switch (firstWord.ToLowerInvariant())
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
        }

        FlushCurrent();
        return features;
    }

    private static string BuildFeatureName(List<Word> row)
    {
        var words = row;
        if (words.Count > 1 && words[^1].Text.StartsWith("Readings:", StringComparison.OrdinalIgnoreCase))
            words = words.Take(words.Count - 1).ToList();

        return string.Join(" ", words.Select(w => w.Text));
    }

    // Values are set in a slightly larger font than their labels, so raw Top/Bottom
    // don't line up exactly - group by vertical center with tolerance instead.
    private static List<List<Word>> GroupWordsIntoRows(IEnumerable<Word> words)
    {
        var rows = new List<List<Word>>();

        foreach (var word in words.OrderByDescending(VerticalCenter))
        {
            var center = VerticalCenter(word);
            var row = rows.FirstOrDefault(r => Math.Abs(r.Average(VerticalCenter) - center) < RowTolerance);

            if (row is null)
            {
                row = [];
                rows.Add(row);
            }

            row.Add(word);
        }

        foreach (var row in rows)
            row.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));

        return rows;
    }

    private static double VerticalCenter(Word word) => (word.BoundingBox.Top + word.BoundingBox.Bottom) / 2.0;
}
