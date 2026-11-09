// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Misaki;
using Pixeval.Download;
using Pixeval.Extensions.Common.FormatProviders;
using Pixeval.Models.Database;
using Pixeval.Models.Extensions;
using Pixeval.Utilities;
using Pixeval.Utilities.IO;

namespace Pixeval.Models.Download.Tasks;

public class MangaDownloadTaskGroup : DownloadTaskGroup
{
    public IImageSet Entry => (IImageSet) DatabaseEntry.Entry;

    public MangaDownloadTaskGroup(DownloadHistoryEntryBase entry) : base(entry)
    {
        DestinationIllustrationFormat = GetFormatToken(entry);
    }

    public MangaDownloadTaskGroup(
        IImageSet entry,
        string destination,
        int? workSubscriptionId = null) : base(entry, destination, workSubscriptionId)
    {
        DestinationIllustrationFormat = IoHelper.GetAvailableIllustrationDownloadFormatToken();
        DatabaseEntry.FormatToken = DestinationIllustrationFormat.Value;
    }

    public override ValueTask InitializeTaskGroupAsync()
    {
        SetTasksSet();
        return ValueTask.CompletedTask;
    }

    private void SetTasksSet()
    {
        if (TasksSet.Count > 0)
            return;
        foreach (var page in Entry.Pages)
        {
            var path = IoHelper.ReplaceTokenExtensionFromUrl(TokenizedDestination, page.ImageUri, page.SetIndex);
            var imageDownloadTask = DestinationIllustrationFormat.IsExtension
                ? new StagedImageDownloadTask(page.ImageUri, path, DatabaseEntry.State)
                : new ImageDownloadTask(page.ImageUri, path, DatabaseEntry.State);
            AddToTasksSet(imageDownloadTask);
        }

        SetNotCreateFromEntry();
    }

    protected override async Task AfterAllDownloadAsyncOverride(DownloadTaskGroup sender, CancellationToken token = default)
    {
        if (DestinationIllustrationFormat.ExtensionFormatExtension is not { } extension)
            return;

        try
        {
            var provider = GetExtensionService().GetStaticImageFormatProvider(extension)
                           ?? throw new NotSupportedException(extension);
            // Keep the existing all-downloads-first, serial-provider ordering. Raw
            // pages are private inputs; only a completed conversion may replace a page.
            foreach (StagedImageDownloadTask task in TasksSet)
            {
                token.ThrowIfCancellationRequested();
                if (task.WasDownloadSkipped)
                    continue;

                var temporaryFile = task.Destination + ".formatted" + IoHelper.PixevalTempExtension;
                if (File.Exists(temporaryFile))
                    File.Delete(temporaryFile);
                try
                {
                    await using var stream = File.OpenAsyncRead(task.SourceDestination);
                    await provider.FormatImageAsync(stream, temporaryFile);
                    token.ThrowIfCancellationRequested();
                    _ = DownloadTaskFileHelper.CommitDownloadedFile(temporaryFile, task.Destination, task.OverwriteOutput);
                }
                finally
                {
                    if (File.Exists(temporaryFile))
                        File.Delete(temporaryFile);
                }
            }
        }
        finally
        {
            foreach (StagedImageDownloadTask task in TasksSet)
                task.DeleteSource();
        }
    }

    private IllustrationDownloadFormatToken DestinationIllustrationFormat { get; }

    public override string OpenLocalDestination
    {
        get
        {
            if (TasksSet.Count is 0)
                SetTasksSet();
            return Path.GetDirectoryName(TasksSet[0].Destination)!;
        }
    }

    public override void Delete()
    {
        foreach (var task in TasksSet)
            task.Delete();
        FileHelper.DeleteEmptyFolder(OpenLocalDestination);
    }

    private static IllustrationDownloadFormatToken GetFormatToken(DownloadHistoryEntryBase entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.FormatToken))
            return IoHelper.GetAvailableIllustrationDownloadFormatToken(entry.FormatToken);

        return IllustrationDownloadFormatToken.Default;
    }

    private static ExtensionService GetExtensionService() =>
        App.AppViewModel.AppServiceProvider.GetRequiredService<ExtensionService>();

    private sealed class StagedImageDownloadTask(Uri uri, string destination, DownloadState state)
        : ImageDownloadTask(uri, destination, state)
    {
        internal string SourceDestination => Destination + ".source" + IoHelper.PixevalTempExtension;

        protected override string DownloadDestination => SourceDestination;

        internal bool OverwriteOutput { get; private set; }

        protected override Task DownloadStartedAsyncOverride(ImageDownloadTask sender)
        {
            OverwriteOutput = OverwriteDownloadedFile;
            DeleteSource();
            return Task.CompletedTask;
        }

        internal void DeleteSource() => base.Delete();

        protected override void DeleteForReset()
        {
            if (OverwriteDownloadedFile)
                DeleteSource();
            else
                Delete();
        }

        public override void Delete()
        {
            DeleteSource();
            if (File.Exists(Destination))
                File.Delete(Destination);
        }
    }
}
