// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading;
using Pixeval.Pixiv;

namespace Pixeval.Storage;

public interface IArtworkHistorySource
{
    event EventHandler? Changed;

    IAsyncEnumerable<object> StreamAsync(SimpleWorkKind workType, CancellationToken token = default);

    void Clear();
}
