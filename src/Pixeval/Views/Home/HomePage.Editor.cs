// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Pixeval.Config;
using Pixeval.Localization;
using Pixeval.Utilities;

namespace Pixeval.Views.Home;

public sealed partial class HomePage
{
    [RelayCommand]
    private void AddConfiguredCard()
    {
        if (ViewModel.IsAddingConfiguredCard || ViewModel.PendingTemplate is not { } template)
            return;

        ViewModel.SetAddingConfiguredCard(true);
        try
        {
            if (!ViewModel.TryCreateCardFromSourceParameters(out var draftNullable) || draftNullable is not { } draft)
            {
                ShowInvalidParameterWarning();
                return;
            }

            var width = int.Min(template.DefaultColumnSpan, ColumnCount);
            var height = int.Min(template.DefaultRowSpan, RowCount);
            if (!TryFindFreePosition(width, height, out var column, out var row)
                && !TryFindBestFittingFreePosition(width, height, out width, out height, out column, out row))
            {
                this.Notifications.ShowWarning(
                    Strings.HomePageNoSpaceWarningTitle(),
                    Strings.HomePageNoSpaceWarningContent());
                return;
            }

            var card = draft with
            {
                Column = column,
                Row = row,
                ColumnSpan = width,
                RowSpan = height
            };
            _cards.Add(card);
            SelectCard(card);
            SaveLayout();
            AddCardControl(card);
            RefreshSelectionVisuals();
        }
        finally
        {
            ViewModel.SetAddingConfiguredCard(false);
        }
    }

    [RelayCommand]
    private void DeleteSelectedCard()
    {
        if (_selectedCard is { } card)
            DeleteCard(card);
    }

    private void ApplyGridSizeFromViewModel()
    {
        var rows = DecimalToPositiveInt(decimal.Clamp(ViewModel.GridRowsValue, MinimumGridSize, MaximumGridSize));
        var columns = DecimalToPositiveInt(decimal.Clamp(ViewModel.GridColumnsValue, MinimumGridSize, MaximumGridSize));
        var settings = this.GetRequiredUiService<AppSettings>().ApplicationSettings.HomePage;
        if (settings.HomePageRows == rows && settings.HomePageColumns == columns)
            return;

        if (!CanResizeGrid(rows, columns))
        {
            UpdateGridSizeControls();
            this.Notifications.ShowWarning(
                Strings.HomePageGridShrinkBlockedWarningTitle(),
                Strings.HomePageGridShrinkBlockedWarningContent());
            return;
        }

        settings.HomePageRows = rows;
        settings.HomePageColumns = columns;
        SaveLayout();
        ViewModel.NotifyGridSizeChanged();
        RefreshGridSize();
        UpdateSelectedCardControls();
        UpdateGridSizeControls();
    }

    private void ApplySelectedCardLayoutFromViewModel()
    {
        if (_selectedCard is not { } selectedCard)
            return;

        var newColumn = DecimalToZeroBasedInt(ViewModel.SelectedColumnValue);
        var newRow = DecimalToZeroBasedInt(ViewModel.SelectedRowValue);
        var newColumnSpan = DecimalToPositiveInt(ViewModel.SelectedWidthValue);
        var newRowSpan = DecimalToPositiveInt(ViewModel.SelectedHeightValue);

        if (newColumn + newColumnSpan > ColumnCount)
            newColumnSpan = ColumnCount - newColumn;
        if (newRow + newRowSpan > RowCount)
            newRowSpan = RowCount - newRow;

        if (newColumnSpan < 1 || newRowSpan < 1
                              || !CanPlace(selectedCard, newColumn, newRow, newColumnSpan, newRowSpan))
        {
            UpdateSelectedCardControls();
            this.Notifications.ShowWarning(
                Strings.HomePageLayoutConflictWarningTitle(),
                Strings.HomePageLayoutConflictWarningContent());
            return;
        }

        var index = _cards.IndexOf(selectedCard);
        var updated = selectedCard with
        {
            Column = newColumn,
            Row = newRow,
            ColumnSpan = newColumnSpan,
            RowSpan = newRowSpan
        };
        if (TryGetCardControl(selectedCard, out var control))
        {
            control.UpdateCard(updated);
            ApplyCardLayout(control);
        }
        if (index >= 0)
        {
            _cards[index] = updated;
        }
        _selectedCard = updated;
        SaveLayout();
        UpdateSelectedCardControls();
    }

    private void ApplySelectedCardBackgroundFromViewModel()
    {
        if (_selectedCard is not { } selectedCard)
            return;

        var color = ViewModel.SelectedBackgroundColor.ToUInt32();
        if (selectedCard.BackgroundColor == color)
            return;

        var index = _cards.IndexOf(selectedCard);
        var updated = selectedCard with { BackgroundColor = color };
        if (TryGetCardControl(selectedCard, out var control))
        {
            control.UpdateCard(updated);
            control.UpdateBackground();
        }
        if (index >= 0)
        {
            _cards[index] = updated;
        }
        _selectedCard = updated;
        SaveLayout();
    }

    private void HomeCardControl_OnCardSelected(object? sender, HomeCardSelectedEventArgs e)
    {
        SelectCard(e.Card);
        if (sender is HomePageCardControl cardControl)
            _activeCardControl = cardControl;
    }

    private void HomeCardControl_OnEditPreview(object? sender, HomeCardEditPreviewEventArgs e)
    {
        e.Accepted = CanPlace(e.Card, e.Bounds);
    }

    private void HomeCardControl_OnEditCompleted(object? sender, HomeCardEditCompletedEventArgs e)
    {
        if (sender is HomePageCardControl cardControl && ReferenceEquals(_activeCardControl, cardControl))
            _activeCardControl = null;

        if (e.HasChanged)
        {
            var index = _cards.IndexOf(e.OriginalCard);
            if (index >= 0)
                _cards[index] = e.Card;
            if (_selectedCard == e.OriginalCard)
                _selectedCard = e.Card;

            SaveLayout();
        }

        RefreshSelectionVisuals();
        UpdateSelectedCardControls();
    }

    private void HomeCardControl_OnDeleteRequested(object? sender, HomeCardDeleteRequestedEventArgs e) => DeleteCard(e.Card);

    private void DeleteCard(HomePageCardLayout card)
    {
        if (!_cards.Remove(card))
            return;

        if (_activeCardControl?.Card == card)
            _activeCardControl = null;

        RemoveCardControl(card);
        if (_selectedCard == card)
            SelectCard(null);

        SaveLayout();
    }

    private void SelectCard(HomePageCardLayout? card)
    {
        _selectedCard = card;
        _isUpdatingSelectedCardControls = true;
        try
        {
            ViewModel.SetSelectedCard(card);
        }
        finally
        {
            _isUpdatingSelectedCardControls = false;
        }

        if (!_isRefreshingGrid)
            RefreshSelectionVisuals();
    }

    private void UpdateSelectedCardControls()
    {
        _isUpdatingSelectedCardControls = true;
        try
        {
            ViewModel.SyncSelectedCardEditorValues();
        }
        finally
        {
            _isUpdatingSelectedCardControls = false;
        }
    }

    private void UpdateGridSizeControls()
    {
        _isUpdatingGridSizeControls = true;
        try
        {
            ViewModel.SyncGridEditorValues();
        }
        finally
        {
            _isUpdatingGridSizeControls = false;
        }
    }

    private void ShowInvalidParameterWarning()
    {
        this.Notifications.ShowWarning(
            Strings.HomePageInvalidSourceParameterWarningTitle(),
            Strings.HomePageInvalidSourceParameterWarningContent());
    }

    private static int DecimalToZeroBasedInt(decimal? value) => int.Max(0, DecimalToPositiveInt(value) - 1);

    private static int DecimalToPositiveInt(decimal? value) => int.Max(1, (int) (value ?? 1));

    private void SaveLayout()
    {
        AppInfo.SaveAppSettings(this.GetRequiredUiService<AppSettings>());
        AppInfo.SaveHomePageCards(this.GetRequiredUiService<HomePageCardSession>().Cards);
    }
}
