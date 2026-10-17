// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Text.Json.Serialization;
using Pixeval.Pixiv;

namespace Pixeval.Mako;

public partial record struct SpotlightArticle
{
    [JsonIgnore]
    public string? ThumbnailUrl => Thumbnail;

    [JsonIgnore]
    public string EffectivePureTitle => string.IsNullOrWhiteSpace(PureTitle) ? Title : PureTitle;

    [JsonIgnore]
    public Uri WebsiteUri => new($"https://www.pixivision.net/a/{Id}");

    [JsonIgnore]
    public Uri AppUri => new($"pixeval://spotlight/{Id}");

    [JsonIgnore]
    public DateTimeOffset PublishDateOffset => DateTimeOffset.TryParse(PublishDate, out var dt) ? dt : default;

    [JsonIgnore]
    public SpotlightCategory CategoryEnum => Category;
}
