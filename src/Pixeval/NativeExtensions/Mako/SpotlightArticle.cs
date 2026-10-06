// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Text.Json.Serialization;
using Misaki;
using Pixeval.Models.Pixiv;

namespace Pixeval.Native.Mako;

public partial record SpotlightArticle : IIdEntry
{
    long IIdEntry.Id => Id;

    string IIdentityInfo.Id => Id == 0 ? "" : Id.ToString();

    string IPlatformInfo.Platform => IPlatformInfo.Pixiv;

    [JsonIgnore]
    public string EffectivePureTitle => string.IsNullOrWhiteSpace(PureTitle) ? Title : PureTitle;

    [JsonIgnore]
    public Uri WebsiteUri => new($"https://www.pixivision.net/a/{Id}");

    [JsonIgnore]
    public Uri AppUri => new($"pixeval://spotlight/{Id}");

    [JsonIgnore]
    public DateTimeOffset PublishDateOffset => DateTimeOffset.TryParse(PublishDate, out var dt) ? dt : default;

    [JsonIgnore]
    public SpotlightCategory CategoryEnum => Category.ToLowerInvariant() switch
    {
        "all" => SpotlightCategory.All,
        "spotlight" => SpotlightCategory.Spotlight,
        "tutorial" => SpotlightCategory.Tutorial,
        "inspiration" => SpotlightCategory.Inspiration,
        _ => SpotlightCategory.Spotlight
    };
}
