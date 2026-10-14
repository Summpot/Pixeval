// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Mako;

public partial record struct SingleUserResponse
{
    public User UserEntity => User;

    public UserProfile UserProfile => Profile;
}

public partial record struct UserProfile
{
    public long? TotalIllustrations => TotalIllusts;

    public long? TotalMyPixivUsers => TotalMypixivUsers;
}
