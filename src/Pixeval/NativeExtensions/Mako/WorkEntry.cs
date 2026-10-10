// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using Misaki;
using Pixeval.Models.Pixiv;

namespace Pixeval.Native.Mako;

public abstract partial record WorkEntry : IArtworkInfo, IWorkEntry
{
    public IWorkEntry AsWorkEntry => this switch
    {
        Illust i => i.Illustration,
        NovelWork n => n.Novel,
        _ => throw new InvalidOperationException("Unsupported work entry type")
    };

    public long RawId => AsWorkEntry.RawId;

    long IIdEntry.Id => AsWorkEntry.RawId;

    string IIdentityInfo.Id => AsWorkEntry.RawId.ToString();

    string IPlatformInfo.Platform => IPlatformInfo.Pixiv;

    public User User => AsWorkEntry.User;

    public string Title => AsWorkEntry.Title;

    public string Description => AsWorkEntry.Description;

    public DateTimeOffset CreateDate => AsWorkEntry.CreateDate;

    public int TotalFavorite => AsWorkEntry.TotalFavorite;

    public int TotalView => AsWorkEntry.TotalView;

    public IPreloadableList<IUser> Authors => AsWorkEntry.Authors;

    public IPreloadableList<IUser> Uploaders => AsWorkEntry.Uploaders;

    public ILookup<ITagCategory, ITag> Tags => AsWorkEntry.Tags;

    public Uri WebsiteUri => AsWorkEntry.WebsiteUri;

    public Uri AppUri => AsWorkEntry.AppUri;

    public ImageType ImageType => AsWorkEntry.ImageType;

    public SafeRating SafeRating => AsWorkEntry.SafeRating;

    public IReadOnlyCollection<IImageFrame> Thumbnails => AsWorkEntry.Thumbnails;

    public IReadOnlyDictionary<string, object> AdditionalInfo => AsWorkEntry.AdditionalInfo;

    public int Width => AsWorkEntry.Width;

    public int Height => AsWorkEntry.Height;

    public bool IsFavorite => AsWorkEntry.IsFavorite;

    public bool IsAiGenerated => AsWorkEntry.IsAiGenerated;

    public Series? Series => AsWorkEntry.Series;

    public string Serialize() => AsWorkEntry.Serialize();

    public string SerializeKey => AsWorkEntry.SerializeKey;
}
