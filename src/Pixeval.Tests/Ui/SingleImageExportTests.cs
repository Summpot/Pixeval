using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Mako.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Misaki;
using Pixeval.Download;
using Pixeval.Extensions.Common;
using Pixeval.Extensions.Common.FormatProviders;
using Pixeval.Models.Database;
using Pixeval.Models.Download;
using Pixeval.Models.Download.Tasks;
using Pixeval.Models.Extensions;
using Pixeval.Models.Options;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Download;
using SQLite;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

[TestClass]
[DoNotParallelize]
[TestCategory("UI")]
public sealed class SingleImageExportTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("static", false, false)]
    [DataRow("static", true, true)]
    [DataRow("zip", false, false)]
    [DataRow("zip", true, true)]
    [DataRow("frames.gif", false, true)]
    [DataRow("frames.gif", true, false)]
    [DataRow("frames.webp", false, false)]
    [DataRow("frames.webp", true, true)]
    public Task SingleImageExportCancellation(string source, bool lateFailure, bool existingOutput) =>
        UiTestSession.RunAsync(TestContext, directory => RunAsync(directory, source, lateFailure, existingOutput, false));

    [TestMethod]
    [DataRow("static")]
    [DataRow("zip")]
    [DataRow("frames.gif")]
    [DataRow("frames.webp")]
    public Task SingleImageExportNoOverwrite(string source) =>
        UiTestSession.RunAsync(TestContext, directory => RunAsync(directory, source, false, true, true));

    [TestMethod]
    [DataRow("static")]
    [DataRow("zip")]
    [DataRow("frames.gif")]
    [DataRow("frames.webp")]
    public Task SingleImageExportDestinationRace(string source) =>
        UiTestSession.RunAsync(TestContext, directory => RunAsync(directory, source, false, false, true, true));

    [TestMethod]
    [DataRow("short-delays")]
    [DataRow("long-delays")]
    [DataRow("empty-zip")]
    [DataRow("invalid-zip")]
    [DataRow("invalid-second-entry")]
    [DataRow("metadata-failure")]
    public Task SingleZipExportRejectsInvalidInputAndRetries(string scenario) =>
        UiTestSession.RunAsync(TestContext, directory => RunInvalidZipAsync(directory, scenario));

    private static async Task RunInvalidZipAsync(string directory, string scenario)
    {
        using var fixture = new IllustrationReaderFixture();
        var history = App.AppViewModel.HistoryPersistHelper;
        await history.RestoreTask;
        var extensions = App.AppViewModel.AppServiceProvider.GetRequiredService<ExtensionService>();
        var exporter = new Exporter(2);
        using var extensionHost = new ExtensionsHostModel(new Host(exporter), []);
        extensions.HostModels.Add(extensionHost);
        var settings = App.AppViewModel.AppSettings.DownloadSettings;
        settings.DownloadFormats.UgoiraDownloadFormat = UgoiraDownloadFormatToken.Extension(exporter);
        settings.OverwriteDownloadedFile = true;
        const string uri = "https://i.pximg.net/m3e/invalid-single-export.zip";
        var goodZip = CreateZip(2);
        var bytes = scenario switch
        {
            "empty-zip" => CreateZip(0),
            "invalid-zip" => new byte[] { 1, 2, 3, 4 },
            "invalid-second-entry" => CorruptSecondEntry(goodZip),
            _ => goodZip
        };
        fixture.Http.ImageResponses[uri] = bytes;
        var entry = new AnimatedSample(IllustrationReaderFixture.Sample(996491, 1), new Uri(uri), true)
        {
            DelaysOverride = scenario switch
            {
                "short-delays" => [100],
                "long-delays" => [100, 200, 300],
                "metadata-failure" => PreloadableList.ToPreloadableList<int>(_ => Task.FromException<IReadOnlyList<int>>(new InvalidDataException("Fixed metadata failure"))),
                _ => [100, 200]
            }
        };
        var factory = App.AppViewModel.AppServiceProvider.GetRequiredService<IllustrationDownloadTaskFactory>();
        var task = (SingleImageDownloadTaskGroupBase) factory.Create(entry, Path.Combine(directory, "exports", "@{id}.@{ext}"));
        Directory.CreateDirectory(Path.GetDirectoryName(task.Destination)!);
        File.WriteAllText(task.Destination, "Previous complete output");
        var page = new DownloadPage();
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 700, Height = 700, Content = host.Shell };
        List<string> report = [];
        using var allocations = new StreamEvents();
        try
        {
            window.Show();
            await RenderAsync(window);
            // Establish that the public diagnostics report individual allocations
            // and explicit disposal; no reflection or product test hook is used.
            using (Streams.RentStream()) { }
            Require(allocations.CreatedCount > 0 && allocations.Unreleased.Length == 0, "Pooled-stream diagnostics must be enabled and paired by stream ID");
            allocations.Reset();
            history.DownloadManager.QueueTask(task);
            await UntilAsync(() => exporter.Pending is not null || task.CurrentState is DownloadState.Error);
            if (exporter.Pending is not null)
            {
                exporter.Complete(false);
                await UntilAsync(() => task.CurrentState is DownloadState.Completed or DownloadState.Error);
            }
            await RenderAsync(window);
            report.Add($"Before retry: state={task.CurrentState}, provider invocations={exporter.Invocations}, streams created={allocations.CreatedCount}, unreleased=[{string.Join(',', allocations.Unreleased)}]");
            Require(allocations.Unreleased.Length == 0, "Every extracted stream must receive explicit disposal, including frames rejected before provider invocation");
            Require(task.CurrentState == DownloadState.Error && task.ErrorMessage is not null && exporter.Invocations == 0,
                "Invalid ZIP/metadata is rejected before the provider runs, rather than silently truncating frame-delay pairs");
            Require(File.ReadAllText(task.Destination) == "Previous complete output", "Invalid input preserves the previous usable output");
            RequireReleased([], task.Destination);
            RequirePersisted(task, DownloadState.Error);
            Save(window, directory, "single-zip-error");
            var row = page.GetVisualDescendants().OfType<DownloadItem>().Single(control =>
                control.DataContext is DownloadItemViewModel vm && ReferenceEquals(vm.DownloadTask, task));
            var primary = row.FindControl<CommandBarButton>("PrimaryActionButton")!;
            fixture.Http.ImageResponses[uri] = goodZip;
            entry.DelaysOverride = [100, 200];
            allocations.Reset();
            await ActivateAsync(window, primary);
            await UntilAsync(() => exporter.Pending is not null);
            Require(task.CurrentState == DownloadState.Pending && exporter.Invocations == 1,
                "The same task retries with corrected input through its actual row");
            var providerStreams = exporter.Streams;
            exporter.Complete(false);
            await UntilAsync(() => task.CurrentState == DownloadState.Completed);
            Require(File.ReadAllText(task.Destination) == "Complete formatted output" && task.ErrorMessage is null,
                "Corrected ZIP and metadata publish one complete output and clear the earlier error");
            RequireReleased(providerStreams, task.Destination);
            RequirePersisted(task, DownloadState.Completed);
            Require(allocations.CreatedCount >= 2 && allocations.Unreleased.Length == 0,
                "Successful retry also pairs every extracted pooled stream with explicit disposal");
            report.Add($"PASS {scenario}: rejected before provider, original output and SQLite error retained; real retry committed; streams created={allocations.CreatedCount}, unreleased=[]");
            host.Shell.CloseAllPages();
            Require(fixture.Http.UnexpectedRequests.IsEmpty, "Invalid ZIP scenarios only use isolated fixed requests");
        }
        finally
        {
            if (exporter.Pending is not null)
            {
                task.Cancel();
                exporter.Complete(false);
                await UntilAsync(() => !task.IsProcessing);
            }
            File.WriteAllLines(Path.Combine(directory, "single-zip-contracts.txt"), report);
            window.Close();
            extensions.HostModels.Remove(extensionHost);
        }

        byte[] CreateZip(int frames)
        {
            using var buffer = new MemoryStream();
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
                for (var i = 0; i < frames; i++)
                {
                    using var frame = zip.CreateEntry($"{i:D6}.png", CompressionLevel.NoCompression).Open();
                    frame.Write(fixture.Http.ImageResponses[IllustrationReaderFixture.ImageUri]);
                }
            return buffer.ToArray();
        }
    }

    private static byte[] CorruptSecondEntry(byte[] source)
    {
        var bytes = source.ToArray();
        var entries = 0;
        for (var i = 0; i < bytes.Length - 12; i++)
            if (BitConverter.ToUInt32(bytes, i) == 0x02014b50 && ++entries == 2)
            {
                // Unsupported compression on the second central directory entry
                // lets the first frame allocate before extraction fails.
                bytes[i + 10] = 99;
                bytes[i + 11] = 0;
                return bytes;
            }
        throw new InvalidOperationException("The fixed ZIP must have two central directory entries");
    }

    private static async Task RunAsync(string directory, string source, bool lateFailure, bool existingOutput, bool noOverwrite, bool destinationRace = false)
    {
        using var fixture = new IllustrationReaderFixture();
        var history = App.AppViewModel.HistoryPersistHelper;
        await history.RestoreTask;
        var extensions = App.AppViewModel.AppServiceProvider.GetRequiredService<ExtensionService>();
        var exporter = new Exporter(source == "static" ? 1 : source == "zip" ? 2 : 3);
        using var extensionHost = new ExtensionsHostModel(new Host(exporter), []);
        extensions.HostModels.Add(extensionHost);
        var settings = App.AppViewModel.AppSettings.DownloadSettings;
        settings.DownloadFormats.IllustrationDownloadFormat = IllustrationDownloadFormatToken.Extension(exporter);
        settings.DownloadFormats.UgoiraDownloadFormat = UgoiraDownloadFormatToken.Extension(exporter);
        settings.OverwriteDownloadedFile = !noOverwrite;
        var entry = CreateEntry(fixture, source);
        var factory = App.AppViewModel.AppServiceProvider.GetRequiredService<IllustrationDownloadTaskFactory>();
        var task = (SingleImageDownloadTaskGroupBase) factory.Create(entry, Path.Combine(directory, "exports", "@{id}.@{ext}"));
        var destination = task.Destination;
        const string previous = "Existing output must survive cancellation";
        if (existingOutput)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllText(destination, previous);
        }
        var page = new DownloadPage();
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 700, Height = 700, Content = host.Shell };
        List<string> report = [];
        try
        {
            window.Show();
            history.DownloadManager.QueueTask(task);
            await UntilAsync(() => exporter.Pending is not null || task.CurrentState is DownloadState.Completed or DownloadState.Error);
            await RenderAsync(window);
            var row = page.GetVisualDescendants().OfType<DownloadItem>().Single(control =>
                control.DataContext is DownloadItemViewModel vm && ReferenceEquals(vm.DownloadTask, task));
            var primary = row.FindControl<CommandBarButton>("PrimaryActionButton")!;
            if (noOverwrite && !destinationRace)
            {
                Require(task.CurrentState == DownloadState.Completed && exporter.Calls == 0 && task.WasDownloadSkipped
                    && File.ReadAllText(destination) == previous, "No-overwrite keeps the existing file without invoking a format provider");
                RequirePersisted(task, DownloadState.Completed);
                report.Add("PASS existing output skipped without invoking provider");
                return;
            }
            Require(exporter.Pending is not null && task.CurrentState == DownloadState.Pending,
                "A real single-file download remains Pending until its format provider finishes: " + task.CurrentState);
            Require(existingOutput ? File.ReadAllText(destination) == previous : !File.Exists(destination),
                "Pending conversion must not replace an existing output or expose unconverted bytes");
            if (destinationRace)
            {
                File.WriteAllText(destination, previous);
                var raceStreams = exporter.Streams;
                exporter.Complete(false);
                await UntilAsync(() => task.CurrentState == DownloadState.Completed);
                Require(task.WasDownloadSkipped && File.ReadAllText(destination) == previous,
                    "No-overwrite also preserves an output created while the provider was running");
                RequireReleased(raceStreams, destination);
                RequirePersisted(task, DownloadState.Completed);
                report.Add("PASS destination created during conversion, skipped commit and released temporary inputs/output");
                return;
            }
            var streams = exporter.Streams;
            var token = task.GetToken().Token;
            await ActivateAsync(window, primary);
            Require(token.IsCancellationRequested && task.CurrentState == DownloadState.Cancelled && task.IsProcessing
                && !primary.IsEffectivelyEnabled, "The real pending-row action cancels and disables retry until provider acknowledgement");
            task.Reset();
            task.Resume();
            Require(task.GetToken().Token == token && exporter.Calls == 1 && streams.All(stream => stream.GetCanRead()),
                "A stale retry cannot replace the cancellation token or close the provider's streams");
            exporter.Complete(lateFailure);
            await UntilAsync(() => !task.IsProcessing);
            Require(task.CurrentState == DownloadState.Cancelled && task.ErrorMessage is null,
                "Late success/failure remains cancelled without a false error or success");
            Require(existingOutput ? File.ReadAllText(destination) == previous : !File.Exists(destination),
                "Cancelled conversion never commits its late output");
            RequireReleased(streams, destination);
            RequirePersisted(task, DownloadState.Cancelled);
            Save(window, directory, "single-export-cancelled");
            report.Add("PASS pending row, cancel, late acknowledgement, original output, input lifetime and persisted cancellation");

            await RenderAsync(window);
            await ActivateAsync(window, primary);
            await UntilAsync(() => exporter.Calls == 2 && exporter.Pending is not null);
            Require(!task.GetToken().Token.IsCancellationRequested && task.CurrentState == DownloadState.Pending,
                "The actual retry action starts one fresh attempt");
            Require(existingOutput ? File.ReadAllText(destination) == previous : !File.Exists(destination),
                "Retry keeps the previous output until a complete replacement exists");
            streams = exporter.Streams;
            exporter.Complete(true);
            await UntilAsync(() => task.CurrentState == DownloadState.Error);
            await RenderAsync(window);
            Require(task.ErrorMessage?.Contains("Fixed exporter failure", StringComparison.Ordinal) == true,
                "Uncancelled provider failures remain visible and retryable");
            Require(existingOutput ? File.ReadAllText(destination) == previous : !File.Exists(destination),
                "Provider failure preserves the previous output and removes incomplete conversion");
            RequireReleased(streams, destination);
            RequirePersisted(task, DownloadState.Error);
            await ActivateAsync(window, primary);
            await UntilAsync(() => exporter.Calls == 3 && exporter.Pending is not null);
            streams = exporter.Streams;
            exporter.Complete(false);
            await UntilAsync(() => task.CurrentState == DownloadState.Completed);
            Require(File.ReadAllText(destination) == "Complete formatted output", "Successful retry publishes the formatted result");
            RequireReleased(streams, destination);
            RequirePersisted(task, DownloadState.Completed);
            report.Add("PASS real retries, uncancelled provider error, successful commit and independent SQLite state/error/format reread");
            host.Shell.CloseAllPages();
            Require(fixture.Http.UnexpectedRequests.IsEmpty, "Only isolated fixed image requests are used");
        }
        finally
        {
            if (exporter.Pending is not null)
            {
                task.Cancel();
                exporter.Complete(false);
                await UntilAsync(() => !task.IsProcessing);
            }
            File.WriteAllLines(Path.Combine(directory, "single-export-contracts.txt"), report);
            window.Close();
            extensions.HostModels.Remove(extensionHost);
        }
    }

    private static IArtworkInfo CreateEntry(IllustrationReaderFixture fixture, string source)
    {
        var sample = IllustrationReaderFixture.Sample(996490, 1);
        if (source == "static") return sample;
        var uri = "https://i.pximg.net/m3e/single-export." + (source == "zip" ? "zip" : source);
        if (source == "zip")
        {
            using var buffer = new MemoryStream();
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
                for (var i = 0; i < 2; i++)
                {
                    using var frame = zip.CreateEntry($"{i:D6}.png").Open();
                    frame.Write(fixture.Http.ImageResponses[IllustrationReaderFixture.ImageUri]);
                }
            fixture.Http.ImageResponses[uri] = buffer.ToArray();
        }
        else
            fixture.Http.ImageResponses[uri] = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ProgressiveImages", source));
        return new AnimatedSample(sample, new Uri(uri), source == "zip");
    }

    // Fixed external artwork metadata chooses the two production single-file paths.
    // Persistence assertions below concern task state, not rehydration of this fixture type.
    private sealed record AnimatedSample : Illustration, ISingleAnimatedImage
    {
        private readonly Uri _uri;
        private readonly bool _zip;
        internal IPreloadableList<int>? DelaysOverride { get; set; }
        [SetsRequiredMembers]
        internal AnimatedSample(Illustration sample, Uri uri, bool zip) : base(sample)
        {
            _uri = uri;
            _zip = zip;
            Type = IllustrationType.Ugoira;
        }
        SingleAnimatedImageType IAnimatedImageFrame.PreferredAnimatedImageType => _zip ? SingleAnimatedImageType.SingleZipFile : SingleAnimatedImageType.SingleFile;
        Uri? IAnimatedImageFrame.SingleImageUri => _uri;
        IPreloadableList<int>? IAnimatedImageFrame.ZipImageDelays => DelaysOverride ?? (_zip ? [100, 200] : null);
        IPreloadableList<(Uri Uri, int MsDelay)>? IAnimatedImageFrame.MultiImageUris => null;
    }

    private static void RequireReleased(IStream[] streams, string destination)
    {
        Require(streams.All(stream => !stream.GetCanRead()), "Provider acknowledgement releases all input streams");
        Require(Directory.GetFiles(Path.GetDirectoryName(destination)!).All(file => file == destination),
            "No partial source or formatted file survives the attempt");
    }

    private static void RequirePersisted(SingleImageDownloadTaskGroupBase task, DownloadState state)
    {
        var database = App.AppViewModel.AppServiceProvider.GetRequiredService<SQLiteConnection>();
        using var reopened = new SQLiteConnection(database.DatabasePath, SQLiteOpenFlags.ReadOnly);
        var destination = task.DatabaseEntry.Destination;
        var entry = reopened.Table<DownloadHistoryEntry>().Where(record => record.Destination == destination).ToList().Single();
        Require(entry.State == state && entry.ErrorMessage == task.ErrorMessage && entry.FormatToken == task.DatabaseEntry.FormatToken,
            "Independent SQLite reads preserve state, error and format token: " + entry.State);
    }

    private sealed class Exporter(int frames) : IStaticImageFormatProviderExtension, IAnimatedImageFormatProviderExtension
    {
        internal int Calls { get; private set; }
        internal int Invocations { get; private set; }
        internal ITaskCompletionSource? Pending { get; private set; }
        internal IStream[] Streams { get; private set; } = [];
        private string _destination = "";
        public void FormatImage(ITaskCompletionSource task, IStream input, string destinationPath)
        {
            Invocations++;
            Begin(task, [input], destinationPath);
        }
        public void FormatImage(ITaskCompletionSource task, IStream[] imagesKeys, int[] imagesValues, string destinationPath, int imagesCount)
        {
            Invocations++;
            Require(imagesCount == frames && imagesValues.All(delay => delay > 0), "Every decoded frame and positive delay reaches the provider");
            if (frames == 2) Require(imagesValues.SequenceEqual(new[] { 100, 200 }), "ZIP preserves the source frame delays");
            Begin(task, imagesKeys, destinationPath);
        }
        private void Begin(ITaskCompletionSource task, IStream[] streams, string destination)
        {
            Require(Pending is null && streams.Length == frames, "Each attempt owns one provider invocation and the expected frames");
            Calls++;
            Streams = streams;
            _destination = destination;
            File.WriteAllText(destination, "Partial formatted output");
            Pending = task;
        }
        internal void Complete(bool fail)
        {
            var pending = Pending ?? throw new InvalidOperationException("No pending provider");
            Pending = null;
            foreach (var stream in Streams)
            {
                var signature = new byte[8];
                Require(stream.GetCanRead() && stream.Read(signature, 0, 8) == 8
                    && signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
                    "Provider inputs stay readable through cancellation until acknowledgement");
            }
            File.WriteAllText(_destination, "Complete formatted output");
            if (fail) pending.SetException(new InvalidOperationException("Fixed exporter failure"));
            else pending.SetCompleted();
        }
        public string GetFormatExtension() => "single-fixture";
        public string GetFormatDescription() => "Offline single-image exporter";
        public void OnExtensionLoaded() { }
        public void OnExtensionUnloaded() { }
    }

    private sealed class StreamEvents : EventListener
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
            // The pool also emits disposal during finalization. That is a leak,
            // not proof that the owning export explicitly released its stream.
            else if (eventData.EventName == "MemoryStreamFinalized") _finalized.TryAdd(id, 0);
        }
    }

    private sealed class Host(Exporter exporter) : IExtensionsHost
    {
        public string GetExtensionName() => "Single image export fixture";
        public string GetAuthorName() => "Pixeval tests";
        public string GetExtensionLink() => "";
        public string GetHelpLink() => "";
        public string GetDescription() => "Managed format-provider boundary";
        public string GetSdkVersion() => ExtensionService.CurrentVersion;
        public string GetVersion() => "1.0";
        public IExtension[] GetExtensions(out int count) { count = 1; return [exporter]; }
        public byte[]? GetIcon(out int count) { count = 0; return null; }
        public void Initialize(string cultureName, string tempDirectory, string extensionDirectory, ILogger logger) { }
    }
}
