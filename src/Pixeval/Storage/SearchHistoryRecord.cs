// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;

namespace Pixeval.Storage;

public partial record struct SearchHistoryRecord
{
    public SearchHistoryRecord() : this(0, "", null, DateTime.UtcNow.ToString("o"))
    {
    }

    public SearchHistoryRecord(string value, string? translatedName, DateTime time)
        : this(0, value, translatedName, time.ToString("o"))
    {
    }

    public SearchHistoryRecord(string value, string? translatedName = null)
        : this(0, value, translatedName, DateTime.UtcNow.ToString("o"))
    {
    }
}
