// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Attributes;

namespace Pixeval.Config;

public enum ThumbnailLayoutType
{
    [LocalizedResource(nameof(Strings.EnumThumbnailLayoutTypeLinedFlow))]
    LinedFlow,

    [LocalizedResource(nameof(Strings.EnumThumbnailLayoutTypeGrid))]
    Grid,

    [LocalizedResource(nameof(Strings.EnumThumbnailLayoutTypeMasonry))]
    Masonry,

    VerticalUniformStack,

    HorizontalUniformStack,

    VerticalStack,

    HorizontalStack
}
