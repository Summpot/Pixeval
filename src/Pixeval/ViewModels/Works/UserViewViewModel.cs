// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Mako;

namespace Pixeval.ViewModels.Works;

public sealed class UserViewViewModel : EntryViewViewModel<User, User>
{
    public UserViewViewModel() : this(null)
    {
    }

    public UserViewViewModel(IBlockedContentService? blockedContent) : base(blockedContent)
    {
    }
}
