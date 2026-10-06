// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Misaki;
using Pixeval.Models.Download.Tasks;
using Pixeval.Native.Storage;
using Pixeval.Utilities;

namespace Pixeval.Models.Database.Managers;

public abstract class DownloadHistoryPersistentManagerBase<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    TEntry> : ArtworkHistoryPersistentManager<TEntry>
    where TEntry : DownloadHistoryEntryBase, new()
{
    protected DownloadHistoryPersistentManagerBase(StorageEngine storage, FileLogger logger)
        : base(storage, logger)
    {
    }

    public async IAsyncEnumerable<IDownloadTaskGroup> StreamTaskGroupsAsync(
        int skip = 0,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var entry in StreamEntriesAsync(skip, token).ConfigureAwait(false))
            yield return ToModel(entry);
    }

    private static IDownloadTaskGroup ToModel(TEntry entry)
    {
        return entry.Entry switch
        {
            ISingleImage { ImageType: ImageType.SingleImage } or
                ISingleImage { ImageType: ImageType.ImageSet, SetIndex: > -1 } => new SingleImageDownloadTaskGroup(entry),
            ISingleAnimatedImage
            {
                ImageType: ImageType.SingleAnimatedImage,
                PreferredAnimatedImageType: SingleAnimatedImageType.SingleZipFile or SingleAnimatedImageType.SingleFile
            } => new SingleAnimatedImageDownloadTaskGroup(entry),
            ISingleAnimatedImage
            {
                ImageType: ImageType.SingleAnimatedImage,
                PreferredAnimatedImageType: SingleAnimatedImageType.MultiFiles
            } => new UgoiraDownloadTaskGroup(entry),
            IImageSet { ImageType: ImageType.ImageSet } => new MangaDownloadTaskGroup(entry),
            Novel => new NovelDownloadTaskGroup(entry),
            _ => new SingleImageDownloadTaskGroup(entry)
        };
    }
}
