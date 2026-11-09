using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Web;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Pixeval.I18N;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Search;
using Pixeval.Views.Viewers;
using Pixeval.Views.Work;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

internal static class NovelBookmarkLifetimeValidation
{
    internal static async Task RunAsync(string directory)
    {
        using var fixture = new NovelReaderFixture();
        await App.AppViewModel.MakoClient.SetTokenAsync("fixture-novel-bookmark-lifetime");
        await App.AppViewModel.MakoClient.ValidateTokenAsync();
        var content = fixture.Http.WorkCardResponseFactory!;
        var requests = Channel.CreateUnbounded<(string Path, string Body, TaskCompletionSource<string> Response)>();
        List<TaskCompletionSource<string>> pending = [];
        fixture.Http.WorkCardResponseFactory = async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == "/webview/v2/novel") return await content(request, token);
            var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Add(response);
            await requests.Writer.WriteAsync((request.RequestUri.AbsolutePath, await request.Content!.ReadAsStringAsync(), response));
            return await response.Task;
        };
        fixture.Http.BookmarkResponseFactory = (_, _) => Task.FromResult(
            "{\"bookmark_detail\":{\"is_bookmarked\":false,\"restrict\":\"private\",\"tags\":[{\"name\":\"原小说 tag\",\"is_registered\":true}]}}");
        using var source = new SimpleOperableSourceView<NovelItemViewModel>([
            NovelReaderFixture.Sample(993140), NovelReaderFixture.Sample(993141)]);
        var model = new NovelViewerPageViewModel(source.CloneSourceView(), 0, false);
        var page = new NovelViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 1000, Height = 800, Content = host.Shell };
        List<string> report = [];
        try
        {
            window.Show();
            (await fixture.NextAsync()).Complete("Original novel");
            await UntilAsync(() => !model.IsLoading);
            await RenderAsync(window);
            var original = model.CurrentNovel!;
            var bar = page.FindControl<CommandBar>("ReaderCommands")!;
            var bookmark = bar.PrimaryCommands.OfType<CommandBarButton>().Single(b => ReferenceEquals(b.Command, original.BookmarkCommand));
            var tags = await OpenTagsAsync();
            Require(tags.WorkId == 993140 && tags.IsPrivate, "The popup starts with the displayed novel and privacy");
            model.NextWorkCommand.Execute(null);
            (await fixture.NextAsync()).Complete("Next novel");
            await UntilAsync(() => !model.IsLoading);
            await RenderAsync(window);
            Require(TopLevel.GetTopLevel(tags) is null && !requests.Reader.TryPeek(out _),
                "Changing novels dismisses the previous bookmark popup without submitting its tags to the next novel");
            Require(ReferenceEquals(bookmark.Command, model.CurrentNovel!.BookmarkCommand), "The toolbar follows the next novel");
            model.PrevWorkCommand.Execute(null);
            await UntilAsync(() => !model.IsLoading && model.NovelId == 993140);
            window.Width = 360;
            window.RequestedThemeVariant = ThemeVariant.Dark;
            await RenderAsync(window);
            tags = await OpenTagsAsync();
            await ActivateAsync(window, tags.FindControl<Button>("ConfirmButton")!);
            var request = await requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            var payload = HttpUtility.ParseQueryString(request.Body);
            Require(request.Path == "/v2/novel/bookmark/add" && payload["novel_id"] == original.Id
                && payload["restrict"] == "private" && payload["tags[]"] == "原小说 tag" && !tags.IsEnabled,
                "Pending tag submission keeps the original novel, privacy and tags and cannot be repeated");
            Save(window, directory, "novel-bookmark-pending-dark-narrow");
            host.Shell.NavigateTo(new SearchPage());
            await RenderAsync(window);
            Require(TopLevel.GetTopLevel(tags) is null, "Leaving the novel closes its pending tag popup");
            host.CloseDestinationAndReturn();
            await RenderAsync(window);
            request.Response.SetResult("{}");
            await UntilAsync(() => !original.AddToBookmarkCommand.IsRunning);
            Require(original.IsFavorite && !host.Notifications.Any(n => n.Message?.ToString() == I18NManager.GetResource(MiscResources.AddedToBookmark)),
                "A completed request updates its original novel without notifying a later reader lifetime");
            report.Add("PASS changed-novel dismissal, exact private payload, temporary navigation/return and late submission ownership");

            var loading = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Add(loading);
            CancellationToken loadingToken = default;
            fixture.Http.BookmarkResponseFactory = (_, token) => { loadingToken = token; return loading.Task; };
            tags = await OpenTagsAsync(false);
            Require(tags.IsLoading, "The final popup is still awaiting its bookmark detail");
            host.Shell.ClosePage(page);
            await RenderAsync(window);
            Require(loadingToken.IsCancellationRequested && TopLevel.GetTopLevel(tags) is null,
                "Actual reader close cancels the popup read and removes its input route");
            loading.SetResult("{\"bookmark_detail\":{\"is_bookmarked\":false,\"restrict\":\"public\",\"tags\":[]}}");
            await RenderAsync(window);
            Require(TopLevel.GetTopLevel(tags) is null && !requests.Reader.TryPeek(out _), "Late detail cannot reopen or submit from a closed reader");
            report.Add("PASS close during loading, original token cancellation and late result isolation");

            async Task<TagSelector> OpenTagsAsync(bool wait = true)
            {
                if (bookmark.IsInOverflow) await OpenOverflowAsync(window, bar);
                Require(bookmark.Focus(NavigationMethod.Tab), "Novel bookmark has a keyboard route");
                Press(window, Key.Apps, PhysicalKey.ContextMenu);
                await RenderAsync(window);
                var selector = window.GetVisualDescendants().OfType<TagSelector>().Single();
                if (wait) await UntilAsync(() => !selector.IsLoading);
                return selector;
            }
        }
        finally
        {
            foreach (var response in pending) response.TrySetResult("{}");
            File.WriteAllLines(Path.Combine(directory, "novel-bookmark-lifetime-contracts.txt"), report);
            window.Close();
        }
        Require(fixture.Http.UnexpectedRequests.IsEmpty, "Novel bookmark ownership uses only isolated transport");
    }
}
