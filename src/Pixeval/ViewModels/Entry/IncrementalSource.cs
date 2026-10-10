// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pixeval.Collections;
using Pixeval.Utilities;

namespace Pixeval.ViewModels;

public class IncrementalSource<T, TViewModel> : IIncrementalSource<TViewModel>, IDisposable
    where T : notnull
{
    private readonly IAsyncEnumerable<T?> _asyncEnumerable;

    private readonly Func<T, int, TViewModel> _factory;

    private readonly int _limit;

    private readonly CancellationTokenSource _lifetimeCts = new();

    private readonly Lock _lifetimeGate = new();

    private readonly IAsyncEnumerator<T?> _asyncEnumerator;

    private readonly HashSet<string> _yieldedItems = [];

    private int _yieldedCounter;

    private int _activeRequests;

    private bool _isDisposed;

    private bool _resourcesDisposed;

    public bool HasMoreItems { get; private set; } = true;

    public bool IsInterrupted { get; private set; }

    public IncrementalSource(IAsyncEnumerable<T?> asyncEnumerable, Func<T, int, TViewModel> factory, int limit = -1)
    {
        ArgumentNullException.ThrowIfNull(asyncEnumerable);
        ArgumentNullException.ThrowIfNull(factory);
        _asyncEnumerable = asyncEnumerable;
        _factory = factory;
        _limit = limit;
        // Keep the engine enumerator itself: an interrupted page can be resumed explicitly without automatic retries.
        _asyncEnumerator = _asyncEnumerable.GetAsyncEnumerator(_lifetimeCts.Token);
    }

    public virtual async Task<IReadOnlyCollection<TViewModel>> GetPagedItemsAsync(int pageIndex, int pageSize, CancellationToken token = default)
    {
        BeginRequest();
        try
        {
            IsInterrupted = false;
            HasMoreItems = true;
            var result = new List<TViewModel>(pageSize);
            var i = 0;
            while (i < pageSize)
            {
                token.ThrowIfCancellationRequested();
                if (_limit is not -1 && _yieldedCounter >= _limit)
                {
                    HasMoreItems = false;
                    return result;
                }

                if (await _asyncEnumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    token.ThrowIfCancellationRequested();
                    if (_asyncEnumerator.Current is { } obj && !_yieldedItems.Contains(Identifier(obj)))
                    {
                        result.Add(_factory(obj, _yieldedCounter));
                        _ = _yieldedItems.Add(Identifier(obj));
                        ++i;
                        _yieldedCounter++;
                        if (_limit is not -1 && _yieldedCounter >= _limit)
                        {
                            HasMoreItems = false;
                            return result;
                        }
                    }
                }
                else
                {
                    IsInterrupted = _asyncEnumerable is IFetchEngine<T>
                    {
                        EngineHandle: { IsCompleted: false, IsCancelled: false }
                    };
                    HasMoreItems = IsInterrupted;
                    return result;
                }
            }

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            HasMoreItems = false;
            throw;
        }
        finally
        {
            EndRequest();
        }
    }

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
        if (_asyncEnumerable is IFetchEngine<T> { EngineHandle: { } engineHandle })
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
            var vt = _asyncEnumerator.DisposeAsync();
            if (!vt.IsCompletedSuccessfully)
            {
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
        Pixeval.Native.Mako.Illustration ill => ill.Id.ToString(),
        Pixeval.Native.Mako.Novel n => n.Id.ToString(),
        Pixeval.Native.Booru.BooruPost bp => bp.Id,
        Pixeval.Native.SauceNao.SauceNaoItem sni => sni.RawId,
        Pixeval.Native.Mako.User u => u.Id.ToString(),
        Pixeval.Native.Booru.BooruUser bu => bu.Id,
        Pixeval.Native.Mako.Series s => s.Id.ToString(),
        Pixeval.Native.Mako.SpotlightArticle a => a.Id.ToString(),
        Pixeval.Models.Pixiv.Comment c => c.Id.ToString(),
        Pixeval.Models.Pixiv.IWorkEntry we => we.Id.ToString(),
        _ => entity.ToString() ?? string.Empty
    };
}
