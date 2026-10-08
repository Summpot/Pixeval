// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using UserViewDataProvider = Pixeval.ViewModels.SharableViewDataProvider<
    Pixeval.Native.Mako.User,
    Pixeval.Native.Mako.User>;

namespace Pixeval.ViewModels;

public sealed class UserViewViewModel
    : EntryViewViewModel<User, User>, IRefCloneable<UserViewViewModel>
{
    public UserViewViewModel() : this(new UserViewDataProvider())
    {
    }

    private UserViewViewModel(UserViewDataProvider dataProvider)
    {
        DataProvider = dataProvider;
    }

    public override UserViewDataProvider DataProvider { get; }

    public UserViewViewModel CloneRef() => new(DataProvider.CloneRef());
}
