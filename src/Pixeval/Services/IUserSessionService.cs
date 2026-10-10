// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.ComponentModel;
using Pixeval.Native.Mako;
using Pixeval.Native.Storage;

namespace Pixeval.Services;

public interface IUserSessionService : INotifyPropertyChanged
{
    TokenUser? CurrentUser { get; }

    User? CurrentUserEntity { get; }

    long CurrentUserId { get; }

    bool IsLoggedIn { get; }

    event Action<TokenUser?>? UserRefreshed;

    void OnTokenRefreshed(TokenResponse? tokenResponse);

    LoginUserRecord? GetCurrentLoginUser();
}
