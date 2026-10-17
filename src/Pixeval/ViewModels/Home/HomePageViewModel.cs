// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pixeval.Config;
using Pixeval.Controls;
using Pixeval.Localization;
using Pixeval.Mako;
using Pixeval.Utilities;
using Pixeval.Views.Home;

namespace Pixeval.ViewModels.Home;

public partial class HomePageViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly HomeCardDefinitions _definitions;
    private readonly IUserSessionService _session;

    public HomeCardDefinitions Definitions => _definitions;

    public HomePageViewModel(AppSettings settings, HomeCardDefinitions definitions, IUserSessionService session)
    {
        _settings = settings;
        _definitions = definitions;
        _session = session;
        _sourceParameterEditors =
        [
            _sourceWorkTypeEditor,
            _sourceSimpleWorkTypeEditor,
            _sourcePrivacyPolicyEditor,
            _sourceRankOptionEditor,
            _sourceRankingDateEditor,
            _sourceUserIdEditor,
            _sourceEntryIdEditor,
            _sourceSeriesIdEditor,
            _sourceSearchTextEditor,
            _sourceTagEditor
        ];
        _sourceSimpleWorkTypeEditor.ValueChanged += SourceSimpleWorkTypeEditor_OnValueChanged;
        UpdateRankOptionEditor();
        SyncGridEditorValues();
        SyncSelectedCardEditorValues();
    }

    private readonly IReadOnlyList<HomeCardParameterEditorViewModel> _sourceParameterEditors;

    private readonly HomeCardChoiceParameterEditorViewModel _sourceWorkTypeEditor = new(
        HomeCardParameterKinds.WorkKind,
        nameof(Strings.HomePageSourceWorkTypeTextBlockText),
        SymbolComboBoxItem.GetValues<WorkKind>(),
        WorkKind.Illustration);

    private readonly HomeCardChoiceParameterEditorViewModel _sourceSimpleWorkTypeEditor = new(
        HomeCardParameterKinds.SimpleWorkKind,
        nameof(Strings.HomePageSourceSimpleWorkTypeTextBlockText),
        SymbolComboBoxItem.GetValues<SimpleWorkKind>(),
        SimpleWorkKind.Illustration);

    private readonly HomeCardChoiceParameterEditorViewModel _sourcePrivacyPolicyEditor = new(
        HomeCardParameterKinds.PrivacyRestrict,
        nameof(Strings.HomePageSourcePrivacyPolicyTextBlockText),
        SymbolComboBoxItem.GetValues<PrivacyRestrict>(),
        PrivacyRestrict.Public);

    private readonly HomeCardChoiceParameterEditorViewModel _sourceRankOptionEditor = new(
        HomeCardParameterKinds.RankMode,
        nameof(Strings.HomePageSourceRankOptionTextBlockText),
        SymbolComboBoxItem.GetValues<RankMode>(SimpleWorkKind.Illustration),
        RankMode.Day);

    private readonly HomeCardRankingDateParameterEditorViewModel _sourceRankingDateEditor = new(
        nameof(Strings.HomePageSourceRankingDateTextBlockText),
        MakoClient.RankingMaxDateTime.LocalDateTime);

    private readonly HomeCardTextParameterEditorViewModel _sourceUserIdEditor = new(
        HomeCardParameterKinds.UserId,
        nameof(Strings.HomePageSourceUserIdTextBlockText));

    private readonly HomeCardTextParameterEditorViewModel _sourceEntryIdEditor = new(
        HomeCardParameterKinds.EntryId,
        nameof(Strings.HomePageSourceEntryIdTextBlockText));

    private readonly HomeCardTextParameterEditorViewModel _sourceSeriesIdEditor = new(
        HomeCardParameterKinds.SeriesId,
        nameof(Strings.HomePageSourceSeriesIdTextBlockText));

    private readonly HomeCardTextParameterEditorViewModel _sourceSearchTextEditor = new(
        HomeCardParameterKinds.SearchText,
        nameof(Strings.HomePageSourceSearchTextTextBlockText));

    private readonly HomeCardTextParameterEditorViewModel _sourceTagEditor = new(
        HomeCardParameterKinds.Tag,
        nameof(Strings.HomePageSourceTagTextBlockText));

    private HomePageCardLayout? _selectedCard;

    public IReadOnlyList<HomeCardDefinition> CardTemplates => _definitions.All;

    [ObservableProperty] public partial bool IsEditMode { get; set; }

    public int RowCount => decimal.ToInt32(decimal.Clamp(_settings.ApplicationSettings.HomePage.HomePageRows, HomePage.MinimumGridSize, HomePage.MaximumGridSize));

    public int ColumnCount => decimal.ToInt32(decimal.Clamp(_settings.ApplicationSettings.HomePage.HomePageColumns, HomePage.MinimumGridSize, HomePage.MaximumGridSize));

    [ObservableProperty] public partial decimal GridColumnsValue { get; set; }

    [ObservableProperty] public partial decimal GridRowsValue { get; set; }

    public bool HasSelectedCard => _selectedCard is not null;

    public string SelectedCardDescription => _selectedCard is { } card
        ? _definitions.BuildTitle(card)
        : Strings.HomePageNoSelectedCardTextBlockText();

    [ObservableProperty] public partial decimal SelectedColumnValue { get; set; } = 1;

    [ObservableProperty] public partial decimal SelectedRowValue { get; set; } = 1;

    [ObservableProperty] public partial decimal SelectedWidthValue { get; set; } = 1;

    [ObservableProperty] public partial decimal SelectedHeightValue { get; set; } = 1;

    [ObservableProperty] public partial Color SelectedBackgroundColor { get; set; } = Color.FromUInt32(0);

    public HomeCardDefinition? PendingTemplate { get; private set; }

    public bool IsAddingConfiguredCard { get; private set; }

    public string SourceParameterTitle =>
        PendingTemplate?.Title ?? Strings.HomePageSourceParametersTitleTextBlockText();

    public string SourceParameterDescription =>
        PendingTemplate?.Description ?? Strings.HomePageSelectCardSourcePromptTextBlockText();

    public bool CanAddConfiguredCard => PendingTemplate is not null && !IsAddingConfiguredCard;

    [ObservableProperty]
    public partial IReadOnlyList<HomeCardParameterEditorViewModel> ActiveParameterEditors { get; private set; } = [];

    public void NotifyGridSizeChanged()
    {
        OnPropertyChanged(nameof(RowCount));
        OnPropertyChanged(nameof(ColumnCount));
    }

    public void SyncGridEditorValues()
    {
        GridRowsValue = RowCount;
        GridColumnsValue = ColumnCount;
    }

    public void SetSelectedCard(HomePageCardLayout? card)
    {
        if (_selectedCard == card)
            return;

        _selectedCard = card;
        OnPropertyChanged(nameof(HasSelectedCard));
        OnPropertyChanged(nameof(SelectedCardDescription));
        SyncSelectedCardEditorValues();
    }

    public void SyncSelectedCardEditorValues()
    {
        var card = _selectedCard;
        SelectedColumnValue = (card?.Column ?? 0) + 1;
        SelectedRowValue = (card?.Row ?? 0) + 1;
        SelectedWidthValue = card?.ColumnSpan ?? 1;
        SelectedHeightValue = card?.RowSpan ?? 1;
        SelectedBackgroundColor = Color.FromUInt32(card?.BackgroundColor ?? 0);
    }

    [RelayCommand]
    public void SelectTemplate(HomeCardDefinition template)
    {
        PendingTemplate = template;
        _sourceWorkTypeEditor.Value = template.WorkKind;
        _sourceSimpleWorkTypeEditor.Value = template.SimpleWorkKind;
        UpdateRankOptionEditor();
        _sourcePrivacyPolicyEditor.Value = template.PrivacyRestrict;
        _sourceRankingDateEditor.Reset(MakoClient.RankingMaxDateTime.LocalDateTime);

        var currentUserId = _session.CurrentUserId;
        _sourceUserIdEditor.Text = template.UseCurrentUserAsDefault ? currentUserId.ToString() : "";
        _sourceEntryIdEditor.Text = "";
        _sourceSeriesIdEditor.Text = "";
        if (!template.HasParameter(HomeCardParameterKinds.SearchText))
            _sourceSearchTextEditor.Text = "";
        if (!template.HasParameter(HomeCardParameterKinds.Tag))
            _sourceTagEditor.Text = "";

        ActiveParameterEditors = [.. _sourceParameterEditors.Where(editor => template.HasParameter(editor.Kind))];
        NotifySourceParameterStateChanged();
    }

    public void SetAddingConfiguredCard(bool isAddingConfiguredCard)
    {
        if (IsAddingConfiguredCard == isAddingConfiguredCard)
            return;

        IsAddingConfiguredCard = isAddingConfiguredCard;
        OnPropertyChanged(nameof(IsAddingConfiguredCard));
        OnPropertyChanged(nameof(CanAddConfiguredCard));
    }

    public bool TryCreateCardFromSourceParameters(out HomePageCardLayout? card)
    {
        card = null;
        if (PendingTemplate is not { } template)
            return false;

        var userId = 0L;
        var entryId = 0L;
        var seriesId = 0L;
        if (template.HasParameter(HomeCardParameterKinds.UserId) && !TryReadUInt64(_sourceUserIdEditor.Text, out userId))
            return false;

        if (template.HasParameter(HomeCardParameterKinds.EntryId) && !TryReadUInt64(_sourceEntryIdEditor.Text, out entryId))
            return false;

        if (template.HasParameter(HomeCardParameterKinds.SeriesId) && !TryReadUInt64(_sourceSeriesIdEditor.Text, out seriesId))
            return false;

        if (template.HasParameter(HomeCardParameterKinds.SearchText) && string.IsNullOrWhiteSpace(_sourceSearchTextEditor.Text))
            return false;

        var useSpecifiedRankingDate = _sourceRankingDateEditor.UseSpecifiedDate;
        var rawRankingDate = useSpecifiedRankingDate
            ? new DateTimeOffset(_sourceRankingDateEditor.SelectedDate).ToString("o")
            : null;

        card = new HomePageCardLayout(
            template.SourceKind,
            StoredEnum.ToIndex(_sourceWorkTypeEditor.GetValue<WorkKind>()),
            StoredEnum.ToIndex(_sourceSimpleWorkTypeEditor.GetValue<SimpleWorkKind>()),
            StoredEnum.ToIndex(_sourcePrivacyPolicyEditor.GetValue<PrivacyRestrict>()),
            StoredEnum.ToIndex(_sourceRankOptionEditor.GetValue<RankMode>()),
            userId,
            entryId,
            seriesId,
            string.IsNullOrWhiteSpace(_sourceSearchTextEditor.Text) ? null : _sourceSearchTextEditor.Text.Trim(),
            string.IsNullOrWhiteSpace(_sourceTagEditor.Text) ? null : _sourceTagEditor.Text.Trim(),
            0,
            useSpecifiedRankingDate,
            rawRankingDate,
            0,
            0,
            1,
            1);
        return true;
    }

    private void NotifySourceParameterStateChanged()
    {
        OnPropertyChanged(nameof(PendingTemplate));
        OnPropertyChanged(nameof(SourceParameterTitle));
        OnPropertyChanged(nameof(SourceParameterDescription));
        OnPropertyChanged(nameof(CanAddConfiguredCard));
    }

    private void SourceSimpleWorkTypeEditor_OnValueChanged(object? sender, EventArgs e) => UpdateRankOptionEditor();

    private void UpdateRankOptionEditor()
    {
        var simpleWorkType = _sourceSimpleWorkTypeEditor.GetValue<SimpleWorkKind>();
        var rankOption = simpleWorkType is SimpleWorkKind.Illustration
            ? _settings.SearchSettings.RankOptions.IllustrationRankOption
            : _settings.SearchSettings.RankOptions.NovelRankOption;
        _sourceRankOptionEditor.Reset(SymbolComboBoxItem.GetValues<RankMode>(simpleWorkType), rankOption);
    }

    private static bool TryReadUInt64(string? text, out long value) =>
        long.TryParse(text, out value) && value > 0;

}
