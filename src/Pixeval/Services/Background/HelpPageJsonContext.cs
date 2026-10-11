using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pixeval.Services.Background;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(McpToolsListResponse))]
internal sealed partial class HelpPageJsonContext : JsonSerializerContext;
