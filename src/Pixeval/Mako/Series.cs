// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Text.Json.Serialization;
using Pixeval.Pixiv;

namespace Pixeval.Mako;

public partial record struct Series
{
    [JsonIgnore]
    public SimpleWorkKind WorkKind { get; init; } = SimpleWorkKind.Illustration;

    [JsonIgnore]
    public string? ThumbnailUrl => CoverUrl ?? "";

    [JsonIgnore]
    public Uri AppUri => new($"pixeval://series/{(WorkKind is SimpleWorkKind.Novel ? "novel" : "illust")}/{Id}");

    [JsonIgnore]
    public Uri WebsiteUri => new(WorkKind is SimpleWorkKind.Novel
        ? $"https://www.pixiv.net/novel/series/{Id}"
        : $"https://www.pixiv.net/user/{User?.Id}/series/{Id}");

    [JsonIgnore]
    public string Caption => MaskText ?? "";

    [JsonIgnore]
    public bool WatchlistAdded { get; init; }
}
