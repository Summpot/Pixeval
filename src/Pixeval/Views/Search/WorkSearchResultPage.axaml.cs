// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Pixeval.Controls;
using Pixeval.Localization;
using Pixeval.Mako;
using Pixeval.Pixiv;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Search;

public partial class WorkSearchResultPage : IconContentPage
{
    public WorkSearchResultPage() : this("")
    {
    }

    public WorkSearchResultPage(string searchText, SimpleWorkKind preferredType) : this(
        searchText,
        QueryEnums.IllustrationKeyword(searchText),
        QueryEnums.NovelKeyword(searchText),
        preferredType)
    {
    }

    public WorkSearchResultPage(IllustrationSearchParams illustrationSearchParams)
        : this(illustrationSearchParams.Word, illustrationSearchParams)
    {
    }

    public WorkSearchResultPage(NovelSearchParams novelSearchParams)
        : this(novelSearchParams.Word, null, novelSearchParams, SimpleWorkKind.Novel)
    {
    }

    public WorkSearchResultPage(
        string searchText,
        IllustrationSearchParams? illustrationSearchParams = null,
        NovelSearchParams? novelSearchParams = null,
        SimpleWorkKind preferredType = default,
        IWorkViewViewModel? viewModel = null)
    {
        InitializeComponent();
        _searchText = searchText;
        Header = Strings.MainPageSearchResultFormatted(searchText: searchText);
        var languageSubscription = AppLocalization.Instance.Subscribe(() => Header = Strings.MainPageSearchResultFormatted(searchText: searchText));
        Pixeval.ViewModels.ViewModelDisposal.Register(this, languageSubscription);
        SimpleWorkTypeComboBox.SelectedValue = preferredType;
        _illustrationParams = illustrationSearchParams;
        _novelParams = novelSearchParams;
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
        _ = LoadPopularPreviewAsync(_searchText, SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkKind>());
    }

    private async Task LoadPopularPreviewAsync(string word, SimpleWorkKind type)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            PopularPreviewContainer.IsVisible = false;
            return;
        }

        try
        {
            if (type is SimpleWorkKind.Novel)
            {
                var novels = await this.GetRequiredUiService<MakoClient>().PopularPreviewNovelAsync(word, null, null, null, null);
                if (novels.Count > 0)
                {
                    PopularPreviewItemsControl.ItemsSource = novels;
                    PopularPreviewContainer.IsVisible = true;
                    return;
                }
            }
            else
            {
                var illusts = await this.GetRequiredUiService<MakoClient>().PopularPreviewIllustAsync(word, null, null, null, null);
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
        if (sender is Avalonia.Controls.Control { DataContext: object artwork } && this.AppNavigation is { } navigation)
        {
            if (artwork is Pixeval.Mako.Novel novel)
                navigation.CreateNovelPage(novel);
            else if (artwork is Pixeval.Mako.Illustration illust)
                navigation.CreateIllustrationPage(illust);
        }
    }

    private readonly string _searchText;

    private void ChangeSource()
    {
        var workType = (_illustrationParams, _novelParams) switch
        {
            (_, null) => SimpleWorkKind.Illustration,
            (null, _) => SimpleWorkKind.Novel,
            _ => SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkKind>()
        };
        IAsyncEnumerable<object> engine = workType is SimpleWorkKind.Novel
            ? _novelParams is null
                ? AsyncEnumerable.Empty<object>()
                : this.GetRequiredUiService<MakoClient>().NovelSearchAdvanced(_novelParams.Value).AsObjects()
            : _illustrationParams is null
                ? AsyncEnumerable.Empty<object>()
                : this.GetRequiredUiService<MakoClient>().IllustrationSearchAdvanced(_illustrationParams.Value).AsObjects();

        WorkContainer.ResetSource(engine, workType);
    }

    private readonly IllustrationSearchParams? _illustrationParams;

    private readonly NovelSearchParams? _novelParams;

    private void SetIsSwitchEnabled()
    {
        if (_illustrationParams is null || _novelParams is null)
            return;
        SimpleWorkTypeComboBox.IsEnabled = true;
        SimpleWorkTypeComboBox.IsVisible = true;
    }
}
