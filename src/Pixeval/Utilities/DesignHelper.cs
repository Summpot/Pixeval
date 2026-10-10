// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
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

    public static Illustration DownloadParserSampleSingleIllustration => DesignIllustration with { PageCount = 1 };
    public static Illustration DownloadParserSampleAnimatedIllustration => DesignIllustration with { IllustType = "ugoira" };
    public static Illustration DownloadParserSampleImageSetIllustration => DesignIllustration with { PageCount = 5, SetIndex = 0 };
    public static Novel DownloadParserSampleNovel => DesignNovel;
}
