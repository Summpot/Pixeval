using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Web;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pixeval.Mako;
using Pixeval.Controls;
using Pixeval.Localization;
using Pixeval.ViewModels;
using Pixeval.Views.Works;

namespace Pixeval.Tests.Ui;

internal static class WorkSourcesValidation
{
    internal static async Task RunAsync(string directory)
    {
        var client = App.AppViewModel.MakoClient;
        using var fixture = new OfflineSearchFixture(client);
        var requests = Channel.CreateUnbounded<PendingSource>();
        ConcurrentBag<PendingSource> pending = [];
        fixture.WorkFeedResponseFactory = async (request, _) =>
        {
            var response = new PendingSource(request.RequestUri!);
            pending.Add(response);
            await requests.Writer.WriteAsync(response);
            return await response.Response.Task;
        };
        client.SetRefreshToken("fixture-work-sources");
        await FixtureAuthentication.IdentifyAsync(client);
        List<string> report = [];
        try
        {
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                foreach (var width in new[] { 360, 1000 })
                    foreach (var type in Enum.GetValues<WorkKind>())
                        foreach (var kind in new[] { "recommended", "new", "posts" })
                        {
                            WorkTypeWorksPage page = kind switch
                            {
                                "recommended" => UiTestSession.Create<WorkRecommendedPage>(type),
                                "new" => UiTestSession.Create<WorkNewPage>(type),
                                _ => UiTestSession.Create<WorkPostsPage>(User.CreateDefault() with { Id = 900007, Name = "Fixture author" }, type)
                            };
                            var works = page.FindControl<WorkContainer>("WorkContainer")!;
                            var view = works.FindControl<WorkView>("WorkView")!;
                            var list = view.FindControl<WorkListBox>("WorkListBox")!;
                            var container = new PageTestHost { Content = page };
                            var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = container.Shell };
                            var key = $"{kind}-{type}-{theme.Key}-{width}";
                            try
                            {
                                Pixeval.Common.MaterialAnimation.Initialize(window);
                                window.Show();
                                var first = await NextAsync();
                                Validate(first.Uri, type, kind);
                                first.Complete(Json(type, [970001, 970002]));
                                await UntilAsync(() => Model().Source.Count == 2 && !Loading());
                                await RenderAsync(window);
                                var field = page.FindControl<SymbolComboBox>("WorkTypeComboBox")!;
                                var selector = field.GetVisualDescendants().OfType<ComboBox>().Single();
                                Require(AutomationProperties.GetName(selector) == Strings.HomePageSourceWorkTypeTextBlockText()
                                    && !field.IconMode, "The shared work type exposes a readable selected value and a purpose label");
                                Require(Equals(field.SelectedValue, type) && Model().RequireAdaptiveGrid == (type == WorkKind.Novel),
                                    "An explicit initial work type retains its corresponding list model");
                                var origin = field.TranslatePoint(default, window)!.Value;
                                Require(origin.X >= -1 && origin.X + field.Bounds.Width <= window.ClientSize.Width + 1, "The shared type field stays inside the narrow viewport");
                                foreach (var text in selector.GetVisualDescendants().OfType<TextBlock>().Where(text => !string.IsNullOrEmpty(text.Text)))
                                    Require(text.TextLayout.TextLines.All(line => !line.HasCollapsed) && text.TextLayout.Width <= text.Bounds.Width + 1,
                                        "The selected work kind remains readable in long-language layouts");
                                Require(page.FindControl<CommandBarButton>("AddSubscriptionButton")!.IsVisible == (kind == "posts")
                                    && !page.FindControl<CommandBarButton>("RemoveSubscriptionButton")!.IsVisible,
                                    "Only posts retain the existing add-subscription entry in the empty isolated database");
                                Save(window, directory, "sources-ready-" + key);

                                var originalIndex = selector.SelectedIndex;
                                var otherIndex = originalIndex == 2 ? 1 : originalIndex + 1;
                                var otherType = (WorkKind) field.ItemsSource![otherIndex].Value;
                                var oldModel = Model();
                                var oldSource = oldModel.Source;
                                Require(selector.Focus(NavigationMethod.Tab), "The work kind selector receives keyboard focus");
                                var down = otherIndex > originalIndex;
                                window.KeyPress(down ? Key.Down : Key.Up, RawInputModifiers.None, down ? PhysicalKey.ArrowDown : PhysicalKey.ArrowUp, null);
                                window.KeyRelease(down ? Key.Down : Key.Up, RawInputModifiers.None, down ? PhysicalKey.ArrowDown : PhysicalKey.ArrowUp, null);
                                var switched = await NextAsync();
                                Validate(switched.Uri, otherType, kind);
                                if ((otherType == WorkKind.Novel) != (type == WorkKind.Novel))
                                    Require(!ReferenceEquals(Model(), oldModel) && oldSource.Count == 0, "Crossing the novel boundary releases the old typed model");
                                field.SelectedValue = type;
                                switched.Complete(Json(otherType, [979999], "https://app-api.pixiv.net/m3e/obsolete-source-next"));
                                var restored = await NextAsync();
                                Validate(restored.Uri, type, kind);
                                restored.Complete(Json(type, [970003], "https://app-api.pixiv.net/m3e/feed-next?offset=1"));
                                var next = await NextAsync();
                                Require(next.Uri.PathAndQuery == "/m3e/feed-next?offset=1", "The source follows the exact current next_url");
                                next.Complete(Json(type, [970003, 970004]));
                                await UntilAsync(() => Model().Source.Count == 2 && !Loading());
                                Require(Model().Source.Select(item => ((IWorkEntry)item).Id.ToString()).SequenceEqual(new[] { "970003", "970004" }),
                                    "Restored pagination keeps order, removes duplicate identities and rejects the obsolete response");

                                await RefreshAsync();
                                var empty = await NextAsync();
                                Validate(empty.Uri, type, kind);
                                empty.Complete(Json(type, []));
                                await UntilAsync(() => !Loading());
                                Require(Model().Source.Count == 0 && !requests.Reader.TryPeek(out _), "One refresh reaches the existing empty state without duplicate requests");
                                Save(window, directory, "sources-empty-" + key);
                                await RefreshAsync();
                                var late = await NextAsync();
                                var closedSource = Model().Source;
                                container.Shell.CloseAllPages();
                                late.Complete(Json(type, [979998], "https://app-api.pixiv.net/m3e/obsolete-source-next"));
                                await RenderAsync(window);
                                Require(closedSource.Count == 0 && !requests.Reader.TryPeek(out _), "Closed sources cannot publish late entries or continue pagination");
                                report.Add($"PASS {key}: readable type, original endpoint/parameters, keyboard source switching, next_url/order/de-duplication, refresh/empty, subscription entry and close");

                                IWorkViewViewModel Model() => (IWorkViewViewModel) view.DataContext!;
                                bool Loading() => list.GetValue(IncrementalLoadingBehavior.IsLoadingMoreProperty);
                                async Task RefreshAsync()
                                {
                                    works.FindControl<CommandBar>("RightToolBar")!.IsOpen = true;
                                    await RenderAsync(window);
                                    var refresh = works.FindControl<CommandBarButton>("RefreshButton")!;
                                    Require(refresh.Focus(NavigationMethod.Tab), "Refresh remains reachable through overflow");
                                    window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                                    window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                                }
                            }
                            finally
                            {
                                container.Shell.CloseAllPages();
                                window.Close();
                            }
                        }
            Require(fixture.UnexpectedRequests.IsEmpty, "All shared work source requests stay inside the fixed HTTP matrix");
            File.WriteAllLines(Path.Combine(directory, "work-source-contracts.txt"), report);
            File.WriteAllLines(Path.Combine(directory, "work-source-requests.txt"), fixture.RequestLog);
            Console.WriteLine($"Validated {report.Count} shared work-source groups.");
        }
        finally
        {
            foreach (var request in pending)
                _ = request.Response.TrySetResult("{\"illusts\":[],\"novels\":[],\"next_url\":null}");
        }
        Task<PendingSource> NextAsync() => requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static void Validate(Uri uri, WorkKind type, string kind)
    {
        var novel = type == WorkKind.Novel;
        var expected = kind == "posts" ? (novel ? "/v1/user/novels" : "/v1/user/illusts")
            : $"/v1/{(novel ? "novel" : "illust")}/{kind}";
        var query = HttpUtility.ParseQueryString(uri.Query);
        Require(uri.AbsolutePath == expected && query["filter"] == "for_android", "The original endpoint and default target filter are preserved");
        if (kind == "posts")
            Require(query["user_id"] == "900007" && (novel || query["type"] == (type == WorkKind.Manga ? "manga" : "illust")), "Posts preserve the explicit author and artwork subtype");
        else if (kind == "new" && !novel)
            Require(query["content_type"] == (type == WorkKind.Manga ? "manga" : "illust"), "New works retain their manga/illustration content type");
        else if (kind == "recommended")
            Require(query[novel ? "include_ranking_novels" : "include_ranking_illusts"] == "true" && query["include_privacy_policy"] == "true", "Recommendations retain their ranking/privacy flags");
    }

    private static string Json(WorkKind type, long[] ids, string? next = null) => type == WorkKind.Novel
        ? NativeFixtureData.Serialize(new { novels = ids.Select(id => Mako.Novel.CreateDefault() with { Id = id, Title = "固定小说 / Fixture novel", User = User.CreateDefault() with { Id = 900007, Name = "Fixture author" } }).ToArray(), next_url = next })
        : NativeFixtureData.Serialize(new { illusts = ids.Select(id => Illustration.CreateDefault() with { Id = id, Title = "固定作品 / Fixture artwork", User = User.CreateDefault() with { Id = 900007, Name = "Fixture author" }, Width = 640, Height = 480, PageCount = 1 }).ToArray(), next_url = next });

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(300);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The shared work source did not reach its expected state.");
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No shared source frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string contract)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, contract);
    }

    private sealed class PendingSource(Uri uri)
    {
        internal Uri Uri { get; } = uri;
        internal TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Complete(string json) => Response.SetResult(json);
    }
}
