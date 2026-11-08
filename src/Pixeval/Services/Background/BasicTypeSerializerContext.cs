// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace Pixeval.Services.Background;

public static class DictionaryExtensions
{
    private static string[]? ToStringArray(JsonElement array)
    {
        var result = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                return null;
            result.Add(item.GetString()!);
        }
        return result.ToArray();
    }

    extension(Dictionary<string, object?> dictionary)
    {
        public bool TryGetTarget<T>(string key, [NotNullWhen(true)] out T? result)
        {
            if (dictionary.TryGetValue(key, out var value))
            {
                if (value is JsonElement json)
                {
                    value = json.ValueKind switch
                    {
                        JsonValueKind.String => json.GetString(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Number when json.TryGetInt64(out var integerValue) => integerValue,
                        JsonValueKind.Number => json.GetDouble(),
                        JsonValueKind.Array => ToStringArray(json),
                        _ => null
                    };
                }
                if (value is T typed)
                {
                    result = typed;
                    return true;
                }

                // Object-valued YAML dictionaries erase the extension's declared type.
                // Restore only lossless representations emitted by our settings writer.
                object? restored = null;
                if (typeof(T) == typeof(int) && value is long integer && integer is >= int.MinValue and <= int.MaxValue)
                    restored = (int) integer;
                else if (typeof(T) == typeof(uint) && value is long color && color is >= uint.MinValue and <= uint.MaxValue)
                    restored = (uint) color;
                else if (typeof(T) == typeof(double) && value is long whole)
                {
                    var number = (double) whole;
                    // long.MaxValue rounds up to 2^63 as double; exclude it before casting back.
                    if (number < 9223372036854775808d && (long) number == whole)
                        restored = number;
                }
                else if (typeof(T) == typeof(DateTimeOffset) && value is string timestamp
                         && DateTimeOffset.TryParseExact(timestamp, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    restored = date;
                else if (typeof(T) == typeof(string[]) && value is IEnumerable<object?> items)
                {
                    List<string> strings = [];
                    foreach (var item in items)
                    {
                        if (item is not string text)
                        {
                            result = default;
                            return false;
                        }
                        strings.Add(text);
                    }
                    restored = strings.ToArray();
                }

                if (restored is T converted)
                {
                    result = converted;
                    return true;
                }
            }

            result = default;
            return false;
        }

        public T TryGetTargetOrAddDefault<T>(string key, T defaultValue)
        {
            if (dictionary.TryGetTarget<T>(key, out var value))
                return value;
            
            dictionary[key] = defaultValue;
            return defaultValue;
        }
    }
}
