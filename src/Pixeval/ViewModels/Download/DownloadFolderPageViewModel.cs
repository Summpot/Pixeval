// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.ObjectModel;

namespace Pixeval.ViewModels;

public sealed class DownloadFolderPageViewModel(DownloadPageViewModel pageViewModel) : ViewModelBase, IDisposable
{
    public DownloadPageViewModel PageViewModel { get; } = pageViewModel;

    public ObservableCollection<DownloadFolderViewModel> View => PageViewModel.SubscriptionFolders;

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
