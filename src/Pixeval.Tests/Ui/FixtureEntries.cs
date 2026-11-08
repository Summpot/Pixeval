using System;
using System.Collections.Generic;

namespace Pixeval.Tests.Ui;

// Readers receive their own immutable-record snapshot, not a reference-counted mutable VM source.
internal sealed class FixtureEntries<T>(IEnumerable<T> entries) : List<T>(entries), IDisposable
{
    internal IReadOnlyList<T> CloneSourceView() => ToArray();
    public void Dispose() => Clear();
}
