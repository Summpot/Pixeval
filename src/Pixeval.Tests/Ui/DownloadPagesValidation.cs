using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mako.Model;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Download;
using Pixeval.Controls;
using Pixeval.Models.Options;
using Pixeval.Models.Database;
using Pixeval.Models.Database.Managers;
using Pixeval.Models.Download.Tasks;
using Pixeval.Models.Subscriptions;
using Pixeval.ViewModels;
using Pixeval.Views.Download;

namespace Pixeval.Tests.Ui;

internal static class DownloadPagesValidation
{
    internal static async Task RunAsync(string directory)
    {
        using var fixture = new OfflineSearchFixture(App.AppViewModel.MakoClient);
        await App.AppViewModel.MakoClient.SetTokenAsync("fixture-download-pages");
        await App.AppViewModel.MakoClient.ValidateTokenAsync();
        var history = App.AppViewModel.HistoryPersistHelper;
        await history.RestoreTask;
        var manager = history.DownloadManager;
        var subscriptions = App.AppViewModel.AppServiceProvider.GetRequiredService<WorkSubscriptionPersistentManager>();
        var service = App.AppViewModel.AppServiceProvider.GetRequiredService<IWorkSubscriptionService>();
        var downloadDirectory = Path.Combine(directory, "fixed-downloads");
        Directory.CreateDirectory(downloadDirectory);
        var report = new List<string>();
        using var pageModel = new DownloadPageViewModel(manager.QueuedTasks, subscriptions, service);
        await pageModel.SubscriptionFoldersLoadTask;
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var width in new[] { 360d, 1000d })
            {
                var tasks = Enumerable.Range(0, 4).Select(i => new FixedDownloadTask(
                    Path.Combine(downloadDirectory, $"{theme}-{width}-{i}.txt"), 999001 + i,
                    i == 3 ? "Unrelated work" : "Fixed download / 很长的作品标题 / Un très long titre pour vérifier la largeur disponible " + i)).ToArray();
                foreach (var item in tasks) manager.QueueTask(item);
                using var listModel = new DownloadItemPageViewModel(pageModel);
                var page = new DownloadItemView(listModel);
                var host = new PageTestHost { Content = page };
                var window = new Window { Width = width, Height = 1000, Content = host.Shell, RequestedThemeVariant = theme };
                try
                {
                    window.Show(); await RenderAsync(window);
                    var filter = page.FindControl<TextBox>("FilterTextBox")!;
                    var status = page.FindControl<SymbolComboBox>("StatusFilter")!;
                    var deleteFiles = page.FindControl<CheckBox>("DeleteLocalFilesCheckBox")!;
                    var list = page.FindControl<ListBox>("ListBox")!;
                    var bulk = page.FindControl<CommandBar>("BulkCommands")!;
                    InWindow(window, filter); InWindow(window, status); InWindow(window, deleteFiles);
                    Require(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(status)) && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(filter)), "Download fields have localized names");
                    Require(list.Items.Count == 4 && !page.HasNoDownloadItem, "Initial real download list contains the queue");
                    var firstContainer = list.GetVisualDescendants().OfType<ListBoxItem>().First();
                    var firstAction = firstContainer.GetVisualDescendants().OfType<CommandBarButton>().Single(button => button.Name == "PrimaryActionButton");
                    Require(firstContainer.Focus(), "Task list item receives keyboard focus");
                    for (var tab = 0; tab < 4 && !ReferenceEquals(window.FocusManager!.GetFocusedElement(), firstAction); tab++)
                    { Press(window, Key.Tab, PhysicalKey.Tab); await RenderAsync(window); }
                    Require(ReferenceEquals(window.FocusManager!.GetFocusedElement(), firstAction), "Tab reaches the task action inside the real list");
                    Save(window, directory, $"downloads-{theme}-{width}");
                    Require(filter.Focus(), "Search field receives keyboard focus");
                    filter.Text = "fixed DOWNLOAD"; Press(window, Key.Enter, PhysicalKey.Enter); await RenderAsync(window);
                    Require(listModel.CurrentOption == DownloadListOption.CustomSearch && list.Items.Count == 3, "Enter applies case-insensitive title search");
                    var added = new FixedDownloadTask(Path.Combine(downloadDirectory, $"{theme}-{width}-new.txt"), 999010, "Fixed download arrives later");
                    manager.QueueTask(added); await RenderAsync(window);
                    Require(list.Items.Count == 4, "New matching tasks appear while a custom search is active");
                    manager.TryRemoveTask(added); await RenderAsync(window);
                    Require(list.Items.Count == 3, "Removed tasks leave custom search");
                    filter.Text = "999004"; Press(window, Key.Enter, PhysicalKey.Enter); await RenderAsync(window);
                    Require(list.Items.Count == 1 && ((DownloadItemViewModel) list.Items[0]!).DownloadTask == tasks[3], "ID search preserves exact matching item");
                    filter.Text = "  "; Press(window, Key.Enter, PhysicalKey.Enter); await RenderAsync(window);
                    Require(listModel.CurrentOption == DownloadListOption.AllQueued && list.Items.Count == 4, "Empty search restores all tasks");
                    foreach (var option in new[] { DownloadListOption.Running, DownloadListOption.Completed, DownloadListOption.Cancelled, DownloadListOption.Error })
                    {
                        foreach (var item in tasks) item.SetState(DownloadState.Paused);
                        listModel.CurrentOption = option; await RenderAsync(window);
                        Require(list.Items.Count == 0 && page.HasNoDownloadItem, "Status filter updates empty state after page load: " + option);
                        tasks[0].SetState(Enum.Parse<DownloadState>(option.ToString())); await RenderAsync(window);
                        Require(list.Items.Count == 1 && !page.HasNoDownloadItem, "Status transitions enter the active filter: " + option);
                        tasks[0].SetState(DownloadState.Paused); await RenderAsync(window);
                        Require(list.Items.Count == 0 && page.HasNoDownloadItem, "Status transitions leave the active filter: " + option);
                    }
                    listModel.CurrentOption = DownloadListOption.AllQueued; await RenderAsync(window);
                    await ActivateAsync(window, bulk, (CommandBarButton) bulk.PrimaryCommands[0]);
                    Require(page.SelectedCount == 4, "Toolbar selects all visible tasks");
                    await ActivateAsync(window, bulk, (CommandBarButton) bulk.PrimaryCommands[1]);
                    Require(page.SelectedCount == 0 && list.AutoScrollToSelectedItem, "Invert clears selection without changing automatic scrolling");
                    list.Selection.Select(list.Items.IndexOf(pageModel.OrdinaryItems.Single(item => item.DownloadTask == tasks[0]))); list.Selection.Select(list.Items.IndexOf(pageModel.OrdinaryItems.Single(item => item.DownloadTask == tasks[2]))); await RenderAsync(window);
                    foreach (var (index, call) in new[] { (3, "resume"), (4, "pause"), (5, "cancel"), (6, "reset") })
                    {
                        foreach (var item in tasks) item.Calls.Clear();
                        await ActivateAsync(window, bulk, (CommandBarButton) bulk.PrimaryCommands[index]);
                        Require(tasks[0].Calls.SequenceEqual(new[] { call }) && tasks[2].Calls.SequenceEqual(new[] { call }) && tasks[1].Calls.Count == 0 && tasks[3].Calls.Count == 0,
                            "Bulk action targets each selected task exactly once: " + call + " / " + string.Join(";", tasks.Select(t => t.Id + ":" + string.Join(",", t.Calls))));
                    }
                    await ActivateAsync(window, bulk, (CommandBarButton) bulk.PrimaryCommands[2]);
                    Require(page.SelectedCount == 0 && !bulk.PrimaryCommands.Skip(2).Cast<CommandBarButton>().Any(button => button.IsEnabled), "Clear selection disables all selection-dependent actions");
                    // Reattach the same view: subscriptions and task identity must survive.
                    host.Content = null; await RenderAsync(window); host.Content = page; await RenderAsync(window);
                    listModel.CurrentOption = DownloadListOption.Error; await RenderAsync(window);
                    Require(page.HasNoDownloadItem, "Empty state remains subscribed after reattachment");
                    tasks[1].SetState(DownloadState.Error); await RenderAsync(window);
                    Require(!page.HasNoDownloadItem, "Restored view receives later queue changes");
                    listModel.CurrentOption = DownloadListOption.AllQueued; await RenderAsync(window);
                    for (var deleteLocal = 0; deleteLocal < 2; deleteLocal++)
                    {
                        var victim = tasks[deleteLocal]; File.WriteAllText(victim.Destination, "isolated validation file");
                        list.UnselectAll(); list.SelectedItem = pageModel.OrdinaryItems.Single(item => item.DownloadTask == victim);
                        deleteFiles.IsChecked = deleteLocal == 1; await RenderAsync(window);
                        await ActivateAsync(window, bulk, (CommandBarButton) bulk.PrimaryCommands[7]);
                        Require(!manager.QueuedTasks.Contains(victim) && File.Exists(victim.Destination) == (deleteLocal == 0), "Record removal respects delete-local-files choice");
                        Require(victim.Calls.Count(call => call == "delete") == deleteLocal, "Local deletion is invoked only by explicit checkbox choice");
                    }
                    page.DataContext = null; await RenderAsync(window);
                    Require(page.DownloadItemsSource is null && page.HasNoDownloadItem, "Removing page context clears old task rows");
                    page.DataContext = listModel; await RenderAsync(window);
                    Require(list.Items.Count == 2, "Restoring the same context restores the remaining task identities");
                    report.Add($"PASS {theme}/{width}: layout, keyboard search, live new/removal/status filters, empty/reattach/context, selected bulk commands, delete-file choices");
                }
                finally
                {
                    page.Dispose(); host.Content = null; window.Close();
                    foreach (var item in tasks) manager.TryRemoveTask(item);
                }
            }
        await ValidateRowsAsync(directory, report);
        await ValidateRunningDownloadAsync(directory, report);
        Require(fixture.UnexpectedRequests.IsEmpty, "No unexpected HTTP request: " + string.Join(", ", fixture.UnexpectedRequests));
        File.WriteAllLines(Path.Combine(directory, "download-contracts.txt"), report);
        File.WriteAllLines(Path.Combine(directory, "offline-requests.txt"), fixture.RequestLog);
        Console.WriteLine($"Download contracts passed: {report.Count} groups.");
    }

    private static async Task ValidateRowsAsync(string directory, List<string> report)
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var width in new[] { 360d, 1000d })
            {
                var task = new FixedDownloadTask(Path.Combine(directory, "missing", "not-downloaded.txt"), 999100, "A very long download title / 下载标题完整保留 / Un titre très long pour le téléchargement");
                using var model = new DownloadItemViewModel(task);
                var row = new DownloadItem { DataContext = model };
                var host = new PageTestHost { Content = row };
                var window = new Window { Width = width, Height = 600, Content = host.Shell, RequestedThemeVariant = theme };
                var navigations = 0;
                row.OpenIllustrationRequested += (_, item) => { Require(item == model, "Original work action keeps the row model"); navigations++; };
                try
                {
                    window.Show(); await RenderAsync(window);
                    var bar = row.FindControl<CommandBar>("ItemCommands")!;
                    var primary = (CommandBarButton) bar.PrimaryCommands[0];
                    foreach (var (state, call) in new[] { (DownloadState.Pending, "cancel"), (DownloadState.Running, "pause"), (DownloadState.Queued, "pause"), (DownloadState.Cancelled, "reset"), (DownloadState.Error, "reset"), (DownloadState.Paused, "resume") })
                    {
                        task.SetState(state); task.Calls.Clear(); await RenderAsync(window);
                        Require(primary.Label == model.ActionButtonContent && primary.IsEnabled, "Primary action reflects " + state);
                        var progress = row.GetVisualDescendants().OfType<ProgressBar>().Single();
                        Require(progress.IsIndeterminate == (state == DownloadState.Pending) && progress.Value == 42, "Progress preserves pending and numeric states");
                        var title = row.FindControl<TextBlock>("TitleText")!;
                        InWindow(window, title); Require(title.Bounds.Width > 150 && ToolTip.GetTip(title)?.ToString() == model.Entry.Title, "Long title has readable width and its complete tooltip");
                        var stats = row.FindControl<TextBlock>("GroupStatsText")!;
                        Require(stats.Text == model.GroupProgressMessage && stats.IsVisible, "Group counts have readable status labels instead of color alone");
                        var text = row.FindControl<TextBlock>("ProgressText")!;
                        Require(ToolTip.GetTip(text)?.ToString() == model.ProgressMessage, "Progress/error text remains available without truncation");
                        Require(((CommandBarButton) bar.SecondaryCommands[0]).IsEnabled == model.IsResetItemEnabled && ((CommandBarButton) bar.SecondaryCommands[1]).IsEnabled == model.IsCancelItemEnabled,
                            "Secondary command enabled states follow the task: " + state);
                        if (state == DownloadState.Error) Save(window, directory, $"download-error-{theme}-{width}");
                        Require(primary.Focus(), "Primary action accepts keyboard focus"); Press(window, Key.Enter, PhysicalKey.Enter); await RenderAsync(window);
                        Require(task.Calls.SequenceEqual(new[] { call }), "Keyboard primary action executes once: " + state);
                    }
                    task.IsProcessing = true; task.SetState(DownloadState.Running); task.Calls.Clear(); await RenderAsync(window);
                    Require(!primary.IsEnabled && !((CommandBarButton) bar.SecondaryCommands[1]).IsEnabled, "Busy tasks disable processing-sensitive actions");
                    task.IsProcessing = false; task.SetState(DownloadState.Error); await RenderAsync(window);
                    await ActivateAsync(window, bar, (CommandBarButton) bar.SecondaryCommands[0]);
                    Require(task.Calls.SequenceEqual(new[] { "reset" }), "Real overflow reset reaches its row once");
                    task.SetState(DownloadState.Error); await RenderAsync(window);
                    await ActivateAsync(window, bar, (CommandBarButton) bar.SecondaryCommands[4]);
                    var dialog = window.GetVisualDescendants().OfType<ContentDialog>().Single();
                    Require(dialog.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == task.ErrorMessage), "Error detail dialog preserves the full original diagnostic");
                    Press(window, Key.Enter, PhysicalKey.Enter); await RenderAsync(window);
                    Require(!window.GetVisualDescendants().OfType<ContentDialog>().Any(), "Acknowledgement closes after keyboard confirmation");
                    task.Calls.Clear(); task.SetState(DownloadState.Running); await RenderAsync(window);
                    await ActivateAsync(window, bar, (CommandBarButton) bar.SecondaryCommands[1]);
                    Require(task.Calls.SequenceEqual(new[] { "cancel" }), "Real overflow cancel reaches its row once");
                    await ActivateAsync(window, bar, (CommandBarButton) bar.SecondaryCommands[3]);
                    Require(navigations == 1, "Original-work overflow action routes exactly once");
                    task.SetState(DownloadState.Completed); await RenderAsync(window);
                    await ActivateAsync(window, bar, primary);
                    Require(host.Notifications.Any(n => n.Message?.ToString() == task.Destination), "Missing completed file reports its path without launching an OS application");
                    var subscribers = task.Subscribers; model.Dispose(); Require(task.Subscribers == subscribers - 1 && task.Disposals == 0, "Disposing the row only detaches its model subscription");
                    report.Add($"PASS row {theme}/{width}: seven states, progress, long text, primary keyboard and overflow, busy guards, work route, missing path, model lifetime");
                }
                finally { host.Content = null; window.Close(); }
            }
    }

    private static async Task ValidateRunningDownloadAsync(string directory, List<string> report)
    {
        var entry = Illustration.CreateDefault() with
        {
            Id = 999201,
            Title = "Background download",
            PageCount = 1,
            MetaSinglePage = new() { OriginalImageUrl = "https://fixture.invalid/unique-background-download.png" }
        };
        using var task = new SingleImageDownloadTaskGroup(new DownloadHistoryEntry(Path.Combine(directory, "background-download.png"), entry) { State = DownloadState.Queued });
        using var handler = new PendingDownloadHandler();
        using var client = new HttpClient(handler);
        var running = task.StartAsync(client);
        var token = await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Require(task.CurrentState == DownloadState.Running, "Real image task is running before UI attachment");
        var manager = App.AppViewModel.HistoryPersistHelper.DownloadManager;
        manager.QueueTask(task);
        for (var i = 0; i < 3; i++)
        {
            using var page = new DownloadPage();
            var host = new PageTestHost { Content = page };
            var window = new Window { Width = 360, Height = 720, Content = host.Shell };
            window.Show(); await RenderAsync(window);
            Require(page.GetVisualDescendants().OfType<DownloadItem>().Any(item => item.DataContext is DownloadItemViewModel model && ReferenceEquals(model.DownloadTask, task)), "Recreated full download page displays the original task");
            host.Content = null; window.Close(); page.Dispose();
            Require(task.CurrentState == DownloadState.Running && !token.IsCancellationRequested && !running.IsCompleted, "Repeated UI teardown neither restarts nor cancels a real pending HTTP download");
        }
        byte[] bytes = [1, 2, 3, 4, 5, 6];
        handler.Response.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        await running.WaitAsync(TimeSpan.FromSeconds(10));
        Require(handler.Requests == 1 && task.CurrentState == DownloadState.Completed && File.ReadAllBytes(task.Destination).SequenceEqual(bytes), "Original download completes once with the original bytes after all UI instances close");
        manager.TryRemoveTask(task);
        report.Add("PASS real image download: three complete page lifetimes, one controlled HTTP request, uncancelled token, complete file bytes");
    }

    private sealed class PendingDownloadHandler : HttpMessageHandler
    {
        internal TaskCompletionSource<CancellationToken> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<HttpResponseMessage> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Requests { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Requests++; Started.TrySetResult(cancellationToken); return await Response.Task.WaitAsync(cancellationToken); }
    }

    private static async Task ActivateAsync(Window window, CommandBar bar, CommandBarButton button)
    {
        if (button.IsInOverflow || bar.SecondaryCommands.Contains(button))
        {
            var more = bar.GetVisualDescendants().OfType<Button>().Single(control => control.Name == "PART_OverflowButton");
            Require(more.Focus(), "Overflow accepts keyboard focus"); Press(window, Key.Enter, PhysicalKey.Enter); await RenderAsync(window);
            Require(bar.IsOpen, "Keyboard opens command overflow");
        }
        InWindow(window, button);
        var point = button.TranslatePoint(new Rect(button.Bounds.Size).Center, window)!.Value;
        window.MouseMove(point); window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); await RenderAsync(window);
        if (bar.IsOpen)
        {
            // Removing the last selected row disables its command and releases focus.
            // Dismiss the remaining overflow from its still-enabled keyboard entry.
            if (!button.IsEffectivelyEnabled)
                Require(bar.GetVisualDescendants().OfType<Button>().Single(control => control.Name == "PART_OverflowButton").Focus(),
                    "Overflow remains keyboard reachable after its action becomes disabled");
            Press(window, Key.Escape, PhysicalKey.Escape);
            await RenderAsync(window);
            Require(!bar.IsOpen, "Escape closes command overflow");
        }
    }
    private static void InWindow(Window window, Control control)
    {
        var point = control.TranslatePoint(default, window) ?? throw new InvalidOperationException("Detached control");
        Require(control.Bounds.Width > 0 && point.X >= -1 && point.X + control.Bounds.Width <= window.ClientSize.Width + 1,
            $"{control.Name ?? control.GetType().Name} fits width: {point} {control.Bounds.Size} / {window.ClientSize}");
    }
    private static void Press(Window window, Key key, PhysicalKey physical)
    { window.KeyPress(key, RawInputModifiers.None, physical, null); window.KeyRelease(key, RawInputModifiers.None, physical, null); }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(120); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }
    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No download frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }
    private static void Require(bool condition, string contract)
    {
        if (!condition) throw new InvalidOperationException(contract);
    }

    // Fixed task state only: no production transport, downloader or user file is invoked.
    internal sealed class FixedDownloadTask(string destination, long id, string title, DownloadHistoryEntryBase? entry = null) : IDownloadTaskGroup
    {
        private PropertyChangedEventHandler? _changed;
        public DownloadHistoryEntryBase DatabaseEntry { get; } = entry ?? new DownloadHistoryEntry(destination, Novel.CreateDefault() with
        {
            Id = id,
            Title = title,
            User = UserInfo.CreateDefault() with { Id = 900001, Name = "Fixed download author" }
        });
        public string Id => DatabaseEntry.Entry.Id;
        public double ProgressPercentage { get; private set; }
        public DownloadState CurrentState { get; private set; } = DownloadState.Completed;
        public string Destination => DatabaseEntry.Destination;
        public string OpenLocalDestination => Destination;
        public string? ErrorMessage { get; private set; }
        public bool IsProcessing { get; set; }
        private (int Active, int Completed, int Errors)? _groupCounts;
        public int ActiveCount => _groupCounts?.Active ?? (CurrentState is DownloadState.Running or DownloadState.Queued or DownloadState.Paused ? 2 : 0);
        public int CompletedCount => _groupCounts?.Completed ?? (CurrentState == DownloadState.Completed ? 3 : 1);
        public int ErrorCount => _groupCounts?.Errors ?? (CurrentState == DownloadState.Error ? 1 : 0);
        internal void SetCounts(int active, int completed, int errors)
        {
            _groupCounts = (active, completed, errors);
            foreach (var property in new[] { nameof(ActiveCount), nameof(CompletedCount), nameof(ErrorCount) })
                _changed?.Invoke(this, new(property));
        }
        public int Count => 3;
        internal int Subscribers { get; private set; }
        internal int Subscriptions { get; private set; }
        internal int Disposals { get; private set; }
        internal List<string> Calls { get; } = [];
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; Subscribers++; }
            remove { _changed -= value; Subscribers--; }
        }
        public event PropertyChangingEventHandler? PropertyChanging { add { } remove { } }
        internal void SetState(DownloadState state, double progress = 42)
        {
            CurrentState = state; ProgressPercentage = progress; ErrorMessage = state == DownloadState.Error ? "Fixed error with a complete long diagnostic message / 固定错误" : null;
            foreach (var property in new[] { nameof(CurrentState), nameof(ProgressPercentage), nameof(ErrorMessage), nameof(IsProcessing), nameof(ActiveCount), nameof(CompletedCount), nameof(ErrorCount) })
                _changed?.Invoke(this, new(property));
        }
        public ValueTask InitializeTaskGroupAsync() => ValueTask.CompletedTask;
        public void SubscribeProgress(ChannelWriter<DownloadToken> writer) => Subscriptions++;
        public DownloadToken GetToken() => new(this, CancellationToken.None);
        public void Reset() { Calls.Add("reset"); if (CurrentState is DownloadState.Completed or DownloadState.Error or DownloadState.Cancelled) SetState(DownloadState.Queued, 0); }
        public void Pause() { Calls.Add("pause"); if (CurrentState is DownloadState.Queued or DownloadState.Running) SetState(DownloadState.Paused); }
        public void Resume() { Calls.Add("resume"); if (CurrentState is DownloadState.Paused) SetState(DownloadState.Queued); }
        public void Cancel() { Calls.Add("cancel"); if (CurrentState is DownloadState.Queued or DownloadState.Running or DownloadState.Paused or DownloadState.Pending) SetState(DownloadState.Cancelled); }
        public void Delete() { Calls.Add("delete"); File.Delete(Destination); }
        public void Dispose() => Disposals++;
        public IEnumerator<ISingleDownloadTaskBase> GetEnumerator() => ((IEnumerable<ISingleDownloadTaskBase>) []).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
