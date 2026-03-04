using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Mako.Engine;
using Mako.Global.Enum;
using Mako.Model;
using Pixeval.AppManagement;
using Pixeval.Controls;
using Frame = FluentAvalonia.UI.Controls.Frame;

namespace Pixeval;

public partial class SearchWorksPage : UserControl
{
    private string? _searchText;
    private bool _suppressModeSelectionChanged;

    public SearchWorksPage()
    {
        InitializeComponent();
        AddHandler(Frame.NavigatedToEvent, (sender, e) =>
        {
            if (e.Parameter is not (SimpleWorkType type, string s))
                return;
            _searchText = s;
            SimpleWorkTypeComboBox.SelectedIndex = (int) type;
            App.AppViewModel.SetCurrentWorkType(type.ToWorkType(App.AppViewModel.CurrentWorkType));
            ChangeSource();
        });

        AttachedToVisualTree += (_, _) => App.AppViewModel.CurrentWorkTypeChanged += AppViewModelOnCurrentWorkTypeChanged;
        DetachedFromVisualTree += (_, _) => App.AppViewModel.CurrentWorkTypeChanged -= AppViewModelOnCurrentWorkTypeChanged;
    }

    private void WorkTypeComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        var selectedType = SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>();
        App.AppViewModel.SetCurrentWorkType(selectedType.ToWorkType(App.AppViewModel.CurrentWorkType));

        if (_suppressModeSelectionChanged)
            return;

        ChangeSource();
    }

    private void WorkContainer_OnRefreshRequested(object? sender, RoutedEventArgs e)
    {
        ChangeSource();
    }

    private void ChangeSource()
    {
        IFetchEngine<IWorkEntry> engine;
        if (_searchText is null)
            engine = App.AppViewModel.MakoClient.Computed(AsyncEnumerable.Empty<IWorkEntry>());
        else
        {
            var settings = App.AppViewModel.AppSettings;
            engine = App.AppViewModel.MakoClient.SearchWorks(
                _searchText,
                SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>(),
                settings.SearchIllustrationTagMatchOption,
                settings.SearchNovelTagMatchOption,
                settings.WorkSortOption,
                settings.UseSearchStartDate ? settings.SearchStartDate : null,
                settings.UseSearchEndDate ? settings.SearchEndDate : null,
                null,
                settings.TargetFilter);
        }

        WorkContainer.ResetEngine(engine);
    }

    private void AppViewModelOnCurrentWorkTypeChanged(object? sender, WorkType workType)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _suppressModeSelectionChanged = true;
            SimpleWorkTypeComboBox.SelectedIndex = (int) workType.ToSimpleWorkType();
            _suppressModeSelectionChanged = false;
            ChangeSource();
        });
    }
}
