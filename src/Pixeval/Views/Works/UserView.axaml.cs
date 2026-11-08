// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pixeval.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Works;

public partial class UserView : UserControl
{
    public UserView() => InitializeComponent();

    public void SetViewModel(UserViewViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        var oldViewModel = DataContext as IDisposable;
        DataContext = viewModel;
        oldViewModel?.Dispose();
        if (IsLoaded)
            RegisterViewModelDisposal(viewModel);
    }

    private void UserItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not UserItem { DataContext: User vm } || !ReferenceEquals(e.Source, sender))
            return;
        e.Handled = true;
        this.AppNavigation?.CreateUserPage(vm.UserId);
    }

    #region Disposal

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is UserViewViewModel viewModel)
            RegisterViewModelDisposal(viewModel);
    }

    private void RegisterViewModelDisposal(UserViewViewModel viewModel) =>
        RaiseEvent(new ViewModelDisposalEventArgs(ViewModelDisposal.ViewModelDisposalEvent, viewModel));

    #endregion
}
