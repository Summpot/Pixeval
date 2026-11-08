// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoSettingsPage.Avalonia;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Avalonia.Controls;
using Mako.Global.Enum;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Models.Database;
using Pixeval.Models.Database.Managers;
using Pixeval.Models.Options;
using Pixeval.Models.Settings.Entries;
using Pixeval.Models.Subscriptions;
using Pixeval.Utilities;
using Pixeval.ViewModels.Settings;

namespace Pixeval.Views.Settings;

public partial class WorkSubscriptionsSettingsExpander : SettingsExpander, IEntryControl<WorkSubscriptionsSettingsEntry>
{
    public static readonly DirectProperty<WorkSubscriptionsSettingsExpander, bool> IsAddingProperty =
        AvaloniaProperty.RegisterDirect<WorkSubscriptionsSettingsExpander, bool>(nameof(IsAdding), control => control.IsAdding);

    public bool IsAdding
    {
        get;
        private set => SetAndRaise(IsAddingProperty, ref field, value);
    }

    private CancellationTokenSource? _addCancellationTokenSource;

    private CancellationTokenSource? _reloadCancellationTokenSource;

    private bool _isLoaded;

    public ObservableCollection<WorkSubscriptionItemViewModel> Subscriptions { get; } = [];

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

    private static WorkSubscriptionPersistentManager SubscriptionManager =>
        App.AppViewModel.AppServiceProvider.GetRequiredService<WorkSubscriptionPersistentManager>();

    private static IWorkSubscriptionService SubscriptionService =>
        App.AppViewModel.AppServiceProvider.GetRequiredService<IWorkSubscriptionService>();

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
            await foreach (var entry in SubscriptionManager.StreamEntriesAsync(token: token))
            {
                token.ThrowIfCancellationRequested();
                Subscriptions.Add(new(entry));
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>()
                .LogError(nameof(ReloadAsync), e);
        }
    }

    private async void AddButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (!_isLoaded || IsAdding)
            return;

        ErrorTextBlock.IsVisible = false;
        if (!long.TryParse(TargetIdTextBox.Text, out var targetId) || targetId <= 0)
        {
            ShowError(I18NManager.GetResource(WorkSubscriptionsSettingsExpanderResources.InvalidTargetId));
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

        var client = App.AppViewModel.MakoClient;
        var accountId = client.Me?.Id;
        var input = TargetIdTextBox.Text;
        var source = new CancellationTokenSource();
        _addCancellationTokenSource = source;
        var token = source.Token;
        IsAdding = true;
        try
        {
            bool added;
            if (subscriptionType is WorkSubscriptionType.Series)
            {
                var simpleWorkType = workKind is WorkSubscriptionWorkKind.Novel
                    ? SimpleWorkType.Novel
                    : SimpleWorkType.Illustration;
                var (detail, first, engine) = await client.GetWorkSeriesAsync(simpleWorkType, targetId, token);
                if (!IsCurrent())
                    return;
                added = detail.Id == targetId && WorkSubscriptionHelper.TryAddOrUpdateSeries(targetId, workKind, detail, first, engine);
            }
            else
            {
                var user = (await client.GetUserFromIdAsync(targetId, token)).UserEntity;
                if (!IsCurrent())
                    return;
                added = user.Id == targetId && WorkSubscriptionHelper.TryAddOrUpdateUser(user, subscriptionType, workKind);
            }

            if (!added)
            {
                ShowError(I18NManager.GetResource(WorkSubscriptionsSettingsExpanderResources.AddFailed));
                return;
            }
            if (TargetIdTextBox.Text == input
                && SubscriptionTypeComboBox.GetSelectedValue<WorkSubscriptionType>() == subscriptionType
                && WorkKindComboBox.SelectedValue is WorkSubscriptionWorkKind currentKind && currentKind == workKind)
                TargetIdTextBox.Text = "";
            await ReloadAsync();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            if (IsCurrent())
                ShowError(I18NManager.GetResource(WorkSubscriptionsSettingsExpanderResources.AddFailed));
        }
        finally
        {
            if (ReferenceEquals(_addCancellationTokenSource, source))
            {
                _addCancellationTokenSource = null;
                IsAdding = false;
            }
            source.Dispose();
        }

        bool IsCurrent() => _isLoaded && !token.IsCancellationRequested
            && ReferenceEquals(_addCancellationTokenSource, source)
            && accountId is > 0 && client.Me?.Id == accountId;
    }

    private void CancelAdd()
    {
        var source = _addCancellationTokenSource;
        _addCancellationTokenSource = null;
        IsAdding = false;
        source?.Cancel();
        source?.Dispose();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        CancelAdd();
        base.OnDataContextChanged(e);
    }

    private void EditorGrid_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < 680;
        EditorGrid.ColumnDefinitions = new ColumnDefinitions(compact ? "*,Auto" : "180,*,*,Auto");
        Grid.SetColumnSpan(TargetIdTextBox, compact ? 2 : 1);
        Grid.SetColumn(SubscriptionTypeComboBox, compact ? 0 : 1);
        Grid.SetRow(SubscriptionTypeComboBox, compact ? 1 : 0);
        Grid.SetColumnSpan(SubscriptionTypeComboBox, compact ? 2 : 1);
        Grid.SetColumn(WorkKindComboBox, compact ? 0 : 2);
        Grid.SetRow(WorkKindComboBox, compact ? 2 : 0);
        Grid.SetColumn(AddSubscriptionButton, compact ? 1 : 3);
        Grid.SetRow(AddSubscriptionButton, compact ? 2 : 0);
        Grid.SetColumnSpan(ErrorTextBlock, compact ? 2 : 4);
        Grid.SetColumnSpan(AddProgress, compact ? 2 : 4);
    }

    private async void DeleteButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: WorkSubscriptionItemViewModel item })
            return;

        _ = await SubscriptionService.TryRemoveAsync(item.Entry.HistoryEntryId);
        if (_isLoaded)
            await ReloadAsync();
    }

    private void SyncAllButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        App.AppViewModel.QueueWorkSubscriptionSyncAll();
    }

    private void SyncSubscriptionButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: WorkSubscriptionItemViewModel item })
            App.AppViewModel.QueueWorkSubscriptionSync(item.Entry);
    }

    private void SubscriptionServiceOnSubscriptionUpdated(object? sender, WorkSubscriptionEntry subscription)
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

    private void ApplySubscriptionUpdate(WorkSubscriptionEntry subscription) =>
        Subscriptions.FirstOrDefault(item =>
                item.Entry.HistoryEntryId == subscription.HistoryEntryId)
            ?.UpdateSubscription(subscription);

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
        CancelAdd();
        SubscriptionService.SubscriptionUpdated -= SubscriptionServiceOnSubscriptionUpdated;
        _reloadCancellationTokenSource?.Cancel();
        _reloadCancellationTokenSource?.Dispose();
        _reloadCancellationTokenSource = null;
        base.OnUnloaded(e);
    }
}
