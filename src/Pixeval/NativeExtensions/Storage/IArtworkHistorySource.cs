// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading;
using Pixeval.Models.Pixiv;

namespace Pixeval.Native.Storage;

public interface IArtworkHistorySource
{
    event EventHandler? Changed;

    IAsyncEnumerable<object> StreamAsync(SimpleWorkType workType, CancellationToken token = default);

    void Clear();
}
