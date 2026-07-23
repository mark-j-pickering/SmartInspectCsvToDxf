using SmartInspectCsvToDxf.Models;

namespace SmartInspectCsvToDxf.Services;

public static class ReportFileReader
{
    public static readonly string[] FilePatterns = ["*.txt", "*.xml", "*.csv", "*.pdf"];

    public static List<Feature> Read(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".xml" => SmartInspectXmlReportReader.Read(path),
            ".csv" => CsvFeatureReader.Read(path),
            ".pdf" => SmartInspectPdfReportReader.Read(path),
            _ => SmartInspectReportReader.Read(path)
        };
    }
}
