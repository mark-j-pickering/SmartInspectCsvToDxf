using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using SmartInspectCsvToDxf.Models;

namespace SmartInspectCsvToDxf.Services;

public static class SmartInspectPdfReportReader
{
    // Rows whose leading word is one of these are either data we care about
    // (Center.x/y/z, Diameter, Radius) or data we deliberately ignore (Circularity,
    // Flatness, Straightness, the Readings sub-table, the actual/nominal/dev header
    // row, the standalone "Properties" sub-heading some report templates print under
    // every feature name, the "x (mm) y (mm) z (mm) ..." column-header row under
    // "Readings", or a "Solver method:"/"Nr. of readings:" line that renders far
    // enough from the feature name to land in its own row) - anything else that
    // starts with a letter is a feature/section name row.
    private static readonly HashSet<string> RecognizedRowKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "actual", "center.x", "center.y", "center.z", "diameter", "radius",
        "circularity", "flatness", "straightness", "readings", "properties",
        "solver", "nr.", "x"
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

        // Line features (e.g. "2D Line N") have no Center.x/y at all - their geometry is
        // the segment between two measured points instead, printed as separate "Readings"
        // rows: a data row that echoes the feature's own name + "-" + x/y/z, immediately
        // followed by a standalone "ActualPtN" label row (offset enough vertically that it
        // never merges into the same row). Plane features use the same ActualPt1/2/3
        // layout but with three points, so only exactly two points is treated as a line.
        (double X, double Y, double Z)? point1 = null;
        (double X, double Y, double Z)? point2 = null;
        (double X, double Y, double Z)? point3 = null;
        (double X, double Y, double Z)? pendingPoint = null;

        void FlushCurrent()
        {
            if (currentName is null)
                return;

            if (centerX.HasValue && centerY.HasValue)
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
            else if (point1.HasValue && point2.HasValue && !point3.HasValue)
            {
                features.Add(new Feature
                {
                    Name = currentName,
                    X = point1.Value.X,
                    Y = point1.Value.Y,
                    Z = point1.Value.Z,
                    X2 = point2.Value.X,
                    Y2 = point2.Value.Y,
                    Z2 = point2.Value.Z
                });
            }
        }

        for (var pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
        {
            var page = document.GetPage(pageNumber);

            foreach (var row in GroupWordsIntoRows(page.GetWords()))
            {
                var firstWord = row[0].Text;

                // "ActualPtN" is its own standalone row (offset far enough from the numeric
                // reading above it that row-grouping never merges them) - assign whatever
                // point was captured from that preceding data row into the matching slot.
                // Must be checked before the new-feature-name test below, since "ActualPt1"
                // otherwise looks exactly like the start of a new feature/section name.
                if (firstWord.StartsWith("ActualPt", StringComparison.OrdinalIgnoreCase))
                {
                    if (pendingPoint.HasValue)
                    {
                        var slot = firstWord.Substring("ActualPt".Length);
                        switch (slot)
                        {
                            case "1": point1 = pendingPoint; break;
                            case "2": point2 = pendingPoint; break;
                            case "3": point3 = pendingPoint; break;
                        }
                        pendingPoint = null;
                    }
                    continue;
                }

                // A feature/section name row starts with a letter (e.g. "Circle 3", "World")
                // or, for line features, a digit-led type prefix like "2D"/"3D" ("2D Line 8").
                // Rather than hardcode every possible prefix, also recognize the row by the
                // "Solver method: Standard   Nr. of readings: N" template text that renders
                // alongside the name (the same "Solver"/"Nr." markers BuildFeatureName already
                // trims off) - that text is unique to name rows, so its presence identifies a
                // new feature regardless of what the type prefix looks like.
                // Reading-echo rows for line/plane features repeat the name followed by a
                // literal "-" (e.g. "2D Line 8 - 2.941 -2.436 ..."), so a "-" anywhere in the
                // row rules out a name-row match - without that check, those rows would be
                // misread as new feature names and wipe out the name they belong to.
                var hasNameRowMarker = row.Any(w => w.Text is "Solver" or "Nr.");
                var looksLikeNameStart = firstWord.Length > 0
                    && !RecognizedRowKeywords.Contains(firstWord)
                    && (char.IsLetter(firstWord[0]) || firstWord is "2D" or "3D" || hasNameRowMarker)
                    && !row.Any(w => w.Text == "-");

                if (looksLikeNameStart)
                {
                    FlushCurrent();
                    currentName = BuildFeatureName(row);
                    centerX = centerY = centerZ = diameter = radius = null;
                    point1 = point2 = point3 = pendingPoint = null;
                    continue;
                }

                // A reading row for a line/plane feature echoes the feature's own name,
                // then "-", then its x/y/z for this particular actual point (see ActualPtN
                // handling above). Detected structurally (a literal "-" token followed by
                // parseable numbers) rather than by matching the echoed prefix text against
                // currentName - the name is user-editable in SmartInspect (renamed features
                // are common, e.g. "RIGHT DOWL"/"CENTER-BORE" elsewhere in this same report),
                // so a text-equality check would silently stop working the moment a line
                // feature is renamed to anything not matching exactly. What actually
                // determines a feature's type is its Properties block (Center.x/Diameter for
                // a circle, Straightness for a line) - name text is irrelevant to that.
                if (currentName is not null)
                {
                    var dashIndex = row.FindIndex(w => w.Text == "-");
                    if (dashIndex > 0 && dashIndex < row.Count - 1)
                    {
                        var rest = row.Skip(dashIndex + 1).ToList();
                        var px = rest.Count > 0 ? ReportValueParser.ParseLeadingNumber(rest[0].Text) : null;
                        var py = rest.Count > 1 ? ReportValueParser.ParseLeadingNumber(rest[1].Text) : null;
                        var pz = rest.Count > 2 ? ReportValueParser.ParseLeadingNumber(rest[2].Text) : null;
                        if (px.HasValue && py.HasValue)
                            pendingPoint = (px.Value, py.Value, pz ?? 0.0);
                        continue;
                    }
                }

                if (row.Count < 2 || currentName is null)
                    continue;

                var keyword = firstWord.ToLowerInvariant();

                // These fields always carry Low/Up Tol columns (label + 2 words); an Actual
                // reading adds a 3rd word in between. When Actual (mm) is blank - an
                // unmeasured feature - PdfPig emits no word for that cell at all, so without
                // this check row[1] would be misread as the Low Tol value instead of skipped.
                var isPlacementField = keyword is "center.x" or "center.y" or "center.z" or "diameter" or "radius";
                if (isPlacementField && row.Count < 4)
                    continue;

                var value = ReportValueParser.ParseLeadingNumber(row[1].Text);
                if (value is null)
                    continue;

                switch (keyword)
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

    // Marks where the trailing "Solver method: Standard   Nr. of readings: N" template
    // text starts within a name row - it renders close enough vertically to the feature
    // name in some reports that both end up grouped into a single row.
    private static readonly HashSet<string> NameSuffixMarkers = new(StringComparer.OrdinalIgnoreCase) { "Solver", "Nr." };

    private static string BuildFeatureName(List<Word> row)
    {
        var cutIndex = row.FindIndex(w => NameSuffixMarkers.Contains(w.Text));
        var words = cutIndex >= 0 ? row.Take(cutIndex).ToList() : row;

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
