// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pixeval.Localization;
using Pixeval.Utilities;

namespace Pixeval.ViewModels.Viewers;

public sealed partial class SeriesViewerPageViewModel : ViewModelBase, IDisposable
{
    private readonly CancellationTokenSource _loadingCts = new();

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

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
    public partial IAsyncEnumerable<WorkEntry>? WorksEngine { get; private set; }

    [ObservableProperty]
    public partial bool IsWatchlistAdded { get; private set; }

    private readonly MakoClient _makoClient;
    private IWorkViewViewModel? _worksViewModel;

    public SeriesViewerPageViewModel(SimpleWorkKind workType, long seriesId, MakoClient makoClient)
    {
        _makoClient = makoClient;
        WorkKind = workType;
        Id = seriesId;
        _ = LoadSeriesAsync();
    }

    public SeriesViewerPageViewModel(
        SimpleWorkKind workType,
        long seriesId,
        Series seriesDetail,
        IWorkEntry? firstWork,
        IWorkViewViewModel worksViewModel,
        MakoClient makoClient)
    {
        _makoClient = makoClient;
        WorkKind = workType;
        Id = seriesId;
        FirstWork = firstWork;
        SeriesDetail = seriesDetail;
        _worksViewModel = worksViewModel;
    }

    public SimpleWorkKind WorkKind { get; }

    public long Id { get; }

    public string Header => SeriesDetail?.Title ?? Id.ToString();

    public long AuthorId => SeriesDetail is { User: { } su } ? su.Id : (FirstWork is { } w ? w.User.Id : 0);

    public string? AuthorName => SeriesDetail is { User: { } su } ? su.Name : (FirstWork is { } w ? w.User.Name : null);


    public string? Caption => SeriesDetail?.Caption;

    public string ContentCountText => Strings.SeriesViewerPageWorksCountFormatted(count: SeriesDetail?.PublishedContentCount ?? 0);

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
            var query = await _makoClient.GetWorkSeriesAsync(WorkKind, Id, token);
            var detail = query.Detail();
            var first = query.First()?.AsWorkEntry;
            var engine = query.Engine();

            if (_disposed)
                return;

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

    private bool CanChangeWatchlist => SeriesDetail is not null;

    [RelayCommand(CanExecute = nameof(CanChangeWatchlist))]
    private async Task AddToWatchlistAsync()
    {
        if (!(await _makoClient.SetSeriesWatchlistUnifiedAsync(WorkKind, Id, true, _loadingCts.Token)).Success)
            return;

        if (SeriesDetail is { } s1)
            SeriesDetail = s1 with { WatchlistAdded = true };
        IsWatchlistAdded = true;
    }

    [RelayCommand(CanExecute = nameof(CanChangeWatchlist))]
    private async Task RemoveFromWatchlistAsync()
    {
        if (!(await _makoClient.SetSeriesWatchlistUnifiedAsync(WorkKind, Id, false, _loadingCts.Token)).Success)
            return;

        if (SeriesDetail is { } s2)
            SeriesDetail = s2 with { WatchlistAdded = false };
        IsWatchlistAdded = false;
    }

    #region Dispose

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        IsLoading = false;
        _loadingCts.Cancel();
        _loadingCts.Dispose();
        WorksEngine = null;
        _worksViewModel?.Dispose();
        _worksViewModel = null;
    }

    #endregion
}
