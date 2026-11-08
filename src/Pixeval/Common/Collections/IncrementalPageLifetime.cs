// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;

namespace Pixeval.Common.Collections;

internal static class IncrementalPageLifetime
{
    // Keep the original failure and finish releasing the entire unpublished part of a page.
    internal static void DisposeItems<T>(IEnumerable<T> items, Exception failure)
    {
        List<Exception>? errors = null;
        foreach (var item in items)
        {
            if (item is not IDisposable disposable)
                continue;
            try
            {
                disposable.Dispose();
            }
            catch (Exception disposeError)
            {
                (errors ??= [failure]).Add(disposeError);
            }
        }
        if (errors is not null)
            throw new AggregateException(errors);
    }
}
