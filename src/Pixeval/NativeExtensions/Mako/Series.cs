// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Misaki;
using Pixeval.Models.Pixiv;

namespace Pixeval.Native.Mako;

public partial record Series : IIdEntry, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    long IIdEntry.Id => Id;

    string IIdentityInfo.Id => Id.ToString();

    string IPlatformInfo.Platform => IPlatformInfo.Pixiv;

    [JsonIgnore]
    public Series Entry => this;

    [JsonIgnore]
    public SimpleWorkType WorkType { get; set; } = SimpleWorkType.Illustration;

    [JsonIgnore]
    public string? ThumbnailUrl => CoverUrl ?? "";

    [JsonIgnore]
    public Uri AppUri => new($"pixeval://series/{(WorkType is SimpleWorkType.Novel ? "novel" : "illust")}/{Id}");

    [JsonIgnore]
    public Uri WebsiteUri => new(WorkType is SimpleWorkType.Novel
        ? $"https://www.pixiv.net/novel/series/{Id}"
        : $"https://www.pixiv.net/user/{User?.Id}/series/{Id}");

    [JsonIgnore]
    public DateTimeOffset LastPublishedDate =>
        DateTimeOffset.TryParse(LastPublishedContentDatetime, out var dt) ? dt : default;

    [JsonIgnore]
    public int ContentCount => PublishedContentCount ?? 0;

    [JsonIgnore]
    public string Caption => MaskText ?? "";

    [JsonIgnore]
    public bool WatchlistAdded { get; set; }
}
