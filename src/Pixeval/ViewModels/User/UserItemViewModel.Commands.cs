// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Pixeval.Controls;
using Pixeval.Models.Blocking;
using Pixeval.Utilities;

namespace Pixeval.ViewModels;

public partial class UserItemViewModel
{
    [RelayCommand]
    private async Task FollowAsync()
    {
        if ((IsFollowedDisplay & HeartButtonState.Pending) is not 0)
            return;
        IsFollowedDisplay |= HeartButtonState.Pending; // pre-update
        var result = await MakoHelper.SetFollowAsync(Entry, !IsFollowed);
        IsFollowedDisplay = result ? HeartButtonState.Checked : HeartButtonState.Unchecked;
    }

    private bool CanBlockUser => !BlockedContentHelper.IsBlocked(Entry);

    [RelayCommand(CanExecute = nameof(CanBlockUser))]
    private void BlockUser()
    {
        if (BlockedContentHelper.TryAddOrUpdateBlockedUser(Entry))
            BlockUserCommand.NotifyCanExecuteChanged();
    }
}

