// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Pixeval.Models.Download.Tasks;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.Utilities.IO;

namespace Pixeval.Models.Download;

public class NovelDownloadTaskFactory : IDownloadTaskFactory<object, NovelDownloadTaskGroup, NovelContent>
{
    public NovelDownloadTaskGroup Create(object context, string rawPath, NovelContent? parameter) =>
        Create(new ParserContext(context), rawPath, parameter);

    public NovelDownloadTaskGroup Create(ParserContext parserContext, string rawPath, NovelContent? parameter)
    {
        var context = parserContext.ArtworkInfo;
        var path = IoHelper.NormalizePath(DownloadPathMacroParser.Reduce(rawPath, parserContext));
        var task = new NovelDownloadTaskGroup(
            (Novel) context,
            path,
            parameter,
            (int?)parserContext.WorkSubscription?.HistoryEntryId);
        return task;
    }
}
