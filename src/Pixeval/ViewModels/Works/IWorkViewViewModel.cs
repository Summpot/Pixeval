// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;

namespace Pixeval.ViewModels.Works;

public interface IWorkViewViewModel : IOperableViewViewModel, IDisposable
{
    void ResetSource(IAsyncEnumerable<object>? source, int itemsPerPage = 20, int itemLimit = -1);
}
