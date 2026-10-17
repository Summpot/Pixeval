// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Text.Json.Serialization;
using Pixeval.Config;

namespace Pixeval.Config;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    Converters = [typeof(RankModeJsonConverter), typeof(WorkKindJsonConverter), typeof(SimpleWorkKindJsonConverter), typeof(PrivacyRestrictJsonConverter)],
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(LoginContext))]
public partial class SettingsSerializerContext : JsonSerializerContext;
