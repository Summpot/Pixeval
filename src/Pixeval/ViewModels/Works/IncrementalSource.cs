// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pixeval.Common.Collections;
using Pixeval.Mako;
using Pixeval.Pixiv;

namespace Pixeval.ViewModels.Works;

public class IncrementalSource<T, TViewModel> : IIncrementalSource<TViewModel>, IDisposable
    where T : notnull
{
    private readonly IAsyncEnumerable<T?> _asyncEnumerable;

    private readonly IPageFeed<T>? _pageFeed;

    private readonly Func<T, int, TViewModel> _factory;

    private readonly int _limit;

    private readonly CancellationTokenSource _lifetimeCts = new();

    private readonly Lock _lifetimeGate = new();

    private readonly IAsyncEnumerator<T?>? _asyncEnumerator;

    private readonly Queue<T> _pageBuffer = new();

    private readonly HashSet<string> _yieldedItems = [];

    private int _yieldedCounter;

    private int _activeRequests;

    private bool _isDisposed;

    private bool _resourcesDisposed;

    private bool _feedExhausted;

    public bool HasMoreItems { get; private set; } = true;

    public bool IsInterrupted { get; private set; }

    public IncrementalSource(IAsyncEnumerable<T?> asyncEnumerable, Func<T, int, TViewModel> factory, int limit = -1)
    {
        ArgumentNullException.ThrowIfNull(asyncEnumerable);
        ArgumentNullException.ThrowIfNull(factory);
        _asyncEnumerable = asyncEnumerable;
        _pageFeed = asyncEnumerable as IPageFeed<T>;
        _factory = factory;
        _limit = limit;
        _asyncEnumerator = _pageFeed is null ? _asyncEnumerable.GetAsyncEnumerator(_lifetimeCts.Token) : null;
    }

    public virtual async Task<IReadOnlyCollection<TViewModel>> GetPagedItemsAsync(int pageIndex, int pageSize, CancellationToken token = default)
    {
        BeginRequest();
        try
        {
            IsInterrupted = false;
            HasMoreItems = true;
            var result = new List<TViewModel>(pageSize);
            if (_pageFeed is { } feed)
                await FillFromFeedAsync(feed, result, pageSize, token).ConfigureAwait(false);
            else
                await FillFromEnumeratorAsync(result, pageSize, token).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_pageFeed is null)
                HasMoreItems = false;
            throw;
        }
        finally
        {
            EndRequest();
        }
    }

    private async Task FillFromFeedAsync(IPageFeed<T> feed, List<TViewModel> result, int pageSize, CancellationToken token)
    {
        while (result.Count < pageSize)
        {
            token.ThrowIfCancellationRequested();
            if (LimitReached())
            {
                HasMoreItems = false;
                return;
            }

            if (_pageBuffer.Count > 0)
            {
                DrainBuffer(result, pageSize);
                continue;
            }

            if (_feedExhausted)
            {
                HasMoreItems = false;
                return;
            }

            IReadOnlyList<T> items;
            bool exhausted;
            try
            {
                (items, exhausted) = await feed.FetchPageAsync(token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                IsInterrupted = true;
                HasMoreItems = true;
                throw;
            }

            _feedExhausted = exhausted;
            foreach (var item in items)
                _pageBuffer.Enqueue(item);

            if (_pageBuffer.Count is 0 && exhausted)
            {
                HasMoreItems = false;
                return;
            }
        }

        HasMoreItems = _pageBuffer.Count > 0 || !_feedExhausted;
        if (LimitReached())
            HasMoreItems = false;
    }

    private async Task FillFromEnumeratorAsync(List<TViewModel> result, int pageSize, CancellationToken token)
    {
        var enumerator = _asyncEnumerator!;
        while (result.Count < pageSize)
        {
            token.ThrowIfCancellationRequested();
            if (LimitReached())
            {
                HasMoreItems = false;
                return;
            }

            if (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();
                if (enumerator.Current is { } obj)
                    TryAdd(result, obj);
            }
            else
            {
                HasMoreItems = false;
                return;
            }
        }
    }

    private void DrainBuffer(List<TViewModel> result, int pageSize)
    {
        while (_pageBuffer.Count > 0 && result.Count < pageSize && !LimitReached())
            TryAdd(result, _pageBuffer.Dequeue());
    }

    private void TryAdd(List<TViewModel> result, T obj)
    {
        if (_yieldedItems.Contains(Identifier(obj)))
            return;
        result.Add(_factory(obj, _yieldedCounter));
        _ = _yieldedItems.Add(Identifier(obj));
        _yieldedCounter++;
    }

    private bool LimitReached() => _limit is not -1 && _yieldedCounter >= _limit;

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        bool disposeResources;
        lock (_lifetimeGate)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            disposeResources = _activeRequests is 0;
            _resourcesDisposed = disposeResources;
        }

        _lifetimeCts.Cancel();
        if (_pageFeed is { } feed)
            feed.Cancel();
        else if (_asyncEnumerable is IFetchEngine<T> { EngineHandle: { } engineHandle })
            engineHandle.Cancel();

        if (disposeResources)
            DisposeResources();
    }

    private void BeginRequest()
    {
        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _activeRequests++;
        }
    }

    private void EndRequest()
    {
        bool disposeResources;
        lock (_lifetimeGate)
        {
            _activeRequests--;
            disposeResources = _isDisposed && _activeRequests is 0 && !_resourcesDisposed;
            if (disposeResources)
                _resourcesDisposed = true;
        }

        if (disposeResources)
            DisposeResources();
    }

    private void DisposeResources()
    {
        try
        {
            if (_asyncEnumerator is { } enumerator)
            {
                var vt = enumerator.DisposeAsync();
                if (!vt.IsCompletedSuccessfully)
                    _ = vt.AsTask().ContinueWith(_ => { }, TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation is the expected result when a page is closed during loading.
        }
        catch
        {
        }
        finally
        {
            _lifetimeCts.Dispose();
        }
    }

    protected virtual string Identifier(T entity) => entity switch
    {
        Pixeval.Mako.Illustration ill => ill.Id.ToString(),
        Pixeval.Mako.Novel n => n.Id.ToString(),
        Pixeval.Booru.BooruPost bp => bp.Id,
        Pixeval.SauceNao.SauceNaoItem sni => sni.RawId,
        Pixeval.Mako.User u => u.Id.ToString(),
        Pixeval.Booru.BooruUser bu => bu.Id,
        Pixeval.Mako.Series s => s.Id.ToString(),
        Pixeval.Mako.SpotlightArticle a => a.Id.ToString(),
        Pixeval.Mako.CommentRecord c => c.Id.ToString(),
        Pixeval.Pixiv.IWorkEntry we => we.Id.ToString(),
        _ => entity.ToString() ?? string.Empty
    };
}
