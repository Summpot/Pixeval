// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Models.Subscriptions;
using Pixeval.Native.Download;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Download;

public partial class DownloadFolderListView : ContentPage, IDisposable
{
    public static readonly DirectProperty<DownloadFolderListView, bool> HasNoFolderProperty =
        AvaloniaProperty.RegisterDirect<DownloadFolderListView, bool>(
            nameof(HasNoFolder),
            view => view.HasNoFolder);

    private INotifyPropertyChanged? _subscribedItemsSource;

    private bool _isDisposed;

    public bool HasNoFolder
    {
        get;
        private set => SetAndRaise(HasNoFolderProperty, ref field, value);
    } = true;

    public DownloadFolderListView() => InitializeComponent();

    public DownloadFolderListView(DownloadFolderPageViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_isDisposed)
            return;

        UpdateItemsSourceSubscription();
        RaiseEvent(new ViewModelDisposalEventArgs(ViewModelDisposal.ViewModelDisposalEvent, this));
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_isDisposed)
            return;

        UpdateItemsSourceSubscription();
    }

    private async void DownloadFolder_OnOpenRequested(DownloadFolder sender, DownloadFolderSnapshot folder)
    {
        if (DataContext is not DownloadFolderPageViewModel vm
            || !IsInNavigationPage
            || Parent is not NavigationPage frame)
            return;

        await frame.PushAsync(new DownloadItemView(new DownloadItemPageViewModel(vm.PageViewModel, folder.SubscriptionId)));
    }

    private void ResumeAll_OnClicked(object? sender, RoutedEventArgs e) =>
        ExecuteForFolder(sender, static (page, key) => page.Resume(key));

    private void PauseAll_OnClicked(object? sender, RoutedEventArgs e) =>
        ExecuteForFolder(sender, static (page, key) => page.Pause(key));

    private void CancelAll_OnClicked(object? sender, RoutedEventArgs e) =>
        ExecuteForFolder(sender, static (page, key) => page.Cancel(key));

    private void ResetAll_OnClicked(object? sender, RoutedEventArgs e) =>
        ExecuteForFolder(sender, static (page, key) => page.Reset(key));

    private static void SyncSubscription_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: DownloadFolderSnapshot folder })
            return;

        if (App.AppViewModel.StorageEngine.GetSubscriptionByHistoryId(folder.SubscriptionId) is { } subscription)
            App.AppViewModel.QueueWorkSubscriptionSync(subscription);
    }

    private static async void RemoveSubscription_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: DownloadFolderSnapshot folder })
            return;

        _ = await App.AppViewModel.AppServiceProvider
            .GetRequiredService<IWorkSubscriptionService>()
            .TryRemoveAsync(folder.SubscriptionId);
    }

    private void ExecuteForFolder(object? sender, Action<DownloadPageViewModel, DownloadTaskKey> action)
    {
        if (sender is not MenuItem { Tag: DownloadFolderSnapshot folder }
            || DataContext is not DownloadFolderPageViewModel { PageViewModel: var page })
            return;

        foreach (var item in folder.Items)
            action(page, item.Key);
    }

    private void UpdateItemsSourceSubscription()
    {
        UnsubscribeFromItemsSource();
        if (DataContext is not DownloadFolderPageViewModel vm)
            return;

        _subscribedItemsSource = vm.View;
        _subscribedItemsSource.PropertyChanged += ItemsSource_OnPropertyChanged;
        UpdateHasNoFolder();
    }

    private void ItemsSource_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ICollection.Count))
            UpdateHasNoFolder();
    }

    private void UpdateHasNoFolder() =>
        HasNoFolder = DataContext is DownloadFolderPageViewModel { View: { Count: 0 } };

    private void UnsubscribeFromItemsSource()
    {
        _subscribedItemsSource?.PropertyChanged -= ItemsSource_OnPropertyChanged;
        _subscribedItemsSource = null;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_isDisposed)
            return;

        _isDisposed = true;
        UnsubscribeFromItemsSource();
    }
}
