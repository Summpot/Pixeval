using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pixeval.I18N;
using Pixeval.Utilities;
using Pixeval.ViewModels.Download;

namespace Pixeval.Views.Capability;

public partial class DownloadPage : UserControl
{
    private readonly DownloadPageViewModel _viewModel = new(App.AppViewModel.DownloadManager.QueuedTasks);

    public DownloadPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        DetachedFromVisualTree += (_, _) => _viewModel.Dispose();
    }

    private void TaskListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _ = e;
        if (sender is not ListBox listBox)
            return;

        _viewModel.SelectedEntries = listBox.SelectedItems is null
            ? []
            : [.. listBox.SelectedItems.Cast<DownloadItemViewModel>()];
    }

    private void SelectAllButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;

        if (TaskListBox.SelectedItems is null)
            return;

        TaskListBox.SelectedItems.Clear();
        foreach (var item in _viewModel.View)
            TaskListBox.SelectedItems.Add(item);
    }

    private void ClearSelectionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;
        TaskListBox.SelectedItems?.Clear();
        _viewModel.SelectedEntries = [];
    }

    private void ResumeSelectedButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;
        _viewModel.ResumeSelectedItems();
    }

    private void PauseSelectedButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;
        _viewModel.PauseSelectedItems();
    }

    private void CancelSelectedButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;
        _viewModel.CancelSelectedItems();
    }

    private void DeleteSelectedRecordsMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;
        DeleteSelected(deleteLocalFiles: false);
    }

    private void DeleteSelectedRecordsAndFilesMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        e.Handled = true;
        DeleteSelected(deleteLocalFiles: true);
    }

    private void DeleteSelected(bool deleteLocalFiles)
    {
        var count = _viewModel.RemoveSelectedItems(deleteLocalFiles);
        TaskListBox.SelectedItems?.Clear();

        if (count <= 0)
            return;

        TopLevel.GetTopLevel(this)?.ViewContainer?.ShowSuccess(
            I18NManager.GetResource(DownloadPageResources.DeleteDownloadHistoryRecordsFormatted, count),
            deleteLocalFiles ? "已同步删除本地文件。" : null);
    }
}
