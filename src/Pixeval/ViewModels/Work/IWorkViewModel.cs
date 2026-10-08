// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Misaki;
using Pixeval.Controls;
using Pixeval.Filters;
using Pixeval.Native.Filters;
using Pixeval.Native.Mako;

namespace Pixeval.ViewModels;

public interface IWorkViewModel : INotifyPropertyChanged
{
    bool IsBookmarkSupported { get; }

    IArtworkInfo Entry { get; }

    string Id => Entry.Id;

    HeartButtonState IsBookmarkedDisplay { get; set; }

    bool IsInWatchLater { get; set; }

    bool HasSeries { get; }

    double AspectRatio { get; }

    string? ThumbnailUrl { get; }

    string Tooltip { get; }

    bool IsPicOne => Entry is Illustration illust ? illust.IsPicOne : Entry is not IImageSet;

    bool IsPicSet => Entry is Illustration illust ? illust.IsPicSet : Entry is IImageSet;

    bool IsPicGif => Entry is Illustration illust && illust.IsPicGif;

    int PageCount => Entry is Illustration illust ? illust.PageCount : (Entry is IImageSet set ? set.Pages.Count : 1);

    Uri AppUri { get; }

    Uri WebsiteUri { get; }

    IAsyncRelayCommand<(IReadOnlyList<string>? Tags, bool IsPrivate, Control? Control)> AddToBookmarkCommand => WorkCommands.AddToBookmarkCommand;

    IAsyncRelayCommand<Control?> BookmarkCommand => WorkCommands.BookmarkCommand;

    IRelayCommand<Control?> AddToWatchLaterCommand => WorkCommands.AddToWatchLaterCommand;

    IAsyncRelayCommand<Control?> SaveCommand => WorkCommands.SaveCommand;

    IAsyncRelayCommand<Image?> CopyCommand => WorkCommands.CopyCommand;

    bool Filter(FilterQuery query) => query.MatchesArtwork(Entry.ToArtworkMetadata());
}
