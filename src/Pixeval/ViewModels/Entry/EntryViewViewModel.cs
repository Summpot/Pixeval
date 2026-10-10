// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Pixeval.Collections;
using Pixeval.Models.Blocking;

namespace Pixeval.ViewModels;

public abstract class EntryViewViewModel<T, TViewModel>
    : ViewModelBase, ISimpleViewViewModel, IDisposable
    where T : class
    where TViewModel : class
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
    IReadOnlyCollection<object> ISimpleViewViewModel.View => View;

    /// <inheritdoc />
    IReadOnlyCollection<object> ISimpleViewViewModel.Source => Source;
}
