using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Pixeval.Mako;

namespace Pixeval.Tests;

internal static class NativeFixtureData
{
    internal static JsonSerializerOptions JsonOptions { get; } = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            for (var i = info.Properties.Count - 1; i >= 0; i--)
                if (info.Properties[i].AttributeProvider?.IsDefined(typeof(JsonIgnoreAttribute), true) == true)
                    info.Properties.RemoveAt(i);
            foreach (var property in info.Properties)
            {
                // Anonymous response envelopes use the service's explicit wire names,
                // including the camelCase webview novel payload.
                if (info.Type.Name.Contains("AnonymousType") && property.AttributeProvider is System.Reflection.PropertyInfo member)
                    property.Name = member.Name;
                else if (property.Name == "illust_type") property.Name = "type";
                else if (info.Type == typeof(Series) && property.Name == "cover_url") property.Name = "url";
                else if (info.Type == typeof(MangaSeriesContextResult) && property.Name == "context") property.Name = "illust_series_context";
                else if (info.Type == typeof(MangaSeriesContextInfo) && property.Name == "prev_illust") property.Name = "prev";
                else if (info.Type == typeof(MangaSeriesContextInfo) && property.Name == "next_illust") property.Name = "next";
            }
        });
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            TypeInfoResolver = resolver,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
        };
    }
    internal static string Serialize<T>(this T value) => JsonSerializer.Serialize(value, JsonOptions);
}

internal static class NativeFixtureExtensions
{
    // Fixtures use immutable records from the production ABI, never item view models.
}

internal static class IllustrationFixtureExtensions
{
    extension(Illustration)
    {
        public static Illustration CreateDefault() => new()
        {
            Title = "", Caption = "", CreateDate = "2026-11-07T00:00:00+00:00",
            User = User.CreateDefault(), Tags = [], Tools = [], MetaPages = [],
            PageCount = 1, Width = 128, Height = 128, Visible = true,
            IllustType = IllustContentType.Illustration
        };
    }
}

internal static class MakoNovelFixtureExtensions
{
    extension(Mako.Novel)
    {
        public static Mako.Novel CreateDefault() => new()
        {
            Title = "", Caption = "", CreateDate = "2026-11-07T00:00:00+00:00",
            User = User.CreateDefault(), Tags = []
        };
    }
}

internal static class UserFixtureExtensions
{
    extension(User)
    {
        public static User CreateDefault() => new() { Name = "", Account = "", SampleWorkThumbnails = [] };
    }
}

internal static class TagFixtureExtensions
{
    extension(Tag)
    {
        public static Tag CreateDefault() => new() { Name = "" };
    }
}

internal static class SeriesFixtureExtensions
{
    extension(Series)
    {
        public static Series CreateDefault() => new() { Title = "" };
    }
}

internal static class SingleUserResponseFixtureExtensions
{
    extension(SingleUserResponse)
    {
        public static SingleUserResponse CreateDefault() => new() { User = User.CreateDefault() };
    }
}

internal static class UserProfileFixtureExtensions
{
    extension(UserProfile)
    {
        public static UserProfile CreateDefault() => new();
    }
}

internal static class SpotlightArticleFixtureExtensions
{
    extension(SpotlightArticle)
    {
        public static SpotlightArticle CreateDefault() => new() { Title = "" };
    }

}
