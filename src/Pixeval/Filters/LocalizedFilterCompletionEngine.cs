using System;
using Pixeval.Storage;

namespace Pixeval.Filters;

/// <summary>Owns the localized native engine while preserving the injected storage provider.</summary>
public sealed class LocalizedFilterCompletionEngine(StorageEngine storage) : IDisposable
{
    private FilterCompletionEngine? _engine;
    private string? _locale;

    public FilterCompletionEngine Current
    {
        get
        {
            if (_engine is null || _locale != Strings.CurrentLocale)
            {
                _engine?.Dispose();
                _engine = FilterCompletionEngine.CreateWithStorage(storage);
                _locale = Strings.CurrentLocale;
            }
            return _engine;
        }
    }

    public void Dispose()
    {
        _engine?.Dispose();
        _engine = null;
    }
}
