// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Pixeval.Views.Converters;

public class NumberEllipsisConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var v = value switch
        {
            int d => (double) d,
            long d => d,
            float d => d,
            double d => d,
            _ => throw new ArgumentException($"{nameof(value)} should be a number", nameof(value))
        };
        if (v < 1000)
            return v % 1d == 0d ? ((long) v).ToString(culture) : v.ToString(culture);
        return $"{v / 1000d:0.#}k";
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();

    internal static readonly NumberEllipsisConverter Instance = new();
}
