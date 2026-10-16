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
using Pixeval.I18N;
using Pixeval.Download;
using Pixeval.Services.Background;

namespace Pixeval.Config;

public class NovelDownloadFormatSettingsEntry(DownloadFormatsSettings settings, ExtensionService extensions)
    : SingleValueSettingsEntry<DownloadFormatsSettings, object>(
        settings,
        nameof(DownloadFormatsSettings.NovelDownloadFormat),
        I18NManager.GetResource(EnumResources.WorkTypeEnum.Novel),
        "",
        Symbol.BookOpen,
        null,
        static settings => new NovelDownloadFormatToken(settings.NovelDownloadFormat),
        static (settings, value) => settings.NovelDownloadFormat = ((NovelDownloadFormatToken) value).Value),
        IEnumSettingsEntry<object>
{
    public IReadOnlyList<IReadOnlyStringPair<object>> EnumItems { get; } = CreateEnumItems(extensions);

    private static IReadOnlyList<IReadOnlyStringPair<object>> CreateEnumItems(ExtensionService extensionService)
    {
        var builtIns = SymbolComboBoxItem.GetValues<NovelDownloadFormat>()
            .Select(t => t with { Value = NovelDownloadFormatToken.BuiltIn((NovelDownloadFormat) t.Value) });

        var extensionItems = extensionService
            .ActiveNovelFormatProviders
            .Select(t => new SymbolComboBoxItem(NovelDownloadFormatToken.Extension(t), t.FormatDescription, t is IEntryExtension entry ? entry.Icon : Symbol.Document));

        return [.. builtIns.Concat(extensionItems)];
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
