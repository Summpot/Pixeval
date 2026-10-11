// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pixeval.Services.Background;

public interface IPixevalMcpService : IAsyncDisposable
{
    Uri? Endpoint { get; }

    Task StartAsync(CancellationToken token = default);

    Task ApplySettingsAsync(CancellationToken token = default);
}
