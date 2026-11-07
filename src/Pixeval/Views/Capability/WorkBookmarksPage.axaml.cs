// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Mako.Global.Enum;
using Mako.Model;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Models.Options;
using Pixeval.Models.Subscriptions;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Work;

namespace Pixeval.Views.Capability;

public partial class WorkBookmarksPage : IconContentPage
{
    private readonly UserBasicInfo _user;
    private CancellationTokenSource? _tagLoadCancellation;
    private bool _suppressChangeSource;

    public static IReadOnlyList<BookmarkTag> DefaultTags { get; } = [AllBookmarkTag.Instance, UncategorizedBookmarkTag.Instance];

    private static IWorkSubscriptionService SubscriptionService =>
        App.AppViewModel.AppServiceProvider.GetRequiredService<IWorkSubscriptionService>();

    public WorkBookmarksPage() : this(PixevalSettings.Me)
    {
    }

    public WorkBookmarksPage(UserBasicInfo user, SimpleWorkType simpleWorkType = SimpleWorkType.Illustration, PrivacyPolicy privacyPolicy = PrivacyPolicy.Public, string? tag = null, IWorkViewViewModel? viewModel = null)
    {
        InitializeComponent();

        _user = user;
        SimpleWorkTypeComboBox.SelectedValue = simpleWorkType;
        PrivacyPolicyComboBox.SelectedValue = privacyPolicy;
        if (_user.Id != PixevalSettings.MyId)
            PrivacyPolicyComboBox.IsEnabled = PrivacyPolicyComboBox.IsVisible = false;

        if (tag is not null)
        {
            var selected = DefaultTags.FirstOrDefault(item => item.Name == tag) ?? new BookmarkTag { Name = tag, Count = 0 };
            TagComboBox.ItemsSource = DefaultTags.Contains(selected) ? DefaultTags : [.. DefaultTags, selected];
            TagComboBox.SelectedItem = selected;
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
        FetchTags();
        ChangeSource();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        FetchTags();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        CancelTagLoad();
        base.OnUnloaded(e);
    }

    private void CancelTagLoad()
    {
        var cancellation = _tagLoadCancellation;
        _tagLoadCancellation = null;
        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    public async void FetchTags()
    {
        if (!IsLoaded)
            return;
        CancelTagLoad();
        var cancellation = _tagLoadCancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        try
        {
            var tags = await MakoHelper.GetBookmarkTagsAsync(
                _user.Id,
                SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>(),
                PrivacyPolicyComboBox.GetSelectedValue<PrivacyPolicy>(), token);
            if (!IsLoaded || token.IsCancellationRequested || !ReferenceEquals(_tagLoadCancellation, cancellation))
                return;

            // Preserve the filter, but never carry a previous source's count into
            // a completed tag list that does not contain it.
            var selected = TagComboBox.SelectedItem as BookmarkTag ?? AllBookmarkTag.Instance;
            if (!tags.Any(tag => tag.Name == selected.Name))
                tags.Add(new BookmarkTag { Name = selected.Name, Count = 0 });
            _suppressChangeSource = true;
            try
            {
                TagComboBox.ItemsSource = tags;
                TagComboBox.SelectedItem = tags.First(tag => tag.Name == selected.Name);
            }
            finally { _suppressChangeSource = false; }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (IsLoaded && !token.IsCancellationRequested && ReferenceEquals(_tagLoadCancellation, cancellation))
            {
                App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>().LogError(nameof(FetchTags), exception);
                TopLevel.GetTopLevel(this)?.ViewContainer?.ShowError(I18NManager.GetResource(EntryViewerPageResources.LoadFailed), exception.Message);
            }
        }
        finally
        {
            if (ReferenceEquals(_tagLoadCancellation, cancellation))
                _tagLoadCancellation = null;
            cancellation.Dispose();
        }
    }

    private void ChangeSource()
    {
        var tag = (TagComboBox.SelectedItem as BookmarkTag)?.Name;
        var workType = SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>();
        var engine = App.AppViewModel.MakoClient.WorkBookmarks(
            workType,
            _user.Id,
            PrivacyPolicyComboBox.GetSelectedValue<PrivacyPolicy>(),
            tag);
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

    private void UpdateSubscriptionButtons() =>
        WorkSubscriptionButtonHelper.UpdateVisibility(
            AddSubscriptionButton,
            RemoveSubscriptionButton,
            SubscriptionService.TryGetSubscription(_user.Id, WorkSubscriptionType.Bookmarks, GetSubscriptionWorkKind()) is not null);

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
