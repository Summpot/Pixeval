// Copyright (c) Mako.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using Mako.Global;

namespace Mako.Net;

public abstract class MakoClientSupportedHttpMessageHandler(MakoClient makoClient) : HttpMessageHandler, IMakoClientSupport
{
    public MakoClient MakoClient { get; } = makoClient;

    private HttpMessageInvoker? _domainFrontingInvoker;
    private HttpMessageInvoker? _directInvoker;
    private IWebProxy? _lastCachedProxy;
    private readonly object _invokerSync = new();
    private readonly List<HttpMessageInvoker> _retiredInvokers = [];

    public HttpMessageInvoker GetHttpMessageInvoker(bool domainFronting)
    {
        lock (_invokerSync)
        {
            if (domainFronting)
            {
                _domainFrontingInvoker ??= MakoClient.CreateHttpMessageInvoker();
                return _domainFrontingInvoker;
            }

            var currentProxy = MakoClient.CurrentSystemProxy;
            // Create on first call or recreate if proxy has changed.
            // Do NOT dispose old invoker immediately, otherwise in-flight requests may throw ObjectDisposedException.
            if (_directInvoker is null || !ReferenceEquals(_lastCachedProxy, currentProxy))
            {
                if (_directInvoker is { } oldInvoker)
                    _retiredInvokers.Add(oldInvoker);

                _directInvoker = MakoClient.CreateDirectHttpMessageInvoker();
                _lastCachedProxy = currentProxy;
            }

            return _directInvoker;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_invokerSync)
            {
                _domainFrontingInvoker?.Dispose();
                _directInvoker?.Dispose();

                foreach (var invoker in _retiredInvokers)
                    invoker.Dispose();

                _retiredInvokers.Clear();
            }
        }

        base.Dispose(disposing);
    }
}
