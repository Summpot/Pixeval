// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pixeval.Native.Mako;

namespace Pixeval.Models.Pixiv;

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

public interface IFetchEngine<out T> : IAsyncEnumerable<T>
{
    IFetchEngineHandle EngineHandle => DummyEngineHandle.Instance;
}

public interface IFetchEngineHandle
{
    void Cancel();
    bool IsCancelled => false;
    bool IsCompleted { get => false; set { } }
}

public sealed class DummyEngineHandle : IFetchEngineHandle
{
    private int _cancelled;
    public static readonly DummyEngineHandle Instance = new();
    public void Cancel() => Interlocked.Exchange(ref _cancelled, 1);
    public bool IsCancelled => _cancelled != 0;
    public bool IsCompleted { get; set; }
}

public sealed class AsyncFetchEngine<T>(IAsyncEnumerable<T> source, Action? onCancel = null) : IFetchEngine<T>
{
    public IFetchEngineHandle EngineHandle { get; } = new ActionEngineHandle(onCancel);

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => source.GetAsyncEnumerator(cancellationToken);
}

public sealed class ActionEngineHandle(Action? onCancel) : IFetchEngineHandle
{
    private int _cancelled;
    public bool IsCancelled => _cancelled != 0;
    public bool IsCompleted { get; set; }

    public void Cancel()
    {
        if (Interlocked.Exchange(ref _cancelled, 1) == 0)
            onCancel?.Invoke();
    }
}

public static class FetchEngineExtensions
{
    public static IFetchEngine<T> ToFetchEngine<T>(this IAsyncEnumerable<T> source, Action? onCancel = null)
    {
        if (source is IFetchEngine<T> existing && onCancel is null)
            return existing;
        return new AsyncFetchEngine<T>(source, onCancel ?? (source is IFetchEngine<T> e ? e.EngineHandle.Cancel : null));
    }
}
