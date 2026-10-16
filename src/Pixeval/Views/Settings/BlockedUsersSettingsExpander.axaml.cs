// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoSettingsPage.Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Avalonia.Controls;
using Pixeval.Localization;
using Pixeval.Config;
using Pixeval.Storage;
using Pixeval.Utilities;

namespace Pixeval.Views.Settings;

public partial class BlockedUsersSettingsExpander : SettingsExpander, IEntryControl<BlockedUsersSettingsEntry>
{
    private CancellationTokenSource? _reloadCancellationTokenSource;

    public ObservableCollection<BlockedUserRecord> Users { get; } = [];

    public BlockedUsersSettingsEntry Entry
    {
        set
        {
            DataContext = value;
            _ = ReloadAsync();
        }
    }

    public BlockedUsersSettingsExpander() => InitializeComponent();

    private async Task ReloadAsync()
    {
        _reloadCancellationTokenSource?.Cancel();
        _reloadCancellationTokenSource?.Dispose();
        _reloadCancellationTokenSource = new();
        var token = _reloadCancellationTokenSource.Token;
        Users.Clear();
        try
        {
            foreach (var entry in this.GetRequiredUiService<StorageEngine>().GetAllBlockedUsers())
            {
                token.ThrowIfCancellationRequested();
                Users.Add(entry);
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
        HideError();
        if (!long.TryParse(TargetIdTextBox.Text, out var userId) || userId <= 0)
        {
            ShowError(Strings.BlockedUsersSettingsExpanderInvalidUserId());
            return;
        }

        try
        {
            var user = (await this.GetRequiredUiService<MakoClient>().GetUserFromIdAsync(userId)).User;
            this.GetRequiredUiService<StorageEngine>().AddOrUpdateBlockedUser(BlockedContentModelHelper.CreateBlockedUserRecord(user));
            TargetIdTextBox.Text = "";
            await ReloadAsync();
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void RefreshButton_OnClicked(object? sender, RoutedEventArgs e) =>
        this.RunInteraction(() => RefreshButton_OnClickedAsync(sender, e));

    private async Task RefreshButton_OnClickedAsync(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: BlockedUserRecord item })
            await RefreshUserAsync(item);
    }

    private void RefreshAllButton_OnClicked(object? sender, RoutedEventArgs e) =>
        this.RunInteraction(() => RefreshAllButton_OnClickedAsync(sender, e));

    private async Task RefreshAllButton_OnClickedAsync(object? sender, RoutedEventArgs e)
    {
        HideError();
        foreach (var item in Users.ToArray())
            await RefreshUserAsync(item);
    }

    private async Task RefreshUserAsync(BlockedUserRecord item)
    {
        try
        {
            var user = (await this.GetRequiredUiService<MakoClient>().GetUserFromIdAsync(item.Id)).User;
            var entry = this.GetRequiredUiService<StorageEngine>().AddOrUpdateBlockedUser(BlockedContentModelHelper.CreateBlockedUserRecord(user));
            ReplaceUser(entry);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void ReplaceUser(BlockedUserRecord entry)
    {
        for (var i = 0; i < Users.Count; i++)
        {
            if (Users[i].Id != entry.Id)
                continue;

            Users[i] = entry;
            return;
        }
    }

    private void DeleteButton_OnClicked(object? sender, RoutedEventArgs e) =>
        this.RunInteraction(() => DeleteButton_OnClickedAsync(sender, e));

    private async Task DeleteButton_OnClickedAsync(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: BlockedUserRecord item })
            return;

        _ = this.GetRequiredUiService<StorageEngine>().TryDeleteBlockedUser(item.Id);
        await ReloadAsync();
    }

    private void ShowError(string text)
    {
        ErrorTextBlock.Text = text;
        ErrorTextBlock.IsVisible = true;
    }

    private void HideError() => ErrorTextBlock.IsVisible = false;

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        _reloadCancellationTokenSource?.Cancel();
        _reloadCancellationTokenSource?.Dispose();
        _reloadCancellationTokenSource = null;
        base.OnUnloaded(e);
    }
}
