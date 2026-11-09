using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Web;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Mako.Global.Enum;
using Mako.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Misaki;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Models.Options;
using Pixeval.ViewModels;
using Pixeval.Views.Capability;
using Pixeval.Views.Search;
using Pixeval.Views.Work;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

[TestClass]
[DoNotParallelize]
[TestCategory("UI")]
public sealed class WorkBatchBookmarkTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task DuplicateInputAndSelectionChangesPreserveTheStartedBatch(bool novel) => UiTestSession.RunAsync(TestContext, async directory =>
    {
        await using var fixture = await Fixture.CreateAsync(novel, directory);
        fixture.Select(0, 1);
        await fixture.ActivateAsync();
        var first = await fixture.NextAsync();
        Press(fixture.Window, Key.Enter, PhysicalKey.Enter);
        await RenderAsync(fixture.Window);
        Assert.AreEqual(1, fixture.Requests.Count, "Repeated input cannot start a second batch while its first request is pending.");
        Assert.IsFalse(fixture.Bookmark.IsEffectivelyEnabled, "The pending batch action must expose its unavailable state.");
        fixture.Select(2);
        first.Response.SetResult("{}");
        var second = await fixture.NextAsync();
        fixture.Check(first, 0);
        fixture.Check(second, 1);
        second.Response.SetResult("{}");
        await UntilAsync(() => fixture.Items.Take(2).All(item => !item.AddToBookmarkCommand.IsRunning));
        await RenderAsync(fixture.Window);
        Assert.AreEqual(2, fixture.Requests.Count, "Changing selection does not add or replace the snapshotted work identities.");
        Assert.IsTrue(fixture.Items[0].Entry.IsFavorite && fixture.Items[1].Entry.IsFavorite && !fixture.Items[2].Entry.IsFavorite);
        Assert.IsTrue(fixture.Host.Notifications.Any(n => n.Title ==
            I18NManager.GetResource(WorkContainerResources.AddedAllToBookmarkContentFormatted, 2)));
        Assert.IsTrue(fixture.Bookmark.IsEffectivelyEnabled, "Completion restores the command for the current selection.");
        await fixture.ActivateAsync();
        var next = await fixture.NextAsync();
        fixture.Check(next, 2);
        next.Response.SetResult("{}");
        await UntilAsync(() => !fixture.Items[2].AddToBookmarkCommand.IsRunning);
        Assert.IsTrue(fixture.Items[2].Entry.IsFavorite);
        Save(fixture.Window, directory, "batch-completed");
    });

    [TestMethod]
    [DataRow(false, "navigate")]
    [DataRow(true, "navigate")]
    [DataRow(false, "replace")]
    [DataRow(true, "replace")]
    [DataRow(false, "close")]
    [DataRow(true, "close")]
    public Task LeavingTheSourceStopsUnsentBookmarksAndLateNotifications(bool novel, string departure) => UiTestSession.RunAsync(TestContext, async directory =>
    {
        await using var fixture = await Fixture.CreateAsync(novel, directory);
        fixture.Select(0, 1);
        await fixture.ActivateAsync();
        var first = await fixture.NextAsync();
        switch (departure)
        {
            case "navigate":
                fixture.Host.Shell.NavigateTo(new SearchPage());
                await RenderAsync(fixture.Window);
                Assert.IsInstanceOfType<SearchPage>(fixture.Host.Shell.SelectedPage);
                fixture.Host.CloseDestinationAndReturn();
                break;
            case "replace":
                fixture.Work.SetSource(fixture.Entries.Skip(2).ToArray(), fixture.Type);
                break;
            case "close":
                fixture.Host.Shell.ClosePage(fixture.Origin);
                Assert.IsFalse(fixture.Host.Shell.Pages.Contains(fixture.Origin));
                break;
        }
        await RenderAsync(fixture.Window);
        Request? resumed = null;
        if (departure == "navigate")
        {
            fixture.Select(2);
            await fixture.ActivateAsync();
            resumed = await fixture.NextAsync();
            fixture.Check(resumed, 2);
        }
        first.Response.SetResult("{}");
        await UntilAsync(() => !fixture.Items[0].AddToBookmarkCommand.IsRunning);
        await RenderAsync(fixture.Window);
        Assert.AreEqual(resumed is null ? 1 : 2, fixture.Requests.Count, "An already sent write may finish, but a departed batch cannot submit the next work.");
        Assert.IsFalse(fixture.Items[1].Entry.IsFavorite);
        Assert.IsFalse(fixture.Host.Notifications.Any(), "An obsolete batch cannot notify the returned or replacement page.");
        if (resumed is not null)
        {
            Assert.IsTrue(fixture.Work.IsBookmarkBatchPending, "Completing the previous lifetime cannot release a newer pending batch.");
            resumed.Response.SetResult("{}");
            await UntilAsync(() => !fixture.Items[2].AddToBookmarkCommand.IsRunning);
        }
    });

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public Task FailedBookmarksAreNotCountedAsSuccessfulAndTheBatchCanRetry(bool novel, bool alreadyBookmarked) => UiTestSession.RunAsync(TestContext, async directory =>
    {
        await using var fixture = await Fixture.CreateAsync(novel, directory, firstBookmarked: alreadyBookmarked);
        fixture.Select(0, 1);
        var tags = await fixture.OpenTagsAsync();
        var privacy = tags.GetVisualDescendants().OfType<CheckBox>().Single();
        Assert.IsTrue(privacy.Focus());
        Press(fixture.Window, Key.Space, PhysicalKey.Space);
        tags.SelectedTags.Add(tags.TagsSource.First(t => t.Name == "批量 tag"));
        await ActivateAsync(fixture.Window, tags.FindControl<Button>("ConfirmButton")!);
        var first = await fixture.NextAsync();
        fixture.Check(first, 0, true, "批量 tag");
        first.Response.SetException(new HttpRequestException("Fixed failed bookmark"));
        var second = await fixture.NextAsync();
        fixture.Check(second, 1, true, "批量 tag");
        second.Response.SetResult("{}");
        await UntilAsync(() => !fixture.Work.IsBookmarkBatchPending);
        await RenderAsync(fixture.Window);
        Assert.AreEqual(alreadyBookmarked, fixture.Items[0].Entry.IsFavorite);
        Assert.IsTrue(fixture.Items[1].Entry.IsFavorite);
        Assert.IsTrue(fixture.Host.Notifications.Any(n => n.Title == I18NManager.GetResource(WorkContainerResources.AddedAllToBookmarkContentFormatted, 1)));
        Assert.IsFalse(fixture.Host.Notifications.Any(n => n.Title == I18NManager.GetResource(WorkContainerResources.AddedAllToBookmarkContentFormatted, 2)));
        fixture.Select(0);
        await fixture.ActivateAsync();
        var retry = await fixture.NextAsync();
        fixture.Check(retry, 0);
        retry.Response.SetResult("{}");
        await UntilAsync(() => !fixture.Work.IsBookmarkBatchPending);
        Assert.IsTrue(fixture.Items[0].Entry.IsFavorite);
    });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task PrivateTagPopupKeepsItsOriginalSelectionAndClosesWithThePage(bool novel) => UiTestSession.RunAsync(TestContext, async directory =>
    {
        await using var fixture = await Fixture.CreateAsync(novel, directory);
        fixture.Select(0, 1);
        var tags = await fixture.OpenTagsAsync();
        Assert.AreEqual(fixture.Type, tags.WorkType);
        Assert.AreEqual(0, tags.WorkId);
        fixture.Select(2);
        var privacy = tags.GetVisualDescendants().OfType<CheckBox>().Single();
        Assert.IsTrue(privacy.Focus());
        Press(fixture.Window, Key.Space, PhysicalKey.Space);
        tags.SelectedTags.Add(tags.TagsSource.First(t => t.Name == "批量 tag"));
        await ActivateAsync(fixture.Window, tags.FindControl<Button>("ConfirmButton")!);
        var first = await fixture.NextAsync();
        fixture.Check(first, 0, true, "批量 tag");
        Assert.IsFalse(tags.IsEnabled);
        first.Response.SetResult("{}");
        var second = await fixture.NextAsync();
        fixture.Check(second, 1, true, "批量 tag");
        second.Response.SetResult("{}");
        await UntilAsync(() => TopLevel.GetTopLevel(tags) is null);
        Assert.AreEqual(2, fixture.Requests.Count);
        var loading = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Pending.Add(loading);
        fixture.Http.BookmarkResponseFactory = (_, _) => loading.Task;
        tags = await fixture.OpenTagsAsync(false);
        Assert.IsTrue(tags.IsLoading);
        fixture.Host.Shell.ClosePage(fixture.Origin);
        await RenderAsync(fixture.Window);
        Assert.IsNull(TopLevel.GetTopLevel(tags), "The real page close removes a loading batch popup.");
        loading.SetResult("{\"bookmark_tags\":[],\"next_url\":null}");
        await RenderAsync(fixture.Window);
        Assert.AreEqual(2, fixture.Requests.Count);
    });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task LargeBatchConfirmationPreservesCancelAndSnapshot(bool novel) => UiTestSession.RunAsync(TestContext, async directory =>
    {
        await using var fixture = await Fixture.CreateAsync(novel, directory, 21);
        fixture.Select(Enumerable.Range(0, 20).ToArray());
        await fixture.ActivateAsync();
        Assert.IsTrue(fixture.Window.GetVisualDescendants().OfType<ContentDialog>().Any());
        Press(fixture.Window, Key.Escape, PhysicalKey.Escape);
        await RenderAsync(fixture.Window);
        Assert.AreEqual(0, fixture.Requests.Count, "Cancelling the existing large-selection confirmation sends no writes.");
        Assert.IsTrue(fixture.Window.FocusManager!.GetFocusedElement() is Control { IsEffectivelyEnabled: true, IsEffectivelyVisible: true } focused
            && TopLevel.GetTopLevel(focused) == fixture.Window, "Cancelling restores a live keyboard target after the batch action is enabled again.");
        await fixture.ActivateAsync();
        var dialog = fixture.Window.GetVisualDescendants().OfType<ContentDialog>().Single();
        fixture.Select(20);
        fixture.AutoComplete = true;
        await ActivateAsync(fixture.Window, dialog.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PART_PrimaryButton"));
        await UntilAsync(() => fixture.Requests.Count >= 20 && fixture.Items.Take(20).All(item => !item.AddToBookmarkCommand.IsRunning));
        Assert.AreEqual(20, fixture.Requests.Count);
        Assert.IsTrue(fixture.Items.Take(20).All(item => item.Entry.IsFavorite));
        Assert.IsFalse(fixture.Items[20].Entry.IsFavorite);
        CollectionAssert.AreEqual(fixture.Entries.Take(20).Select(e => e.Id).ToArray(), fixture.Requests.Select(r => r.Id(novel)).ToArray());
    });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task PendingConfirmationIsDismissedWhenItsPageCloses(bool novel) => UiTestSession.RunAsync(TestContext, async directory =>
    {
        await using var fixture = await Fixture.CreateAsync(novel, directory, 20);
        fixture.Select(Enumerable.Range(0, 20).ToArray());
        await fixture.ActivateAsync();
        var dialog = fixture.Window.GetVisualDescendants().OfType<ContentDialog>().Single();
        fixture.Host.Shell.ClosePage(fixture.Origin);
        await RenderAsync(fixture.Window);
        Assert.IsNull(TopLevel.GetTopLevel(dialog));
        Assert.AreEqual(0, fixture.Requests.Count);
        Assert.IsFalse(fixture.Work.IsBookmarkBatchPending);
    });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task EmbeddedContainerAlsoCancelsThroughTheLastTabClose(bool novel) => UiTestSession.RunAsync(TestContext, async directory =>
    {
        await using var fixture = await Fixture.CreateAsync(novel, directory, embedded: true);
        fixture.Select(0, 1);
        await fixture.ActivateAsync();
        var first = await fixture.NextAsync();
        fixture.Host.Shell.ClosePage(fixture.Origin);
        await RenderAsync(fixture.Window);
        Assert.IsNull(fixture.View.DataContext, "Closing a ContentPage must dispose its registered WorkView too.");
        Assert.IsFalse(fixture.Work.IsLoaded, "An empty shell cannot keep the removed page loaded.");
        first.Response.SetResult("{}");
        await UntilAsync(() => !fixture.Items[0].AddToBookmarkCommand.IsRunning);
        await RenderAsync(fixture.Window);
        Assert.AreEqual(1, fixture.Requests.Count);
        Assert.IsFalse(fixture.Host.Notifications.Any());
    });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task ResettingAnExistingEngineCancelsItsOriginalBatch(bool novel) => UiTestSession.RunAsync(TestContext, async directory =>
    {
        await using var fixture = await Fixture.CreateAsync(novel, directory);
        await fixture.UseEngineAsync();
        var originalModel = fixture.Model;
        fixture.Select(0, 1);
        await fixture.ActivateAsync();
        var first = await fixture.NextAsync();
        fixture.Work.ResetEngine(novel
            ? fixture.Entries.Skip(2).Cast<Novel>().ToAsyncEnumerable()
            : fixture.Entries.Skip(2).Cast<Illustration>().ToAsyncEnumerable());
        await RenderAsync(fixture.Window);
        Assert.AreSame(originalModel, fixture.View.DataContext, "This path resets the engine without replacing its view model.");
        first.Response.SetResult("{}");
        await UntilAsync(() => !fixture.Items[0].AddToBookmarkCommand.IsRunning);
        await RenderAsync(fixture.Window);
        Assert.AreEqual(1, fixture.Requests.Count);
        Assert.IsFalse(fixture.Host.Notifications.Any());
    });

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public Task AccountChangesInAnotherWindowStopTheOriginalBatch(bool novel, bool returnToOriginalAccount) => UiTestSession.RunAsync(TestContext, async directory =>
    {
        await using var fixture = await Fixture.CreateAsync(novel, directory);
        fixture.Select(0, 1);
        await fixture.ActivateAsync();
        var first = await fixture.NextAsync();
        Assert.AreEqual("fixture-batch-access-900701", first.AccessToken);
        var otherShell = App.CreateViewContainer();
        otherShell.NavigateTo(new ContentPage { Content = new TextBlock { Text = "Second account window" } });
        var other = new Window { Width = 500, Height = 400, Content = otherShell };
        try
        {
            other.Show();
            await RenderAsync(other);
            await fixture.LoginAsync(900702);
            if (returnToOriginalAccount)
                await fixture.LoginAsync(900701);
            Assert.IsTrue(fixture.Work.IsLoaded);
            Assert.AreSame(fixture.Origin, fixture.Host.Shell.SelectedPage);
            first.Response.SetResult("{}");
            await UntilAsync(() => !fixture.Items[0].AddToBookmarkCommand.IsRunning);
            await RenderAsync(fixture.Window);
            Assert.AreEqual(1, fixture.Requests.Count, "A batch from the previous account cannot send its next work with the new account's token.");
            Assert.IsFalse(fixture.Items[0].Entry.IsFavorite, "The old account's result cannot publish a bookmark into the visible card for another account.");
            Assert.AreEqual(HeartButtonState.Unchecked, fixture.Items[0] switch
            {
                NovelItemViewModel item => item.IsBookmarkedDisplay,
                IllustrationItemViewModel item => item.IsBookmarkedDisplay,
                _ => throw new InvalidOperationException()
            }, "The old pending heart must clear without displaying the departed account's successful result.");
            Assert.IsFalse(fixture.Host.Notifications.Any());
            fixture.Select(2);
            await fixture.ActivateAsync();
            var current = await fixture.NextAsync();
            fixture.Check(current, 2);
            Assert.AreEqual(returnToOriginalAccount ? "fixture-batch-access-900701" : "fixture-batch-access-900702", current.AccessToken);
            current.Response.SetResult("{}");
            await UntilAsync(() => !fixture.Work.IsBookmarkBatchPending);
        }
        finally { other.Close(); }
    });

    [TestMethod]
    public Task SameAccountOAuthRenewalPreservesThePendingBatch() => UiTestSession.RunAsync(TestContext, async directory =>
    {
        await using var fixture = await Fixture.CreateAsync(false, directory, expiresIn: 3);
        fixture.Select(0, 1);
        await fixture.ActivateAsync();
        var first = await fixture.NextAsync();
        fixture.ExpiresIn = 3600;
        await Task.Delay(TimeSpan.FromSeconds(3.1));
        first.Response.SetResult("{}");
        var second = await fixture.NextAsync();
        fixture.Check(second, 1);
        Assert.AreEqual(first.AccessToken, second.AccessToken);
        second.Response.SetResult("{}");
        await UntilAsync(() => !fixture.Work.IsBookmarkBatchPending);
        Assert.AreEqual(2, fixture.Requests.Count);
        Assert.AreEqual(2, fixture.Http.RequestLog.Count(line => line == "POST /auth/token"), "The real OAuth provider renewed the same account between two batch writes.");
        Assert.IsTrue(fixture.Items.Take(2).All(item => item.Entry.IsFavorite));
    });

    private sealed record Request(string Path, string Body, string? AccessToken, TaskCompletionSource<string> Response)
    {
        internal string? Id(bool novel) => HttpUtility.ParseQueryString(Body)[novel ? "novel_id" : "illust_id"];
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private const string ImageUri = "https://i.pximg.net/m3e/batch-bookmark.png";
        private readonly Channel<Request> _requests = Channel.CreateUnbounded<Request>();
        private readonly string _directory;
        internal OfflineSearchFixture Http { get; }
        internal ConcurrentQueue<Request> Requests { get; } = new();
        internal ConcurrentBag<TaskCompletionSource<string>> Pending { get; } = new();
        internal bool AutoComplete { get; set; }
        internal int ExpiresIn { get; set; }
        private long _userId = 900701;
        internal SimpleWorkType Type { get; }
        internal IArtworkInfo[] Entries { get; }
        internal WorkContainer Work { get; }
        internal WorkView View { get; }
        internal IOperableViewViewModel Model { get; private set; }
        internal IWorkViewModel[] Items { get; private set; }
        internal PageTestHost Host { get; }
        internal Page Origin { get; }
        internal Window Window { get; }
        internal CommandBarButton Bookmark { get; }

        private Fixture(bool novel, string directory, int count, bool embedded, int expiresIn, bool firstBookmarked)
        {
            _directory = directory;
            Type = novel ? SimpleWorkType.Novel : SimpleWorkType.Illustration;
            Http = new(App.AppViewModel.MakoClient);
            ExpiresIn = expiresIn;
            Http.AuthResponseFactory = (_, _) => Task.FromResult($$"""
                {"access_token":"fixture-batch-access-{{_userId}}","refresh_token":"fixture-batch-refresh-{{_userId}}",
                 "expires_in":{{ExpiresIn}},"token_type":"bearer","scope":"",
                 "user":{"id":{{_userId}},"name":"Fixture","account":"fixture","mail_address":"",
                  "is_premium":false,"x_restrict":0,"is_mail_authorized":true,"require_policy_agreement":false,
                  "profile_image_urls":{"px_16x16":"","px_50x50":"","px_170x170":""}
                 }
                }
                """);
            using (var image = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png")))
            using (var bytes = new MemoryStream())
            {
                image.CopyTo(bytes);
                Http.ImageResponses[ImageUri] = bytes.ToArray();
            }
            Http.BookmarkResponseFactory = (_, _) => Task.FromResult("{\"bookmark_tags\":[{\"name\":\"批量 tag\",\"count\":2}],\"next_url\":null}");
            Http.WorkCardResponseFactory = async (message, _) =>
            {
                var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                Pending.Add(response);
                var request = new Request(message.RequestUri!.AbsolutePath, await message.Content!.ReadAsStringAsync(), message.Headers.Authorization?.Parameter, response);
                Requests.Enqueue(request);
                await _requests.Writer.WriteAsync(request);
                if (AutoComplete) response.TrySetResult("{}");
                return await response.Task;
            };
            Entries = Enumerable.Range(0, count).Select(index => Sample(novel, index)).ToArray();
            ((WorkBase) Entries[0]).IsFavorite = firstBookmarked;
            var page = embedded ? null : new WorkRelatedPage(0, Type);
            Work = page?.FindControl<WorkContainer>("WorkContainer") ?? new WorkContainer();
            View = Work.FindControl<WorkView>("WorkView")!;
            View.LayoutType = ThumbnailLayoutType.Grid;
            Work.SetSource(Entries, Type);
            Model = (IOperableViewViewModel) View.DataContext!;
            Items = Model.View.ToArray();
            Host = new() { Content = (object?) page ?? Work };
            Origin = Host.Shell.Pages.Single();
            Window = new() { Width = novel ? 360 : 1000, Height = 800, RequestedThemeVariant = novel ? ThemeVariant.Dark : ThemeVariant.Light, Content = Host.Shell };
            Bookmark = Work.FindControl<CommandBarButton>("AddAllToBookmarkButton")!;
        }

        internal static async Task<Fixture> CreateAsync(bool novel, string directory, int count = 3, bool embedded = false, int expiresIn = 3600, bool firstBookmarked = false)
        {
            var fixture = new Fixture(novel, directory, count, embedded, expiresIn, firstBookmarked);
            await fixture.LoginAsync(900701);
            fixture.Window.Show();
            await RenderAsync(fixture.Window);
            Assert.AreSame(fixture.Origin, fixture.Host.Shell.SelectedPage, "The close target is the actual loaded origin, not a pre-template selection.");
            Assert.IsTrue(fixture.Work.IsLoaded);
            fixture.Model.IsSelecting = true;
            await RenderAsync(fixture.Window);
            return fixture;
        }

        internal async Task LoginAsync(long id)
        {
            _userId = id;
            await App.AppViewModel.MakoClient.SetTokenAsync("fixture-work-batch-" + id);
            await App.AppViewModel.MakoClient.ValidateTokenAsync();
        }

        internal async Task UseEngineAsync()
        {
            IWorkViewViewModel model = Type is SimpleWorkType.Novel ? new NovelViewViewModel() : new IllustrationViewViewModel();
            model.ResetEngine(Type is SimpleWorkType.Novel ? Entries.Cast<Novel>().ToAsyncEnumerable() : Entries.Cast<Illustration>().ToAsyncEnumerable());
            Work.SetViewModel(model);
            await RenderAsync(Window);
            await UntilAsync(() => model.View.Count == Entries.Length);
            Model = model;
            Items = model.View.ToArray();
            Model.IsSelecting = true;
            await RenderAsync(Window);
        }

        internal void Select(params int[] indices)
        {
            var list = View.FindControl<WorkListBox>("WorkListBox")!;
            list.Selection.Clear();
            foreach (var index in indices) list.Selection.Select(index);
        }

        internal Task ActivateAsync() => NovelReaderValidation.ActivateAsync(Window, Bookmark);
        internal Task<Request> NextAsync() => _requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

        internal void Check(Request request, int index, bool privately = false, string? tags = null)
        {
            var novel = Type is SimpleWorkType.Novel;
            Assert.AreEqual(novel ? "/v2/novel/bookmark/add" : "/v2/illust/bookmark/add", request.Path);
            Assert.AreEqual(Entries[index].Id, request.Id(novel));
            var payload = HttpUtility.ParseQueryString(request.Body);
            Assert.AreEqual(privately ? "private" : "public", payload["restrict"]);
            Assert.AreEqual(tags, payload["tags[]"]);
        }

        internal async Task<TagSelector> OpenTagsAsync(bool wait = true)
        {
            if (Bookmark.IsInOverflow) await OpenOverflowAsync(Window, Work.FindControl<CommandBar>("SelectionToolBar")!);
            Assert.IsTrue(Bookmark.Focus());
            Press(Window, Key.Apps, PhysicalKey.ContextMenu);
            await RenderAsync(Window);
            var tags = Window.GetVisualDescendants().OfType<TagSelector>().Single();
            if (wait) await UntilAsync(() => !tags.IsLoading);
            return tags;
        }

        public async ValueTask DisposeAsync()
        {
            AutoComplete = true;
            foreach (var response in Pending) response.TrySetResult("{}");
            await RenderAsync(Window);
            Window.Close();
            File.WriteAllLines(Path.Combine(_directory, "bookmark-requests.txt"), Requests.Select(r => r.Path + " " + r.Body + " " + r.AccessToken));
            File.WriteAllLines(Path.Combine(_directory, "offline-requests.txt"), Http.RequestLog);
            Assert.IsTrue(Http.UnexpectedRequests.IsEmpty, string.Join(", ", Http.UnexpectedRequests));
            Http.Dispose();
        }

        private static IArtworkInfo Sample(bool novel, int index) => novel
            ? Novel.CreateDefault() with
            {
                Id = 984001 + index,
                Title = "Batch novel " + index,
                User = UserInfo.CreateDefault() with { Id = 900007, Name = "Fixture author" },
                ThumbnailUrls = new() { Medium = ImageUri, SquareMedium = ImageUri, Large = ImageUri }
            }
            : Illustration.CreateDefault() with
            {
                Id = 984001 + index,
                Title = "Batch illustration " + index,
                Type = IllustrationType.Illustration,
                Width = 600,
                Height = 400,
                PageCount = 1,
                Visible = true,
                SanityLevel = 2,
                User = UserInfo.CreateDefault() with { Id = 900007, Name = "Fixture author" },
                ThumbnailUrls = new() { Medium = ImageUri, SquareMedium = ImageUri, Large = ImageUri },
                MetaSinglePage = new() { OriginalImageUrl = ImageUri }
            };
    }
}
