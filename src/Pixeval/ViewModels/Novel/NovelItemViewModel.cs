// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading.Tasks;
using Mako.Model;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Models.Blocking;
using Pixeval.Utilities;

namespace Pixeval.ViewModels;

public partial class NovelItemViewModel(Novel novel)
    : WorkEntryViewModel<Novel>(BlockedContentHelper.Replace(novel)), IFactory<Novel, NovelItemViewModel>
{
    public static NovelItemViewModel CreateInstance(Novel entry) => new(entry);

    public int TextLength => Entry.TextLength;

    private readonly object _contentLock = new();
    private Task<NovelContent>? _contentTask;

    public Task<NovelContent> ContentAsync
    {
        get
        {
            lock (_contentLock)
            {
                // Readers and downloads share successful/in-flight content. A failed attempt
                // must not poison this item permanently when the reader explicitly retries.
                if (_contentTask is null or { IsFaulted: true } or { IsCanceled: true })
                    _contentTask = BlockedContentHelper.IsBlockedPlaceholder(novel)
                        ? Task.FromResult(BlockedContentModelHelper.CreateBlockedNovelContent(BlockedContentHelper.Replace(novel)))
                        : LoadContentAsync();
                return _contentTask;
            }
        }
    }

    private async Task<NovelContent> LoadContentAsync()
    {
        var content = await App.AppViewModel.MakoClient.GetNovelContentAsync(novel.Id);
        // Mako returns a default model after reporting transport/parse failures. It is
        // not an empty successful novel and must not be cached as one.
        if (content.Id != novel.Id)
            throw new InvalidOperationException(I18NManager.GetResource(EntryViewerPageResources.LoadFailed));
        return content;
    }
}
