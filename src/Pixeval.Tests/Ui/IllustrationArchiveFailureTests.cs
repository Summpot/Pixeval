using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Mako.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Models.Options;
using Pixeval.Utilities;
using Pixeval.Utilities.IO.Caching;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;
using SkiaSharp;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

[TestClass]
[DoNotParallelize]
[TestCategory("UI")]
public sealed class IllustrationArchiveFailureTests
{
    private const string MediumUri = "https://i.pximg.net/m3e/archive-reader-600x600.zip";
    private const string LargeUri = "https://i.pximg.net/m3e/archive-reader-1920x1080.zip";
    private const string UnrelatedUri = "https://i.pximg.net/m3e/archive-reader-unrelated.png";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("raw-image", false)]
    [DataRow("raw-image", true)]
    [DataRow("invalid-second-entry", false)]
    [DataRow("invalid-second-entry", true)]
    [DataRow("empty", false)]
    [DataRow("empty", true)]
    [DataRow("missing-frame", false)]
    [DataRow("missing-frame", true)]
    [DataRow("extra-frame", false)]
    [DataRow("extra-frame", true)]
    [DataRow("invalid-frame", false)]
    [DataRow("invalid-frame", true)]
    [DataRow("metadata", false)]
    [DataRow("metadata", true)]
    public Task AnimationArchiveFailureReleasesStreamsAndRetries(string scenario, bool continuous) =>
        UiTestSession.RunAsync(TestContext, directory => RunAsync(directory, scenario, continuous));

    private static async Task RunAsync(string directory, string scenario, bool continuous)
    {
        using var fixture = new IllustrationReaderFixture();
        await App.AppViewModel.MakoClient.SetTokenAsync("fixture-archive-recovery");
        await App.AppViewModel.MakoClient.ValidateTokenAsync();
        App.AppViewModel.AppSettings.ApplicationSettings.UseFileCache = true;
        App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = continuous ? BrowseMode.Continuous : BrowseMode.Swipe;
        var red = IllustrationFramePlaybackValidation.Png(SKColors.Red);
        var green = IllustrationFramePlaybackValidation.Png(SKColors.Green);
        var validArchive = CreateZip(red, green);
        var initialBytes = scenario switch
        {
            "raw-image" => red,
            "invalid-second-entry" => CorruptSecondEntry(validArchive),
            "empty" => CreateZip(),
            "missing-frame" => CreateZip(red),
            "extra-frame" => CreateZip(red, green, red),
            "invalid-frame" => CreateZip(red, [1, 2, 3, 4]),
            _ => validArchive
        };
        var servingValidInput = false;
        var imageRequests = 0;
        var metadataRequests = 0;
        string? requestedArchiveUri = null;
        fixture.Http.ImageResponseFactory = (request, _) =>
        {
            Require(request.RequestUri!.AbsoluteUri is MediumUri or LargeUri, "Only the animation archive is requested from this boundary");
            requestedArchiveUri = request.RequestUri.AbsoluteUri;
            imageRequests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(servingValidInput ? validArchive : initialBytes)
            });
        };
        fixture.Http.AnimationResponseFactory = (_, _) =>
        {
            metadataRequests++;
            if (scenario == "metadata" && !servingValidInput)
                throw new HttpRequestException("Fixed unavailable animation metadata");
            return Task.FromResult("""
                {"ugoira_metadata":{"zip_urls":{"medium":"https://i.pximg.net/m3e/archive-reader-600x600.zip"},
                "frames":[{"file":"000000.png","delay":100},{"file":"000001.png","delay":200}]}}
                """);
        };
        using (var unrelated = new MemoryStream(green))
            _ = CacheHelper.TryCacheStream(UnrelatedUri, unrelated);
        var work = IllustrationReaderFixture.Sample(996740, 1) with
        {
            Type = IllustrationType.Ugoira,
            MetaSinglePage = new() { OriginalImageUrl = "https://i.pximg.net/m3e/archive-reader-ugoira0.png" }
        };
        using var allocations = new StreamEvents();
        // Verify the public event source before interpreting an empty event list.
        using (Streams.RentStream()) { }
        Require(allocations.CreatedCount > 0 && allocations.Unreleased.Length == 0, "Pooled-stream diagnostics pair allocation and explicit disposal by ID");
        allocations.Reset();
        var model = new IllustrationViewerPageViewModel(new IllustrationItemViewModel(work), false);
        var page = new IllustrationViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = continuous ? 420 : 700, Height = 760, Content = host.Shell };
        List<string> report = [];
        try
        {
            window.Show();
            await UntilAsync(() => model.CurrentImage?.CurrentPage is { LoadSuccessfully: true } or { LoadErrorMessage: not null });
            await RenderAsync(window);
            var image = model.CurrentImage!.CurrentPage!;
            report.Add($"Before retry: scenario={scenario}; loaded={image.LoadSuccessfully}; error={image.LoadErrorMessage}; frames={image.DisplaySource.FrameCount}; imageRequests={imageRequests}; metadataRequests={metadataRequests}; created={allocations.CreatedCount}; unreleased=[{string.Join(',', allocations.Unreleased)}]");
            Save(window, directory, "archive-before-retry");
            Require(allocations.Unreleased.Length == 0, "Every rejected archive stream is explicitly disposed, including frames extracted before a later entry fails");
            Require(!image.LoadSuccessfully && !image.IsLoading && image.LoadErrorMessage is not null && image.OriginalSource is null,
                "An invalid archive or metadata cannot become a successful animation or an endless loading state");
            var viewer = page.GetVisualDescendants().OfType<SingleImageViewer>().Single(control => ReferenceEquals(control.DataContext, image));
            var retry = viewer.GetVisualDescendants().OfType<Button>().Single(control => control.Name == "PART_RetryButton");
            Require(retry.IsEffectivelyVisible && retry.IsEffectivelyEnabled, "The actual image error presents a reachable retry action");
            if (scenario != "metadata")
            {
                using var cached = CacheHelper.TryGetStream(requestedArchiveUri!);
                Require(cached is not null, "The initial invalid ZIP response exercises the real file cache");
                using var bytes = new MemoryStream();
                cached!.CopyTo(bytes);
                Require(bytes.ToArray().SequenceEqual(initialBytes), "The rejected cache entry contains the original failure input");
            }
            servingValidInput = true;
            var beforeRetry = imageRequests;
            allocations.Reset();
            await ActivateAsync(window, retry);
            if (image.RetryImageCommand.ExecutionTask is { } retryTask)
                await retryTask;
            await UntilAsync(() => !image.IsLoading);
            Require(image.LoadSuccessfully && image.LoadErrorMessage is null && image.OriginalSource is { IsInitialized: true },
                "Retry through the real reader recovers after the archive or metadata is corrected");
            Require(imageRequests == beforeRetry + 1 && image.DisplaySource.Frames.Count == 2
                && image.DisplaySource.Delays.SequenceEqual(new[] { 100, 200 }),
                "Retry refetches exactly the failed archive and preserves all frame-delay pairs");
            using (var unrelated = CacheHelper.TryGetStream(UnrelatedUri))
            {
                Require(unrelated is not null, "Retry preserves unrelated cached images");
                using var bytes = new MemoryStream();
                unrelated!.CopyTo(bytes);
                Require(bytes.ToArray().SequenceEqual(green), "Retry does not rewrite the unrelated cache entry");
            }
            Require(allocations.CreatedCount >= 2 && allocations.Unreleased.Length == 0,
                "Recovered animation decoding explicitly releases every extracted frame stream");
            await RenderAsync(window);
            Save(window, directory, "archive-recovered");
            await IllustrationFramePlaybackValidation.ObserveAsync(window, directory, "archive-recovered-playing", true, 2);
            host.Shell.ClosePage(page);
            Require(model.CurrentImage is null && !image.RetryImageCommand.CanExecute(null), "Production close removes the recovered reader and disables retry");
            Require(fixture.Http.UnexpectedRequests.IsEmpty, "Archive recovery stays within fixed offline responses");
            report.Add($"PASS {scenario}/{(continuous ? "Continuous" : "Swipe")}: visible error, owned-stream disposal, real retry, specific cache invalidation, decoded frame delays and playback");
        }
        finally
        {
            File.WriteAllLines(Path.Combine(directory, "archive-recovery-contracts.txt"), report);
            File.WriteAllLines(Path.Combine(directory, "archive-recovery-requests.txt"), fixture.Http.RequestLog);
            window.Close();
        }
    }

    internal static byte[] CreateZip(params byte[][] images)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
            for (var i = 0; i < images.Length; i++)
            {
                using var entry = archive.CreateEntry($"{i:D6}.png", CompressionLevel.NoCompression).Open();
                entry.Write(images[i]);
            }
        return output.ToArray();
    }

    private static byte[] CorruptSecondEntry(byte[] source)
    {
        var bytes = source.ToArray();
        var entries = 0;
        for (var i = 0; i < bytes.Length - 12; i++)
            if (BitConverter.ToUInt32(bytes, i) == 0x02014b50 && ++entries == 2)
            {
                // A readable first entry allocates before the unsupported second
                // compression method throws during extraction.
                bytes[i + 10] = 99;
                bytes[i + 11] = 0;
                return bytes;
            }
        throw new InvalidOperationException("The fixed archive must contain two central directory entries");
    }

    internal sealed class StreamEvents : EventListener
    {
        private readonly ConcurrentDictionary<string, byte> _created = new();
        private readonly ConcurrentDictionary<string, byte> _disposed = new();
        private readonly ConcurrentDictionary<string, byte> _finalized = new();
        internal StreamEvents()
        {
            foreach (var source in EventSource.GetSources()) Enable(source);
        }
        internal int CreatedCount => _created.Count;
        internal string[] Unreleased => _created.Keys.Where(id => !_disposed.ContainsKey(id) || _finalized.ContainsKey(id)).Order().ToArray();
        internal void Reset() { _created.Clear(); _disposed.Clear(); _finalized.Clear(); }
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (_created is not null) Enable(eventSource);
        }
        private void Enable(EventSource source)
        {
            if (source.Name.Contains("RecyclableMemoryStream", StringComparison.Ordinal))
                EnableEvents(source, EventLevel.Verbose);
        }
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (_created is null || eventData.Payload is not { Count: > 0 } payload || payload[0]?.ToString() is not { } id) return;
            if (eventData.EventName == "MemoryStreamCreated") _created.TryAdd(id, 0);
            else if (eventData.EventName == "MemoryStreamDisposed") _disposed.TryAdd(id, 0);
            else if (eventData.EventName == "MemoryStreamFinalized") _finalized.TryAdd(id, 0);
        }
    }
}
