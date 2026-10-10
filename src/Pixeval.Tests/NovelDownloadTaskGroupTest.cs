using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.AppManagement;
using Pixeval.Native.Mako;
using Pixeval.ViewModels;

namespace Pixeval.Tests;

[TestClass]
public sealed class NovelDownloadTaskGroupTest
{
    [TestMethod]
    public void BuiltInDocumentsShouldReferenceDownloadedOriginalImageNames()
    {
        using var context = new NovelContext(CreateNovelContent());
        ((INovelContext<Stream>) context).InitImages();
        context.SetStream(0, new MemoryStream());
        context.SetStream(1, new MemoryStream());
        context.SetStream(2, new MemoryStream());

        Assert.AreSequenceEqual((string[]) ["cover.png", "101.png", "202-2.webp"], context.AllFileNames);

        var html = context.LoadHtmlContent().ToString();
        Assert.Contains("src=\"101.png\"", html);
        Assert.Contains("src=\"202-2.webp\"", html);

        var markdown = context.LoadMdContent().ToString();
        Assert.Contains("![101](101.png)", markdown);
        Assert.Contains("![202-2](202-2.webp)", markdown);
    }

    [TestMethod]
    public void BuiltInDocumentsShouldReferenceDownloadedCover()
    {
        var content = CreateNovelContent() with { CoverUrl = "https://i.pximg.net/img-original/novel/1.jpg?token=cover" };
        using var context = new NovelContext(content);
        ((INovelContext<Stream>) context).InitImages();
        var coverStream = new MemoryStream();
        var uploadedImageStream = new MemoryStream();
        var illustrationStream = new MemoryStream();
        context.SetStream(0, coverStream);
        context.SetStream(1, uploadedImageStream);
        context.SetStream(2, illustrationStream);

        Assert.AreEqual("cover.jpg", context.CoverFileName);
        Assert.AreEqual(3, context.TotalImagesCount);
        Assert.AreSequenceEqual((string[]) ["cover.jpg", "101.png", "202-2.webp"], context.AllFileNames);
        Assert.AreSame(coverStream, context.TryGetStream(0));
        Assert.AreSame(uploadedImageStream, context.TryGetStream(1));
        Assert.AreSame(illustrationStream, context.TryGetStream(2));

        var html = context.LoadHtmlContent().ToString();
        Assert.Contains("<img src=\"cover.jpg\" alt=\"cover\" />", html);
        Assert.IsLessThan(html.IndexOf("101.png", StringComparison.Ordinal), html.IndexOf("cover.jpg", StringComparison.Ordinal));

        var markdown = context.LoadMdContent().ToString();
        Assert.Contains("![cover](cover.jpg)", markdown);
        Assert.IsLessThan(markdown.IndexOf("101.png", StringComparison.Ordinal), markdown.IndexOf("cover.jpg", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not a uri")]
    [DataRow("file:///cover.jpg")]
    public void InvalidCoverUrlShouldUseDefaultImage(string coverUrl)
    {
        var content = CreateNovelContent() with { CoverUrl = coverUrl };
        using var context = new NovelContext(content);

        Assert.AreEqual(AppInfo.ImageNotAvailablePath, context.CoverUri.OriginalString);
        Assert.AreEqual("cover.png", context.CoverFileName);
        Assert.AreEqual(AppInfo.ImageNotAvailablePath, context.AllUrls[0]);
        Assert.AreEqual("cover.png", context.AllFileNames[0]);
    }

    private static NovelContent CreateNovelContent() => NovelContent.CreateDefault() with
    {
        Id = 1,
        Title = "Novel",
        UserId = 1,
        Text = "[uploadedimage:101]\n[pixivimage:202-2]",
        Illusts =
        [
            new(
                true,
                null,
                new("", "", 0, 0, 0, [], new(null, "https://i.pximg.net/c/600x1200/novel/202_p1.webp?token=thumbnail", null)),
                new(1, "", ""),
                202,
                2)
        ],
        Images =
        [
            new(
                101,
                0,
                new("", "", "https://i.pximg.net/c/1200x1200/novel/101.jpg?token=thumbnail", "", "https://i.pximg.net/img-original/novel/101.png?token=original"))
        ]
    };
}
