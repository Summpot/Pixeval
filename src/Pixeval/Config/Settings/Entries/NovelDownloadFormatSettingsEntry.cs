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

namespace Pixeval.Config;

public class NovelDownloadFormatSettingsEntry(DownloadFormatsSettings settings, ExtensionService extensions)
    : SingleValueSettingsEntry<DownloadFormatsSettings, object>(
        settings,
        nameof(DownloadFormatsSettings.NovelDownloadFormat),
        new SettingsEntryAttribute(Symbol.Document, nameof(Strings.EnumWorkTypeEnumNovel), null),
        static settings => new NovelDownloadFormatToken(settings.NovelDownloadFormat),
        static (settings, value) => settings.NovelDownloadFormat = ((NovelDownloadFormatToken) value).Value),
        IEnumSettingsEntry<object>
{
    public IReadOnlyList<IReadOnlyStringPair<object>> EnumItems { get; } = CreateEnumItems(extensions);

    private static IReadOnlyList<IReadOnlyStringPair<object>> CreateEnumItems(ExtensionService extensionService)
    {
        var builtIns = SymbolComboBoxItem.GetValues<NovelDownloadFormat>()
            .Select(t => new SymbolComboBoxItem(NovelDownloadFormatToken.BuiltIn((NovelDownloadFormat) t.Value), "", t.Symbol) { ResourceKey = t.ResourceKey });

        var extensionItems = extensionService
            .ActiveNovelFormatProviders
            .Select(t => new SymbolComboBoxItem(NovelDownloadFormatToken.Extension(t), t.FormatDescription, t is IEntryExtension entry ? entry.Icon : Symbol.Document));

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
                : NovelDownloadFormatToken.Default;
        }
        set => base.Value = value;
    }
}
