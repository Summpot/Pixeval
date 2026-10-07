// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Pixeval.AppManagement.Settings;
using Pixeval.Models.Home;

namespace Pixeval.AppManagement;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(LoginContext))]
[JsonSerializable(typeof(ObservableCollection<HomePageCardLayout>))]
public partial class SettingsSerializerContext : JsonSerializerContext;
