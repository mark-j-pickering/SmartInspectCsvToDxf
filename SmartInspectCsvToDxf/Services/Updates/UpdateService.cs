using System.Diagnostics;
using System.Reflection;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace SmartInspectCsvToDxf.Services.Updates;

// Owns all Velopack/GitHub interaction so MainForm only ever sees UpdateCheckResult /
// UpdateInfo - no HttpClient, UpdateManager, or Velopack types leak into the form.
public sealed class UpdateService
{
    private const string RepositoryUrl = "https://github.com/mark-j-pickering/SmartInspectCsvToDxf";

    // Not a user-facing setting (nothing in AppSettings/the UI exposes it) - an explicit
    // opt-in for maintainers testing a prerelease build, per the "ignore drafts/prereleases
    // unless an explicit development setting enables them" requirement.
    private const string AllowPrereleaseEnvironmentVariable = "SMARTINSPECTCSVTODXF_ALLOW_PRERELEASE_UPDATES";

    private readonly UpdateManager _updateManager;
    private readonly UpdateDiagnosticLog _log;

    public UpdateService(UpdateDiagnosticLog log)
    {
        _log = log;
        var source = new GithubSource(RepositoryUrl, accessToken: null, prerelease: IsPrereleaseOptInEnabled());
        _updateManager = new UpdateManager(source);
    }

    public bool IsInstalled => _updateManager.IsInstalled;

    // Velopack update checks only make sense for a real installed copy: running from
    // Visual Studio/`dotnet run` isn't an install and throws NotInstalledException, and a
    // debugger attached to a real install is almost always a maintainer, not an end user.
    public static bool ShouldSkipAutomaticStartupCheck =>
        Debugger.IsAttached && Environment.GetEnvironmentVariable("SMARTINSPECTCSVTODXF_FORCE_UPDATE_CHECK") != "1";

    public string GetCurrentVersionText()
    {
        if (_updateManager.IsInstalled)
        {
            var installed = _updateManager.CurrentVersion;
            if (installed is not null)
                return installed.ToString();
        }

        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return informational ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        var currentVersion = GetCurrentVersionText();

        try
        {
            var info = await _updateManager.CheckForUpdatesAsync();
            if (info is null)
            {
                _log.Info($"Update check: already up to date (current {currentVersion}).");
                return new UpdateCheckResult(UpdateCheckStatus.UpToDate, currentVersion);
            }

            var availableVersion = info.TargetFullRelease.Version.ToString();
            _log.Info($"Update check: {availableVersion} available (current {currentVersion}).");
            return new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, currentVersion, availableVersion, info);
        }
        catch (NotInstalledException ex)
        {
            _log.Info($"Update check skipped - not running as a Velopack install: {ex.Message}");
            return new UpdateCheckResult(
                UpdateCheckStatus.CheckFailed,
                currentVersion,
                ErrorMessage: "Automatic updates are only available in the installed version of the application.");
        }
        catch (Exception ex)
        {
            _log.Error("Update check failed.", ex);
            return new UpdateCheckResult(UpdateCheckStatus.CheckFailed, currentVersion, ErrorMessage: DescribeError(ex));
        }
    }

    public async Task DownloadUpdateAsync(UpdateInfo update, Action<int>? onProgress, CancellationToken cancellationToken)
    {
        try
        {
            await _updateManager.DownloadUpdatesAsync(update, onProgress, cancellationToken);
        }
        catch (Exception ex)
        {
            _log.Error("Update download failed.", ex);
            throw;
        }
    }

    // Never touches the running exe directly - Velopack stages the new version in its own
    // versioned app directory and swaps a shell shortcut/registry pointer, then this call
    // exits the current process and relaunches the new one.
    public void ApplyUpdateAndRestart(UpdateInfo update)
    {
        _log.Info($"Applying update {update.TargetFullRelease.Version} and restarting.");
        _updateManager.ApplyUpdatesAndRestart(update.TargetFullRelease);
    }

    private static bool IsPrereleaseOptInEnabled() =>
        Environment.GetEnvironmentVariable(AllowPrereleaseEnvironmentVariable) is "1" or "true";

    // HttpRequestException.StatusCode is only set once a response actually arrived (a real
    // HTTP error status, e.g. a 404 for a missing/inaccessible release asset) - null means
    // the request never got a response at all (DNS failure, no route, TLS handshake failure
    // etc.), which is the only case that's actually a connectivity problem.
    private static string DescribeError(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: { } status } =>
            $"GitHub returned an unexpected response ({(int)status} {status}) while checking for updates.",
        HttpRequestException => "Could not reach GitHub. Check your internet connection and try again.",
        TaskCanceledException or OperationCanceledException => "The update check timed out.",
        IOException => "A file error occurred while checking for updates.",
        _ => "An unexpected error occurred while checking for updates."
    };
}
