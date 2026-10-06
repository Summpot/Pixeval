// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Misaki;
using Pixeval.Extensions.Common.FormatProviders;
using Pixeval.Models.Extensions;
using Pixeval.Models.Options;
using Pixeval.Native.Media;
using Pixeval.Native.Storage;
using Pixeval.Utilities;
using Pixeval.Utilities.IO;

namespace Pixeval.Models.Download.Tasks;

public class SingleAnimatedImageDownloadTaskGroup : SingleImageDownloadTaskGroupBase
{
    public ISingleAnimatedImage Entry => (ISingleAnimatedImage) DatabaseEntry.Entry!;

    private UgoiraDownloadFormatToken DestinationUgoiraFormat { get; }

    public SingleAnimatedImageDownloadTaskGroup(
        ISingleAnimatedImage entry,
        string destination,
        int? workSubscriptionId = null) : base(entry, destination, workSubscriptionId)
    {
        if (entry.PreferredAnimatedImageType is not SingleAnimatedImageType.SingleZipFile and not SingleAnimatedImageType.SingleFile)
            throw new InvalidOperationException($"{nameof(ISingleAnimatedImage.PreferredAnimatedImageType)} should be {nameof(SingleAnimatedImageType.SingleZipFile)} or {nameof(SingleAnimatedImageType.SingleFile)}");
        DestinationUgoiraFormat = IoHelper.GetAvailableUgoiraDownloadFormatToken();
        DatabaseEntry.FormatToken = DestinationUgoiraFormat.Value;
    }

    public SingleAnimatedImageDownloadTaskGroup(IDownloadHistoryEntry entry) : base(entry)
    {
        DestinationUgoiraFormat = GetFormatToken(entry);
    }

    protected override async Task AfterDownloadAsyncOverride(ImageDownloadTask sender, CancellationToken token = default)
    {
        var ext = DestinationUgoiraFormat.ExtensionFormatExtension ?? IoHelper.GetUgoiraExtension(DestinationUgoiraFormat);
        if (ext is not null)
        {
            await FormatNativeOrExtensionAsync(sender, ext);
            return;
        }

        var builtInFormat = DestinationUgoiraFormat.BuiltInFormat ?? UgoiraDownloadFormatToken.DefaultBuiltInFormat;
        if (builtInFormat is UgoiraDownloadFormat.Original)
            return;

        throw new NotSupportedException(builtInFormat.ToString());
    }

    private async Task FormatNativeOrExtensionAsync(ImageDownloadTask sender, string extension)
    {
        var tempPath = sender.Destination + ".source";
        if (File.Exists(tempPath))
            File.Delete(tempPath);
        FileHelper.Move(sender.Destination, tempPath);

        var nativeFormat = extension.ToLowerInvariant() switch
        {
            "gif" => UgoiraFormat.Gif,
            "png" or "apng" => UgoiraFormat.Apng,
            "webp" => UgoiraFormat.Webp,
            "mp4" => UgoiraFormat.Mp4,
            _ => (UgoiraFormat?)null
        };

        try
        {
            if (nativeFormat is { } targetFormat && Entry.PreferredAnimatedImageType == SingleAnimatedImageType.SingleZipFile)
            {
                await Entry.ZipImageDelays!.TryPreloadListAsync(Entry);
                var uDelays = Entry.ZipImageDelays!.Select(d => (uint)Math.Max(1, d)).ToList();
                await MediaEngine.Shared.SynthesizeUgoiraAsync(tempPath, sender.Destination, targetFormat, uDelays);
            }
            else if (GetExtensionService().GetAnimatedImageFormatProvider(extension) is { } provider)
            {
                await FormatByExtensionAsync(provider, tempPath, sender.Destination);
            }
            else
            {
                throw new NotSupportedException(extension);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private async Task FormatByExtensionAsync(IAnimatedImageFormatProviderExtension provider, string sourcePath, string destinationPath)
    {
        IReadOnlyDictionary<Stream, int>? streams = null;
        try
        {
            switch (Entry.PreferredAnimatedImageType)
            {
                case SingleAnimatedImageType.SingleZipFile:
                    await Entry.ZipImageDelays!.TryPreloadListAsync(Entry);
                    await using (var read = File.OpenAsyncRead(sourcePath))
                        streams = (await Streams.ReadZipAsync(read, true))
                            .ToArray<Stream>()
                            .Zip(Entry.ZipImageDelays!)
                            .ToDictionary(t => t.First, t => t.Second);
                    break;
                case SingleAnimatedImageType.SingleFile:
                    await using (var read = File.OpenAsyncRead(sourcePath))
                        streams = await IoHelper.SplitAnimatedImageStreamAsync(read);
                    break;
                default:
                    throw new NotSupportedException(Entry.PreferredAnimatedImageType.ToString());
            }

            await provider.FormatImageAsync(streams, destinationPath);
        }
        finally
        {
            if (streams is not null)
                foreach (var stream in streams.Keys)
                    await stream.DisposeAsync();
        }
    }

    private static UgoiraDownloadFormatToken GetFormatToken(IDownloadHistoryEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.FormatToken))
            return IoHelper.GetAvailableUgoiraDownloadFormatToken(entry.FormatToken);

        return UgoiraDownloadFormatToken.Default;
    }

    private static ExtensionService GetExtensionService() =>
        App.AppViewModel.AppServiceProvider.GetRequiredService<ExtensionService>();
}
