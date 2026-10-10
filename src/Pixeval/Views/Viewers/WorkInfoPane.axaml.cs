// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data.Converters;
using CommunityToolkit.Mvvm.Input;
using Pixeval.AppManagement;
using Pixeval.AppManagement.Settings;
using Pixeval.Models;
using Pixeval.Models.Blocking;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Booru;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Services;
using Pixeval.Utilities;
using Pixeval.Views.Capability;
using Pixeval.Views.Search;

namespace Pixeval.Views.Viewers;

public record WorkInfoTag(string Name, string? TranslatedName);

public record WorkInfoTagGroup(string Key, IReadOnlyList<WorkInfoTag> Tags);

public record WorkInfoUser(string Id, string Name, string? AvatarUrl, object RawUser);

public class WorkInfoPane : TemplatedControl
{
    public static readonly StyledProperty<object?> ArtworkInfoProperty =
        AvaloniaProperty.Register<WorkInfoPane, object?>(nameof(ArtworkInfo));

    public static readonly StyledProperty<object?> ActionZoneProperty =
        AvaloniaProperty.Register<WorkInfoPane, object?>(nameof(ActionZone));

    public static readonly StyledProperty<IDataTemplate> ActionZoneTemplateProperty =
        AvaloniaProperty.Register<WorkInfoPane, IDataTemplate>(nameof(ActionZoneTemplate));

    public static readonly DirectProperty<WorkInfoPane, string> WorkTitleProperty =
        AvaloniaProperty.RegisterDirect<WorkInfoPane, string>(nameof(WorkTitle), o => o.WorkTitle);

    public static readonly DirectProperty<WorkInfoPane, string> WorkDescriptionProperty =
        AvaloniaProperty.RegisterDirect<WorkInfoPane, string>(nameof(WorkDescription), o => o.WorkDescription);

    public static readonly DirectProperty<WorkInfoPane, string> WorkIdProperty =
        AvaloniaProperty.RegisterDirect<WorkInfoPane, string>(nameof(WorkId), o => o.WorkId);

    public static readonly DirectProperty<WorkInfoPane, long> TotalViewProperty =
        AvaloniaProperty.RegisterDirect<WorkInfoPane, long>(nameof(TotalView), o => o.TotalView);

    public static readonly DirectProperty<WorkInfoPane, long> TotalFavoriteProperty =
        AvaloniaProperty.RegisterDirect<WorkInfoPane, long>(nameof(TotalFavorite), o => o.TotalFavorite);

    public static readonly DirectProperty<WorkInfoPane, DateTimeOffset> CreateDateProperty =
        AvaloniaProperty.RegisterDirect<WorkInfoPane, DateTimeOffset>(nameof(CreateDate), o => o.CreateDate);

    public static readonly DirectProperty<WorkInfoPane, IReadOnlyList<WorkInfoUser>> AuthorsProperty =
        AvaloniaProperty.RegisterDirect<WorkInfoPane, IReadOnlyList<WorkInfoUser>>(nameof(Authors), o => o.Authors);

    public static readonly DirectProperty<WorkInfoPane, IReadOnlyList<WorkInfoUser>> UploadersProperty =
        AvaloniaProperty.RegisterDirect<WorkInfoPane, IReadOnlyList<WorkInfoUser>>(nameof(Uploaders), o => o.Uploaders);

    public static readonly DirectProperty<WorkInfoPane, bool> IsAiGeneratedProperty =
        AvaloniaProperty.RegisterDirect<WorkInfoPane, bool>(nameof(IsAiGenerated), o => o.IsAiGenerated);

    public static readonly DirectProperty<WorkInfoPane, SafeRating> SafeRatingProperty =
        AvaloniaProperty.RegisterDirect<WorkInfoPane, SafeRating>(nameof(SafeRating), o => o.SafeRating);

    public static readonly DirectProperty<WorkInfoPane, IReadOnlyList<WorkInfoTagGroup>> TagGroupsProperty =
        AvaloniaProperty.RegisterDirect<WorkInfoPane, IReadOnlyList<WorkInfoTagGroup>>(nameof(TagGroups), o => o.TagGroups);

    public string WorkTitle { get => field; private set => SetAndRaise(WorkTitleProperty, ref field, value); } = "";
    public string WorkDescription { get => field; private set => SetAndRaise(WorkDescriptionProperty, ref field, value); } = "";
    public string WorkId { get => field; private set => SetAndRaise(WorkIdProperty, ref field, value); } = "";
    public long TotalView { get => field; private set => SetAndRaise(TotalViewProperty, ref field, value); }
    public long TotalFavorite { get => field; private set => SetAndRaise(TotalFavoriteProperty, ref field, value); }
    public DateTimeOffset CreateDate { get => field; private set => SetAndRaise(CreateDateProperty, ref field, value); }
    public IReadOnlyList<WorkInfoUser> Authors { get => field; private set => SetAndRaise(AuthorsProperty, ref field, value); } = [];
    public IReadOnlyList<WorkInfoUser> Uploaders { get => field; private set => SetAndRaise(UploadersProperty, ref field, value); } = [];
    public bool IsAiGenerated { get => field; private set => SetAndRaise(IsAiGeneratedProperty, ref field, value); }
    public SafeRating SafeRating { get => field; private set => SetAndRaise(SafeRatingProperty, ref field, value); } = SafeRating.General;
    public IReadOnlyList<WorkInfoTagGroup> TagGroups { get => field; private set => SetAndRaise(TagGroupsProperty, ref field, value); } = [];

    public IDataTemplate ActionZoneTemplate
    {
        get => GetValue(ActionZoneTemplateProperty);
        set => SetValue(ActionZoneTemplateProperty, value);
    }

    public object? ActionZone
    {
        get => GetValue(ActionZoneProperty);
        set => SetValue(ActionZoneProperty, value);
    }

    public object? ArtworkInfo
    {
        get => GetValue(ArtworkInfoProperty);
        set => SetValue(ArtworkInfoProperty, value);
    }

    public IAsyncRelayCommand<object?> OpenAuthorCommand { get; }
    public IRelayCommand<WorkInfoTag?> OpenTagCommand { get; }
    public IRelayCommand<WorkInfoTag?> BlockTagCommand { get; }
    public IRelayCommand<object?> BlockUserCommand { get; }
    public IRelayCommand ViewLikedUsersCommand { get; }

    public WorkInfoPane()
    {
        OpenAuthorCommand = new AsyncRelayCommand<object?>(OpenAuthorAsync);
        OpenTagCommand = new RelayCommand<WorkInfoTag?>(OpenTag);
        BlockTagCommand = new RelayCommand<WorkInfoTag?>(BlockTag);
        BlockUserCommand = new RelayCommand<object?>(BlockUser);
        ViewLikedUsersCommand = new RelayCommand(ViewLikedUsers);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ArtworkInfoProperty)
        {
            UpdateArtworkInfo(change.GetNewValue<object?>());
        }
    }

    private void UpdateArtworkInfo(object? entry)
    {
        switch (entry)
        {
            case Illustration illust:
                WorkTitle = illust.Title;
                WorkDescription = illust.Description;
                WorkId = illust.Id.ToString();
                TotalView = illust.TotalView;
                TotalFavorite = illust.TotalFavorite;
                CreateDate = illust.CreateDateOffset;
                Authors = [new WorkInfoUser(illust.User.Id.ToString(), illust.User.Name, illust.User.AvatarUrl, illust.User)];
                Uploaders = [];
                IsAiGenerated = illust.IsAiGenerated;
                SafeRating = illust.SafeRating;
                TagGroups = [new WorkInfoTagGroup("", illust.Tags.Select(t => new WorkInfoTag(t.Name, t.TranslatedName)).ToList())];
                break;
            case Novel novel:
                WorkTitle = novel.Title;
                WorkDescription = novel.Description;
                WorkId = novel.Id.ToString();
                TotalView = novel.TotalView;
                TotalFavorite = novel.TotalFavorite;
                CreateDate = novel.CreateDateOffset;
                Authors = [new WorkInfoUser(novel.User.Id.ToString(), novel.User.Name, novel.User.AvatarUrl, novel.User)];
                Uploaders = [];
                IsAiGenerated = novel.IsAiGenerated;
                SafeRating = novel.SafeRating;
                TagGroups = [new WorkInfoTagGroup("", novel.Tags.Select(t => new WorkInfoTag(t.Name, t.TranslatedName)).ToList())];
                break;
            case BooruPost booru:
                WorkTitle = booru.Title;
                WorkDescription = booru.Description;
                WorkId = booru.Id;
                TotalView = booru.TotalViewCount >= 0 ? booru.TotalViewCount : 0;
                TotalFavorite = booru.TotalFavorite;
                CreateDate = booru.CreateDateOffset;
                Authors = [];
                Uploaders = !string.IsNullOrWhiteSpace(booru.UploaderName)
                    ? [new WorkInfoUser(booru.UploaderName, booru.UploaderName, null, new BooruUser(booru.UploaderName, booru.Platform))]
                    : [];
                IsAiGenerated = booru.IsAiGenerated;
                SafeRating = booru.SafeRating;
                TagGroups = booru.Tags
                    .GroupBy(t => t.TagType)
                    .Select(g => new WorkInfoTagGroup(g.Key, g.Select(t => new WorkInfoTag(t.Name, null)).ToList()))
                    .ToList();
                break;
            case WorkEntry we:
                UpdateArtworkInfo(we.AsWorkEntry);
                break;
            default:
                WorkTitle = "";
                WorkDescription = "";
                WorkId = "";
                TotalView = 0;
                TotalFavorite = 0;
                CreateDate = default;
                Authors = [];
                Uploaders = [];
                IsAiGenerated = false;
                SafeRating = SafeRating.General;
                TagGroups = [];
                break;
        }
    }

    private void ViewLikedUsers()
    {
        var nav = App.Services?.GetService<INavigationService>() ?? new NavigationService();

        if (ArtworkInfo is Illustration illust && illust.Id > 0)
        {
            nav.NavigateTo<BookmarkUsersPage>((illust.Id, false, illust.Title), sourceControl: this);
        }
        else if (ArtworkInfo is Novel novel && novel.Id > 0)
        {
            nav.NavigateTo<BookmarkUsersPage>((novel.Id, true, novel.Title), sourceControl: this);
        }
        else if (ArtworkInfo is WorkEntry we)
        {
            if (we.AsWorkEntry is Illustration i && i.Id > 0)
                nav.NavigateTo<BookmarkUsersPage>((i.Id, false, i.Title), sourceControl: this);
            else if (we.AsWorkEntry is Novel n && n.Id > 0)
                nav.NavigateTo<BookmarkUsersPage>((n.Id, true, n.Title), sourceControl: this);
        }
    }

    private async Task OpenAuthorAsync(object? user)
    {
        if (TopLevel.GetTopLevel(this) is not { Launcher: { } launcher, ViewContainer: { } viewContainer }
            || user is null)
            return;

        if (user is User u && u.Id > 0)
        {
            viewContainer.CreateUserPage(u.Id);
        }
        else if (user is BooruUser bu)
        {
            await launcher.LaunchUriAsync(bu.WebsiteUri);
        }
        else if (user is TokenUser tu && tu.RawId > 0)
        {
            viewContainer.CreateUserPage(tu.RawId);
        }
    }

    private void OpenTag(WorkInfoTag? tag)
    {
        if (tag is null)
            return;

        var type = ArtworkInfo is Novel ? SimpleWorkType.Novel : SimpleWorkType.Illustration;
        App.Services!.GetRequiredService<SearchHistorySession>().Add(tag.Name, tag.TranslatedName);
        var nav = App.Services?.GetService<INavigationService>() ?? new NavigationService();
        nav.NavigateToWorkSearch(tag.Name, type, this);
    }

    private void BlockTag(WorkInfoTag? tag)
    {
        if (tag is null)
            return;

        var blockedTags = App.Services!.GetRequiredService<AppSettings>().BrowsingExperienceSettings.BlockedTags;
        if (!blockedTags.Contains(tag.Name))
        {
            blockedTags.Add(tag.Name);
            AppInfo.SaveAppSettings(App.Services!.GetRequiredService<AppSettings>());
        }
    }

    private static void BlockUser(object? user)
    {
        if (user is User u)
            _ = BlockedContentHelper.TryAddOrUpdateBlockedUser(u);
    }

    public static IValueConverter HalfVerticalSpaceConverter { get; } = new FuncValueConverter<Rect, double>(x => x.Height / 2);
}
