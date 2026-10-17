// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.
namespace Pixeval.ViewModels.Viewers;

public sealed record WorkSeriesInfoViewModel(
    SimpleWorkKind WorkKind,
    long Id,
    string Title,
    WorkSeriesNavigationViewModel? Previous = null,
    WorkSeriesNavigationViewModel? Next = null,
    string? PositionText = null)
{
    public static WorkSeriesInfoViewModel? Create(object? work, SimpleWorkKind workType)
    {
        var series = work switch
        {
            Pixeval.Mako.Illustration illust => illust.Series,
            Pixeval.Mako.Novel novel => novel.Series,
            _ => null
        };
        return series is { Id: > 0 } s ? new(workType, s.Id, s.Title) : null;
    }

    public static WorkSeriesInfoViewModel Create(MangaSeriesContextResult response)
    {
        return new(
            SimpleWorkKind.Illustration,
            response.Series?.Id ?? 0,
            response.Series?.Title ?? "",
            CreateNavigation(response.Context.PrevIllust),
            CreateNavigation(response.Context.NextIllust),
            $"{response.Context.ContentOrder}/{response.Series?.PublishedContentCount ?? 0}");

        static WorkSeriesNavigationViewModel? CreateNavigation(Illustration? work) =>
            work is { Id: > 0, Visible: true } w
                ? new(w.Id, w.Title, w)
                : null;
    }

    public static WorkSeriesInfoViewModel? Create(NovelContent content, Pixeval.Mako.Series? fallback)
    {
        var id = content.SeriesId ?? fallback?.Id;
        if (id is not > 0)
            return null;

        var title = string.IsNullOrWhiteSpace(content.SeriesTitle)
            ? fallback?.Title ?? ""
            : content.SeriesTitle;
        return new(
            SimpleWorkKind.Novel,
            id.Value,
            title,
            CreateNavigation(content.SeriesNavigation?.PrevNovel),
            CreateNavigation(content.SeriesNavigation?.NextNovel));

        static WorkSeriesNavigationViewModel? CreateNavigation(NovelNavigation? work) =>
            work is { Id: > 0, Viewable: true } w
                ? new(w.Id, w.Title)
                : null;
    }

}

public sealed record WorkSeriesNavigationViewModel(
    long Id,
    string Title,
    Illustration? Illustration = null);
