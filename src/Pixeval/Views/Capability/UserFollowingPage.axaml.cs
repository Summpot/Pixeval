// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
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
    public static readonly StyledProperty<long> UserIdProperty =
        AvaloniaProperty.Register<UserFollowingPage, long>(nameof(UserId));

    private static long CurrentUserId =>
        App.Services?.GetService<IUserSessionService>()?.CurrentUserId ?? 0;

    private long _userId;
    private bool _hasViewModel;
    private bool _suppressChangeSource;

    public UserFollowingPage()
    {
        InitializeComponent();
        PrivacyPolicyComboBox.SelectedValue = PrivacyPolicy.Public;
        UpdatePrivacyVisibility();
    }

    public UserFollowingPage(long id, PrivacyPolicy privacyPolicy = PrivacyPolicy.Public, UserViewViewModel? viewModel = null)
    {
        InitializeComponent();
        _suppressChangeSource = true;
        PrivacyPolicyComboBox.SelectedValue = privacyPolicy;
        _suppressChangeSource = false;
        if (viewModel is not null)
        {
            _hasViewModel = true;
            UserContainer.UserView.SetViewModel(viewModel);
        }

        UserId = id;
    }

    public long UserId
    {
        get => GetValue(UserIdProperty);
        set => SetValue(UserIdProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UserIdProperty)
            ApplyUserId(change.GetNewValue<long>());
    }

    private void ApplyUserId(long id)
    {
        if (id == _userId && _userId > 0)
            return;

        _userId = id;
        UpdatePrivacyVisibility();
        if (_hasViewModel)
        {
            _hasViewModel = false;
            return;
        }

        if (!_suppressChangeSource)
            ChangeSource();
    }

    private void UpdatePrivacyVisibility()
    {
        var visible = _userId > 0 && _userId == CurrentUserId;
        PrivacyPolicyComboBox.IsEnabled = PrivacyPolicyComboBox.IsVisible = visible;
    }

    private void WorkTypeComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        if (_suppressChangeSource)
            return;

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
        var makoClient = App.Services!.GetRequiredService<MakoClient>();
        ResetEngine(makoClient.UserFollowing(_userId, privacy));
    }

    private void ResetEngine(IAsyncEnumerable<Pixeval.Native.Mako.User> fetchEngine) =>
        (UserContainer.UserView.DataContext as UserViewViewModel)?.ResetEngine(fetchEngine);
}
