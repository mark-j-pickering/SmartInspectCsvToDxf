using Velopack;

namespace SmartInspectCsvToDxf.Services.Updates;

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    CheckFailed
}

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    string CurrentVersion,
    string? AvailableVersion = null,
    UpdateInfo? UpdateInfo = null,
    string? ErrorMessage = null);
