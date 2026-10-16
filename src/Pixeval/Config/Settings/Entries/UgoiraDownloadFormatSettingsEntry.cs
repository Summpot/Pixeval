// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Linq;
using AutoSettingsPage;
using AutoSettingsPage.Models;
using FluentIcons.Common;
using Pixeval.Config;
using Pixeval.Controls;
using Pixeval.Extensions.Common;
using Pixeval.Extensions.Common.FormatProviders;
using Pixeval.Localization;
using Pixeval.Download;
using Pixeval.Services.Background;
using Pixeval.Media;

namespace Pixeval.Config;

public class UgoiraDownloadFormatSettingsEntry(DownloadFormatsSettings settings, ExtensionService extensions)
    : SingleValueSettingsEntry<DownloadFormatsSettings, object>(
        settings,
        nameof(DownloadFormatsSettings.UgoiraDownloadFormat),
        new SettingsEntryAttribute(Symbol.Gif, nameof(Strings.EnumWorkTypeEnumUgoira), null),
        static settings => new UgoiraDownloadFormatToken(settings.UgoiraDownloadFormat),
        static (settings, value) => settings.UgoiraDownloadFormat = ((UgoiraDownloadFormatToken) value).Value),
        IEnumSettingsEntry<object>
{
    public IReadOnlyList<IReadOnlyStringPair<object>> EnumItems { get; } = CreateEnumItems(extensions);

    private static IReadOnlyList<IReadOnlyStringPair<object>> CreateEnumItems(ExtensionService extensionService)
    {
        var builtIns = SymbolComboBoxItem.GetValues<UgoiraDownloadFormat>()
            .Where(static t => (UgoiraDownloadFormat) t.Value != UgoiraDownloadFormat.Mp4 || MediaEngine.IsMp4Available)
            .Select(t => new SymbolComboBoxItem(UgoiraDownloadFormatToken.BuiltIn((UgoiraDownloadFormat) t.Value), "", t.Symbol) { ResourceKey = t.ResourceKey });

        var extensionItems = extensionService
            .ActiveAnimatedImageFormatProviders
            .Select(t => new SymbolComboBoxItem(UgoiraDownloadFormatToken.Extension(t), t.FormatDescription, t is IEntryExtension entry ? entry.Icon : Symbol.Gif));

        var items = builtIns.Concat(extensionItems).ToArray();
        foreach (var item in items) item.EnableLocalization();
        return items;
    }

    public override object Value
    {
        get
        {
            var value = base.Value;
            return EnumItems.Any(t => Equals(t.Value, value))
                ? value
                : UgoiraDownloadFormatToken.Default;
        }
        set => base.Value = value;
    }
}
