using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Mako.Global.Enum;
using Pixeval.AppManagement;
using Pixeval.Controls;

namespace Pixeval.Views.Capability;

public partial class RecentWorkPostsPage : UserControl
{
    private bool _suppressModeSelectionChanged;

    public RecentWorkPostsPage()
    {
        InitializeComponent();

        AddHandler(Frame.NavigatedToEvent, (sender, e) =>
        {
            SyncFromCurrentWorkType();
            ChangeSource();
        });

        AttachedToVisualTree += (_, _) => App.AppViewModel.CurrentWorkTypeChanged += AppViewModelOnCurrentWorkTypeChanged;
        DetachedFromVisualTree += (_, _) => App.AppViewModel.CurrentWorkTypeChanged -= AppViewModelOnCurrentWorkTypeChanged;
    }

    private void WorkTypeComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        if (sender == SimpleWorkTypeComboBox)
        {
            var selected = SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>();
            App.AppViewModel.SetCurrentWorkType(selected.ToWorkType(App.AppViewModel.CurrentWorkType));

            if (_suppressModeSelectionChanged)
                return;
        }

        ChangeSource();
    }

    private void WorkContainer_OnRefreshRequested(object? sender, RoutedEventArgs e)
    {
        ChangeSource();
    }

    private void ChangeSource()
    {
        WorkContainer.ResetEngine(App.AppViewModel.MakoClient.RecentWorkPosts(
            SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>(),
            PrivacyPolicyComboBox.GetSelectedValue<PrivacyPolicy>()));
    }

    private void AppViewModelOnCurrentWorkTypeChanged(object? sender, WorkType workType)
    {
        Dispatcher.UIThread.Post(() =>
        {
            SyncFromCurrentWorkType();
            ChangeSource();
        });
    }

    private void SyncFromCurrentWorkType()
    {
        _suppressModeSelectionChanged = true;
        SimpleWorkTypeComboBox.SelectedIndex = (int) App.AppViewModel.CurrentWorkType.ToSimpleWorkType();
        _suppressModeSelectionChanged = false;
    }
}
