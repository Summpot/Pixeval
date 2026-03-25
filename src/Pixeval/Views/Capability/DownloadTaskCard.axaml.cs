using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pixeval.I18N;
using Pixeval.Utilities;
using Pixeval.ViewModels.Download;
using Pixeval.ViewModels.WorkDetails;

namespace Pixeval.Views.Capability;

public partial class DownloadTaskCard : UserControl
{
    public DownloadTaskCard()
    {
        InitializeComponent();
        AttachedToVisualTree += async (_, _) =>
        {
            if (ViewModel is { } viewModel)
                _ = await viewModel.TryLoadThumbnailAsync(this);
        };
        DetachedFromVisualTree += (_, _) => ViewModel?.UnloadThumbnail(this);
    }

    private DownloadItemViewModel? ViewModel => DataContext as DownloadItemViewModel;

    private void PrimaryActionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;

        if (ViewModel is not { } viewModel)
            return;

        switch (viewModel.PrimaryActionSymbol)
        {
            case FluentIcons.Common.Symbol.Dismiss:
                viewModel.DownloadTask.Cancel();
                break;
            case FluentIcons.Common.Symbol.Pause:
                viewModel.DownloadTask.Pause();
                break;
            case FluentIcons.Common.Symbol.ArrowRepeatAll:
                viewModel.DownloadTask.TryReset();
                break;
            case FluentIcons.Common.Symbol.Play:
                viewModel.DownloadTask.TryResume();
                break;
            case FluentIcons.Common.Symbol.Open:
                OpenPath(viewModel.DestinationPath);
                break;
        }
    }

    private void RetryButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;
        ViewModel?.DownloadTask.TryReset();
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;
        ViewModel?.DownloadTask.Cancel();
    }

    private void OpenDownloadedFileMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;
        if (ViewModel is { } viewModel)
            OpenPath(viewModel.DestinationPath);
    }

    private void OpenDownloadFolderMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;
        if (ViewModel is { } viewModel)
            OpenPath(viewModel.FolderPath);
    }

    private void OpenWorkDetailsMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;

        if (ViewModel is not { CanNavigateToWorkDetails: true } viewModel
            || TopLevel.GetTopLevel(this)?.ViewContainer is not { } viewContainer)
            return;

        var symbol = viewModel.WorkKind switch
        {
            WorkDetailsKind.Novel => FluentIcons.Common.Symbol.BookNumber,
            WorkDetailsKind.Manga => FluentIcons.Common.Symbol.ImageMultiple,
            _ => FluentIcons.Common.Symbol.Image
        };

        viewContainer.NavigateTo(
            typeof(WorkDetailsPage),
            new FluentIcons.Avalonia.SymbolIcon
            {
                Symbol = symbol,
                FontSize = 16
            },
            string.IsNullOrWhiteSpace(viewModel.Title) ? $"作品 {viewModel.WorkId}" : viewModel.Title,
            viewModel.NavigationParameter);
    }

    private void ShowErrorMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;

        if (ViewModel is not { HasError: true } viewModel)
            return;

        TopLevel.GetTopLevel(this)?.ViewContainer?.ShowError(
            I18NManager.GetResource(DownloadItemResources.ErrorMessageDialogTitle),
            string.IsNullOrWhiteSpace(viewModel.ErrorMessage)
                ? I18NManager.GetResource(MiscResources.DownloadItemMaybeDeleted)
                : viewModel.ErrorMessage);
    }

    private void OpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) && !Directory.Exists(path))
        {
            TopLevel.GetTopLevel(this)?.ViewContainer?.ShowError(
                I18NManager.GetResource(MiscResources.DownloadItemOpenFailed),
                I18NManager.GetResource(MiscResources.DownloadItemMaybeDeleted));
            return;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });

            if (process is null)
            {
                TopLevel.GetTopLevel(this)?.ViewContainer?.ShowError(
                    I18NManager.GetResource(MiscResources.DownloadItemOpenFailed),
                    I18NManager.GetResource(MiscResources.DownloadItemMaybeDeleted));
            }
        }
        catch (Exception ex)
        {
            TopLevel.GetTopLevel(this)?.ViewContainer?.ShowError(
                I18NManager.GetResource(MiscResources.DownloadItemOpenFailed),
                ex.Message);
        }
    }
}
