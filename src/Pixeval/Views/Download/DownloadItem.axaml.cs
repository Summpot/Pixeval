// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using FluentIcons.Common;
using Pixeval.I18N;
using Pixeval.Native.Download;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Download;

public partial class DownloadItem : UserControl
{
    public event Action<DownloadItem, DownloadItemSnapshot>? OpenIllustrationRequested;

    public DownloadItem() => InitializeComponent();

    private DownloadItemSnapshot? Item => DataContext as DownloadItemSnapshot;

    private DownloadPageViewModel? Page =>
        this.FindAncestorOfType<DownloadItemView>()?.DataContext is DownloadItemPageViewModel itemPage
            ? itemPage.PageViewModel
            : null;

    private async void ActionButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (Item is not { } item || Page is not { } page)
            return;

        if (item.ActionButtonSymbol is Symbol.Open)
            await OpenPathAsync(item.OpenDestination);
        else
            ExecutePrimaryAction(page, item);
    }

    private void ResetItem_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (Item is { } item && Page is { } page)
            page.Reset(item.Key);
    }

    private void CancelDownloadItem_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (Item is { } item && Page is { } page)
            page.Cancel(item.Key);
    }

    private async void OpenDownloadLocationItem_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (Item is not { } item)
            return;

        var path = Path.GetDirectoryName(item.OpenDestination);
        if (!string.IsNullOrWhiteSpace(path))
            await OpenPathAsync(path);
    }

    private void GoToPageItem_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (Item is { } item)
            OpenIllustrationRequested?.Invoke(this, item);
    }

    private async void CheckErrorMessageInDetail_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (Item is not { } item)
            return;

        if (TopLevel.GetTopLevel(this)?.ViewContainer is { } viewContainer)
            _ = await viewContainer.CreateAcknowledgementAsync(
                I18NManager.GetResource(DownloadItemResources.ErrorMessageDialogTitle),
                item.ErrorMessage);
    }

    private static void ExecutePrimaryAction(DownloadPageViewModel page, DownloadItemSnapshot item)
    {
        switch (item.ActionButtonSymbol)
        {
            case Symbol.Dismiss:
                page.Cancel(item.Key);
                break;
            case Symbol.Pause:
                page.Pause(item.Key);
                break;
            case Symbol.ArrowRepeatAll:
                page.Reset(item.Key);
                break;
            case Symbol.Play:
                page.Resume(item.Key);
                break;
        }
    }

    private async Task OpenPathAsync(string path)
    {
        if (TopLevel.GetTopLevel(this) is not { Launcher: { } launcher })
            return;

        try
        {
            var info = new DirectoryInfo(path);
            if (info.Exists)
            {
                _ = await launcher.LaunchDirectoryInfoAsync(info);
                return;
            }

            var file = new FileInfo(path);
            if (file.Exists)
            {
                _ = await launcher.LaunchFileInfoAsync(file);
                return;
            }

            TopLevel.GetTopLevel(this)?.ViewContainer?.ShowError(
                I18NManager.GetResource(DownloadItemResources.ActionButtonContentOpen),
                path);
        }
        catch
        {
            TopLevel.GetTopLevel(this)?.ViewContainer?.ShowError(
                I18NManager.GetResource(DownloadItemResources.ActionButtonContentOpen),
                path);
        }
    }
}
