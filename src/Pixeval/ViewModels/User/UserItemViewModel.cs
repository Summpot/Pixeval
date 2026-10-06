// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Controls;
using Pixeval.Models.Pixiv;

using Pixeval.Native.Mako;

namespace Pixeval.ViewModels;

public partial class UserItemViewModel : EntryViewModel<User>, IFactory<User, UserItemViewModel>
{
    public static UserItemViewModel CreateInstance(User entry) => new(entry);

    public UserItemViewModel(User user) : base(user)
    {
        IsFollowedDisplay = IsFollowed ? HeartButtonState.Checked : HeartButtonState.Unchecked;
    }

    public bool IsFollowed => Entry.IsFollowedState;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFollowed))]
    public partial HeartButtonState IsFollowedDisplay { get; set; }

    public string Username => Entry.Name;

    public long UserId => Entry.RawId;

    public string AvatarUrl => Entry.AvatarUrl;

    public override Uri AppUri => Entry.AppUri;

    public override Uri WebsiteUri => Entry.WebsiteUri;

    public string? Banner0Url { get; }

    public string? Banner1Url { get; }

    public string? Banner2Url { get; }
}
