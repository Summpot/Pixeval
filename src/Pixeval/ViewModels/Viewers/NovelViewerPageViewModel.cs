// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.AppManagement.Settings;
using Pixeval.I18N;
using Pixeval.Models.Blocking;
using Pixeval.Models.Pixiv;
using Pixeval.Models.Settings;
using Pixeval.Native.Mako;
using Pixeval.Utilities;

namespace Pixeval.ViewModels.Viewers;

public sealed partial class NovelViewerPageViewModel : PagedViewerViewModel, IDisposable
{
    private readonly Dictionary<int, Novel> _refreshedNovels = [];

    private readonly bool _needRefresh;

    private readonly ISourceView<Novel>? _sourceView;

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial string? LoadErrorMessage { get; private set; }

    [ObservableProperty]
    public partial WorkSeriesInfoViewModel? SeriesInfo { get; private set; }

    public NovelViewerPageViewModel(Novel novelViewModel, bool needRefresh)
    {
        _needRefresh = needRefresh;
        CurrentNovel = novelViewModel;
        CurrentWorkIndex = 0;
    }

    public NovelViewerPageViewModel(long id)
    {
        _ = LoadSingleNovelAsync(id, _loadingCts.Token);
    }

    public NovelViewerPageViewModel(ISourceView<Novel> dataProvider, int currentNovelIndex, bool needRefresh)
    {
        _needRefresh = needRefresh;
        _sourceView = dataProvider;
        CurrentWorkIndex = currentNovelIndex;
    }

    public IReadOnlyList<Novel>? Novels => _sourceView?.View;

    public Novel? CurrentNovel
    {
        get
        {
            if (_refreshedNovels.TryGetValue(CurrentWorkIndex, out var value))
                return value;

            if (field is not null)
                return field;

            return CurrentWorkIndex < 0 || CurrentWorkIndex >= WorkCount
                ? null
                : _sourceView?.View[CurrentWorkIndex];
        }
        private set
        {
            if (Equals(value, field))
                return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NovelId));
        }
    }

    public long NovelId => CurrentNovel?.Entry.Id ?? 0;

    public override int CurrentWorkIndex
    {
        get;
        set
        {
            if (value is -1)
                return;
            if (value == field)
                return;

            field = value;
            _ = LoadCurrentNovelAsync();

            OnPropertyChanged();
            OnPropertyChanged(nameof(NovelId));
            OnPropertyChanged(nameof(CurrentNovel));
        }
        // 第一次赋值属性时会判断 value == field，如果是0则无法进入set方法体
        // ReSharper disable once MemberInitializerValueIgnored
    } = -1;

    private bool _suppressMarkerSync;
    private CancellationTokenSource? _syncMarkerCts;

    public override int CurrentPageIndex
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentNovel));
            OnPropertyChanged(nameof(CurrentMarkdown));
            OnPropertyChanged(nameof(NextButtonText));
            OnPropertyChanged(nameof(PrevButtonText));
            NextCommand.NotifyCanExecuteChanged();
            PrevCommand.NotifyCanExecuteChanged();
            NextWorkCommand.NotifyCanExecuteChanged();
            PrevWorkCommand.NotifyCanExecuteChanged();

            if (!_suppressMarkerSync && CurrentNovel is { Entry.Id: var novelId } && novelId > 0 && PageCount > 0)
            {
                SyncMarkerDebounced(novelId, value);
            }
        }
    }

    private void SyncMarkerDebounced(long novelId, int pageIndex)
    {
        _syncMarkerCts?.Cancel();
        _syncMarkerCts?.Dispose();
        var cts = new CancellationTokenSource();
        _syncMarkerCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1500, cts.Token);
                if (cts.Token.IsCancellationRequested || _disposed)
                    return;

                await App.AppViewModel.MakoClient.AddNovelMarkerAsync(novelId, pageIndex + 1);
            }
            catch
            {
                // Silent cloud sync - non-intrusive
            }
        }, cts.Token);
    }

    public override int PageCount => _pageMarkdowns.Count;

    /// <inheritdoc />
    public override int WorkCount => Novels?.Count ?? 1;

    public bool IsMultiPage => PageCount > 1;

    public string CurrentMarkdown => PageCount is 0 ? "" : _pageMarkdowns[CurrentPageIndex];

    #region Settings

    private static AppSettings Settings => App.AppViewModel.AppSettings;

    public uint NovelBackground => Settings.NovelSettings.NovelBackground;

    public uint NovelFontColor => Settings.NovelSettings.NovelFontColor;

    public FontFamily? NovelFontFamilyObject => FontFamilyHelper.Create(Settings.NovelSettings.NovelFontFamily);

    public FontWeight NovelFontWeight => Settings.NovelSettings.NovelFontWeight;

    public int NovelFontSize => Settings.NovelSettings.NovelFontSize;

    public int NovelLineHeight => Settings.NovelSettings.NovelLineHeight;

    public int NovelMaxWidth => Settings.NovelSettings.NovelMaxWidth;

    public void NotifyNovelBackgroundChanged() => OnPropertyChanged(nameof(NovelBackground));
    public void NotifyNovelFontColorChanged() => OnPropertyChanged(nameof(NovelFontColor));
    public void NotifyNovelFontFamilyChanged() => OnPropertyChanged(nameof(NovelFontFamilyObject));
    public void NotifyNovelFontWeightChanged() => OnPropertyChanged(nameof(NovelFontWeight));
    public void NotifyNovelFontSizeChanged() => OnPropertyChanged(nameof(NovelFontSize));
    public void NotifyNovelLineHeightChanged() => OnPropertyChanged(nameof(NovelLineHeight));
    public void NotifyNovelMaxWidthChanged() => OnPropertyChanged(nameof(NovelMaxWidth));

    #endregion

    private List<string> _pageMarkdowns = [];

    private CancellationTokenSource _loadingCts = new();

    private async Task LoadSingleNovelAsync(long id, CancellationToken token)
    {
        var novel = await LoadNovelAsync(id, item => CurrentNovel = item, token);
        if (novel is null || token.IsCancellationRequested)
        {
            if (!token.IsCancellationRequested)
                IsLoading = false;
            return;
        }

        if (CurrentWorkIndex is 0)
            await LoadCurrentNovelAsync();
        else
            CurrentWorkIndex = 0;
    }

    private async Task LoadCurrentNovelAsync()
    {
        if (_disposed)
            return;

        var index = CurrentWorkIndex;
        var token = ResetLoadingToken();

        IsLoading = true;
        LoadErrorMessage = null;
        SeriesInfo = null;
        try
        {
            var currentNovel = await GetCurrentNovelAsync(index, token);
            token.ThrowIfCancellationRequested();
            if (currentNovel is null || index != CurrentWorkIndex || _disposed)
                return;

            if (BlockedContentHelper.IsBlockedPlaceholder(currentNovel.Entry))
                _pageMarkdowns = [I18NManager.GetResource(BlockedContentResources.Work)];
            else
            {
                SeriesInfo = WorkSeriesInfoViewModel.Create(currentNovel.Entry, SimpleWorkType.Novel);
                var content = await currentNovel.ContentAsync;
                token.ThrowIfCancellationRequested();
                if (index != CurrentWorkIndex || _disposed)
                    return;

                SeriesInfo = WorkSeriesInfoViewModel.Create(content, currentNovel.Entry.Series);
                App.AppViewModel.AddBrowseHistory(currentNovel.Entry);
                var markdowns = await Task.Run(() => BuildPageMarkdowns(content), token);
                token.ThrowIfCancellationRequested();
                if (index != CurrentWorkIndex || _disposed)
                    return;

                _pageMarkdowns = markdowns;

                var targetPage = 0;
                if (content.Marker is { Page: > 0 } marker && marker.Page <= markdowns.Count)
                {
                    targetPage = marker.Page - 1;
                }
                else
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var markers = await App.AppViewModel.MakoClient.GetNovelMarkersAsync();
                            var match = markers.MarkedNovels.FirstOrDefault(x => x.Novel.Id == currentNovel.Entry.Id);
                            if (match is { NovelMarker: { Page: > 0 and var page } } && page <= _pageMarkdowns.Count)
                            {
                                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    if (!_disposed && CurrentNovel?.Entry.Id == currentNovel.Entry.Id && CurrentPageIndex == 0)
                                    {
                                        _suppressMarkerSync = true;
                                        CurrentPageIndex = page - 1;
                                        _suppressMarkerSync = false;
                                    }
                                });
                            }
                        }
                        catch
                        {
                            // Silent
                        }
                    });
                }

                _suppressMarkerSync = true;
                CurrentPageIndex = targetPage;
                _suppressMarkerSync = false;
            }
            OnPropertyChanged(nameof(PageCount));
            OnPropertyChanged(nameof(IsMultiPage));
            OnPropertyChanged(nameof(CurrentNovel));
            OnPropertyChanged(nameof(NovelId));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            if (!token.IsCancellationRequested)
                LoadErrorMessage = e.Message;
        }
        finally
        {
            if (!token.IsCancellationRequested)
                IsLoading = false;
        }

        return;

        static List<string> BuildPageMarkdowns(NovelContent content)
        {
            var pages = content.RenderMarkdownPages();
            return pages as List<string> ?? [.. pages];
        }
    }

    private async Task<Novel?> GetCurrentNovelAsync(int index, CancellationToken token)
    {
        if (!_needRefresh)
            return CurrentNovel;

        if (_sourceView is not null && _refreshedNovels.TryGetValue(index, out var cached))
            return cached;

        if (CurrentNovel is not { Entry.Id: var id })
            return null;

        return await LoadNovelAsync(
            id,
            novel =>
            {
                // 单项刷新写回当前项，列表刷新只缓存到当前 Viewer，避免污染原始 SourceView。
                if (_sourceView is null)
                    CurrentNovel = novel;
                else
                    _refreshedNovels[index] = novel;
            },
            token);
    }

    private CancellationToken ResetLoadingToken()
    {
        _loadingCts.Cancel();
        _loadingCts.Dispose();
        _loadingCts = new CancellationTokenSource();
        return _loadingCts.Token;
    }

    private async Task<Novel?> LoadNovelAsync(long id, Action<Novel> onLoaded, CancellationToken token)
    {
        IsLoading = true;
        LoadErrorMessage = null;
        try
        {
            var novel = BlockedContentHelper.Replace(
                await App.AppViewModel.MakoClient.GetNovelFromIdAsync(id, token));
            token.ThrowIfCancellationRequested();
            onLoaded(novel);
            return novel;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception e)
        {
            if (!token.IsCancellationRequested)
                LoadErrorMessage = e.Message;
            return null;
        }
    }


    #region Dispose

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        IsLoading = false;
        _syncMarkerCts?.Cancel();
        _syncMarkerCts?.Dispose();
        _loadingCts.Cancel();
        _loadingCts.Dispose();

        _sourceView?.Dispose();
    }

    #endregion
}
