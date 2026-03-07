using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Mako.Model;
using Misaki;
using Pixeval.AppManagement;
using Pixeval.Utilities.IO;
using Pixeval.Utilities.IO.Caching;

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
    private const int MaxPreviewImages = 12;

    private const int MaxRelatedWorks = 12;

    private CancellationTokenSource _loadCancellationTokenSource = new();

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
    public partial Bitmap? MainImage { get; set; }

    [ObservableProperty]
    public partial string WebsiteUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AppUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<string> Tags { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<WorkDetailsImageItemViewModel> PageImages { get; set; } = [];

    [ObservableProperty]
    public partial string PageSectionTitle { get; set; } = "作品页";

    [ObservableProperty]
    public partial IReadOnlyList<RelatedWorkCardViewModel> RelatedWorks { get; set; } = [];

    [ObservableProperty]
    public partial string RelatedSectionTitle { get; set; } = "相关作品";

    [ObservableProperty]
    public partial string NovelText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public bool IsNovel => Kind is WorkDetailsKind.Novel;

    public bool HasPageImages => PageImages.Count > 0;

    public bool HasRelatedWorks => RelatedWorks.Count > 0;

    partial void OnKindChanged(WorkDetailsKind value)
    {
        _ = value;
        OnPropertyChanged(nameof(IsNovel));
    }

    partial void OnPageImagesChanged(IReadOnlyList<WorkDetailsImageItemViewModel> value)
    {
        _ = value;
        OnPropertyChanged(nameof(HasPageImages));
    }

    partial void OnRelatedWorksChanged(IReadOnlyList<RelatedWorkCardViewModel> value)
    {
        _ = value;
        OnPropertyChanged(nameof(HasRelatedWorks));
    }

    public async Task LoadAsync(WorkDetailsNavigationParameter parameter)
    {
        var nextCancellationTokenSource = new CancellationTokenSource();
        var previousCancellationTokenSource = Interlocked.Exchange(ref _loadCancellationTokenSource, nextCancellationTokenSource);
        previousCancellationTokenSource.Cancel();
        previousCancellationTokenSource.Dispose();

        var cancellationToken = nextCancellationTokenSource.Token;

        ResetState();
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            switch (parameter.Kind)
            {
                case WorkDetailsKind.Illustration:
                case WorkDetailsKind.Manga:
                    await LoadIllustrationLikeAsync(parameter.Id, parameter.Kind, cancellationToken);
                    break;
                case WorkDetailsKind.Novel:
                    await LoadNovelAsync(parameter.Id, cancellationToken);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // ignored
        }
        catch (Exception e)
        {
            ErrorMessage = e.Message;
        }
        finally
        {
            if (ReferenceEquals(_loadCancellationTokenSource, nextCancellationTokenSource))
                IsLoading = false;
        }
    }

    private async Task LoadIllustrationLikeAsync(long id, WorkDetailsKind kind, CancellationToken cancellationToken)
    {
        var illustration = await App.AppViewModel.MakoClient.GetIllustrationFromIdAsync(id);
        cancellationToken.ThrowIfCancellationRequested();

        Kind = kind;
        WorkId = illustration.Id;
        Title = illustration.Title;
        Author = illustration.User.Name;
        Description = NormalizeDescription(illustration.Description);
        CreateDateText = illustration.CreateDate.ToLocalTime().ToString("G");
        WebsiteUrl = illustration.WebsiteUri.OriginalString;
        AppUrl = illustration.AppUri.OriginalString;

        var isManga = kind is WorkDetailsKind.Manga || illustration.PageCount > 1;
        KindText = isManga ? "漫画" : "插画";
        StatsText = $"收藏 {illustration.TotalFavorite:N0} · 浏览 {illustration.TotalView:N0} · 页数 {illustration.PageCount}";
        Tags = illustration.Tags.Select(t => string.IsNullOrWhiteSpace(t.TranslatedName)
            ? t.Name
            : $"{t.Name} ({t.TranslatedName})").ToArray();

        var mainImageUrl = illustration.PageCount > 1
            ? illustration.MetaPages.FirstOrDefault()?.OriginalUrl ?? illustration.MetaPages.FirstOrDefault()?.LargeUrl ?? illustration.ThumbnailUrls.NotCropped
            : illustration.OriginalSingleUrl ?? illustration.ThumbnailUrls.NotCropped;

        MainImage = await LoadBitmapAsync(mainImageUrl, desiredWidth: 1200, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        PageSectionTitle = illustration.PageCount > MaxPreviewImages
            ? $"作品页（前 {MaxPreviewImages} 页）"
            : "作品页";

        PageImages = illustration.PageCount > 1
            ? await CreateImageItemsAsync(
                illustration.MetaPages
                    .Take(MaxPreviewImages)
                    .Select((page, index) => ($"第 {index + 1} 页", page.LargeUrl)),
                desiredWidth: 360,
                cancellationToken)
            : [];

        RelatedSectionTitle = "相关作品";
        RelatedWorks = await LoadRelatedIllustrationsAsync(id, cancellationToken);
    }

    private async Task LoadNovelAsync(long id, CancellationToken cancellationToken)
    {
        var novel = await App.AppViewModel.MakoClient.GetNovelFromIdAsync(id);
        var content = await App.AppViewModel.MakoClient.GetNovelContentAsync(id);
        cancellationToken.ThrowIfCancellationRequested();

        Kind = WorkDetailsKind.Novel;
        WorkId = novel.Id;
        Title = novel.Title;
        Author = novel.User.Name;
        Description = NormalizeDescription(novel.Description);
        CreateDateText = novel.CreateDate.ToLocalTime().ToString("G");
        WebsiteUrl = novel.WebsiteUri.OriginalString;
        AppUrl = novel.AppUri.OriginalString;

        KindText = "小说";
        StatsText = $"收藏 {novel.TotalFavorite:N0} · 浏览 {novel.TotalView:N0} · 评论 {novel.TotalComments:N0} · 字数 {novel.TextLength:N0}";
        Tags = novel.Tags.Select(t => string.IsNullOrWhiteSpace(t.TranslatedName)
            ? t.Name
            : $"{t.Name} ({t.TranslatedName})").ToArray();

        MainImage = await LoadBitmapAsync(
            string.IsNullOrWhiteSpace(content.CoverUrl) ? novel.ThumbnailUrls.NotCropped : content.CoverUrl,
            desiredWidth: 900,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        NovelText = string.IsNullOrWhiteSpace(content.Text)
            ? "（小说正文为空）"
            : content.Text;

        var pageImages = content.Illustrations
            .Select((illustration, index) => ($"插图 {index + 1}", illustration.ThumbnailUrl))
            .Concat(content.Images.Select((image, index) => ($"文中图片 {index + 1}", image.ThumbnailUrl)))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Item2))
            .DistinctBy(pair => pair.Item2)
            .Take(MaxPreviewImages)
            .ToArray();

        PageSectionTitle = pageImages.Length > 0 ? "文中插图" : "作品页";
        PageImages = await CreateImageItemsAsync(pageImages, desiredWidth: 320, cancellationToken);

        RelatedWorks = await LoadNovelRelatedWorksAsync(id, content, cancellationToken);
        RelatedSectionTitle = RelatedWorks.Count > 0 && content.SeriesNavigation is not null
            ? "系列与推荐"
            : "推荐作品";
    }

    private async Task<IReadOnlyList<RelatedWorkCardViewModel>> LoadRelatedIllustrationsAsync(long id, CancellationToken cancellationToken)
    {
        var relatedWorks = new List<RelatedWorkCardViewModel>(MaxRelatedWorks);

        await foreach (var illustration in App.AppViewModel.MakoClient.RelatedIllustrations(id))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (illustration.Id == id)
                continue;

            relatedWorks.Add(await CreateRelatedWorkAsync(illustration, cancellationToken));
            if (relatedWorks.Count >= MaxRelatedWorks)
                break;
        }

        return relatedWorks;
    }

    private async Task<IReadOnlyList<RelatedWorkCardViewModel>> LoadNovelRelatedWorksAsync(
        long id,
        NovelContent content,
        CancellationToken cancellationToken)
    {
        var relatedWorks = new List<RelatedWorkCardViewModel>(MaxRelatedWorks);
        var knownIds = new HashSet<long> { id };

        if (content.SeriesNavigation?.PrevNovel is { Viewable: true, Id: var previousId })
        {
            var previousNovel = await App.AppViewModel.MakoClient.GetNovelFromIdAsync(previousId);
            cancellationToken.ThrowIfCancellationRequested();
            if (knownIds.Add(previousNovel.Id))
                relatedWorks.Add(await CreateRelatedWorkAsync(previousNovel, cancellationToken));
        }

        if (content.SeriesNavigation?.NextNovel is { Viewable: true, Id: var nextId })
        {
            var nextNovel = await App.AppViewModel.MakoClient.GetNovelFromIdAsync(nextId);
            cancellationToken.ThrowIfCancellationRequested();
            if (knownIds.Add(nextNovel.Id))
                relatedWorks.Add(await CreateRelatedWorkAsync(nextNovel, cancellationToken));
        }

        if (relatedWorks.Count >= MaxRelatedWorks)
            return relatedWorks;

        await foreach (var novel in App.AppViewModel.MakoClient.RecommendedNovels())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!knownIds.Add(novel.Id))
                continue;

            relatedWorks.Add(await CreateRelatedWorkAsync(novel, cancellationToken));
            if (relatedWorks.Count >= MaxRelatedWorks)
                break;
        }

        return relatedWorks;
    }

    private async Task<IReadOnlyList<WorkDetailsImageItemViewModel>> CreateImageItemsAsync(
        IEnumerable<(string Title, string Url)> items,
        int desiredWidth,
        CancellationToken cancellationToken)
    {
        var result = new List<WorkDetailsImageItemViewModel>();
        foreach (var (title, url) in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(new WorkDetailsImageItemViewModel(title, await LoadBitmapAsync(url, desiredWidth, cancellationToken)));
        }

        return result;
    }

    private async Task<RelatedWorkCardViewModel> CreateRelatedWorkAsync(Illustration illustration, CancellationToken cancellationToken)
    {
        var kind = illustration.ImageType is ImageType.ImageSet ? WorkDetailsKind.Manga : WorkDetailsKind.Illustration;
        var kindText = kind is WorkDetailsKind.Manga ? "漫画" : "插画";
        var statsText = $"收藏 {illustration.TotalFavorite:N0} · 浏览 {illustration.TotalView:N0}";

        return new RelatedWorkCardViewModel(
            illustration.Id,
            illustration.Title,
            illustration.User.Name,
            kindText,
            statsText,
            kind,
            await LoadBitmapAsync(illustration.ThumbnailUrls.Medium, desiredWidth: 360, cancellationToken));
    }

    private async Task<RelatedWorkCardViewModel> CreateRelatedWorkAsync(Novel novel, CancellationToken cancellationToken)
    {
        var statsText = $"收藏 {novel.TotalFavorite:N0} · 浏览 {novel.TotalView:N0} · 字数 {novel.TextLength:N0}";

        return new RelatedWorkCardViewModel(
            novel.Id,
            novel.Title,
            novel.User.Name,
            "小说",
            statsText,
            WorkDetailsKind.Novel,
            await LoadBitmapAsync(novel.ThumbnailUrls.Medium, desiredWidth: 360, cancellationToken));
    }

    private static async Task<Bitmap> LoadBitmapAsync(string? url, int desiredWidth, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            using var fallbackStream = AppInfo.GetImageNotAvailableStream();
            return await fallbackStream.DecodeBitmapImageAsync(true, desiredWidth);
        }

        using var stream = await CacheHelper.GetStreamFromCacheAsync(url, cancellationToken: cancellationToken);
        return await stream.DecodeBitmapImageAsync(true, desiredWidth);
    }

    private void ResetState()
    {
        var oldMainImage = MainImage;
        var oldPageImages = PageImages;
        var oldRelatedWorks = RelatedWorks;

        Title = string.Empty;
        Author = string.Empty;
        Description = string.Empty;
        CreateDateText = string.Empty;
        StatsText = string.Empty;
        KindText = string.Empty;
        MainImage = null;
        WebsiteUrl = string.Empty;
        AppUrl = string.Empty;
        Tags = [];
        PageImages = [];
        PageSectionTitle = "作品页";
        RelatedWorks = [];
        RelatedSectionTitle = "相关作品";
        NovelText = string.Empty;
        WorkId = 0;

        DisposeTransientAssetsLater(oldMainImage, oldPageImages, oldRelatedWorks);
    }

    public void ReleaseResources()
    {
        var oldMainImage = MainImage;
        var oldPageImages = PageImages;
        var oldRelatedWorks = RelatedWorks;

        MainImage = null;
        PageImages = [];
        RelatedWorks = [];

        DisposeTransientAssetsLater(oldMainImage, oldPageImages, oldRelatedWorks);
    }

    private static void DisposeTransientAssetsLater(
        Bitmap? mainImage,
        IReadOnlyList<WorkDetailsImageItemViewModel> pageImages,
        IReadOnlyList<RelatedWorkCardViewModel> relatedWorks)
    {
        if (mainImage is null && pageImages.Count is 0 && relatedWorks.Count is 0)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            mainImage?.Dispose();

            foreach (var pageImage in pageImages)
                pageImage.Dispose();

            foreach (var relatedWork in relatedWorks)
                relatedWork.Dispose();
        }, DispatcherPriority.Background);
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
