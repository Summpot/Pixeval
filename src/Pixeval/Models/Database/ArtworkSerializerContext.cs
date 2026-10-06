// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Imouto.BooruParser;
using Misaki;
using Pixeval.Native.Mako;
using Pixeval.Utilities;

namespace Pixeval.Models.Database;

public static class ArtworkSerializerTable
{
    extension<T>(T) where T : ISerializable
    {
        public static string SerializeToken => typeof(T).FullName!;
    }

    public static FrozenDictionary<string, Func<string, ISerializable>> ArtworkTypeMethodsTable { get; } =
        new Dictionary<string, Func<string, ISerializable>>
        {
            // 本地记录中IsFavorite已过时，反序列化时直接设为默认值false
            [Illustration.LegacyIllustrationToken] = s => Illustration.Deserialize(s).Apply(t => t.IsFavorite = false),
            [typeof(Illustration).FullName!] = s => Illustration.Deserialize(s).Apply(t => t.IsFavorite = false),
            ["Pixeval.Models.Pixiv.PixivIllustration"] = s => Illustration.Deserialize(s).Apply(t => t.IsFavorite = false),
            [Novel.LegacyNovelToken] = s => Novel.Deserialize(s).Apply(t => t.IsFavorite = false),
            [typeof(Novel).FullName!] = s => Novel.Deserialize(s).Apply(t => t.IsFavorite = false),
            ["Pixeval.Models.Pixiv.PixivNovel"] = s => Novel.Deserialize(s).Apply(t => t.IsFavorite = false),
            [Post.SerializeToken] = Post.Deserialize
        }.ToFrozenDictionary();
}
