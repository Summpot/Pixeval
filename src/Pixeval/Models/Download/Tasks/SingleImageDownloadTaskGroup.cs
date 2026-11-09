// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Misaki;
using Pixeval.Extensions.Common.FormatProviders;
using Pixeval.Models.Database;
using Pixeval.Models.Extensions;
using Pixeval.Utilities;
using Pixeval.Utilities.IO;

namespace Pixeval.Models.Download.Tasks;

public class SingleImageDownloadTaskGroup : SingleImageDownloadTaskGroupBase
{
    public ISingleImage Entry => (ISingleImage) DatabaseEntry.Entry;

    private IllustrationDownloadFormatToken DestinationIllustrationFormat { get; }

    public SingleImageDownloadTaskGroup(
        ISingleImage entry,
        string destination,
        int? workSubscriptionId = null) : base(entry, destination, workSubscriptionId)
    {
        DestinationIllustrationFormat = IoHelper.GetAvailableIllustrationDownloadFormatToken();
        DatabaseEntry.FormatToken = DestinationIllustrationFormat.Value;
    }

    public SingleImageDownloadTaskGroup(DownloadHistoryEntryBase entry) : base(entry)
    {
        DestinationIllustrationFormat = GetFormatToken(entry);
    }

    protected override bool RequiresPostProcessing => DestinationIllustrationFormat.IsExtension;

    protected override async Task ProcessDownloadedFileAsync(string source, string destination, CancellationToken token)
    {
        if (DestinationIllustrationFormat.ExtensionFormatExtension is not { } extension)
            return;

        var provider = GetExtensionService().GetStaticImageFormatProvider(extension)
                       ?? throw new NotSupportedException(extension);
        token.ThrowIfCancellationRequested();
        await using var stream = File.OpenAsyncRead(source);
        // The extension ABI has no cancellation method; retain the input until
        // acknowledgement. The caller isolates any late output before commit.
        await provider.FormatImageAsync(stream, destination);
    }

    private static IllustrationDownloadFormatToken GetFormatToken(DownloadHistoryEntryBase entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.FormatToken))
            return IoHelper.GetAvailableIllustrationDownloadFormatToken(entry.FormatToken);

        return IllustrationDownloadFormatToken.Default;
    }

    private static ExtensionService GetExtensionService() =>
        App.AppViewModel.AppServiceProvider.GetRequiredService<ExtensionService>();
}
