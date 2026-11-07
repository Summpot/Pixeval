using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pixeval.Controls;
using Pixeval.Views.Work;

namespace Pixeval.MigrationValidation;

internal static class PerformanceBaseline
{
    private static readonly double[] Positions = [0, 0.1, 0.25, 0.5, 0.75, 0.9, 1, 0.75, 0.5, 0.25, 0];

    internal static async Task ExportAsync(string directory)
    {
        List<PerformanceSample> samples = [];
        List<ClosedSample> closedSamples = [];
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var count in new[] { 1000, 10000 })
                for (var iteration = 0; iteration < 5; iteration++)
                    await RecordAsync(() => MeasureList(directory, theme, count, iteration));
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            for (var iteration = 0; iteration < 5; iteration++)
                await RecordAsync(() => MeasureNovel(directory, theme, iteration));

        for (var pass = 0; pass < 3 && closedSamples.Any(static sample => sample.Window.IsAlive || sample.Content.IsAlive); pass++)
        {
            await Task.Delay(16);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
            Dispatcher.UIThread.RunJobs();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        for (var index = 0; index < samples.Count; index++)
            samples[index] = samples[index] with
            {
                WindowAliveAfterSuite = closedSamples[index].Window.IsAlive,
                ContentAliveAfterSuite = closedSamples[index].Content.IsAlive
            };
        var report = new PerformanceReport(RuntimeInformation.OSDescription, RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeInformation.FrameworkDescription, Environment.ProcessorCount, 1000, 720, Positions,
            "Skia headless CPU rendering plus frame readback. First sample includes JIT/cache warmup; no native GPU/FPS claim.", samples);
        File.WriteAllText(Path.Combine(directory, "performance.json"),
            JsonSerializer.Serialize(report, PerformanceJsonContext.Default.PerformanceReport) + Environment.NewLine);
        Console.WriteLine($"Recorded {samples.Count} workload samples; raw frame times and post-close weak references saved.");
        return;

        async Task RecordAsync(Func<ClosedSample> measure)
        {
            var closed = measure();
            closedSamples.Add(closed);
            // Allow window-close cancellation/dispatcher continuations to release their
            // references before collecting. Collection is outside all timing samples.
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            samples.Add(closed.Sample with
            {
                ManagedBytesAfterCollection = GC.GetTotalMemory(false),
                WindowAliveAfterCollection = closed.Window.IsAlive,
                ContentAliveAfterCollection = closed.Content.IsAlive
            });
            Console.WriteLine($"{closed.Sample.Workload}/{closed.Sample.Theme}/{closed.Sample.Items} sample {closed.Sample.Iteration + 1}/5 complete.");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ClosedSample MeasureList(string directory, ThemeVariant theme, int count, int iteration)
    {
        var startBytes = GC.GetTotalAllocatedBytes();
        var start = Stopwatch.GetTimestamp();
        var items = Enumerable.Range(0, count).Select(static index => $"作品 {index:D5} · Fixture").ToArray();
        List<WorkItem> cards = [];
        var list = new WorkListBox
        {
            ItemsSource = items,
            ItemsPanel = new FuncTemplate<Panel?>(() => new Pixeval.Controls.VirtualizingWrapPanel
            {
                ItemWidth = 160,
                ItemHeight = 120,
                CacheLength = 0
            })
        };
        list.DataTemplates.Add(new FuncDataTemplate<string>((_, _) =>
        {
            var card = new WorkItem { Content = new TextBlock { [!TextBlock.TextProperty] = new Binding(".") } };
            cards.Add(card);
            return card;
        }));
        var window = CreateWindow(theme, list);
        try
        {
            window.Show();
            Render(window);
            var firstFrame = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var scroll = list.Scroll ?? throw new InvalidOperationException("WorkListBox has no scroll owner.");
            if (scroll.Extent.Height <= scroll.Viewport.Height)
                throw new InvalidOperationException("The list fixture did not overflow its viewport.");
            var peak = list.GetRealizedContainers().Count();
            List<double> frames = [];
            foreach (var position in Positions)
            {
                var frameStart = Stopwatch.GetTimestamp();
                scroll.Offset = new(0, (scroll.Extent.Height - scroll.Viewport.Height) * position);
                Render(window);
                frames.Add(Stopwatch.GetElapsedTime(frameStart).TotalMilliseconds);
                var realized = list.GetRealizedContainers().Cast<ListBoxItem>().ToArray();
                peak = Math.Max(peak, realized.Length);
                if (realized.Length is 0 or > 120)
                    throw new InvalidOperationException($"Unbounded or empty realized list: {realized.Length}/{count}.");
                if (position is 0 && list.ContainerFromIndex(0) is null
                    || position is 1 && list.ContainerFromIndex(count - 1) is null)
                    throw new InvalidOperationException("A list endpoint did not realize its requested item.");
                foreach (var container in realized)
                    if (container.Content is not WorkItem { Content: TextBlock text } card || text.Text != card.DataContext as string)
                        throw new InvalidOperationException("A recycled card retained a stale item binding.");
            }
            if (iteration is 0)
                Save(window, Path.Combine(directory, $"list-{count}-{theme.Key}.png"));
            var sample = new PerformanceSample("worklist-text", theme.Key.ToString()!, iteration, count, 0, firstFrame,
                [.. frames], peak, cards.Count, GC.GetTotalAllocatedBytes() - startBytes, 0, false, false, false, false);
            return new(sample, new(window), new(list));
        }
        finally
        {
            list.ItemsSource = null;
            window.Content = null;
            window.Close();
            if (cards.Any(static card => card.DataContext is not null))
                throw new InvalidOperationException("Clearing the list retained a card data context.");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ClosedSample MeasureNovel(string directory, ThemeVariant theme, int iteration)
    {
        var startBytes = GC.GetTotalAllocatedBytes();
        var start = Stopwatch.GetTimestamp();
        var markdown = string.Join("\n\n", Enumerable.Range(0, 500).Select(static index =>
            $"第 {index + 1} 段：迁移固定小说样本。保留中文、日本語、Latin 与 emoji 🌸。**粗体文字**、*斜体文字* 和 `inline code` 不应丢失。"));
        var novel = new NovelMarkdownBox { Markdown = markdown, MarkdownFontSize = 18 };
        var window = CreateWindow(theme, novel);
        try
        {
            window.Show();
            Render(window);
            var firstFrame = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var scroll = novel.GetVisualDescendants().OfType<ScrollViewer>().First();
            if (scroll.Extent.Height <= scroll.Viewport.Height)
                throw new InvalidOperationException("The long novel fixture did not overflow its viewport.");
            List<double> frames = [];
            foreach (var position in Positions)
            {
                var frameStart = Stopwatch.GetTimestamp();
                var target = (scroll.Extent.Height - scroll.Viewport.Height) * position;
                scroll.Offset = new(0, target);
                Render(window);
                if (Math.Abs(scroll.Offset.Y - target) > 0.01)
                    throw new InvalidOperationException("The novel did not reach its requested scroll offset.");
                frames.Add(Stopwatch.GetElapsedTime(frameStart).TotalMilliseconds);
            }
            if (iteration is 0)
                Save(window, Path.Combine(directory, $"novel-{theme.Key}.png"));
            var sample = new PerformanceSample("novel-markdown", theme.Key.ToString()!, iteration, 500, markdown.Length,
                firstFrame, [.. frames], novel.GetVisualDescendants().OfType<Control>().Count(), 0,
                GC.GetTotalAllocatedBytes() - startBytes, 0, false, false, false, false);
            return new(sample, new(window), new(novel));
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    private static Window CreateWindow(ThemeVariant theme, Control content) => new()
    {
        Width = 1000,
        Height = 720,
        RequestedThemeVariant = theme,
        Content = content
    };

    private static void Render(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("No benchmark frame was rendered.");
    }

    private static void Save(Window window, string path)
    {
        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("No benchmark frame was rendered.");
        frame.Save(path, new PngBitmapEncoderOptions());
    }

    private sealed record ClosedSample(PerformanceSample Sample, WeakReference Window, WeakReference Content);
}

internal sealed record PerformanceSample(string Workload, string Theme, int Iteration, int Items, int Characters,
    double FirstFrameMilliseconds, double[] ScrollFrameMilliseconds, int PeakVisualItems, int TemplatesBuilt,
    long AllocatedBytes, long ManagedBytesAfterCollection, bool WindowAliveAfterCollection, bool ContentAliveAfterCollection,
    bool WindowAliveAfterSuite, bool ContentAliveAfterSuite);

internal sealed record PerformanceReport(string OperatingSystem, string Architecture, string Runtime, int Processors,
    int Width, int Height, double[] ScrollPositions, string Measurement, List<PerformanceSample> Samples);

[JsonSerializable(typeof(PerformanceReport))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class PerformanceJsonContext : JsonSerializerContext;
