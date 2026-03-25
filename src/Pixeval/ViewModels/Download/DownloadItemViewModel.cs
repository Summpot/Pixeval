// Copyright (c) Pixeval.
// Licensed under the GPL v3 License.

using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Media;
using FluentIcons.Common;
using Misaki;
using Pixeval.Controls;
using Pixeval.Download;
using Pixeval.I18N;
using Pixeval.Models.Database;
using Pixeval.Models.Download.Tasks;
using Pixeval.ViewModels.WorkDetails;

namespace Pixeval.ViewModels.Download;

public sealed class DownloadItemViewModel : ThumbnailEntryViewModel<IArtworkInfo>, IFactory<IDownloadTaskGroupBase, DownloadItemViewModel>
{
    public DownloadItemViewModel(IDownloadTaskGroupBase downloadTaskBase) : base(((IDownloadTaskGroup)downloadTaskBase).DatabaseEntry.Entry)
    {
        DownloadTask = (IDownloadTaskGroup)downloadTaskBase;
        DownloadTask.PropertyChanged += DownloadTaskOnPropertyChanged;
    }

    public IDownloadTaskGroup DownloadTask { get; }

    public static DownloadItemViewModel CreateInstance(IDownloadTaskGroupBase entry) => new(entry);

    public string Title => Entry.Title;

    public string AuthorText => string.Join(", ", Entry.Authors
        .Select(static author => author.Name)
        .Where(static name => !string.IsNullOrWhiteSpace(name)));

    public string SubtitleText => string.IsNullOrWhiteSpace(AuthorText)
        ? $"ID {Entry.Id}"
        : $"{AuthorText} · ID {Entry.Id}";

    public string MetaText => $"{KindText} · {Entry.CreateDate.ToLocalTime():g}";

    public string KindText => DownloadTask.DatabaseEntry.Type switch
    {
        DownloadItemType.Manga => "漫画",
        DownloadItemType.Ugoira => "动图",
        DownloadItemType.Novel => "小说",
        _ => "插画"
    };

    public DownloadState CurrentState => DownloadTask.CurrentState;

    public double ProgressPercentage => DownloadTask.ProgressPercentage;

    public bool IsGroupTask => DownloadTask is DownloadTaskGroup;

    public int ActiveCount => DownloadTask is DownloadTaskGroup group ? group.ActiveCount : 0;

    public int CompletedCount => DownloadTask is DownloadTaskGroup group ? group.CompletedCount : 0;

    public int ErrorCount => DownloadTask is DownloadTaskGroup group ? group.ErrorCount : 0;

    public bool ShowActiveCount => IsGroupTask && ActiveCount > 0;

    public bool ShowCompletedCount => IsGroupTask && CompletedCount > 0;

    public bool ShowErrorCount => IsGroupTask && ErrorCount > 0;

    public bool HasError => CurrentState is DownloadState.Error;

    public bool IsPending => CurrentState is DownloadState.Pending;

    public bool IsPaused => CurrentState is DownloadState.Paused;

    public bool IsCompleted => CurrentState is DownloadState.Completed;

    public bool IsRetryEnabled => !DownloadTask.IsProcessing && CurrentState is DownloadState.Completed or DownloadState.Error;

    public bool IsCancelEnabled => !DownloadTask.IsProcessing && CurrentState is DownloadState.Running or DownloadState.Queued or DownloadState.Paused;

    public bool IsPrimaryActionEnabled => !DownloadTask.IsProcessing || CurrentState is DownloadState.Completed;

    public bool CanOpenDownloadedTarget => File.Exists(DownloadTask.OpenLocalDestination) || Directory.Exists(DownloadTask.OpenLocalDestination);

    public bool CanOpenFolder => !string.IsNullOrWhiteSpace(FolderPath) && Directory.Exists(FolderPath);

    public bool CanNavigateToWorkDetails => WorkId > 0;

    public string FolderPath => Path.GetDirectoryName(DownloadTask.OpenLocalDestination)
        ?? Path.GetDirectoryName(DownloadTask.Destination)
        ?? string.Empty;

    public string DestinationPath => DownloadTask.OpenLocalDestination;

    public string StatusText => CurrentState switch
    {
        DownloadState.Queued => I18NManager.GetResource(DownloadItemResources.DownloadQueued),
        DownloadState.Running => I18NManager.GetResource(DownloadItemResources.DownloadRunningFormatted, (int)ProgressPercentage),
        DownloadState.Error => I18NManager.GetResource(DownloadItemResources.DownloadErrorMessageFormatted, DownloadTask.ErrorCause?.Message ?? "Unknown error"),
        DownloadState.Completed => I18NManager.GetResource(DownloadItemResources.DownloadCompleted),
        DownloadState.Cancelled => I18NManager.GetResource(DownloadItemResources.DownloadCancelled),
        DownloadState.Pending => I18NManager.GetResource(DownloadItemResources.DownloadPending),
        DownloadState.Paused => I18NManager.GetResource(DownloadItemResources.DownloadPaused),
        _ => throw new ArgumentOutOfRangeException(nameof(CurrentState), CurrentState, null)
    };

    public string StateBadgeText => CurrentState switch
    {
        DownloadState.Queued => "队列中",
        DownloadState.Running => "下载中",
        DownloadState.Error => "失败",
        DownloadState.Completed => "已完成",
        DownloadState.Cancelled => "已取消",
        DownloadState.Pending => "处理中",
        DownloadState.Paused => "已暂停",
        _ => throw new ArgumentOutOfRangeException(nameof(CurrentState), CurrentState, null)
    };

    public Symbol PrimaryActionSymbol => CurrentState switch
    {
        DownloadState.Pending => Symbol.Dismiss,
        DownloadState.Queued or DownloadState.Running => Symbol.Pause,
        DownloadState.Cancelled or DownloadState.Error => Symbol.ArrowRepeatAll,
        DownloadState.Completed => Symbol.Open,
        DownloadState.Paused => Symbol.Play,
        _ => throw new ArgumentOutOfRangeException(nameof(CurrentState), CurrentState, null)
    };

    public string PrimaryActionText => PrimaryActionSymbol switch
    {
        Symbol.Dismiss => I18NManager.GetResource(DownloadItemResources.ActionDownloadCancelled),
        Symbol.Pause => I18NManager.GetResource(DownloadItemResources.ActionButtonContentPause),
        Symbol.ArrowRepeatAll => I18NManager.GetResource(DownloadItemResources.ActionButtonContentRetry),
        Symbol.Open => I18NManager.GetResource(DownloadItemResources.ActionButtonContentOpen),
        Symbol.Play => I18NManager.GetResource(DownloadItemResources.ActionButtonContentResume),
        _ => throw new ArgumentOutOfRangeException(nameof(PrimaryActionSymbol), PrimaryActionSymbol, null)
    };

    public IBrush StateBrush => CurrentState switch
    {
        DownloadState.Completed => Brushes.MediumSeaGreen,
        DownloadState.Error => Brushes.IndianRed,
        DownloadState.Cancelled => Brushes.DarkGray,
        DownloadState.Paused => Brushes.Goldenrod,
        DownloadState.Pending => Brushes.MediumPurple,
        DownloadState.Running => Brushes.DodgerBlue,
        DownloadState.Queued => Brushes.CornflowerBlue,
        _ => Brushes.Gray
    };

    public string ErrorMessage => DownloadTask.ErrorCause?.ToString() ?? string.Empty;

    public long WorkId => long.TryParse(Convert.ToString(Entry.Id, CultureInfo.InvariantCulture), out var id) ? id : 0;

    public WorkDetailsKind WorkKind => DownloadTask.DatabaseEntry.Type switch
    {
        DownloadItemType.Novel => WorkDetailsKind.Novel,
        DownloadItemType.Manga => WorkDetailsKind.Manga,
        _ => WorkDetailsKind.Illustration
    };

    public WorkDetailsNavigationParameter NavigationParameter => new(WorkId, WorkKind);

    public override Uri AppUri => Entry.AppUri;

    public override Uri WebsiteUri => Entry.WebsiteUri;

    protected override string ThumbnailUrl => Entry.Thumbnails.PickClosestHeight(240)?.ImageUri.OriginalString
        ?? Entry.Thumbnails.PickMax()?.ImageUri.OriginalString
        ?? string.Empty;

    protected override void DisposeOverride()
    {
        DownloadTask.PropertyChanged -= DownloadTaskOnPropertyChanged;
        base.DisposeOverride();
    }

    private void DownloadTaskOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = sender;
        switch (e.PropertyName)
        {
            case nameof(IDownloadTaskBase.CurrentState):
            case nameof(IDownloadTaskBase.ProgressPercentage):
            case nameof(IDownloadTaskBase.ErrorCause):
            case nameof(IDownloadTaskBase.IsProcessing):
            case nameof(DownloadTaskGroup.IsPending):
                RaiseDownloadStateChanged();
                break;
        }
    }

    private void RaiseDownloadStateChanged()
    {
        OnPropertyChanged(nameof(CurrentState));
        OnPropertyChanged(nameof(ProgressPercentage));
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(ShowActiveCount));
        OnPropertyChanged(nameof(ShowCompletedCount));
        OnPropertyChanged(nameof(ShowErrorCount));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(IsRetryEnabled));
        OnPropertyChanged(nameof(IsCancelEnabled));
        OnPropertyChanged(nameof(IsPrimaryActionEnabled));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StateBadgeText));
        OnPropertyChanged(nameof(PrimaryActionSymbol));
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(StateBrush));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(CanOpenDownloadedTarget));
        OnPropertyChanged(nameof(CanOpenFolder));
    }
}
