// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Linq;
using Avalonia;
using Avalonia.Controls.Primitives;
using Misaki;
using Pixeval.AppManagement;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;

namespace Pixeval.Controls;

public class UserBasicInfoPresenter : TemplatedControl
{
    public static readonly StyledProperty<IUser?> UserProperty =
        AvaloniaProperty.Register<UserBasicInfoPresenter, IUser?>(nameof(User));

    public static readonly DirectProperty<UserBasicInfoPresenter, string> AvatarUrlProperty =
        AvaloniaProperty.RegisterDirect<UserBasicInfoPresenter, string>(nameof(AvatarUrl), o => o.AvatarUrl);

    public static readonly DirectProperty<UserBasicInfoPresenter, string> UserDisplayNameProperty =
        AvaloniaProperty.RegisterDirect<UserBasicInfoPresenter, string>(nameof(UserDisplayName), o => o.UserDisplayName);

    public static readonly DirectProperty<UserBasicInfoPresenter, string> AccountDisplayProperty =
        AvaloniaProperty.RegisterDirect<UserBasicInfoPresenter, string>(nameof(AccountDisplay), o => o.AccountDisplay);

    static UserBasicInfoPresenter()
    {
        UserProperty.Changed.AddClassHandler<UserBasicInfoPresenter>(static (control, e) =>
        {
            control.UpdateFromUser(e.GetNewValue<IUser?>());
        });
    }

    public UserBasicInfoPresenter()
    {
        UpdateFromUser(User);
    }

    public IUser? User
    {
        get => GetValue(UserProperty);
        set => SetValue(UserProperty, value);
    }

    public string AvatarUrl
    {
        get;
        private set => SetAndRaise(AvatarUrlProperty, ref field, value);
    } = AppInfo.ImageNotAvailablePath;

    public string UserDisplayName
    {
        get;
        private set => SetAndRaise(UserDisplayNameProperty, ref field, value);
    } = "";

    public string AccountDisplay
    {
        get;
        private set => SetAndRaise(AccountDisplayProperty, ref field, value);
    } = "";

    private void UpdateFromUser(IUser? user)
    {
        var avatarUrl = (user as User)?.AvatarUrl
            ?? (user as TokenUser)?.AvatarUrl
            ?? user?.Avatar.FirstOrDefault()?.ImageUri.OriginalString;

        AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl)
            ? AppInfo.ImageNotAvailablePath
            : avatarUrl;
        UserDisplayName = user is null
            ? ""
            : string.IsNullOrWhiteSpace(user.Name)
                ? (user is IIdEntry ide && ide.Id != 0 ? ide.Id.ToString() : user.Id)
                : user.Name;
        var account = (user as User)?.Account
            ?? (user as TokenUser)?.Account;
        AccountDisplay = string.IsNullOrWhiteSpace(account) ? "" : $"@{account}";
    }
}
