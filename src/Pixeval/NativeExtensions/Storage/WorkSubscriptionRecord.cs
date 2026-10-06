// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Models.Options;

namespace Pixeval.Native.Storage;

public partial record WorkSubscriptionRecord
{
    public WorkSubscriptionType Type => (WorkSubscriptionType)SubscriptionType;

    public WorkSubscriptionWorkKind Kind => (WorkSubscriptionWorkKind)WorkKind;

    public string Name => Title;

    public string DisplayName => string.IsNullOrWhiteSpace(Title) ? Id.ToString() : Title;

    public string Account => Author;

    public string AvatarUrl => Avatar;

    public WorkSubscriptionRecord(
        long id,
        WorkSubscriptionType subscriptionType,
        WorkSubscriptionWorkKind workKind = WorkSubscriptionWorkKind.Illustration,
        string name = "",
        string account = "",
        string avatarUrl = "",
        string lastCheckTime = "",
        string? lastWorkId = null)
        : this(0, id, (uint)subscriptionType, (uint)workKind, name, account, avatarUrl, lastCheckTime, lastWorkId)
    {
    }
}
