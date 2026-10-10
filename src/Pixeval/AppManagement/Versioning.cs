// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Pixeval.AppManagement.Settings;
using Pixeval.Native.Update;
using Pixeval.Utilities;
using Pixeval.Utilities.GitHub;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace Pixeval.AppManagement;

public class Versioning
{
    private const string GitHubRepositoryUri = "https://github.com/Pixeval/Pixeval";

    private GithubSource? _velopackSource;
    private UpdateManager? _velopackUpdateManager;
    private UpdateInfo? _velopackUpdateInfo;
    private AppRelease? _velopackUpdateReleaseModel;
    private UpdateEngine? _updateEngine;
    private AppSettings? _settings;
    private FileLogger? _logger;
    private readonly SemaphoreSlim _updateCheckLock = new(1, 1);
    private readonly SemaphoreSlim _updateDownloadLock = new(1, 1);
    private bool _updateApplyRequested;

    public Versioning()
    {
        var assembly = typeof(Versioning).Assembly;
        CurrentVersion = assembly.GetName().Version ?? new(0, 0, 0, 0);
        CurrentVersionShortText = CurrentVersion.ToString();
        CurrentVersionFullText = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? CurrentVersionShortText;
    }

    public Version CurrentVersion { get; }

    public string CurrentVersionShortText { get; }

    /// <remarks>
    /// <see cref="AssemblyInformationalVersionAttribute"/> 包含 GitSha 信息
    /// </remarks>
    public string CurrentVersionFullText { get; }

    public Version? NewestVersion => NewestAppReleaseModel?.ParsedVersion;

    public AppRelease? NewestAppReleaseModel => _velopackUpdateReleaseModel ?? AppReleaseModels?.FirstOrDefault();

    public AppRelease? CurrentAppReleaseModel => AppReleaseModels?.FirstOrDefault(t => t.ParsedVersion == CurrentVersion || t.Version == CurrentVersionShortText);

    internal void Attach(AppSettings settings, FileLogger logger)
    {
        _settings = settings;
        _logger = logger;
        _updateEngine = null;
    }

    public UpdateEngine UpdateEngine => _updateEngine ??= UpdateEngine.CreateFromSettings(_settings?.NetworkSettings ?? new NetworkSettingsGroup());

    public void ResetUpdateEngine() => _updateEngine = null;

    public UpdateState CompareUpdateState(Version currentVersion, Version? newVersion)
    {
        if (newVersion is null)
            return UpdateState.Unknown;

        return UpdateEngine.CompareVersions(currentVersion.ToString(), newVersion.ToString());
    }

    public UpdateState UpdateState { get; private set; } = UpdateState.Unknown;

    public bool UpdateAvailable => UpdateState is not UpdateState.UpToDate and not UpdateState.Insider and not UpdateState.Unknown;

    public IReadOnlyList<AppRelease>? AppReleaseModels { get; private set; }

    public bool UsesVelopack => StoreDataMigration.IsVelopackInstallation;

    public bool CanApplyUpdate => UsesVelopack && _velopackUpdateManager?.UpdatePendingRestart is not null;

    public Version? PendingUpdateVersion => _velopackUpdateManager?.UpdatePendingRestart?.Version.Version;

    public async Task CheckForUpdateAsync()
    {
        await _updateCheckLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (UsesVelopack)
            {
                await CheckVelopackForUpdateAsync().ConfigureAwait(false);
            }
            else
            {
                var result = await UpdateEngine.CheckForUpdatesAsync(CurrentVersionShortText, includePrereleases: false).ConfigureAwait(false);
                AppReleaseModels = result.AllReleases;
                _velopackUpdateInfo = null;
                _velopackUpdateReleaseModel = result.LatestRelease;
                UpdateState = result.UpdateState;
                TouchLastCheckedUpdate();
            }
        }
        catch (Exception exception)
        {
            AppReleaseModels = null;
            _velopackUpdateInfo = null;
            _velopackUpdateReleaseModel = null;
            UpdateState = UpdateState.Unknown;
            _logger?.LogError(nameof(CheckForUpdateAsync), exception);
        }
        finally
        {
            _updateCheckLock.Release();
        }
    }

    public async Task<bool> DownloadUpdateAsync(Action<int>? progress = null, CancellationToken cancelToken = default)
    {
        await _updateDownloadLock.WaitAsync(cancelToken).ConfigureAwait(false);
        try
        {
            if (UsesVelopack)
            {
                var manager = GetVelopackUpdateManager();
                if (manager is null)
                    return false;
                if (manager.UpdatePendingRestart is not null)
                    return true;
                if (_velopackUpdateInfo is not { } updateInfo)
                    return false;

                await manager.DownloadUpdatesAsync(updateInfo, progress ?? (static _ => { }), cancelToken)
                    .ConfigureAwait(false);
                return true;
            }

            if (NewestAppReleaseModel is not { } release || release.Assets.Count == 0)
                return false;

            var asset = PickMatchingAsset(release.Assets);
            if (asset is null)
                return false;

            var updatesDir = Path.Combine(AppInfo.CacheFolder, "Updates");
            var destinationPath = Path.Combine(updatesDir, asset.Name);
            var progressAdapter = progress != null ? new Progress<int>(progress) : null;

            await UpdateEngine.DownloadAssetWithProgressAsync(asset, destinationPath, progressAdapter, cancelToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _updateDownloadLock.Release();
        }
    }

    private static ReleaseAsset? PickMatchingAsset(IReadOnlyList<ReleaseAsset> assets)
    {
        if (OperatingSystem.IsWindows())
        {
            return assets.FirstOrDefault(static a => a.Name.Contains("win-x64", StringComparison.OrdinalIgnoreCase))
                ?? assets.FirstOrDefault(static a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                ?? assets.FirstOrDefault(static a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                ?? assets.FirstOrDefault();
        }
        if (OperatingSystem.IsLinux())
        {
            return assets.FirstOrDefault(static a => a.Name.Contains("linux-x64", StringComparison.OrdinalIgnoreCase))
                ?? assets.FirstOrDefault(static a => a.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                ?? assets.FirstOrDefault();
        }
        if (OperatingSystem.IsMacOS())
        {
            return assets.FirstOrDefault(static a => a.Name.Contains("osx", StringComparison.OrdinalIgnoreCase) || a.Name.Contains("mac", StringComparison.OrdinalIgnoreCase))
                ?? assets.FirstOrDefault(static a => a.Name.EndsWith(".dmg", StringComparison.OrdinalIgnoreCase))
                ?? assets.FirstOrDefault();
        }
        return assets.FirstOrDefault();
    }

    public void ApplyUpdateAndRestart()
    {
        if (!UsesVelopack)
            return;

        var manager = GetVelopackUpdateManager();
        var target = manager?.UpdatePendingRestart;
        if (manager is null || target is null)
            return;

        AppInfo.SaveContext();
        _updateApplyRequested = true;
        try
        {
            manager.ApplyUpdatesAndRestart(target, [.. Environment.GetCommandLineArgs().Skip(1)]);
        }
        catch
        {
            _updateApplyRequested = false;
            throw;
        }
    }

    public void ApplyPendingUpdateOnExit()
    {
        if (!UsesVelopack || _updateApplyRequested)
            return;

        try
        {
            var manager = GetVelopackUpdateManager();
            if (manager?.UpdatePendingRestart is not { } target)
                return;

            AppInfo.SaveContext();
            _updateApplyRequested = true;
            manager.WaitExitThenApplyUpdates(target, silent: true, restart: false);
        }
        catch (Exception exception)
        {
            _updateApplyRequested = false;
            _logger?.LogError(nameof(ApplyPendingUpdateOnExit), exception);
        }
    }

    private async Task CheckVelopackForUpdateAsync()
    {
        _velopackUpdateInfo = null;
        _velopackUpdateReleaseModel = null;

        var manager = GetVelopackUpdateManager();
        if (manager is null)
        {
            AppReleaseModels = null;
            UpdateState = UpdateState.Unknown;
            return;
        }

        _velopackUpdateInfo = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
        if (_velopackUpdateInfo is not { TargetFullRelease: { } release })
        {
            _velopackUpdateReleaseModel = null;
            AppReleaseModels = [];
            UpdateState = UpdateState.UpToDate;
        }
        else
        {
            var versionStr = release.Version.Version.ToString();
            _velopackUpdateReleaseModel = new AppRelease(
                Version: versionStr,
                TagName: versionStr,
                Title: versionStr,
                ReleaseNotes: release.NotesMarkdown ?? string.Empty,
                PublishedAt: null,
                HtmlUrl: string.Empty,
                IsPrerelease: false,
                Assets: []);
            AppReleaseModels = [_velopackUpdateReleaseModel];
            UpdateState = UpdateEngine.CompareVersions(CurrentVersionShortText, versionStr);
        }

        TouchLastCheckedUpdate();
    }

    private void TouchLastCheckedUpdate()
    {
        if (_settings is null)
            return;

        _settings.ApplicationSettings.LastCheckedUpdate = DateTime.UtcNow;
    }

    public async Task<AppRelease?> GetCurrentAppReleaseModelAsync()
    {
        await _updateCheckLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (AppReleaseModels?.FirstOrDefault(t => t.ParsedVersion == CurrentVersion || t.Version == CurrentVersionShortText) is { } currentRelease)
                return currentRelease;

            if (UsesVelopack)
            {
                if (await GetVelopackReleaseModelsAsync().ConfigureAwait(false) is not { Count: > 0 } appReleaseModels)
                    return null;

                AppReleaseModels = appReleaseModels;
                return appReleaseModels.FirstOrDefault(t => t.ParsedVersion == CurrentVersion || t.Version == CurrentVersionShortText);
            }

            var releases = await UpdateEngine.GetReleasesAsync(includePrereleases: false).ConfigureAwait(false);
            if (releases.Count == 0)
                return null;

            AppReleaseModels = releases;
            return releases.FirstOrDefault(t => t.ParsedVersion == CurrentVersion || t.Version == CurrentVersionShortText);
        }
        catch
        {
            return null;
        }
        finally
        {
            _updateCheckLock.Release();
        }
    }

    private async Task<IReadOnlyList<AppRelease>?> GetVelopackReleaseModelsAsync()
    {
        var feed = await GetVelopackSource()
            .GetReleaseFeed(
                NullVelopackLogger.Instance,
                appId: null,
                channel: VelopackRuntimeInfo.SystemRid)
            .ConfigureAwait(false);

        var appReleaseModels = feed.Assets
            .Where(static asset => asset.Type is VelopackAssetType.Full)
            .GroupBy(static asset => asset.Version.Version)
            .Select(static assets =>
            {
                var release = assets.First();
                var versionStr = release.Version.Version.ToString();
                return new AppRelease(
                    Version: versionStr,
                    TagName: versionStr,
                    Title: versionStr,
                    ReleaseNotes: release.NotesMarkdown ?? string.Empty,
                    PublishedAt: null,
                    HtmlUrl: string.Empty,
                    IsPrerelease: false,
                    Assets: []);
            })
            .OrderByDescending(static release => release.ParsedVersion)
            .ToArray();

        return appReleaseModels.Length is 0 ? null : appReleaseModels;
    }

    private GithubSource GetVelopackSource() =>
        _velopackSource ??= new GithubSource(
            GitHubRepositoryUri,
            string.Empty,
            prerelease: false,
            downloader: new GitHubFileDownloader(() => UpdateEngine));

    private UpdateManager? GetVelopackUpdateManager()
    {
        if (!UsesVelopack)
            return null;

        return _velopackUpdateManager ??= new UpdateManager(
            GetVelopackSource(),
            new UpdateOptions { MaximumDeltasBeforeFallback = 10 });
    }
}
