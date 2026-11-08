using System.Threading.Tasks;
using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Pixeval.Localization;
using Pixeval.Utilities;
using Pixeval.ViewModels.Search;
using Pixeval.Views.Works;

namespace Pixeval.Views.Search;

public partial class SauceNaoSearchPage : ContentPage
{
    private bool _isPicking;
    private int _attachmentVersion;

    public SauceNaoSearchPage()
    {
        InitializeComponent();
    }

    private void OpenFileButton_OnClick(object? sender, RoutedEventArgs e) =>
        this.RunInteraction(() => OpenFileButton_OnClickAsync(sender, e));

    private async Task OpenFileButton_OnClickAsync(object? sender, RoutedEventArgs e)
    {
        if (_isPicking || DataContext is not SauceNaoSearchPageViewModel { IsApiKeyExisted: true } viewModel)
            return;
        var attachmentVersion = _attachmentVersion;
        _isPicking = true;
        try
        {
            if (await this.StoragePicker.OpenFilesAsync(new FilePickerOpenOptions
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
                    Strings.MiscExceptionEncountered(),
                    Strings.SearchSauceNAOImageLoadFailed());
        }
        finally
        {
            _isPicking = false;
        }
    }

    private void SearchButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SauceNaoSearchPageViewModel { IsApiKeyExisted: true, CanSearch: true } viewModel)
            return;

        var nav = this.GetRequiredUiService<INavigationService>();
        nav.NavigateTo<ArtworkSauceNaoSearchResultPage>((viewModel.ApiKey, viewModel.File), sourceControl: this);
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
