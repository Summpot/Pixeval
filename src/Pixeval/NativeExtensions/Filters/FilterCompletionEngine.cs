// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.I18N;
using Pixeval.Native.Storage;

namespace Pixeval.Native.Filters;

public sealed class StorageFilterProvider(StorageEngine storage) : IFilterStorageProvider
{
    public List<TagCandidate> QuerySearchHistoryTags(string pattern, uint limit)
    {
        return storage.QuerySearchHistorySuggestions(pattern, limit)
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

public partial class FilterCompletionEngine
{
    private static FilterCompletionEngine? _default;

    public static FilterCompletionEngine Default => _default ??= CreateDefault();

    public static FilterCompletionEngine CreateWithStorage(StorageEngine storage)
    {
        var provider = new StorageFilterProvider(storage);
        return WithProvider(provider, CreateLocalization());
    }

    public static FilterCompletionEngine CreateDefault()
    {
        var storage = App.Services?.GetService<StorageEngine>();
        if (storage is not null)
        {
            return CreateWithStorage(storage);
        }

        return WithProvider(null, CreateLocalization());
    }

    public FilterAnalysisResult Analyze(string text) => Analyze(text, -1);

    public static FilterLocalization CreateLocalization()
    {
        return new FilterLocalization(
            AndDescription: I18NManager.GetResource(FilterResources.Completions.And),
            OrDescription: I18NManager.GetResource(FilterResources.Completions.Or),
            NotDescription: I18NManager.GetResource(FilterResources.Completions.Not),
            TitleDescription: I18NManager.GetResource(FilterResources.Completions.Title),
            AuthorDescription: I18NManager.GetResource(FilterResources.Completions.Author),
            TagDescription: I18NManager.GetResource(FilterResources.Completions.Tag),
            BookmarkDescription: I18NManager.GetResource(FilterResources.Completions.Bookmark),
            RatioDescription: I18NManager.GetResource(FilterResources.Completions.Ratio),
            StartDateDescription: I18NManager.GetResource(FilterResources.Completions.StartDate),
            EndDateDescription: I18NManager.GetResource(FilterResources.Completions.EndDate),
            IncludeConstraintDescription: I18NManager.GetResource(FilterResources.Completions.Include.Constraint),
            ExcludeConstraintDescription: I18NManager.GetResource(FilterResources.Completions.Exclude.Constraint),
            IncludeAiDescription: I18NManager.GetResource(FilterResources.Completions.Include.Ai),
            ExcludeAiDescription: I18NManager.GetResource(FilterResources.Completions.Exclude.Ai),
            IncludeR18Description: I18NManager.GetResource(FilterResources.Completions.Include.R18),
            ExcludeR18Description: I18NManager.GetResource(FilterResources.Completions.Exclude.R18),
            IncludeR18gDescription: I18NManager.GetResource(FilterResources.Completions.Include.R18G),
            ExcludeR18gDescription: I18NManager.GetResource(FilterResources.Completions.Exclude.R18G),
            IncludeGifDescription: I18NManager.GetResource(FilterResources.Completions.Include.Gif),
            ExcludeGifDescription: I18NManager.GetResource(FilterResources.Completions.Exclude.Gif));
    }
}
