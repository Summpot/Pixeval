// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnimatedControls.Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using Pixeval.Extensions.Common;
using Pixeval.Extensions.Common.Commands.Transformers;
using Pixeval.Localization;
using Pixeval.Services.Background;
using Pixeval.Booru;
using Pixeval.Cache;
using Pixeval.Mako;
using Pixeval.Utilities;
using Pixeval.Common.IO;

namespace Pixeval.ViewModels.Viewers;

public sealed partial class SingleViewerViewModel : ViewModelBase, IDisposable
{
    public bool HasTransformerExtensions => TransformerExtensionItems.Count is not 0;

    private readonly List<ExtensionsHostModel> _observedExtensionHosts = [];
    private bool _observingExtensionHosts;
    private IImageTransformerCommandExtension? _activeTransformer;
    private CancellationTokenSource? _transformCancellation;

    // Multiple viewer interactions can request the same page concurrently; share one load task to keep progress stable.
    private readonly Lock _loadOriginalImageTaskGate = new();
    private readonly CancellationTokenSource _lifetimeCancellationTokenSource = new();
    private Task? _loadOriginalImageTask;
    private int _previewConsumers;
    private bool _retryImageLoad;
    private bool _retryOriginalImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadingIndeterminate))]
    public partial double? LoadingProgress { get; private set; }

    public bool IsLoadingIndeterminate => LoadingProgress is null;

    public bool IsLoading => IsLoadingImage || IsProcessingImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    [NotifyCanExecuteChangedFor(nameof(RetryImageCommand))]
    public partial bool IsLoadingImage { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RetryImageCommand))]
    public partial string? LoadErrorMessage { get; private set; }

    private bool CanRetryImage => !_disposed && !IsLoadingImage && LoadErrorMessage is not null;

    [RelayCommand(CanExecute = nameof(CanRetryImage))]
    private Task RetryImageAsync()
    {
        LoadErrorMessage = null;
        return LoadOriginalImageAsync();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    [NotifyCanExecuteChangedFor(nameof(TransformExtensionCommand))]
    [NotifyCanExecuteChangedFor(nameof(ViewOriginalCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAsCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    public partial bool LoadSuccessfully { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    [NotifyCanExecuteChangedFor(nameof(TransformExtensionCommand))]
    [NotifyCanExecuteChangedFor(nameof(ViewOriginalCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAsCommand))]
    public partial bool IsProcessingImage { get; private set; }

    /// <summary>
    /// 显示用图源
    /// </summary>
    // Completed sources remain owned by this model so image transformers can reuse the original.
    public UpdatableAnimatedBitmap DisplaySource { get; } = new(disposeSources: false);

    public bool HasDisplayImage => DisplaySource.Size is { Width: > 0, Height: > 0 };

    private void NotifyDisplayImageChanged()
    {
        OnPropertyChanged(nameof(HasDisplayImage));
        MirrorCommand.NotifyCanExecuteChanged();
        RotateClockwiseCommand.NotifyCanExecuteChanged();
        RotateCounterclockwiseCommand.NotifyCanExecuteChanged();
        ZoomInCommand.NotifyCanExecuteChanged();
        ZoomOutCommand.NotifyCanExecuteChanged();
        ZoomToOriginalCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
    }

    partial void OnCachedPreviewSourceChanged(IAnimatedBitmap? value) => DisplaySource.SetFallback(value);

    private void DisplaySourceOnChanged(object? sender, EventArgs e)
    {
        if (_disposed)
            return;
        if (Dispatcher.UIThread.CheckAccess())
            NotifyDisplayImageChanged();
        else
            Dispatcher.UIThread.Post(() =>
            {
                if (!_disposed)
                    NotifyDisplayImageChanged();
            });
    }

    [ObservableProperty]
    public partial IAnimatedBitmap? TransformedSource { get; private set; }

    /// <summary>
    /// 原图源（处理前的图片）
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TransformExtensionCommand))]
    public partial IAnimatedBitmap? OriginalSource { get; private set; }

    [ObservableProperty]
    public partial Bitmap? ThumbnailSource { get; private set; }

    [ObservableProperty]
    public partial IAnimatedBitmap? CachedPreviewSource { get; private set; }

    private bool _cachedPreviewLoading;

    internal void AttachPreview()
    {
        if (Interlocked.Increment(ref _previewConsumers) is not 1)
            return;
        if (_disposed)
            return;
        StartObservingExtensionHosts();
        if (!LoadSuccessfully)
            _ = LoadCachedPreviewAsync();
    }

    private async Task LoadCachedPreviewAsync()
    {
        if (_cachedPreviewLoading || _disposed)
            return;
        _cachedPreviewLoading = true;
        try
        {
            var bitmap = await LoadThumbnailImageOverrideAsync(_lifetimeCancellationTokenSource.Token,
                ProgressiveImageDecoder.PreviewDimension);
            if (_disposed || LoadSuccessfully || _previewConsumers is 0)
                bitmap?.Dispose();
            else
                CachedPreviewSource = CreatePreviewSource(bitmap);
        }
        catch (OperationCanceledException) when (_lifetimeCancellationTokenSource.IsCancellationRequested)
        {
        }
        finally
        {
            _cachedPreviewLoading = false;
        }
    }

    internal void DetachPreview()
    {
        if (Interlocked.Decrement(ref _previewConsumers) is not 0)
            return;
        if (_activeTransformer is null)
            StopObservingExtensionHosts();
        ReleaseLoadingPreview();
    }

    public bool IsPicGif => _entry is Illustration { IsPicGif: true };

    private bool IsGifLoadSuccessfully => !_disposed && LoadSuccessfully && IsPicGif;

    public bool CanViewOriginal => !_disposed && !IsProcessingImage && LoadSuccessfully;

    public bool CanTransformExtension => !IsPicGif && CanViewOriginal && OriginalSource is not null;

    public bool CanSaveDisplayedImage => !_disposed && CanViewOriginal && DisplaySource.Frames is [_];

    private bool CanCopyDisplayedImage => !_disposed && LoadSuccessfully && DisplaySource.Frames is [_];

    private bool CanSaveImage => !_disposed;

    public IReadOnlyList<ImageTransformerExtensionCommandItem> TransformerExtensionItems { get; private set; } = [];

    public ImageTransformerExtensionCommandItem? PrimaryTransformerExtensionItem =>
        TransformerExtensionItems is [{ } first, ..] ? first : null;

    private bool _disposed;

    private bool _thumbnailLoaded;
    private readonly string _platform;
    private readonly object _entry;
    private readonly object _saveTarget;
    private readonly IImageProviderService _images;
    private readonly IArtworkActionService _artworkActions;
    private readonly INotificationService _notifications;
    private readonly MakoClient _makoClient;
    private readonly ExtensionService _extensions;

    public int Index { get; }

    [ObservableProperty]
    public partial double ZoomFactor { get; set; } = 1;

    [ObservableProperty]
    public partial bool IsPlaying { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MirrorScaleX))]
    public partial bool IsMirrored { get; set; }

    [ObservableProperty]
    public partial int RotationDegree { get; set; }

    /// <summary>
    /// 镜像时为-1，否则为1
    /// </summary>
    public double MirrorScaleX => IsMirrored ? -1 : 1;

    /// <inheritdoc/>
    public SingleViewerViewModel(
        string platform,
        object entry,
        int index,
        IImageProviderService images,
        IArtworkActionService artworkActions,
        INotificationService notifications,
        MakoClient makoClient,
        ExtensionService extensions,
        object? saveTarget = null)
    {
        _platform = platform;
        _entry = entry is WorkEntry we ? we.AsWorkEntry : entry;
        _saveTarget = saveTarget ?? _entry;
        _images = images;
        _artworkActions = artworkActions;
        _notifications = notifications;
        _makoClient = makoClient;
        Index = index;
        _extensions = extensions;
        DisplaySource.Changed += DisplaySourceOnChanged;
        RefreshTransformerExtensions();
        _ = LoadThumbnailImageAsync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        RetryImageCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        SaveImageCommand.NotifyCanExecuteChanged();
        PlayPauseCommand.NotifyCanExecuteChanged();
        StopObservingExtensionHosts();
        TransformExtensionCommand.NotifyCanExecuteChanged();
        ViewOriginalCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
        foreach (var item in TransformerExtensionItems)
            item.Dispose();
        _lifetimeCancellationTokenSource.Cancel();
        _lifetimeCancellationTokenSource.Dispose();
        DisplaySource.Changed -= DisplaySourceOnChanged;
        DisplaySource.Dispose();
        TransformedSource = null;
        OriginalSource?.Dispose();
        OriginalSource = null;
        ThumbnailSource = null;
        ReleaseLoadingPreview();
    }

    public async Task LoadThumbnailImageAsync()
    {
        if (_thumbnailLoaded || _disposed)
            return;

        _thumbnailLoaded = true;

        try
        {
            var source = await LoadThumbnailImageOverrideAsync(_lifetimeCancellationTokenSource.Token);
            if (_disposed)
            {
                source?.Dispose();
                return;
            }

            ThumbnailSource = source;
        }
        catch (OperationCanceledException) when (_lifetimeCancellationTokenSource.IsCancellationRequested)
        {
        }
    }

    public Task LoadOriginalImageAsync()
    {
        if (LoadSuccessfully || _disposed || LoadErrorMessage is not null)
            return Task.CompletedTask;

        lock (_loadOriginalImageTaskGate)
        {
            if (_loadOriginalImageTask is { IsCompleted: false } loadingTask)
                return loadingTask;

            var newLoadingTask = LoadOriginalImageCoreAsync();
            _loadOriginalImageTask = newLoadingTask;
            _ = ResetLoadOriginalImageTaskAsync(newLoadingTask);
            return newLoadingTask;
        }
    }

    private async Task LoadOriginalImageCoreAsync()
    {
        if (LoadSuccessfully || _disposed)
            return;

        var token = _lifetimeCancellationTokenSource.Token;
        IsLoadingImage = true;
        LoadErrorMessage = null;
        LoadingProgress = null;
        try
        {
            var source = await DisplaySource.UpdateAsync(
                updateToken => LoadWithPreviewAsync(false, updateToken), token);
            if (_disposed)
            {
                source?.Dispose();
                return;
            }
            if (source is null)
                throw new InvalidDataException("The image could not be loaded or decoded.");
            OriginalSource?.Dispose();
            OriginalSource = source;
            ReleaseLoadingPreview();
            LoadSuccessfully = source.IsInitialized;
            _retryImageLoad = false;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch
        {
            if (!token.IsCancellationRequested)
            {
                _retryImageLoad = true;
                LoadErrorMessage = Strings.EntryViewerPageLoadFailed();
            }
        }
        finally
        {
            if (!_disposed)
                IsLoadingImage = false;
        }
    }

    // The single-frame source owns its bitmap; dispose the source rather than the frame separately.
    private static IAnimatedBitmap? CreatePreviewSource(Bitmap? bitmap) =>
        bitmap is null ? null : IAnimatedBitmap.Load([bitmap], [0]);

    internal void ReleaseLoadingPreview()
    {
        var cached = CachedPreviewSource;
        CachedPreviewSource = null;
        DisplaySource.UpdatePreview(null);
        cached?.Dispose();
    }

    private async Task<IAnimatedBitmap?> LoadWithPreviewAsync(bool original, CancellationToken token)
    {
        Action<DecodedPreviewFrame>? onPreview = Volatile.Read(ref _previewConsumers) > 0
            ? frame => PublishLoadingPreview(frame, token)
            : null;
        var source = await LoadImageAsync(original, onPreview, token);
        return ReferenceEquals(source, _images.AnimatedImageNotAvailable) ? null : source;
    }

    private void PublishLoadingPreview(DecodedPreviewFrame frame, CancellationToken token)
    {
        if (_disposed || Volatile.Read(ref _previewConsumers) is 0)
            return;

        var bitmap = frame.ToWriteableBitmap();
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_disposed || Volatile.Read(ref _previewConsumers) is 0)
            {
                bitmap.Dispose();
                return;
            }

            DisplaySource.UpdatePreview(bitmap, token);
        });
    }

    private async Task ResetLoadOriginalImageTaskAsync(Task loadingTask)
    {
        try
        {
            await loadingTask;
        }
        catch
        {
            // The initiating caller observes the failure; this continuation only resets shared state.
        }
        finally
        {
            lock (_loadOriginalImageTaskGate)
            {
                if (ReferenceEquals(_loadOriginalImageTask, loadingTask))
                    _loadOriginalImageTask = null;
            }
        }
    }

    private void UpdateLoadingProgress(double progress)
    {
        if (_disposed)
            return;

        LoadingProgress = progress;
    }

    private IEnumerable<(string Url, int Width, int Height)> GetThumbnailCandidates()
    {
        switch (_entry)
        {
            case Illustration ill:
                if (!string.IsNullOrEmpty(ill.OriginalUrl)) yield return (ill.OriginalUrl, (int) ill.Width, (int) ill.Height);
                if (!string.IsNullOrEmpty(ill.ImageUrls.Large)) yield return (ill.ImageUrls.Large, (int) ill.Width, (int) ill.Height);
                if (!string.IsNullOrEmpty(ill.ImageUrls.Medium)) yield return (ill.ImageUrls.Medium, (int) ill.Width, (int) ill.Height);
                if (!string.IsNullOrEmpty(ill.ImageUrls.SquareMedium)) yield return (ill.ImageUrls.SquareMedium, (int) ill.Width, (int) ill.Height);
                break;
            case BooruPost bp:
                if (!string.IsNullOrEmpty(bp.OriginalUrl)) yield return (bp.OriginalUrl, (int) bp.Width, (int) bp.Height);
                if (!string.IsNullOrEmpty(bp.LargeFileUrl)) yield return (bp.LargeFileUrl, (int) bp.Width, (int) bp.Height);
                if (!string.IsNullOrEmpty(bp.PreviewFileUrl)) yield return (bp.PreviewFileUrl, (int) bp.Width, (int) bp.Height);
                break;
        }
    }

    private async Task<Bitmap?> LoadThumbnailImageOverrideAsync(CancellationToken token, int maximumDimension = 100)
    {
        foreach (var (url, w, h) in GetThumbnailCandidates())
        {
            token.ThrowIfCancellationRequested();
            await using var stream = _images.TryGetStream(url);
            if (stream is null)
                continue;

            // Reuse the largest already-downloaded thumbnail; never start a placeholder download.
            var width = w > 0 && h > 0
                ? Math.Max(1, (int) (w * Math.Min(1d, maximumDimension / (double) Math.Max(w, h))))
                : maximumDimension;
            try
            {
                return await stream.DecodeBitmapImageAsync(false, width);
            }
            catch (Exception) when (!token.IsCancellationRequested)
            {
                // A damaged cached variant should not prevent trying a smaller cached thumbnail.
            }
        }
        return null;
    }

    private async Task<IAnimatedBitmap?> LoadImageAsync(bool isOriginal, Action<DecodedPreviewFrame>? onPreview = null, CancellationToken token = default)
    {
        switch (_entry)
        {
            case Illustration { IsPicGif: true } ill:
            {
                var metadata = ill.UgoiraMetadata ?? await _makoClient.GetUgoiraMetadataAsync(ill.Id, token);
                var zipUrl = isOriginal ? (metadata.LargeUrl ?? metadata.MediumUrl) : metadata.MediumUrl;
                if (string.IsNullOrEmpty(zipUrl))
                    return null;
                if (isOriginal ? _retryOriginalImage : _retryImageLoad)
                    _images.ImageCache.Remove(zipUrl);
                var delays = metadata.Delays;
                return await _images.GetUgoiraAnimatedImageAsync(
                    _platform,
                    new Uri(zipUrl),
                    delays,
                    new Progress<double>(UpdateLoadingProgress),
                    onPreview, token);
            }
            case Illustration ill:
            {
                var url = isOriginal
                    ? (ill.OriginalUrl ?? ill.ImageUrls.Large ?? ill.ImageUrls.Medium)
                    : (ill.ImageUrls.Large ?? ill.ImageUrls.Medium ?? ill.OriginalUrl);
                if (string.IsNullOrEmpty(url))
                    return null;
                if (isOriginal ? _retryOriginalImage : _retryImageLoad)
                    _images.ImageCache.Remove(url);
                return await _images.GetSingleImageAsync(
                    _platform,
                    url,
                    new Progress<double>(UpdateLoadingProgress),
                    onPreview, token);
            }
            case BooruPost bp:
            {
                var url = isOriginal
                    ? (bp.OriginalUrl ?? bp.LargeFileUrl ?? bp.PreviewFileUrl)
                    : (bp.LargeFileUrl ?? bp.PreviewFileUrl ?? bp.OriginalUrl);
                if (string.IsNullOrEmpty(url))
                    return null;
                if (isOriginal ? _retryOriginalImage : _retryImageLoad)
                    _images.ImageCache.Remove(url);
                return await _images.GetSingleImageAsync(
                    _platform,
                    url,
                    new Progress<double>(UpdateLoadingProgress),
                    onPreview, token);
            }
            default:
                return null;
        }
    }

    partial void OnTransformedSourceChanging(IAnimatedBitmap? value)
    {
        if (!ReferenceEquals(value, TransformedSource))
            TransformedSource?.Dispose();
    }

    [RelayCommand(CanExecute = nameof(CanViewOriginal))]
    private async Task ViewOriginalAsync(Control? control)
    {
        if (!CanViewOriginal)
            return;
        var token = _lifetimeCancellationTokenSource.Token;
        var viewContainer = control is null ? null : TopLevel.GetTopLevel(control)?.ViewContainer;
        IsProcessingImage = true;
        try
        {
            _notifications.ShowInformation(Strings.ImageViewerPageLoadingOriginalImage());
            LoadingProgress = null;
            var source = await DisplaySource.UpdateAsync(
                updateToken => LoadWithPreviewAsync(true, updateToken), token);
            if (_disposed)
            {
                source?.Dispose();
                return;
            }
            if (source is null)
                throw new InvalidDataException("The original image could not be loaded or decoded.");
            TransformedSource = source;
            _retryOriginalImage = false;
            viewContainer?.ShowSuccess(Strings.ImageViewerPageOriginalImageLoadedSuccessfully());
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch
        {
            if (!token.IsCancellationRequested)
            {
                _retryOriginalImage = true;
                viewContainer?.ShowError(Strings.ImageViewerPageOriginalImageLoadFailed());
            }
        }
        finally
        {
            if (!_disposed)
            {
                ReleaseLoadingPreview();
                IsProcessingImage = false;
            }
        }
    }

    internal async Task ExecuteTransformerExtensionAsync(IImageTransformerCommandExtension extension, Control? control)
    {
        if (!CanExecuteTransformer(extension))
            return;
        var token = _lifetimeCancellationTokenSource.Token;
        var viewContainer = control is null ? null : TopLevel.GetTopLevel(control)?.ViewContainer;
        try
        {
            _notifications.ShowInformation(Strings.ImageViewerPageApplyingTransformerExtensions());
            await TransformExtensionCommand.ExecuteAsync(extension);
            if (!token.IsCancellationRequested && IsTransformerActive(extension))
                viewContainer?.ShowSuccess(Strings.ImageViewerPageTransformerExtensionFinishedSuccessfully());
        }
        catch (OperationCanceledException e) when (e.CancellationToken.IsCancellationRequested || !IsTransformerActive(extension))
        {
            // Closing the viewer or disabling the extension discards its result.
        }
        catch
        {
            if (!token.IsCancellationRequested && IsTransformerActive(extension))
                viewContainer?.ShowError(Strings.ImageViewerPageTransformerExtensionFailed());
        }
    }

    private bool CanExecuteTransformer(IImageTransformerCommandExtension? extension) =>
        CanTransformExtension && extension is not null && IsTransformerActive(extension);

    private bool IsTransformerActive(IImageTransformerCommandExtension extension) =>
        _extensions.ActiveImageTransformerCommands.Contains(extension);

    [RelayCommand(CanExecute = nameof(CanExecuteTransformer))]
    private async Task TransformExtensionAsync(IImageTransformerCommandExtension? extension)
    {
        if (extension is null || !CanExecuteTransformer(extension) || OriginalSource is not { } originalSource)
            return;

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellationTokenSource.Token);
        var token = cancellation.Token;
        _activeTransformer = extension;
        _transformCancellation = cancellation;
        StartObservingExtensionHosts();
        IsProcessingImage = true;
        try
        {
            originalSource.Init();
            if (originalSource.Frames is not [var frame, ..])
                return;

            await using var source = Streams.RentStream();
            frame.Save(source, new PngBitmapEncoderOptions());
            source.Position = 0;

            // The extension ABI cannot cancel work. Keep both streams valid until
            // it actually finishes, even when the viewer has already closed.
            await using var destination = Streams.RentStream();
            await extension.TransformAsync(source, destination);
            token.ThrowIfCancellationRequested();
            if (!IsTransformerActive(extension))
                throw new OperationCanceledException();
            destination.Position = 0;
            var updatedSource = await DisplaySource.UpdateAsync(
                _ => Task.FromResult<IAnimatedBitmap?>(_images.LoadAnimatedBitmap(destination)), token);
            if (_disposed)
            {
                updatedSource?.Dispose();
                token.ThrowIfCancellationRequested();
            }
            if (updatedSource is null)
                throw new InvalidDataException("The image transformer did not produce a decodable image.");
            TransformedSource = updatedSource;
        }
        finally
        {
            _activeTransformer = null;
            _transformCancellation = null;
            if (_previewConsumers is 0)
                StopObservingExtensionHosts();
            if (!_disposed)
                IsProcessingImage = false;
        }
    }

    private void ExtensionHostsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => ObserveExtensionHosts();

    private void StartObservingExtensionHosts()
    {
        if (_observingExtensionHosts)
            return;
        _observingExtensionHosts = true;
        _extensions.HostModels.CollectionChanged += ExtensionHostsOnCollectionChanged;
        ObserveExtensionHosts();
    }

    private void ObserveExtensionHosts()
    {
        foreach (var host in _observedExtensionHosts)
            host.PropertyChanged -= ExtensionHostOnPropertyChanged;
        _observedExtensionHosts.Clear();
        _observedExtensionHosts.AddRange(_extensions.HostModels);
        foreach (var host in _observedExtensionHosts)
            host.PropertyChanged += ExtensionHostOnPropertyChanged;
        RefreshTransformerExtensions();
    }

    private void StopObservingExtensionHosts()
    {
        _observingExtensionHosts = false;
        _extensions.HostModels.CollectionChanged -= ExtensionHostsOnCollectionChanged;
        foreach (var host in _observedExtensionHosts)
            host.PropertyChanged -= ExtensionHostOnPropertyChanged;
        _observedExtensionHosts.Clear();
    }

    private void ExtensionHostOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExtensionsHostModel.IsActive))
            RefreshTransformerExtensions();
    }

    private void RefreshTransformerExtensions()
    {
        if (_disposed)
            return;
        if (_activeTransformer is { } active && !IsTransformerActive(active))
            _transformCancellation?.Cancel();
        var previous = TransformerExtensionItems;
        TransformerExtensionItems = [.. _extensions.ActiveImageTransformerCommands.Select(extension =>
            previous.FirstOrDefault(item => ReferenceEquals(item.Extension, extension)) ?? new ImageTransformerExtensionCommandItem(this, extension))];
        foreach (var item in previous.Except(TransformerExtensionItems))
            item.Dispose();
        OnPropertyChanged(nameof(TransformerExtensionItems));
        OnPropertyChanged(nameof(PrimaryTransformerExtensionItem));
        OnPropertyChanged(nameof(HasTransformerExtensions));
        TransformExtensionCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasDisplayImage))]
    private void ZoomIn() => ZoomFactor *= 1.2;

    [RelayCommand(CanExecute = nameof(HasDisplayImage))]
    private void ZoomOut() => ZoomFactor /= 1.2;

    [RelayCommand(CanExecute = nameof(HasDisplayImage))]
    private void ZoomToOriginal() => ZoomFactor = 1;

    [RelayCommand(CanExecute = nameof(HasDisplayImage))]
    private void Mirror()
    {
        // 仅做IsEnabled绑定，实际逻辑修改IsMirrored属性
    }

    [RelayCommand(CanExecute = nameof(HasDisplayImage))]
    private void RotateClockwise() => RotationDegree = (RotationDegree + 90) % 360;

    [RelayCommand(CanExecute = nameof(HasDisplayImage))]
    private void RotateCounterclockwise() => RotationDegree = (RotationDegree - 90 + 360) % 360;

    [RelayCommand(CanExecute = nameof(IsGifLoadSuccessfully))]
    private void PlayPause()
    {
        // 仅做IsEnabled绑定，实际逻辑修改IsPlaying属性
    }

    [RelayCommand(CanExecute = nameof(CanCopyDisplayedImage))]
    private async Task CopyAsync(Control? control)
    {
        if (!CanCopyDisplayedImage || DisplaySource.Frames is not [var frame])
            return;
        try { await _artworkActions.CopyBitmapAsync(frame, _lifetimeCancellationTokenSource.Token); }
        catch
        {
            if (!_disposed)
                _notifications.ShowError(Strings.ImageViewerPageCopyImageFailed());
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveImage))]
    private Task SaveImageAsync(Control? control)
    {
        if (!CanSaveImage)
            return Task.CompletedTask;
        var target = _saveTarget is WorkEntry work ? work.AsWorkEntry : _saveTarget;
        return target is Illustration illustration
            ? _artworkActions.SaveIllustrationAsync(illustration, Index)
            : _artworkActions.SaveWorkAsync(target);
    }

    [RelayCommand(CanExecute = nameof(CanSaveDisplayedImage))]
    private async Task SaveAsAsync(Control? control)
    {
        if (!CanSaveDisplayedImage || DisplaySource.Frames is not [var frame])
            return;
        try { await _artworkActions.SaveBitmapAsAsync(frame, GetEntryIdString(), _lifetimeCancellationTokenSource.Token); }
        catch
        {
            if (!_disposed)
                _notifications.ShowError(Strings.ImageViewerPageSaveImageFailed());
        }
    }

    private string GetEntryIdString() => _entry switch
    {
        Illustration ill => ill.Id.ToString(),
        BooruPost bp => bp.Id.ToString(),
        _ => "image"
    };

}

public sealed class ImageTransformerExtensionCommandItem : IDisposable
{
    private readonly SingleViewerViewModel _viewModel;
    public ImageTransformerExtensionCommandItem(SingleViewerViewModel viewModel, IImageTransformerCommandExtension extension)
    {
        _viewModel = viewModel;
        Extension = extension;
        Label = extension.Label;
        Description = extension.Description;
        Symbol = extension.Icon;
        Command = new AsyncRelayCommand<Control?>(
            control => viewModel.ExecuteTransformerExtensionAsync(extension, control),
            _ => viewModel.TransformExtensionCommand.CanExecute(extension));
        viewModel.TransformExtensionCommand.CanExecuteChanged += CanExecuteChanged;
    }

    private void CanExecuteChanged(object? sender, EventArgs e) => Command.NotifyCanExecuteChanged();

    public void Dispose() => _viewModel.TransformExtensionCommand.CanExecuteChanged -= CanExecuteChanged;

    public IImageTransformerCommandExtension Extension { get; }

    public string Label { get; }

    public string Description { get; }

    public Symbol Symbol { get; }

    public IAsyncRelayCommand<Control?> Command { get; }
}
