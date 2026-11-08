using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mako.Model;
using Mako.Net.Responses;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Download;
using Pixeval.Models.Database;
using Pixeval.Models.Database.Managers;
using Pixeval.Models.Options;
using Pixeval.Models.Subscriptions;
using Pixeval.ViewModels;
using Pixeval.Views.Download;

namespace Pixeval.MigrationValidation;

internal static class DownloadFoldersValidation
{
    internal static async Task RunAsync(string directory)
    {
        using var fixture = new OfflineSearchFixture(App.AppViewModel.MakoClient);
        await App.AppViewModel.MakoClient.SetTokenAsync("fixture-download-folders");
        await App.AppViewModel.MakoClient.ValidateTokenAsync();
        await App.AppViewModel.HistoryPersistHelper.RestoreTask;
        var manager = App.AppViewModel.HistoryPersistHelper.DownloadManager;
        var subscriptions = App.AppViewModel.AppServiceProvider.GetRequiredService<WorkSubscriptionPersistentManager>();
        var service = App.AppViewModel.AppServiceProvider.GetRequiredService<WorkSubscriptionDownloadService>();
        fixture.UserResponseFactory = (request, _) => Task.FromResult(JsonSerializer.Serialize(SingleUserResponse.CreateDefault() with
        {
            UserEntity = SingleUserInfo.CreateDefault() with { Id = long.Parse(System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)["user_id"]!), Name = "Refreshed subscription name / 完整订阅标题" }
        }));
        fixture.WorkFeedResponseFactory = (_, _) => Task.FromResult("{\"novels\":[],\"next_url\":null}");
        List<string> report = [];
        try
        {
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                foreach (var width in new[] { 360d, 1000d })
                {
                    var first = subscriptions.Upsert(new WorkSubscriptionEntry { Id = 999301, Name = "很长的订阅标题 / A very long subscription name that stays available in full", SubscriptionType = WorkSubscriptionType.Posts, WorkKind = WorkSubscriptionWorkKind.Novel });
                    var second = subscriptions.Upsert(new WorkSubscriptionEntry { Id = 999302, Name = "Empty subscription", SubscriptionType = WorkSubscriptionType.Bookmarks, WorkKind = WorkSubscriptionWorkKind.Novel });
                    var tasks = Enumerable.Range(0, 2).Select(i => new DownloadPagesValidation.FixedDownloadTask(
                        Path.Combine(directory, $"folder-{theme}-{width}-{i}.txt"), 999310 + i, "Folder work " + i,
                        new SubscriptionDownloadHistoryEntry(Path.Combine(directory, $"folder-{theme}-{width}-{i}.txt"), Novel.CreateDefault() with { Id = 999310 + i, Title = "Folder work " + i }, first.HistoryEntryId))).ToArray();
                    foreach (var task in tasks) { manager.QueueTask(task); task.SetState(DownloadState.Completed, 100); }
                    using var pageModel = new DownloadPageViewModel(manager.QueuedTasks, subscriptions, service);
                    await pageModel.SubscriptionFoldersLoadTask;
                    using var folderModel = new DownloadFolderPageViewModel(pageModel);
                    using var frame = new DownloadFolderView(folderModel);
                    var host = new SearchCompletionValidation.FixtureViewContainer { Content = frame };
                    var window = new Window { Width = width, Height = 900, Content = host, RequestedThemeVariant = theme };
                    try
                    {
                        window.Show(); await UntilAsync(() => frame.CurrentPage is DownloadFolderListView && !frame.IsNavigating); await RenderAsync(window);
                        var listPage = (DownloadFolderListView) frame.CurrentPage!;
                        var folder = pageModel.SubscriptionFolders.Single(item => item.Subscription.HistoryEntryId == first.HistoryEntryId);
                        Require(pageModel.OrdinaryItems.Count == 0 && pageModel.SubscriptionFolders.Count == 2 && folder.Items.Count == 2 && !listPage.HasNoFolder, "Ordinary tasks, populated subscription and empty subscription remain separate");
                        var row = listPage.GetVisualDescendants().OfType<DownloadFolder>().Single(control => ReferenceEquals(control.DataContext, folder));
                        var card = row.FindControl<Button>("FolderCard")!;
                        Require(AutomationProperties.GetName(card) == folder.Title && AutomationProperties.GetHelpText(card) == folder.Subtitle, "Folder navigation has its complete title and current status");
                        var title = row.FindControl<TextBlock>("FolderTitle")!;
                        Require(title.Bounds.Width >= 160 && ToolTip.GetTip(title)?.ToString() == folder.Title, "Long folder titles keep readable width and a complete tooltip");
                        tasks[0].SetState(DownloadState.Running, 40); tasks[1].SetState(DownloadState.Error, 20); await RenderAsync(window);
                        Require(folder.ProgressPercentage == 30 && row.GetVisualDescendants().OfType<ProgressBar>().Single().Value == 30,
                            "Folder progress follows its current task group");
                        Require(row.FindControl<TextBlock>("GroupStatsText")!.Text == folder.GroupProgressMessage && folder.ErrorCount == 1, "Aggregate counts include text status labels and update with task states");
                        tasks[0].SetCounts(1, 2, 0); await RenderAsync(window);
                        Require(folder.ActiveCount == 1 && folder.CompletedCount == 3 && folder.ErrorCount == 1
                            && row.FindControl<TextBlock>("GroupStatsText")!.Text == folder.GroupProgressMessage,
                            "Partial group completion updates aggregate labels without changing the parent task state");
                        Save(window, directory, $"download-folders-{theme}-{width}");
                        var originalTasks = folder.Items.Select(item => item.DownloadTask).ToArray();
                        var container = listPage.GetVisualDescendants().OfType<ListBoxItem>().Single(item => ReferenceEquals(item.DataContext, folder));
                        Require(container.Focus(), "Folder list item receives focus"); Press(window, Key.Tab, PhysicalKey.Tab); await RenderAsync(window);
                        Require(ReferenceEquals(window.FocusManager!.GetFocusedElement(), card), "Tab reaches the folder's actual navigation button");
                        window.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null); window.KeyRelease(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null); await RenderAsync(window);
                        Require(ReferenceEquals(window.FocusManager.GetFocusedElement(), container), "Shift+Tab returns from the folder action to its list item");
                        Require(card.Focus(), "Folder navigation receives keyboard focus"); Press(window, Key.Enter, PhysicalKey.Enter);
                        await UntilAsync(() => frame.CurrentPage is DownloadItemView && !frame.IsNavigating); await RenderAsync(window);
                        var child = (DownloadItemView) frame.CurrentPage!;
                        Require(ReferenceEquals(child.Folder, folder) && ((DownloadItemPageViewModel) child.DataContext!).View.Count == 2, "Keyboard opens only the selected subscription's task list");
                        var childModel = (DownloadItemPageViewModel) child.DataContext!;
                        var incoming = new DownloadPagesValidation.FixedDownloadTask(Path.Combine(directory, "incoming-folder.txt"), 999399, "Incoming folder work",
                            new SubscriptionDownloadHistoryEntry(Path.Combine(directory, "incoming-folder.txt"), Novel.CreateDefault() with { Id = 999399, Title = "Incoming folder work" }, first.HistoryEntryId));
                        manager.QueueTask(incoming); await RenderAsync(window);
                        Require(childModel.View.Count == 3 && folder.Items.Count == 3, "An open folder immediately receives its newly queued task");
                        manager.TryRemoveTask(incoming); await RenderAsync(window);
                        Require(childModel.View.Count == 2, "Removing a task updates the open folder list");
                        var pop = frame.PopAsync(); await UntilAsync(() => pop.IsCompleted && !frame.IsNavigating); await pop; await RenderAsync(window);
                        Require(ReferenceEquals(frame.CurrentPage, listPage) && folder.Items.Select(item => item.DownloadTask).SequenceEqual(originalTasks), "Returning retains folder identity and the original tasks");
                        row = listPage.GetVisualDescendants().OfType<DownloadFolder>().Single(control => ReferenceEquals(control.DataContext, folder)); card = row.FindControl<Button>("FolderCard")!;
                        var menu = row.ContextMenu!;
                        foreach (var (index, action) in new[] { (0, "resume"), (1, "pause"), (2, "cancel"), (3, "reset") })
                        {
                            foreach (var task in tasks) task.Calls.Clear();
                            await OpenMenuAsync();
                            var item = menu.Items.OfType<MenuItem>().ElementAt(index);
                            Require(ReferenceEquals(item.Tag, folder), "Detached menu preserves its folder parameter");
                            Require(item.Focus(), "Folder menu action accepts focus"); Press(window, Key.Enter, PhysicalKey.Enter); await RenderAsync(window);
                            Require(tasks.All(task => task.Calls.SequenceEqual(new[] { action })), "Folder command dispatches exactly once to each task: " + action);
                        }
                        var before = fixture.RequestLog.Count;
                        await OpenMenuAsync(); var sync = menu.Items.OfType<MenuItem>().ElementAt(4); sync.Focus(); Press(window, Key.Enter, PhysicalKey.Enter);
                        await UntilAsync(() => fixture.RequestLog.Count > before && !service.IsSyncInProgress); await RenderAsync(window);
                        Require(fixture.RequestLog.Skip(before).Any(request => request.Contains("user_id=999301", StringComparison.Ordinal)) && folder.Title.Contains("Refreshed subscription name", StringComparison.Ordinal) && AutomationProperties.GetName(card) == folder.Title, "Folder sync uses the original subscription and publishes its refreshed metadata");
                        await OpenMenuAsync(); var remove = menu.Items.OfType<MenuItem>().ElementAt(5); remove.Focus(); Press(window, Key.Enter, PhysicalKey.Enter);
                        await UntilAsync(() => pageModel.SubscriptionFolders.Count == 1); await RenderAsync(window);
                        Require(manager.QueuedTasks.Count == 0 && subscriptions.GetByKey(first.HistoryEntryId) is null && !listPage.HasNoFolder, "Removing one subscription removes its tasks but retains the empty subscription");
                        listPage.DataContext = null; await RenderAsync(window);
                        Require(listPage.HasNoFolder, "Clearing the folder context shows a true empty state");
                        listPage.DataContext = folderModel; await RenderAsync(window);
                        Require(!listPage.HasNoFolder, "Restoring the folder context resubscribes to its current collection");
                        await service.TryRemoveAsync(second.HistoryEntryId); await UntilAsync(() => listPage.HasNoFolder);
                        frame.Dispose(); pageModel.Dispose();
                        Require(tasks.All(task => task.Disposals == 0), "Folder/view disposal does not dispose backend download tasks");
                        report.Add($"PASS {theme}/{width}: populated/empty groups, progress and names, keyboard navigation/back, context menu batch/sync/remove, null/rebind empty state, backend task identity");
                        continue;
                        async Task OpenMenuAsync()
                        {
                            card.Focus(); Press(window, Key.Apps, PhysicalKey.ContextMenu); await RenderAsync(window);
                            Require(menu.IsOpen, "Platform menu key opens folder actions");
                        }
                    }
                    finally
                    {
                        host.Content = null; window.Close();
                        foreach (var task in tasks) manager.TryRemoveTask(task);
                        await service.TryRemoveAsync(first.HistoryEntryId); await service.TryRemoveAsync(second.HistoryEntryId);
                    }
                }
            Require(fixture.UnexpectedRequests.IsEmpty, "No unexpected folder HTTP request: " + string.Join(", ", fixture.UnexpectedRequests));
            File.WriteAllLines(Path.Combine(directory, "folder-contracts.txt"), report);
            Console.WriteLine($"Download folder contracts passed: {report.Count} groups.");
        }
        finally
        { File.WriteAllLines(Path.Combine(directory, "offline-requests.txt"), fixture.RequestLog); await service.CancelAndWaitAsync(); }
    }
    private static async Task UntilAsync(Func<bool> predicate)
    {
        var stop = DateTime.UtcNow.AddSeconds(10);
        while (!predicate())
        { if (DateTime.UtcNow > stop) throw new TimeoutException("Folder state did not settle"); Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(1); await Task.Delay(30); }
    }
    private static void Press(Window window, Key key, PhysicalKey physical)
    { window.KeyPress(key, RawInputModifiers.None, physical, null); window.KeyRelease(key, RawInputModifiers.None, physical, null); }
    private static async Task RenderAsync(Window window)
    { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(1); await Task.Delay(150); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(1); }
    private static void Save(Window window, string directory, string name)
    { using var frame = window.CaptureRenderedFrame()!; frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions()); }
    private static void Require(bool condition, string contract)
    { if (!condition) throw new InvalidOperationException(contract); }
}
