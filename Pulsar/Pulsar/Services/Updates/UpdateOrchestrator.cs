using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Models.Enums;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services.Updates;

/// <summary>High-level state of the update orchestrator.</summary>
public enum UpdateState
{
    /// <summary>No check has run yet (or the feature is disabled).</summary>
    Idle,
    /// <summary>A check is currently in progress.</summary>
    Checking,
    /// <summary>Last check confirmed the app is up to date.</summary>
    UpToDate,
    /// <summary>A newer release is available.</summary>
    UpdateAvailable,
    /// <summary>Downloading the update asset.</summary>
    Downloading,
    /// <summary>Download complete and ready to launch the installer.</summary>
    ReadyToInstall,
    /// <summary>Last check or download failed.</summary>
    Failed,
}

/// <summary>
/// Orchestrates the in-app update flow: version check, asset download with
/// integrity verification, tray notification, and installer handoff.
/// The pure check/download services (<see cref="UpdateCheckService"/>,
/// <see cref="UpdateDownloadService"/>) stay network-only; this class owns
/// the UI-facing state and the startup/notification wiring.
/// </summary>
public partial class UpdateOrchestrator : ObservableObject
{
    private readonly UpdateCheckService _checkService;
    private readonly UpdateDownloadService _downloadService;
    private readonly ITrayService _trayService;
    private readonly IConfigService _configService;
    private readonly ILocalizationService _loc;
    private readonly ILogger<UpdateOrchestrator>? _logger;

    private int _startupNotificationFired; // Interlocked flag: 0 = not fired, 1 = fired

    [ObservableProperty]
    private UpdateState _state = UpdateState.Idle;

    [ObservableProperty]
    private string? _latestTag;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private double _downloadProgress; // 0.0 - 1.0

    [ObservableProperty]
    private string? _downloadedFilePath;

    /// <summary>Normalized current version (no "v" prefix) used for comparison.</summary>
    public string CurrentVersion { get; }

    /// <summary>The full release info from the last successful check (assets, digests).</summary>
    public GitHubReleaseInfo? LastRelease { get; private set; }

    public bool IsAutoCheckEnabled { get; private set; } = true;

    public UpdateOrchestrator(
        UpdateCheckService checkService,
        UpdateDownloadService downloadService,
        ITrayService trayService,
        IConfigService configService,
        ILocalizationService loc,
        ILogger<UpdateOrchestrator>? logger = null)
    {
        _checkService = checkService ?? throw new ArgumentNullException(nameof(checkService));
        _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
        _trayService = trayService ?? throw new ArgumentNullException(nameof(trayService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _loc = loc ?? throw new ArgumentNullException(nameof(loc));
        _logger = logger;

        CurrentVersion = ResolveCurrentVersion();

        _downloadService.ProgressChanged += OnDownloadProgress;
    }

    /// <summary>
    /// Called from <see cref="AppStartupCoordinator"/> during deferred warm-up.
    /// Reads the auto-check flag from config and runs a background check when
    /// enabled. A single tray notification is shown on UpdateAvailable; repeated
    /// startups do not re-notify for the same session.
    /// </summary>
    public async Task StartupCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await _configService.LoadSnapshotAsync().ConfigureAwait(false);
            IsAutoCheckEnabled = config?.Settings?.Update?.AutoCheckEnabled ?? true;

            if (!IsAutoCheckEnabled)
            {
                _logger?.LogInformation("[Update] Auto-check disabled in config; skipping startup check");
                return;
            }

            // Defer slightly so the check does not compete with first-launch UI rendering.
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);

            var result = await CheckAsync(cancellationToken).ConfigureAwait(false);
            if (result == UpdateState.UpdateAvailable
                && Interlocked.Exchange(ref _startupNotificationFired, 1) == 0)
            {
                _trayService.ShowNotification(
                    _loc["Update.Tray.Title"],
                    string.Format(_loc["Update.Tray.BodyFormat"], LatestTag),
                    PulsarNotificationIcon.Info);
            }
        }
        catch (OperationCanceledException)
        {
            // App shutting down during the deferred check — harmless.
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Update] Startup check failed");
            State = UpdateState.Failed;
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Runs a three-tier update check. Returns the resulting <see cref="UpdateState"/>.
    /// Safe to call from the UI (manual "Check for updates" button).
    /// </summary>
    public async Task<UpdateState> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (State is UpdateState.Checking or UpdateState.Downloading)
        {
            return State;
        }

        State = UpdateState.Checking;
        ErrorMessage = null;
        DownloadProgress = 0;
        DownloadedFilePath = null;

        try
        {
            var result = await _checkService.CheckAsync(CurrentVersion, cancellationToken).ConfigureAwait(false);

            switch (result.Outcome)
            {
                case UpdateCheckOutcome.UpToDate:
                    LatestTag = result.LatestTag;
                    LastRelease = result.Release;
                    State = UpdateState.UpToDate;
                    break;

                case UpdateCheckOutcome.UpdateAvailable:
                    LatestTag = result.LatestTag;
                    LastRelease = result.Release;
                    State = UpdateState.UpdateAvailable;
                    break;

                case UpdateCheckOutcome.NetworkError:
                    ErrorMessage = result.Error ?? _loc["Update.Error.Network"];
                    State = UpdateState.Failed;
                    break;

                case UpdateCheckOutcome.ParseFailed:
                default:
                    ErrorMessage = result.Error ?? _loc["Update.Error.Parse"];
                    State = UpdateState.Failed;
                    break;
            }

            _logger?.LogInformation("[Update] Check result: {Outcome} (current={Current}, latest={Latest}, source={Source})",
                result.Outcome, CurrentVersion, result.LatestTag, result.Source);
            return State;
        }
        catch (OperationCanceledException)
        {
            State = UpdateState.Idle;
            return State;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Update] Check threw unexpectedly");
            ErrorMessage = ex.Message;
            State = UpdateState.Failed;
            return State;
        }
    }

    /// <summary>
    /// Downloads the preferred asset (Setup.exe first, then standalone zip) and
    /// verifies integrity. On success transitions to <see cref="UpdateState.ReadyToInstall"/>.
    /// </summary>
    public async Task<bool> DownloadAsync(CancellationToken cancellationToken = default)
    {
        if (LastRelease == null || State != UpdateState.UpdateAvailable)
        {
            return false;
        }

        var asset = PickPreferredAsset(LastRelease);
        if (asset == null)
        {
            ErrorMessage = _loc["Update.Error.NoAsset"];
            State = UpdateState.Failed;
            return false;
        }

        State = UpdateState.Downloading;
        DownloadProgress = 0;
        ErrorMessage = null;

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"Pulsar-Update-{LatestTag ?? "latest"}-{Path.GetFileName(asset.Name)}");

        try
        {
            var result = await _downloadService.DownloadAsync(asset, tempPath, cancellationToken).ConfigureAwait(false);

            if (!result.Success || result.FilePath == null)
            {
                ErrorMessage = result.Error ?? _loc["Update.Error.Download"];
                State = UpdateState.Failed;
                return false;
            }

            DownloadedFilePath = result.FilePath;
            State = UpdateState.ReadyToInstall;
            _logger?.LogInformation("[Update] Download ready: {Path} (integrity={Integrity})",
                result.FilePath, result.IntegrityMode);
            return true;
        }
        catch (OperationCanceledException)
        {
            State = UpdateState.UpdateAvailable;
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Update] Download threw unexpectedly");
            ErrorMessage = ex.Message;
            State = UpdateState.Failed;
            return false;
        }
    }

    /// <summary>
    /// Launches the downloaded installer and shuts down the app. For zip assets
    /// (no installer) opens the containing folder in Explorer instead.
    /// </summary>
    public async Task InstallAsync()
    {
        if (string.IsNullOrEmpty(DownloadedFilePath) || !File.Exists(DownloadedFilePath))
        {
            ErrorMessage = _loc["Update.Error.NoDownload"];
            State = UpdateState.Failed;
            return;
        }

        try
        {
            var ext = Path.GetExtension(DownloadedFilePath).Equals(".exe", StringComparison.OrdinalIgnoreCase);
            if (ext)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = DownloadedFilePath,
                    UseShellExecute = true,
                    Verb = "runas" // installer needs elevation
                });
            }
            else
            {
                // Standalone zip: reveal in Explorer rather than trying to "install".
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{DownloadedFilePath}\"",
                    UseShellExecute = true
                });
            }

            await Task.Delay(500).ConfigureAwait(false); // let the installer start
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Update] Failed to launch installer");
            ErrorMessage = ex.Message;
        }
    }

    private void OnDownloadProgress(long bytesRead, long totalBytes)
    {
        if (totalBytes > 0)
        {
            DownloadProgress = Math.Clamp((double)bytesRead / totalBytes, 0.0, 1.0);
        }
    }

    /// <summary>
    /// Asset preference: Setup.exe (installer) first, then the standalone zip.
    /// The Atom/redirect tiers carry no asset list, so this only matters when
    /// the REST API tier succeeded.
    /// </summary>
    private static GitHubReleaseAsset? PickPreferredAsset(GitHubReleaseInfo release)
    {
        var assets = release.Assets ?? Array.Empty<GitHubReleaseAsset>();
        return assets.FirstOrDefault(a => a.Name.EndsWith("Setup.exe", StringComparison.OrdinalIgnoreCase))
               ?? assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
               ?? assets.FirstOrDefault();
    }

    private static string ResolveCurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version == null) return "1.0.0";
        // Build=0 is commonly unset (e.g. 1.2.0); render as three segments when zero.
        return version.Build == 0
            ? $"{version.Major}.{version.Minor}.{version.Revision}"
            : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
