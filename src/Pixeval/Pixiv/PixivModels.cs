// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pixeval.Mako;

namespace Pixeval.Pixiv;

public interface IWorkEntry
{
    long Id { get; }
    User User { get; }
    string Title { get; }
    Series? Series => null;
}

public enum SpotlightCategory
{
    All,
    Spotlight,
    Tutorial,
    Inspiration
}


public class SearchArgumentsBase(string searchText)
{
    public string SearchText { get; set; } = searchText;
    public WorkSortOption SortOption { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public bool AiType { get; set; }
    public bool MergePlainKeywordResults { get; set; } = true;
    public bool IncludeTranslatedTagResults { get; set; } = true;
    public bool IncludePotentialViolationWorks { get; set; } = false;
}

public class IllustrationSearchArguments(string searchText) : SearchArgumentsBase(searchText)
{
    public SearchIllustrationTagMatchOption MatchOption { get; set; }
    public SearchIllustrationContentType ContentType { get; set; } = SearchIllustrationContentType.IllustrationAndMangaAndUgoira;
    public SearchIllustrationRatioPattern RatioPattern { get; set; } = SearchIllustrationRatioPattern.All;
    public int? WidthMin { get; set; }
    public int? WidthMax { get; set; }
    public int? HeightMin { get; set; }
    public int? HeightMax { get; set; }
    public string? Tool { get; set; }
}

public class NovelSearchArguments(string searchText) : SearchArgumentsBase(searchText)
{
    public SearchNovelTagMatchOption MatchOption { get; set; }
    public string? LangCode { get; set; }
    public SearchNovelContentLengthOption Option { get; set; }
    public int? ContentLengthMin { get; set; }
    public int? ContentLengthMax { get; set; }
    public bool IsOriginalOnly { get; set; }
    public int? GenreId { get; set; }
    public bool IsReplaceableOnly { get; set; }
}

public record MangaSeriesContextResponse
{
    public required Series Detail { get; set; }
    public required MangaSeriesContext Context { get; set; }
}

public record MangaSeriesContext
{
    public required int ContentOrder { get; set; }
    public Illustration? Previous { get; set; }
    public Illustration? Next { get; set; }
}



public enum DomainFrontingType
{
    Fragmentation,
    Desync,
    Ech
}

public static class MakoHttpOptions
{
    public const string AppApiHost = "app-api.pixiv.net";
    public const string WebApiHost = "www.pixiv.net";
    public const string AccountHost = "accounts.pixiv.net";
    public const string OAuthHost = "oauth.secure.pixiv.net";
    public const string ImageHost = "i.pximg.net";
    public const string ImageHost2 = "s.pximg.net";
}

public class RateLimitEventArgs(DateTimeOffset retryAt) : EventArgs
{
    public DateTimeOffset RetryAt { get; } = retryAt;
}

public static class FetchEngineExtensions
{
    public static IAsyncEnumerable<IWorkEntry> AsWorkEntries<T>(this IAsyncEnumerable<T> source) where T : IWorkEntry
    {
        return System.Linq.AsyncEnumerable.Select(source, static x => (IWorkEntry)x);
    }

    public static IAsyncEnumerable<object> AsObjects<T>(this IAsyncEnumerable<T> source)
    {
        return System.Linq.AsyncEnumerable.Select(source, static x => (object)x!);
    }
}
