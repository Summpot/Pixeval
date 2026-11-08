using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Web;
using Avalonia.Platform;
using Pixeval.Mako;
using Pixeval.ViewModels;

namespace Pixeval.Tests.Ui;

internal sealed class NovelReaderFixture : IDisposable
{
    internal const string ImageUri = "https://i.pximg.net/m3e/novel-reader.png";
    internal const string Title = "长篇小说 / A very long novel title ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    internal OfflineSearchFixture Http { get; } = new(App.AppViewModel.MakoClient);
    internal Channel<Pending> ContentRequests { get; } = Channel.CreateUnbounded<Pending>();
    private readonly ConcurrentBag<Pending> _pending = [];

    internal NovelReaderFixture()
    {
        using var asset = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png"));
        using var buffer = new MemoryStream();
        asset.CopyTo(buffer);
        Http.ImageResponses[ImageUri] = buffer.ToArray();
        Http.WorkCardResponseFactory = async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath != "/webview/v2/novel")
                return "{}";
            var pending = new Pending(long.Parse(HttpUtility.ParseQueryString(request.RequestUri.Query)["id"]!), token);
            _pending.Add(pending);
            await ContentRequests.Writer.WriteAsync(pending, token);
            return await pending.Response.Task;
        };
        Http.ArtworkDetailResponseFactory = (request, _) => Task.FromResult(NativeFixtureData.Serialize(new
        {
            novel = Sample(long.Parse(HttpUtility.ParseQueryString(request.RequestUri!.Query)["novel_id"]!))
        }));
        Http.CommentResponseFactory = (_, _) => Task.FromResult("{\"comments\":[],\"next_url\":null}");
        Http.NovelTextResponseFactory = (_, _) => Task.FromException<string>(new HttpRequestException("Fixed novel fallback content unavailable"));
        Http.RelatedResponseFactory = (_, _) => Task.FromResult("{\"novels\":[],\"next_url\":null}");
    }

    internal Task<Pending> NextAsync() => ContentRequests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

    internal static Mako.Novel Sample(long id) => Mako.Novel.CreateDefault() with
    {
        Id = id,
        Title = Title + " / " + id,
        Series = null,
        User = User.CreateDefault() with { Id = 900001, Name = "Fixture author", ProfileImageUrls = new() { Medium = ImageUri } },
        ImageUrls = new() { Medium = ImageUri, Large = ImageUri, SquareMedium = ImageUri }
    };

    internal static string Html(long id, string text, bool withImages = false)
    {
        var content = new
        {
            id,
            title = Title,
            userId = 900001,
            text,
            coverUrl = ImageUri,
            rating = new { like = 0, bookmark = 0, view = 0 },
            seriesId = (long?) null,
            seriesTitle = (string?) null,
            seriesIsWatched = (bool?) null,
            tags = Array.Empty<string>(),
            caption = "",
            cdate = "2026-01-01T00:00:00Z",
            marker = (object?) null,
            illusts = withImages ? new Dictionary<string, NovelIllustration>
            {
                ["994101-1"] = new NovelIllustration() with
                {
                    Id = 994101,
                    Page = 1,
                                Illust = new NovelIllustrationInfo() with
                    {
                        Images = new NovelIllustrationUrls() with { Medium = ImageUri }
                    }
                }
            } : [],
            images = withImages ? new Dictionary<string, object>
            {
                ["11"] = new
                {
                    novelImageId = 11,
                    urls = new { x1200 = ImageUri, original = ImageUri }
                }
            } : [],
            seriesNavigation = (object?) null,
            glossaryItems = Array.Empty<object>(),
            replaceableItemIds = Array.Empty<long>(),
            aiType = 0,
            isOriginal = true,
            seasonalEffectTagData = (string?) null,
            eventBanners = (string?) null,
            language = "ja"
        };
        // Preserve the nested bootstrap-object boundary consumed by Mako's webview parser.
        return "<script>{{{}" + NativeFixtureData.Serialize(content) + "}}</script>";
    }

    public void Dispose()
    {
        foreach (var request in _pending)
            request.Response.TrySetCanceled();
        Http.Dispose();
    }

    internal sealed record Pending(long Id, CancellationToken Token)
    {
        internal TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Complete(string text, bool withImages = false) => Response.SetResult(Html(Id, text, withImages));
        internal void Fail() => Response.SetException(new HttpRequestException("Fixed novel content unavailable"));
    }
}
