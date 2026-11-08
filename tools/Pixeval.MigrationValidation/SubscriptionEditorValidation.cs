using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Web;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mako.Model;
using Mako.Net.Responses;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Controls;
using Pixeval.Models.Database.Managers;
using Pixeval.Models.Options;
using Pixeval.Models.Settings.Entries;
using Pixeval.Models.Subscriptions;
using Pixeval.Views.Settings;

namespace Pixeval.MigrationValidation;

internal static class SubscriptionEditorValidation
{
    private const string LongName = "长订阅名称 / Very long subscription name / Nom complet de la série et de son auteur";
    internal static async Task RunAsync(string directory)
    {
        var client = App.AppViewModel.MakoClient;
        using var fixture = new OfflineSearchFixture(client);
        await client.SetTokenAsync("fixture-subscription-editor");
        await client.ValidateTokenAsync();
        await App.AppViewModel.HistoryPersistHelper.RestoreTask;
        var manager = App.AppViewModel.AppServiceProvider.GetRequiredService<WorkSubscriptionPersistentManager>();
        var service = App.AppViewModel.AppServiceProvider.GetRequiredService<WorkSubscriptionDownloadService>();
        var requests = Channel.CreateUnbounded<Pending>();
        var blockNext = 0;
        var pendingOperations = new ConcurrentBag<Pending>();
        fixture.SeriesResponseFactory = async (request, token) =>
        {
            if (Interlocked.Exchange(ref blockNext, 0) == 1)
            {
                var pending = new Pending(request.RequestUri!, token);
                pendingOperations.Add(pending);
                await requests.Writer.WriteAsync(pending);
                return await pending.Response.Task;
            }
            return Metadata(request.RequestUri!);
        };
        fixture.WorkFeedResponseFactory = (request, _) => Task.FromResult(EmptyFeed(request.RequestUri!));
        fixture.BookmarkWorksResponseFactory = (request, _) => Task.FromResult(EmptyFeed(request.RequestUri!));
        List<string> report = [];
        try
        {
            // Baseline reproduction is deliberately first: an unfinished add belongs to this editor.
            var editor = new WorkSubscriptionsSettingsExpander { Entry = new WorkSubscriptionsSettingsEntry(group => group.WorkSubscriptions), IsExpanded = true };
            var host = new SearchCompletionValidation.FixtureViewContainer { Content = editor };
            var window = new Window { Width = 1000, Height = 900, Content = host };
            window.Show(); await RenderAsync(window);
            var target = editor.FindControl<TextBox>("TargetIdTextBox")!;
            var add = editor.GetVisualDescendants().OfType<Button>().Single(button => button is Material3Expressive.Components.IconButton icon && icon.Symbol.ToString() == "Add");
            target.Text = "998801"; blockNext = 1;
            Click(window, add);
            var operation = await NextAsync(requests);
            host.Content = null; await RenderAsync(window);
            try
            {
                Require(operation.Token.IsCancellationRequested, "Unloading the subscription editor cancels its pending metadata request");
                operation.Response.SetResult(Metadata(operation.Uri));
                await RenderAsync(window);
                Require(manager.Count == 0, "A late metadata response cannot create a subscription after the editor closes");
            }
            finally
            {
                operation.Response.TrySetResult("{}"); window.Close();
            }
            report.Add("PASS editor detach cancels pending metadata and rejects late persistence");
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                foreach (var width in new[] { 360d, 1000d })
                {
                    editor = new WorkSubscriptionsSettingsExpander { Entry = new WorkSubscriptionsSettingsEntry(group => group.WorkSubscriptions), IsExpanded = true };
                    var scroll = new ScrollViewer { Content = new StackPanel { Children = { editor } }, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
                    host = new SearchCompletionValidation.FixtureViewContainer { Content = scroll };
                    window = new Window { Width = width, Height = 900, RequestedThemeVariant = theme, Content = host };
                    try
                    {
                        window.Show(); await RenderAsync(window);
                        await Task.Delay(300); await RenderAsync(window); // SettingsExpander's opening transition must expose the last input row.
                        target = editor.FindControl<TextBox>("TargetIdTextBox")!;
                        var type = editor.FindControl<SymbolComboBox>("SubscriptionTypeComboBox")!;
                        var kind = editor.FindControl<SymbolComboBox>("WorkKindComboBox")!;
                        add = editor.FindControl<Button>("AddSubscriptionButton")!;
                        var error = editor.FindControl<InfoBar>("ErrorTextBlock")!;
                        foreach (var control in new Control[] { target, type, kind, add })
                        {
                            InWindow(window, control);
                            Require(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)), "Subscription fields and add action have localized purposes");
                        }
                        Save(window, directory, $"subscription-empty-{theme}-{width}");
                        foreach (var invalid in new[] { "", "0", "-1", "abc", "9223372036854775808" })
                        {
                            var before = fixture.RequestLog.Count;
                            target.Text = invalid; Click(window, add); await RenderAsync(window);
                            if (!error.IsVisible) Save(window, directory, "invalid-input-diagnostic");
                            Require(error.IsVisible && (target.Text ?? "") == invalid && fixture.RequestLog.Count == before && manager.Count == 0,
                                $"Invalid target preserves input and makes no request: {invalid}; error={error.IsVisible}, text={target.Text ?? "<null>"}, requests={fixture.RequestLog.Count - before}, rows={manager.Count}, enabled={add.IsEnabled}");
                        }
                        foreach (var subscriptionType in Enum.GetValues<WorkSubscriptionType>())
                        {
                            type.SelectedValue = subscriptionType; await RenderAsync(window);
                            var expectedKinds = subscriptionType == WorkSubscriptionType.Posts
                                ? new[] { WorkSubscriptionWorkKind.Illustration, WorkSubscriptionWorkKind.Manga, WorkSubscriptionWorkKind.Novel }
                                : new[] { WorkSubscriptionWorkKind.Illustration, WorkSubscriptionWorkKind.Novel };
                            Require(kind.ItemsSource!.Cast<SymbolComboBoxItem>().Select(item => (WorkSubscriptionWorkKind) item.Value!).SequenceEqual(expectedKinds), "Original work-kind choices are preserved for " + subscriptionType);
                            foreach (var workKind in expectedKinds)
                            {
                                kind.SelectedValue = workKind;
                                var id = 998900 + (int) subscriptionType * 10 + (int) workKind;
                                target.Text = id.ToString(); blockNext = 1;
                                Require(add.Focus(), "Add accepts keyboard focus"); Press(window, Key.Enter, PhysicalKey.Enter);
                                var pending = await NextAsync(requests);
                                await RenderAsync(window);
                                Require(!add.IsEnabled && !error.IsVisible, "A metadata request owns one busy state");
                                add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await RenderAsync(window);
                                Require(!requests.Reader.TryPeek(out _) && !add.IsEnabled, "Repeated activation cannot start a second add");
                                var query = HttpUtility.ParseQueryString(pending.Uri.Query);
                                var parameter = subscriptionType == WorkSubscriptionType.Series ? workKind == WorkSubscriptionWorkKind.Novel ? "series_id" : "illust_series_id" : "user_id";
                                Require(query[parameter] == id.ToString(), "Metadata request retains the target ID and work category");
                                target.Text = "new draft";
                                pending.Response.SetResult(Metadata(pending.Uri));
                                await UntilAsync(() => add.IsEnabled && manager.GetBySubscriptionKey(id, subscriptionType, workKind) is not null && !service.IsSyncInProgress);
                                await UntilAsync(() => editor.Subscriptions.Any(item => item.Entry.Id == id && item.Entry.SubscriptionType == subscriptionType && item.Entry.WorkKind == workKind));
                                Require(target.Text == "new draft" && !error.IsVisible, "Successful add preserves edits made during the request");
                                Require(manager.GetBySubscriptionKey(id, subscriptionType, workKind)!.Name == LongName, "The original subscription stores resolved metadata");
                                if (subscriptionType != WorkSubscriptionType.Series)
                                {
                                    var path = subscriptionType == WorkSubscriptionType.Bookmarks
                                        ? workKind == WorkSubscriptionWorkKind.Novel ? "/v1/user/bookmarks/novel" : "/v1/user/bookmarks/illust"
                                        : workKind == WorkSubscriptionWorkKind.Novel ? "/v1/user/novels" : "/v1/user/illusts";
                                    Require(fixture.RequestLog.Select(line => new Uri("https://fixture.invalid" + line[(line.IndexOf(' ') + 1)..])).Any(uri =>
                                        uri.AbsolutePath == path && HttpUtility.ParseQueryString(uri.Query)["user_id"] == id.ToString()
                                        && (subscriptionType != WorkSubscriptionType.Bookmarks || HttpUtility.ParseQueryString(uri.Query)["restrict"] == "public")
                                        && (subscriptionType != WorkSubscriptionType.Posts || workKind == WorkSubscriptionWorkKind.Novel
                                            || HttpUtility.ParseQueryString(uri.Query)["type"] == (workKind == WorkSubscriptionWorkKind.Manga ? "manga" : "illust"))),
                                        "Initial sync preserves the original endpoint, author ID, public bookmark scope and post type");
                                }
                                else
                                    Require(pending.Uri.AbsolutePath == (workKind == WorkSubscriptionWorkKind.Novel ? "/v2/novel/series" : "/v1/illust/series"), "Series work kind selects the matching API");
                            }
                        }
                        Require(manager.Count == 7 && editor.Subscriptions.Count == 7, "All seven supported type/work-kind combinations persist independently");
                        foreach (var presenter in editor.GetVisualDescendants().OfType<UserBasicInfoPresenter>())
                        {
                            InWindow(window, presenter);
                            Require(ToolTip.GetTip(presenter)?.ToString() == LongName && AutomationProperties.GetName(presenter) == LongName, "Full subscription names survive narrow display through help and automation");
                        }
                        Save(window, directory, $"subscription-editor-{theme}-{width}");
                        // Re-adding an existing key updates metadata instead of duplicating it.
                        type.SelectedValue = WorkSubscriptionType.Posts; kind.SelectedValue = WorkSubscriptionWorkKind.Illustration;
                        var existing = editor.Subscriptions.Single(item => item.Entry.SubscriptionType == WorkSubscriptionType.Posts && item.Entry.WorkKind == WorkSubscriptionWorkKind.Illustration);
                        target.Text = existing.Entry.Id.ToString(); Click(window, add);
                        await UntilAsync(() => add.IsEnabled && target.Text == "" && !service.IsSyncInProgress);
                        Require(manager.Count == 7, "Repeated add keeps the existing subscription identity");
                        foreach (var failureType in new[] { WorkSubscriptionType.Posts, WorkSubscriptionType.Series })
                        {
                            type.SelectedValue = failureType; kind.SelectedValue = WorkSubscriptionWorkKind.Illustration;
                            target.Text = "998700"; blockNext = 1; Click(window, add);
                            var pending = await NextAsync(requests); pending.Response.SetResult("{}");
                            await UntilAsync(() => add.IsEnabled); await RenderAsync(window);
                            Require(error.IsVisible && target.Text == "998700" && manager.Count == 7, "Unavailable user/series cannot create a default subscription and retains the draft");
                        }
                        // Account changes cannot publish a previous account's pending metadata.
                        type.SelectedValue = WorkSubscriptionType.Posts; target.Text = "998701"; blockNext = 1; Click(window, add);
                        var oldAccount = await NextAsync(requests);
                        fixture.AuthResponseFactory = (_, _) => Task.FromResult(JsonSerializer.Serialize(TokenResponse.CreateDefault() with
                        {
                            AccessToken = "fixture-new-access",
                            RefreshToken = "fixture-new-refresh",
                            ExpiresIn = 3600,
                            User = TokenUser.CreateDefault() with { Id = 900002, Name = "Second fixture account", Account = "second" }
                        }));
                        await client.SetTokenAsync("fixture-new-account"); await client.ValidateTokenAsync();
                        oldAccount.Response.SetResult(Metadata(oldAccount.Uri));
                        await UntilAsync(() => add.IsEnabled);
                        Require(manager.Count == 7 && target.Text == "998701", "Old account metadata cannot persist or clear the current editor");
                        fixture.AuthResponseFactory = null; await client.SetTokenAsync("fixture-subscription-editor"); await client.ValidateTokenAsync();
                        // Rebinding cancels the old add; its finally cannot unlock a newer request.
                        target.Text = "998702"; blockNext = 1; Click(window, add); var oldContext = await NextAsync(requests);
                        editor.Entry = new WorkSubscriptionsSettingsEntry(group => group.WorkSubscriptions);
                        Require(oldContext.Token.IsCancellationRequested, "Changing editor context cancels the old metadata request");
                        target.Text = "998703"; blockNext = 1; Click(window, add); var newContext = await NextAsync(requests);
                        oldContext.Response.SetResult(Metadata(oldContext.Uri)); await RenderAsync(window);
                        Require(!add.IsEnabled && manager.Count == 7, "Old completion cannot unlock or persist into the replacement context");
                        newContext.Response.SetResult("{}"); await UntilAsync(() => add.IsEnabled);
                        await UntilAsync(() => editor.Subscriptions.Count == 7);
                        // Single sync belongs to the service and survives an editor close/reopen.
                        var item = editor.Subscriptions.Single(item => item.Entry.SubscriptionType == WorkSubscriptionType.Posts && item.Entry.WorkKind == WorkSubscriptionWorkKind.Illustration);
                        var sync = RowButton(editor, item.Entry.HistoryEntryId, "SyncSubscriptionButton");
                        sync.BringIntoView(); await RenderAsync(window); blockNext = 1; Click(window, sync);
                        var syncRequest = await NextAsync(requests);
                        host.Content = null; await RenderAsync(window);
                        Require(!syncRequest.Token.IsCancellationRequested && service.IsSyncInProgress, "Closing the editor does not cancel an already queued subscription sync");
                        host.Content = scroll; await RenderAsync(window); syncRequest.Response.SetResult(Metadata(syncRequest.Uri));
                        await UntilAsync(() => !service.IsSyncInProgress && editor.Subscriptions.Count == 7);
                        // A delete during metadata refresh cancels that service request and prevents late resurrection.
                        sync = RowButton(editor, item.Entry.HistoryEntryId, "SyncSubscriptionButton");
                        sync.BringIntoView(); await RenderAsync(window); blockNext = 1; Click(window, sync);
                        var deletedRequest = await NextAsync(requests);
                        var remove = RowButton(editor, item.Entry.HistoryEntryId, "DeleteSubscriptionButton");
                        Click(window, remove);
                        await UntilAsync(() => manager.GetByKey(item.Entry.HistoryEntryId) is null);
                        Require(deletedRequest.Token.IsCancellationRequested, "Deleting a syncing subscription cancels its metadata refresh");
                        deletedRequest.Response.SetResult(Metadata(deletedRequest.Uri));
                        await UntilAsync(() => !service.IsSyncInProgress && editor.Subscriptions.Count == 6);
                        Require(manager.GetByKey(item.Entry.HistoryEntryId) is null && App.AppViewModel.HistoryPersistHelper.DownloadManager.QueuedTasks.Count == 0,
                            "Deleted subscription stays absent after late metadata and no downloads are queued");
                        scroll.Offset = default; await RenderAsync(window);
                        var requestsBefore = fixture.RequestLog.Count;
                        Click(window, editor.FindControl<Button>("SyncAllSubscriptionsButton")!);
                        await UntilAsync(() => fixture.RequestLog.Count > requestsBefore && !service.IsSyncInProgress);
                        Require(manager.Count == 6 && App.AppViewModel.HistoryPersistHelper.DownloadManager.QueuedTasks.Count == 0, "Sync all preserves remaining empty subscriptions without duplicate tasks");
                        foreach (var entry in editor.Subscriptions.ToArray())
                            await service.TryRemoveAsync(entry.Entry.HistoryEntryId);
                        editor.Entry = new WorkSubscriptionsSettingsEntry(group => group.WorkSubscriptions);
                        await UntilAsync(() => editor.Subscriptions.Count == 0);
                        Require(manager.Count == 0, "All cleanup remains in the isolated subscription database");
                        report.Add($"PASS {theme}/{width}: seven parameter pairs, invalid/default records, busy/draft/duplicates, account/context isolation, single/all sync, editor lifetime, delete cancellation and late response");
                    }
                    finally { host.Content = null; window.Close(); }
                }
            File.WriteAllLines(Path.Combine(directory, "subscription-contracts.txt"), report);
            Require(fixture.UnexpectedRequests.IsEmpty, "No unexpected HTTP request: " + string.Join(", ", fixture.UnexpectedRequests));
            File.WriteAllLines(Path.Combine(directory, "offline-requests.txt"), fixture.RequestLog);
            Console.WriteLine($"Subscription editor contracts passed: {report.Count} groups.");
        }
        finally
        {
            File.WriteAllLines(Path.Combine(directory, "offline-requests.txt"), fixture.RequestLog);
            foreach (var pending in pendingOperations) pending.Response.TrySetResult("{}");
            await service.CancelAndWaitAsync();
        }
    }

    private static Button RowButton(WorkSubscriptionsSettingsExpander editor, int id, string name) => editor.GetVisualDescendants().OfType<Button>()
        .Single(button => button.Name == name && button.Tag is Pixeval.ViewModels.Settings.WorkSubscriptionItemViewModel item && item.Entry.HistoryEntryId == id);
    private static async Task UntilAsync(Func<bool> predicate)
    {
        var stop = DateTime.UtcNow.AddSeconds(10);
        while (!predicate())
        {
            if (DateTime.UtcNow > stop) throw new TimeoutException("Subscription state did not settle");
            Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(1); await Task.Delay(30);
        }
    }
    private static void InWindow(Window window, Control control)
    {
        var point = control.TranslatePoint(default, window)!.Value;
        Require(control.Bounds.Width > 0 && point.X >= -1 && point.X + control.Bounds.Width <= window.ClientSize.Width + 1,
            $"Subscription {control.Name} fits window: {point} {control.Bounds.Size}");
    }
    private static void Save(Window window, string directory, string name)
    { using var frame = window.CaptureRenderedFrame()!; frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions()); }
    private static void Press(Window window, Key key, PhysicalKey physical)
    { window.KeyPress(key, RawInputModifiers.None, physical, null); window.KeyRelease(key, RawInputModifiers.None, physical, null); }

    private static string EmptyFeed(Uri uri) => uri.AbsolutePath.Contains("novel", StringComparison.Ordinal)
        ? "{\"novels\":[],\"next_url\":null}" : "{\"illusts\":[],\"next_url\":null}";
    private static string Metadata(Uri uri)
    {
        var query = HttpUtility.ParseQueryString(uri.Query);
        if (uri.AbsolutePath == "/v1/user/detail")
            return JsonSerializer.Serialize(SingleUserResponse.CreateDefault() with { UserEntity = SingleUserInfo.CreateDefault() with { Id = long.Parse(query["user_id"]!), Name = LongName, Account = "fixture-subscription" } });
        var novel = uri.AbsolutePath.Contains("novel", StringComparison.Ordinal);
        var id = long.Parse(query[novel ? "series_id" : "illust_series_id"]!);
        return novel
            ? JsonSerializer.Serialize(new { novel_series_detail = NovelSeriesDetail.CreateDefault() with { Id = id, Title = LongName }, novel_series_first_novel = Novel.CreateDefault(), novel_series_latest_novel = Novel.CreateDefault(), novels = Array.Empty<Novel>(), next_url = (string?) null })
            : JsonSerializer.Serialize(new { illust_series_detail = MangaSeriesDetail.CreateDefault() with { Id = id, Title = LongName }, illust_series_first_illust = Illustration.CreateDefault(), illusts = Array.Empty<Illustration>(), next_url = (string?) null });
    }
    private static Task<Pending> NextAsync(Channel<Pending> requests) => requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
    private sealed record Pending(Uri Uri, CancellationToken Token)
    { internal TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); }
    private static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)!.Value;
        Require(window.InputHitTest(point) is Visual hit && (ReferenceEquals(hit, control) || hit.GetVisualAncestors().Contains(control)),
            $"Pointer reaches {control.Name} at {point}, bounds={control.Bounds}; hit={window.InputHitTest(point)}");
        window.MouseMove(point); window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
    }
    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(120); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }
    private static void Require(bool condition, string contract)
    { if (!condition) throw new InvalidOperationException(contract); }
}
