using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Download;
using Pixeval.Models.Download.Tasks;
using Pixeval.Utilities.IO;
using Pixeval.Native.Download;
using Pixeval.AppManagement;

namespace Pixeval.Tests;

[TestClass]
public sealed class ImageDownloadTaskTest
{
    [TestMethod]
    public async Task NativeCompletedShouldSetStateAndTriggerPostProcess()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var source = Path.Combine(directory, "source.txt");
            var destination = Path.Combine(directory, "destination.txt");
            await File.WriteAllTextAsync(destination, "new");

            using var task = new TestImageDownloadTask(new(source), destination, true);
            await task.OnNativeCompletedAsync(destination);

            Assert.AreEqual(DownloadState.Completed, task.CurrentState);
            Assert.IsFalse(task.WasDownloadSkipped);
            Assert.AreEqual(1, task.PostProcessCount);
            Assert.AreEqual("new", await File.ReadAllTextAsync(destination));
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    [TestMethod]
    [DataRow(false, "old", false)]
    [DataRow(true, "new", true)]
    public async Task CommitShouldRespectDestinationCreatedDuringDownload(
        bool overwrite,
        string expectedContent,
        bool expectedCommitted)
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var temporaryFile = Path.Combine(directory, "download.tmp");
            var destination = Path.Combine(directory, "destination.txt");
            await File.WriteAllTextAsync(temporaryFile, "new");
            await File.WriteAllTextAsync(destination, "old");

            var committed = DownloadTaskFileHelper.CommitDownloadedFile(
                temporaryFile,
                destination,
                overwrite);

            Assert.AreEqual(expectedCommitted, committed);
            Assert.AreEqual(expectedContent, await File.ReadAllTextAsync(destination));
            Assert.IsFalse(File.Exists(temporaryFile));
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    [TestMethod]
    public async Task MissingDestinationDirectoryShouldNotFailTemporaryFileCleanup()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        var destinationDirectory = Path.Combine(directory, "missing");
        var destination = Path.Combine(destinationDirectory, "destination.txt");
        try
        {
            var source = Path.Combine(directory, "source.txt");
            await File.WriteAllTextAsync(source, "content");
            Directory.CreateDirectory(destinationDirectory);
            await File.WriteAllTextAsync(destination, "content");

            using var task = new TestImageDownloadTask(new(source), destination, false);
            await task.OnNativeCompletedAsync(destination);

            Assert.AreEqual(DownloadState.Completed, task.CurrentState);
            Assert.AreEqual("content", await File.ReadAllTextAsync(destination));
            Assert.IsFalse(File.Exists(destination + IoHelper.PixevalTempExtension));
        }
        finally
        {
            if (File.Exists(destination))
                File.Delete(destination);
            var temporaryFile = destination + IoHelper.PixevalTempExtension;
            if (File.Exists(temporaryFile))
                File.Delete(temporaryFile);
            if (Directory.Exists(destinationDirectory))
                Directory.Delete(destinationDirectory);
            DeleteTestDirectory(directory);
        }
    }

    [TestMethod]
    [Ignore("Requires running UI thread event loop for Dispatcher.UIThread")]
    public async Task PixivAssetsUriShouldCopyPackagedAsset()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            AppBuilder.Configure<Application>()
                .UseStandardRuntimePlatformSubsystem()
                .UseWindowingSubsystem(static () => { })
                .UseRenderingSubsystem(static () => { })
                .UseTextShapingSubsystem(static () => { })
                .SetupWithoutStarting();
            var destination = Path.Combine(directory, "cover.png");
            using var task = new TestImageDownloadTask(new(AppInfo.ImageNotAvailablePath), destination, false);
            await task.OnNativeCompletedAsync(destination);

            Assert.AreEqual(DownloadState.Completed, task.CurrentState, task.ErrorMessage);
            Assert.IsTrue(new FileInfo(destination).Length > 0);
            Assert.IsFalse(File.Exists(destination + IoHelper.PixevalTempExtension));
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    private static void DeleteTestDirectory(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
            File.Delete(file);
        Directory.Delete(directory);
    }

    private sealed class TestImageDownloadTask(Uri uri, string destination, bool overwrite)
        : ImageDownloadTask(uri, destination)
    {
        public int PostProcessCount { get; private set; }

        protected override bool OverwriteDownloadedFile => overwrite;

        protected override Task AfterDownloadAsyncOverride(
            ImageDownloadTask sender,
            CancellationToken token = default)
        {
            ++PostProcessCount;
            return Task.CompletedTask;
        }
    }
}
