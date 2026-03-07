// Copyright (c) Pixeval.
// Licensed under the GPL v3 License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Misaki;
using Pixeval.Utilities.IO.Caching;

namespace Pixeval.ViewModels;

public abstract partial class ThumbnailEntryViewModel<T>(T entry) : EntryViewModel<T>(entry), IDisposable
    where T : class, IIdentityInfo
{
    private const int RetryDelaySeconds = 5;

    public string Id => Entry.Id;

    private HashSet<object> References { get; } = new(ReferenceEqualityComparer.Instance);

    private readonly SemaphoreSlim _loadingGate = new(1, 1);

    protected abstract string ThumbnailUrl { get; }

    /// <summary>
    /// 缩略图图片
    /// </summary>
    [ObservableProperty]
    public partial Bitmap? Thumbnail { get; protected set; }

    private CancellationTokenSource _loadingThumbnailCts = new();

    private DateTimeOffset _nextRetryAtUtc;

    /// <summary>
    /// 是否正在加载缩略图
    /// </summary>
    protected bool LoadingThumbnail { get; private set; }

    /// <summary>
    /// 当控件需要显示图片时，调用此方法加载缩略图
    /// </summary>
    /// <returns>缩略图首次加载完成则返回<see langword="true"/>，之前已加载、正在加载或加载失败则返回<see langword="false"/></returns>
    public virtual async ValueTask<bool> TryLoadThumbnailAsync(object key)
    {
        _ = References.Add(key);

        if (Thumbnail is not null || LoadingThumbnail || DateTimeOffset.UtcNow < _nextRetryAtUtc)
            return false;

        await _loadingGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Thumbnail is not null || LoadingThumbnail || DateTimeOffset.UtcNow < _nextRetryAtUtc)
                return false;

            LoadingThumbnail = true;
            _loadingThumbnailCts.Dispose();
            _loadingThumbnailCts = new CancellationTokenSource();

            var bitmap = await ThumbnailBitmapProvider.Current
                .GetBitmapAsync(ThumbnailUrl, _loadingThumbnailCts.Token)
                .ConfigureAwait(false);

            if (bitmap is null)
            {
                _nextRetryAtUtc = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(RetryDelaySeconds);
                return false;
            }

            ReleaseThumbnail();
            Thumbnail = bitmap;
            _nextRetryAtUtc = DateTimeOffset.MinValue;

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception)
        {
            _nextRetryAtUtc = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(RetryDelaySeconds);
            return false;
        }
        finally
        {
            LoadingThumbnail = false;
            _loadingGate.Release();
        }
    }

    /// <summary>
    /// 当控件Unload时，调用此方法以尝试释放内存
    /// </summary>
    public void UnloadThumbnail(object key)
    {
        _ = References.Remove(key);
        if (References.Count is not 0)
            return;

        if (LoadingThumbnail)
            _loadingThumbnailCts.Cancel();

        ReleaseThumbnail();
    }

    /// <summary>
    /// 强制释放所有缩略图
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        References.Clear();
        _loadingThumbnailCts.Cancel();
        _loadingThumbnailCts.Dispose();
        _loadingGate.Dispose();
        ReleaseThumbnail();
        DisposeOverride();
    }

    protected virtual void DisposeOverride()
    {
    }

    private void ReleaseThumbnail()
    {
        if (Thumbnail is { } thumbnail)
            thumbnail.Dispose();

        Thumbnail = null;
    }

    public override bool Equals(object? obj) => obj is ThumbnailEntryViewModel<T> viewModel && Entry.Equals(viewModel.Entry);

    public override int GetHashCode() => Entry.GetHashCode();
}
