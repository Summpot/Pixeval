// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Collections;
using Pixeval.Models.Options;

namespace Pixeval.ViewModels;

public sealed partial class DownloadItemPageViewModel : ViewModelBase, IDisposable
{
    private bool _isDisposed;

    [ObservableProperty]
    public partial DownloadListOption CurrentOption { get; set; } = DownloadListOption.AllQueued;

    [ObservableProperty]
    public partial string? FilterText { get; set; }

    public DownloadPageViewModel PageViewModel { get; }

    public DownloadFolderViewModel? Folder { get; }

    public AdvancedObservableCollection<DownloadItemViewModel> View { get; }

    partial void OnCurrentOptionChanged(DownloadListOption value) => ResetFilter();

    partial void OnFilterTextChanged(string? value) => ResetFilter();

    public DownloadItemPageViewModel(DownloadPageViewModel pageViewModel, DownloadFolderViewModel? folder = null)
    {
        PageViewModel = pageViewModel;
        Folder = folder;
        View = new AdvancedObservableCollection<DownloadItemViewModel>(folder?.Items ?? pageViewModel.OrdinaryItems, true);
        ResetFilter();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_isDisposed)
            return;

        _isDisposed = true;
        View.Filters.Clear();
        View.Source = [];
        View.Dispose();
    }

    private void ResetFilter()
    {
        if (_isDisposed)
            return;

        using (View.DeferFiltersChange())
        {
            View.Filters.Clear();
            View.Filters.Add(IFilter<DownloadItemViewModel>.Create(
                new HashSet<string> { nameof(DownloadItemViewModel.CurrentState) },
                item => CurrentOption is DownloadListOption.CustomSearch
                    ? string.IsNullOrWhiteSpace(FilterText) || item.MatchesSearch(FilterText)
                    : item.MatchesOption(CurrentOption, null),
                false));
        }
    }
}
