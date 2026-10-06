// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using FluentIcons.Common;
using Pixeval.I18N;
using Pixeval.Native.Config;
using Pixeval.Utilities;

namespace Pixeval.Models.Navigation;

public static class NavigationYamlParser
{
    private static readonly ConfigEngine Engine = new();

    public static NavigationParseResult Parse(string? yaml)
    {
        var text = string.IsNullOrWhiteSpace(yaml) ? NavigationMenuYaml.DefaultYaml : yaml;
        var knownPages = NavigationPageRegistry.PagesByKey.Keys.ToList();
        var knownIcons = Enum.GetNames<Symbol>().ToList();

        var rawResult = Engine.ParseNavigationYaml(text, knownPages, knownIcons, null);

        var diagnostics = rawResult.Diagnostics
            .Select(static d => d with { Message = FormatDiagnosticMessage(d) })
            .ToList();

        var document = rawResult.Settings;
        NavigationConfiguration? configuration = null;

        if (document is not null && diagnostics.Count is 0)
        {
            var newTab = string.IsNullOrWhiteSpace(document.NewTab) ? null : document.NewTab.Trim();
            NavigationPageDefinition? newTabDefinition = null;
            if (newTab is not null)
                NavigationPageRegistry.TryGetPage(newTab, out newTabDefinition);

            var header = BuildItems(document.Header);
            var footer = BuildItems(document.Footer);

            configuration = new NavigationConfiguration(newTab, newTabDefinition, header, footer);
        }

        return rawResult with
        {
            Diagnostics = diagnostics,
            Configuration = configuration
        };
    }

    public static NavigationConfiguration ParseOrDefault(string? yaml)
    {
        var result = Parse(yaml);
        if (result.Configuration is { } configuration)
            return configuration;

        var parseResult = Parse(
            Design.IsDesignMode
                ? NavigationMenuYaml.DefaultYamlForDesigner
                : NavigationMenuYaml.DefaultYaml);
        return parseResult.Configuration!;
    }

    private static List<NavigationMenuItem> BuildItems(IReadOnlyList<NavigationYamlItem>? source)
    {
        if (source is null || source.Count is 0)
            return [];

        var items = new List<NavigationMenuItem>();
        foreach (var item in source)
        {
            if (item.Page is not null && !string.IsNullOrWhiteSpace(item.Page))
            {
                if (BuildPageItem(item) is { } pageItem)
                    items.Add(pageItem);
            }
            else if (item.Folder is not null && !string.IsNullOrWhiteSpace(item.Folder))
            {
                items.Add(BuildFolderItem(item));
            }
        }

        return items;
    }

    private static NavigationPageItem? BuildPageItem(NavigationYamlItem source)
    {
        var page = source.Page!.Trim();
        if (!NavigationPageRegistry.TryGetPage(page, out var definition))
            return null;

        var icon = ResolveIcon(source.Icon, definition.Icon);
        var (header, headerSource) = ResolveDisplayText(source.Title, definition.Header);

        return new NavigationPageItem(
            definition.PageType,
            definition.Key,
            header,
            headerSource,
            icon,
            definition.NeedLogin);
    }

    private static NavigationFolderItem BuildFolderItem(NavigationYamlItem source)
    {
        var folder = source.Folder!.Trim();
        var children = BuildItems(source.Children);
        var icon = ResolveIcon(source.Icon, Symbol.Folder);
        var (header, headerSource) = ResolveDisplayText(folder, folder);
        var needLogin = children.Count > 0 && children.All(static child => child.NeedLogin);

        return new NavigationFolderItem(
            header,
            headerSource,
            icon,
            needLogin,
            children);
    }

    private static Symbol ResolveIcon(string? icon, Symbol fallback)
    {
        if (string.IsNullOrWhiteSpace(icon))
            return fallback;

        return Enum.TryParse<Symbol>(icon.Trim(), true, out var symbol) ? symbol : fallback;
    }

    private static (string Header, string? Source) ResolveDisplayText(string? text, string fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (fallback, null);

        var trimmed = text.Trim();
        if (!trimmed.StartsWith('$'))
            return (trimmed, trimmed);

        if (trimmed.StartsWith("$$", StringComparison.Ordinal))
            return (trimmed[1..], trimmed);

        var resourceKey = trimmed[1..].Trim();
        if (resourceKey.Length is not 0 && I18NManager.TryGetResource(resourceKey, out var value))
            return (value, trimmed);

        return (trimmed, trimmed);
    }

    private static string FormatDiagnosticMessage(NavigationDiagnostic diagnostic)
    {
        return diagnostic.Kind switch
        {
            NavigationDiagnosticKind.YamlSyntaxError => diagnostic.Arguments.Count > 0 ? diagnostic.Arguments[0] : diagnostic.Message,
            NavigationDiagnosticKind.BothHeaderAndFooterEmpty => I18NManager.GetResource(NavigationYamlParserResources.Diagnostics.BothHeaderAndFooterEmpty),
            NavigationDiagnosticKind.MenuMustContainSettingsPage => I18NManager.GetResource(NavigationYamlParserResources.Diagnostics.MenuMustContainSettingsPage),
            NavigationDiagnosticKind.EmptyFolder => I18NManager.GetResource(NavigationYamlParserResources.Diagnostics.EmptyFolderFormatted, [.. diagnostic.Arguments]),
            NavigationDiagnosticKind.FieldNotAllowed => I18NManager.GetResource(NavigationYamlParserResources.Diagnostics.FieldNotAllowedFormatted, [.. diagnostic.Arguments]),
            NavigationDiagnosticKind.I18nResourceKeyEmpty => I18NManager.GetResource(NavigationYamlParserResources.Diagnostics.I18NResourceKeyEmptyFormatted, [.. diagnostic.Arguments]),
            NavigationDiagnosticKind.ItemMustHaveEitherPageOrFolder => I18NManager.GetResource(NavigationYamlParserResources.Diagnostics.ItemMustHaveEitherPageOrFolderFormatted, [.. diagnostic.Arguments]),
            NavigationDiagnosticKind.MaxDepthExceededFolder => I18NManager.GetResource(NavigationYamlParserResources.Diagnostics.MaxDepthExceededFolderFormatted, [.. diagnostic.Arguments]),
            NavigationDiagnosticKind.MaxDepthExceeded => I18NManager.GetResource(NavigationYamlParserResources.Diagnostics.MaxDepthExceededFormatted, [.. diagnostic.Arguments]),
            NavigationDiagnosticKind.UnknownField => I18NManager.GetResource(NavigationYamlParserResources.Diagnostics.UnknownFieldFormatted, [.. diagnostic.Arguments]),
            NavigationDiagnosticKind.UnknownI18nResourceKey => I18NManager.GetResource(NavigationYamlParserResources.Diagnostics.UnknownI18NResourceKeyFormatted, [.. diagnostic.Arguments]),
            NavigationDiagnosticKind.UnknownIcon => I18NManager.GetResource(NavigationYamlParserResources.Diagnostics.UnknownIconFormatted, [.. diagnostic.Arguments]),
            NavigationDiagnosticKind.UnknownPage => I18NManager.GetResource(NavigationYamlParserResources.Diagnostics.UnknownPageFormatted, [.. diagnostic.Arguments]),
            _ => diagnostic.Message
        };
    }
}
