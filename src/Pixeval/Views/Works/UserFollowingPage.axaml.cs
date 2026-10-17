// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Interactivity;
using Pixeval.Controls;
using Pixeval.Pixiv;
using Pixeval.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Works;

public partial class UserFollowingPage : IconContentPage
{
    public static readonly StyledProperty<long> UserIdProperty =
        AvaloniaProperty.Register<UserFollowingPage, long>(nameof(UserId));

    private MakoClient? _client;
    private IUserSessionService? _session;
    private bool _sourcePending;
    private long _userId;
    private bool _hasViewModel;
    private bool _suppressChangeSource;

    /// <summary>XAML constructor: services are resolved from <see cref="UiServices.ProviderProperty"/> once the page is attached under the shell.</summary>
    public UserFollowingPage() : this(0, PrivacyPolicy.Public, null, null)
    {
    }

    public UserFollowingPage(MakoClient client, IUserSessionService session)
        : this(session.CurrentUserId, client, session)
    {
    }

    public UserFollowingPage(long id, MakoClient client, IUserSessionService session)
        : this(id, PrivacyPolicy.Public, client, session)
    {
    }

    public UserFollowingPage(
        long id,
        PrivacyPolicy privacyPolicy,
        MakoClient? client,
        IUserSessionService? session,
        UserViewViewModel? viewModel = null)
    {
        _client = client;
        _session = session;
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
        else if (change.Property == UiServices.ProviderProperty && _sourcePending)
        {
            UpdatePrivacyVisibility();
            ChangeSource();
        }
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
        _session ??= this.GetUiService<IUserSessionService>();
        var visible = _userId > 0 && _userId == _session?.CurrentUserId;
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
            ResetSource(AsyncEnumerable.Empty<Pixeval.Mako.User>());
            return;
        }
        _client ??= this.GetUiService<MakoClient>();
        if (_sourcePending = _client is null)
            return;
        var privacy = PrivacyPolicyComboBox.GetSelectedValue<PrivacyPolicy>();
        ResetSource(_client!.UserFollowing(_userId, privacy));
    }

    private void ResetSource(IAsyncEnumerable<Pixeval.Mako.User> fetchEngine) =>
        (UserContainer.UserView.DataContext as UserViewViewModel)?.ResetSource(fetchEngine);
}
