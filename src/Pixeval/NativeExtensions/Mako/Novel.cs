// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Text.Json.Serialization;
using Pixeval.Models;
using Pixeval.Models.Blocking;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Storage;

namespace Pixeval.Native.Mako;

public partial record Novel : IWorkEntry, IArtworkSerializable
{
    [JsonIgnore]
    public bool IsBookmarkSupported => !BlockedContentHelper.IsBlockedPlaceholder(this) && Platform is PlatformConstants.Pixiv;

    [JsonIgnore]
    public bool HasSeries => Series is not null;

    [JsonIgnore]
    public double AspectRatio => 1;

    [JsonIgnore]
    public string? ThumbnailUrl => ImageUrls?.SquareMedium ?? ImageUrls?.Medium ?? ImageUrls?.Large ?? "";

    [JsonIgnore]
    public string Tooltip => Title;

    [JsonIgnore]
    public string Platform => PlatformConstants.Pixiv;

    [JsonIgnore]
    public int Width => 0;

    [JsonIgnore]
    public int Height => 0;

    [JsonIgnore]
    public DateTimeOffset CreateDateOffset => DateTimeOffset.TryParse(CreateDate, out var dt) ? dt : default;

    [JsonIgnore]
    public Uri WebsiteUri => new($"https://www.pixiv.net/novel/show.php?id={Id}");

    [JsonIgnore]
    public Uri AppUri => new($"pixeval://novel/{Id}");

    [JsonIgnore]
    public SafeRating SafeRating => XRestrict switch
    {
        1 => SafeRating.Explicit,
        2 => SafeRating.Guro,
        _ => SafeRating.NotSpecified
    };

    [JsonIgnore]
    public XRestrict XRestrictValue => XRestrict switch
    {
        1 => Pixeval.Models.Pixiv.XRestrict.R18,
        2 => Pixeval.Models.Pixiv.XRestrict.R18G,
        _ => Pixeval.Models.Pixiv.XRestrict.Ordinary
    };

    [JsonIgnore]
    public bool IsAiGenerated => NovelAiType == 2;

    public const string LegacyNovelToken = "Mako.Model.Novel";

    public string Serialize() => System.Text.Json.JsonSerializer.Serialize(this);

    [JsonIgnore]
    public string SerializeKey => LegacyNovelToken;

    private static readonly System.Text.Json.JsonSerializerOptions s_snakeCaseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly System.Text.Json.JsonSerializerOptions s_caseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static Novel Deserialize(string data)
    {
        try
        {
            var res = System.Text.Json.JsonSerializer.Deserialize<Novel>(data, s_snakeCaseOptions);
            if (res != null && (res.Id != 0 || res.ImageUrls != null))
                return res;
        }
        catch
        {
            // fallback
        }

        try
        {
            var res = System.Text.Json.JsonSerializer.Deserialize<Novel>(data, s_caseInsensitiveOptions);
            if (res != null)
                return res;
        }
        catch
        {
            // fallback
        }

        return System.Text.Json.JsonSerializer.Deserialize<Novel>(data)!;
    }
}
