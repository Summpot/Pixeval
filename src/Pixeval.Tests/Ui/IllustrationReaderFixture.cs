using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Web;
using Avalonia.Platform;
using Mako.Model;
using Misaki;

namespace Pixeval.Tests.Ui;

internal sealed class IllustrationReaderFixture : IDisposable
{
    internal const string ImageUri = "https://i.pximg.net/m3e/illustration-reader.png";
    internal const string Title = "完整插画标题 / A very long illustration title ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    internal OfflineSearchFixture Http { get; } = new(App.AppViewModel.MakoClient);
    private readonly Channel<Pending> _details = Channel.CreateUnbounded<Pending>();
    private readonly ConcurrentBag<Pending> _pending = [];

    internal IllustrationReaderFixture()
    {
        using var asset = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png"));
        using var buffer = new MemoryStream();
        asset.CopyTo(buffer);
        Http.ImageResponses[ImageUri] = buffer.ToArray();
        Http.ArtworkDetailResponseFactory = async (request, token) =>
        {
            var pending = new Pending(long.Parse(HttpUtility.ParseQueryString(request.RequestUri!.Query)["illust_id"]!), token);
            _pending.Add(pending);
            await _details.Writer.WriteAsync(pending, token);
            // Ignore transport cancellation deliberately: the consumer must reject late results.
            return await pending.Response.Task;
        };
        Http.CommentResponseFactory = (_, _) => Task.FromResult("{\"comments\":[],\"next_url\":null}");
        Http.RelatedResponseFactory = (_, _) => Task.FromResult("{\"illusts\":[],\"next_url\":null}");
    }

    internal Task<Pending> NextDetailAsync() => _details.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

    internal static IIdentityInfo Identity(long id) => new IdentityInfo(id.ToString(), "pixiv");
    private sealed record IdentityInfo(string Id, string Platform) : IIdentityInfo;

    internal static Illustration Sample(long id, int pages = 3) => Illustration.CreateDefault() with
    {
        Id = id,
        Title = Title + " / " + id,
        Type = pages == 1 ? IllustrationType.Illustration : IllustrationType.Manga,
        Width = 128,
        Height = 128,
        PageCount = pages,
        Visible = true,
        SanityLevel = 2,
        Series = null,
        User = UserInfo.CreateDefault() with { Id = 900001, Name = "Fixture author", ProfileImageUrls = new() { Medium = ImageUri } },
        ThumbnailUrls = new() { Medium = ImageUri, Large = ImageUri, SquareMedium = ImageUri },
        MetaSinglePage = new() { OriginalImageUrl = ImageUri },
        MetaPages = Enumerable.Range(0, pages).Select(_ => new MetaPage
        {
            ImageUrls = new() { Original = ImageUri, Medium = ImageUri, Large = ImageUri, SquareMedium = ImageUri }
        }).ToArray()
    };

    public void Dispose()
    {
        foreach (var request in _pending)
            request.Response.TrySetCanceled();
        Http.Dispose();
    }

    internal sealed record Pending(long Id, CancellationToken Token)
    {
        internal TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Complete(Illustration? illustration = null) => Response.SetResult("{\"illust\":" + (illustration ?? Sample(Id)).Serialize() + "}");
        internal void Fail() => Response.SetException(new HttpRequestException("Fixed illustration detail unavailable"));
    }
}
