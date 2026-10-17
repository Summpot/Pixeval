using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Pixeval.Download;

namespace Pixeval.Localization;

/// <summary>Reevaluates localized value-type snapshot fields when the language changes.</summary>
public sealed class LocalizedDownloadTextExtension(string property) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => new MultiBinding
    {
        Bindings =
        {
            CompiledBinding.Create<object, object>(static value => value),
            CompiledBinding.Create<LocalizationManager, string>(static manager => manager.CurrentLocale, LocalizationManager.Instance)
        },
        Converter = new SnapshotConverter(property)
    };

    private sealed class SnapshotConverter(string property) : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
            (values.Count > 0 ? values[0] : null, property) switch
            {
                (DownloadItemSnapshot item, "ProgressMessage") => item.ProgressMessage,
                (DownloadItemSnapshot item, "ActionButtonContent") => item.ActionButtonContent,
                (DownloadFolderSnapshot folder, "Title") => folder.Title,
                (DownloadFolderSnapshot folder, "Subtitle") => folder.Subtitle,
                _ => string.Empty
            };
    }
}
