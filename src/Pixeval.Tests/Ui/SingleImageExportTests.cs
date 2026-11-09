using System;
using System.Collections.Generic;
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
        [SetsRequiredMembers]
        internal AnimatedSample(Illustration sample, Uri uri, bool zip) : base(sample)
        {
            _uri = uri;
            _zip = zip;
            Type = IllustrationType.Ugoira;
        }
        SingleAnimatedImageType IAnimatedImageFrame.PreferredAnimatedImageType => _zip ? SingleAnimatedImageType.SingleZipFile : SingleAnimatedImageType.SingleFile;
        Uri? IAnimatedImageFrame.SingleImageUri => _uri;
        IPreloadableList<int>? IAnimatedImageFrame.ZipImageDelays => _zip ? [100, 200] : null;
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
        internal ITaskCompletionSource? Pending { get; private set; }
        internal IStream[] Streams { get; private set; } = [];
        private string _destination = "";
        public void FormatImage(ITaskCompletionSource task, IStream input, string destinationPath) => Begin(task, [input], destinationPath);
        public void FormatImage(ITaskCompletionSource task, IStream[] imagesKeys, int[] imagesValues, string destinationPath, int imagesCount)
        {
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
