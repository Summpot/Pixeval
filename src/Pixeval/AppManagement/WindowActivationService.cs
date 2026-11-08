using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Utilities;
using Pixeval.Views.Settings;
using Pixeval.Views.ViewContainers;

namespace Pixeval.AppManagement;

/// <summary>External update checks and release announcements for a newly opened window.</summary>
public interface IWindowActivationService
{
    Task ActivateAsync(ViewContainerBase container);
}

internal sealed class WindowActivationService : IWindowActivationService
{
    private bool _automaticUpdateStarted;

    public async Task ActivateAsync(ViewContainerBase container)
    {
        if (AppInfo.AppVersion.UsesVelopack)
        {
            StartAutomaticUpdate(container);
            if (App.AppViewModel.AppSettings.IsNewVersion
                && await AppInfo.AppVersion.GetCurrentAppReleaseModelAsync() is { } currentRelease)
            {
                await container.CreateAcknowledgementAsync(
                    SettingsPage.ReleaseTitle,
                    SettingsPage.CreateReleaseNotes(currentRelease));
            }

            return;
        }

        await AppInfo.AppVersion.CheckForUpdateAsync();
        var dialogTasks = new List<Task<ContentDialogResult>>();
        if (App.AppViewModel.AppSettings.IsNewVersion)
            dialogTasks.Add(container.CreateAcknowledgementAsync(
                SettingsPage.ReleaseTitle,
                SettingsPage.CreateReleaseNotes(AppInfo.AppVersion.CurrentAppReleaseModel)));
        if (AppInfo.AppVersion is { UpdateAvailable: true, NewestAppReleaseModel: { } release })
            dialogTasks.Add(container.CreateAcknowledgementAsync(
                SettingsPage.GetReleaseTitle(release.Version),
                SettingsPage.CreateReleaseNotes(release)));

        await Task.WhenAll(dialogTasks);
    }

    private void StartAutomaticUpdate(ViewContainerBase container)
    {
        if (_automaticUpdateStarted)
            return;

        _automaticUpdateStarted = true;
        _ = DownloadVelopackUpdateAutomaticallyAsync(container);
    }

    private async Task DownloadVelopackUpdateAutomaticallyAsync(ViewContainerBase container)
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
            container.ShowSuccess(I18NManager.GetResource(
                SettingsMainViewResources.UpdateDownloadReadyFormatted,
                readyVersion));
        }
        catch (Exception exception)
        {
            App.AppViewModel.AppServiceProvider.GetService<FileLogger>()?.LogError(
                nameof(DownloadVelopackUpdateAutomaticallyAsync),
                exception);
        }
    }

}
