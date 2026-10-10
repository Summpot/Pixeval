// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pixeval.Controls;
using Pixeval.Models;
using Pixeval.Native.Storage;
using Pixeval.ViewModels;

namespace Pixeval.Native.Booru;

public partial record BooruPost : IArtworkSerializable
{
    [JsonIgnore]
    public bool IsBookmarkSupported => false;

    [JsonIgnore]
    public bool HasSeries => false;

    [JsonIgnore]
    public double AspectRatio => Width > 0 && Height > 0 ? (double) Width / Height : 1;

    [JsonIgnore]
    public string? ThumbnailUrl => PreviewUrl ?? SampleUrl ?? OriginalUrl;

    [JsonIgnore]
    public string? LargeFileUrl => SampleUrl ?? OriginalUrl;

    [JsonIgnore]
    public string? PreviewFileUrl => PreviewUrl ?? SampleUrl;

    [JsonIgnore]
    public string Tooltip => Title;

    [JsonIgnore]
    public int SetIndex => -1;

    [JsonIgnore]
    public string PlatformName => Platform.ToPlatformString();

    [JsonIgnore]
    public string Title => Source ?? "";

    [JsonIgnore]
    public string Description => "";

    [JsonIgnore]
    public DateTimeOffset CreateDateOffset => DateTimeOffset.TryParse(CreatedAt, out var dt) ? dt : default;

    [JsonIgnore]
    public int TotalViewCount => -1;

    [JsonIgnore]
    public bool IsFavorite => false;

    [JsonIgnore]
    public bool IsAiGenerated => false;

    [JsonIgnore]
    public SafeRating SafeRating => Rating.ToLowerInvariant() switch
    {
        "general" or "safe" => SafeRating.General,
        "sensitive" => SafeRating.Questionable,
        "questionable" => SafeRating.Questionable,
        "explicit" => SafeRating.Explicit,
        _ => SafeRating.NotSpecified
    };

    [JsonIgnore]
    public Uri WebsiteUri => new(Platform switch
    {
        BooruPlatform.Danbooru => $"https://danbooru.donmai.us/posts/{Id}",
        BooruPlatform.Gelbooru => $"https://gelbooru.com/index.php?page=post&s=view&id={Id}",
        BooruPlatform.Sankaku => $"https://chan.sankakucomplex.com/posts/show/{Id}",
        BooruPlatform.Yandere => $"https://yande.re/post/show/{Id}",
        BooruPlatform.Rule34 => $"https://rule34.xxx/index.php?page=post&s=view&id={Id}",
        _ => "about:blank"
    });

    [JsonIgnore]
    public Uri AppUri => new($"pixeval://{Platform.ToPlatformString()}/{Id}");

    public const string LegacyPostToken = "Imouto.BooruParser.Post";
    public const string NativePostToken = "Pixeval.Native.Booru.BooruPost";

    [JsonIgnore]
    public string SerializeKey => LegacyPostToken;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Serialize() => JsonSerializer.Serialize(this, s_jsonOptions);

    public static BooruPost Deserialize(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
            throw new ArgumentException("Payload is empty", nameof(data));

        try
        {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;

            // Check if this is legacy Imouto.BooruParser.Post format
            if (root.TryGetProperty("FileResolution", out var res) ||
                (root.TryGetProperty("Id", out var idProp) && idProp.ValueKind == JsonValueKind.Object))
            {
                return DeserializeLegacyPost(root);
            }

            var post = JsonSerializer.Deserialize<BooruPost>(data, s_jsonOptions);
            if (post != null)
                return post;
        }
        catch
        {
            // fallback
        }

        using var fallbackDoc = JsonDocument.Parse(data);
        return DeserializeLegacyPost(fallbackDoc.RootElement);
    }

    private static BooruPost DeserializeLegacyPost(JsonElement root)
    {
        string id = "";
        var platform = BooruPlatform.Danbooru;
        string? md5 = null;

        if (root.TryGetProperty("Id", out var idElem))
        {
            if (idElem.ValueKind == JsonValueKind.Object)
            {
                id = idElem.TryGetProperty("Id", out var i) ? i.GetString() ?? "" : "";
                if (idElem.TryGetProperty("PlatformType", out var pt))
                {
                    platform = pt.GetInt32() switch
                    {
                        0 => BooruPlatform.Danbooru,
                        1 => BooruPlatform.Gelbooru,
                        2 => BooruPlatform.Sankaku,
                        3 => BooruPlatform.Yandere,
                        4 => BooruPlatform.Rule34,
                        _ => BooruPlatform.Danbooru
                    };
                }
                if (idElem.TryGetProperty("Md5Hash", out var m))
                    md5 = m.GetString();
            }
            else
            {
                id = idElem.GetString() ?? "";
            }
        }

        var origUrl = root.TryGetProperty("OriginalUrl", out var ou) ? ou.GetString() ?? "" : "";
        var sampleUrl = root.TryGetProperty("SampleUrl", out var su) ? su.GetString() : null;
        var previewUrl = root.TryGetProperty("PreviewUrl", out var pu) ? pu.GetString() : null;

        uint width = 0;
        uint height = 0;
        if (root.TryGetProperty("FileResolution", out var res))
        {
            if (res.TryGetProperty("Width", out var w)) width = w.GetUInt32();
            if (res.TryGetProperty("Height", out var h)) height = h.GetUInt32();
        }

        ulong byteSize = root.TryGetProperty("ByteSize", out var bs) ? bs.GetUInt64() : 0;

        var ratingStr = "general";
        if (root.TryGetProperty("SafeRating", out var sr))
        {
            ratingStr = sr.GetInt32() switch
            {
                0 => "general",
                1 => "general",
                2 => "questionable",
                3 => "explicit",
                _ => "general"
            };
        }

        var tags = new List<BooruTag>();
        if (root.TryGetProperty("Tags", out var tagsElem) && tagsElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in tagsElem.EnumerateArray())
            {
                var name = t.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                var typeStr = t.TryGetProperty("Type", out var typ) ? typ.GetString() ?? "" : "general";
                tags.Add(new BooruTag(typeStr, name));
            }
        }

        string? uploader = null;
        if (root.TryGetProperty("Uploader", out var up))
        {
            if (up.ValueKind == JsonValueKind.Object && up.TryGetProperty("Name", out var un))
                uploader = un.GetString();
            else if (up.ValueKind == JsonValueKind.String)
                uploader = up.GetString();
        }

        string? source = root.TryGetProperty("Source", out var s) ? s.GetString() : null;
        string? createdAt = root.TryGetProperty("CreateDate", out var cd) ? cd.GetString() : null;

        var ext = origUrl.Contains('.') ? origUrl[(origUrl.LastIndexOf('.') + 1)..] : "jpg";

        return new BooruPost(
            id,
            md5 ?? "",
            platform,
            origUrl,
            sampleUrl,
            previewUrl,
            width,
            height,
            byteSize,
            ext,
            createdAt ?? "",
            "",
            uploader ?? "",
            source,
            ratingStr,
            tags,
            null,
            false,
            0,
            false,
            false,
            null);
    }
}
