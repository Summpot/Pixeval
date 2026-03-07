using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using Pixeval.I18N;
using Pixeval.Utilities;
using Pixeval.ViewModels.WorkDetails;

namespace Pixeval.Views.Capability;

public partial class WorkDetailsPage : UserControl
{
    private readonly WorkDetailsViewModel _viewModel = new();

    public WorkDetailsPage()
    {
        InitializeComponent();
        DataContext = _viewModel;

        AddHandler(Frame.NavigatedToEvent, (sender, e) =>
        {
            if (e.Parameter is not WorkDetailsNavigationParameter parameter)
                return;

            _ = LoadAsync(parameter);
        });
    }

    private async Task LoadAsync(WorkDetailsNavigationParameter parameter)
    {
        await _viewModel.LoadAsync(parameter).ConfigureAwait(false);
    }

    private async void OpenInBrowserButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.WebsiteUrl)
            || !Uri.TryCreate(_viewModel.WebsiteUrl, UriKind.Absolute, out var uri)
            || TopLevel.GetTopLevel(this) is not { Launcher: { } launcher })
            return;

        _ = await launcher.LaunchUriAsync(uri);
    }

    private async void CopyAppLinkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not
            {
                Clipboard: { } clipboard,
                ViewContainer: { } viewContainer
            })
            return;

        await clipboard.SetTextAsync(_viewModel.AppUrl);

        Dispatcher.UIThread.Post(() =>
            viewContainer.ShowSuccess(I18NManager.GetResource(EntryItemResources.LinkCopiedToClipboard)));
    }

    private void RelatedWorkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: RelatedWorkCardViewModel relatedWork }
            || TopLevel.GetTopLevel(this)?.ViewContainer is not { } viewContainer)
            return;

        var symbol = relatedWork.Kind switch
        {
            WorkDetailsKind.Novel => Symbol.BookNumber,
            WorkDetailsKind.Manga => Symbol.ImageMultiple,
            _ => Symbol.Image
        };

        viewContainer.NavigateTo(
            typeof(WorkDetailsPage),
            new SymbolIcon
            {
                Symbol = symbol,
                FontSize = 16,
                IconVariant = IconVariant.Color
            },
            string.IsNullOrWhiteSpace(relatedWork.Title) ? $"作品 {relatedWork.Id}" : relatedWork.Title,
            relatedWork.NavigationParameter);
    }
}
