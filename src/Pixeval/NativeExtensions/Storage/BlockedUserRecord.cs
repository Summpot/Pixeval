// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Native.Storage;

public partial record BlockedUserRecord
{
    public string Name => UserName;

    public string DisplayName => UserName;

    public BlockedUserRecord(long id, string userName)
        : this(0, id, userName, "", "")
    {
    }
}
