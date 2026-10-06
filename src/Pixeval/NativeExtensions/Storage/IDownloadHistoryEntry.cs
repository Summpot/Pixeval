// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Misaki;
using Pixeval.Models.Download.Tasks;
using Pixeval.Native.Download;
using Pixeval.Native.Mako;
using MakoNovel = Pixeval.Native.Mako.Novel;

namespace Pixeval.Native.Storage;

public interface IDownloadHistoryEntry
{
    long HistoryEntryId { get; }

    string Destination { get; }

    DownloadState State { get; set; }

    string? FormatToken { get; set; }

    string? ErrorMessage { get; set; }

    IArtworkInfo? Entry { get; }

    DownloadTaskKey DownloadTaskKey { get; }

    public static IDownloadHistoryEntry Create(
        string destination,
        IArtworkInfo entry,
        int? workSubscriptionId = null)
    {
        var serializable = entry as ISerializable;
        var serializeKey = serializable?.SerializeKey;
        var payloadJson = serializable?.Serialize();

        if (workSubscriptionId is > 0 and var subId)
        {
            var record = new SubscriptionDownloadHistoryRecord(
                0,
                entry.Id,
                serializeKey,
                destination,
                (uint)DownloadState.Queued,
                null,
                null,
                subId,
                entry.Id,
                payloadJson)
            {
                EntryOverride = entry
            };
            return record;
        }

        var dlRecord = new DownloadHistoryRecord(
            0,
            entry.Id,
            serializeKey,
            destination,
            (uint)DownloadState.Queued,
            null,
            null,
            payloadJson)
        {
            EntryOverride = entry
        };
        return dlRecord;
    }
}

public static class DownloadHistoryEntryExtensions
{
    public static IDownloadTaskGroup ToTaskGroup(this IDownloadHistoryEntry entry)
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
            MakoNovel => new NovelDownloadTaskGroup(entry),
            _ => new SingleImageDownloadTaskGroup(entry)
        };
    }
}
