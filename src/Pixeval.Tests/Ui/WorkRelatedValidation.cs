using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Web;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pixeval.Mako;
using Pixeval.Common.Collections;
using Pixeval.Controls;
using Pixeval.ViewModels;
using Pixeval.Views.Works;

namespace Pixeval.Tests.Ui;

internal static class WorkRelatedValidation
{
    internal static async Task RunAsync(string directory)
    {
        var client = App.AppViewModel.MakoClient;
        using var fixture = new OfflineSearchFixture(client);
        var requests = Channel.CreateUnbounded<PendingRelated>();
        ConcurrentBag<PendingRelated> pending = [];
        fixture.RelatedResponseFactory = async (request, _) =>
        {
            var response = new PendingRelated(request.RequestUri!);
            pending.Add(response);
            await requests.Writer.WriteAsync(response);
            // Deliberately finish obsolete HTTP responses after source cancellation.
            return await response.Response.Task;
        };
        client.SetRefreshToken("fixture-related");
        await FixtureAuthentication.IdentifyAsync(client);
        List<string> report = [];
        try
        {
            foreach (var type in new[] { SimpleWorkKind.Illustration, SimpleWorkKind.Novel })
                foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                    foreach (var width in new[] { 360, 1000 })
                    {
                        var key = $"{type}-{theme.Key}-{width}";
                        var page = UiTestSession.Create<WorkRelatedPage>(980007L, type);
                        var works = page.FindControl<WorkContainer>("WorkContainer")!;
                        var view = works.FindControl<WorkView>("WorkView")!;
                        var list = view.FindControl<WorkListBox>("WorkListBox")!;
                        var host = new PageTestHost { Content = page };
                        var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = host.Shell };
                        try
                        {
                            Pixeval.Common.MaterialAnimation.Initialize(window);
                            window.Show();
                            var first = await NextAsync();
                            Validate(first.Uri, type);
                            Require(Loading() && Model().Source.Count == 0, "Related works expose the existing initial loading state");
                            first.Complete(Json(type, [980001, 980002], "https://app-api.pixiv.net/m3e/related-next?offset=2"));
                            var next = await NextAsync();
                            Require(next.Uri.PathAndQuery == "/m3e/related-next?offset=2", "Related works preserve the exact next_url");
                            next.Complete(Json(type, [980002, 980003]));
                            await UntilAsync(() => Model().Source.Count == 3 && !Loading());
                            await RenderAsync(window);
                            Require(Model().RequireAdaptiveGrid == (type == SimpleWorkKind.Novel)
                                && Model().Source.Select(item => ((IWorkEntry)item).Id.ToString()).SequenceEqual(new[] { "980001", "980002", "980003" }),
                                "Related work types, pagination order and duplicate identity handling remain unchanged");
                            Require(view.GetVisualDescendants().OfType<WorkItem>().All(card => (card is NovelItem) == (type == SimpleWorkKind.Novel)),
                                "The related page uses the migrated card for its original work type");
                            Save(window, directory, "related-ready-" + key);
                            page.IsCommandBarCollapsed = true;
                            await RenderAsync(window);
                            Require(works.IsCommandBarCollapsed && !works.FindControl<CommandBar>("RightToolBar")!.IsEffectivelyVisible,
                                "Home preview can collapse the command bar without changing the source");
                            works.IsCommandBarCollapsed = false;
                            await RenderAsync(window);
                            Require(!page.IsCommandBarCollapsed && !requests.Reader.TryPeek(out _), "The command bar binding restores both ways without a duplicate load");

                            await RefreshAsync(window, works);
                            var obsolete = await NextAsync();
                            var obsoleteSource = Model().Source;
                            await RefreshAsync(window, works);
                            Require(!ReferenceEquals(Model().Source, obsoleteSource), "A second refresh replaces the pending source immediately");
                            obsolete.Complete(Json(type, [989999], "https://app-api.pixiv.net/m3e/obsolete-related-next"));
                            var fresh = await NextAsync();
                            Validate(fresh.Uri, type);
                            fresh.Complete(Json(type, [980004]));
                            await UntilAsync(() => Model().Source.Count == 1 && !Loading());
                            Require(obsoleteSource.Count == 0 && ((IWorkEntry)Model().Source.Single()).Id.ToString() == "980004" && !requests.Reader.TryPeek(out _),
                                "Refresh replaces its source and rejects obsolete items and pagination");

                            await RefreshAsync(window, works);
                            var failure = await NextAsync();
                            failure.Response.SetException(new InvalidOperationException("Fixed related-source failure"));
                            await UntilAsync(() => !Loading());
                            Require(Model().Source.Count == 0, "Failed refresh does not retain obsolete works");
                            await RefreshAsync(window, works);
                            var retry = await NextAsync();
                            Validate(retry.Uri, type);
                            retry.Complete(Json(type, []));
                            await UntilAsync(() => !Loading());
                            Require(Model().Source.Count == 0 && !requests.Reader.TryPeek(out _), "Retry reaches the existing empty state with one request");
                            await RenderAsync(window);
                            Save(window, directory, "related-empty-" + key);
                            await RefreshAsync(window, works);
                            var late = await NextAsync();
                            var closedSource = Model().Source;
                            host.Shell.CloseAllPages();
                            late.Complete(Json(type, [989998], "https://app-api.pixiv.net/m3e/obsolete-related-next"));
                            await RenderAsync(window);
                            Require(closedSource.Count == 0 && !requests.Reader.TryPeek(out _), "Closing rejects late related data and follow-up requests");
                            report.Add($"PASS {key}: endpoint/ID/type, pagination, refresh, failure/retry, collapse and late close");
                        }
                        finally
                        {
                            host.Shell.CloseAllPages();
                            window.Close();
                        }

                        var emptyPage = UiTestSession.Create<WorkRelatedPage>(0L, type);
                        var emptyWorks = emptyPage.FindControl<WorkContainer>("WorkContainer")!;
                        var emptyView = emptyWorks.FindControl<WorkView>("WorkView")!;
                        var emptyHost = new PageTestHost { Content = emptyPage };
                        window = new() { Width = width, Height = 720, RequestedThemeVariant = theme, Content = emptyHost.Shell };
                        try
                        {
                            Pixeval.Common.MaterialAnimation.Initialize(window);
                            window.Show();
                            await RenderAsync(window);
                            await RefreshAsync(window, emptyWorks);
                            var emptyModel = (IWorkViewViewModel) emptyView.DataContext!;
                            Require(emptyModel.RequireAdaptiveGrid == (type == SimpleWorkKind.Novel) && emptyModel.Source.Count == 0
                                && !requests.Reader.TryPeek(out _), "An unset work ID preserves its typed empty layout and never contacts the service");
                            report.Add($"PASS empty-{key}: typed empty source and no request on refresh");
                        }
                        finally
                        {
                            emptyView.Dispose();
                            window.Close();
                        }

                        IWorkViewViewModel owner = type == SimpleWorkKind.Novel ? UiTestSession.Create<NovelViewViewModel>() : UiTestSession.Create<IllustrationViewViewModel>();
                        if (type == SimpleWorkKind.Novel)
                            owner.ResetSource(new object[] { Mako.Novel.CreateDefault() with { Id = 980008, Title = "Shared novel preview" } }.ToAsyncEnumerable());
                        else
                            owner.ResetSource(new object[] { Illustration.CreateDefault() with { Id = 980008, Title = "Shared illustration preview" } }.ToAsyncEnumerable());
                        await ((IIncrementalLoading) owner.Source).LoadMoreItemsAsync(20);
                        IWorkViewViewModel clone = type == SimpleWorkKind.Novel
                            ? UiTestSession.Create<NovelViewViewModel>() : UiTestSession.Create<IllustrationViewViewModel>();
                        clone.ResetSource(owner.Source.ToArray().ToAsyncEnumerable());
                        await ((IIncrementalLoading)clone.Source).LoadMoreItemsAsync(20);
                        var sharedSource = owner.Source;
                        var injected = UiTestSession.Create<WorkRelatedPage>(980007L, type, clone);
                        var injectedWorks = injected.FindControl<WorkContainer>("WorkContainer")!;
                        var injectedView = injectedWorks.FindControl<WorkView>("WorkView")!;
                        var injectedHost = new PageTestHost { Content = injected };
                        window = new() { Width = width, Height = 720, RequestedThemeVariant = theme, Content = injectedHost.Shell };
                        try
                        {
                            Pixeval.Common.MaterialAnimation.Initialize(window);
                            window.Show();
                            await RenderAsync(window);
                            Require(ReferenceEquals(injectedView.DataContext, clone) && !ReferenceEquals(clone.Source, sharedSource)
                                && ((IWorkEntry)clone.Source.Single()).Id.ToString() == "980008" && !requests.Reader.TryPeek(out _),
                                "Opening an existing preview adopts its record snapshot without fetching the same data again");
                            await RefreshAsync(window, injectedWorks);
                            var refreshed = await NextAsync();
                            Validate(refreshed.Uri, type);
                            refreshed.Complete(Json(type, [980009]));
                            await UntilAsync(() => clone.Source.Count == 1 && ((IWorkEntry)clone.Source.Single()).Id.ToString() == "980009");
                            var detachedSource = clone.Source;
                            injectedView.Dispose();
                            Require(detachedSource.Count == 0 && ((IWorkEntry)sharedSource.Single()).Id.ToString() == "980008",
                                "Refreshing and closing the adopted clone leave the home preview owner intact");
                            owner.Dispose();
                            Require(sharedSource.Count == 0, "The final preview owner releases its shared source");
                            report.Add($"PASS shared-{key}: preview adoption, refresh identity and independent snapshot disposal");
                        }
                        finally
                        {
                            injectedView.Dispose();
                            owner.Dispose();
                            window.Close();
                        }

                        IWorkViewViewModel Model() => (IWorkViewViewModel) view.DataContext!;
                        bool Loading() => list.GetValue(IncrementalLoadingBehavior.IsLoadingMoreProperty);
                    }
            Require(fixture.UnexpectedRequests.IsEmpty, "Related requests stay inside the offline fixture: " + string.Join(", ", fixture.UnexpectedRequests));
            File.WriteAllLines(Path.Combine(directory, "work-related-contracts.txt"), report);
            File.WriteAllLines(Path.Combine(directory, "work-related-requests.txt"), fixture.RequestLog);
            Console.WriteLine($"Validated {report.Count} related-work groups.");
        }
        finally
        {
            foreach (var response in pending)
                _ = response.Response.TrySetResult("{\"illusts\":[],\"novels\":[],\"next_url\":null}");
        }
        Task<PendingRelated> NextAsync() => requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static void Validate(Uri uri, SimpleWorkKind type)
    {
        var novel = type == SimpleWorkKind.Novel;
        var query = HttpUtility.ParseQueryString(uri.Query);
        Require(uri.AbsolutePath == (novel ? "/v1/novel/related" : "/v2/illust/related")
            && query[novel ? "novel_id" : "illust_id"] == "980007" && (novel || query["filter"] == "for_android"),
            "Related works preserve the original endpoint, work identity and target filter");
    }

    private static string Json(SimpleWorkKind type, long[] ids, string? next = null) => type == SimpleWorkKind.Novel
        ? NativeFixtureData.Serialize(new { novels = ids.Select(id => Mako.Novel.CreateDefault() with { Id = id, Title = "相关小说 / Related novel", User = User.CreateDefault() with { Id = 900007, Name = "Fixture author" } }).ToArray(), next_url = next })
        : NativeFixtureData.Serialize(new { illusts = ids.Select(id => Illustration.CreateDefault() with { Id = id, Title = "相关作品 / Related artwork", User = User.CreateDefault() with { Id = 900007, Name = "Fixture author" }, Width = 640, Height = 480, PageCount = 1 }).ToArray(), next_url = next });

    private static async Task RefreshAsync(Window window, WorkContainer works)
    {
        works.FindControl<CommandBar>("RightToolBar")!.IsOpen = true;
        await RenderAsync(window);
        Require(works.FindControl<CommandBarButton>("RefreshButton")!.Focus(NavigationMethod.Tab), "Refresh remains keyboard reachable in overflow");
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(200);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Related works did not reach the expected state.");
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No related-work frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string contract)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, contract);
    }

    private sealed class PendingRelated(Uri uri)
    {
        internal Uri Uri { get; } = uri;
        internal TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Complete(string json) => Response.SetResult(json);
    }
}
