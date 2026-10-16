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

public class IllustrationDownloadFormatSettingsEntry(DownloadFormatsSettings settings, ExtensionService extensions)
    : SingleValueSettingsEntry<DownloadFormatsSettings, object>(
        settings,
        nameof(DownloadFormatsSettings.IllustrationDownloadFormat),
        I18NManager.GetResource(EnumResources.WorkTypeEnum.Illustration),
        "",
        Symbol.Image,
        null,
        static settings => new IllustrationDownloadFormatToken(settings.IllustrationDownloadFormat),
        static (settings, value) => settings.IllustrationDownloadFormat = ((IllustrationDownloadFormatToken) value).Value),
        IEnumSettingsEntry<object>
{
    public IReadOnlyList<IReadOnlyStringPair<object>> EnumItems { get; } = CreateEnumItems(extensions);

    private static IReadOnlyList<IReadOnlyStringPair<object>> CreateEnumItems(ExtensionService extensionService)
    {
        var builtIns = SymbolComboBoxItem.GetValues<IllustrationDownloadFormat>()
            .Where(static t => (IllustrationDownloadFormat) t.Value is IllustrationDownloadFormat.Original)
            .Select(t => t with { Value = IllustrationDownloadFormatToken.BuiltIn((IllustrationDownloadFormat) t.Value) });

        var extensionItems = extensionService
            .ActiveStaticImageFormatProviders
            .Select(t => new SymbolComboBoxItem(IllustrationDownloadFormatToken.Extension(t), t.FormatDescription, t is IEntryExtension entry ? entry.Icon : Symbol.Image));

        return [.. builtIns.Concat(extensionItems)];
    }

    public override object Value
    {
        get
        {
            var value = base.Value;
            return EnumItems.Any(t => Equals(t.Value, value))
                ? value
                : IllustrationDownloadFormatToken.Default;
        }
        set => base.Value = value;
    }
}
