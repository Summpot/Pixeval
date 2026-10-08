// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using AutoSettingsPage;
using AutoSettingsPage.Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Pixeval.AppManagement.Settings;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Models.Blocking;
using Pixeval.Models.Pixiv;
using Pixeval.Models.Settings;
using Pixeval.Native.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Capability;
using Pixeval.Views.Settings;
using Pixeval.Views.Work;

namespace Pixeval.Views.Viewers;

public partial class NovelViewerPage : IconContentPage
{
    private NovelViewerPageViewModel ViewModel => (NovelViewerPageViewModel) DataContext!;

    public NovelViewerPage() : this(null)
    {
    }

    public NovelViewerPage(NovelViewerPageViewModel? viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        if (viewModel is not null)
        {
            viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
            UpdatePanePages(viewModel.CurrentNovel);
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        }
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NovelViewerPageViewModel.CurrentNovel))
        {
            UpdatePanePages(ViewModel?.CurrentNovel);
        }
    }

    private void UpdatePanePages(Novel? currentNovel)
    {
        if (currentNovel is null)
        {
            NovelTabbedPage.Pages = [];
            return;
        }

        if (BlockedContentHelper.IsBlockedPlaceholder(currentNovel.Entry))
        {
            NovelTabbedPage.Pages = [new WorkInfoPage(currentNovel.Entry)];
            return;
        }

        NovelTabbedPage.Pages =
        [
            new WorkInfoPage(currentNovel.Entry),
            new CommentsPage(new CommentsViewViewModel(SimpleWorkType.Novel, currentNovel.Entry.Id)),
            new WorkRelatedPage(currentNovel.Entry.Id, SimpleWorkType.Novel) { IsCommandBarCollapsed = true },
            CreateSettingsPage()
        ];
    }

    private SettingsSubView CreateSettingsPage()
    {
        LocalSettingsEntryHelper.Initialize();
        return new SettingsSubView(
            SettingsBuilder.CreateGroupList(App.AppViewModel.AppSettings)
                .NewGroup(t => t.NovelSettings, group => group
                    .Color(t => t.NovelBackground, t => t.PropertyChanged += (_, _) => ViewModel?.NotifyNovelBackgroundChanged())
                    .Color(t => t.NovelFontColor, t => t.PropertyChanged += (_, _) => ViewModel?.NotifyNovelFontColorChanged())
                    .Font(t => t.NovelFontFamily, t => t.PropertyChanged += (_, _) => ViewModel?.NotifyNovelFontFamilyChanged())
                    .Enum(t => t.NovelFontWeight, t => t.PropertyChanged += (_, _) => ViewModel?.NotifyNovelFontWeightChanged())
                    .Int(t => t.NovelFontSize, 5, 100, 1, t => t.PropertyChanged += (_, _) => ViewModel?.NotifyNovelFontSizeChanged())
                    .Int(t => t.NovelLineHeight, 0, 150, 1, t => t.PropertyChanged += (_, _) => ViewModel?.NotifyNovelLineHeightChanged())
                    .Int(t => t.NovelMaxWidth, 50, 10000, 50, t => t.PropertyChanged += (_, _) => ViewModel?.NotifyNovelMaxWidthChanged()))
                .Build()[0]);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        _ = KeyboardShortcut.TryExecute(e, Key.Left, ViewModel.PrevCommand)
            || KeyboardShortcut.TryExecute(e, Key.Right, ViewModel.NextCommand)
            || KeyboardShortcut.TryExecute(e, Key.Up, ViewModel.PrevWorkCommand)
            || KeyboardShortcut.TryExecute(e, Key.Down, ViewModel.NextWorkCommand);
    }

    private void PrevButton_OnRightClick(object? sender, ContextRequestedEventArgs e) => ViewModel.PrevWorkCommand.Execute(null);

    private void NextButton_OnRightClick(object? sender, ContextRequestedEventArgs e) => ViewModel.NextWorkCommand.Execute(null);

    private void MarkdownBox_OnHyperlinkClicked(object? sender, MarkdownHyperlinkClickedEventArgs e)
    {
        if (!TryParsePageLink(e.Url, out var pageIndex))
            return;

        e.Handled = true;

        if (pageIndex >= 0 && pageIndex < ViewModel.PageCount)
            ViewModel.CurrentPageIndex = pageIndex;
    }

    private async void AddToBookmarkButton_OnClick(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is Control c && ViewModel.CurrentNovel is { Entry.Id: var id })
            await BookmarkTagSelectorFlyoutHelper.ShowAsync(
                c,
                SimpleWorkType.Novel,
                id,
                AddToBookmarkAsync,
                PlacementMode.Bottom);
    }

    private async Task AddToBookmarkAsync((bool IsPrivate, IReadOnlyList<string>? Tags) e)
    {
        if (ViewModel.CurrentNovel is { } current)
        {
            await current.AddToBookmarkCommand.ExecuteAsync((e.Tags, e.IsPrivate, current));
            TopLevel.GetTopLevel(this)?.ViewContainer?.ShowSuccess(
                I18NManager.GetResource(MiscResources.AddedToBookmark));
        }
    }

    private static bool TryParsePageLink(string url, out int pageIndex)
    {
        pageIndex = 0;
        var link = url.StartsWith('#') ? url[1..] : url;

        if (!link.StartsWith("page", StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(link[4..], out pageIndex);
    }

    #region Disposal

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        RaiseEvent(new ViewModelDisposalEventArgs(ViewModelDisposal.ViewModelDisposalEvent, ViewModel));
    }

    #endregion
}
