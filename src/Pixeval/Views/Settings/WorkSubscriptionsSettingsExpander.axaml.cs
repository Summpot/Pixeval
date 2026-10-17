// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoSettingsPage.Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Avalonia.Controls;
using Pixeval.Controls;
using Pixeval.Pixiv;
using Pixeval.Localization;
using Pixeval.Config;
using Pixeval.Services.Background;
using Pixeval.Storage;
using Pixeval.Utilities;

namespace Pixeval.Views.Settings;

public partial class WorkSubscriptionsSettingsExpander : SettingsExpander, IEntryControl<WorkSubscriptionsSettingsEntry>
{
    private CancellationTokenSource? _reloadCancellationTokenSource;

    private bool _isLoaded;

    public ObservableCollection<WorkSubscriptionRecord> Subscriptions { get; } = [];

    public WorkSubscriptionsSettingsEntry Entry
    {
        set
        {
            DataContext = value;
            _ = ReloadAsync();
        }
    }

    public WorkSubscriptionsSettingsExpander()
    {
        InitializeComponent();
        UpdateWorkKindItems();
    }

    private IWorkSubscriptionService SubscriptionService =>
        this.GetRequiredUiService<IWorkSubscriptionService>();

    private static IReadOnlyList<SymbolComboBoxItem> BookmarkWorkKinds { get; } =
        [.. SymbolComboBoxItem.GetValues<WorkSubscriptionWorkKind>().Where(t => t.Value is WorkSubscriptionWorkKind.Illustration or WorkSubscriptionWorkKind.Novel)];

    private static IReadOnlyList<SymbolComboBoxItem> PostWorkKinds { get; } =
        [.. SymbolComboBoxItem.GetValues<WorkSubscriptionWorkKind>().Where(t => t.Value is WorkSubscriptionWorkKind.Illustration or WorkSubscriptionWorkKind.Manga or WorkSubscriptionWorkKind.Novel)];

    private static IReadOnlyList<SymbolComboBoxItem> SeriesWorkKinds => BookmarkWorkKinds;

    private async Task ReloadAsync()
    {
        _reloadCancellationTokenSource?.Cancel();
        _reloadCancellationTokenSource?.Dispose();
        _reloadCancellationTokenSource = new();
        var token = _reloadCancellationTokenSource.Token;
        Subscriptions.Clear();
        try
        {
            foreach (var entry in this.GetRequiredUiService<StorageEngine>().GetAllSubscriptions())
            {
                token.ThrowIfCancellationRequested();
                Subscriptions.Add(entry);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            this.GetRequiredUiService<FileLogger>()
                .LogError(nameof(ReloadAsync), e);
        }
    }

    private void AddButton_OnClicked(object? sender, RoutedEventArgs e) =>
        this.RunInteraction(() => AddButton_OnClickedAsync(sender, e));

    private async Task AddButton_OnClickedAsync(object? sender, RoutedEventArgs e)
    {
        ErrorTextBlock.IsVisible = false;
        if (!long.TryParse(TargetIdTextBox.Text, out var targetId) || targetId <= 0)
        {
            ShowError(Strings.WorkSubscriptionsSettingsExpanderInvalidTargetId());
            return;
        }

        var subscriptionType = SubscriptionTypeComboBox.GetSelectedValue<WorkSubscriptionType>();
        if (WorkKindComboBox.SelectedValue is not WorkSubscriptionWorkKind workKind)
        {
            UpdateWorkKindItems();
            if (WorkKindComboBox.SelectedValue is not WorkSubscriptionWorkKind selectedWorkKind)
                return;

            workKind = selectedWorkKind;
        }

        if (subscriptionType is WorkSubscriptionType.Series)
        {
            var simpleWorkType = workKind is WorkSubscriptionWorkKind.Novel
                ? SimpleWorkKind.Novel
                : SimpleWorkKind.Illustration;
            var storage = this.GetRequiredUiService<StorageEngine>();
            var query = await this.GetRequiredUiService<MakoClient>().GetWorkSeriesAsync(simpleWorkType, targetId);
            var detail = query.Detail();
            var first = query.First()?.AsWorkEntry;
            var engine = query.Engine();
            _ = WorkSubscriptionHelper.TryAddOrUpdateSeries(targetId, workKind, storage, SubscriptionService, detail, first);
        }
        else
        {
            var storage = this.GetRequiredUiService<StorageEngine>();
            var user = (await this.GetRequiredUiService<MakoClient>().GetUserDetailAsync(targetId)).User;
            _ = WorkSubscriptionHelper.TryAddOrUpdateUser(user, subscriptionType, workKind, storage, SubscriptionService);
        }

        TargetIdTextBox.Text = "";
        await ReloadAsync();
    }

    private void DeleteButton_OnClicked(object? sender, RoutedEventArgs e) =>
        this.RunInteraction(() => DeleteButton_OnClickedAsync(sender, e));

    private async Task DeleteButton_OnClickedAsync(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: WorkSubscriptionRecord item })
            return;

        _ = await SubscriptionService.TryRemoveAsync(item.HistoryEntryId);
        await ReloadAsync();
    }

    private void SyncAllButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        SubscriptionService.QueueSyncAll();
    }

    private void SyncSubscriptionButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: WorkSubscriptionRecord item })
            SubscriptionService.QueueSyncSubscription(item);
    }

    private void SubscriptionServiceOnSubscriptionUpdated(object? sender, WorkSubscriptionRecord subscription)
    {
        if (!_isLoaded)
            return;

        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplySubscriptionUpdate(subscription);
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (_isLoaded)
                ApplySubscriptionUpdate(subscription);
        });
    }

    private void ApplySubscriptionUpdate(WorkSubscriptionRecord subscription)
    {
        for (var i = 0; i < Subscriptions.Count; i++)
        {
            if (Subscriptions[i].HistoryEntryId != subscription.HistoryEntryId)
                continue;

            Subscriptions[i] = subscription;
            return;
        }
    }

    private void SubscriptionTypeComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        UpdateWorkKindItems();
    }

    private void UpdateWorkKindItems()
    {
        var items = SubscriptionTypeComboBox.GetSelectedValue<WorkSubscriptionType>() switch
        {
            WorkSubscriptionType.Bookmarks => BookmarkWorkKinds,
            WorkSubscriptionType.Posts => PostWorkKinds,
            WorkSubscriptionType.Series => SeriesWorkKinds,
            _ => throw new ArgumentOutOfRangeException()
        };
        WorkKindComboBox.ItemsSource = items;
        if (items is not [])
            WorkKindComboBox.SelectedValue = items[0].Value;
    }

    private void ShowError(string text)
    {
        ErrorTextBlock.Text = text;
        ErrorTextBlock.IsVisible = true;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _isLoaded = true;
        SubscriptionService.SubscriptionUpdated -= SubscriptionServiceOnSubscriptionUpdated;
        SubscriptionService.SubscriptionUpdated += SubscriptionServiceOnSubscriptionUpdated;
        _ = ReloadAsync();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        _isLoaded = false;
        SubscriptionService.SubscriptionUpdated -= SubscriptionServiceOnSubscriptionUpdated;
        _reloadCancellationTokenSource?.Cancel();
        _reloadCancellationTokenSource?.Dispose();
        _reloadCancellationTokenSource = null;
        base.OnUnloaded(e);
    }
}
