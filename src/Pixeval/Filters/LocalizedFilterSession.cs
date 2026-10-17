using System;
using Pixeval.Storage;

namespace Pixeval.Filters;

/// <summary>Owns the localized native engine while preserving the injected storage provider.</summary>
public sealed class LocalizedFilterSession(StorageEngine storage) : IDisposable
{
    private FilterSession? _engine;
    private string? _locale;

    public FilterSession Current
    {
        get
        {
            if (_engine is null || _locale != Strings.CurrentLocale)
            {
                _engine?.Dispose();
                _engine = FilterSession.CreateWithStorage(storage);
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
