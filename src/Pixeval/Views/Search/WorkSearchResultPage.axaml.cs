// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Misaki;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Models.Pixiv;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Search;

public partial class WorkSearchResultPage : IconContentPage
{
    public WorkSearchResultPage() : this("")
    {
    }

    public WorkSearchResultPage(string searchText, SimpleWorkType preferredType) : this(
        searchText,
        new IllustrationSearchArguments(searchText),
        new NovelSearchArguments(searchText),
        preferredType)
    {
    }

    public WorkSearchResultPage(IllustrationSearchArguments illustrationSearchArguments)
        : this(illustrationSearchArguments.SearchText, illustrationSearchArguments)
    {
    }

    public WorkSearchResultPage(NovelSearchArguments novelSearchArguments)
        : this(novelSearchArguments.SearchText, null, novelSearchArguments, SimpleWorkType.Novel)
    {
    }

    public WorkSearchResultPage(
        string searchText,
        IllustrationSearchArguments? illustrationSearchArguments = null,
        NovelSearchArguments? novelSearchArguments = null,
        SimpleWorkType preferredType = default,
        IWorkViewViewModel? viewModel = null)
    {
        InitializeComponent();
        _searchText = searchText;
        Header = I18NManager.GetResource(MainPageResources.SearchResultFormatted, searchText);
        SimpleWorkTypeComboBox.SelectedValue = preferredType;
        _illustrationArguments = illustrationSearchArguments;
        _novelArguments = novelSearchArguments;
        SetIsSwitchEnabled();
        if (viewModel is not null)
            WorkContainer.SetViewModel(viewModel);
        else
            ChangeSource();

        _ = LoadPopularPreviewAsync(searchText, preferredType);
    }

    private void WorkTypeComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        ChangeSource();
        _ = LoadPopularPreviewAsync(_searchText, SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>());
    }

    private async Task LoadPopularPreviewAsync(string word, SimpleWorkType type)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            PopularPreviewContainer.IsVisible = false;
            return;
        }

        try
        {
            if (type is SimpleWorkType.Novel)
            {
                var novels = await App.AppViewModel.MakoClient.PopularPreviewNovelAsync(word, null, null, null, null);
                if (novels.Count > 0)
                {
                    PopularPreviewItemsControl.ItemsSource = novels;
                    PopularPreviewContainer.IsVisible = true;
                    return;
                }
            }
            else
            {
                var illusts = await App.AppViewModel.MakoClient.PopularPreviewIllustAsync(word, null, null, null, null);
                if (illusts.Count > 0)
                {
                    PopularPreviewItemsControl.ItemsSource = illusts;
                    PopularPreviewContainer.IsVisible = true;
                    return;
                }
            }
        }
        catch
        {
            // Ignore preview failure silently
        }

        PopularPreviewContainer.IsVisible = false;
    }

    private void PopularPreviewItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Avalonia.Controls.Control { DataContext: IArtworkInfo artwork } && TopLevel.GetTopLevel(this) is { } topLevel)
        {
            if (artwork is Pixeval.Native.Mako.Novel novel)
                topLevel.ViewContainer?.CreateNovelPage(novel);
            else if (artwork is Pixeval.Native.Mako.Illustration illust)
                topLevel.ViewContainer?.CreateIllustrationPage(illust);
        }
    }

    private readonly string _searchText;

    private void ChangeSource()
    {
        IAsyncEnumerable<IArtworkInfo> engine = (_illustrationArguments, _novelArguments) switch
        {
            (null, null) => App.AppViewModel.MakoClient.Computed(AsyncEnumerable.Empty<IArtworkInfo>()),
            (_, null) => App.AppViewModel.MakoClient.IllustrationSearch(_illustrationArguments!),
            (null, _) => App.AppViewModel.MakoClient.NovelSearch(_novelArguments!),
            _ => SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkType>() switch
            {
                SimpleWorkType.Novel => App.AppViewModel.MakoClient.NovelSearch(_novelArguments!),
                _ => App.AppViewModel.MakoClient.IllustrationSearch(_illustrationArguments!),
            }
        };

        WorkContainer.ResetEngine(engine);
    }

    private readonly IllustrationSearchArguments? _illustrationArguments;

    private readonly NovelSearchArguments? _novelArguments;

    private void SetIsSwitchEnabled()
    {
        if (_illustrationArguments is null || _novelArguments is null)
            return;
        SimpleWorkTypeComboBox.IsEnabled = true;
        SimpleWorkTypeComboBox.IsVisible = true;
    }
}
