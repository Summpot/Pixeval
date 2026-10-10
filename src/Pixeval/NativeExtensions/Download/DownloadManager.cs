// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Threading;
using Pixeval.Utilities.Network;

namespace Pixeval.Native.Download;

public partial class DownloadManager
{
    private int _concurrencyDegree;
    private bool _disposed;

    public void MarkDisposed() => _disposed = true;

    public ObservableCollection<DownloadItemSnapshot> OrdinaryItems { get; } = [];

    public ObservableCollection<DownloadFolderSnapshot> Folders { get; } = [];

    public int ConcurrencyDegree
    {
        get => _concurrencyDegree;
        set
        {
            _concurrencyDegree = value;
            SetConcurrency((uint)Math.Max(1, value));
        }
    }

    public DownloadManager(HttpClient? httpClient, int concurrencyDegree)
        : this((uint)Math.Max(1, concurrencyDegree), null, GetEffectiveNetworkOptions())
    {
        _ = httpClient;
        _concurrencyDegree = Math.Max(1, concurrencyDegree);
    }

    public void AttachPageSnapshotCallback() =>
        SetPageCallback(new PageSnapshotCallback(this));

    public void UpdateNetworkOptions()
    {
        UpdateNetworkOptions(GetEffectiveNetworkOptions());
    }

    public static DownloadNetworkOptions GetEffectiveNetworkOptions()
    {
        var networkSettings = App.Services?.GetService<AppManagement.Settings.AppSettings>()?.NetworkSettings;
        var proxyUrl = ProxyHelper.GetEffectiveProxyUrl(networkSettings);

        var staticDomainIps = new Dictionary<string, List<string>>();
        if (networkSettings?.PixivDomainFronting is { EnablePixivDomainFronting: true } df)
        {
            if (df.PixivImageNameResolver.Count > 0)
                staticDomainIps[MakoHttpOptions.ImageHost] = [.. df.PixivImageNameResolver];
            if (df.PixivImageNameResolver2.Count > 0)
                staticDomainIps[MakoHttpOptions.ImageHost2] = [.. df.PixivImageNameResolver2];
        }

        return new DownloadNetworkOptions(proxyUrl, staticDomainIps);
    }

    public void ApplyPageSnapshot(DownloadPageSnapshot snapshot)
    {
        if (_disposed)
            return;

        SnapshotListDiff.Apply(OrdinaryItems, snapshot.OrdinaryItems, static item => item.Key);
        SnapshotListDiff.Apply(Folders, snapshot.Folders, static folder => folder.SubscriptionId);
    }

    public void ClearTasks()
    {
        var snapshot = CurrentPageSnapshot();
        var keys = snapshot.OrdinaryItems
            .Select(item => item.Key)
            .Concat(snapshot.Folders.SelectMany(folder => folder.Items.Select(item => item.Key)))
            .ToList();
        foreach (var key in keys)
            RemoveWork(key, false);
        ClearNativeTasks();
    }

    private void OnPageSnapshot(DownloadPageSnapshot snapshot)
    {
        if (_disposed)
            return;

        if (Application.Current is null || Dispatcher.UIThread.CheckAccess())
            ApplyPageSnapshot(snapshot);
        else
            Dispatcher.UIThread.Post(() => ApplyPageSnapshot(snapshot));
    }

    private sealed class PageSnapshotCallback(DownloadManager parent) : IDownloadPageCallback
    {
        public void OnPageSnapshot(DownloadPageSnapshot snapshot) => parent.OnPageSnapshot(snapshot);
    }
}
