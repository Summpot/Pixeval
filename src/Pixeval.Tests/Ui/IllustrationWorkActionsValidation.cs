using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Web;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Models.Options;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Search;
using Pixeval.Views.Viewers;
using Pixeval.Views.Work;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

internal static class IllustrationWorkActionsValidation
{
    internal static async Task RunAsync(string directory)
    {
        using var fixture = new IllustrationReaderFixture();
        await App.AppViewModel.MakoClient.SetTokenAsync("fixture-image-work-actions");
        await App.AppViewModel.MakoClient.ValidateTokenAsync();
        await App.AppViewModel.HistoryPersistHelper.RestoreTask;
        fixture.Http.BookmarkResponseFactory = (_, _) => Task.FromResult(
            "{\"bookmark_detail\":{\"is_bookmarked\":false,\"restrict\":\"private\",\"tags\":[{\"name\":\"原作品 tag\",\"is_registered\":true}]}}");
        var requests = Channel.CreateUnbounded<(string Path, string Body, TaskCompletionSource<string> Response)>();
        List<TaskCompletionSource<string>> pending = [];
        fixture.Http.WorkCardResponseFactory = async (request, _) =>
        {
            var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Add(response);
            await requests.Writer.WriteAsync((request.RequestUri!.AbsolutePath, await request.Content!.ReadAsStringAsync(), response));
            return await response.Task;
        };
        var continuous = System.Globalization.CultureInfo.CurrentUICulture.Name is "en-US" or "ru-RU";
        App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = continuous ? BrowseMode.Continuous : BrowseMode.Swipe;
        using var source = new SimpleOperableSourceView<IllustrationItemViewModel>([
            IllustrationReaderFixture.Sample(996450, 1), IllustrationReaderFixture.Sample(996451, 1)]);
        var model = new IllustrationViewerPageViewModel(source.CloneSourceView(), 0, false);
        var page = new IllustrationViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 1000, Height = 800, Content = host.Shell };
        List<string> report = [];
        try
        {
            window.Show();
            await UntilAsync(() => model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
            await RenderAsync(window);
            var info = page.GetVisualDescendants().OfType<IllustrationViewerInfoPane>().Single();
            if (!info.IsDocked)
                await ActivateAsync(window, info.FindControl<Button>("ToggleInformationButton")!);
            await UntilAsync(() => page.FindControl<FloatingPaneView>("EntryViewerFloatingPaneView")!.DockProgress > 0.999);
            var bar = info.FindControl<CommandBar>("BottomActionBar")!;
            var original = model.CurrentIllustration!;
            var bookmark = bar.PrimaryCommands.OfType<CommandBarButton>().Single(b => ReferenceEquals(b.Command, original.BookmarkCommand));
            var watch = bar.PrimaryCommands.OfType<CommandBarButton>().Single(b => ReferenceEquals(b.Command, original.AddToWatchLaterCommand));

            await ActivateAsync(window, bookmark);
            var add = await NextAsync();
            Require(add.Path == "/v2/illust/bookmark/add" && HttpUtility.ParseQueryString(add.Body)["illust_id"] == original.Id,
                "Information bookmark targets the displayed work exactly once");
            Require((original.IsBookmarkedDisplay & HeartButtonState.Pending) != 0 && !bookmark.IsEffectivelyEnabled,
                "Pending bookmark disables the real information action");
            add.Response.SetResult("{}");
            await UntilAsync(() => !original.BookmarkCommand.IsRunning);
            Require(original.Entry.IsFavorite && original.IsBookmarkedDisplay == HeartButtonState.Checked, "Successful add updates the same work and icon state");
            await ActivateAsync(window, bookmark);
            var remove = await NextAsync();
            Require(remove.Path == "/v1/illust/bookmark/delete", "The same information command removes an existing bookmark");
            remove.Response.SetResult("{}");
            await UntilAsync(() => !original.BookmarkCommand.IsRunning);
            Require(!original.Entry.IsFavorite, "Removal restores the unbookmarked work");
            await ActivateAsync(window, watch);
            Require(original.IsInWatchLater && App.AppViewModel.HistoryPersistHelper.ContainsWatchLater(original.Entry), "Watch-later action persists the displayed work");
            await ActivateAsync(window, watch);
            Require(!original.IsInWatchLater && !App.AppViewModel.HistoryPersistHelper.ContainsWatchLater(original.Entry), "Repeating watch-later removes only that work");
            report.Add("PASS information add/remove, pending state, watch-later database identity");

            // Auto-play or a source selection can advance the work while its popup is open.
            var selector = await OpenTagsAsync();
            Require(selector.WorkId.ToString() == original.Id && selector.IsPrivate, "The tag popup loads the original work and privacy");
            model.NextWorkCommand.Execute(null);
            await UntilAsync(() => model.CurrentWorkIndex == 1 && model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
            await RenderAsync(window);
            Require(TopLevel.GetTopLevel(selector) is null && !requests.Reader.TryPeek(out _),
                "Changing the displayed work dismisses its old tag popup without writing to either work");
            Require(ReferenceEquals(bookmark.Command, model.CurrentIllustration!.BookmarkCommand)
                && ReferenceEquals(watch.Command, model.CurrentIllustration.AddToWatchLaterCommand), "Information actions rebind to the next work");
            window.Width = 360;
            window.RequestedThemeVariant = ThemeVariant.Dark;
            await RenderAsync(window);
            await CheckLinksAsync();
            model.PrevWorkCommand.Execute(null);
            await UntilAsync(() => model.CurrentWorkIndex == 0 && model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
            window.Width = 360;
            window.RequestedThemeVariant = ThemeVariant.Dark;
            await RenderAsync(window);
            await CheckLinksAsync();
            selector = await OpenTagsAsync();
            await ActivateAsync(window, selector.FindControl<Button>("ConfirmButton")!);
            var tagged = await NextAsync();
            var payload = HttpUtility.ParseQueryString(tagged.Body);
            Require(payload["illust_id"] == original.Id && payload["restrict"] == "private" && payload["tags[]"] == "原作品 tag",
                "Tag submission preserves the original ID, privacy and tag payload: " + tagged.Body);
            Require(!selector.IsEnabled, "An in-flight tag submission cannot be repeated");
            Save(window, directory, "work-actions-private-tags-pending-dark-narrow");
            host.Shell.NavigateTo(new SearchPage());
            await RenderAsync(window);
            Require(TopLevel.GetTopLevel(selector) is null, "Temporary navigation closes the original viewer's popup");
            host.CloseDestinationAndReturn();
            await RenderAsync(window);
            tagged.Response.SetResult("{}");
            await UntilAsync(() => !original.AddToBookmarkCommand.IsRunning);
            Require(original.Entry.IsFavorite && !host.Notifications.Any(n => n.Message?.ToString() == I18NManager.GetResource(MiscResources.AddedToBookmark)),
                "A committed request updates its original work but cannot notify a later viewer lifetime");
            report.Add("PASS changed-work dismissal, exact private tags, navigation/return and late result ownership");

            var loading = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Add(loading);
            fixture.Http.BookmarkResponseFactory = (_, _) => loading.Task;
            selector = await OpenTagsAsync(waitForLoad: false);
            host.Shell.ClosePage(page);
            await RenderAsync(window);
            Require(TopLevel.GetTopLevel(selector) is null && model.CurrentImage is null, "Closing the actual tab dismisses even a loading tag selector");
            loading.SetResult("{\"bookmark_detail\":{\"is_bookmarked\":false,\"restrict\":\"public\",\"tags\":[]}}");
            await RenderAsync(window);
            Require(TopLevel.GetTopLevel(selector) is null && !requests.Reader.TryPeek(out _), "A late tag read cannot reopen or submit from the closed viewer");
            report.Add("PASS real tab close during tag load, no late popup or submission");

            async Task CheckLinksAsync()
            {
                var current = model.CurrentIllustration!;
                var copy = bar.PrimaryCommands.OfType<CommandBarButton>().Single(b => b.Label == I18NManager.GetResource(EntryItemResources.GenerateWebLink));
                var browser = bar.PrimaryCommands.OfType<CommandBarButton>().Single(b => b.Label == I18NManager.GetResource(EntryItemResources.OpenInWebBrowser));
                if (copy.IsInOverflow) await OpenOverflowAsync(window, bar);
                Require(Equals(copy.Tag, current.WebsiteUri), "The overflow web-link binding follows the displayed illustration");
                await ActivateAsync(window, copy);
                Require(await window.Clipboard!.TryGetTextAsync() == current.WebsiteUri.OriginalString,
                    "The actual copy action sends the displayed illustration URI to the headless clipboard");
                if (browser.IsInOverflow) await OpenOverflowAsync(window, bar);
                Require(Equals(browser.Tag, current.WebsiteUri), "The overflow browser binding follows the displayed illustration");
                await ActivateAsync(window, browser);
                Require(host.Navigations == 0, "The headless external-browser action does not replace the reader");
                report.Add("PASS narrow web link copy and browser URI after work change: " + current.Id);
            }

            async Task<TagSelector> OpenTagsAsync(bool waitForLoad = true)
            {
                if (bookmark.IsInOverflow) await OpenOverflowAsync(window, bar);
                Require(bookmark.Focus(NavigationMethod.Tab), "Bookmark tag entry is keyboard reachable");
                Press(window, Key.Apps, PhysicalKey.ContextMenu);
                await RenderAsync(window);
                var tags = window.GetVisualDescendants().OfType<TagSelector>().Single();
                if (waitForLoad) await UntilAsync(() => !tags.IsLoading);
                return tags;
            }
        }
        finally
        {
            foreach (var response in pending) response.TrySetResult("{}");
            File.WriteAllLines(Path.Combine(directory, "illustration-work-actions-contracts.txt"), report);
            window.Close();
        }
        Require(fixture.Http.UnexpectedRequests.IsEmpty, "Work actions remain within the offline transport");
        Task<(string Path, string Body, TaskCompletionSource<string> Response)> NextAsync() => requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
    }
}
