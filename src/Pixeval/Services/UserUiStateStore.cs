// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Controls;
using Pixeval.Native.Mako;

namespace Pixeval.Services;

public sealed partial class UserUiState : ObservableObject
{
    [ObservableProperty]
    private HeartButtonState _followState;

    public UserUiState(HeartButtonState followState)
    {
        _followState = followState;
    }
}

public static class UserUiStateStore
{
    private static readonly ConcurrentDictionary<long, UserUiState> s_states = new();

    public static UserUiState GetOrCreate(User user)
    {
        return s_states.GetOrAdd(user.RawId, _ =>
        {
            var followState = user.IsFollowed ? HeartButtonState.Checked : HeartButtonState.Unchecked;
            return new UserUiState(followState);
        });
    }

    public static bool TryGetState(User user, out UserUiState? state)
    {
        return s_states.TryGetValue(user.RawId, out state);
    }

    public static void SetFollowPending(User user)
    {
        var state = GetOrCreate(user);
        state.FollowState |= HeartButtonState.Pending;
    }

    public static void SetFollowState(User user, bool isFollowed)
    {
        var state = GetOrCreate(user);
        state.FollowState = isFollowed ? HeartButtonState.Checked : HeartButtonState.Unchecked;
    }

    public static void SetFollowState(User user, HeartButtonState followState)
    {
        var state = GetOrCreate(user);
        state.FollowState = followState;
    }

    public static void UpdateFollow(User user, HeartButtonState followState) => SetFollowState(user, followState);

    public static void RevertFollowPending(User user, bool? fallback = null)
    {
        var state = GetOrCreate(user);
        if (fallback.HasValue)
        {
            state.FollowState = fallback.Value ? HeartButtonState.Checked : HeartButtonState.Unchecked;
        }
        else
        {
            state.FollowState &= ~HeartButtonState.Pending;
        }
    }

    public static void Clear()
    {
        s_states.Clear();
    }
}
