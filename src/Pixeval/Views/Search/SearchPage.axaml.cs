// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pixeval.Utilities;
using Pixeval.ViewModels.Search;
using Pixeval.Views.Shell;

namespace Pixeval.Views.Search;

public partial class SearchPage : IconContentPage
{
    private const string SearchTextBoxPart = "PART_TextBox";
    private const string SearchSelectingItemsControlPart = "PART_SelectingItemsControl";

    private TextBox? _searchTextBox;
    private SelectingItemsControl? _searchSuggestionItemsControl;

    public SearchPage()
    {
        InitializeComponent();
    }

    private void AdvancedOptionsTabControl_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TabbedPage
            {
                CurrentPage: SauceNaoSearchPage
                {
                    DataContext: SauceNaoSearchPageViewModel viewModel
                } page
            }
            || !viewModel.IsApiKeyExisted)
            return;

        _ = KeyboardShortcut.TryExecutePaste(e, viewModel.PasteCommand, page);
    }

    private void SearchAutoCompleteBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Enter)
            return;

        if (DataContext is not SearchPageViewModel viewModel)
            return;

        if (SearchAutoCompleteBox.IsDropDownOpen && GetSelectedSearchCompletion() is { } completion)
        {
            if (viewModel.CommitCompletion(completion))
            {
                if (completion.Kind is SearchCompletionKind.Tag)
                    FocusSearchTextBoxToEnd();
                e.Handled = true;
                return;
            }
        }

        viewModel.Search(AdvancedExpander.IsExpanded);
        e.Handled = true;
    }

    private void SearchAutoCompleteBox_OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        DetachSearchSuggestionItemsControl();

        _searchTextBox =
            e.NameScope.Find<TextBox>(SearchTextBoxPart)
            ?? SearchAutoCompleteBox.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
        _searchSuggestionItemsControl = e.NameScope.Find<SelectingItemsControl>(SearchSelectingItemsControlPart);
        if (_searchSuggestionItemsControl is not null)
        {
            _searchSuggestionItemsControl.SelectionChanged += SearchSuggestionItemsControl_OnSelectionChanged;
            _searchSuggestionItemsControl.AddHandler(
                InputElement.PointerPressedEvent,
                SearchSuggestionItemsControl_OnPointerPressed,
                RoutingStrategies.Bubble,
                handledEventsToo: true);
        }
    }

    private void DetachSearchSuggestionItemsControl()
    {
        if (_searchSuggestionItemsControl is not null)
        {
            _searchSuggestionItemsControl.SelectionChanged -= SearchSuggestionItemsControl_OnSelectionChanged;
            _searchSuggestionItemsControl.RemoveHandler(
                InputElement.PointerPressedEvent,
                SearchSuggestionItemsControl_OnPointerPressed);
        }

        _searchSuggestionItemsControl = null;
    }

    private void SearchSuggestionItemsControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is SearchPageViewModel viewModel && e.AddedItems.OfType<SearchCompletionItem>().LastOrDefault() is { } completion)
            viewModel.SelectedSearchCompletion = completion;
    }

    private void SearchSuggestionItemsControl_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.Properties.IsLeftButtonPressed || e.Source is not Control source)
            return;

        var completion = source.GetSelfAndVisualAncestors()
            .OfType<Control>()
            .Select(control => control.DataContext)
            .OfType<SearchCompletionItem>()
            .FirstOrDefault();

        if (completion is not null && DataContext is SearchPageViewModel viewModel)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (viewModel.CommitCompletion(completion))
                {
                    if (completion.Kind is SearchCompletionKind.Tag)
                        FocusSearchTextBoxToEnd();
                }
            });
        }
    }

    private void SearchAutoCompleteBox_OnTapped(object? sender, TappedEventArgs e)
    {
        if (sender is AutoCompleteBox box)
            box.IsDropDownOpen = true;
    }

    private string SearchAutoCompleteBox_OnSelectItem(string? text, object item)
        => SearchAutoCompleteBox.Text ?? "";

    private SearchCompletionItem? GetSelectedSearchCompletion()
        => (DataContext as SearchPageViewModel)?.SelectedSearchCompletion
           ?? SearchAutoCompleteBox.SelectedItem as SearchCompletionItem
           ?? _searchSuggestionItemsControl?.SelectedItem as SearchCompletionItem;

    private TextBox? GetSearchTextBox()
        => _searchTextBox ??= SearchAutoCompleteBox.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();

    private void FocusSearchTextBoxToEnd()
    {
        if (GetSearchTextBox() is { } textBox)
        {
            _ = textBox.Focus();
            textBox.CaretIndex = SearchAutoCompleteBox.Text?.Length ?? 0;
            textBox.SelectionStart = textBox.CaretIndex;
            textBox.SelectionEnd = textBox.CaretIndex;
        }
    }
}
