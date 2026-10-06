// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.IO;
using Pixeval.Native.Config;
using SharpYaml;

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

        // Keep type conversion and validation in the AOT-compatible generated serializer.
        return YamlSerializer.Deserialize(migratedYaml, SettingsSerializerContext.Default.AppSettings);
    }
}
