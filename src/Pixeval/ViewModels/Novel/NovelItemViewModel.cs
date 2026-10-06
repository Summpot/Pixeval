// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Linq;
using System.Threading.Tasks;
using Pixeval.Controls;
using Pixeval.Models.Blocking;
using Pixeval.Models.Pixiv;
using Pixeval.Utilities;

using Pixeval.Native.Mako;

namespace Pixeval.ViewModels;

public partial class NovelItemViewModel(Novel novel)
    : WorkEntryViewModel<Novel>(BlockedContentHelper.Replace(novel)), IFactory<Novel, NovelItemViewModel>
{
    public static NovelItemViewModel CreateInstance(Novel entry) => new(entry);

    public int TextLength => Entry.TextLength;

    public Task<NovelContent> ContentAsync => _contentAsync.Value;

    private readonly Lazy<Task<NovelContent>> _contentAsync =
        new(async () =>
        {
            if (BlockedContentHelper.IsBlockedPlaceholder(novel))
                return BlockedContentModelHelper.CreateBlockedNovelContent(BlockedContentHelper.Replace(novel));

            var content = await App.AppViewModel.MakoClient.GetNovelContentStructuredAsync(novel.RawId);
            return content with
            {
                Title = string.IsNullOrWhiteSpace(content.Title) ? novel.Title : content.Title,
                CoverUrl = string.IsNullOrWhiteSpace(content.CoverUrl) ? (novel.Thumbnails.FirstOrDefault()?.ImageUri.OriginalString ?? "") : content.CoverUrl,
                UserId = content.UserId == 0 ? novel.Author.Id : content.UserId
            };
        });
}
