// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Pixeval.Localization;
using Pixeval.Services.Background;
using Pixeval.Utilities;

namespace Pixeval.Views.Settings;

public partial class ExtensionsPage : IconContentPage
{
    private ExtensionService ExtensionService => this.GetRequiredUiService<ExtensionService>();

    private bool _firstTime = true;

    public ObservableCollection<ExtensionsHostModel> Models => ExtensionService.HostModels;

    public ExtensionsPage() => InitializeComponent();

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_firstTime && ExtensionService.OutDateExtensionHostsCount is var count and not 0)
        {
            _firstTime = false;
            this.Notifications.ShowWarning(
                Strings.ExtensionsPageSomeExtensionsOutdatedFormatted(count: count));
        }
    }

    private void AddExtensionsOnClick(object? sender, RoutedEventArgs e) =>
        this.RunInteraction(() => AddExtensionsOnClickAsync(sender, e));

    private async Task AddExtensionsOnClickAsync(object? sender, RoutedEventArgs e)
    {
                if (ExtensionService.NativeLibraryExtension is not { } extension)
        {
            this.Notifications.ShowError(Strings.ExtensionsPagePlatformNotSupportExtensions());
            return;
        }

        var logger = this.GetRequiredUiService<FileLogger>();

        var files = await this.StoragePicker.OpenFilesAsync(new()
        {
            FileTypeFilter =
            [
                new("Zipped Extension") { Patterns = ["*.zip"] },
                new("Native Library Extension") { Patterns = ["*" + extension] }
            ],
            AllowMultiple = true
        });

        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file.Path.LocalPath);
            var fileName = fileInfo.Name;
            try
            {
                var installResult = await Task.Run(() =>
                    ExtensionService.PluginEngine.VerifyAndInstallPlugin(fileInfo.FullName, AppInfo.ExtensionsFolder));

                if (installResult.InstalledHostLibraries.Count is 0)
                {
                    this.Notifications.ShowWarning(
                        Strings.ExtensionsPageZipContainsNoExtension(), fileName);
                    continue;
                }

                foreach (var libraryPath in installResult.InstalledHostLibraries)
                {
                    LoadExtension(libraryPath, Path.GetFileName(libraryPath));
                }
            }
            catch (Exception ex)
            {
                this.Notifications.ShowError(Strings.ExtensionsPageExtensionLoadFailed(),
                    $"{fileName}: {ex.Message}");
            }
        }

        return;

        void LoadExtension(string libraryPath, string fileName)
        {
            try
            {
                var result =
                    ExtensionService.TryLoadHostWithResult(libraryPath, logger, out _, out var outdatedVersion);
                if (result is ExtensionHostLoadResult.Loaded)
                {
                    this.Notifications.ShowSuccess(
                        Strings.ExtensionsPageExtensionLoadedSuccessfully(), fileName);
                    return;
                }

                this.Notifications.ShowError(
                    outdatedVersion is null
                        ? Strings.ExtensionsPageExtensionLoadFailed()
                        : Strings.ExtensionsPageExtensionOutdatedFormatted(version: outdatedVersion),
                    fileName);
            }
            catch (Exception ex)
            {
                this.Notifications.ShowError(Strings.ExtensionsPageExtensionLoadFailed(),
                    $"{fileName}: {ex.Message}");
            }
        }
    }

    private void UninstallHostOnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: ExtensionsHostModel model })
            return;

        if (model.IsPendingUninstall)
        {
            if (ExtensionService.CancelHostUninstall(model))
                this.Notifications.ShowInformation(
                    Strings.ExtensionsPageExtensionPendingUninstallCancelled(),
                    model.Name);
            return;
        }

        if (ExtensionService.ScheduleHostUninstall(model))
            this.Notifications.ShowInformation(
                Strings.ExtensionsPageExtensionWillUninstallOnNextStartup(),
                model.Name);
    }

    private void UninstallAllExtensionsOnClick(object? sender, RoutedEventArgs e)
    {
        var count = ExtensionService.ScheduleAllHostUninstalls();
        this.Notifications.ShowInformation(count is 0
            ? Strings.ExtensionsPageNoExtensionsToUninstall()
            : Strings.ExtensionsPageMarkedExtensionsPendingUninstallFormatted(count: count));
    }

    private void UninstallOutdatedExtensionsOnClick(object? sender, RoutedEventArgs e)
    {
        var count = ExtensionService.ScheduleOutdatedHostUninstalls();
        this.Notifications.ShowInformation(count is 0
            ? Strings.ExtensionsPageNoOutdatedExtensionsToUninstall()
            : Strings.ExtensionsPageMarkedOutdatedExtensionsPendingUninstallFormatted(count: count));
    }

    private void OpenExtensionsOnClick(object? sender, RoutedEventArgs e)
    {
        _ = this.LauncherService.LaunchDirectoryInfoAsync(new DirectoryInfo(AppInfo.ExtensionsFolder));
    }

    private void ExtensionsHelpOnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: Uri uri })
            _ = this.LauncherService.LaunchUriAsync(uri);
    }

    private void ExtensionsPriorityUpOnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: ExtensionsHostModel model })
            return;
        var oldIndex = Models.IndexOf(model);
        if (oldIndex < 1)
            return;

        Models.Move(oldIndex, oldIndex - 1);
    }

    private void ExtensionsPriorityDownOnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: ExtensionsHostModel model })
            return;
        var oldIndex = Models.IndexOf(model);
        if (oldIndex < 0 || oldIndex >= Models.Count - 1)
            return;

        Models.Move(oldIndex, oldIndex + 1);
    }

    private void ExtensionsPriorityTopOnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: ExtensionsHostModel model })
            return;
        var oldIndex = Models.IndexOf(model);
        if (oldIndex < 1)
            return;

        Models.Move(oldIndex, 0);
    }

    private void ExtensionsPriorityBottomOnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: ExtensionsHostModel model })
            return;
        var oldIndex = Models.IndexOf(model);
        if (oldIndex < 0 || oldIndex >= Models.Count - 1)
            return;

        Models.Move(oldIndex, Models.Count - 1);
    }
}
