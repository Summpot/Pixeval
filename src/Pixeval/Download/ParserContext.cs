// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Storage;

namespace Pixeval.Download;

public sealed record ParserContext(
    object ArtworkInfo,
    WorkSubscriptionRecord? WorkSubscription = null);
