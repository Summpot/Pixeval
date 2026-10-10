// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Avalonia;
using Avalonia.Controls.Primitives;
using Pixeval.AppManagement;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Booru;
using Pixeval.Native.Mako;
using Pixeval.Native.Storage;

namespace Pixeval.Controls;

public class UserBasicInfoPresenter : TemplatedControl
{
    public static readonly StyledProperty<object?> UserProperty =
        AvaloniaProperty.Register<UserBasicInfoPresenter, object?>(nameof(User));

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
            control.UpdateFromUser(e.GetNewValue<object?>());
        });
    }

    public UserBasicInfoPresenter()
    {
        UpdateFromUser(User);
    }

    public object? User
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

    private void UpdateFromUser(object? user)
    {
        var avatarUrl = user switch
        {
            User u => u.AvatarUrl,
            TokenUser tokenUser => tokenUser.AvatarUrl,
            BooruUser booruUser => booruUser.AvatarUrl,
            BlockedUserRecord blocked => blocked.AvatarUrl,
            WorkSubscriptionRecord subscription => subscription.AvatarUrl,
            _ => null
        };

        AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl)
            ? AppInfo.ImageNotAvailablePath
            : avatarUrl;
        UserDisplayName = user switch
        {
            User u => string.IsNullOrWhiteSpace(u.Name) ? u.Id.ToString() : u.Name,
            TokenUser tokenUser => string.IsNullOrWhiteSpace(tokenUser.Name) ? tokenUser.Id.ToString() : tokenUser.Name,
            BooruUser booruUser => string.IsNullOrWhiteSpace(booruUser.Name) ? booruUser.Id.ToString() : booruUser.Name,
            BlockedUserRecord blocked => string.IsNullOrWhiteSpace(blocked.DisplayName) ? blocked.Id.ToString() : blocked.DisplayName,
            WorkSubscriptionRecord subscription => subscription.DisplayName,
            _ => ""
        };
        var account = user switch
        {
            User u => u.Account,
            TokenUser tokenUser => tokenUser.Account,
            BlockedUserRecord blocked => blocked.Account,
            WorkSubscriptionRecord subscription => subscription.Account,
            _ => null
        };
        AccountDisplay = string.IsNullOrWhiteSpace(account) ? "" : $"@{account}";
    }
}
