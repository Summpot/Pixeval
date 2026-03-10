// Copyright (c) Mako.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Mako.Utilities;

namespace Mako.Net.Response;

public partial record ShowAiSettingsResponse
{
    [JsonPropertyName("show_ai")]
    public required bool ShowAi { get; set; }
}
