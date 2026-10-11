// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Mako;

public partial record SingleUserResponse
{
    public User UserEntity => User;

    public UserProfile UserProfile => Profile;
}

public partial record UserProfile
{
    public long? TotalIllustrations => TotalIllusts;

    public long? TotalMyPixivUsers => TotalMypixivUsers;
}
