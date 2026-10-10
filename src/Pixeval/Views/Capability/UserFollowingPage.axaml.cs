// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Controls;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.Services;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Capability;

public partial class UserFollowingPage : IconContentPage
{
    private static long CurrentUserId =>
        App.Services?.GetService<IUserSessionService>()?.CurrentUserId ?? PixevalSettings.MyId;

    private readonly long _userId;

    public UserFollowingPage() : this(CurrentUserId)
    {
    }

    public UserFollowingPage(long id, PrivacyPolicy privacyPolicy = PrivacyPolicy.Public, UserViewViewModel? viewModel = null)
    {
        InitializeComponent();
        _userId = id;
        PrivacyPolicyComboBox.SelectedValue = privacyPolicy;
        var myId = CurrentUserId;
        if (id <= 0 || id != myId)
            PrivacyPolicyComboBox.IsEnabled = PrivacyPolicyComboBox.IsVisible = false;
        if (viewModel is not null)
            UserContainer.UserView.SetViewModel(viewModel);
        else
        {
            ChangeSource();
        }
    }

    private void WorkTypeComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        ChangeSource();
    }

    private void UserContainer_OnRefreshRequested(object? sender, RoutedEventArgs e)
    {
        ChangeSource();
    }

    private void ChangeSource()
    {
        if (_userId <= 0)
        {
            ResetEngine(AsyncEnumerable.Empty<Pixeval.Native.Mako.User>());
            return;
        }
        var privacy = PrivacyPolicyComboBox.GetSelectedValue<PrivacyPolicy>();
        var makoClient = App.Services?.GetService<MakoClient>() ?? App.AppViewModel.MakoClient;
        ResetEngine(makoClient.UserFollowing(_userId, privacy));
    }

    private void ResetEngine(IAsyncEnumerable<Pixeval.Native.Mako.User> fetchEngine) =>
        (UserContainer.UserView.DataContext as UserViewViewModel)?.ResetEngine(fetchEngine);
}
