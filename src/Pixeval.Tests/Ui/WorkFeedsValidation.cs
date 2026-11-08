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
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mako.Global.Enum;
using Mako.Model;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.ViewModels;
using Pixeval.Views.Capability;
using Pixeval.Views.Work;

namespace Pixeval.Tests.Ui;

internal static class WorkFeedsValidation
{
    internal static async Task RunAsync(string directory)
    {
        var client = App.AppViewModel.MakoClient;
        using var fixture = new OfflineSearchFixture(client);
        var requests = Channel.CreateUnbounded<PendingFeed>();
        ConcurrentBag<PendingFeed> pending = [];
        fixture.WorkFeedResponseFactory = async (request, _) =>
        {
            var response = new PendingFeed(request.RequestUri!);
            pending.Add(response);
            await requests.Writer.WriteAsync(response);
            return await response.Response.Task;
        };
        const string imageUri = "https://i.pximg.net/m3e/feeds.png";
        using (var asset = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png")))
        using (var buffer = new MemoryStream())
        {
            await asset.CopyToAsync(buffer);
            fixture.ImageResponses[imageUri] = buffer.ToArray();
        }
        await client.SetTokenAsync("fixture-work-feeds");
        await client.ValidateTokenAsync();
        List<string> report = [];
        try
        {
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                foreach (var width in new[] { 360, 1000 })
                    foreach (var type in new[] { SimpleWorkType.Illustration, SimpleWorkType.Novel })
                        foreach (var kind in new[] { "following-public", "following-private", "mypixiv" })
                        {
                            var isFollowing = kind != "mypixiv";
                            var privacy = kind == "following-private" ? PrivacyPolicy.Private : PrivacyPolicy.Public;
                            ContentPage page = isFollowing ? new WorkFollowingPage(type, privacy) : new WorkMyPixivPage(type);
                            var works = page.FindControl<WorkContainer>("WorkContainer")!;
                            var view = works.FindControl<WorkView>("WorkView")!;
                            var list = view.FindControl<WorkListBox>("WorkListBox")!;
                            var container = new PageTestHost { Content = page };
                            var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = container.Shell };
                            var key = $"{kind}-{type}-{theme.Key}-{width}";
                            try
                            {
                                window.Show();
                                var first = await NextAsync();
                                Validate(first.Uri, type, privacy);
                                first.Complete(Json(type, [950001, 950002]));
                                await UntilAsync(() => Model().Source.Count == 2 && !Loading());
                                await RenderAsync(window);
                                Require(Model().RequireAdaptiveGrid == (type == SimpleWorkType.Novel), "Each feed keeps the existing illustration/novel list model and layout policy");
                                var picker = page.FindControl<SymbolComboBox>("SimpleWorkTypeComboBox")!;
                                var selector = picker.GetVisualDescendants().OfType<ComboBox>().Single();
                                CheckPicker(picker, HomePageResources.Source.SimpleWorkTypeTextBlock.Text);
                                if (isFollowing)
                                    CheckPicker(page.FindControl<SymbolComboBox>("PrivacyPolicyComboBox")!, HomePageResources.Source.PrivacyPolicyTextBlock.Text);
                                Save(window, directory, "feeds-ready-" + key);

                                await RefreshAsync();
                                var refreshed = await NextAsync();
                                Validate(refreshed.Uri, type, privacy);
                                refreshed.Complete(Json(type, [950003], "https://app-api.pixiv.net/m3e/feed-next?offset=1"));
                                var next = await NextAsync();
                                Require(next.Uri.AbsolutePath == "/m3e/feed-next" && HttpUtility.ParseQueryString(next.Uri.Query)["offset"] == "1", "Feed pagination follows the exact service URL");
                                next.Complete(Json(type, [950003, 950004]));
                                await UntilAsync(() => Model().Source.Count == 2 && !Loading());
                                Require(Model().Source.Select(item => item.Entry.Id).SequenceEqual(new[] { "950003", "950004" }), "Feed pagination preserves order and de-duplicates work identity");

                                if (isFollowing)
                                {
                                    var privacyPicker = page.FindControl<SymbolComboBox>("PrivacyPolicyComboBox")!;
                                    var privacySelector = privacyPicker.GetVisualDescendants().OfType<ComboBox>().Single();
                                    await ActivateAsync(window, privacySelector, privacy == PrivacyPolicy.Public ? Key.Down : Key.Up,
                                        privacy == PrivacyPolicy.Public ? PhysicalKey.ArrowDown : PhysicalKey.ArrowUp);
                                    var switchedPrivacy = await NextAsync();
                                    Validate(switchedPrivacy.Uri, type, privacy == PrivacyPolicy.Public ? PrivacyPolicy.Private : PrivacyPolicy.Public);
                                    privacyPicker.SelectedValue = privacy;
                                    switchedPrivacy.Complete(Json(type, [959999], "https://app-api.pixiv.net/m3e/obsolete-feed-next"));
                                    var restored = await NextAsync();
                                    Validate(restored.Uri, type, privacy);
                                    restored.Complete(Json(type, [950005]));
                                    await UntilAsync(() => Model().Source.Count == 1 && !Loading());
                                    Require(Model().Source.Single().Entry.Id == "950005", "A late private/public source cannot replace the restored feed");
                                }

                                var previous = Model();
                                var oldSource = previous.Source;
                                await ActivateAsync(window, selector, type == SimpleWorkType.Illustration ? Key.Down : Key.Up,
                                    type == SimpleWorkType.Illustration ? PhysicalKey.ArrowDown : PhysicalKey.ArrowUp);
                                var switched = await NextAsync();
                                var otherType = type == SimpleWorkType.Illustration ? SimpleWorkType.Novel : SimpleWorkType.Illustration;
                                Validate(switched.Uri, otherType, privacy);
                                Require(!ReferenceEquals(Model(), previous) && oldSource.Count == 0, "Changing work kind replaces and disposes the previous typed model");
                                switched.Complete(Json(otherType, [950006]));
                                await UntilAsync(() => Model().Source.Count == 1 && !Loading());
                                Require(Model().RequireAdaptiveGrid == (otherType == SimpleWorkType.Novel), "The switched feed uses the new work layout");

                                await RefreshAsync();
                                var failing = await NextAsync();
                                failing.Response.SetException(new InvalidOperationException("Fixed offline feed failure"));
                                await UntilAsync(() => !Loading());
                                await RenderAsync(window);
                                Require(Model().Source.Count == 0, "A failed refresh does not leave old works in the new source");
                                await RefreshAsync();
                                var retry = await NextAsync();
                                Validate(retry.Uri, otherType, privacy);
                                retry.Complete(Json(otherType, []));
                                await UntilAsync(() => !Loading());
                                Require(Model().Source.Count == 0, "Retry can recover to the existing empty state");
                                Save(window, directory, "feeds-empty-" + key);
                                await RefreshAsync();
                                var late = await NextAsync();
                                var closedSource = Model().Source;
                                container.Shell.CloseAllPages();
                                late.Complete(Json(otherType, [959998], "https://app-api.pixiv.net/m3e/obsolete-feed-next"));
                                await RenderAsync(window);
                                Require(closedSource.Count == 0 && !requests.Reader.TryPeek(out _), "Closing a feed rejects its late response and does not follow obsolete pagination");
                                report.Add($"PASS {key}: initial parameters, readable named fields, refresh/pagination, privacy/type switching, failure/retry/empty and disposal");

                                void Validate(Uri uri, SimpleWorkType currentType, PrivacyPolicy currentPrivacy)
                                {
                                    var path = (isFollowing, currentType) switch
                                    {
                                        (true, SimpleWorkType.Illustration) => "/v2/illust/follow",
                                        (true, _) => "/v1/novel/follow",
                                        (false, SimpleWorkType.Illustration) => "/v2/illust/mypixiv",
                                        _ => "/v2/novel/mypixiv"
                                    };
                                    Require(uri.AbsolutePath == path && (!isFollowing || HttpUtility.ParseQueryString(uri.Query)["restrict"] == (currentPrivacy == PrivacyPolicy.Private ? "private" : "public")),
                                        "The work feed preserves its exact endpoint, kind and privacy value");
                                }
                                void CheckPicker(SymbolComboBox field, string resource)
                                {
                                    var combo = field.GetVisualDescendants().OfType<ComboBox>().Single();
                                    Require(AutomationProperties.GetName(combo) == I18NManager.GetResource(resource), "Feed selectors have explicit purpose labels rather than only the selected icon");
                                    Require(!field.IconMode, "Feed choices remain visible as text");
                                    var origin = field.TranslatePoint(default, window)!.Value;
                                    Require(origin.X >= -1 && origin.X + field.Bounds.Width <= window.ClientSize.Width + 1, "The feed header fits within the horizontal viewport");
                                    foreach (var text in combo.GetVisualDescendants().OfType<TextBlock>().Where(text => !string.IsNullOrEmpty(text.Text)))
                                        Require(text.TextLayout.TextLines.All(line => !line.HasCollapsed) && text.TextLayout.Width <= text.Bounds.Width + 1, "The full selected feed value is readable in narrow and long-language layouts");
                                }
                                IWorkViewViewModel Model() => (IWorkViewViewModel) view.DataContext!;
                                bool Loading() => list.GetValue(IncrementalLoadingBehavior.IsLoadingMoreProperty);
                                async Task RefreshAsync()
                                {
                                    works.FindControl<CommandBar>("RightToolBar")!.IsOpen = true;
                                    await RenderAsync(window);
                                    await ActivateAsync(window, works.FindControl<CommandBarButton>("RefreshButton")!, Key.Enter, PhysicalKey.Enter);
                                }
                            }
                            finally
                            {
                                container.Shell.CloseAllPages();
                                window.Close();
                            }
                        }
            Require(fixture.UnexpectedRequests.IsEmpty, "All feed requests stay in the offline endpoint matrix");
            File.WriteAllLines(Path.Combine(directory, "work-feed-contracts.txt"), report);
            File.WriteAllLines(Path.Combine(directory, "work-feed-requests.txt"), fixture.RequestLog);
            Console.WriteLine($"Validated {report.Count} work-feed groups.");
        }
        finally
        {
            foreach (var request in pending)
                _ = request.Response.TrySetResult("{\"illusts\":[],\"novels\":[],\"next_url\":null}");
        }

        Task<PendingFeed> NextAsync() => requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        string Json(SimpleWorkType type, long[] ids, string? next = null) => type == SimpleWorkType.Novel
            ? JsonSerializer.Serialize(new { novels = ids.Select(id => Novel.CreateDefault() with { Id = id, Title = "固定小说 / Fixture novel", User = UserInfo.CreateDefault() with { Id = 900007, Name = "Fixture author" }, ThumbnailUrls = new() { Medium = imageUri, SquareMedium = imageUri, Large = imageUri } }).ToArray(), next_url = next })
            : JsonSerializer.Serialize(new { illusts = ids.Select(id => Illustration.CreateDefault() with { Id = id, Title = "固定插画 / Fixture illustration", User = UserInfo.CreateDefault() with { Id = 900007, Name = "Fixture author" }, Width = 640, Height = 480, PageCount = 1, ThumbnailUrls = new() { Medium = imageUri, SquareMedium = imageUri, Large = imageUri } }).ToArray(), next_url = next });
    }

    private static async Task ActivateAsync(Window window, Control control, Key key, PhysicalKey physical)
    {
        Require(control.Focus(NavigationMethod.Tab), "The feed action is keyboard reachable");
        window.KeyPress(key, RawInputModifiers.None, physical, null);
        window.KeyRelease(key, RawInputModifiers.None, physical, null);
        await RenderAsync(window);
    }

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
                throw new TimeoutException("The feed did not reach its expected state.");
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No feed frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string contract)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, contract);
    }

    private sealed class PendingFeed(Uri uri)
    {
        internal Uri Uri { get; } = uri;
        internal TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Complete(string json) => Response.SetResult(json);
    }
}
