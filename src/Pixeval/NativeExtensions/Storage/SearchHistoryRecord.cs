// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;

namespace Pixeval.Native.Storage;

public partial record SearchHistoryRecord
{
    public DateTime ParsedTime
    {
        get
        {
            if (DateTime.TryParse(Time, out var dt))
                return dt;
            if (long.TryParse(Time, out var ticks))
            {
                try
                {
                    return new DateTime(ticks, DateTimeKind.Utc);
                }
                catch
                {
                    // ignore invalid tick range
                }
            }
            return DateTime.MinValue;
        }
    }

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
