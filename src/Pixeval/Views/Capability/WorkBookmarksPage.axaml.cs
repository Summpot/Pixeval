// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Controls;
using Pixeval.Models.Options;
using Pixeval.Models.Subscriptions;
using Pixeval.Native.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Work;

namespace Pixeval.Views.Capability;

public partial class WorkBookmarksPage : IconContentPage
{
    private readonly User _user;
    private readonly string? _initialTag;
    private bool _suppressChangeSource;

    public static IReadOnlyList<BookmarkTag> DefaultTags { get; } = [AllBookmarkTag.Instance, UncategorizedBookmarkTag.Instance];

    private static IWorkSubscriptionService SubscriptionService =>
        App.AppViewModel.AppServiceProvider.GetRequiredService<IWorkSubscriptionService>();

    public WorkBookmarksPage() : this(PixevalSettings.MyUser ?? new User(0, "", "", new ProfileImageUrls(null, null, null, null), false, null, []))
    {
    }

    public WorkBookmarksPage(User user, SimpleWorkType simpleWorkType = SimpleWorkType.Illustration, PrivacyPolicy privacyPolicy = PrivacyPolicy.Public, string? tag = null, IWorkViewViewModel? viewModel = null)
    {
        InitializeComponent();

        _user = user;
        _initialTag = tag;
        SimpleWorkTypeComboBox.SelectedValue = simpleWorkType;
        PrivacyPolicyComboBox.SelectedValue = privacyPolicy;
        if (_user.Id <= 0 || _user.Id != PixevalSettings.MyId)
            PrivacyPolicyComboBox.IsEnabled = PrivacyPolicyComboBox.IsVisible = false;

        if (_user.Id > 0)
            FetchTags();
        else
        {
            _suppressChangeSource = true;
            TagComboBox.ItemsSource = DefaultTags;
            TagComboBox.SelectedItem = AllBookmarkTag.Instance;
            _suppressChangeSource = false;
        }

        if (viewModel is not null)
        {
            WorkContainer.SetViewModel(viewModel);
            UpdateSubscriptionButtons();
        }
        else
            ChangeSource();
    }

    private void WorkTypeComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        FetchTags();
        ChangeSource();
    }

    private void TagComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _suppressChangeSource)
            return;

        ChangeSource();
    }

    private void WorkContainer_OnRefreshRequested(object? sender, RoutedEventArgs e)
    {
        ChangeSource();
    }

    public async void FetchTags()
    {
        if (_user.Id <= 0)
        {
            _suppressChangeSource = true;
            TagComboBox.ItemsSource = DefaultTags;
            TagComboBox.SelectedItem = AllBookmarkTag.Instance;
            _suppressChangeSource = false;
            return;
        }

        try
        {
            var tags = await MakoHelper.GetBookmarkTagsAsync(
                _user.Id,
                SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>(),
                PrivacyPolicyComboBox.GetSelectedValue<PrivacyPolicy>());

            _suppressChangeSource = true;
            TagComboBox.ItemsSource = tags;
            TagComboBox.SelectedItem = tags.FirstOrDefault(tag => tag.Name == _initialTag) ?? AllBookmarkTag.Instance;
            _suppressChangeSource = false;
        }
        catch (Exception ex)
        {
            App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>()
                .LogError(nameof(FetchTags), ex);
            _suppressChangeSource = true;
            TagComboBox.ItemsSource = new List<BookmarkTag> { AllBookmarkTag.Instance, UncategorizedBookmarkTag.Instance };
            TagComboBox.SelectedItem = AllBookmarkTag.Instance;
            _suppressChangeSource = false;
        }
    }

    private void ChangeSource()
    {
        var tag = (TagComboBox.SelectedItem as BookmarkTag)?.Name;
        var workType = SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>();
        var privacy = PrivacyPolicyComboBox.GetSelectedValue<PrivacyPolicy>() is PrivacyPolicy.Private ? "private" : "public";
        var engine = (workType is SimpleWorkType.Novel
            ? (IAsyncEnumerable<IWorkEntry>) App.AppViewModel.MakoClient.NovelBookmarks(_user.Id, privacy, tag)
            : App.AppViewModel.MakoClient.WorkBookmarks(_user.Id, privacy, tag)).ToFetchEngine();
        WorkContainer.ResetEngine(engine);
        App.AppViewModel.QueueWorkSubscriptionSyncCurrentSource(
            _user.Id,
            WorkSubscriptionType.Bookmarks,
            GetSubscriptionWorkKind(),
            engine);
        UpdateSubscriptionButtons();
    }

    private async void AddSubscriptionButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        var workKind = GetSubscriptionWorkKind();

        _ = await WorkSubscriptionButtonHelper.RunAsync(
            AddSubscriptionButton,
            RemoveSubscriptionButton,
            () => Task.FromResult(WorkSubscriptionHelper.TryAddOrUpdateUser(_user, WorkSubscriptionType.Bookmarks, workKind)),
            UpdateSubscriptionButtons);
    }

    private async void RemoveSubscriptionButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        _ = await WorkSubscriptionButtonHelper.RunAsync(
            AddSubscriptionButton,
            RemoveSubscriptionButton,
            RemoveCurrentSubscriptionAsync,
            UpdateSubscriptionButtons);
    }

    private void UpdateSubscriptionButtons()
    {
        if (_user.Id <= 0)
        {
            AddSubscriptionButton.IsVisible = false;
            RemoveSubscriptionButton.IsVisible = false;
            return;
        }

        WorkSubscriptionButtonHelper.UpdateVisibility(
            AddSubscriptionButton,
            RemoveSubscriptionButton,
            SubscriptionService.TryGetSubscription(_user.Id, WorkSubscriptionType.Bookmarks, GetSubscriptionWorkKind()) is not null);
    }

    private async Task<bool> RemoveCurrentSubscriptionAsync()
    {
        if (SubscriptionService.TryGetSubscription(_user.Id, WorkSubscriptionType.Bookmarks, GetSubscriptionWorkKind()) is not { HistoryEntryId: var historyEntryId })
            return false;

        _ = await SubscriptionService.TryRemoveAsync(historyEntryId);
        return true;
    }

    private WorkSubscriptionWorkKind GetSubscriptionWorkKind() =>
        SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>() is SimpleWorkType.Novel
            ? WorkSubscriptionWorkKind.Novel
            : WorkSubscriptionWorkKind.Illustration;
}
