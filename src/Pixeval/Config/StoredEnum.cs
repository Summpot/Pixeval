using System;

namespace Pixeval.Config;

// The current persisted format uses zero-based declaration indices, independently of FFI tags.
internal static class StoredEnum
{
    public static T FromIndex<T>(uint index) where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        return index < values.Length ? values[index] : throw new ArgumentOutOfRangeException(nameof(index));
    }
    public static uint ToIndex<T>(T value) where T : struct, Enum
    {
        var index = Array.IndexOf(Enum.GetValues<T>(), value);
        return index >= 0 ? (uint)index : throw new ArgumentOutOfRangeException(nameof(value));
    }
}
