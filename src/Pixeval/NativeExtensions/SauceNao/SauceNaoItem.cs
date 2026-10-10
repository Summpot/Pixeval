// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pixeval.Models;
using Pixeval.Native.Storage;

namespace Pixeval.Native.SauceNao;

public partial record SauceNaoItem : IArtworkSerializable
{
    [JsonIgnore]
    public int SetIndex => -1;

    [JsonIgnore]
    public string RawId => ArtworkId ?? IndexId.ToString();

    [JsonIgnore]
    public string TitleText => !string.IsNullOrWhiteSpace(Title) ? Title : IndexName;

    [JsonIgnore]
    public string Description => $"Similarity: {Similarity:F2}%\nSource: {SourceUrl ?? ""}";

    [JsonIgnore]
    public int TotalFavorite => -1;

    [JsonIgnore]
    public int TotalView => -1;

    [JsonIgnore]
    public bool IsFavorite => false;

    [JsonIgnore]
    public bool IsAiGenerated => false;

    [JsonIgnore]
    public SafeRating SafeRating => IsNsfw ? SafeRating.Explicit : SafeRating.General;

    [JsonIgnore]
    public Uri WebsiteUri => new(SourceUrl ?? ExtUrls.FirstOrDefault() ?? "about:blank");

    [JsonIgnore]
    public Uri AppUri => new($"pixeval://saucenao/{RawId}");

    public const string SauceNaoItemToken = "Pixeval.Native.SauceNao.SauceNaoItem";

    [JsonIgnore]
    public string SerializeKey => SauceNaoItemToken;

    public string Serialize() => JsonSerializer.Serialize(this);

    public static SauceNaoItem Deserialize(string data) => JsonSerializer.Deserialize<SauceNaoItem>(data)!;
}
