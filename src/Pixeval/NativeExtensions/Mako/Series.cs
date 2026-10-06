// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Text.Json.Serialization;
using Misaki;
using Pixeval.Models.Pixiv;

namespace Pixeval.Native.Mako;

public partial record Series : IIdEntry
{
    long IIdEntry.Id => Id;

    string IIdentityInfo.Id => Id.ToString();

    string IPlatformInfo.Platform => IPlatformInfo.Pixiv;

    [JsonIgnore]
    public DateTimeOffset LastPublishedDate =>
        DateTimeOffset.TryParse(LastPublishedContentDatetime, out var dt) ? dt : default;

    [JsonIgnore]
    public int ContentCount => PublishedContentCount ?? 0;

    [JsonIgnore]
    public string Caption => MaskText ?? "";

    [JsonIgnore]
    public bool WatchlistAdded { get; set; }
}
