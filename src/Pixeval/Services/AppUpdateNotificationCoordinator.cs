// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pixeval.AppManagement;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Utilities;
using Pixeval.Views.Settings;
using Pixeval.Views.ViewContainers;

namespace Pixeval.Services;

public class AppUpdateNotificationCoordinator : IAppUpdateNotificationCoordinator
{
    private readonly FileLogger? _logger;
    private bool _automaticUpdateStarted;

    public AppUpdateNotificationCoordinator(FileLogger? logger = null)
    {
        _logger = logger;
    }

    public async Task CheckAndNotifyUpdatesAsync(ViewContainerBase viewContainer)
    {
        try
        {
            if (AppInfo.AppVersion.UsesVelopack)
            {
                StartAutomaticUpdate(viewContainer);
                if (App.AppViewModel.AppSettings.IsNewVersion
                    && await AppInfo.AppVersion.GetCurrentAppReleaseModelAsync() is { } currentRelease)
                {
                    await viewContainer.CreateAcknowledgementAsync(
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

            if (App.AppViewModel.AppSettings.IsNewVersion)
            {
                await viewContainer.CreateAcknowledgementAsync(
                    SettingsPage.ReleaseTitle,
                    SettingsPage.CreateReleaseNotes(AppInfo.AppVersion.CurrentAppReleaseModel));
            }

            if (AppInfo.AppVersion is { UpdateAvailable: true, NewestAppReleaseModel: { } release })
            {
                await viewContainer.CreateAcknowledgementAsync(
                    SettingsPage.GetReleaseTitle(release.Version),
                    SettingsPage.CreateReleaseNotes(release));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(nameof(CheckAndNotifyUpdatesAsync), ex);
        }
    }

    private void StartAutomaticUpdate(ViewContainerBase viewContainer)
    {
        if (_automaticUpdateStarted)
            return;

        _automaticUpdateStarted = true;
        _ = DownloadVelopackUpdateAutomaticallyAsync(viewContainer);
    }

    private async Task DownloadVelopackUpdateAutomaticallyAsync(ViewContainerBase viewContainer)
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
            viewContainer.ShowSuccess(I18NManager.GetResource(
                SettingsMainViewResources.UpdateDownloadReadyFormatted,
                readyVersion));
        }
        catch (Exception exception)
        {
            _logger?.LogError(nameof(DownloadVelopackUpdateAutomaticallyAsync), exception);
        }
    }
}
