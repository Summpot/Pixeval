// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.AppManagement;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;

namespace Pixeval.Native.Storage;

public partial record LoginUserRecord
{
    public long Id => UserId;

    public string AvatarUrl => string.IsNullOrWhiteSpace(Avatar50Url) ? AppInfo.ImageNotAvailablePath : Avatar50Url;

    public string AccountDisplay => string.IsNullOrWhiteSpace(Account) ? "" : $"@{Account}";

    public XRestrict RestrictLevel => (XRestrict)XRestrict;

    public TokenUser TokenUser => new(
        UserId.ToString(),
        Name,
        Account,
        MailAddress,
        IsPremium,
        new ProfileImageUrls(Avatar16Url, Avatar50Url, Avatar170Url, null));

    public static LoginUserRecord FromTokenUser(string refreshToken, TokenUser user)
    {
        long.TryParse(user.Id, out var uid);
        return new(
            0,
            uid,
            user.Name,
            user.Account,
            user.MailAddress,
            user.IsPremium,
            0,
            true,
            false,
            user.ProfileImageUrls?.Px16x16 ?? AppInfo.ImageNotAvailablePath,
            user.ProfileImageUrls?.Px50x50 ?? AppInfo.ImageNotAvailablePath,
            user.ProfileImageUrls?.Px170x170 ?? AppInfo.ImageNotAvailablePath,
            refreshToken);
    }
}
