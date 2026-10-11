// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Config;
using Pixeval.Utilities;
using Pixeval.ViewModels.Home;

namespace Pixeval.Views.Home;

public sealed partial class HomePageCardControl : UserControl, IDisposable
{
    public static readonly StyledProperty<bool> IsEditingProperty =
        AvaloniaProperty.Register<HomePageCardControl, bool>(nameof(IsEditing));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<HomePageCardControl, bool>(nameof(IsSelected));

    private readonly HomeCardDefinitions _definitions = App.Services!.GetRequiredService<HomeCardDefinitions>();
    private readonly bool _loadPreview;
    private PointerEditState? _pointerEditState;
    private bool _isDisposed;
    private int _openRequestVersion;

    public HomePageCardControl(
        HomePageCardLayout card,
        int rowCount,
        int columnCount) : this(card, rowCount, columnCount, true)
    {
    }

    private HomePageCardControl(
        HomePageCardLayout card,
        int rowCount,
        int columnCount,
        bool loadPreview)
    {
        Card = card;
        Definition = _definitions.Get(card.SourceKind);
        RowCount = rowCount;
        ColumnCount = columnCount;
        PreviewViewModel = new(card, Definition.CreatePreviewSourceAsync);
        _loadPreview = loadPreview;
        InitializeComponent();
        UpdateBackground();
    }

    public HomePageCardControl() : this(new(), 1, 1, false)
    {
    }

    public event EventHandler<HomeCardSelectedEventArgs>? CardSelected;

    public event EventHandler<HomeCardEditPreviewEventArgs>? EditPreview;

    public event EventHandler<HomeCardEditCompletedEventArgs>? EditCompleted;

    public event EventHandler<HomeCardDeleteRequestedEventArgs>? DeleteRequested;

    public HomePageCardLayout Card { get; internal set; }

    public void UpdateCard(HomePageCardLayout card) => Card = card;

    private HomeCardDefinition Definition { get; }

    public int RowCount { get; private set; }

    public int ColumnCount { get; private set; }

    public string CardTitle => Definition.BuildTitle(Card);

    public Symbol CardSymbol => Definition.Symbol;

    public bool IsEditing
    {
        get => GetValue(IsEditingProperty);
        set => SetValue(IsEditingProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public HomeCardPreviewViewModel PreviewViewModel { get; }

    public void CancelEdit() => CompleteEdit();

    public void UpdateGridSize(int rowCount, int columnCount)
    {
        RowCount = rowCount;
        ColumnCount = columnCount;
    }

    public void UpdateBackground()
    {
        CardBorder.Background = new SolidColorBrush(Color.FromUInt32(Card.BackgroundColor));
    }

    protected override void OnLoaded(RoutedEventArgs e)

    {

        base.OnLoaded(e);

        this.RunInteraction(() => OnLoadedAsync(e));

    }


    private async Task OnLoadedAsync(RoutedEventArgs e)
    {
        if (_loadPreview)
            await PreviewViewModel.LoadAsync();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        _openRequestVersion++;
        base.OnUnloaded(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != IsEditingProperty)
            return;
        _openRequestVersion++;
        OpenPreviewItemCommand.NotifyCanExecuteChanged();
        OpenCardPageCommand.NotifyCanExecuteChanged();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_isDisposed || !IsEditing || !ReferenceEquals(e.Source, this)
            || e.Key is not (Key.Enter or Key.Space) || e.KeyModifiers != KeyModifiers.None)
            return;
        CardSelected?.Invoke(this, new(Card));
        e.Handled = true;
    }

    private void QuickDeleteButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DeleteRequested?.Invoke(this, new(Card));
        e.Handled = true;
    }

    private bool CanOpenCard() => !_isDisposed && !IsEditing;

    [RelayCommand(CanExecute = nameof(CanOpenCard))]
    private void OpenPreviewItem(object? parameter)
    {
        if (parameter is not Control control
            || control.AppNavigation is not { } navigation)
            return;

        HomeCardDefinitions.OpenPreviewItem(navigation, control.LauncherService, control.DataContext, PreviewViewModel.ViewModel);
    }

    [RelayCommand(CanExecute = nameof(CanOpenCard))]
    private async Task OpenCardPageAsync()
    {
        await PreviewViewModel.EnsureLoadedAsync();
        var source = PreviewViewModel.Source;
        if (source is null)
            return;

        Definition.OpenCardPage(Card, source);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _openRequestVersion++;
        OpenPreviewItemCommand.NotifyCanExecuteChanged();
        OpenCardPageCommand.NotifyCanExecuteChanged();
        CompleteEdit(false);
        CardSelected = null;
        EditPreview = null;
        EditCompleted = null;
        DeleteRequested = null;
        PreviewViewModel.Dispose();
    }
}
