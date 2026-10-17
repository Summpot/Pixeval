// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Interactivity;
using Pixeval.Config;
using Pixeval.Controls;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Works;

public partial class WorkFollowingPage : IconContentPage
{
    private readonly MakoClient _client;

    public WorkFollowingPage(AppSettings settings, MakoClient client)
        : this(settings.SearchSettings.DefaultSimpleWorkType, PrivacyRestrict.Public, client)
    {
    }

    public WorkFollowingPage(SimpleWorkKind simpleWorkType, PrivacyRestrict privacyPolicy, MakoClient client, IWorkViewViewModel? viewModel = null)
    {
        _client = client;
        InitializeComponent();
        SimpleWorkTypeComboBox.SelectedValue = simpleWorkType;
        PrivacyPolicyComboBox.SelectedValue = privacyPolicy;
        if (viewModel is not null)
            WorkContainer.SetViewModel(viewModel);
        else
            ChangeSource();
    }

    private void WorkTypeComboBox_OnSelectionChanged(SymbolComboBox sender, EventArgs e)
    {
        ChangeSource();
    }

    private void WorkContainer_OnRefreshRequested(object? sender, RoutedEventArgs e)
    {
        ChangeSource();
    }

    private void ChangeSource()
    {
        var workType = SimpleWorkTypeComboBox.GetSelectedValue<SimpleWorkKind>();
        var privacy = PrivacyPolicyComboBox.GetSelectedValue<PrivacyRestrict>();
        var engine = _client.WorkFollowingUnified(workType, privacy);
        WorkContainer.ResetSource(engine.AsObjects(), workType);
    }
}
