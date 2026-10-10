// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.AppManagement;
using Pixeval.AppManagement.Settings;
using Pixeval.Native.Mako;
using Pixeval.Native.Storage;
using Pixeval.Utilities;

namespace Pixeval.Services;

public sealed class UserSessionService : ObservableObject, IUserSessionService
{
    private readonly MakoClient _makoClient;
    private readonly StorageEngine _storageEngine;
    private readonly LoginContext _loginContext;
    private readonly FileLogger _logger;

    public UserSessionService(
        MakoClient makoClient,
        StorageEngine storageEngine,
        LoginContext loginContext,
        FileLogger logger)
    {
        _makoClient = makoClient;
        _storageEngine = storageEngine;
        _loginContext = loginContext;
        _logger = logger;
    }

    public TokenUser? CurrentUser => _makoClient.GetUser();

    public long CurrentUserId => long.TryParse(CurrentUser?.Id, out var id) ? id : 0;

    public User? CurrentUserEntity => CurrentUser is { } u
        ? new User(CurrentUserId, u.Name, u.Account, u.ProfileImageUrls, false, null)
        : null;

    public bool IsLoggedIn => CurrentUser is not null;

    public event Action<TokenUser?>? UserRefreshed;

    public LoginUserRecord? GetCurrentLoginUser() =>
        _storageEngine.GetLoginUserByKey(_loginContext.CurrentKey);

    public void OnTokenRefreshed(TokenResponse? tokenResponse)
    {
        TokenUser? user = null;
        if (tokenResponse is null)
        {
            _loginContext.CurrentKey = 0;
            _makoClient.ClearToken();
        }
        else
        {
            user = tokenResponse.User ?? _makoClient.GetUser();
            if (user is not null)
            {
                var entry = _storageEngine.UpsertLoginUser(LoginUserRecord.FromTokenUser(tokenResponse.RefreshToken, user));
                _loginContext.CurrentKey = (int)entry.HistoryEntryId;
            }
        }

        void Notify()
        {
            OnPropertyChanged(nameof(CurrentUser));
            OnPropertyChanged(nameof(CurrentUserEntity));
            OnPropertyChanged(nameof(CurrentUserId));
            OnPropertyChanged(nameof(IsLoggedIn));
            UserRefreshed?.Invoke(user);
        }

        if (Dispatcher.UIThread.CheckAccess())
            Notify();
        else
            Dispatcher.UIThread.Post(Notify);

        AppInfo.SaveLoginContext(_loginContext);
    }
}
