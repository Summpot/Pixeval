// Copyright (c) Pixeval.
// Licensed under the GPL v3 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mako.Model;
using Pixeval.Controls;

namespace Pixeval.ViewModels;

public partial class NovelItemViewModel(Novel novel) : WorkEntryViewModel<Novel>(novel), IFactory<Novel, NovelItemViewModel>
{
    private const int MaxDisplayedTagCount = 8;

    private const int MaxDisplayedTagCharacters = 36;

    private const int MaxSingleTagCharacters = 12;

    private const double MinMetadataWidth = 148;

    private const double MaxMetadataWidth = 320;

    private const double MetadataWidthPerCharacter = 5.2;

    private const double MetadataWidthPerTag = 12;

    /// <inheritdoc />
    public override bool IsBookmarkSupported => true;

    private IReadOnlyList<NovelDisplayTag> DisplayTagsCore { get; } = BuildDisplayTags(novel.Tags);

    public static NovelItemViewModel CreateInstance(Novel entry) => new(entry);

    public int TextLength => Entry.TextLength;

    public IReadOnlyList<NovelDisplayTag> DisplayTags => DisplayTagsCore;

    public double PreferredMetadataWidth => CalculatePreferredMetadataWidth(DisplayTagsCore);

    public Task<NovelContent> ContentAsync { get; } = App.AppViewModel.MakoClient.GetNovelContentAsync(novel.Id);

    private static IReadOnlyList<NovelDisplayTag> BuildDisplayTags(IEnumerable<Tag> tags)
    {
        var displayTags = new List<NovelDisplayTag>();
        var hiddenCount = 0;
        var displayedCharacters = 0;

        foreach (var tag in tags)
        {
            var fullText = string.IsNullOrWhiteSpace(tag.ToolTip) ? tag.Name : tag.ToolTip;
            var displayText = TruncateTag(fullText);
            var tagCharacters = displayText.Length;

            if (displayTags.Count >= MaxDisplayedTagCount ||
                displayTags.Count > 0 && displayedCharacters + tagCharacters > MaxDisplayedTagCharacters)
            {
                hiddenCount++;
                continue;
            }

            displayTags.Add(new NovelDisplayTag(tag, displayText, fullText));
            displayedCharacters += tagCharacters;
        }

        if (hiddenCount > 0)
        {
            while (displayTags.Count >= MaxDisplayedTagCount && displayTags.Count > 0)
            {
                displayTags.RemoveAt(displayTags.Count - 1);
                hiddenCount++;
            }

            displayTags.Add(NovelDisplayTag.CreateOverflow(hiddenCount));
        }

        return displayTags;
    }

    private static double CalculatePreferredMetadataWidth(IReadOnlyList<NovelDisplayTag> displayTags)
    {
        var characters = displayTags.Sum(tag => tag.DisplayText.Length);
        var width = MinMetadataWidth + characters * MetadataWidthPerCharacter + Math.Max(0, displayTags.Count - 1) * MetadataWidthPerTag;
        return Math.Clamp(width, MinMetadataWidth, MaxMetadataWidth);
    }

    private static string TruncateTag(string value)
    {
        if (value.Length <= MaxSingleTagCharacters)
            return value;

        return string.Concat(value.AsSpan(0, MaxSingleTagCharacters - 1), "…");
    }
}

public sealed record NovelDisplayTag(Tag? Tag, string DisplayText, string ToolTipText)
{
    public bool IsInteractive => Tag is not null;

    public static NovelDisplayTag CreateOverflow(int hiddenCount) => new(null, $"+{hiddenCount}", $"+{hiddenCount}");
}
