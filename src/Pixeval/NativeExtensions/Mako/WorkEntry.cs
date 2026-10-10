// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Pixeval.Models;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Storage;

namespace Pixeval.Native.Mako;

public abstract partial record WorkEntry : IWorkEntry, IArtworkSerializable
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
}
