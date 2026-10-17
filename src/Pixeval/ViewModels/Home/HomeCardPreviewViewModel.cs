// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pixeval.Common.Collections;
using Pixeval.Localization;
using Pixeval.Config;

namespace Pixeval.ViewModels.Home;

public sealed partial class HomeCardPreviewViewModel(
    HomePageCardLayout card,
    Func<HomePageCardLayout, Task<HomeCardPreviewSource>> previewSourceFactory) : ViewModelBase, IDisposable
{
    private readonly CancellationTokenSource _loadingCts = new();
    private HomeCardPreviewSource? _source;
    private INotifyCollectionChanged? _itemsCollectionChanged;
    private Task? _loadingTask;
    private bool _isDisposed;

    public HomePageCardLayout Card { get; } = card;

    [ObservableProperty]
    public partial ISimpleViewViewModel? ViewModel { get; private set; }

    public IReadOnlyCollection<object> Items => ViewModel?.View ?? [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    public partial bool CanRetry { get; private set; }

    [RelayCommand(CanExecute = nameof(CanRetry))]
    private Task RetryAsync() => LoadAsync();

    private string? _placeholder = nameof(Strings.HomePageCardPreviewLoadingTextBlockText);
    public string? PlaceholderText => _placeholder is null ? null : Strings.GetString(_placeholder);
    private void SetPlaceholder(string? key)
    {
        _placeholder = key;
        OnPropertyChanged(nameof(PlaceholderText));
    }

    public async Task LoadAsync()
    {
        if (_isDisposed)
            return;

        if (_loadingTask is { } loadingTask && (!loadingTask.IsCompleted || !CanRetry))
        {
            await loadingTask;
            return;
        }

        _loadingTask = LoadCoreAsync();
        await _loadingTask;
    }

    public Task EnsureLoadedAsync() => ViewModel is null ? LoadAsync() : Task.CompletedTask;

    public HomeCardPreviewSource? Source => _source;

    private async Task LoadCoreAsync()
    {
        try
        {
            CanRetry = false;
            SetPlaceholder(nameof(Strings.HomePageCardPreviewLoadingTextBlockText));
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var oldSource = _source;
                var source = await previewSourceFactory(Card);
                if (_isDisposed)
                {
                    source.Dispose();
                    return;
                }

                DetachItems();
                _source = source;
                ViewModel = source.ViewModel;
                OnPropertyChanged(nameof(Items));
                AttachItems();
                if (!ReferenceEquals(oldSource, source))
                    oldSource?.Dispose();

                await LoadInitialItemsAsync();
                if (attempt is not 0 || Items.Count is not 0)
                    break;

                if (Items is IIncrementalLoading { IsInterrupted: true, HasMoreItems: true })
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), _loadingCts.Token);
                    await LoadInitialItemsAsync();
                    break;
                }

                if (Items is not IIncrementalLoading { LoadError: not null })
                    break;

                // An iterator that threw may be permanently closed. Retry with a fresh source.
                await Task.Delay(TimeSpan.FromSeconds(1), _loadingCts.Token);
            }
            UpdatePlaceholder();
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception)
        {
            if (!_isDisposed)
            {
                CanRetry = true;
                SetPlaceholder(nameof(Strings.HomePageCardPreviewFailedTextBlockText));
            }
        }
    }

    private async Task LoadInitialItemsAsync()
    {
        if (_isDisposed || Items is not IIncrementalLoading { HasMoreItems: true } incremental)
            return;

        await incremental.LoadMoreItemsAsync(0, _loadingCts.Token);
    }

    private void AttachItems()
    {
        if (Items is not INotifyCollectionChanged items)
            return;

        _itemsCollectionChanged = items;
        _itemsCollectionChanged.CollectionChanged += Items_OnCollectionChanged;
    }

    private void DetachItems()
    {
        _itemsCollectionChanged?.CollectionChanged -= Items_OnCollectionChanged;
        _itemsCollectionChanged = null;
    }

    private void Items_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Items));
        UpdatePlaceholder();
    }

    private void UpdatePlaceholder()
    {
        if (_isDisposed)
            return;
        CanRetry = Items is IIncrementalLoading incremental && (incremental.LoadError is not null || incremental.IsInterrupted);
        SetPlaceholder(Items.Count is not 0 ? null : CanRetry
            ? nameof(Strings.HomePageCardPreviewFailedTextBlockText)
            : nameof(Strings.HomePageCardPreviewEmptyTextBlockText));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        CanRetry = false;
        _loadingCts.Cancel();
        _loadingCts.Dispose();
        DetachItems();
        var source = _source;
        _source = null;
        ViewModel = null;
        OnPropertyChanged(nameof(Items));
        source?.Dispose();
    }
}
