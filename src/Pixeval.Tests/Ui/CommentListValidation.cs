using System;
using System.Collections.Generic;
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
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pixeval.Mako;
using Pixeval.Controls;
using Pixeval.Localization;
using Pixeval.Utilities;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views;
using Pixeval.Views.Works;
using Pixeval.Views.Viewers;
using SkiaSharp;

namespace Pixeval.Tests.Ui;

internal static class CommentListValidation
{
    private const long WorkId = 997000;
    private const string ImageUri = "https://i.pximg.net/m3e/comment-list.png";
    private const string LongName = "评论作者 / A very long author name with Unicode 名称 ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Content = "Plain text **not markdown** (normal) 保留评论与翻译入口。 " +
        "Long comment content remains readable in a narrow pane without overlapping the author or the actions.";

    internal static async Task RunAsync(string directory)
    {
        var client = App.AppViewModel.MakoClient;
        using var fixture = new OfflineSearchFixture(client);
        client.SetRefreshToken("fixture-comment-list");
        await FixtureAuthentication.IdentifyAsync(client);
        using (var bitmap = new SKBitmap(160, 80))
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.CornflowerBlue);
            using var image = SKImage.FromBitmap(bitmap);
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            fixture.ImageResponses[ImageUri] = png.ToArray();
        }
        var requests = Channel.CreateUnbounded<Pending>();
        List<Pending> pending = [];
        fixture.CommentResponseFactory = async (request, token) =>
        {
            var operation = new Pending(request.RequestUri!, request.Content is null ? "" : await request.Content.ReadAsStringAsync(token), token);
            pending.Add(operation);
            await requests.Writer.WriteAsync(operation);
            return await operation.Response.Task;
        };
        List<string> report = [];
        try
        {
            var original = CommentRecord(997001);
            var replacement = CommentRecord(997003);
            using var owner = UiTestSession.Create<CommentsViewViewModel>(SimpleWorkKind.Illustration, WorkId);
            var identityPage = new CommentsPage(owner);
            var identityHost = new PageTestHost { Content = identityPage };
            var window = new Window { Width = 360, Height = 500, Content = identityHost.Shell };
            CommentRecord? deleted = null;
            try
            {
                Pixeval.Common.MaterialAnimation.Initialize(window);
                window.Show();
                await RenderAsync(window);
                (await NextAsync()).Response.SetResult(Page([original]));
                await UntilAsync(() => owner.Source.Count == 1);
                await RenderAsync(window);
                var item = identityPage.GetVisualDescendants().OfType<CommentItem>().Single();
                item.DeleteButtonClick += model => deleted = model;
                item.FindControl<Button>("DeleteButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var operation = await requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
                Require(operation.Uri.AbsolutePath == "/v1/illust/comment/delete", "The original delete entry sends the expected operation");
                item.DataContext = replacement;
                await UntilAsync(() => operation.Token.IsCancellationRequested);
                Require(operation.Token.IsCancellationRequested, "Rebinding cancels the old delete transport");
                item.FindControl<Button>("DeleteButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var replacementOperation = await requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
                CheckDelete(replacementOperation, "illust", replacement.Id);
                operation.Response.SetResult("{}");
                await Task.Delay(200);
                Dispatcher.UIThread.RunJobs();
                Require(deleted is null && !item.FindControl<Button>("DeleteButton")!.IsEnabled,
                    "A rebound comment must not publish a stale deletion or unlock its replacement's pending action");
                replacementOperation.Response.SetResult("{}");
                await UntilAsync(() => item.FindControl<Button>("DeleteButton")!.IsEnabled);
                Require(deleted == replacement, "Only the replacement's own completion can publish its deletion");
                report.Add("PASS deletion identity: rebound item rejects the old completion while its replacement completes independently");
            }
            finally { window.Close(); }

            foreach (var type in new[] { SimpleWorkKind.Illustration, SimpleWorkKind.Novel })
                foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                    foreach (var width in new[] { 360, 1000 })
                    {
                        using var model = UiTestSession.Create<CommentsViewViewModel>(type, WorkId);
                        var page = new CommentsPage(model);
                        var host = new PageTestHost
                        {
                            Content = page,
                            NavigationObserver = target => Require(target.DataContext is UserViewerPageViewModel { Id: 900001 }, "Author navigation retains the original user ID")
                        };
                        var viewWindow = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = host.Shell };
                        var key = $"{type}-{theme.Key}-{width}";
                        try
                        {
                            Pixeval.Common.MaterialAnimation.Initialize(viewWindow);
                            viewWindow.Show();
                            await RenderAsync(viewWindow);
                            var first = await NextAsync();
                            var category = type == SimpleWorkKind.Illustration ? "illust" : "novel";
                            Require(first.Uri.AbsolutePath == $"/v3/{category}/comments"
                                && HttpUtility.ParseQueryString(first.Uri.Query)[category + "_id"] == WorkId.ToString(), "Comments use the original endpoint and work ID");
                            first.Response.SetResult(Page(Enumerable.Range(1, 20).Select(i => CommentRecord(WorkId + i)), "https://app-api.pixiv.net/m3e/comments-next?offset=20"));
                            await UntilAsync(() => model.Source.Count == 20);
                            await RenderAsync(viewWindow);
                            var root = (CommentContainer) page.CurrentPage!;
                            var view = root.GetVisualDescendants().OfType<CommentView>().Single();
                            var list = view.FindControl<ListBox>("ListBox")!;
                            var own = Item(WorkId + 1);
                            var other = Item(WorkId + 2);
                            var delete = own.FindControl<Button>("DeleteButton")!;
                            var replies = own.FindControl<Button>("RepliesButton")!;
                            var author = own.FindControl<Button>("AuthorButton")!;
                            Require(delete.IsVisible && !other.FindControl<Button>("DeleteButton")!.IsVisible && replies.IsVisible,
                                "Only own comments expose deletion; top comments expose replies");
                            Require(AutomationProperties.GetName(delete) == Strings.CommentItemDelete()
                                && AutomationProperties.GetName(replies) == Strings.CommentItemReplies()
                                && AutomationProperties.GetName(author) == LongName, "CommentRecord actions and author have complete readable names");
                            foreach (var button in new[] { delete, replies, author })
                            {
                                var origin = button.TranslatePoint(default, viewWindow)!.Value;
                                Require(button.Bounds.Width >= 30 && button.Bounds.Height >= 30 && origin.X >= 0
                                    && origin.X + button.Bounds.Width <= width + 1, "CommentRecord actions fit in the actual window");
                            }
                            var text = own.FindControl<TextBlock>("CommentText")!;
                            Require(text.Text == Content && text.GetValue(TranslatableTextBlockBehavior.IsEnabledProperty)
                                && text.TextLayout.TextLines.All(line => !line.HasCollapsed), "Plain text, emoji placeholders and the translation behavior are retained without truncation");
                            Require(list.ContainerFromIndex(0)!.Focus(NavigationMethod.Tab), "The first comment accepts Tab traversal");
                            Press(viewWindow, Key.Tab, PhysicalKey.Tab);
                            Require(delete.IsFocused, "Tab enters the comment's delete action");
                            Press(viewWindow, Key.Tab, PhysicalKey.Tab);
                            Require(replies.IsFocused, "Tab reaches replies");
                            Press(viewWindow, Key.Tab, PhysicalKey.Tab);
                            Require(author.IsFocused, "Tab reaches the author");
                            Press(viewWindow, Key.Tab, PhysicalKey.Tab, RawInputModifiers.Shift);
                            Require(replies.IsFocused, "Shift+Tab traverses comment actions backwards");
                            Save(viewWindow, directory, "comments-" + key);

                            other.FindControl<Button>("DeleteButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            Require(!requests.Reader.TryPeek(out _), "A direct hidden delete invocation cannot bypass ownership");
                            await ActivateAsync(delete);
                            var deletion = await NextAsync();
                            CheckDelete(deletion, category, WorkId + 1);
                            Require(!delete.IsEnabled, "Deletion has a visible busy guard");
                            delete.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            Require(!await model.DeleteCommentAsync((CommentRecord)own.DataContext!) && !requests.Reader.TryPeek(out _), "Repeated UI and model actions cannot duplicate deletion");
                            deletion.Response.SetException(new HttpRequestException("Fixed comment deletion failure"));
                            await UntilAsync(() => delete.IsEnabled);
                            Require(model.Source.Count == 20 && host.Notifications.Any(n => n.Title == Strings.CommentItemDeleteFailed()), "Failure retains the comment, reports an error and permits retry");
                            Save(viewWindow, directory, "delete-failed-" + key);
                            await ActivateAsync(delete);
                            (await NextAsync()).Response.SetResult("{}");
                            await UntilAsync(() => model.Source.Count == 19);
                            Require(model.Source.All(entry => entry.Id != WorkId + 1), "Success removes exactly the submitted comment");
                            report.Add("PASS " + key + ": surface/layout, localized actions, permissions, busy deletion, failure/retry and exact removal");

                            var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
                            Require(list.ContainerFromIndex(0)!.Focus(NavigationMethod.Tab), "The first comment accepts keyboard traversal");
                            Press(viewWindow, Key.End, PhysicalKey.End, RawInputModifiers.Control);
                            // ScrollViewer input is the same explicit end-of-list gesture used by this page.
                            if (!requests.Reader.TryPeek(out _))
                            {
                                scroll.Focus(NavigationMethod.Tab);
                                Press(viewWindow, Key.End, PhysicalKey.End, RawInputModifiers.Control);
                            }
                            await RenderAsync(viewWindow);
                            var next = await NextAsync();
                            Require(next.Uri.AbsolutePath == "/m3e/comments-next" && HttpUtility.ParseQueryString(next.Uri.Query)["offset"] == "20", "Scrolling to the end follows the original next_url");
                            next.Response.SetResult(Page(Enumerable.Range(20, 5).Select(i => CommentRecord(WorkId + i) with
                            {
                                Stamp = i == 24 ? new StampInfo() with { StampId = 301, StampUrl = ImageUri } : null
                            })));
                            await UntilAsync(() => model.Source.Count == 23);
                            Require(model.Source.Select(entry => entry.Id).SequenceEqual(Enumerable.Range(2, 23).Select(i => WorkId + i)), "Pagination preserves order and deduplicates the repeated boundary comment");
                            list.ContainerFromIndex(list.ItemCount - 1)!.BringIntoView();
                            await RenderAsync(viewWindow);
                            var sticker = Item(WorkId + 24).FindControl<Image>("StickerImage")!;
                            await UntilAsync(() => sticker.Source is not null);
                            await RenderAsync(viewWindow);
                            Require(sticker.IsEffectivelyVisible && sticker.Stretch == Stretch.Uniform
                                && sticker.Source!.Size.AspectRatio == 2 && sticker.Bounds.Width / sticker.Bounds.Height is > 1.95 and < 2.05,
                                "A received non-square sticker keeps its source and arranged aspect ratio");
                            Save(viewWindow, directory, "received-sticker-" + key);
                            scroll.Offset = default;
                            await RenderAsync(viewWindow);
                            own = Item(WorkId + 3);
                            author = own.FindControl<Button>("AuthorButton")!;
                            fixture.UserResponseFactory = (_, _) => Task.FromResult("{}");
                            await ActivateAsync(author);
                            Require(host.Navigations == 1 && host.LastPageType == typeof(UserViewerPage), "The author action routes once to user details");
                            host.CloseDestinationAndReturn();
                            replies = own.FindControl<Button>("RepliesButton")!;
                            await ActivateAsync(replies);
                            var replyRequest = await NextAsync();
                            Require(replyRequest.Uri.AbsolutePath == $"/v2/{category}/comment/replies"
                                && HttpUtility.ParseQueryString(replyRequest.Uri.Query)["comment_id"] == (WorkId + 3).ToString(), "Reply navigation retains the parent comment ID and type");
                            replyRequest.Response.SetResult(Page([CommentRecord(WorkId + 101)]));
                            await UntilAsync(() => page.CurrentPage is CommentContainer { DataContext: CommentsViewViewModel parent } && parent.Source.Count == 1);
                            await RenderAsync(viewWindow);
                            var replyPage = (CommentContainer) page.CurrentPage!;
                            var replyItem = replyPage.GetVisualDescendants().OfType<CommentItem>().Single();
                            var replyModel = (CommentsViewViewModel) replyPage.DataContext!;
                            Require(!replyModel.AllowsReplies && replyModel.ParentId == WorkId && replyModel.ParentType == type
                                && !replyItem.FindControl<Button>("RepliesButton")!.IsVisible, "Replies retain work context and cannot create a third comment level");
                            replyItem.FindControl<Button>("RepliesButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            Require(page.NavigationStack.Count == 2 && !requests.Reader.TryPeek(out _), "A hidden reply action cannot push a third level");
                            Save(viewWindow, directory, "replies-" + key);
                            await ActivateAsync(replyItem.FindControl<Button>("DeleteButton")!);
                            var replyDelete = await NextAsync();
                            CheckDelete(replyDelete, category, WorkId + 101);
                            replyDelete.Response.SetResult("{}");
                            await UntilAsync(() => ((CommentsViewViewModel) replyPage.DataContext!).Source.Count == 0);
                            Require(model.Source.Count == 23, "Deleting a reply removes only that reply, not its top-level parent");
                            await UntilAsync(() => !page.IsNavigating);
                            var pop = page.PopAsync();
                            await UntilAsync(() => pop.IsCompleted);
                            await pop;
                            await RenderAsync(viewWindow);
                            Require(ReferenceEquals(page.CurrentPage, root) && model.Source.Count == 23 && !requests.Reader.TryPeek(out _), $"Back navigation retains the existing parent list without refetching it: same={ReferenceEquals(page.CurrentPage, root)}, count={model.Source.Count}, request={requests.Reader.TryPeek(out var unexpected)}/{unexpected?.Uri}");
                            report.Add("PASS " + key + ": keyboard pagination/order/deduplication, author, two-level replies and return state");

                            await ActivateAsync(view.FindControl<Button>("RefreshButton")!);
                            (await NextAsync()).Response.SetException(new HttpRequestException("Fixed comment load failure"));
                            await UntilAsync(() => view.IsLoadInterrupted);
                            Require(view.FindControl<TextBlock>("LoadErrorText")!.IsVisible && model.Source.Count == 0
                                && !view.GetVisualDescendants().OfType<EntryView>().Single().HasNoItem,
                                "Interrupted initial loading shows an actionable error rather than a successful empty result");
                            Save(viewWindow, directory, "load-failed-" + key);
                            await ActivateAsync(view.FindControl<Button>("RefreshButton")!);
                            (await NextAsync()).Response.SetResult(Page([]));
                            await UntilAsync(() => !list.GetValue(IncrementalLoadingBehavior.IsLoadingMoreProperty));
                            Require(!view.IsLoadInterrupted && model.Source.Count == 0 && !((IIncrementalLoading)model.View).HasMoreItems, "Successful empty retry clears error and completes the source");
                            report.Add("PASS " + key + ": load failure, explicit retry and empty state");

                            CommentItem Item(long id) => list.GetVisualDescendants().OfType<CommentItem>().Single(entry => entry.DataContext is CommentRecord vm && vm.Id == id);
                        }
                        finally
                        {
                            host.Content = null;
                            viewWindow.Close();
                        }
                    }

            // A pending write belongs to both its original item and its account.
            using (var own = UiTestSession.Create<CommentsViewViewModel>(SimpleWorkKind.Novel, WorkId))
            {
                var lifetimePage = new CommentsPage(own);
                var host = new PageTestHost { Content = lifetimePage };
                CommentItem entry = null!;
                var lifetimeWindow = new Window { Width = 360, Height = 500, Content = host.Shell };
                var events = 0;
                try
                {
                    Pixeval.Common.MaterialAnimation.Initialize(lifetimeWindow);
                    lifetimeWindow.Show();
                    await RenderAsync(lifetimeWindow);
                    (await NextAsync()).Response.SetResult(Page([CommentRecord(997501)]));
                    await UntilAsync(() => own.Source.Count == 1);
                    await RenderAsync(lifetimeWindow);
                    entry = lifetimePage.GetVisualDescendants().OfType<CommentItem>().Single();
                    entry.DeleteButtonClick += _ => events++;
                    await ActivateAsync(entry.FindControl<Button>("DeleteButton")!);
                    var detached = await NextAsync();
                    host.Content = null;
                    await UntilAsync(() => detached.Token.IsCancellationRequested);
                    Require(detached.Token.IsCancellationRequested, "Detaching a comment cancels its pending delete");
                    detached.Response.SetResult("{}");
                    await UntilAsync(() => entry.FindControl<Button>("DeleteButton")!.IsEnabled);
                    Require(events == 0, "A detached item cannot publish a deletion");
                    host.Content = lifetimePage;
                    await RenderAsync(lifetimeWindow);
                    entry = lifetimePage.GetVisualDescendants().OfType<CommentItem>().Single();
                    entry.DeleteButtonClick += _ => events++;
                    await ActivateAsync(entry.FindControl<Button>("DeleteButton")!);
                    var switched = await NextAsync();
                    fixture.AuthResponseFactory = (_, _) => Task.FromResult(NativeFixtureData.Serialize(new TokenResponse { AccessToken = "fixture", RefreshToken = "fixture", TokenType = "Bearer", ExpiresIn = 3600 } with
                    {
                        AccessToken = "fixture-next-access",
                        RefreshToken = "fixture-next-refresh",
                        ExpiresIn = 3600,
                        User = new TokenUser { Id = "", Name = "", Account = "" } with { Id = "900002", Name = "Next fixture account", Account = "fixture-next" }
                    }));
                    client.SetRefreshToken("fixture-next-account");
                    await FixtureAuthentication.IdentifyAsync(client);
                    switched.Response.SetResult("{}");
                    await UntilAsync(() => entry.FindControl<Button>("DeleteButton")!.IsEnabled);
                    Require(events == 0 && !own.IsOwnComment((CommentRecord)entry.DataContext!), "A previous account's late delete cannot alter the current page");
                    entry.FindControl<Button>("DeleteButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Require(!requests.Reader.TryPeek(out _), "The fresh account permission guard rejects an old visible delete action");
                    fixture.AuthResponseFactory = null;
                    client.SetRefreshToken("fixture-comment-list");
                    await FixtureAuthentication.IdentifyAsync(client);
                    await ActivateAsync(entry.FindControl<Button>("DeleteButton")!);
                    var closing = await NextAsync();
                    lifetimeWindow.Close();
                    await UntilAsync(() => closing.Token.IsCancellationRequested);
                    Require(closing.Token.IsCancellationRequested, "Window.Close cancels a pending comment deletion");
                    closing.Response.SetResult("{}");
                    await UntilAsync(() => entry.FindControl<Button>("DeleteButton")!.IsEnabled);
                    Require(events == 0, "The closed window rejects the late deletion");
                    report.Add("PASS deletion lifetime: detach/remount, account switch, permission recheck and actual Window.Close");
                }
                finally { host.Content = null; lifetimeWindow.Close(); }
            }

            using (var old = UiTestSession.Create<CommentsViewViewModel>(SimpleWorkKind.Illustration, WorkId))
            using (var current = UiTestSession.Create<CommentsViewViewModel>(SimpleWorkKind.Novel, WorkId + 1))
            {
                var view = new CommentView { DataContext = old };
                var lifetimeHost = new PageTestHost { Content = view };
                var lifetimeWindow = new Window { Width = 360, Height = 500, Content = lifetimeHost.Shell };
                List<IDisposable> registered = [];
                view.AddHandler(ViewModelDisposal.ViewModelDisposalEvent, (_, e) => registered.Add(e.Disposable));
                try
                {
                    Pixeval.Common.MaterialAnimation.Initialize(lifetimeWindow);
                    lifetimeWindow.Show();
                    await RenderAsync(lifetimeWindow);
                    var stale = await NextAsync();
                    var oldSource = old.Source;
                    view.DataContext = current;
                    stale.Response.SetResult(Page([CommentRecord(997601)], "https://app-api.pixiv.net/m3e/comments-next?stale=true"));
                    var active = await NextAsync();
                    Require(active.Uri.AbsolutePath == "/v3/novel/comments", "A replacement source owns the next request after the old serial load ends");
                    active.Response.SetResult(Page([CommentRecord(997603)]));
                    await UntilAsync(() => current.Source.Count == 1);
                    Require(oldSource.Count == 0 && old.Source.Count == 0 && current.Source[0].Id == 997603
                        && !requests.Reader.TryPeek(out _), "Replaced lists release old items and reject late results and next_url");
                    Require(registered.Contains(old) && registered.Contains(current), "Both initial and replacement models register with the existing page lifetime route");
                    await ActivateAsync(view.FindControl<Button>("RefreshButton")!);
                    var beforeRefresh = await NextAsync();
                    var refreshingSource = current.Source;
                    await ActivateAsync(view.FindControl<Button>("RefreshButton")!);
                    beforeRefresh.Response.SetResult(Page([CommentRecord(997607)], "https://app-api.pixiv.net/m3e/comments-next?refresh-stale=true"));
                    var afterRefresh = await NextAsync();
                    afterRefresh.Response.SetResult(Page([CommentRecord(997609)]));
                    await UntilAsync(() => current.Source.Count == 1);
                    Require(refreshingSource.Count == 0 && current.Source[0].Id == 997609 && !requests.Reader.TryPeek(out _),
                        "A second refresh supersedes the first without publishing its late page or following next_url");
                    await ActivateAsync(view.FindControl<Button>("RefreshButton")!);
                    var closed = await NextAsync();
                    lifetimeWindow.Close();
                    closed.Response.SetResult(Page([CommentRecord(997605)], "https://app-api.pixiv.net/m3e/comments-next?closed=true"));
                    await RenderAsync(lifetimeWindow);
                    Require(current.Source.Count == 0 && !requests.Reader.TryPeek(out _), "Closing the production window rejects a pending feed result and further pagination");
                    report.Add("PASS feed lifetime: replacement, stale source cleanup/next_url rejection and production window disposal");
                }
                finally { lifetimeWindow.Content = null; lifetimeWindow.Close(); }
            }

            Require(fixture.UnexpectedRequests.IsEmpty, "All requests stay within the fixed comment matrix: " + string.Join(", ", fixture.UnexpectedRequests));
            File.WriteAllLines(Path.Combine(directory, "comment-list-contracts.txt"), report);
            Console.WriteLine($"Validated {report.Count} comment-list groups.");
        }
        finally
        {
            foreach (var operation in pending)
                operation.Response.TrySetCanceled();
            File.WriteAllLines(Path.Combine(directory, "comment-list-requests.txt"), fixture.RequestLog);
        }
        return;
        Task<Pending> NextAsync() => requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static CommentRecord CommentRecord(long id) => new CommentRecord() with
    {
        Id = id,
        Comment = Content,
        Date = DateTimeOffset.UnixEpoch.ToString("O"),
        HasReplies = true,
        User = User.CreateDefault() with { Id = id % 2 == 1 ? 900001 : 900002, Name = LongName, ProfileImageUrls = new() { Medium = ImageUri } }
    };
    private static string Page(IEnumerable<CommentRecord> comments, string? next = null) => NativeFixtureData.Serialize(new { comments, next_url = next });
    private static void CheckDelete(Pending request, string category, long id) => Require(request.Uri.AbsolutePath == $"/v1/{category}/comment/delete"
        && HttpUtility.ParseQueryString(request.Body)["comment_id"] == id.ToString(), "Deletion retains its endpoint and exact comment ID");
    private static void Press(Window window, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, physical, null);
        window.KeyRelease(key, modifiers, physical, null);
    }
    private static async Task ActivateAsync(Button button)
    {
        var window = (Window) TopLevel.GetTopLevel(button)!;
        Require(button.Focus(NavigationMethod.Tab), "The comment action is keyboard reachable");
        Press(window, Key.Enter, PhysicalKey.Enter);
        await RenderAsync(window);
    }
    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("CommentRecord list did not reach its expected state");
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        }
    }
    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No comment-list frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }
    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(100);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }
    private static void Require(bool condition, string contract)
    {
        if (!condition) throw new InvalidOperationException(contract);
    }
    private sealed record Pending(Uri Uri, string Body, CancellationToken Token)
    {
        internal TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
