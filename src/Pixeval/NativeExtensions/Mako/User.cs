// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Text.Json.Serialization;
using Pixeval.Models;

namespace Pixeval.Native.Mako;

public partial record User
{
    public User(long id, string name, string account, ProfileImageUrls profileImageUrls, bool isFollowed, string? comment)
        : this(id, name, account, profileImageUrls, isFollowed, comment, [])
    {
    }

    [JsonIgnore]
    public long UserId => RawId;

    [JsonIgnore]
    public string? Banner0Url => SampleWorkThumbnails.Count > 0 ? SampleWorkThumbnails[0] : null;

    [JsonIgnore]
    public string? Banner1Url => SampleWorkThumbnails.Count > 1 ? SampleWorkThumbnails[1] : null;

    [JsonIgnore]
    public string? Banner2Url => SampleWorkThumbnails.Count > 2 ? SampleWorkThumbnails[2] : null;

    [JsonIgnore]
    public long RawId => Id;

    [JsonIgnore]
    public string Platform => PlatformConstants.Pixiv;

    [JsonIgnore]
    public string AvatarUrl => ProfileImageUrls?.Medium
        ?? ProfileImageUrls?.Px170x170
        ?? ProfileImageUrls?.Px50x50
        ?? "";

    [JsonIgnore]
    public string Description => Comment ?? "";

    [JsonIgnore]
    public Uri WebsiteUri => new($"https://www.pixiv.net/users/{Id}");

    [JsonIgnore]
    public Uri AppUri => new($"pixeval://user/{Id}");
}

public partial record TokenUser
{
    [JsonIgnore]
    public long RawId => long.TryParse(Id, out var id) ? id : 0;

    [JsonIgnore]
    public string Platform => PlatformConstants.Pixiv;

    [JsonIgnore]
    public string AvatarUrl => ProfileImageUrls?.Medium
        ?? ProfileImageUrls?.Px170x170
        ?? ProfileImageUrls?.Px50x50
        ?? "";

    [JsonIgnore]
    public string Description => "";

    [JsonIgnore]
    public Uri WebsiteUri => new($"https://www.pixiv.net/users/{Id}");

    [JsonIgnore]
    public Uri AppUri => new($"pixeval://user/{Id}");
}
