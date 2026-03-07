using System;
using Avalonia.Media.Imaging;

namespace Pixeval.ViewModels.WorkDetails;

public sealed class WorkDetailsImageItemViewModel(string title, Bitmap image) : IDisposable
{
    public string Title { get; } = title;

    public Bitmap Image { get; } = image;

    public void Dispose()
    {
        Image.Dispose();
    }
}

public sealed class RelatedWorkCardViewModel(
    long id,
    string title,
    string author,
    string kindText,
    string statsText,
    WorkDetailsKind kind,
    Bitmap thumbnail) : IDisposable
{
    public long Id { get; } = id;

    public string Title { get; } = title;

    public string Author { get; } = author;

    public string KindText { get; } = kindText;

    public string StatsText { get; } = statsText;

    public WorkDetailsKind Kind { get; } = kind;

    public Bitmap Thumbnail { get; } = thumbnail;

    public WorkDetailsNavigationParameter NavigationParameter => new(Id, Kind);

    public void Dispose()
    {
        Thumbnail.Dispose();
    }
}
