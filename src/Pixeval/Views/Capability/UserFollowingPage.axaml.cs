// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Interactivity;
using Pixeval.Controls;
using Pixeval.Models.Pixiv;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Capability;

public partial class UserFollowingPage : IconContentPage
{
    private readonly long _userId;

    public UserFollowingPage() : this(PixevalSettings.MyId)
    {
    }

    public UserFollowingPage(long id, PrivacyPolicy privacyPolicy = PrivacyPolicy.Public, UserViewViewModel? viewModel = null)
    {
        InitializeComponent();
        _userId = id;
        PrivacyPolicyComboBox.SelectedValue = privacyPolicy;
        if (id <= 0 || id != PixevalSettings.MyId)
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
        ResetEngine(App.AppViewModel.MakoClient.UserFollowing(_userId, privacy));
    }

    private void ResetEngine(IAsyncEnumerable<Pixeval.Native.Mako.User> fetchEngine) =>
        (UserContainer.UserView.DataContext as UserViewViewModel)?.ResetEngine(fetchEngine);
}
