using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Mako.Model;

namespace Pixeval.ViewModels.WorkDetails;

public enum WorkDetailsKind
{
    Illustration,
    Manga,
    Novel
}

public readonly record struct WorkDetailsNavigationParameter(long Id, WorkDetailsKind Kind);

public partial class WorkDetailsViewModel : ObservableObject
{
    [ObservableProperty]
    public partial WorkDetailsKind Kind { get; set; }

    [ObservableProperty]
    public partial long WorkId { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Author { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CreateDateText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string KindText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? CoverUrl { get; set; }

    [ObservableProperty]
    public partial string WebsiteUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AppUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<string> Tags { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<string> GalleryUrls { get; set; } = [];

    [ObservableProperty]
    public partial string NovelText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public bool IsNovel => Kind is WorkDetailsKind.Novel;

    public bool HasGallery => GalleryUrls.Count > 0;

    partial void OnKindChanged(WorkDetailsKind value)
    {
        OnPropertyChanged(nameof(IsNovel));
    }

    partial void OnGalleryUrlsChanged(IReadOnlyList<string> value)
    {
        OnPropertyChanged(nameof(HasGallery));
    }

    public async Task LoadAsync(WorkDetailsNavigationParameter parameter)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            switch (parameter.Kind)
            {
                case WorkDetailsKind.Illustration:
                case WorkDetailsKind.Manga:
                    await LoadIllustrationLikeAsync(parameter.Id, parameter.Kind).ConfigureAwait(false);
                    break;
                case WorkDetailsKind.Novel:
                    await LoadNovelAsync(parameter.Id).ConfigureAwait(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception e)
        {
            ErrorMessage = e.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadIllustrationLikeAsync(long id, WorkDetailsKind kind)
    {
        var illustration = await App.AppViewModel.MakoClient.GetIllustrationFromIdAsync(id).ConfigureAwait(false);

        Kind = kind;
        WorkId = illustration.Id;
        Title = illustration.Title;
        Author = illustration.User.Name;
        Description = NormalizeDescription(illustration.Description);
        CreateDateText = illustration.CreateDate.ToLocalTime().ToString("G");
        WebsiteUrl = illustration.WebsiteUri.OriginalString;
        AppUrl = illustration.AppUri.OriginalString;
        CoverUrl = illustration.ThumbnailUrls.Large;

        var isManga = kind is WorkDetailsKind.Manga || illustration.PageCount > 1;
        KindText = isManga ? "漫画" : "插画";
        StatsText = $"收藏 {illustration.TotalFavorite:N0} · 浏览 {illustration.TotalView:N0} · 页数 {illustration.PageCount}";
        Tags = illustration.Tags.Select(t => string.IsNullOrWhiteSpace(t.TranslatedName)
            ? t.Name
            : $"{t.Name} ({t.TranslatedName})").ToArray();

        GalleryUrls = illustration.PageCount > 1
            ? illustration.MetaPages.Select(p => p.ImageUrls.Medium).ToArray()
            : [illustration.ThumbnailUrls.Large];
    }

    private async Task LoadNovelAsync(long id)
    {
        var novel = await App.AppViewModel.MakoClient.GetNovelFromIdAsync(id).ConfigureAwait(false);
        var content = await App.AppViewModel.MakoClient.GetNovelContentAsync(id).ConfigureAwait(false);

        Kind = WorkDetailsKind.Novel;
        WorkId = novel.Id;
        Title = novel.Title;
        Author = novel.User.Name;
        Description = NormalizeDescription(novel.Description);
        CreateDateText = novel.CreateDate.ToLocalTime().ToString("G");
        WebsiteUrl = novel.WebsiteUri.OriginalString;
        AppUrl = novel.AppUri.OriginalString;
        CoverUrl = novel.ThumbnailUrls.Large;

        KindText = "小说";
        StatsText = $"收藏 {novel.TotalFavorite:N0} · 浏览 {novel.TotalView:N0} · 评论 {novel.TotalComments:N0} · 字数 {novel.TextLength:N0}";
        Tags = novel.Tags.Select(t => string.IsNullOrWhiteSpace(t.TranslatedName)
            ? t.Name
            : $"{t.Name} ({t.TranslatedName})").ToArray();

        NovelText = string.IsNullOrWhiteSpace(content.Text)
            ? "（小说正文为空）"
            : content.Text;

        GalleryUrls = content.Illustrations
            .Select(i => i.ThumbnailUrl)
            .Concat(content.Images.Select(i => i.ThumbnailUrl))
            .Distinct()
            .ToArray();
    }

    private static string NormalizeDescription(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return "（无简介）";

        var normalized = source
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase);

        normalized = Regex.Replace(normalized, "<.*?>", string.Empty);

        return string.IsNullOrWhiteSpace(normalized)
            ? "（无简介）"
            : normalized.Trim();
    }
}
