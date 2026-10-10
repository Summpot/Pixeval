// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Text.Json.Serialization;
using Pixeval.Models.Pixiv;

namespace Pixeval.Native.Mako;

public partial record Series
{
    [JsonIgnore]
    public Series Entry => this;

    [JsonIgnore]
    public SimpleWorkType WorkType { get; init; } = SimpleWorkType.Illustration;

    [JsonIgnore]
    public string? ThumbnailUrl => CoverUrl ?? "";

    [JsonIgnore]
    public Uri AppUri => new($"pixeval://series/{(WorkType is SimpleWorkType.Novel ? "novel" : "illust")}/{Id}");

    [JsonIgnore]
    public Uri WebsiteUri => new(WorkType is SimpleWorkType.Novel
        ? $"https://www.pixiv.net/novel/series/{Id}"
        : $"https://www.pixiv.net/user/{User?.Id}/series/{Id}");

    [JsonIgnore]
    public DateTimeOffset LastPublishedDate =>
        DateTimeOffset.TryParse(LastPublishedContentDatetime, out var dt) ? dt : default;

    [JsonIgnore]
    public int ContentCount => PublishedContentCount ?? 0;

    [JsonIgnore]
    public string Caption => MaskText ?? "";

    [JsonIgnore]
    public bool WatchlistAdded { get; init; }
}
