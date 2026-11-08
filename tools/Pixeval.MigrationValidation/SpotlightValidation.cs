using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mako.Model;
using Pixeval.Collections;
using Pixeval.ViewModels;
using Pixeval.Views.Capability;
using Pixeval.Views.Entry;

namespace Pixeval.MigrationValidation;

internal static class SpotlightValidation
{
    private const string LongTitle = "长特辑标题 / A very long spotlight article title ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const string LongCategory = "超长分类说明 / A very long category label";

    internal static async Task RunAsync(string directory)
    {
        var client = App.AppViewModel.MakoClient;
        using var fixture = new OfflineSearchFixture(client);
        var requests = Channel.CreateUnbounded<PendingArticles>();
        ConcurrentBag<PendingArticles> pending = [];
        fixture.SpotlightResponseFactory = async (request, _) =>
        {
            var response = new PendingArticles(request.RequestUri!);
            pending.Add(response);
            await requests.Writer.WriteAsync(response);
            return await response.Response.Task;
        };
        const string imageUri = "https://i.pximg.net/m3e/spotlight.png";
        using (var asset = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png")))
        using (var buffer = new MemoryStream())
        {
            await asset.CopyToAsync(buffer);
            fixture.ImageResponses[imageUri] = buffer.ToArray();
        }
        await client.SetTokenAsync("fixture-spotlight");
        await client.ValidateTokenAsync();
        List<string> report = [];
        try
        {
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                foreach (var width in new[] { 360, 1000 })
                {
                    var page = new SpotlightPage();
                    var view = page.FindControl<SpotlightView>("SpotlightView")!;
                    var model = (SpotlightViewViewModel) view.DataContext!;
                    var container = new SearchCompletionValidation.FixtureViewContainer { Content = page };
                    var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = container };
                    SpotlightViewViewModel? replacement = null;
                    var key = $"{theme.Key}-{width}";
                    try
                    {
                        window.Show();
                        Require(window.Launcher.GetType().Name == "NoopLauncher", "The isolated host must never open the system browser");
                        var initial = await NextAsync();
                        Require(initial.Uri.AbsolutePath == "/v1/spotlight/articles", "The existing spotlight article source is retained");
                        var samples = new[] { Sample(960001, SpotlightCategory.Spotlight), Sample(960002, SpotlightCategory.Tutorial), Sample(960003, SpotlightCategory.Inspiration) with { Thumbnail = "" } };
                        initial.Complete(samples[..2], "https://app-api.pixiv.net/m3e/spotlight-next?offset=2");
                        var next = await NextAsync();
                        Require(next.Uri.PathAndQuery == "/m3e/spotlight-next?offset=2", "Spotlight pagination follows the exact service URL");
                        next.Complete([samples[1], samples[2]]);
                        await UntilAsync(() => model.Source.Count == 3 && !Source(model).IsLoading);
                        Require(model.Source.Select(item => item.Entry.Id).SequenceEqual(new long[] { 960001, 960002, 960003 }), "Spotlight order and de-duplication preserve article identity");
                        await RenderAsync(window);
                        Save(window, directory, "spotlight-before-" + key);
                        var cards = view.GetVisualDescendants().OfType<SpotlightItem>().ToArray();
                        Require(cards[0].Focus(NavigationMethod.Tab), "The first spotlight can receive keyboard focus");
                        for (var step = 0; step < 3 && !cards[1].IsFocused; step++)
                        {
                            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                        }
                        Require(cards[1].IsFocused, "Tab can reach the next spotlight card without leaving the list");
                        for (var step = 0; step < 3 && !cards[0].IsFocused; step++)
                        {
                            window.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
                            window.KeyRelease(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
                        }
                        Require(cards[0].IsFocused, "Shift+Tab returns to the previous spotlight card");
                        foreach (var sample in samples)
                        {
                            var card = view.GetVisualDescendants().OfType<SpotlightItem>().Single(item => item.DataContext is SpotlightItemViewModel row && row.Entry.Id == sample.Id);
                            Require(Equals(card.Tag, sample.ArticleUrl), "The shared external-link action receives the complete service article URL, including its locale/query");
                            Require(AutomationProperties.GetName(card) == LongTitle && Equals(ToolTip.GetTip(card), LongTitle), "Truncated spotlight titles retain the full accessible name and tooltip");
                            var clicks = 0;
                            card.Click += (_, _) => clicks++;
                            card.BringIntoView();
                            await RenderAsync(window);
                            Require(card.Focus(NavigationMethod.Tab), "A spotlight card receives keyboard focus");
                            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
                            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
                            var point = card.TranslatePoint(new Point(25, 25), window)!.Value;
                            window.MouseDown(point, MouseButton.Left);
                            window.MouseUp(point, MouseButton.Left);
                            await RenderAsync(window);
                            Require(clicks == 3, "Enter, Space and pointer each produce exactly one external-link Click");
                            var title = card.FindControl<TextBlock>("TitleText")!;
                            var category = card.FindControl<TextBlock>("CategoryText")!;
                            Require(title.MaxLines == 2 && title.TextWrapping == TextWrapping.Wrap, "The spotlight preview allows two title lines");
                            Require(Equals(ToolTip.GetTip(category), LongCategory), "A long category remains available in full");
                            Bounds(title, card);
                            Bounds(category, card);
                            Require(category.Foreground is ISolidColorBrush { Color: { R: 0, G: 0, B: 0, A: 255 } }, "Category text retains contrast against each original category color in both themes");
                        }
                        Save(window, directory, "spotlight-ready-" + key);

                        var oldSource = model.Source;
                        replacement = new SpotlightViewViewModel();
                        replacement.ResetEngine(client.Computed(new[] { Sample(960004, SpotlightCategory.Spotlight) }.ToAsyncEnumerable()), static (entry, _) => new(entry));
                        view.SetViewModel(replacement);
                        await UntilAsync(() => replacement.Source.Count == 1 && !Source(replacement).IsLoading);
                        await RenderAsync(window);
                        Require(oldSource.Count == 0 && !requests.Reader.TryPeek(out _), "Replacing a spotlight model releases the original source without an unsolicited request");
                        var rebuilt = view.GetVisualDescendants().OfType<SpotlightItem>().Single();
                        Require(Equals(rebuilt.Tag, Sample(960004, SpotlightCategory.Spotlight).ArticleUrl), "A rebuilt spotlight card binds the replacement article URL");
                        var rebuiltClicks = 0;
                        rebuilt.Click += (_, _) => rebuiltClicks++;
                        rebuilt.Focus(NavigationMethod.Tab);
                        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                        Require(rebuiltClicks == 1, "Rebuilding the list retains one keyboard Click per article action");
                        replacement.ResetEngine(client.Spotlight(), static (entry, _) => new(entry));
                        var late = await NextAsync();
                        var closedSource = replacement.Source;
                        container.Content = null;
                        replacement.Dispose();
                        late.Complete([Sample(969999, SpotlightCategory.Spotlight)], "https://app-api.pixiv.net/m3e/obsolete-spotlight-next");
                        await RenderAsync(window);
                        Require(closedSource.Count == 0 && !requests.Reader.TryPeek(out _), "Closing the article list rejects late content and obsolete next_url");
                        report.Add($"PASS {key}: category/title/missing image, exact external URL binding and single Click, next_url/order/de-duplication, replacement and close; OS launch not exercised");
                    }
                    finally
                    {
                        container.Content = null;
                        model.Dispose();
                        replacement?.Dispose();
                        window.Close();
                    }
                }
            Require(fixture.UnexpectedRequests.IsEmpty, "Spotlight requests stayed inside the offline endpoint matrix");
            File.WriteAllLines(Path.Combine(directory, "spotlight-contracts.txt"), report);
            File.WriteAllLines(Path.Combine(directory, "spotlight-requests.txt"), fixture.RequestLog);
            Console.WriteLine($"Validated {report.Count} spotlight groups; external launch remains a native check.");
        }
        finally
        {
            foreach (var request in pending)
                _ = request.Response.TrySetResult("{\"spotlight_articles\":[],\"next_url\":null}");
        }
        Task<PendingArticles> NextAsync() => requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Spotlight Sample(long id, SpotlightCategory category) => new()
        {
            Id = id, Title = LongTitle, PureTitle = LongTitle, Thumbnail = imageUri,
            ArticleUrl = $"https://www.pixivision.net/zh/a/{id}?from=fixture&label=%E4%B8%AD%E6%96%87",
            Category = category, SubcategoryLabel = LongCategory, PublishDate = new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero)
        };
    }

    private static IncrementalLoadingCollection<SpotlightItemViewModel> Source(SpotlightViewViewModel model) => (IncrementalLoadingCollection<SpotlightItemViewModel>) model.Source;

    private static void Bounds(Control control, Control card)
    {
        var origin = control.TranslatePoint(default, card)!.Value;
        Require(control.Bounds.Width > 0 && control.Bounds.Height > 0 && origin.X >= -1 && origin.Y >= -1
            && origin.X + control.Bounds.Width <= card.Bounds.Width + 1 && origin.Y + control.Bounds.Height <= card.Bounds.Height + 1,
            "Spotlight metadata remains inside its card");
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(500);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The spotlight source did not reach its expected state.");
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No spotlight frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string contract)
    {
        if (!condition)
            throw new InvalidOperationException(contract);
    }

    private sealed class PendingArticles(Uri uri)
    {
        internal Uri Uri { get; } = uri;
        internal TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Complete(IReadOnlyList<Spotlight> articles, string? next = null) =>
            Response.SetResult(JsonSerializer.Serialize(new { spotlight_articles = articles, next_url = next }));
    }
}
