// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Pixeval.Common.Collections;
using Pixeval.Pixiv;

namespace Pixeval.ViewModels.Works;

public abstract class EntryViewViewModel<T, TViewModel>
    : ViewModelBase, ISimpleViewViewModel, IDisposable
    where T : notnull
    where TViewModel : notnull
{
    private readonly IBlockedContentService? _blockedContent;
    private bool _isDisposed;
    private IncrementalLoadingCollection<TViewModel>? _source;
    private BoxedReadOnlyCollectionAdapter? _boxedView;
    private BoxedReadOnlyCollectionAdapter? _boxedSource;

    protected EntryViewViewModel(IBlockedContentService? blockedContent = null)
    {
        _blockedContent = blockedContent;
    }

    public virtual ObservableCollection<TViewModel> View => Source;

    public ObservableCollection<TViewModel> Source => _source ??= CreateEmptyCollection();

    protected IncrementalLoadingCollection<TViewModel>? BackingCollection => _source;

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_isDisposed)
            return;

        _isDisposed = true;
        _source?.Dispose();
        _source = null;
    }

    public virtual void ResetEngine(IAsyncEnumerable<T>? newEngine, Func<T, int, TViewModel>? factory = null, int itemsPerPage = 20, int itemLimit = -1)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _source?.Dispose();

        var snapshot = _blockedContent?.CaptureSnapshot()
                       ?? new BlockedContentSnapshot(FrozenSet<string>.Empty, FrozenSet<long>.Empty);
        var effectiveFactory = (T entry, int index) =>
        {
            var replaced = BlockedContentHelper.ReplaceEntry(entry, snapshot);
            return factory is not null ? factory(replaced, index) : (TViewModel) (object) replaced;
        };

        var engine = newEngine ?? EmptyAsyncEnumerable();
        var incrementalSource = new IncrementalSource<T, TViewModel>(engine, effectiveFactory, itemLimit);
        _source = new IncrementalLoadingCollection<TViewModel>(incrementalSource, itemsPerPage);

        OnPropertyChanged(nameof(Source));
        OnPropertyChanged(nameof(View));
    }

    private static IncrementalLoadingCollection<TViewModel> CreateEmptyCollection() =>
        new(new IncrementalSource<T, TViewModel>(EmptyAsyncEnumerable(), static (entry, _) => (TViewModel) (object) entry));

    private static async IAsyncEnumerable<T> EmptyAsyncEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }

    /// <inheritdoc />
    IReadOnlyCollection<object> ISimpleViewViewModel.View => AsObjects(View, ref _boxedView);

    /// <inheritdoc />
    IReadOnlyCollection<object> ISimpleViewViewModel.Source => AsObjects(Source, ref _boxedSource);

    private static IReadOnlyCollection<object> AsObjects(ObservableCollection<TViewModel> collection, ref BoxedReadOnlyCollectionAdapter? adapter)
    {
        if (collection is IReadOnlyCollection<object> objects)
            return objects;

        // Value-type entries cannot use collection covariance. Keep the adapter stable so
        // bindings and incremental loaders can observe the same collection throughout a load.
        if (adapter is null || !ReferenceEquals(adapter.Inner, collection))
            adapter = new(collection);
        return adapter;
    }

    private sealed class BoxedReadOnlyCollectionAdapter(ObservableCollection<TViewModel> inner)
        : IReadOnlyCollection<object>, IIncrementalLoading, INotifyCollectionChanged, INotifyPropertyChanged
    {
        public ObservableCollection<TViewModel> Inner => inner;
        public int Count => inner.Count;
        public bool HasMoreItems => inner is IIncrementalLoading { HasMoreItems: true };
        public bool IsInterrupted => inner is IIncrementalLoading { IsInterrupted: true };

        public Task<int> LoadMoreItemsAsync(int count, CancellationToken token = default) =>
            inner is IIncrementalLoading incremental
                ? incremental.LoadMoreItemsAsync(count, token)
                : Task.FromResult(0);

        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add => inner.CollectionChanged += value;
            remove => inner.CollectionChanged -= value;
        }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add => ((INotifyPropertyChanged) inner).PropertyChanged += value;
            remove => ((INotifyPropertyChanged) inner).PropertyChanged -= value;
        }

        public IEnumerator<object> GetEnumerator()
        {
            foreach (var item in inner)
                yield return item;
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

