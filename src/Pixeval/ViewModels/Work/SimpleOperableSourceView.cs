// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Pixeval.Collections;
using Pixeval.Models.Blocking;

namespace Pixeval.ViewModels;

public sealed class SimpleOperableSourceView<TViewModel>(IReadOnlyCollection<object> source)
    : ViewModelBase, ISourceView<object>
    where TViewModel : class
{
    private bool _isDisposed;

    public AdvancedObservableAdaptor<object, object> View { get; } = new(CreateSource(source), CreateArtwork);

    IAdvancedObservableView<object> ISourceView<object>.View => View;

    public ObservableCollection<object> Source => View.MappedSource;

    public ISourceView<TViewModel> CloneSourceView()
        => new SnapshotSourceView<TViewModel>(View.OfType<TViewModel>().Select(CloneItem));

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        View.Dispose();
    }

    private static object CreateArtwork(object info) => info;

    private static ObservableCollection<object> CreateSource(IReadOnlyCollection<object> source) =>
        [.. source.Select(static entry => BlockedContentHelper.Replace(entry))];

    private static TViewModel CloneItem(TViewModel viewModel) => viewModel;

    private sealed class SnapshotSourceView<TSnapshotViewModel>(IEnumerable<TSnapshotViewModel> source) : ViewModelBase, ISourceView<TSnapshotViewModel>
        where TSnapshotViewModel : class
    {
        private bool _isDisposed;

        public AdvancedObservableCollection<TSnapshotViewModel> View { get; } = new([.. source]);

        IAdvancedObservableView<TSnapshotViewModel> ISourceView<TSnapshotViewModel>.View => View;

        public ObservableCollection<TSnapshotViewModel> Source => View.Source;

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            View.Dispose();
        }
    }
}
