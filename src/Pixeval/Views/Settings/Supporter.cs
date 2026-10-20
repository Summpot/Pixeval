// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Utilities;
using Pixeval.Services.Update;
using Pixeval.Common.IO;
using Pixeval.ViewModels;

namespace Pixeval.Views.Settings;

public partial class Supporter : ViewModelBase
{
    public Supporter() // JsonSerializer use
    {
    }

    public Supporter(string nickname, string name)
    {
        Nickname = nickname;
        Name = name;
    }

    static Supporter()
    {
        Random.Shared.Shuffle(Supporters);
    }

    public static string BasePath { get; } = Path.Combine(AppInfo.CacheFolder, "GitHubSupporters");

    public string AtName => "@" + Name;

    public string Nickname { get; init; } = null!;

    public string Name { get; init; } = null!;

    public string? LocalProfilePicture
    {
        get
        {
            var path = Path.Combine(BasePath, Name + ".png");
            return File.Exists(path) ? path : null;
        }
    }

    [ObservableProperty]
    public partial Uri? ProfilePicture { get; set; }

    [ObservableProperty]
    public partial Uri? ProfileUri { get; set; }

    // ReSharper disable StringLiteralTypo
    public static Supporter[] Supporters { get; } =
    [
        new("探姬", "PerolNotsfsssf"),
        new("Summpot", "Summpot"),
        new("扑克", "Poker-sang"),
        new("Shirasagi", "Shirasagi0012"),
        new("cnbluefire", "cnbluefire"),
        new("岛风", "frg2089"),
        new("Ёж, просто ёж", "bropines"),
        new("irony", "kokoro-aya"),
        new("Betta_Fish", "zxbmmmmmmmmm"),
        new("Dylech30th", "dylech30th")
    ];
    // ReSharper restore StringLiteralTypo

    public static async Task GetSupportersAsync(GitHubService github)
    {
        try
        {
            _ = Directory.CreateDirectory(BasePath);

            var path = Path.Combine(BasePath, "github-supporters.json");
            var exists = File.Exists(path);
            if (exists)
            {
                await using var fileStream = File.OpenAsyncRead(path);

                if (await JsonSerializer.DeserializeAsync(fileStream, GitHubUserSerializeContext.Default.SupporterArray) is { } supporters
                    && supporters.Length == Supporters.Length)
                {
                    foreach (var supporter in Supporters)
                        await LoadAvatarAsync(supporter, BasePath, github);

                    return;
                }
            }

            foreach (var supporter in Supporters)
                if (await github.GetUserProfileAsync(supporter.Name) is { } user)
                {
                    supporter.ProfilePicture = new Uri(user.AvatarUrl);
                    supporter.ProfileUri = new Uri(user.HtmlUrl);
                    await LoadAvatarAsync(supporter, BasePath, github);
                }

            if (!exists)
            {
                await using var fs = File.CreateAsyncWrite(path);
                await JsonSerializer.SerializeAsync(fs, Supporters, GitHubUserSerializeContext.Default.SupporterArray);
            }
        }
        catch
        {
            // ignored
        }

        return;

        static async ValueTask LoadAvatarAsync(Supporter supporter, string basePath, GitHubService client)
        {
            var path = Path.Combine(basePath, supporter.Name + ".png");
            if (File.Exists(path) || supporter.ProfilePicture is null)
                return;

            try
            {
                var bytes = await client.DownloadBytesAsync(supporter.ProfilePicture.AbsoluteUri);
                await File.WriteAllBytesAsync(path, bytes);
                supporter.OnPropertyChanged(nameof(LocalProfilePicture));
            }
            catch
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}

[JsonSerializable(typeof(Supporter[]))]
public partial class GitHubUserSerializeContext : JsonSerializerContext;
