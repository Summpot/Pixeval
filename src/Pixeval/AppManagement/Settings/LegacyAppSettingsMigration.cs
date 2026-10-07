// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.IO;
using System.Text.Json;
using Pixeval.Native.Config;

namespace Pixeval.AppManagement.Settings;

/// <summary>
/// Migration for settings written before the nested expander settings were introduced,
/// delegated directly to Rust Core ConfigEngine.
/// </summary>
public static class LegacyAppSettingsMigration
{
    private static readonly ConfigEngine Engine = new();

    public static AppSettings? Deserialize(Stream stream)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var rawYaml = reader.ReadToEnd();
        var migratedYaml = Engine.MigrateYaml(rawYaml);
        var json = Engine.YamlToJson(migratedYaml);
        return JsonSerializer.Deserialize(json, SettingsSerializerContext.Default.AppSettings);
    }
}
