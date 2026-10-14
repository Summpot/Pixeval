// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Pixeval.Common.Collections;
using Pixeval.Pixiv;

namespace Pixeval.ViewModels.Works;

public abstract class EntryViewViewModel<T, TViewModel>
    : ViewModelBase, ISimpleViewViewModel, IDisposable
    where T : notnull
    where TViewModel : notnull
{
    private bool _isDisposed;
    private IncrementalLoadingCollection<TViewModel>? _source;

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

        var snapshot = BlockedContentHelper.CaptureSnapshot();
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
    IReadOnlyCollection<object> ISimpleViewViewModel.View => View as IReadOnlyCollection<object> ?? new BoxedReadOnlyCollectionAdapter(View);

    /// <inheritdoc />
    IReadOnlyCollection<object> ISimpleViewViewModel.Source => Source as IReadOnlyCollection<object> ?? new BoxedReadOnlyCollectionAdapter(Source);

    private sealed class BoxedReadOnlyCollectionAdapter(IReadOnlyCollection<TViewModel> inner) : IReadOnlyCollection<object>
    {
        public int Count => inner.Count;
        public IEnumerator<object> GetEnumerator()
        {
            foreach (var item in inner)
                yield return item;
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

