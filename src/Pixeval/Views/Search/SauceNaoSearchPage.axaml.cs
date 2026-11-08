using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Pixeval.I18N;
using Pixeval.Utilities;
using Pixeval.ViewModels.Search;
using Pixeval.Views.Work;

namespace Pixeval.Views.Search;

public partial class SauceNaoSearchPage : ContentPage
{
    private bool _isPicking;
    private int _attachmentVersion;

    public SauceNaoSearchPage()
    {
        InitializeComponent();
    }

    private async void OpenFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_isPicking || !SauceNaoSearchPageViewModel.IsApiKeyExisted
            || DataContext is not SauceNaoSearchPageViewModel viewModel
            || TopLevel.GetTopLevel(this) is not { StorageProvider: { } provider })
            return;
        var attachmentVersion = _attachmentVersion;
        _isPicking = true;
        try
        {
            if (await provider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = [FilePickerFileTypes.ImageAll, FilePickerFileTypes.All]
            }) is not [{ } file])
                return;
            using (file)
                if (attachmentVersion == _attachmentVersion && ReferenceEquals(DataContext, viewModel))
                    await viewModel.LoadFileAsync(file);
        }
        catch (Exception)
        {
            if (attachmentVersion == _attachmentVersion)
                TopLevel.GetTopLevel(this)?.ViewContainer?.ShowError(
                    I18NManager.GetResource(MiscResources.ExceptionEncountered),
                    I18NManager.GetResource(SearchResources.SauceNAO.ImageLoadFailed));
        }
        finally
        {
            _isPicking = false;
        }
    }

    private void SearchButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!SauceNaoSearchPageViewModel.IsApiKeyExisted
            || DataContext is not SauceNaoSearchPageViewModel { CanSearch: true } viewModel
            || TopLevel.GetTopLevel(this) is not { ViewContainer: { } viewContainer })
            return;
        viewContainer.NavigateTo(new ArtworkSauceNaoSearchResultPage(SauceNaoSearchPageViewModel.ApiKey, viewModel.File));
    }

    #region Disposal

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is SauceNaoSearchPageViewModel vm)
        {
            vm.RefreshApiKeyState();
            RaiseEvent(new ViewModelDisposalEventArgs(ViewModelDisposal.ViewModelDisposalEvent, vm));
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        ++_attachmentVersion;
        (DataContext as SauceNaoSearchPageViewModel)?.CancelLoad();
        base.OnUnloaded(e);
    }

    #endregion
}
