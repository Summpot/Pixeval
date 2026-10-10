// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Controls;
using Pixeval.Models.Options;
using Pixeval.Models.Pixiv;
using Pixeval.Models.Subscriptions;
using Pixeval.Native.Mako;
using Pixeval.Services;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Work;

namespace Pixeval.Views.Capability;

public partial class WorkBookmarksPage : IconContentPage
{
    public static readonly StyledProperty<User?> UserProperty =
        AvaloniaProperty.Register<WorkBookmarksPage, User?>(nameof(User));

    private static User EmptyUser() =>
        new(0, "", "", new ProfileImageUrls(null, null, null, null), false, null, []);

    private User _user = EmptyUser();
    private readonly string? _initialTag;
    private IWorkViewViewModel? _viewModel;
    private bool _suppressChangeSource;
    private bool _initialized;

    public static IReadOnlyList<BookmarkTag> DefaultTags { get; } = [AllBookmarkTag.Instance, UncategorizedBookmarkTag.Instance];

    private static IWorkSubscriptionService SubscriptionService =>
        App.Services?.GetService<IWorkSubscriptionService>() ?? App.AppViewModel.AppServiceProvider.GetRequiredService<IWorkSubscriptionService>();

    public WorkBookmarksPage()
    {
        InitializeComponent();
        Configure(SimpleWorkType.Illustration, PrivacyPolicy.Public, null);
    }

    public WorkBookmarksPage(User user, SimpleWorkType simpleWorkType = SimpleWorkType.Illustration, PrivacyPolicy privacyPolicy = PrivacyPolicy.Public, string? tag = null, IWorkViewViewModel? viewModel = null)
    {
        InitializeComponent();
        _initialTag = tag;
        Configure(simpleWorkType, privacyPolicy, viewModel);
        User = user;
    }

    public User? User
    {
        get => GetValue(UserProperty);
        set => SetValue(UserProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UserProperty)
            ApplyUser(change.GetNewValue<User?>());
    }

    private void Configure(SimpleWorkType simpleWorkType, PrivacyPolicy privacyPolicy, IWorkViewViewModel? viewModel)
    {
        _suppressChangeSource = true;
        SimpleWorkTypeComboBox.SelectedValue = simpleWorkType;
        PrivacyPolicyComboBox.SelectedValue = privacyPolicy;
        _suppressChangeSource = false;
        _viewModel = viewModel;
    }

    private void ApplyUser(User? user)
    {
        var next = user ?? EmptyUser();
        if (_initialized && next.Id == _user.Id)
            return;

        _user = next;
        UpdatePrivacyVisibility();
        _initialized = true;
        if (_viewModel is not null)
        {
            WorkContainer.SetViewModel(_viewModel);
            UpdateSubscriptionButtons();
            _viewModel = null;
            return;
        }

        if (_user.Id > 0)
            FetchTags();
        else
        {
            _suppressChangeSource = true;
            TagComboBox.ItemsSource = DefaultTags;
            TagComboBox.SelectedItem = AllBookmarkTag.Instance;
            _suppressChangeSource = false;
        }

        ChangeSource();
    }

    private void UpdatePrivacyVisibility()
    {
        var myId = App.Services?.GetService<IUserSessionService>()?.CurrentUserId ?? PixevalSettings.MyId;
        var enabled = _user.Id > 0 && _user.Id == myId;
        PrivacyPolicyComboBox.IsEnabled = PrivacyPolicyComboBox.IsVisible = enabled;
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
            var tags = await App.AppViewModel.MakoClient.GetBookmarkTagsAsync(
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
        if (_user.Id <= 0)
        {
            WorkContainer.ResetEngine(AsyncEnumerable.Empty<IWorkEntry>().ToFetchEngine());
            UpdateSubscriptionButtons();
            return;
        }

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
