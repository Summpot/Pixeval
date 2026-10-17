// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using Pixeval.Localization;
using Pixeval.Storage;

namespace Pixeval.Filters;

public sealed class StorageFilterProvider(StorageEngine storage) : IFilterStorageProvider
{
    public List<TagCandidate> QuerySearchHistoryTags(string pattern, uint limit)
    {
        return storage.HistoryRepository.QuerySearchHistorySuggestions(pattern, limit)
            .Select(r => new TagCandidate(r.Value, r.TranslatedName))
            .ToList();
    }

    public List<AuthorCandidate> QuerySubscriptionAuthors(string pattern, uint limit)
    {
        return storage.QuerySubscriptionAuthorSuggestions(pattern, limit)
            .Select(s => new AuthorCandidate(s.Title, string.IsNullOrEmpty(s.Author) ? null : s.Author))
            .ToList();
    }
}

public partial class FilterSession
{
    public static FilterSession CreateWithStorage(StorageEngine storage)
    {
        var provider = new StorageFilterProvider(storage);
        return WithProvider(provider, CreateLocalization());
    }

    public static FilterSession CreateDefault() =>
        WithProvider(null, CreateLocalization());

    public FilterAnalysisResult Analyze(string text) => Analyze(text, -1);

    public static FilterLocalization CreateLocalization()
    {
        return new FilterLocalization(
            AndDescription: Strings.FilterCompletionsAnd(),
            OrDescription: Strings.FilterCompletionsOr(),
            NotDescription: Strings.FilterCompletionsNot(),
            TitleDescription: Strings.FilterCompletionsTitle(),
            AuthorDescription: Strings.FilterCompletionsAuthor(),
            TagDescription: Strings.FilterCompletionsTag(),
            BookmarkDescription: Strings.FilterCompletionsBookmark(),
            RatioDescription: Strings.FilterCompletionsRatio(),
            StartDateDescription: Strings.FilterCompletionsStartDate(),
            EndDateDescription: Strings.FilterCompletionsEndDate(),
            IncludeConstraintDescription: Strings.FilterCompletionsIncludeConstraint(),
            ExcludeConstraintDescription: Strings.FilterCompletionsExcludeConstraint(),
            IncludeAiDescription: Strings.FilterCompletionsIncludeAi(),
            ExcludeAiDescription: Strings.FilterCompletionsExcludeAi(),
            IncludeR18Description: Strings.FilterCompletionsIncludeR18(),
            ExcludeR18Description: Strings.FilterCompletionsExcludeR18(),
            IncludeR18gDescription: Strings.FilterCompletionsIncludeR18G(),
            ExcludeR18gDescription: Strings.FilterCompletionsExcludeR18G(),
            IncludeGifDescription: Strings.FilterCompletionsIncludeGif(),
            ExcludeGifDescription: Strings.FilterCompletionsExcludeGif());
    }
}
