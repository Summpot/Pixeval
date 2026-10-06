// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Misaki;
using Pixeval.Models.Pixiv;

namespace Pixeval.Native.Mako;

public partial record User : IUser, IIdEntry
{
    public User(long id, string name, string account, ProfileImageUrls profileImageUrls, bool isFollowed, string? comment)
        : this(id, name, account, profileImageUrls, isFollowed, comment, [])
    {
    }

    private static readonly Dictionary<string, Uri> s_emptyContact = [];
    private static readonly Dictionary<string, object> s_emptyDict = [];

    private bool? _isFollowed;

    [JsonIgnore]
    public long RawId => Id;

    long IIdEntry.Id => Id;

    string IIdentityInfo.Id => Id == 0 ? "" : Id.ToString();

    [JsonIgnore]
    public string Platform => IPlatformInfo.Pixiv;

    [JsonIgnore]
    public string AvatarUrl => ProfileImageUrls?.Medium
        ?? ProfileImageUrls?.Px170x170
        ?? ProfileImageUrls?.Px50x50
        ?? "";

    [JsonIgnore]
    public string Description => Comment ?? "";

    string IUser.Description => Description;

    IReadOnlyCollection<IImageFrame> IUser.Avatar =>
    [
        new ImageFrame(new ImageSize(170, 170))
        {
            ImageUri = new(string.IsNullOrWhiteSpace(AvatarUrl) ? "avares://Pixeval/Assets/EmptyImage.png" : AvatarUrl)
        }
    ];

    IReadOnlyDictionary<string, Uri> IUser.ContactInformation => s_emptyContact;

    IReadOnlyDictionary<string, object> IUser.AdditionalInfo => s_emptyDict;

    [JsonIgnore]
    public Uri WebsiteUri => new($"https://www.pixiv.net/users/{Id}");

    [JsonIgnore]
    public Uri AppUri => new($"pixeval://user/{Id}");

    [JsonIgnore]
    public bool IsFollowedState
    {
        get => _isFollowed ?? IsFollowed;
        set => _isFollowed = value;
    }
}

public partial record TokenUser : IUser, IIdEntry
{
    private static readonly Dictionary<string, Uri> s_emptyContact = [];
    private static readonly Dictionary<string, object> s_emptyDict = [];

    long IIdEntry.Id => long.TryParse(Id, out var id) ? id : 0;

    string IIdentityInfo.Id => Id;

    string IPlatformInfo.Platform => IPlatformInfo.Pixiv;

    [JsonIgnore]
    public string AvatarUrl => ProfileImageUrls?.Medium
        ?? ProfileImageUrls?.Px170x170
        ?? ProfileImageUrls?.Px50x50
        ?? "";

    [JsonIgnore]
    public string Description => "";

    string IUser.Description => Description;

    IReadOnlyCollection<IImageFrame> IUser.Avatar =>
    [
        new ImageFrame(new ImageSize(170, 170))
        {
            ImageUri = new(string.IsNullOrWhiteSpace(AvatarUrl) ? "avares://Pixeval/Assets/EmptyImage.png" : AvatarUrl)
        }
    ];

    IReadOnlyDictionary<string, Uri> IUser.ContactInformation => s_emptyContact;

    IReadOnlyDictionary<string, object> IUser.AdditionalInfo => s_emptyDict;

    [JsonIgnore]
    public Uri WebsiteUri => new($"https://www.pixiv.net/users/{Id}");

    [JsonIgnore]
    public Uri AppUri => new($"pixeval://user/{Id}");

    public static implicit operator User(TokenUser u) => new(
        long.TryParse(u.Id, out var id) ? id : 0,
        u.Name,
        u.Account,
        u.ProfileImageUrls,
        false,
        null);
}
