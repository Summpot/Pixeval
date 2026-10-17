// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Pixeval.Controls;
using Pixeval.Pixiv;
using Pixeval.Storage;
using Pixeval.Utilities;

namespace Pixeval.Views.Works;

public abstract partial class HistoryPage : IconContentPage, IDisposable
{
    private bool _isDisposed;
    private bool _isSourceInitialized;

    protected HistoryPage() => InitializeComponent();

    private void SimpleWorkTypeComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        if (!_isSourceInitialized)
            return;

        ResetSource(sender.GetSelectedValue<SimpleWorkKind>());
    }

    private void WorkContainer_OnRefreshRequested(object? sender, RoutedEventArgs e) => ResetSource();

    protected void InitializeSource()
    {
        if (_isSourceInitialized)
            return;

        _isSourceInitialized = true;
        Source.Changed += Source_OnChanged;
        SimpleWorkTypeComboBox_OnSelectionChanged(SimpleWorkTypeComboBox, EventArgs.Empty);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_isDisposed)
            return;

        RaiseEvent(new ViewModelDisposalEventArgs(ViewModelDisposal.ViewModelDisposalEvent, this));
    }

    private void Source_OnChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (!_isDisposed)
                ResetSource();
        });

    private void ResetSource(SimpleWorkKind? workType = null)
    {
        var type = workType ?? SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkKind>();
        WorkContainer.ResetSource(Source.StreamAsync(type), type);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_isDisposed)
            return;

        _isDisposed = true;
        Source.Changed -= Source_OnChanged;
    }

    protected abstract IArtworkHistorySource Source { get; }
}

public class BrowsingHistoryPage : HistoryPage
{
    private readonly IArtworkHistorySource _source;

    public BrowsingHistoryPage(StorageEngine storage)
    {
        _source = storage.HistoryRepository;
        InitializeSource();
    }

    /// <inheritdoc />
    protected override IArtworkHistorySource Source => _source;
}

public class WatchLaterPage : HistoryPage
{
    private readonly IArtworkHistorySource _source;

    public WatchLaterPage(StorageEngine storage)
    {
        _source = storage.WatchLaterRepository;
        InitializeSource();
    }

    /// <inheritdoc />
    protected override IArtworkHistorySource Source => _source;
}
