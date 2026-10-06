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

            var text = await App.AppViewModel.MakoClient.GetNovelContentAsync(novel.RawId);
            return NovelContent.CreateDefault() with
            {
                Id = novel.RawId,
                Title = novel.Title,
                Text = text,
                Date = novel.CreateDateOffset,
                UserId = novel.Author.Id,
                CoverUrl = novel.Thumbnails.FirstOrDefault()?.ImageUri.OriginalString ?? ""
            };
        });
}
