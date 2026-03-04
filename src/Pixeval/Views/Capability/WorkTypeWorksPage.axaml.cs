using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Mako;
using Mako.Engine;
using Mako.Global.Enum;
using Mako.Model;
using Pixeval.Controls;
using Frame = FluentAvalonia.UI.Controls.Frame;

namespace Pixeval.Views.Capability;

public abstract partial class WorkTypeWorksPage : UserControl
{
    private bool _suppressSelectionChanged;

    public WorkTypeWorksPage()
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
        if (sender.SelectedValue is WorkType workType)
            App.AppViewModel.SetCurrentWorkType(workType);

        if (_suppressSelectionChanged)
            return;

        ChangeSource();
    }

    private void WorkContainer_OnRefreshRequested(object? sender, RoutedEventArgs e)
    {
        ChangeSource();
    }

    private void ChangeSource()
    {
        WorkContainer.ResetEngine(GetFetchEngine(App.AppViewModel.MakoClient, WorkTypeComboBox.GetSelectedValue<WorkType>()));
    }

    private void AppViewModelOnCurrentWorkTypeChanged(object? sender, WorkType workType)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _suppressSelectionChanged = true;
            WorkTypeComboBox.SelectedIndex = (int) workType;
            _suppressSelectionChanged = false;
            ChangeSource();
        });
    }

    private void SyncFromCurrentWorkType()
    {
        _suppressSelectionChanged = true;
        WorkTypeComboBox.SelectedIndex = (int) App.AppViewModel.CurrentWorkType;
        _suppressSelectionChanged = false;
    }

    protected abstract IFetchEngine<IWorkEntry> GetFetchEngine(MakoClient makoClient, WorkType workType);
}

public class RecommendWorksPage : WorkTypeWorksPage
{
    protected override IFetchEngine<IWorkEntry> GetFetchEngine(MakoClient makoClient, WorkType workType)
    {
        return makoClient.RecommendedWorks(workType, PixevalSettings.TargetFilter);
    }
}

public class NewWorksPage : WorkTypeWorksPage
{
    protected override IFetchEngine<IWorkEntry> GetFetchEngine(MakoClient makoClient, WorkType workType)
    {
        return makoClient.NewWorks(workType, PixevalSettings.TargetFilter);
    }
}

public class UserWorkPostsPage : WorkTypeWorksPage
{
    private long _userId;

    public UserWorkPostsPage()
    {
        AddHandler(Frame.NavigatedToEvent, (sender, e) =>
        {
            if (e.Parameter is not long uid)
                uid = App.AppViewModel.PixivUid;

            _userId = uid;
        });
    }

    protected override IFetchEngine<IWorkEntry> GetFetchEngine(MakoClient makoClient, WorkType workType)
    {
        return makoClient.WorkPosts(_userId, workType, PixevalSettings.TargetFilter);
    }
}
