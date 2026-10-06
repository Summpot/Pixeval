// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Misaki;
using Pixeval.Models.Download.Tasks;
using Pixeval.Models.Pixiv;
using Pixeval.Utilities.IO;

namespace Pixeval.Models.Download;

public class NovelDownloadTaskFactory : IDownloadTaskFactory<IArtworkInfo, NovelDownloadTaskGroup, NovelContent>
{
    public NovelDownloadTaskGroup Create(IArtworkInfo context, string rawPath, NovelContent? parameter) =>
        Create(new ParserContext(context), rawPath, parameter);

    public NovelDownloadTaskGroup Create(ParserContext parserContext, string rawPath, NovelContent? parameter)
    {
        var context = parserContext.ArtworkInfo;
        var path = IoHelper.NormalizePath(DownloadPathMacroParser.Reduce(rawPath, parserContext));
        var task = new NovelDownloadTaskGroup(
            context,
            path,
            parameter,
            (int?)parserContext.WorkSubscription?.HistoryEntryId);
        return task;
    }
}
