using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoSettingsPage.Models;
using Pixeval.Models.Options;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.MigrationValidation;

internal sealed record MigrationInventory(
    NavigationInventory[] Navigation,
    PageInventory[] Pages,
    SettingInventory[] Settings,
    string[] HomeCardSources)
{
    internal static void Export(string path)
    {
        // Export metadata only. Never read IReadOnlySingleValueSettingsEntry.Value: it can
        // contain tokens, cookies, API keys, file paths or third-party extension settings.
        var groups = new SettingsPageViewModel().LocalGroups;
        var navigation = NavigationPageRegistry.Pages
            .OrderBy(static page => page.Key, StringComparer.Ordinal)
            .Select(static page => new NavigationInventory(page.Key, page.PageType.FullName!,
                page.Icon.ToString(), page.HeaderResource, page.NeedLogin))
            .ToArray();
        var pages = AvaloniaHelper.PageTypeToHeaderMap.Keys
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .Select(type => new PageInventory(type.FullName!, navigation.Any(page => page.Type == type.FullName)))
            .ToArray();
        var settings = groups.Select(group => new SettingInventory(group.Token, group.Header, "Group", null,
            [.. group.Select(entry => Describe(entry, group.Token))])).ToArray();
        var sources = Enum.GetValues<HomePageCardSourceKind>();

        if (navigation.Select(static page => page.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != navigation.Length)
            throw new InvalidOperationException("Navigation keys are not unique.");
        foreach (var source in sources)
            if (!AvaloniaHelper.HomeCardSourceKindToHeaderMap.ContainsKey(source))
                throw new InvalidOperationException($"Missing home card registration: {source}");

        var inventory = new MigrationInventory(navigation, pages, settings, [.. sources.Select(static source => source.ToString())]);
        File.WriteAllText(path, JsonSerializer.Serialize(inventory, InventoryJsonContext.Default.MigrationInventory) + Environment.NewLine);
        Console.WriteLine($"Exported {navigation.Length} navigation entries, {pages.Length} pages, {groups.Count} settings groups and {sources.Length} home sources.");
    }

    private static SettingInventory Describe(ISettingsEntry entry, string parent)
    {
        var path = parent + "." + entry.Token;
        List<SettingInventory> children = [];
        if (entry is IMultiValuesWithMainValueSettingsEntry main)
            children.Add(Describe(main.MainValue, path));
        if (entry is IMultiValuesSettingsEntry multiple)
            children.AddRange(multiple.Entries.Select(child => Describe(child, path)));
        var range = entry switch
        {
            INumberSettingsEntry<int> number => $"{number.Min}..{number.Max}; step={number.Step}",
            INumberSettingsEntry<ushort> number => $"{number.Min}..{number.Max}; step={number.Step}",
            _ => null
        };
        return new(path, entry.Header, entry.GetType().Name.Split('`')[0], range, [.. children]);
    }
}

internal sealed record NavigationInventory(string Key, string Type, string Icon, string HeaderResource, bool NeedLogin);
internal sealed record PageInventory(string Type, bool InNavigation);
internal sealed record SettingInventory(string Path, string Header, string Kind, string? Range, SettingInventory[] Children);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(MigrationInventory))]
internal sealed partial class InventoryJsonContext : JsonSerializerContext;
