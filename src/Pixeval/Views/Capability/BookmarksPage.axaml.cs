using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Mako.Global.Enum;
using Mako.Model;
using Pixeval.AppManagement;
using Pixeval.Controls;
using Pixeval.Utilities;
using Frame = FluentAvalonia.UI.Controls.Frame;

namespace Pixeval;

public partial class BookmarksPage : UserControl
{
    private long _userId;
    private bool _suppressChangeSource;
    private bool _suppressModeSelectionChanged;

    public static IReadOnlyList<BookmarkTag> DefaultTags { get; }= [AllBookmarkTag.Instance];

    public BookmarksPage()
    {
        InitializeComponent();

        AddHandler(Frame.NavigatedToEvent, (sender, e) =>
        {
            if (e.Parameter is not long uid)
                uid = App.AppViewModel.PixivUid;
            else if (uid != App.AppViewModel.PixivUid)
                PrivacyPolicyComboBox.IsEnabled =  PrivacyPolicyComboBox.IsVisible = false;

            _userId = uid;
            SyncFromCurrentWorkType();
            FetchTags();
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
        var tags = await MakoHelper.GetBookmarkTagsAsync(
            _userId,
            SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>(),
            PrivacyPolicyComboBox.GetSelectedValue<PrivacyPolicy>());

        _suppressChangeSource = true;
        TagComboBox.ItemsSource = tags;
        TagComboBox.SelectedIndex = 0;
        _suppressChangeSource = false;
    }

    private void ChangeSource()
    {
        var tag = (TagComboBox.SelectedItem as BookmarkTag)?.Name;
        WorkContainer.ResetEngine(App.AppViewModel.MakoClient.WorkBookmarks(
            _userId,
            SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>(),
            PrivacyPolicyComboBox.GetSelectedValue<PrivacyPolicy>(),
            tag));
    }

    private void AppViewModelOnCurrentWorkTypeChanged(object? sender, WorkType workType)
    {
        Dispatcher.UIThread.Post(() =>
        {
            SyncFromCurrentWorkType();
            FetchTags();
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
