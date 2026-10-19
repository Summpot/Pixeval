// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

#pragma warning disable CS0436

using System.Collections.Generic;
using UniFFISharp;

namespace Pixeval.Novel;

public static partial class PixevalNovelMethods
{
    [PreferUniFFI]
    public static partial byte[] BuildEpub(NovelEpubMetadataDto metadata, NovelDocument doc, Dictionary<string, byte[]> images);

    [PreferUniFFI]
    public static partial byte[] BuildEpubFromText(NovelEpubMetadataDto metadata, string text, Dictionary<string, byte[]> images);
}
