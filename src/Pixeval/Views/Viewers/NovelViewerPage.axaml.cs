// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Mako.Global.Enum;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Models.Extensions;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Work;

namespace Pixeval.Views.Viewers;

public partial class NovelViewerPage : IconContentPage
{
    private NovelViewerPageViewModel ViewModel => (NovelViewerPageViewModel) DataContext!;

    private readonly List<ExtensionsHostModel> _observedExtensionHosts = [];
    private NovelViewerPageViewModel? _observedReader;
    private (long Novel, int Page)? _readingPosition;
    private NovelViewerPageViewModel? _bookmarkReader;
    private NovelItemViewModel? _bookmarkTarget;
    private CancellationTokenSource? _bookmarkLifetime;

    private void ReaderOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NovelViewerPageViewModel.NovelId) or nameof(NovelViewerPageViewModel.CurrentPageIndex))
            UpdateReadingPosition();
    }

    private void UpdateReadingPosition()
    {
        if (_observedReader is not { } model)
            return;
        var position = (model.NovelId, model.CurrentPageIndex);
        if (_readingPosition == position)
            return;
        _readingPosition = position;
        // Run after the Markdown binding has rebuilt the document. Theme changes,
        // translation toggles and a temporary tab switch retain their scroll position.
        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(_observedReader, model) && _readingPosition == position)
                ReaderMarkdown.ScrollValue = default;
        });
    }

    private static ExtensionService ExtensionService =>
        App.AppViewModel.AppServiceProvider.GetRequiredService<ExtensionService>();

    public NovelViewerPage() : this(null)
    {
    }

    public NovelViewerPage(NovelViewerPageViewModel? viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void ReaderLayout_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        InfoSplitView.DisplayMode = e.NewSize.Width < 720 ? SplitViewDisplayMode.Overlay : SplitViewDisplayMode.Inline;
        InfoSplitView.OpenPaneLength = Math.Min(360, Math.Max(0, e.NewSize.Width));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        _ = KeyboardShortcut.TryExecute(e, Key.Left, ViewModel.PrevCommand)
            || KeyboardShortcut.TryExecute(e, Key.Right, ViewModel.NextCommand)
            || KeyboardShortcut.TryExecute(e, Key.Up, ViewModel.PrevWorkCommand)
            || KeyboardShortcut.TryExecute(e, Key.Down, ViewModel.NextWorkCommand);
    }

    private void PrevButton_OnRightClick(object? sender, ContextRequestedEventArgs e)
    {
        if (ViewModel.PrevWorkCommand.CanExecute(null))
        {
            ViewModel.PrevWorkCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void NextButton_OnRightClick(object? sender, ContextRequestedEventArgs e)
    {
        if (ViewModel.NextWorkCommand.CanExecute(null))
        {
            ViewModel.NextWorkCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void MarkdownBox_OnHyperlinkClicked(object? sender, MarkdownHyperlinkClickedEventArgs e)
    {
        if (!TryParsePageLink(e.Url, out var pageIndex))
            return;

        e.Handled = true;

        if (pageIndex >= 0 && pageIndex < ViewModel.PageCount)
            ViewModel.CurrentPageIndex = pageIndex;
    }

    private async void AddToBookmarkButton_OnClick(object? sender, ContextRequestedEventArgs e)
    {
        if (!IsLoaded || sender is not Control anchor
            || ViewModel.CurrentNovel is not { IsBookmarkSupported: true } target
            || (target.IsBookmarkedDisplay & HeartButtonState.Pending) != 0
            || !target.AddToBookmarkCommand.CanExecute((null, false, this)))
            return;

        e.Handled = true;
        CancelBookmark();
        _bookmarkReader = ViewModel;
        _bookmarkTarget = target;
        _bookmarkReader.PropertyChanged += BookmarkReaderOnPropertyChanged;
        _bookmarkLifetime = new();
        var token = _bookmarkLifetime.Token;
        await BookmarkTagSelectorFlyoutHelper.ShowAsync(
            anchor, SimpleWorkType.Novel, target.Entry.Id,
            async selection =>
            {
                if (token.IsCancellationRequested)
                    return;
                await target.AddToBookmarkCommand.ExecuteAsync((selection.Tags, selection.IsPrivate, this));
                if (!token.IsCancellationRequested && target.LastBookmarkSucceeded)
                    TopLevel.GetTopLevel(this)?.ViewContainer?.ShowSuccess(I18NManager.GetResource(MiscResources.AddedToBookmark));
            },
            PlacementMode.Bottom, token);
    }

    private void BookmarkReaderOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NovelViewerPageViewModel.CurrentNovel)
            && !ReferenceEquals(_bookmarkTarget, _bookmarkReader?.CurrentNovel))
            CancelBookmark();
    }

    private void CancelBookmark()
    {
        _bookmarkReader?.PropertyChanged -= BookmarkReaderOnPropertyChanged;
        _bookmarkReader = null;
        _bookmarkTarget = null;
        _bookmarkLifetime?.Cancel();
        _bookmarkLifetime?.Dispose();
        _bookmarkLifetime = null;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        CancelBookmark();
        base.OnDataContextChanged(e);
    }

    private static bool TryParsePageLink(string url, out int pageIndex)
    {
        pageIndex = 0;
        var link = url.StartsWith('#') ? url[1..] : url;

        if (!link.StartsWith("page", StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(link[4..], out pageIndex);
    }

    #region Disposal

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _observedReader = ViewModel;
        _observedReader.PropertyChanged += ReaderOnPropertyChanged;
        UpdateReadingPosition();
        ExtensionService.HostModels.CollectionChanged += ExtensionHostsOnCollectionChanged;
        ObserveExtensionHosts();
        RaiseEvent(new ViewModelDisposalEventArgs(ViewModelDisposal.ViewModelDisposalEvent, ViewModel));
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        CancelBookmark();
        if (_observedReader is { } model)
            model.PropertyChanged -= ReaderOnPropertyChanged;
        _observedReader = null;
        ExtensionService.HostModels.CollectionChanged -= ExtensionHostsOnCollectionChanged;
        foreach (var host in _observedExtensionHosts)
            host.PropertyChanged -= ExtensionHostOnPropertyChanged;
        _observedExtensionHosts.Clear();
        base.OnUnloaded(e);
    }

    private void ExtensionHostsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => ObserveExtensionHosts();

    private void ObserveExtensionHosts()
    {
        foreach (var host in _observedExtensionHosts)
            host.PropertyChanged -= ExtensionHostOnPropertyChanged;
        _observedExtensionHosts.Clear();
        _observedExtensionHosts.AddRange(ExtensionService.HostModels);
        foreach (var host in _observedExtensionHosts)
            host.PropertyChanged += ExtensionHostOnPropertyChanged;
        ViewModel.RefreshTranslationAvailability();
    }

    private void ExtensionHostOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ExtensionsHostModel.IsActive))
            ViewModel.RefreshTranslationAvailability();
    }

    #endregion
}
