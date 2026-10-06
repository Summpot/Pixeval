// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pixeval.I18N;
using Pixeval.Utilities;

namespace Pixeval.ViewModels.Viewers;

public sealed partial class SeriesViewerPageViewModel : ViewModelBase, IDisposable
{
    private readonly CancellationTokenSource _loadingCts = new();

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial bool IsChangingWatchlist { get; private set; }

    [ObservableProperty]
    public partial string? LoadErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Header))]
    [NotifyPropertyChangedFor(nameof(AuthorId))]
    [NotifyPropertyChangedFor(nameof(AuthorName))]
    [NotifyPropertyChangedFor(nameof(Caption))]
    [NotifyPropertyChangedFor(nameof(ContentCountText))]
    [NotifyPropertyChangedFor(nameof(CoverUrl))]
    public partial Series? SeriesDetail { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoverUrl))]
    public partial IWorkEntry? FirstWork { get; private set; }

    [ObservableProperty]
    public partial IFetchEngine<IWorkEntry>? WorksEngine { get; private set; }

    [ObservableProperty]
    public partial bool IsWatchlistAdded { get; private set; }

    private IWorkViewViewModel? _worksViewModel;

    public SeriesViewerPageViewModel(SimpleWorkType workType, long seriesId)
    {
        WorkType = workType;
        Id = seriesId;
        _ = LoadSeriesAsync();
    }

    public SeriesViewerPageViewModel(
        SimpleWorkType workType,
        long seriesId,
        Series seriesDetail,
        IWorkEntry firstWork,
        IWorkViewViewModel worksViewModel)
    {
        WorkType = workType;
        Id = seriesId;
        FirstWork = firstWork;
        SeriesDetail = seriesDetail;
        _worksViewModel = worksViewModel;
    }

    public SimpleWorkType WorkType { get; }

    public long Id { get; }

    public string Header => SeriesDetail?.Title ?? Id.ToString();

    public long AuthorId => SeriesDetail?.User?.Id ?? FirstWork?.User?.Id ?? 0;

    public string? AuthorName => SeriesDetail?.User?.Name ?? FirstWork?.User?.Name;

    public string? Caption => SeriesDetail?.Caption;

    public string ContentCountText => I18NManager.GetResource(
        SeriesViewerPageResources.WorksCountFormatted,
        SeriesDetail?.PublishedContentCount ?? 0);

    public string? CoverUrl => SeriesDetail?.CoverUrl ?? FirstWork?.GetThumbnailUrl();

    public IWorkViewViewModel? TakeWorksViewModel()
    {
        var viewModel = _worksViewModel;
        _worksViewModel = null;
        return viewModel;
    }

    partial void OnSeriesDetailChanged(Series? value)
    {
        IsWatchlistAdded = false;
        AddToWatchlistCommand.NotifyCanExecuteChanged();
        RemoveFromWatchlistCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadSeriesAsync()
    {
        var token = _loadingCts.Token;
        IsLoading = true;
        LoadErrorMessage = null;
        try
        {
            var (detail, first, engine) = await App.AppViewModel.MakoClient.GetWorkSeriesAsync(WorkType, Id, token);

            if (_disposed || token.IsCancellationRequested || detail.Id != Id)
            {
                engine.EngineHandle.Cancel();
                if (!_disposed && !token.IsCancellationRequested)
                    LoadErrorMessage = I18NManager.GetResource(EntryViewerPageResources.LoadFailed);
                return;
            }

            FirstWork = first;
            SeriesDetail = detail;
            WorksEngine = engine;
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
            if (!token.IsCancellationRequested && !_disposed)
                IsLoading = false;
        }
    }

    private bool CanChangeWatchlist => !_disposed && !IsLoading && !IsChangingWatchlist && SeriesDetail is { Id: > 0 }
                                      && App.AppViewModel.MakoClient.Me is not null;

    [RelayCommand(CanExecute = nameof(CanChangeWatchlist))]
    private Task AddToWatchlistAsync() => SetWatchlistAsync(true);

    [RelayCommand(CanExecute = nameof(CanChangeWatchlist))]
    private Task RemoveFromWatchlistAsync() => SetWatchlistAsync(false);

    private async Task SetWatchlistAsync(bool added)
    {
        if (!CanChangeWatchlist || SeriesDetail is not { } detail)
            return;

        var client = App.AppViewModel.MakoClient;
        var accountId = client.Me?.Id;
        var token = _loadingCts.Token;
        IsChangingWatchlist = true;
        try
        {
            var result = added
                ? await client.PostWorkSeriesWatchlistAsync(WorkType, Id, token)
                : await client.RemoveWorkSeriesWatchlistAsync(WorkType, Id, token);
            if (result && !_disposed && !token.IsCancellationRequested && client.Me?.Id == accountId
                && ReferenceEquals(SeriesDetail, detail))
            {
                detail.WatchlistAdded = added;
                IsWatchlistAdded = added;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        finally
        {
            if (!_disposed)
                IsChangingWatchlist = false;
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
        IsChangingWatchlist = false;
        NotifyWatchlistCommands();
        _loadingCts.Cancel();
        _loadingCts.Dispose();
        var worksEngine = WorksEngine;
        WorksEngine = null;
        worksEngine?.EngineHandle.Cancel();
        _worksViewModel?.Dispose();
        _worksViewModel = null;
    }

    #endregion
}
