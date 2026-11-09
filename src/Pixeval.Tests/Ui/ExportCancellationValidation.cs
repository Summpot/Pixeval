using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Mako.Model;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Download;
using Pixeval.Extensions.Common;
using Pixeval.Extensions.Common.FormatProviders;
using Pixeval.Models.Database;
using Pixeval.Models.Download;
using Pixeval.Models.Download.Tasks;
using Pixeval.Models.Extensions;
using Pixeval.Models.Options;
using Pixeval.Utilities.IO;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Download;
using Pixeval.Views.Viewers;
using SQLite;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

internal static class ExportCancellationValidation
{
    internal static async Task RunAsync(string directory, bool lateFailure, bool animated = false, bool existingOutput = false)
    {
        using var fixture = new NovelReaderFixture();
        await App.AppViewModel.MakoClient.SetTokenAsync("fixture-cancel-export");
        await App.AppViewModel.MakoClient.ValidateTokenAsync();
        var history = App.AppViewModel.HistoryPersistHelper;
        await history.RestoreTask;
        var extensions = App.AppViewModel.AppServiceProvider.GetRequiredService<ExtensionService>();
        var exporter = new Exporter();
        using var extensionHost = new ExtensionsHostModel(new Host(exporter), []);
        extensions.HostModels.Add(extensionHost);
        var format = NovelDownloadFormatToken.Extension(exporter);
        App.AppViewModel.AppSettings.DownloadSettings.DownloadFormats.NovelDownloadFormat = format;
        App.AppViewModel.AppSettings.DownloadSettings.DownloadFormats.UgoiraDownloadFormat = UgoiraDownloadFormatToken.Extension(exporter);
        App.AppViewModel.AppSettings.DownloadSettings.OverwriteDownloadedFile = true;
        App.AppViewModel.AppSettings.DownloadSettings.DownloadPathMacro = Path.Combine(directory, "exports", "@{id}.@{ext}");
        IWorkViewModel item;
        ContentPage reader;
        Func<bool> ready;
        if (animated)
        {
            const string original = "https://i.pximg.net/m3e/cancel-ugoira0.png";
            const string zipUri = "https://i.pximg.net/m3e/cancel-600x600.zip";
            var png = fixture.Http.ImageResponses[NovelReaderFixture.ImageUri];
            fixture.Http.ImageResponses[IllustrationReaderFixture.ImageUri] = png;
            fixture.Http.ImageResponses[original] = png;
            fixture.Http.ImageResponses[original.Replace("ugoira0", "ugoira1")] = png;
            using (var buffer = new MemoryStream())
            {
                using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
                    for (var i = 0; i < 2; i++)
                    {
                        using var frame = zip.CreateEntry($"{i:D6}.png").Open();
                        frame.Write(png);
                    }
                fixture.Http.ImageResponses[zipUri] = buffer.ToArray();
                fixture.Http.ImageResponses[zipUri.Replace("600x600", "1920x1080")] = buffer.ToArray();
            }
            fixture.Http.AnimationResponseFactory = (_, _) => Task.FromResult("""
                {"ugoira_metadata":{"zip_urls":{"medium":"https://i.pximg.net/m3e/cancel-600x600.zip"},
                "frames":[{"file":"000000.png","delay":100},{"file":"000001.png","delay":200}]}}
                """);
            var work = new IllustrationItemViewModel(IllustrationReaderFixture.Sample(996470, 1) with
            {
                Type = IllustrationType.Ugoira,
                ThumbnailUrls = new() { Medium = NovelReaderFixture.ImageUri, Large = NovelReaderFixture.ImageUri, SquareMedium = NovelReaderFixture.ImageUri },
                MetaSinglePage = new() { OriginalImageUrl = original }
            });
            var model = new IllustrationViewerPageViewModel(work, false);
            item = work;
            reader = new IllustrationViewerPage(model);
            ready = () => model.CurrentImage?.CurrentPage?.LoadSuccessfully == true;
        }
        else
        {
            var work = NovelItemViewModel.CreateInstance(NovelReaderFixture.Sample(993120));
            var model = new NovelViewerPageViewModel(work, false);
            item = work;
            reader = new NovelViewerPage(model);
            ready = () => !model.IsLoading;
        }
        var host = new PageTestHost { Content = reader };
        var window = new Window { Width = 700, Height = 850, Content = host.Shell };
        List<string> report = [];
        try
        {
            window.Show();
            if (!animated)
                (await fixture.NextAsync()).Complete("[uploadedimage:11]\n[pixivimage:994101-1]\nExport after cancel", withImages: true);
            await UntilAsync(ready);
            await RenderAsync(window);
            var save = reader.GetVisualDescendants().OfType<CommandBar>().SelectMany(bar => bar.PrimaryCommands).OfType<CommandBarButton>()
                .Single(button => ReferenceEquals(button.Command, item.SaveCommand));
            var originalFile = Path.Combine(directory, "exports", item.Entry.Id + ".cancel-fixture");
            if (existingOutput)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(originalFile)!);
                File.WriteAllText(originalFile, "Existing output must survive cancellation");
            }
            await ActivateAsync(window, save);
            await UntilAsync(() => exporter.Pending is not null);
            var task = (DownloadTaskGroup) history.DownloadManager.QueuedTasks.Single();
            var paths = animated
                ? (NovelFile: task.OpenLocalDestination, ImageFolderPath: task.OpenLocalDestination + IoHelper.PixevalTempExtension)
                : NovelDownloadTaskGroup.GetOutputPaths(task.TokenizedDestination, format);
            var inputStreams = exporter.Streams;
            Require(task.CurrentState == DownloadState.Pending && inputStreams.Length == (animated ? 2 : 3) && File.Exists(exporter.Destination),
                "The actual queued export is pending with all image streams and a partial temporary file");
            var downloads = new DownloadPage();
            host.Shell.NavigateTo(downloads);
            await RenderAsync(window);
            var row = downloads.GetVisualDescendants().OfType<DownloadItem>().Single(control =>
                control.DataContext is DownloadItemViewModel vm && ReferenceEquals(vm.DownloadTask, task));
            var primary = row.FindControl<CommandBarButton>("PrimaryActionButton")!;
            await ActivateAsync(window, primary);
            var busyAfterCancel = task.IsProcessing && !primary.IsEffectivelyEnabled;
            var cancelledToken = task.GetToken().Token;
            Require(cancelledToken.IsCancellationRequested, "The visible pending-row action cancels the original task token");
            task.Reset();
            task.Resume();
            await RenderAsync(window);
            Require(task.GetToken().Token == cancelledToken && exporter.Calls == 1 && inputStreams.All(stream => stream.GetCanRead()),
                "Stale retry/resume requests cannot replace an active provider's token or release its streams");
            // The ABI has no cancellation entry point: providers must retain readable
            // streams until they acknowledge completion, even after the UI cancels.
            exporter.Complete(lateFailure);
            await UntilAsync(() => !task.IsPending);
            report.Add($"After cancelled provider: state={task.CurrentState}; database={task.DatabaseEntry.State}; output={File.Exists(paths.NovelFile)}; busyWhileCancelling={busyAfterCancel}");
            Require(existingOutput
                    ? File.ReadAllText(paths.NovelFile) == "Existing output must survive cancellation"
                    : !File.Exists(paths.NovelFile),
                "A cancelled provider must never publish its late output file or overwrite an existing output");
            Require(task.CurrentState == DownloadState.Cancelled && task.DatabaseEntry.State == DownloadState.Cancelled
                && task.ErrorMessage is null && !task.IsProcessing, "Cancelled finalization remains cancelled without a late success/error");
            RequirePersisted(task, DownloadState.Cancelled);
            Require(busyAfterCancel, "A cancelled provider remains busy until its streams can safely be released");
            Require(inputStreams.All(stream => !stream.GetCanRead()) && !Directory.Exists(paths.ImageFolderPath),
                "After acknowledgement, input streams and partial export files are released");
            await RenderAsync(window);
            Save(window, directory, "export-cancelled");
            Require(primary.IsEffectivelyEnabled, "The actual retry action becomes available after cancelled finalization ends");
            await ActivateAsync(window, primary);
            await UntilAsync(() => exporter.Calls == 2 && exporter.Pending is not null);
            Require(task.CurrentState == DownloadState.Pending && !task.GetToken().Token.IsCancellationRequested,
                "Retry reuses the task with a fresh token and freshly downloaded inputs");
            if (existingOutput)
            {
                Require(File.Exists(paths.NovelFile) && File.ReadAllText(paths.NovelFile) == "Existing output must survive cancellation",
                    "Starting a retry must retain the previous output until replacement is complete");
                await ActivateAsync(window, primary);
                Require(task.GetToken().Token.IsCancellationRequested, "The retry can also be cancelled through its actual row");
                exporter.Complete(!lateFailure);
                await UntilAsync(() => !task.IsPending);
                Require(task.CurrentState == DownloadState.Cancelled && task.DatabaseEntry.State == DownloadState.Cancelled
                    && File.ReadAllText(paths.NovelFile) == "Existing output must survive cancellation"
                    && exporter.Streams.All(stream => !stream.GetCanRead()) && !Directory.Exists(paths.ImageFolderPath),
                    "Repeated cancellation preserves the original output and releases the retried provider's inputs");
                RequirePersisted(task, DownloadState.Cancelled);
                await RenderAsync(window);
                await ActivateAsync(window, primary);
                await UntilAsync(() => exporter.Calls == 3 && exporter.Pending is not null);
            }
            exporter.Complete(false);
            await UntilAsync(() => task.CurrentState is DownloadState.Completed or DownloadState.Error);
            Require(task.CurrentState == DownloadState.Completed && File.ReadAllText(paths.NovelFile).EndsWith("Export after cancel", StringComparison.Ordinal),
                "The uncancelled retry publishes its complete file exactly once");
            Require(exporter.Streams.All(stream => !stream.GetCanRead()) && !Directory.Exists(paths.ImageFolderPath), "Successful retry also releases all temporary inputs");
            RequirePersisted(task, DownloadState.Completed);
            report.Add("PASS separate read-only SQLite connections restore cancelled/retried states, format token and cleared error");
            report.Add($"PASS animated={animated}, lateFailure={lateFailure}, existingOutput={existingOutput}: real reader save, row cancel, input lifetime, cancelled state, row retry and committed output");
            host.Shell.CloseAllPages();
            Require(fixture.Http.UnexpectedRequests.IsEmpty, "Export cancellation only uses isolated content and images");
        }
        finally
        {
            if (exporter.Pending is not null) exporter.Complete(false);
            File.WriteAllLines(Path.Combine(directory, "export-cancellation-contracts.txt"), report);
            window.Close();
            extensions.HostModels.Remove(extensionHost);
        }
    }

    private static void RequirePersisted(DownloadTaskGroup task, DownloadState state)
    {
        var database = App.AppViewModel.AppServiceProvider.GetRequiredService<SQLiteConnection>();
        Require(File.Exists(database.DatabasePath), "Persistence is verified against the isolated database file");
        using var reopened = new SQLiteConnection(database.DatabasePath, SQLiteOpenFlags.ReadOnly);
        var destination = task.TokenizedDestination;
        var entry = reopened.Table<DownloadHistoryEntry>().Where(record => record.Destination == destination).ToList().Single();
        Require(entry.State == state && entry.ErrorMessage is null && entry.FormatToken == task.DatabaseEntry.FormatToken,
            "A separate database connection must see the completed attempt's state and original format: " + entry.State);
    }

    private sealed class Exporter : INovelFormatProviderExtension, IAnimatedImageFormatProviderExtension
    {
        internal int Calls { get; private set; }
        internal ITaskCompletionSource? Pending { get; private set; }
        internal IStream[] Streams { get; private set; } = [];
        internal string Destination { get; private set; } = "";
        private string _input = "";

        public void FormatNovel(ITaskCompletionSource task, string input, string destination, string[] keys, IStream[] values, int count)
        {
            Require(Pending is null, "Retry cannot overlap an in-flight provider");
            Require(count == 3 && keys.Contains("cover.png") && keys.Contains("11.png") && keys.Contains("994101-1.png"), "All original image keys reach the format provider");
            Begin(task, input, destination, values);
        }

        public void FormatImage(ITaskCompletionSource task, IStream[] imagesKeys, int[] imagesValues, string destinationPath, int imagesCount)
        {
            Require(imagesCount == 2 && imagesValues.SequenceEqual(new[] { 100, 200 }), "Both original animation frames and delays reach the format provider");
            Begin(task, "Export after cancel", destinationPath, imagesKeys);
        }

        private void Begin(ITaskCompletionSource task, string input, string destination, IStream[] values)
        {
            Require(Pending is null, "Retry cannot overlap an in-flight provider");
            Calls++;
            Streams = values;
            Destination = destination;
            _input = input;
            File.WriteAllText(destination, "partial");
            Pending = task;
        }

        internal void Complete(bool fail)
        {
            var task = Pending ?? throw new InvalidOperationException("No pending export");
            Pending = null;
            foreach (var stream in Streams)
            {
                var signature = new byte[8];
                Require(stream.GetCanRead() && stream.Read(signature, 0, signature.Length) == 8
                    && signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), "A cancelled extension still reads the original PNG stream until it completes");
            }
            File.WriteAllText(Destination, _input);
            if (fail) task.SetException(new InvalidOperationException("Fixed late exporter failure"));
            else task.SetCompleted();
        }

        public string GetFormatExtension() => "cancel-fixture";
        public string GetFormatDescription() => "Offline delayed novel exporter";
        public void OnExtensionLoaded() { }
        public void OnExtensionUnloaded() { }
    }

    private sealed class Host(Exporter exporter) : IExtensionsHost
    {
        public string GetExtensionName() => "Cancellation exporter fixture";
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
