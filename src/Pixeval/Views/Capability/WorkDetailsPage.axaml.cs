using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using Pixeval.I18N;
using Pixeval.Utilities;
using Pixeval.ViewModels.WorkDetails;

namespace Pixeval.Views.Capability;

public partial class WorkDetailsPage : UserControl
{
    private const double HeroZoomStep = 0.2;

    private const double HeroMinZoom = 1.0;

    private const double HeroMaxZoom = 3.0;

    private readonly WorkDetailsViewModel _viewModel = new();

    private double _heroZoom = HeroMinZoom;

    public WorkDetailsPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        DetachedFromVisualTree += (_, _) => _viewModel.ReleaseResources();
        UpdateHeroZoomUi();

        AddHandler(Frame.NavigatedToEvent, (sender, e) =>
        {
            if (e.Parameter is not WorkDetailsNavigationParameter parameter)
                return;

            _ = LoadAsync(parameter);
        });
    }

    private async Task LoadAsync(WorkDetailsNavigationParameter parameter)
    {
        await _viewModel.LoadAsync(parameter).ConfigureAwait(false);
        Dispatcher.UIThread.Post(ResetHeroZoom);
    }

    private async void DownloadButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;

        if (TopLevel.GetTopLevel(this)?.ViewContainer is not { } viewContainer)
            return;

        if (await _viewModel.QueueDownloadAsync(App.AppViewModel.AppSettings.DownloadPathMacro))
            viewContainer.ShowSuccess(I18NManager.GetResource(EntryItemResources.DownloadTaskCreated));
    }

    private async void DownloadAsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;

        if (TopLevel.GetTopLevel(this) is not { StorageProvider: { } storageProvider, ViewContainer: { } viewContainer })
            return;

        var folder = await storageProvider.OpenFolderPickerAsync(new() { AllowMultiple = false });
        if (folder is not [{ } single])
        {
            viewContainer.ShowInformation(EntryItemResources.SaveAsCancelled);
            return;
        }

        var name = Path.GetFileName(App.AppViewModel.AppSettings.DownloadPathMacro);
        var path = Path.Combine(single.Path.LocalPath, name);
        if (await _viewModel.QueueDownloadAsync(path))
            viewContainer.ShowSuccess(I18NManager.GetResource(EntryItemResources.DownloadTaskCreated));
    }

    private async void OpenInBrowserButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.WebsiteUrl)
            || !Uri.TryCreate(_viewModel.WebsiteUrl, UriKind.Absolute, out var uri)
            || TopLevel.GetTopLevel(this) is not { Launcher: { } launcher })
            return;

        _ = await launcher.LaunchUriAsync(uri);
    }

    private async void CopyAppLinkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not
            {
                Clipboard: { } clipboard,
                ViewContainer: { } viewContainer
            })
            return;

        await clipboard.SetTextAsync(_viewModel.AppUrl);

        Dispatcher.UIThread.Post(() =>
            viewContainer.ShowSuccess(I18NManager.GetResource(EntryItemResources.LinkCopiedToClipboard)));
    }

    private void RelatedWorkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: RelatedWorkCardViewModel relatedWork }
            || TopLevel.GetTopLevel(this)?.ViewContainer is not { } viewContainer)
            return;

        var symbol = relatedWork.Kind switch
        {
            WorkDetailsKind.Novel => Symbol.BookNumber,
            WorkDetailsKind.Manga => Symbol.ImageMultiple,
            _ => Symbol.Image
        };

        viewContainer.NavigateTo(
            typeof(WorkDetailsPage),
            new SymbolIcon
            {
                Symbol = symbol,
                FontSize = 16,
                IconVariant = IconVariant.Color
            },
            string.IsNullOrWhiteSpace(relatedWork.Title) ? $"作品 {relatedWork.Id}" : relatedWork.Title,
            relatedWork.NavigationParameter);
    }

    private void HeroZoomInButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;
        SetHeroZoom(_heroZoom + HeroZoomStep);
    }

    private void HeroZoomOutButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;
        SetHeroZoom(_heroZoom - HeroZoomStep);
    }

    private void HeroZoomResetButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;
        ResetHeroZoom();
    }

    private void HeroImageViewport_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        _ = sender;
        SetHeroZoom(_heroZoom + (e.Delta.Y > 0 ? HeroZoomStep : -HeroZoomStep));
        e.Handled = true;
    }

    private void HeroImageViewport_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        _ = sender;
        e.Handled = true;
        ResetHeroZoom();
    }

    private void ResetHeroZoom()
    {
        SetHeroZoom(HeroMinZoom);
    }

    private void SetHeroZoom(double zoom)
    {
        _heroZoom = Math.Clamp(zoom, HeroMinZoom, HeroMaxZoom);

        if (HeroImage.RenderTransform is not ScaleTransform transform)
        {
            transform = new ScaleTransform(1, 1);
            HeroImage.RenderTransform = transform;
        }

        transform.ScaleX = _heroZoom;
        transform.ScaleY = _heroZoom;
        UpdateHeroZoomUi();
    }

    private void UpdateHeroZoomUi()
    {
        if (HeroZoomText is null)
            return;

        HeroZoomText.Text = $"{_heroZoom:P0}";
        HeroZoomOutButton.IsEnabled = _heroZoom > HeroMinZoom + 0.001;
        HeroZoomInButton.IsEnabled = _heroZoom < HeroMaxZoom - 0.001;
        HeroZoomResetButton.IsEnabled = Math.Abs(_heroZoom - HeroMinZoom) > 0.001;
    }
}
