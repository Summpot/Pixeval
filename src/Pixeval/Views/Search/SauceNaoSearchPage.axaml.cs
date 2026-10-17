using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Pixeval.Utilities;
using Pixeval.ViewModels.Search;
using Pixeval.Views.Works;

namespace Pixeval.Views.Search;

public partial class SauceNaoSearchPage : ContentPage
{
    public SauceNaoSearchPage()
    {
        InitializeComponent();
    }

    private void OpenFileButton_OnClick(object? sender, RoutedEventArgs e) =>
        this.RunInteraction(() => OpenFileButton_OnClickAsync(sender, e));

    private async Task OpenFileButton_OnClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SauceNaoSearchPageViewModel viewModel)
            return;
        if (await this.StoragePicker.OpenFilesAsync(new FilePickerOpenOptions { AllowMultiple = false }) is not [{ } file])
            return;
        await using var stream = await file.OpenReadAsync();
        await viewModel.LoadAsync(stream);
    }

    private void SearchButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SauceNaoSearchPageViewModel viewModel)
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
            RaiseEvent(new ViewModelDisposalEventArgs(ViewModelDisposal.ViewModelDisposalEvent, vm));
    }

    #endregion
}
