using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Mako.Global.Enum;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Download;
using Pixeval.Extensions.Common;
using Pixeval.Extensions.Common.FormatProviders;
using Pixeval.I18N;
using Pixeval.Models.Download;
using Pixeval.Models.Download.Tasks;
using Pixeval.Models.Extensions;
using Pixeval.Models.Options;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;
using Pixeval.Views.Work;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

internal static class NovelActionsValidation
{
    private const string SourceText = "[chapter:Export chapter]\n[[rb:漢字 > かんじ]] [b:Bold]\n[uploadedimage:11]\n[pixivimage:994101-1]\n[newpage]Second export page 中文 🌸";

    internal static async Task RunNativeAsync(string directory)
    {
        using var fixture = new NovelReaderFixture();
        await App.AppViewModel.MakoClient.SetTokenAsync("fixture-native-export");
        await App.AppViewModel.MakoClient.ValidateTokenAsync();
        var history = App.AppViewModel.HistoryPersistHelper;
        await history.RestoreTask;
        var extension = App.AppViewModel.AppServiceProvider.GetRequiredService<ExtensionService>().ActiveNovelFormatProviders.Single();
        var format = NovelDownloadFormatToken.Extension(extension);
        App.AppViewModel.AppSettings.DownloadSettings.DownloadFormats.NovelDownloadFormat = format;
        App.AppViewModel.AppSettings.DownloadSettings.DownloadPathMacro = Path.Combine(directory, "native-export", "@{id}.@{ext}");
        var item = NovelItemViewModel.CreateInstance(NovelReaderFixture.Sample(993101));
        var model = new NovelViewerPageViewModel(item, false);
        var page = new NovelViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 1100, Height = 800, Content = host.Shell };
        try
        {
            window.Show();
            (await fixture.NextAsync()).Complete(SourceText, withImages: true);
            await UntilAsync(() => !model.IsLoading);
            await RenderAsync(window);
            var save = page.FindControl<CommandBar>("ReaderCommands")!.PrimaryCommands.OfType<CommandBarButton>()
                .Single(button => ReferenceEquals(button.Command, item.SaveCommand));
            await ActivateAsync(window, save);
            await UntilAsync(() => history.DownloadManager.QueuedTasks.Count == 1);
            var task = (NovelDownloadTaskGroup) history.DownloadManager.QueuedTasks.Single();
            await UntilAsync(() => task.CurrentState is DownloadState.Completed or DownloadState.Error);
            var paths = NovelDownloadTaskGroup.GetOutputPaths(task.TokenizedDestination, format);
            // The pinned official C++ demo intentionally completes without writing a file.
            // Exercise its real ABI and require the consumer to reject this incomplete output.
            Require(task.CurrentState == DownloadState.Error && !File.Exists(paths.NovelFile)
                && task.ErrorMessage is { } error && error.Contains(nameof(FileNotFoundException)) && error.Contains(Path.GetFileName(paths.NovelFile)),
                "An installed native provider returning without an output file cannot report a completed export");
            File.WriteAllLines(Path.Combine(directory, "novel-native-export-contracts.txt"),
                [$"PASS pinned C++ provider: format={format.Value}; state={task.CurrentState}; missing output rejected", task.ErrorMessage!]);
            Require(fixture.Http.UnexpectedRequests.IsEmpty, "Native export uses only the isolated image/content transport");
            host.Shell.ClosePage(page);
        }
        finally { window.Close(); }
    }

    internal static async Task RunAsync(string directory)
    {
        using var fixture = new NovelReaderFixture();
        await App.AppViewModel.MakoClient.SetTokenAsync("fixture-novel-actions");
        await App.AppViewModel.MakoClient.ValidateTokenAsync();
        var history = App.AppViewModel.HistoryPersistHelper;
        await history.RestoreTask;
        var manager = history.DownloadManager;
        var contentHandler = fixture.Http.WorkCardResponseFactory!;
        List<(Uri Uri, string Body)> bookmarks = [];
        fixture.Http.WorkCardResponseFactory = async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == "/webview/v2/novel") return await contentHandler(request, token);
            bookmarks.Add((request.RequestUri, await request.Content!.ReadAsStringAsync(token)));
            return "{}";
        };
        fixture.Http.BookmarkResponseFactory = (_, _) => Task.FromResult("{\"bookmark_detail\":{\"is_bookmarked\":false,\"restrict\":\"public\",\"tags\":[{\"name\":\"固定标签\",\"is_registered\":true}]}}");
        var item = NovelItemViewModel.CreateInstance(NovelReaderFixture.Sample(993001));
        var model = new NovelViewerPageViewModel(item, false);
        var page = new NovelViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 1100, Height = 850, Content = host.Shell };
        List<string> report = [];
        try
        {
            window.Show();
            var initial = await fixture.NextAsync();
            await RenderAsync(window);
            var bar = page.FindControl<CommandBar>("ReaderCommands")!;
            var save = bar.PrimaryCommands.OfType<CommandBarButton>().Single(button => ReferenceEquals(button.Command, item.SaveCommand));
            var failedSave = item.SaveCommand.ExecuteAsync(save);
            initial.Fail();
            await failedSave;
            await UntilAsync(() => !model.IsLoading);
            await RenderAsync(window);
            Require(manager.QueuedTasks.Count == 0 && host.Notifications.Any(notification => notification.Type == NotificationType.Error),
                "A content failure shows a save error without queuing a fake download or escaping the async command");
            await ActivateAsync(window, page.FindControl<CommandBarButton>("ReloadButton")!);
            (await fixture.NextAsync()).Complete(SourceText, withImages: true);
            await UntilAsync(() => !model.IsLoading);
            Require(model.LoadErrorMessage is null, "The same novel recovers after a failed save/content load");

            var bookmark = bar.PrimaryCommands.OfType<CommandBarButton>().Single(button => ReferenceEquals(button.Command, item.BookmarkCommand));
            await ActivateAsync(window, bookmark);
            await UntilAsync(() => !item.BookmarkCommand.IsRunning);
            Require(bookmarks.Count == 1 && bookmarks[0].Uri.AbsolutePath == "/v2/novel/bookmark/add"
                && HttpUtility.ParseQueryString(bookmarks[0].Body)["novel_id"] == "993001" && item.IsFavorite,
                "The reader bookmark action sends one original novel ID/type request");
            await ActivateAsync(window, bookmark);
            await UntilAsync(() => !item.BookmarkCommand.IsRunning);
            Require(bookmarks.Count == 2 && bookmarks[^1].Uri.AbsolutePath == "/v1/novel/bookmark/delete" && !item.IsFavorite,
                "Bookmark removal preserves the original endpoint and displayed state");
            Require(bookmark.Focus(), "Bookmark options retain a keyboard route");
            Press(window, Key.Apps, PhysicalKey.ContextMenu);
            await UntilAsync(() => window.GetVisualDescendants().OfType<TagSelector>().Any(selector => !selector.IsLoading));
            var tags = window.GetVisualDescendants().OfType<TagSelector>().Single();
            Require(tags.WorkId == 993001 && tags.WorkType == SimpleWorkType.Novel && tags.SelectedTags.Single().Name == "固定标签", "Reader bookmark options retain type, ID and selected tags");
            tags.IsPrivate = true;
            await ActivateAsync(window, tags.FindControl<Button>("ConfirmButton")!);
            await UntilAsync(() => !item.AddToBookmarkCommand.IsRunning);
            var payload = HttpUtility.ParseQueryString(bookmarks[^1].Body);
            Require(bookmarks.Count == 3 && payload["restrict"] == "private" && payload["tags[]"] == "固定标签", $"The actual tag editor submits the original private/tag payload once: count={bookmarks.Count}, body={bookmarks[^1].Body}");

            var watch = bar.PrimaryCommands.OfType<CommandBarButton>().Single(button => ReferenceEquals(button.Command, item.AddToWatchLaterCommand));
            var copy = bar.PrimaryCommands.OfType<CommandBarButton>().Single(button => button.Label == I18NManager.GetResource(EntryItemResources.GenerateWebLink));
            var browser = bar.PrimaryCommands.OfType<CommandBarButton>().Single(button => button.Label == I18NManager.GetResource(EntryItemResources.OpenInWebBrowser));
            window.Width = 360;
            window.RequestedThemeVariant = ThemeVariant.Dark;
            await RenderAsync(window);
            await ActivateAsync(window, watch);
            Require(history.ContainsWatchLater(item.Entry) && item.IsInWatchLater, "The narrow overflow action persists watch-later");
            await ActivateAsync(window, watch);
            Require(!history.ContainsWatchLater(item.Entry) && !item.IsInWatchLater, "The same overflow action removes watch-later");
            await OpenOverflowAsync(window, bar);
            Require(Equals(copy.Tag, item.WebsiteUri), "The reader web link binding preserves its original URI");
            await ActivateAsync(window, copy);
            Require(await window.Clipboard!.TryGetTextAsync() == item.WebsiteUri.OriginalString, "Reader link copy uses the in-memory headless clipboard");
            await OpenOverflowAsync(window, bar);
            Require(Equals(browser.Tag, item.WebsiteUri), "Browser action preserves its URI and remains enabled in narrow overflow");
            await ActivateAsync(window, browser);
            Require(host.Navigations == 0, "External browser action does not navigate the application shell");
            report.Add("PASS reader actions: bookmark add/remove/private tags, narrow overflow watch-later persistence, link copy and browser URI binding (headless launcher)");

            foreach (var format in new[] { NovelDownloadFormat.OriginalTxt, NovelDownloadFormat.Html, NovelDownloadFormat.Md })
            {
                var task = await SaveAsync(format.ToString(), format.ToString());
                Require(task.CurrentState == DownloadState.Completed, "Built-in novel export completes: " + task.ErrorMessage);
                var paths = NovelDownloadTaskGroup.GetOutputPaths(task.TokenizedDestination, new(format.ToString()));
                var text = File.ReadAllText(paths.NovelFile);
                Require(format == NovelDownloadFormat.OriginalTxt ? text == SourceText : text.Contains("かんじ") && text.Contains("page1") && text.Contains("11.png") && text.Contains("994101-1.png"),
                    "Built-in formats preserve source or parsed Unicode, page anchors and local image paths: " + format);
                Require(Directory.GetFiles(paths.ImageFolderPath, "*.png").Length == 3, "Cover and both image kinds are downloaded beside the novel");
            }

            var service = App.AppViewModel.AppServiceProvider.GetRequiredService<ExtensionService>();
            var first = new Exporter("first");
            var second = new Exporter("second");
            using var firstHost = new ExtensionsHostModel(new ExportHost(first), []);
            using var secondHost = new ExtensionsHostModel(new ExportHost(second), []);
            service.HostModels.Add(firstHost); service.HostModels.Add(secondHost);
            try
            {
                var extension = await SaveAsync("extension-success", "extension:fixture");
                var file = NovelDownloadTaskGroup.GetOutputPaths(extension.TokenizedDestination, new("extension:fixture")).NovelFile;
                Require(extension.CurrentState == DownloadState.Completed && File.ReadAllText(file) == "first:" + SourceText && first.Calls == 1 && second.Calls == 0,
                    "The first active format provider receives the source and owns the committed output");
                firstHost.IsActive = false;
                extension = await SaveAsync("extension-second", "extension:fixture");
                file = NovelDownloadTaskGroup.GetOutputPaths(extension.TokenizedDestination, new("extension:fixture")).NovelFile;
                Require(extension.CurrentState == DownloadState.Completed && File.ReadAllText(file) == "second:" + SourceText && second.Calls == 1, "Disabling the first provider exposes the next active provider");
                second.Throw = true;
                extension = await SaveAsync("extension-failure", "extension:fixture");
                file = NovelDownloadTaskGroup.GetOutputPaths(extension.TokenizedDestination, new("extension:fixture")).NovelFile;
                Require(extension.CurrentState == DownloadState.Error && !File.Exists(file)
                    && extension.ErrorMessage!.Contains("Fixed export provider failure")
                    && !Directory.Exists(NovelDownloadTaskGroup.GetOutputPaths(extension.TokenizedDestination, new("extension:fixture")).ImageFolderPath),
                    "A provider failure never commits its partial temporary file and cleans its temporary images");
            }
            finally { service.HostModels.Remove(firstHost); service.HostModels.Remove(secondHost); }
            report.Add("PASS save/export: failed content yields feedback/no task, same-item retry, actual queued Html/Md/OriginalTxt files and images, active format priority and extension partial-file failure");
            File.WriteAllLines(Path.Combine(directory, "novel-actions-contracts.txt"), report);
            Require(fixture.Http.RequestLog.Count(request => request.Contains("/webview/v2/novel")) == 2, "All successful exports reuse the recovered content without extra network requests");
            Require(fixture.Http.UnexpectedRequests.IsEmpty, "Reader actions only reach declared offline boundaries");
            host.Shell.ClosePage(page);

            async Task<NovelDownloadTaskGroup> SaveAsync(string name, string token)
            {
                var settings = App.AppViewModel.AppSettings.DownloadSettings;
                settings.DownloadFormats.NovelDownloadFormat = token;
                settings.DownloadPathMacro = Path.Combine(directory, "exports", name, "@{id}.@{ext}");
                var count = manager.QueuedTasks.Count;
                var previous = manager.QueuedTasks.ToArray();
                await ActivateAsync(window, save);
                await UntilAsync(() => manager.QueuedTasks.Count == count + 1);
                var task = (NovelDownloadTaskGroup) manager.QueuedTasks.Except(previous).Single();
                await UntilAsync(() => task.CurrentState is DownloadState.Completed or DownloadState.Error);
                return task;
            }
        }
        finally { window.Close(); }
    }

    private sealed class Exporter(string name) : INovelFormatProviderExtension
    {
        public int Calls { get; private set; }
        public bool Throw { get; set; }
        public void FormatNovel(ITaskCompletionSource task, string input, string destination, string[] keys, IStream[] values, int count)
        {
            Calls++;
            Require(count == 3 && keys.Contains("cover.png") && keys.Contains("11.png") && keys.Contains("994101-1.png") && values.Length == count,
                "The export ABI receives the cover and both embedded image streams");
            File.WriteAllText(destination, name + ":" + input);
            if (Throw) throw new InvalidOperationException("Fixed export provider failure");
            task.SetCompleted();
        }
        public string GetFormatExtension() => "fixture";
        public string GetFormatDescription() => "Offline novel export";
        public void OnExtensionLoaded() { }
        public void OnExtensionUnloaded() { }
    }

    private sealed class ExportHost(Exporter exporter) : IExtensionsHost
    {
        public string GetExtensionName() => "Novel exporter fixture";
        public string GetAuthorName() => "Pixeval tests";
        public string GetExtensionLink() => "";
        public string GetHelpLink() => "";
        public string GetDescription() => "Managed offline export boundary";
        public string GetSdkVersion() => ExtensionService.CurrentVersion;
        public string GetVersion() => "1.0";
        public IExtension[] GetExtensions(out int count) { count = 1; return [exporter]; }
        public byte[]? GetIcon(out int count) { count = 0; return null; }
        public void Initialize(string cultureName, string tempDirectory, string extensionDirectory, ILogger logger) { }
    }
}
