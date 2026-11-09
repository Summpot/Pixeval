using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Download;
using Pixeval.Extensions.Common;
using Pixeval.Extensions.Common.FormatProviders;
using Pixeval.Models.Database;
using Pixeval.Models.Download;
using Pixeval.Models.Download.Tasks;
using Pixeval.Models.Extensions;
using Pixeval.Utilities.IO;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Download;
using Pixeval.Views.Viewers;
using SQLite;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

[TestClass]
[DoNotParallelize]
[TestCategory("UI")]
public sealed class MangaExportTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false, true)]
    [DataRow(true, true)]
    [DataRow(false, false)]
    public Task CancelSecondPage(bool lateFailure, bool existingOutput) => UiTestSession.RunAsync(TestContext,
        directory => RunAsync(directory, cancel: true, lateFailure, existingOutput));

    [TestMethod]
    public Task ProviderFailureAndRetry() => UiTestSession.RunAsync(TestContext,
        directory => RunAsync(directory, cancel: false, lateFailure: true, existingOutput: true));

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public Task ExistingPagesWithoutOverwrite(bool allExisting) => UiTestSession.RunAsync(TestContext,
        directory => RunAsync(directory, cancel: false, lateFailure: false, existingOutput: true, overwrite: false, allExisting));

    private static async Task RunAsync(string directory, bool cancel, bool lateFailure, bool existingOutput, bool overwrite = true, bool allExisting = true)
    {
        using var fixture = new IllustrationReaderFixture();
        await App.AppViewModel.MakoClient.SetTokenAsync("fixture-manga-export");
        await App.AppViewModel.MakoClient.ValidateTokenAsync();
        var history = App.AppViewModel.HistoryPersistHelper;
        await history.RestoreTask;
        var extensions = App.AppViewModel.AppServiceProvider.GetRequiredService<ExtensionService>();
        var exporter = new Exporter();
        using var extensionHost = new ExtensionsHostModel(new Host(exporter), []);
        extensions.HostModels.Add(extensionHost);
        var settings = App.AppViewModel.AppSettings.DownloadSettings;
        settings.DownloadFormats.IllustrationDownloadFormat = IllustrationDownloadFormatToken.Extension(exporter);
        settings.OverwriteDownloadedFile = overwrite;
        var output = Path.Combine(directory, "exports");
        settings.DownloadPathMacro = Path.Combine(output, "@{id}-@{is_pic_set?@{pic_set_index}:0}.@{ext}");
        var work = new IllustrationItemViewModel(IllustrationReaderFixture.Sample(996580, 3));
        var model = new IllustrationViewerPageViewModel(work, false);
        var page = new IllustrationViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 700, Height = 850, Content = host.Shell };
        List<string> report = [];
        MangaDownloadTaskGroup? task = null;
        try
        {
            window.Show();
            await UntilAsync(() => model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
            await RenderAsync(window);
            var save = page.GetVisualDescendants().OfType<CommandBar>().SelectMany(bar => bar.PrimaryCommands)
                .OfType<CommandBarButton>().Single(button => ReferenceEquals(button.Command, work.SaveCommand));
            // Use the production factory to resolve the unchanged macro/index contract before the UI queues it.
            var factory = App.AppViewModel.AppServiceProvider.GetRequiredService<IllustrationDownloadTaskFactory>();
            using (var preview = (MangaDownloadTaskGroup) factory.Create(work.Entry, settings.DownloadPathMacro))
            {
                await preview.InitializeTaskGroupAsync();
                Directory.CreateDirectory(output);
                if (existingOutput)
                    for (var i = 0; i < preview.Destinations.Count; i++)
                        if (allExisting || i == 1)
                            File.WriteAllText(preview.Destinations[i], "old-" + i);
            }
            await ActivateAsync(window, save);
            await UntilAsync(() => history.DownloadManager.QueuedTasks.Count == 1);
            task = (MangaDownloadTaskGroup) history.DownloadManager.QueuedTasks.Single();
            await UntilAsync(() => task.Destinations.Count == 3);
            var paths = task.Destinations;
            Require(paths.Count == 3 && paths.Distinct().Count() == 3 && paths.All(path => Path.GetDirectoryName(path) == output)
                && task.OpenLocalDestination == output, "Manga retains three distinct final paths and its original open-folder destination");
            var downloads = new DownloadPage();
            host.Shell.NavigateTo(downloads);
            await RenderAsync(window);
            var row = downloads.GetVisualDescendants().OfType<DownloadItem>().Single(control =>
                control.DataContext is DownloadItemViewModel vm && ReferenceEquals(vm.DownloadTask, task));
            var primary = row.FindControl<CommandBarButton>("PrimaryActionButton")!;
            if (!overwrite)
            {
                if (!allExisting)
                {
                    await UntilAsync(() => exporter.Pending is not null);
                    Require(!File.Exists(paths[0]) && File.ReadAllText(paths[1]) == "old-1" && !File.Exists(paths[2]),
                        "With mixed existing pages, only the old final page is visible while conversion is pending");
                    File.WriteAllText(paths[0], "arrived-during-export");
                    exporter.Complete(fail: false);
                    await UntilAsync(() => exporter.Calls == 2 && exporter.Pending is not null);
                    exporter.Complete(fail: false);
                }
                await UntilAsync(() => task.CurrentState is DownloadState.Completed);
                Require(exporter.Calls == (allExisting ? 0 : 2) && paths.Select(File.ReadAllText).SequenceEqual(allExisting
                    ? new[] { "old-0", "old-1", "old-2" } : new[] { "arrived-during-export", "old-1", "formatted-2" }),
                    "Without overwrite existing pages skip conversion and a destination created during export also retains its bytes");
                RequirePersisted(task, DownloadState.Completed, error: false);
                RequireClean(output, paths);
                report.Add($"PASS no-overwrite allExisting={allExisting}: existing/late destinations retained, missing pages converted, SQLite completed and no temporary files");
                Require(fixture.Http.UnexpectedRequests.IsEmpty, "Only fixed offline requests are used");
                host.Shell.CloseAllPages();
                return;
            }

            await UntilAsync(() => exporter.Pending is not null);
            Require(task.CurrentState is DownloadState.Pending && exporter.Calls == 1 && task.All(child => child.CurrentState is DownloadState.Completed),
                "All raw downloads finish before the first serial provider starts and the real group remains pending");
            if (existingOutput)
                Require(paths.Select(File.ReadAllText).SequenceEqual(new[] { "old-0", "old-1", "old-2" }),
                    "Raw downloads and a pending provider never replace existing page outputs");
            else
                Require(paths.All(path => !File.Exists(path)), "Raw downloads are not published as final converted pages");
            var firstStream = exporter.Stream!;
            exporter.Complete(fail: false);
            await UntilAsync(() => exporter.Calls == 2 && exporter.Pending is not null);
            Require(!firstStream.GetCanRead() && File.ReadAllText(paths[0]) == "formatted-1", "First successful page commits and releases its source before page two");
            var secondStream = exporter.Stream!;
            if (cancel)
            {
                await RenderAsync(window);
                await ActivateAsync(window, primary);
                var cancelledToken = task.GetToken().Token;
                Require(cancelledToken.IsCancellationRequested && task.IsProcessing, "The pending row cancels and remains busy until the provider acknowledges");
                task.Reset();
                task.Resume();
                await RenderAsync(window);
                Require(!primary.IsEffectivelyEnabled && task.GetToken().Token == cancelledToken && exporter.Calls == 2 && secondStream.GetCanRead(),
                    "In-flight cancellation retains its token and readable stream and rejects retry/resume");
            }
            exporter.Complete(lateFailure);
            await UntilAsync(() => !task.IsPending);
            var expectedState = cancel ? DownloadState.Cancelled : DownloadState.Error;
            Require(task.CurrentState == expectedState && exporter.Calls == 2 && !secondStream.GetCanRead(),
                "Page two completion belongs to its original attempt and page three never starts after failure/cancellation");
            Require(File.ReadAllText(paths[0]) == "formatted-1", "The already committed first page survives cancellation/failure");
            for (var i = 1; i < paths.Count; i++)
                Require(existingOutput ? File.ReadAllText(paths[i]) == "old-" + i : !File.Exists(paths[i]),
                    "Uncommitted pages preserve their old output or remain absent");
            RequirePersisted(task, expectedState, error: !cancel);
            RequireClean(output, paths);
            await RenderAsync(window);
            Save(window, directory, "manga-export-stopped");
            Require(primary.IsEffectivelyEnabled, "The real retry action becomes enabled after all source streams close");
            await ActivateAsync(window, primary);
            await UntilAsync(() => exporter.Calls == 3 && exporter.Pending is not null);
            Require(task.CurrentState is DownloadState.Pending && !task.GetToken().Token.IsCancellationRequested
                && File.ReadAllText(paths[0]) == "formatted-1", "Retry starts a fresh attempt while retaining the successfully committed page");
            for (var i = 1; i < paths.Count; i++)
                Require(existingOutput ? File.ReadAllText(paths[i]) == "old-" + i : !File.Exists(paths[i]), "Retry preserves every not-yet-committed output");
            for (var i = 0; i < paths.Count; i++)
            {
                await UntilAsync(() => exporter.Pending is not null);
                var stream = exporter.Stream!;
                var call = exporter.Calls;
                exporter.Complete(fail: false);
                await UntilAsync(() => !stream.GetCanRead());
                Require(File.ReadAllText(paths[i]) == "formatted-" + call, "Retry commits each complete page in order");
            }
            await UntilAsync(() => task.CurrentState is DownloadState.Completed);
            Require(exporter.Calls == 5 && !task.IsProcessing, "Successful retry runs exactly three serial providers");
            RequirePersisted(task, DownloadState.Completed, error: false);
            RequireClean(output, paths);
            report.Add($"PASS cancel={cancel}, lateFailure={lateFailure}, existingOutput={existingOutput}: production save, page-two stop, output preservation, serial retry, SQLite and stream/temp cleanup");
            Require(fixture.Http.UnexpectedRequests.IsEmpty, "Only fixed offline requests are used");
            host.Shell.CloseAllPages();
        }
        finally
        {
            task?.Cancel();
            exporter.ReleaseAll = true;
            if (exporter.Pending is not null) exporter.Complete(fail: false);
            if (task is { IsPending: true }) await UntilAsync(() => !task.IsPending);
            File.WriteAllLines(Path.Combine(directory, "manga-export-contracts.txt"), report);
            window.Close();
            extensions.HostModels.Remove(extensionHost);
        }
    }

    private static void RequireClean(string directory, IReadOnlyList<string> paths) =>
        Require(Directory.GetFiles(directory, "*", SearchOption.AllDirectories).All(paths.Contains), "Only committed final files remain; no raw, partial or formatted temporary files survive");

    private static void RequirePersisted(MangaDownloadTaskGroup task, DownloadState state, bool error)
    {
        var database = App.AppViewModel.AppServiceProvider.GetRequiredService<SQLiteConnection>();
        using var reopened = new SQLiteConnection(database.DatabasePath, SQLiteOpenFlags.ReadOnly);
        var destination = task.TokenizedDestination;
        var entry = reopened.Table<DownloadHistoryEntry>().Where(record => record.Destination == destination).ToList().Single();
        Require(entry.State == state && (entry.ErrorMessage is not null) == error && entry.FormatToken == task.DatabaseEntry.FormatToken,
            "A separate SQLite connection restores the actual state, error and unchanged format token");
    }

    private sealed class Exporter : IStaticImageFormatProviderExtension
    {
        internal int Calls { get; private set; }
        internal ITaskCompletionSource? Pending { get; private set; }
        internal IStream? Stream { get; private set; }
        internal bool ReleaseAll { get; set; }
        private string _destination = "";

        public void FormatImage(ITaskCompletionSource task, IStream input, string destinationPath)
        {
            Require(Pending is null, "Manga providers must remain serial");
            Calls++;
            Stream = input;
            _destination = destinationPath;
            File.WriteAllText(destinationPath, "partial");
            Pending = task;
            if (ReleaseAll) Complete(fail: false);
        }

        internal void Complete(bool fail)
        {
            var task = Pending ?? throw new InvalidOperationException("No pending page export");
            var signature = new byte[8];
            Require(Stream!.GetCanRead() && Stream.Read(signature, 0, 8) == 8
                && signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), "Provider keeps its original PNG stream readable until acknowledgment");
            File.WriteAllText(_destination, "formatted-" + Calls);
            Pending = null;
            if (fail) task.SetException(new InvalidOperationException("Fixed manga page exporter failure"));
            else task.SetCompleted();
        }

        public string GetFormatExtension() => "manga-fixture";
        public string GetFormatDescription() => "Offline serial manga exporter";
        public void OnExtensionLoaded() { }
        public void OnExtensionUnloaded() { }
    }

    private sealed class Host(Exporter exporter) : IExtensionsHost
    {
        public string GetExtensionName() => "Manga exporter fixture";
        public string GetAuthorName() => "Pixeval tests";
        public string GetExtensionLink() => "";
        public string GetHelpLink() => "";
        public string GetDescription() => "Managed serial format-provider boundary";
        public string GetSdkVersion() => ExtensionService.CurrentVersion;
        public string GetVersion() => "1.0";
        public IExtension[] GetExtensions(out int count) { count = 1; return [exporter]; }
        public byte[]? GetIcon(out int count) { count = 0; return null; }
        public void Initialize(string cultureName, string tempDirectory, string extensionDirectory, ILogger logger) { }
    }
}
