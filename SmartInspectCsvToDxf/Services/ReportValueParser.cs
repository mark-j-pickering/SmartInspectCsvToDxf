using System.Globalization;
using System.Text.RegularExpressions;

namespace SmartInspectCsvToDxf.Services;

internal static partial class ReportValueParser
{
    public static double? ParseLeadingNumber(string text)
    {
        var match = LeadingNumberRegex().Match(text);
        return match.Success ? double.Parse(match.Value, CultureInfo.InvariantCulture) : null;
    }

    [GeneratedRegex(@"-?\d+(\.\d+)?")]
    private static partial Regex LeadingNumberRegex();
}
