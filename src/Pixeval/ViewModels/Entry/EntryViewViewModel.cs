// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Misaki;
using Pixeval.Collections;
using Pixeval.Models.Blocking;

namespace Pixeval.ViewModels;

public abstract class EntryViewViewModel<T, TViewModel>
    : ViewModelBase, ISimpleViewViewModel, IDisposable
    where T : class, IIdentityInfo
    where TViewModel : class
{
    private bool _isDisposed;

    public abstract IDataProvider<T, TViewModel> DataProvider { get; }

    public AdvancedObservableCollection<TViewModel> View => DataProvider.View;

    public ObservableCollection<TViewModel> Source => DataProvider.Source;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_isDisposed)
            return;

        _isDisposed = true;
        DataProvider.Dispose();
    }

    public void ResetEngine(IAsyncEnumerable<T>? newEngine, Func<T, int, TViewModel>? factory = null, int itemsPerPage = 20, int itemLimit = -1)
    {
        var snapshot = BlockedContentHelper.CaptureSnapshot();
        DataProvider.ResetEngine(
            newEngine,
            (entry, index) =>
            {
                var replaced = BlockedContentHelper.ReplaceEntry(entry, snapshot);
                return factory is not null ? factory(replaced, index) : (TViewModel) (object) replaced;
            },
            itemsPerPage,
            itemLimit);
    }

    /// <inheritdoc />
    IReadOnlyCollection<object> ISimpleViewViewModel.View => View;

    /// <inheritdoc />
    IReadOnlyCollection<object> ISimpleViewViewModel.Source => Source;
}
