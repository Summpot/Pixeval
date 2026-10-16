// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
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

        if (_loadingTask is { } loadingTask)
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
            SetPlaceholder(nameof(Strings.HomePageCardPreviewLoadingTextBlockText));
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
                SetPlaceholder(nameof(Strings.HomePageCardPreviewFailedTextBlockText));
        }
    }

    private async Task LoadInitialItemsAsync()
    {
        if (!_isDisposed && Items is IIncrementalLoading { HasMoreItems: true } incremental)
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
        SetPlaceholder(Items.Count is 0 ? nameof(Strings.HomePageCardPreviewEmptyTextBlockText) : null);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
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
