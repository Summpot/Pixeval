// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.Input;
using Misaki;
using Pixeval.Controls;
using Pixeval.Models.Blocking;
using Pixeval.Models.Pixiv;
using Pixeval.Utilities;

namespace Pixeval.Native.Mako;

public partial record User : IUser, IIdEntry, INotifyPropertyChanged
{
    public User(long id, string name, string account, ProfileImageUrls profileImageUrls, bool isFollowed, string? comment)
        : this(id, name, account, profileImageUrls, isFollowed, comment, [])
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private HeartButtonState? _isFollowedDisplay;

    [JsonIgnore]
    public HeartButtonState IsFollowedDisplay
    {
        get => _isFollowedDisplay ?? (IsFollowedState ? HeartButtonState.Checked : HeartButtonState.Unchecked);
        set
        {
            if (_isFollowedDisplay != value)
            {
                _isFollowedDisplay = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonIgnore]
    public User Entry => this;

    [JsonIgnore]
    public string Username => Name;

    [JsonIgnore]
    public long UserId => RawId;

    [JsonIgnore]
    public string? Banner0Url => SampleWorkThumbnails.Count > 0 ? SampleWorkThumbnails[0] : null;

    [JsonIgnore]
    public string? Banner1Url => SampleWorkThumbnails.Count > 1 ? SampleWorkThumbnails[1] : null;

    [JsonIgnore]
    public string? Banner2Url => SampleWorkThumbnails.Count > 2 ? SampleWorkThumbnails[2] : null;

    [JsonIgnore]
    public IAsyncRelayCommand FollowCommand => field ??= new AsyncRelayCommand(async () =>
    {
        if ((IsFollowedDisplay & HeartButtonState.Pending) is not 0)
            return;

        IsFollowedDisplay |= HeartButtonState.Pending;
        var target = !IsFollowedState;
        var result = await MakoHelper.SetFollowAsync(this, target);
        if (result)
            IsFollowedState = target;

        IsFollowedDisplay = (result ? target : IsFollowedState) ? HeartButtonState.Checked : HeartButtonState.Unchecked;
    });

    [JsonIgnore]
    public IRelayCommand BlockUserCommand => field ??= new RelayCommand(() =>
    {
        _ = BlockedContentHelper.TryAddOrUpdateBlockedUser(this);
    });

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
