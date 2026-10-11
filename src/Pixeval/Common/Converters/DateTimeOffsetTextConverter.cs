// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Pixeval.I18N;

namespace Pixeval.Views.Converters;

public sealed class DateTimeOffsetTextConverter : IValueConverter
{
    public static readonly DateTimeOffsetTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTimeOffset date
            ? date.ToString("O", CultureInfo.InvariantCulture)
            : throw new ArgumentException($"Expected {nameof(DateTimeOffset)}.", nameof(value));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string text && DateTimeOffset.TryParseExact(text, "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : new BindingNotification(new DataValidationException(
                I18NManager.GetResource(SettingsMainViewResources.InvalidDateTimeOffset)), BindingErrorType.DataValidationError);
}
