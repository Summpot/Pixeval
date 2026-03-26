using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
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

    private bool _isPanningHeroImage;

    private Point _lastHeroPanPoint;

    public WorkDetailsPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        DetachedFromVisualTree += (_, _) => _viewModel.ReleaseResources();
        HeroImageViewport.PropertyChanged += HeroImageViewport_OnPropertyChanged;
        HeroImageScrollViewer.AddHandler(
            InputElement.PointerWheelChangedEvent,
            HeroImageScrollViewer_OnPointerWheelChanged,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            handledEventsToo: true);
        HeroImageScrollViewer.AddHandler(
            InputElement.PointerPressedEvent,
            HeroImageScrollViewer_OnPointerPressed,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            handledEventsToo: true);
        HeroImageScrollViewer.AddHandler(
            InputElement.PointerMovedEvent,
            HeroImageScrollViewer_OnPointerMoved,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            handledEventsToo: true);
        HeroImageScrollViewer.AddHandler(
            InputElement.PointerReleasedEvent,
            HeroImageScrollViewer_OnPointerReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            handledEventsToo: true);
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
        SetHeroZoom(_heroZoom + HeroZoomStep, preserveCenter: true);
    }

    private void HeroZoomOutButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;
        SetHeroZoom(_heroZoom - HeroZoomStep, preserveCenter: true);
    }

    private void HeroZoomResetButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;
        ResetHeroZoom();
    }

    private void HeroImageScrollViewer_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        _ = sender;
        SetHeroZoom(_heroZoom + (e.Delta.Y > 0 ? HeroZoomStep : -HeroZoomStep), preserveCenter: true);
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

    private void SetHeroZoom(double zoom, bool preserveCenter = false)
    {
        var previousOffset = HeroImageScrollViewer.Offset;
        var previousViewport = HeroImageScrollViewer.Viewport;
        var previousExtent = HeroImageScrollViewer.Extent;

        _heroZoom = Math.Clamp(zoom, HeroMinZoom, HeroMaxZoom);
        UpdateHeroImageLayout(preserveCenter, previousOffset, previousViewport, previousExtent);
        UpdateHeroZoomUi();
    }

    private void UpdateHeroImageLayout(bool preserveCenter = false, Vector previousOffset = default, Size previousViewport = default, Size previousExtent = default)
    {
        if (_viewModel.MainImage is not { } bitmap)
            return;

        var viewportWidth = HeroImageViewport.Bounds.Width;
        var viewportHeight = HeroImageViewport.Bounds.Height;
        if (viewportWidth <= 1 || viewportHeight <= 1)
            return;

        var imageWidth = Math.Max(1, bitmap.PixelSize.Width);
        var imageHeight = Math.Max(1, bitmap.PixelSize.Height);
        var fitScale = Math.Min(viewportWidth / imageWidth, viewportHeight / imageHeight);

        var targetImageWidth = Math.Max(1, imageWidth * fitScale * _heroZoom);
        var targetImageHeight = Math.Max(1, imageHeight * fitScale * _heroZoom);
        var targetExtentWidth = Math.Max(targetImageWidth, viewportWidth);
        var targetExtentHeight = Math.Max(targetImageHeight, viewportHeight);

        HeroImage.Width = targetImageWidth;
        HeroImage.Height = targetImageHeight;
        HeroImageContentRoot.Width = targetExtentWidth;
        HeroImageContentRoot.Height = targetExtentHeight;

        Dispatcher.UIThread.Post(() =>
        {
            if (preserveCenter && previousExtent.Width > 0 && previousExtent.Height > 0)
            {
                var centerRatioX = (previousOffset.X + previousViewport.Width / 2) / previousExtent.Width;
                var centerRatioY = (previousOffset.Y + previousViewport.Height / 2) / previousExtent.Height;

                ApplyHeroImageOffset(new Vector(
                    targetExtentWidth * centerRatioX - HeroImageScrollViewer.Viewport.Width / 2,
                    targetExtentHeight * centerRatioY - HeroImageScrollViewer.Viewport.Height / 2));
            }
            else
            {
                ApplyHeroImageOffset(new Vector(
                    Math.Max(0, (targetExtentWidth - HeroImageScrollViewer.Viewport.Width) / 2),
                    Math.Max(0, (targetExtentHeight - HeroImageScrollViewer.Viewport.Height) / 2)));
            }
        }, DispatcherPriority.Background);
    }

    private void ApplyHeroImageOffset(Vector requestedOffset)
    {
        var maxOffsetX = Math.Max(0, HeroImageScrollViewer.Extent.Width - HeroImageScrollViewer.Viewport.Width);
        var maxOffsetY = Math.Max(0, HeroImageScrollViewer.Extent.Height - HeroImageScrollViewer.Viewport.Height);

        HeroImageScrollViewer.Offset = new Vector(
            Math.Clamp(requestedOffset.X, 0, maxOffsetX),
            Math.Clamp(requestedOffset.Y, 0, maxOffsetY));
    }

    private void HeroImageScrollViewer_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_heroZoom <= HeroMinZoom + 0.001 || !e.GetCurrentPoint(HeroImageScrollViewer).Properties.IsLeftButtonPressed)
            return;

        _ = sender;
        _isPanningHeroImage = true;
        _lastHeroPanPoint = e.GetPosition(HeroImageScrollViewer);
        e.Pointer.Capture(HeroImageScrollViewer);
        e.Handled = true;
    }

    private void HeroImageScrollViewer_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanningHeroImage)
            return;

        _ = sender;
        var currentPoint = e.GetPosition(HeroImageScrollViewer);
        var delta = currentPoint - _lastHeroPanPoint;
        _lastHeroPanPoint = currentPoint;

        ApplyHeroImageOffset(new Vector(
            HeroImageScrollViewer.Offset.X - delta.X,
            HeroImageScrollViewer.Offset.Y - delta.Y));
        e.Handled = true;
    }

    private void HeroImageScrollViewer_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _ = sender;
        if (!_isPanningHeroImage)
            return;

        _isPanningHeroImage = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void HeroImageViewport_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        _ = sender;
        if (e.Property == BoundsProperty)
            UpdateHeroImageLayout();
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
