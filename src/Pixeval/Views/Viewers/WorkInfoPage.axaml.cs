// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Avalonia;
using Avalonia.Controls.Templates;
namespace Pixeval.Views.Viewers;

public partial class WorkInfoPage : IconContentPage
{
    public static readonly StyledProperty<object?> ActionZoneProperty =
        AvaloniaProperty.Register<WorkInfoPage, object?>(nameof(ActionZone));

    public static readonly StyledProperty<IDataTemplate> ActionZoneTemplateProperty =
        AvaloniaProperty.Register<WorkInfoPage, IDataTemplate>(nameof(ActionZoneTemplate));

    public object? ActionZone
    {
        get => GetValue(ActionZoneProperty);
        set => SetValue(ActionZoneProperty, value);
    }

    public IDataTemplate ActionZoneTemplate
    {
        get => GetValue(ActionZoneTemplateProperty);
        set => SetValue(ActionZoneTemplateProperty, value);
    }

    public WorkInfoPage() : this(null)
    {
    }

    public WorkInfoPage(object? viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
