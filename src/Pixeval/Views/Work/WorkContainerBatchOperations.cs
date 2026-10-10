// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Models;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Booru;
using Pixeval.Native.Mako;
using Pixeval.Services;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.ViewContainers;

namespace Pixeval.Views.Work;

public static class WorkContainerBatchOperations
{
    public static async Task AddAllToBookmarkAsync(
        Control sourceControl,
        IOperableViewViewModel? viewModel,
        object? singleTarget = null)
    {
        await AddToBookmarkAsync(sourceControl, viewModel, singleTarget, (false, null));
    }

    public static async Task RequestAddToBookmarkWithTagSelectorAsync(
        Control placementTarget,
        IOperableViewViewModel? viewModel,
        object? target)
    {
        if (target is null && viewModel is not { SelectedEntries.Count: > 0 })
            return;

        var id = target switch
        {
            Illustration illust => illust.Id,
            Novel novel => novel.Id,
            BooruPost booru => long.TryParse(booru.Id, out var idLong) ? idLong : 0,
            _ => 0
        };

        var type = target is Novel || viewModel is NovelViewViewModel or SimpleOperableViewViewModel<Novel>
            ? SimpleWorkType.Novel
            : SimpleWorkType.Illustration;

        await BookmarkTagSelectorFlyoutHelper.ShowAsync(
            placementTarget,
            type,
            id,
            async e => await AddToBookmarkAsync(placementTarget, viewModel, target, e),
            PlacementMode.Bottom);
    }

    public static async Task AddToBookmarkAsync(
        Control sourceControl,
        IOperableViewViewModel? viewModel,
        object? target,
        (bool IsPrivate, IReadOnlyList<string>? Tags) options)
    {
        var viewContainer = TopLevel.GetTopLevel(sourceControl)?.ViewContainer;

        if (target is IWorkEntry workTarget)
        {
            await WorkCommands.AddToBookmarkCommand.ExecuteAsync(new BookmarkRequest(workTarget, options.IsPrivate, options.Tags));
            viewContainer?.ShowSuccess(I18NManager.GetResource(MiscResources.AddedToBookmark));
            return;
        }

        if (viewModel is null)
            return;

        if (viewContainer is not null
            && viewModel.SelectedEntries.Count >= 20
            && await viewContainer.CreateOkCancelAsync(
                I18NManager.GetResource(WorkContainerResources.SelectedTooManyItems.ForBookmarkTitle),
                I18NManager.GetResource(WorkContainerResources.SelectedTooManyItems.Content)) is not ContentDialogResult.Primary)
            return;

        foreach (var i in viewModel.SelectedEntries)
        {
            if (i is IWorkEntry work)
                await WorkCommands.AddToBookmarkCommand.ExecuteAsync(new BookmarkRequest(work, options.IsPrivate, options.Tags));
        }

        if (viewModel.SelectedEntries.Count is var c and > 0)
            viewContainer?.ShowSuccess(I18NManager.GetResource(WorkContainerResources.AddedAllToBookmarkContentFormatted, c));
    }

    public static async Task SaveAllAsync(Control sourceControl, IOperableViewViewModel? viewModel)
    {
        if (viewModel is null)
            return;

        var viewContainer = TopLevel.GetTopLevel(sourceControl)?.ViewContainer;
        if (viewContainer is not null
            && viewModel.SelectedEntries.Count >= 20
            && await viewContainer.CreateOkCancelAsync(
                I18NManager.GetResource(WorkContainerResources.SelectedTooManyItems.ForSaveTitle),
                I18NManager.GetResource(WorkContainerResources.SelectedTooManyItems.Content)) is not ContentDialogResult.Primary)
            return;

        foreach (var i in viewModel.SelectedEntries)
            WorkCommands.SaveCommand.Execute(i);

        viewContainer?.ShowInformation(
            I18NManager.GetResource(WorkContainerResources.DownloadItemsQueuedFormatted, viewModel.SelectedEntries.Count));
    }

    public static async Task OpenAllInBrowserAsync(Control sourceControl, IOperableViewViewModel? viewModel)
    {
        if (viewModel is null)
            return;

        var topLevel = TopLevel.GetTopLevel(sourceControl);
        if (topLevel?.ViewContainer is { } viewContainer
            && viewModel.SelectedEntries.Count > 15
            && await viewContainer.CreateOkCancelAsync(
                I18NManager.GetResource(WorkContainerResources.SelectedTooManyItems.ForOpenInBrowser.Title),
                I18NManager.GetResource(WorkContainerResources.SelectedTooManyItems.ForOpenInBrowser.Content)) is not ContentDialogResult.Primary)
            return;

        foreach (var selectedEntry in viewModel.SelectedEntries)
        {
            var uri = selectedEntry switch
            {
                Illustration illust => illust.WebsiteUri,
                Novel novel => novel.WebsiteUri,
                BooruPost booru => booru.WebsiteUri,
                _ => null
            };
            if (uri is not null && topLevel is not null)
                _ = await topLevel.Launcher.LaunchUriAsync(uri);
        }
    }
}
