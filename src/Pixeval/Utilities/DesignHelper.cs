// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using Misaki;
using Pixeval.AppManagement;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
namespace Pixeval.Utilities;

public static class DesignHelper
{
    public static UserViewerPageViewModel DesignUserViewerPageViewModel => field ??= new(123456);

    public static User DesignUserViewModel => DesignUser;

    public static Illustration DesignIllustrationViewModel => DesignIllustration;

    public static Novel DesignNovelViewModel => DesignNovel;

    public static Series DesignSeriesViewModel => DesignSeries;

    public static WorkSeriesInfoViewModel DesignWorkSeriesInfoViewModel => field ??= new(SimpleWorkType.Illustration, 123456, "Title", new(12345, "PrevTitle"), new(1234567, "NextTitle"), "2/3");

    public static SingleUserResponse DesignSingleUserResponse => field ??= new(DesignUser, new UserProfile(
        "WebPage", "Gender", "Birth", "BirthDay", 1990, "Region", 0, "CountryCode", "Job", 0,
        100, 10, 50, 5, 10, 1000, 2, 1, AppInfo.ImageNotAvailablePath, "TwitterAccount", "TwitterUrl", true));

    public static User DesignUser => field ??= new(
        123456, "Username", "Account", new ProfileImageUrls(null, null, null, AppInfo.ImageNotAvailablePath), true, "Comment");

    public static Novel DesignNovel => field ??= new(
        123456, "Title", "Caption", 0, 0, false,
        new ImageUrls(AppInfo.ImageNotAvailablePath, AppInfo.ImageNotAvailablePath, AppInfo.ImageNotAvailablePath, AppInfo.ImageNotAvailablePath),
        DateTimeOffset.UtcNow.ToString("o"),
        [new Tag("Tag A", null), new Tag("Tag B", null)],
        3, 50, DesignUser, new Series(123, "SeriesTitle", null, null, null, null, null, null), false, 123, 456, 3, false, 0);

    public static Illustration DesignIllustration => field ??= new(
        123456, "Title", "illust",
        new ImageUrls(AppInfo.ImageNotAvailablePath, AppInfo.ImageNotAvailablePath, AppInfo.ImageNotAvailablePath, AppInfo.ImageNotAvailablePath),
        "Caption", 0, DesignUser,
        [new Tag("Tag A", null), new Tag("Tag B", null)],
        [], DateTimeOffset.UtcNow.ToString("o"), 3, 800, 600, 0, 0,
        new Series(123, "SeriesTitle", null, null, null, null, null, null),
        new MetaSinglePage(AppInfo.ImageNotAvailablePath),
        [], 456, 123, true, true, false, 3, 0, 0, null);

    public static Series DesignSeries => field ??= new(
        123456,
        "Title",
        DesignUser,
        null,
        AppInfo.ImageNotAvailablePath,
        233,
        0,
        DateTimeOffset.UtcNow.ToString("o"));

    public static ISingleImage DownloadParserSampleWork(ImageType imageType) => new DownloadParserSampleWork(imageType);
}

file record DownloadParserSampleWork(ImageType ImageType) : ISingleImage, IImageSet, ISingleAnimatedImage
{
    public ulong ByteSize => 0;

    public Uri ImageUri => null!;

    public int SetIndex => 0;

    public IPreloadableList<ISingleImage> Pages => null!;

    public int PageCount => 0;

    public SingleAnimatedImageType PreferredAnimatedImageType => SingleAnimatedImageType.MultiFiles;

    public Uri? SingleImageUri => null;

    public IPreloadableList<int>? ZipImageDelays => null;

    public IPreloadableList<(Uri Uri, int MsDelay)>? MultiImageUris => null;

    public IPreloadableList<IAnimatedImageFrame> AnimatedThumbnails => null!;

    public int Width => 0;

    public int Height => 0;

    public string Platform => null!;

    public string Id => "12345678";

    public string Title => nameof(Title);

    public string Description => null!;

    public Uri WebsiteUri => null!;

    public Uri AppUri => null!;

    public DateTimeOffset CreateDate => new(2020, 10, 12, 0, 0, 0, TimeSpan.Zero);

    public IPreloadableList<IUser> Authors { get; } = [];

    public IPreloadableList<IUser> Uploaders => null!;

    public SafeRating SafeRating => default;

    public ILookup<ITagCategory, ITag> Tags => null!;

    public IReadOnlyCollection<IImageFrame> Thumbnails => null!;

    public IReadOnlyDictionary<string, object> AdditionalInfo => null!;

    public int TotalFavorite => 0;

    public int TotalView => 0;

    public bool IsFavorite => false;

    public bool IsAiGenerated => false;
}
