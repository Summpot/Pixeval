// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Misaki;
using Pixeval.Native.Storage;

namespace Pixeval.Models.Download;

public sealed record ParserContext(
    IArtworkInfo ArtworkInfo,
    WorkSubscriptionRecord? WorkSubscription = null);
