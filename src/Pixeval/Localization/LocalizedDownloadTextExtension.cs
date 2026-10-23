using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Pixeval.Download;

namespace Pixeval.Localization;

/// <summary>Reevaluates localized value-type snapshot fields when the language changes.</summary>
public sealed class LocalizedDownloadTextExtension(string property) : MarkupExtension
{
    private static readonly IObservable<string> LocaleChanges = new LocaleObservable();

    public override object ProvideValue(IServiceProvider serviceProvider) => new MultiBinding
    {
        Bindings =
        {
            CompiledBinding.Create<object, object>(static value => value),
            LocaleChanges.ToBinding()
        },
        Converter = new SnapshotConverter(property)
    };

    // Each binding owns its subscription. A shared generated-manager weak event
    // can stop delivery when a listener belongs to an already disposed UI host.
    private sealed class LocaleObservable : IObservable<string>
    {
        public IDisposable Subscribe(IObserver<string> observer)
        {
            var subscription = AppLocalization.Instance.Subscribe(() => observer.OnNext(Strings.CurrentLocale));
            try
            {
                observer.OnNext(Strings.CurrentLocale);
                return subscription;
            }
            catch
            {
                subscription.Dispose();
                throw;
            }
        }
    }

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
