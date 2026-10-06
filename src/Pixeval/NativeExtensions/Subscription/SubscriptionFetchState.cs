// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;

namespace Pixeval.Native.Subscription;

public partial record SubscriptionFetchState
{
    public int WorkSubscriptionId => (int)SubscriptionId;

    public bool IsFetching => Status == SubscriptionStatus.Fetching;

    public int FetchedCount => (int)TotalFetched;

    public DateTimeOffset? RetryAt => null;
}
