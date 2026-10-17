// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Linq;
using System.Text.Json.Serialization;
using Pixeval.Filters;
using Pixeval.Pixiv;

namespace Pixeval.SauceNao;

public partial record struct SauceNaoItem
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

    public ArtworkMetadata ToArtworkMetadata() => new(
        RawId,
        TitleText,
        AuthorName ?? string.Empty,
        string.Empty,
        [],
        0,
        0,
        0,
        0,
        SafeRating.IsR18G ? 2 : SafeRating.IsR18 ? 1 : 0,
        0,
        0);
}
