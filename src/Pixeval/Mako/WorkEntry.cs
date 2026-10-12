// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Pixeval.Pixiv;
using Pixeval.Storage;

namespace Pixeval.Mako;

public partial union WorkEntry : IWorkEntry, IArtworkSerializable
{
    public IWorkEntry AsWorkEntry => this switch
    {
        Illust i => i.Illustration,
        NovelWork n => n.Novel,
        _ => throw new InvalidOperationException("Unsupported work entry type")
    };

    public long Id => AsWorkEntry.Id;

    public User User => AsWorkEntry.User;

    public string Title => AsWorkEntry.Title;

    public DateTimeOffset CreateDateOffset => this switch
    {
        Illust i => i.Illustration.CreateDateOffset,
        NovelWork n => n.Novel.CreateDateOffset,
        _ => default
    };

    public Uri WebsiteUri => this switch
    {
        Illust i => i.Illustration.WebsiteUri,
        NovelWork n => n.Novel.WebsiteUri,
        _ => new("about:blank")
    };

    public Uri AppUri => this switch
    {
        Illust i => i.Illustration.AppUri,
        NovelWork n => n.Novel.AppUri,
        _ => new("about:blank")
    };

    public SafeRating SafeRating => this switch
    {
        Illust i => i.Illustration.SafeRating,
        NovelWork n => n.Novel.SafeRating,
        _ => SafeRating.NotSpecified
    };

    public int Width => this switch
    {
        Illust i => (int) i.Illustration.Width,
        NovelWork n => n.Novel.Width,
        _ => 0
    };

    public int Height => this switch
    {
        Illust i => (int) i.Illustration.Height,
        NovelWork n => n.Novel.Height,
        _ => 0
    };

    public bool IsAiGenerated => this switch
    {
        Illust i => i.Illustration.IsAiGenerated,
        NovelWork n => n.Novel.IsAiGenerated,
        _ => false
    };

    public Series? Series => AsWorkEntry.Series;

    public string Serialize() => AsWorkEntry switch
    {
        IArtworkSerializable s => s.Serialize(),
        _ => throw new InvalidOperationException("WorkEntry is not serializable")
    };

    public string SerializeKey => AsWorkEntry switch
    {
        IArtworkSerializable s => s.SerializeKey,
        _ => throw new InvalidOperationException("WorkEntry is not serializable")
    };

    public string? ThumbnailUrl => this switch
    {
        Illust i => i.Illustration.ThumbnailUrl,
        NovelWork n => n.Novel.ThumbnailUrl,
        _ => null
    };

    public double AspectRatio => this switch
    {
        Illust i => i.Illustration.AspectRatio,
        NovelWork n => n.Novel.AspectRatio,
        _ => 1
    };

    public bool IsBookmarkSupported => this switch
    {
        Illust i => i.Illustration.IsBookmarkSupported,
        NovelWork n => n.Novel.IsBookmarkSupported,
        _ => false
    };

    public bool HasSeries => Series is not null;

    public bool IsPicGif => this switch
    {
        Illust i => i.Illustration.IsPicGif,
        _ => false
    };

    public bool IsPicSet => this switch
    {
        Illust i => i.Illustration.IsPicSet,
        _ => false
    };

    public bool IsPicOne => this switch
    {
        Illust i => i.Illustration.IsPicOne,
        _ => false
    };

    public long TotalBookmarks => this switch
    {
        Illust i => (long) i.Illustration.TotalBookmarks,
        NovelWork n => (long) n.Novel.TotalBookmarks,
        _ => 0L
    };

    public string? OriginalUrl => this switch
    {
        Illust i => i.Illustration.OriginalUrl,
        _ => null
    };

    public System.Collections.Generic.IReadOnlyList<Illustration> Pages => this switch
    {
        Illust i => i.Illustration.Pages,
        _ => []
    };

    public string Platform => PlatformConstants.Pixiv;

    public string Tooltip => this switch
    {
        Illust i => i.Illustration.Tooltip,
        NovelWork n => n.Novel.Tooltip,
        _ => Title
    };

    public string? Caption => this switch
    {
        Illust i => i.Illustration.Caption,
        NovelWork n => n.Novel.Caption,
        _ => null
    };

    public string CreateDate => this switch
    {
        Illust i => i.Illustration.CreateDate,
        NovelWork n => n.Novel.CreateDate,
        _ => ""
    };

    public long TextLength => this switch
    {
        NovelWork n => (long) n.Novel.TextLength,
        _ => 0L
    };

    public long TotalView => this switch
    {
        Illust i => (long) i.Illustration.TotalView,
        NovelWork n => (long) n.Novel.TotalView,
        _ => 0L
    };
}
