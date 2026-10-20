// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Pixeval.Common.Collections;

/// <summary>
/// This class represents an <see cref="ObservableCollection{IType}"/> whose items can be loaded incrementally.
/// </summary>
/// <typeparam name="IType">
/// The type of collection items.
/// </typeparam>
/// <seealso cref="IIncrementalSource{TSource}"/>
/// <seealso cref="IIncrementalLoading"/>
public class IncrementalLoadingCollection<IType> : ObservableRangeCollection<IType>, IIncrementalLoading, IDisposable
{
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly Lock _loadingTaskGate = new();
    private Task<int>? _loadingTask;
    private bool _isDisposed;

    /// <summary>
    /// Gets a value indicating the source of incremental loading.
    /// </summary>
    protected IIncrementalSource<IType> Source { get; }

    public bool IsInterrupted => !IsLoading && Source.IsInterrupted;

    public Exception? LoadError { get; private set; }

    /// <summary>
    /// Gets a value indicating how many items that must be retrieved for each incremental call.
    /// </summary>
    protected int ItemsPerPage { get; }

    /// <summary>
    /// Gets or sets a value indicating The zero-based index of the current items page.
    /// </summary>
    protected int CurrentPageIndex { get; set; }

    /// <summary>
    /// Gets a value indicating whether new items are being loaded.
    /// </summary>
    public bool IsLoading
    {
        get;
        private set
        {
            if (value == field)
                return;
            field = value;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsLoading)));
        }
    }

    /// <summary>
    /// Gets a value indicating whether the collection contains more items to retrieve.
    /// </summary>
    public bool HasMoreItems
    {
        get;

        private set
        {
            if (value == field)
                return;
            field = value;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasMoreItems)));
        }
    } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="IncrementalLoadingCollection{IType}"/> class using the specified <see cref="IIncrementalSource{TSource}"/> implementation and, optionally, how many items to load for each data page.
    /// </summary>
    /// <param name="source">
    /// An implementation of the <see cref="IIncrementalSource{TSource}"/> interface that contains the logic to actually load data incrementally.
    /// </param>
    /// <param name="itemsPerPage">
    /// The number of items to retrieve for each call. Default is 20.
    /// </param>
    /// <seealso cref="IIncrementalSource{TSource}"/>

    public IncrementalLoadingCollection(IIncrementalSource<IType> source, int itemsPerPage = 20)
    {
        ArgumentNullException.ThrowIfNull(source);

        Source = source;

        ItemsPerPage = itemsPerPage;
    }

    /// <summary>
    /// Actually performs the incremental loading.
    /// </summary>
    /// <param name="token">
    /// Used to propagate notification that operation should be canceled.
    /// </param>
    /// <returns>
    /// Returns a collection of <typeparamref name="IType"/>.
    /// </returns>
    protected virtual async Task<IReadOnlyCollection<IType>> LoadDataAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var result = await Source.GetPagedItemsAsync(CurrentPageIndex, ItemsPerPage, token);
        token.ThrowIfCancellationRequested();
        CurrentPageIndex += 1;
        return result;
    }

    /// <summary>
    /// Initializes incremental loading from the view.
    /// </summary>
    /// <param name="count">
    /// The number of items to load.
    /// </param>
    /// <param name="token"></param>
    /// <returns>
    /// How many items have been actually retrieved.
    /// </returns>
    public Task<int> LoadMoreItemsAsync(int count, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (token.IsCancellationRequested)
            return Task.FromCanceled<int>(token);

        lock (_loadingTaskGate)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_loadingTask is { IsCompleted: false } loadingTask)
                return WaitForLoadingTaskAsync(loadingTask, token);

            // A caller may stop waiting without canceling a load shared by another view.
            var newLoadingTask = LoadMoreItemsCoreAsync(_lifetimeCts.Token);
            _loadingTask = newLoadingTask;
            _ = ResetLoadingTaskAsync(newLoadingTask);
            return WaitForLoadingTaskAsync(newLoadingTask, token);
        }
    }

    private async Task<int> LoadMoreItemsCoreAsync(CancellationToken token)
    {
        LoadError = null;
        try
        {
            IReadOnlyCollection<IType> data;
            try
            {
                IsLoading = true;
                data = await LoadDataAsync(token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LoadError = ex;
                HasMoreItems = Source.HasMoreItems;
                return 0;
            }

            if (data is not { Count: not 0 })
            {
                HasMoreItems = Source.HasMoreItems;
                return 0;
            }

            AddRange(data as IReadOnlyList<IType> ?? [.. data]);

            HasMoreItems = Source.HasMoreItems;
            return data.Count;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static Task<int> WaitForLoadingTaskAsync(Task<int> loadingTask, CancellationToken token)
    {
        return token.CanBeCanceled ? loadingTask.WaitAsync(token) : loadingTask;
    }

    private async Task ResetLoadingTaskAsync(Task<int> loadingTask)
    {
        try
        {
            await loadingTask;
        }
        catch
        {
            // The initiating caller observes the load failure; this continuation only resets shared state.
        }
        finally
        {
            lock (_loadingTaskGate)
            {
                if (ReferenceEquals(_loadingTask, loadingTask))
                    _loadingTask = null;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Task<int>? loadingTask;
        lock (_loadingTaskGate)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            loadingTask = _loadingTask;
        }

        _lifetimeCts.Cancel();
        if (Source is IDisposable disposable)
            disposable.Dispose();
        if (loadingTask is { IsCompleted: false })
            _ = DisposeLifetimeCtsAsync(loadingTask, _lifetimeCts);
        else
            _lifetimeCts.Dispose();
    }

    private static async Task DisposeLifetimeCtsAsync(Task loadingTask, CancellationTokenSource lifetimeCts)
    {
        try
        {
            await loadingTask.ConfigureAwait(false);
        }
        catch
        {
            // The initiating caller observes the load result; disposal only waits for it to finish.
        }
        finally
        {
            lifetimeCts.Dispose();
        }
    }
}
