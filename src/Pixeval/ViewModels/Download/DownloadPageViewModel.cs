// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.ObjectModel;

namespace Pixeval.ViewModels;

public sealed class DownloadPageViewModel(DownloadManager manager) : ViewModelBase, IDisposable
{
    public DownloadManager Manager { get; } = manager;

    public ObservableCollection<DownloadItemSnapshot> OrdinaryItems => Manager.OrdinaryItems;

    public ObservableCollection<DownloadFolderSnapshot> Folders => Manager.Folders;

    public void Pause(DownloadTaskKey key) => Manager.PauseWork(key);

    public void Resume(DownloadTaskKey key) => Manager.ResumeWork(key);

    public void Cancel(DownloadTaskKey key) => Manager.CancelWork(key);

    public void Reset(DownloadTaskKey key) => Manager.ResetWork(key);

    public void Remove(DownloadTaskKey key, bool deleteLocalFiles) => Manager.RemoveWork(key, deleteLocalFiles);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
