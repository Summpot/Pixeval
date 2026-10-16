// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
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

    private readonly bool _loadPreview;
    private readonly IDisposable _languageSubscription;
    public static readonly DirectProperty<HomePageCardControl, string> CardTitleProperty =
        AvaloniaProperty.RegisterDirect<HomePageCardControl, string>(nameof(CardTitle), static c => c.CardTitle);
    private PointerEditState? _pointerEditState;
    private bool _isDisposed;

    public HomePageCardControl(
        HomePageCardLayout card,
        int rowCount,
        int columnCount,
        HomeCardDefinitions definitions) : this(card, rowCount, columnCount, definitions, true)
    {
    }

    private HomePageCardControl(
        HomePageCardLayout card,
        int rowCount,
        int columnCount,
        HomeCardDefinitions definitions,
        bool loadPreview)
    {
        Card = card;
        Definition = definitions.Get(card.SourceKind);
        RowCount = rowCount;
        ColumnCount = columnCount;
        PreviewViewModel = new(card, Definition.CreatePreviewSourceAsync);
        _loadPreview = loadPreview;
        InitializeComponent();
        _languageSubscription = AppLocalization.Instance.Subscribe(() => RaisePropertyChanged(CardTitleProperty, "", CardTitle));
        UpdateBackground();
    }

    public HomePageCardControl()
    {
        Card = new();
        Definition = DesignDefinition;
        RowCount = 1;
        ColumnCount = 1;
        PreviewViewModel = new(Card, Definition.CreatePreviewSourceAsync);
        _loadPreview = false;
        InitializeComponent();
    }

    private static HomeCardDefinition DesignDefinition { get; } = new(
        HomePageCardSourceKind.WorkRecommended,
        static _ => Task.FromResult(new HomeCardPreviewSource(new UserViewViewModel())),
        static (_, _) => { });

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

    private void QuickDeleteButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DeleteRequested?.Invoke(this, new(Card));
        e.Handled = true;
    }

    [RelayCommand]
    private void OpenPreviewItem(object? parameter)
    {
        if (parameter is not Control control
            || control.AppNavigation is not { } navigation)
            return;

        HomeCardDefinitions.OpenPreviewItem(navigation, control.LauncherService, control.DataContext, PreviewViewModel.ViewModel);
    }

    [RelayCommand]
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
        _languageSubscription.Dispose();
        CompleteEdit(false);
        CardSelected = null;
        EditPreview = null;
        EditCompleted = null;
        DeleteRequested = null;
        PreviewViewModel.Dispose();
    }
}
