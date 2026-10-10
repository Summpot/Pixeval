// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Native.Storage;

namespace Pixeval.Models.Download;

public sealed record ParserContext(
    object ArtworkInfo,
    WorkSubscriptionRecord? WorkSubscription = null);
