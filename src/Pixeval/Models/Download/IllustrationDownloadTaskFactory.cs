// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Pixeval.Models.Download.Tasks;
using Pixeval.Native.Booru;
using Pixeval.Native.Mako;
using Pixeval.Utilities.IO;

namespace Pixeval.Models.Download;

public class IllustrationDownloadTaskFactory : IDownloadTaskFactory<object, IDownloadTaskGroup, int>
{
    public IDownloadTaskGroup Create(object context, string rawPath, int setIndex = -1) =>
        Create(new ParserContext(context), rawPath, setIndex);

    public IDownloadTaskGroup Create(ParserContext parserContext, string rawPath, int setIndex = -1)
    {
        parserContext = SelectPage(parserContext, setIndex);
        var context = parserContext.ArtworkInfo;
        var path = IoHelper.NormalizePath(DownloadPathMacroParser.Reduce(rawPath, parserContext));
        var workSubscriptionId = (int?)parserContext.WorkSubscription?.HistoryEntryId;

        IDownloadTaskGroup task = context switch
        {
            Illustration illust when illust.IsPicGif => new UgoiraDownloadTaskGroup(illust, path, workSubscriptionId),
            Illustration illust when illust.IsPicSet && illust.SetIndex == -1 => new MangaDownloadTaskGroup(illust, path, workSubscriptionId),
            Illustration illust => new SingleImageDownloadTaskGroup(illust, path, workSubscriptionId),
            BooruPost booru => new SingleImageDownloadTaskGroup(booru, path, workSubscriptionId),
            _ => new SingleImageDownloadTaskGroup(context, path, workSubscriptionId)
        };

        return task;
    }

    private static ParserContext SelectPage(ParserContext parserContext, int setIndex) =>
        setIndex >= 0 && parserContext.ArtworkInfo is Illustration illust && illust.IsPicSet
            ? parserContext with { ArtworkInfo = illust.Pages[setIndex] }
            : parserContext;
}
