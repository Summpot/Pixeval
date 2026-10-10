// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading.Tasks;
using Pixeval.Config;
using Pixeval.I18N;
using Pixeval.Services.Core;
using Pixeval.Utilities;
using Pixeval.Views.Settings;

namespace Pixeval.Services.Update;

public class AppUpdateNotificationCoordinator : IAppUpdateNotificationCoordinator
{
    private readonly AppSettings _appSettings;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private readonly FileLogger? _logger;
    private bool _automaticUpdateStarted;

    public AppUpdateNotificationCoordinator(
        AppSettings appSettings,
        IDialogService dialogs,
        INotificationService notifications,
        FileLogger? logger = null)
    {
        _appSettings = appSettings;
        _dialogs = dialogs;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task CheckAndNotifyUpdatesAsync()
    {
        try
        {
            if (AppInfo.AppVersion.UsesVelopack)
            {
                StartAutomaticUpdate();
                if (_appSettings.IsNewVersion
                    && await AppInfo.AppVersion.GetCurrentAppReleaseModelAsync() is { } currentRelease)
                {
                    await _dialogs.CreateAcknowledgementAsync(
                        SettingsPage.ReleaseTitle,
                        SettingsPage.CreateReleaseNotes(currentRelease));
                }

                return;
            }

            var checkTask = AppInfo.AppVersion.CheckForUpdateAsync();
            var completedTask = await Task.WhenAny(checkTask, Task.Delay(TimeSpan.FromSeconds(6)));
            if (completedTask != checkTask)
            {
                _logger?.LogWarning("CheckForUpdateAsync timed out after 6 seconds.", null);
                return;
            }
            await checkTask;

            if (_appSettings.IsNewVersion)
            {
                await _dialogs.CreateAcknowledgementAsync(
                    SettingsPage.ReleaseTitle,
                    SettingsPage.CreateReleaseNotes(AppInfo.AppVersion.CurrentAppReleaseModel));
            }

            if (AppInfo.AppVersion is { UpdateAvailable: true, NewestAppReleaseModel: { } release })
            {
                await _dialogs.CreateAcknowledgementAsync(
                    SettingsPage.GetReleaseTitle(release.Version),
                    SettingsPage.CreateReleaseNotes(release));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(nameof(CheckAndNotifyUpdatesAsync), ex);
        }
    }

    private void StartAutomaticUpdate()
    {
        if (_automaticUpdateStarted)
            return;

        _automaticUpdateStarted = true;
        _ = DownloadVelopackUpdateAutomaticallyAsync();
    }

    private async Task DownloadVelopackUpdateAutomaticallyAsync()
    {
        try
        {
            await AppInfo.AppVersion.CheckForUpdateAsync();
            if (AppInfo.AppVersion is not
                {
                    UpdateAvailable: true,
                    NewestVersion: { } version
                })
                return;

            if (!await AppInfo.AppVersion.DownloadUpdateAsync())
                return;

            var readyVersion = AppInfo.AppVersion.PendingUpdateVersion ?? version;
            _notifications.ShowSuccess(I18NManager.GetResource(
                SettingsMainViewResources.UpdateDownloadReadyFormatted,
                readyVersion));
        }
        catch (Exception exception)
        {
            _logger?.LogError(nameof(DownloadVelopackUpdateAutomaticallyAsync), exception);
        }
    }
}
