// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Media;

public static class MediaCodec
{
    public static bool IsMp4Available => PixevalMediaMethods.IsMp4Supported();

}
