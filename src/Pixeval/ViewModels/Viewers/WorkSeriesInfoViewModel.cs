// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.
namespace Pixeval.ViewModels.Viewers;

public sealed record WorkSeriesInfoViewModel(
    SimpleWorkType WorkType,
    long Id,
    string Title,
    WorkSeriesNavigationViewModel? Previous = null,
    WorkSeriesNavigationViewModel? Next = null,
    string? PositionText = null)
{
    public static WorkSeriesInfoViewModel? Create(object? work, SimpleWorkType workType)
    {
        var series = work switch
        {
            Pixeval.Native.Mako.Illustration illust => illust.Series,
            Pixeval.Native.Mako.Novel novel => novel.Series,
            _ => null
        };
        return series is { Id: > 0 } s ? new(workType, s.Id, s.Title) : null;
    }

    public static WorkSeriesInfoViewModel Create(MangaSeriesContextResponse response)
    {
        return new(
            SimpleWorkType.Illustration,
            response.Detail.Id,
            response.Detail.Title,
            CreateNavigation(response.Context.Previous),
            CreateNavigation(response.Context.Next),
            $"{response.Context.ContentOrder}/{response.Detail.PublishedContentCount ?? 0}");

        static WorkSeriesNavigationViewModel? CreateNavigation(Illustration? work) =>
            work is { Id: > 0, Visible: true }
                ? new(work.Id, work.Title, work)
                : null;
    }

    public static WorkSeriesInfoViewModel? Create(NovelContent content, Pixeval.Native.Mako.Series? fallback)
    {
        var id = content.SeriesId ?? fallback?.Id;
        if (id is not > 0)
            return null;

        var title = string.IsNullOrWhiteSpace(content.SeriesTitle)
            ? fallback?.Title ?? ""
            : content.SeriesTitle;
        return new(
            SimpleWorkType.Novel,
            id.Value,
            title,
            CreateNavigation(content.SeriesNavigation?.PrevNovel),
            CreateNavigation(content.SeriesNavigation?.NextNovel));

        static WorkSeriesNavigationViewModel? CreateNavigation(NovelNavigation? work) =>
            work is { Id: > 0, Viewable: true }
                ? new(work.Id, work.Title)
                : null;
    }
}

public sealed record WorkSeriesNavigationViewModel(
    long Id,
    string Title,
    Illustration? Illustration = null);
